using System;
using Godot;

/// <summary>
/// First-person controller: mouse look, walking, sprinting, crouching, hiding and being grabbed.
/// The body turns left and right and the head looks up and down. Like Unreal Engine's character movement, the
/// player speeds up and brakes at a steady rate, so starting, stopping and turning feel direct.
/// How all of it feels is set in <see cref="Settings"/>.
/// </summary>
public partial class Player : CharacterBody3D
{
	private const float FullHearingHz = 20000f;

	[Export] public FirstPersonSettings Settings { get; set; }

	[ExportGroup("Noise")]
	[Export] public float WalkNoiseRadius { get; set; } = 3f;
	[Export] public float SprintNoiseRadius { get; set; } = 10f;

	[ExportGroup("Footsteps")]
	[Export] public float WalkStepVolumeDb { get; set; } = -6f;
	[Export] public float SprintStepVolumeDb { get; set; } = 0f;
	[Export] public float CrouchStepVolumeDb { get; set; } = -15f;
	/// <summary>The jacket and the camcorder strap moving with each step.</summary>
	[Export] public AudioStreamPlayer ClothSound { get; set; }
	[Export] public AudioStreamPlayer CrouchSound { get; set; }
	[Export] public AudioStreamPlayer StandUpSound { get; set; }

	[ExportGroup("Being Grabbed")]
	[Export] public float GrabbedTurnSpeed { get; set; } = 8f;
	[Export] public float StaggerDeceleration { get; set; } = 6f;
	[Export] public float ChokePitchDegrees { get; set; } = 5f;
	/// <summary>Parent of the points an attacker grabs; it sways along with the head.</summary>
	[Export] public Node3D GrabPoints { get; set; }
	[Export] public Node3D LeftShoulder { get; set; }
	[Export] public Node3D RightShoulder { get; set; }
	[Export] public Node3D ThroatLeft { get; set; }
	[Export] public Node3D ThroatRight { get; set; }
	[Export] public ColorRect ChokeOverlay { get; set; }
	/// <summary>Ringing in the ears, as loud as the player is close to passing out.</summary>
	[Export] public AudioStreamPlayer EarRing { get; set; }
	/// <summary>The low-pass cutoff that muffles the world's sounds just before the player passes out.</summary>
	[Export] public float PassingOutHearingHz { get; set; } = 400f;

	[ExportGroup("Hiding")]
	/// <summary>The low-pass cutoff of the world's sounds heard from inside a hiding spot.</summary>
	[Export] public float HidingHearingHz { get; set; } = 2500f;
	[Export] public float HidingMuffleSeconds { get; set; } = 0.5f;

	[ExportGroup("Nodes")]
	/// <summary>Sits at eye height and holds the up and down look; the camera follows it.</summary>
	[Export] public Node3D Head { get; set; }
	[Export] public FirstPersonCamera Camera { get; set; }
	[Export] public CollisionShape3D StandingCollision { get; set; }
	[Export] public CollisionShape3D CrouchingCollision { get; set; }
	[Export] public RayCast3D CeilingCheck { get; set; }
	[Export] public Camcorder Camcorder { get; set; }
	[Export] public DeathScreen DeathScreen { get; set; }
	[Export] public ViewHands Hands { get; set; }
	[Export] public Footsteps Footsteps { get; set; }

	public bool IsCrouching { get; private set; }
	public bool IsSprinting => !IsCrouching && HorizontalSpeed > Settings.WalkSpeed + 0.5f;
	public bool IsWalking => !IsGrabbed && IsOnFloor() && HorizontalSpeed > 0.1f;
	public IInteractable HidingSpot { get; private set; }
	public bool IsHiding => HidingSpot is not null;
	public bool IsGrabbed => _grabLookTarget is not null;

	/// <summary>
	/// True while something else, like a locker, is moving the player. They can't walk, look or interact.
	/// </summary>
	public bool AreControlsLocked { get; private set; }

	/// <summary>
	/// Raised when an attacker grabs the player, so whatever was moving them can stop.
	/// </summary>
	public event Action Grabbed;

	/// <summary>
	/// How far an attacker has pushed or pulled the player's upper body, in the player's own space.
	/// </summary>
	public Vector3 BodySway { get; set; }

	/// <summary>
	/// How far into the current step the player is, from 0 as a foot lands to 1 as the next one does.
	/// </summary>
	public float StepProgress => _strideDistance / Settings.StrideLength;

	/// <summary>
	/// How close to passing out the player is, from 0 to 1. Darkens the view and shakes it harder.
	/// </summary>
	public float ChokeAmount
	{
		get => _chokeAmount;
		set
		{
			_chokeAmount = value;
			_chokeMaterial.SetShaderParameter("amount", value);
			EarRing.VolumeLinear = _earRingVolume * value;
		}
	}

	/// <summary>
	/// How far away enemies can hear the player's footsteps.
	/// </summary>
	public float NoiseRadius
	{
		get
		{
			if (IsHiding || IsCrouching || HorizontalSpeed < 1f)
				return 0f;

			return IsSprinting ? SprintNoiseRadius : WalkNoiseRadius;
		}
	}

	public float HorizontalSpeed => new Vector2(Velocity.X, Velocity.Z).Length();

	private float CurrentSpeed => IsCrouching ? Settings.CrouchSpeed
		: Input.IsActionPressed("sprint") ? Settings.SprintSpeed
		: Settings.WalkSpeed;

	private uint _collisionLayer;
	private uint _collisionMask;
	private Transform3D _previousTransform;
	private float _previousPitch;
	private Transform3D _hidingExitPoint;
	private float _eyeHeight;
	private float _strideDistance;
	private Node3D _grabLookTarget;
	private Vector3 _staggerVelocity;
	private float _chokeAmount;
	private ShaderMaterial _chokeMaterial;
	private AudioEffectLowPassFilter _worldMuffle;
	private float _hidden;
	private float _earRingVolume;

	public override void _Ready()
	{
		_collisionLayer = CollisionLayer;
		_collisionMask = CollisionMask;
		_eyeHeight = Settings.EyeHeight;
		_strideDistance = Settings.StrideLength * 0.5f;
		_previousTransform = GlobalTransform;
		_chokeMaterial = (ShaderMaterial)ChokeOverlay.Material;
		_worldMuffle = (AudioEffectLowPassFilter)AudioServer.GetBusEffect(AudioServer.GetBusIndex("World"), 0);
		_earRingVolume = EarRing.VolumeLinear;
		ChokeAmount = 0f;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsGrabbed || AreControlsLocked)
			return;

		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
			Look(motion.ScreenRelative);
		else if (@event.IsActionPressed("ui_cancel"))
			Input.MouseMode = Input.MouseModeEnum.Visible;
		else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		UpdateHeadPosition(dt);
		if (IsGrabbed)
			UpdateGrabbedView(dt);
		Camera.UpdateView(dt);
		UpdateHearing(dt);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		// Scripted moves like a locker's run after this, so the last step's movement is measured here.
		Vector3 moved = GlobalPosition - _previousTransform.Origin;
		_previousTransform = GlobalTransform;
		_previousPitch = Head.Rotation.X;

		if (IsGrabbed)
			Stagger(dt);
		else if (IsHiding || AreControlsLocked)
			// Something else moves the player now; the velocity still drives the footsteps and the head bob.
			Velocity = moved / dt;
		else
		{
			UpdateCrouch();
			Move(dt);
		}

		UpdateStride(moved);
	}

	/// <summary>
	/// Where the body is this frame. It only moves in physics steps, so between two steps its position is blended
	/// from where it was after the previous one; that keeps the camera smooth at any frame rate. The mouse turns the
	/// body every frame, so the turn is only blended while a scripted move like a locker's is turning it.
	/// </summary>
	public Transform3D GetInterpolatedTransform()
	{
		float weight = (float)Engine.GetPhysicsInterpolationFraction();
		Vector3 position = _previousTransform.Origin.Lerp(GlobalPosition, weight);
		float yaw = AreControlsLocked
			? Mathf.LerpAngle(_previousTransform.Basis.GetEuler().Y, Rotation.Y, weight)
			: Rotation.Y;
		return new Transform3D(new Basis(Vector3.Up, yaw), position);
	}

	/// <summary>The head's up and down look this frame, blended like <see cref="GetInterpolatedTransform"/>.</summary>
	public float GetInterpolatedPitch()
	{
		float weight = (float)Engine.GetPhysicsInterpolationFraction();
		return AreControlsLocked ? Mathf.Lerp(_previousPitch, Head.Rotation.X, weight) : Head.Rotation.X;
	}

	public void LockControls()
	{
		AreControlsLocked = true;
		StandUp();
	}

	public void UnlockControls()
	{
		AreControlsLocked = false;
	}

	/// <summary>
	/// The player is now inside <paramref name="spot"/>: they can't move or be seen until they stop hiding.
	/// </summary>
	public void StartHiding(IInteractable spot, Transform3D exitPoint)
	{
		HidingSpot = spot;
		_hidingExitPoint = exitPoint;
		SetCollisionEnabled(false);
	}

	public void StopHiding()
	{
		HidingSpot = null;
		SetCollisionEnabled(true);
	}

	/// <summary>
	/// Hands control over to an attacker: the player is knocked back by <paramref name="shove"/>,
	/// can no longer move and keeps looking at <paramref name="lookTarget"/>.
	/// </summary>
	public void GetGrabbed(Node3D lookTarget, Vector3 shove)
	{
		Grabbed?.Invoke();
		Hands.HideImmediately();
		AreControlsLocked = false;
		StandUp();
		if (IsHiding)
		{
			// Dragged out of the hiding spot.
			GlobalTransform = _hidingExitPoint;
			_previousTransform = GlobalTransform;
			StopHiding();
		}

		_grabLookTarget = lookTarget;
		_staggerVelocity = shove;
		Camera.AddShake(1f);
	}

	public void Die()
	{
		DeathScreen.Play();
	}

	private void UpdateStride(Vector3 moved)
	{
		if (!IsWalking)
		{
			// Standing still is halfway through a step, so the first footstep comes half a stride in.
			_strideDistance = Settings.StrideLength * 0.5f;
			return;
		}

		_strideDistance += new Vector2(moved.X, moved.Z).Length();
		if (_strideDistance < Settings.StrideLength)
			return;

		_strideDistance %= Settings.StrideLength;
		float volumeDb = IsCrouching ? CrouchStepVolumeDb
			: IsSprinting ? SprintStepVolumeDb
			: WalkStepVolumeDb;
		Footsteps.Step(volumeDb);
		ClothSound.VolumeDb = volumeDb;
		ClothSound.Play();
	}

	private void Stagger(float delta)
	{
		Velocity = _staggerVelocity;
		MoveAndSlide();
		_staggerVelocity = _staggerVelocity.MoveToward(Vector3.Zero, StaggerDeceleration * delta);
	}

	private void UpdateGrabbedView(float delta)
	{
		// Keep looking at the attacker. Choking pushes the head back.
		Vector3 toTarget = _grabLookTarget.GlobalPosition - Head.GlobalPosition;
		float targetYaw = Mathf.Atan2(-toTarget.X, -toTarget.Z);
		float targetPitch = Mathf.Atan2(toTarget.Y, new Vector2(toTarget.X, toTarget.Z).Length())
			+ Mathf.DegToRad(ChokePitchDegrees) * ChokeAmount;
		Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, GrabbedTurnSpeed * delta), 0f);
		Head.Rotation = new Vector3(Mathf.LerpAngle(Head.Rotation.X, targetPitch, GrabbedTurnSpeed * delta), 0f, 0f);
	}

	private void SetCollisionEnabled(bool enabled)
	{
		// A hidden player must not block or be seen by anything.
		CollisionLayer = enabled ? _collisionLayer : 0;
		CollisionMask = enabled ? _collisionMask : 0;
	}

	private void Look(Vector2 mouseMotion)
	{
		// Setting the angle instead of calling RotateY keeps rounding errors from building up in the basis.
		float turn = Mathf.DegToRad(Settings.MouseSensitivity);
		Rotation = new Vector3(0f, Rotation.Y - mouseMotion.X * turn, 0f);

		float maxPitch = Mathf.DegToRad(Settings.MaxPitchDegrees);
		Vector3 headRotation = Head.Rotation;
		headRotation.X = Mathf.Clamp(headRotation.X - mouseMotion.Y * turn, -maxPitch, maxPitch);
		Head.Rotation = headRotation;
	}

	private void Move(float delta)
	{
		Vector3 velocity = Velocity;
		if (!IsOnFloor())
			velocity += GetGravity() * delta;

		// The input vector is already at most 1 long, so diagonals are no faster.
		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 targetVelocity = Transform.Basis * new Vector3(input.X, 0f, input.Y) * CurrentSpeed;

		// Speed changes at a fixed rate instead of easing in, so the player starts and stops crisply.
		float rate = input == Vector2.Zero ? Settings.BrakingDeceleration : Settings.Acceleration;
		if (!IsOnFloor())
			rate *= Settings.AirControl;
		Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z).MoveToward(targetVelocity, rate * delta);

		Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);
		MoveAndSlide();
	}

	private void UpdateCrouch()
	{
		// Stay crouched while something is above the head, even if the key is released.
		SetCrouching(Input.IsActionPressed("crouch") || (IsCrouching && CeilingCheck.IsColliding()));
	}

	private void StandUp()
	{
		SetCrouching(false);
	}

	private void SetCrouching(bool crouching)
	{
		if (crouching != IsCrouching)
			(crouching ? CrouchSound : StandUpSound).Play();

		IsCrouching = crouching;
		StandingCollision.Disabled = crouching;
		CrouchingCollision.Disabled = !crouching;
	}

	/// <summary>
	/// Muffles the world's sounds while hiding and, much more, as the player passes out, where the ears also ring.
	/// Cutoffs move in octaves rather than hertz, so the muffling grows evenly.
	/// </summary>
	private void UpdateHearing(float delta)
	{
		_hidden = Mathf.MoveToward(_hidden, IsHiding ? 1f : 0f, delta / HidingMuffleSeconds);
		float hiding = Octaves(FullHearingHz, HidingHearingHz, _hidden);
		float choking = Octaves(FullHearingHz, PassingOutHearingHz, ChokeAmount);
		_worldMuffle.CutoffHz = Mathf.Min(hiding, choking);
	}

	private static float Octaves(float fromHz, float toHz, float weight)
	{
		return Mathf.Exp(Mathf.Lerp(Mathf.Log(fromHz), Mathf.Log(toHz), weight));
	}

	private void UpdateHeadPosition(float delta)
	{
		// The eyes ease between standing and crouching height. An attacker can sway the head and grab points.
		float targetHeight = IsCrouching ? Settings.CrouchEyeHeight : Settings.EyeHeight;
		_eyeHeight = Mathf.Lerp(_eyeHeight, targetHeight, Settings.CrouchTransitionSpeed * delta);
		Head.Position = new Vector3(0f, _eyeHeight, 0f) + BodySway;
		GrabPoints.Position = BodySway;
	}
}
