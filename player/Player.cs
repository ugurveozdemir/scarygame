using System;
using Godot;

/// <summary>
/// First-person controller: mouse look, walking, sprinting, crouching, hiding, getting hit and dying.
/// </summary>
public partial class Player : CharacterBody3D
{
	private const float FullHearingHz = 20000f;

	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 2.5f;
	[Export] public float SprintSpeed { get; set; } = 5f;
	[Export] public float CrouchSpeed { get; set; } = 1.3f;
	[Export] public float Acceleration { get; set; } = 10f;

	[ExportGroup("Noise")]
	[Export] public float WalkNoiseRadius { get; set; } = 4f;
	[Export] public float SprintNoiseRadius { get; set; } = 12f;

	[ExportGroup("Footsteps")]
	[Export] public float WalkStepVolumeDb { get; set; } = -6f;
	[Export] public float SprintStepVolumeDb { get; set; } = 0f;
	[Export] public float CrouchStepVolumeDb { get; set; } = -15f;
	/// <summary>The jacket and the camcorder strap moving with each step.</summary>
	[Export] public AudioStreamPlayer ClothSound { get; set; }
	[Export] public AudioStreamPlayer CrouchSound { get; set; }
	[Export] public AudioStreamPlayer StandUpSound { get; set; }

	[ExportGroup("Look")]
	[Export] public float MouseSensitivity { get; set; } = 0.002f;
	[Export] public float MaxPitchDegrees { get; set; } = 85f;

	[ExportGroup("Crouch")]
	[Export] public float StandingHeadHeight { get; set; } = 1.6f;
	[Export] public float CrouchingHeadHeight { get; set; } = 0.9f;
	[Export] public float CrouchTransitionSpeed { get; set; } = 8f;

	[ExportGroup("Getting Hit")]
	/// <summary>How many hits knock the player down for good.</summary>
	[Export] public int HitsToDie { get; set; } = 4;
	/// <summary>How long after the last hit the player starts to recover.</summary>
	[Export] public float RecoveryDelaySeconds { get; set; } = 8f;
	/// <summary>How long it takes to recover from one hit, once recovering.</summary>
	[Export] public float RecoverySecondsPerHit { get; set; } = 6f;
	/// <summary>How fast a hit shoves the player away from the attacker.</summary>
	[Export] public float KnockbackSpeed { get; set; } = 6f;
	/// <summary>How long the red flash, the ringing ears and the muffled hearing last after a hit.</summary>
	[Export] public float HitDazeSeconds { get; set; } = 1.5f;
	/// <summary>The low-pass cutoff that muffles the world's sounds right after a hit.</summary>
	[Export] public float DazedHearingHz { get; set; } = 700f;
	[Export] public ColorRect HurtOverlay { get; set; }
	/// <summary>The blow landing.</summary>
	[Export] public AudioStreamPlayer HitSound { get; set; }
	/// <summary>The player crying out with each hit.</summary>
	[Export] public AudioStreamPlayer PainSound { get; set; }
	/// <summary>Ringing in the ears, fading as the daze wears off.</summary>
	[Export] public AudioStreamPlayer EarRing { get; set; }

	[ExportGroup("Dying")]
	[Export] public float FallSeconds { get; set; } = 0.8f;
	[Export] public float FallenHeadHeight { get; set; } = 0.25f;
	[Export] public float FallenRollDegrees { get; set; } = 75f;
	/// <summary>How long the player lies still before the screen fades out.</summary>
	[Export] public float LieStillSeconds { get; set; } = 1f;

	[ExportGroup("Hiding")]
	/// <summary>The low-pass cutoff of the world's sounds heard from inside a hiding spot.</summary>
	[Export] public float HidingHearingHz { get; set; } = 2500f;
	[Export] public float HidingMuffleSeconds { get; set; } = 0.5f;
	/// <summary>Head height while lying on the floor, like under a bed.</summary>
	[Export] public float LyingHeadHeight { get; set; } = 0.22f;

	[ExportGroup("Nodes")]
	[Export] public Node3D Head { get; set; }
	[Export] public PlayerCamera CameraRig { get; set; }
	[Export] public CollisionShape3D StandingCollision { get; set; }
	[Export] public CollisionShape3D CrouchingCollision { get; set; }
	[Export] public RayCast3D CeilingCheck { get; set; }
	[Export] public Camcorder Camcorder { get; set; }
	[Export] public DeathScreen DeathScreen { get; set; }
	[Export] public ViewHands Hands { get; set; }
	[Export] public Footsteps Footsteps { get; set; }
	[Export] public ObjectiveDisplay ObjectiveDisplay { get; set; }

	public bool IsCrouching { get; private set; }
	public bool IsLyingDown { get; private set; }
	public bool IsSprinting => !IsCrouching && HorizontalSpeed > WalkSpeed + 0.5f;
	public IHidingSpot HidingSpot { get; private set; }
	public bool IsHiding => HidingSpot is not null;
	public bool IsDead { get; private set; }

	/// <summary>How badly hurt the player is, from 0 to 1 when the next hit would be the last.</summary>
	public float Hurt => Mathf.Clamp(_damage / (HitsToDie - 1), 0f, 1f);

	/// <summary>
	/// True while something else, like a locker, is moving the player. They can't walk, look or interact.
	/// </summary>
	public bool AreControlsLocked { get; private set; }

	/// <summary>
	/// Raised when something hits the player, so whatever was moving them can stop.
	/// </summary>
	public event Action Hit;

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

	private uint _collisionLayer;
	private uint _collisionMask;
	private Vector3 _previousPosition;
	private Transform3D _hidingExitPoint;
	private float _headHeight;
	// Counted in hits; it wears off gradually, so it is fractional.
	private float _damage;
	private float _secondsSinceHit;
	private float _daze;
	private ShaderMaterial _hurtMaterial;
	private AudioEffectLowPassFilter _worldMuffle;
	private float _hidden;
	private float _earRingVolume;

	public float HorizontalSpeed => new Vector2(Velocity.X, Velocity.Z).Length();

	private float CurrentSpeed => IsCrouching ? CrouchSpeed
		: Input.IsActionPressed("sprint") ? SprintSpeed
		: WalkSpeed;

	public override void _Ready()
	{
		_collisionLayer = CollisionLayer;
		_collisionMask = CollisionMask;
		_headHeight = StandingHeadHeight;
		_previousPosition = GlobalPosition;
		_hurtMaterial = (ShaderMaterial)HurtOverlay.Material;
		_worldMuffle = (AudioEffectLowPassFilter)AudioServer.GetBusEffect(AudioServer.GetBusIndex("World"), 0);
		_earRingVolume = EarRing.VolumeLinear;
		UpdateDaze(0f);
		CameraRig.Stepped += OnStepped;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (AreControlsLocked)
			return;

		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
			Look(motion.Relative);
		else if (@event.IsActionPressed("ui_cancel"))
			Input.MouseMode = Input.MouseModeEnum.Visible;
		else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		UpdateHeadPosition(dt);
		Recover(dt);
		UpdateDaze(dt);
		CameraRig.UpdateCamera(dt);
		UpdateHearing(dt);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (IsDead)
		{
			SlideToAStop(dt);
			return;
		}

		if (IsHiding || AreControlsLocked)
		{
			// Something else moves the player now; the camera still follows the scripted move.
			Velocity = (GlobalPosition - _previousPosition) / dt;
			_previousPosition = GlobalPosition;
			return;
		}

		UpdateCrouch();
		Move(dt);
		_previousPosition = GlobalPosition;
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

	/// <summary>Brings the head down to the floor, like when crawling under a bed.</summary>
	public void LieDown()
	{
		IsLyingDown = true;
	}

	public void GetUp()
	{
		IsLyingDown = false;
	}

	/// <summary>
	/// The player is now inside <paramref name="spot"/>: they can't move or be seen until they stop hiding.
	/// </summary>
	public void StartHiding(IHidingSpot spot, Transform3D exitPoint)
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

	/// <summary>Dragged out of the hiding spot by an enemy, the player stands in front of it, free to run.</summary>
	public void PullOutOfHiding()
	{
		GlobalTransform = _hidingExitPoint;
		StopHiding();
		GetUp();
		Hands.HideImmediately();
		AreControlsLocked = false;
	}

	/// <summary>
	/// A blow from <paramref name="attacker"/> shoves the player away, snaps the head back and dazes them for
	/// a moment, but leaves them in control, so they can run. <see cref="HitsToDie"/> hits in a row knock
	/// them down.
	/// </summary>
	public void TakeHit(Node3D attacker)
	{
		if (IsDead)
			return;

		Hit?.Invoke();
		Hands.HideImmediately();
		AreControlsLocked = false;
		GetUp();
		_damage += 1f;
		_secondsSinceHit = 0f;
		_daze = 1f;
		HitSound.Play();
		PainSound.Play();

		Vector3 away = GlobalPosition - attacker.GlobalPosition;
		away.Y = 0f;
		away = away.Normalized();
		Velocity += away * KnockbackSpeed;
		CameraRig.AddShake(1f);
		CameraRig.Flinch(GlobalBasis.Inverse() * away);

		if (_damage >= HitsToDie)
			Die();
	}

	private void Die()
	{
		IsDead = true;
		AreControlsLocked = true;
		StandUp();

		// Collapse onto the floor, lie still for a moment, then fade out.
		Tween fall = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		fall.TweenProperty(Head, "rotation", new Vector3(0f, 0f, Mathf.DegToRad(FallenRollDegrees)), FallSeconds);
		fall.TweenInterval(LieStillSeconds);
		fall.TweenCallback(Callable.From(DeathScreen.Play));
	}

	private void OnStepped()
	{
		float volumeDb = IsCrouching ? CrouchStepVolumeDb
			: IsSprinting ? SprintStepVolumeDb
			: WalkStepVolumeDb;
		Footsteps.Step(volumeDb);
		ClothSound.VolumeDb = volumeDb;
		ClothSound.Play();
	}

	private void SlideToAStop(float delta)
	{
		Vector3 velocity = Velocity.MoveToward(new Vector3(0f, Velocity.Y, 0f), Acceleration * delta);
		if (!IsOnFloor())
			velocity += GetGravity() * delta;
		Velocity = velocity;
		MoveAndSlide();
	}

	/// <summary>A while after the last hit, the player slowly gets over their injuries.</summary>
	private void Recover(float delta)
	{
		_secondsSinceHit += delta;
		if (!IsDead && _secondsSinceHit >= RecoveryDelaySeconds)
			_damage = Mathf.MoveToward(_damage, 0f, delta / RecoverySecondsPerHit);
	}

	/// <summary>
	/// The daze after a hit: a red flash, ringing ears and muffled hearing that wear off together.
	/// The edges of the view stay red the more hurt the player is.
	/// </summary>
	private void UpdateDaze(float delta)
	{
		_daze = Mathf.MoveToward(_daze, 0f, delta / HitDazeSeconds);
		_hurtMaterial.SetShaderParameter("hurt", IsDead ? 1f : Hurt);
		_hurtMaterial.SetShaderParameter("flash", _daze * _daze);
		EarRing.VolumeLinear = _earRingVolume * _daze;
	}

	private void SetCollisionEnabled(bool enabled)
	{
		// A hidden player must not block or be seen by anything.
		CollisionLayer = enabled ? _collisionLayer : 0;
		CollisionMask = enabled ? _collisionMask : 0;
	}

	private void Look(Vector2 mouseDelta)
	{
		// Setting the angle instead of calling RotateY keeps rounding errors from building up in the basis.
		Rotation = new Vector3(0f, Rotation.Y - mouseDelta.X * MouseSensitivity, 0f);

		float maxPitch = Mathf.DegToRad(MaxPitchDegrees);
		Vector3 headRotation = Head.Rotation;
		headRotation.X = Mathf.Clamp(headRotation.X - mouseDelta.Y * MouseSensitivity, -maxPitch, maxPitch);
		Head.Rotation = headRotation;
	}

	private void Move(float delta)
	{
		Vector3 velocity = Velocity;
		if (!IsOnFloor())
			velocity += GetGravity() * delta;

		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 direction = (Transform.Basis * new Vector3(input.X, 0f, input.Y)).Normalized();
		Vector3 targetVelocity = direction * CurrentSpeed;
		velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, Acceleration * delta);
		velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, Acceleration * delta);

		Velocity = velocity;
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
	/// Muffles the world's sounds while hiding and, much more, right after a hit.
	/// Cutoffs move in octaves rather than hertz, so the muffling grows evenly.
	/// </summary>
	private void UpdateHearing(float delta)
	{
		_hidden = Mathf.MoveToward(_hidden, IsHiding ? 1f : 0f, delta / HidingMuffleSeconds);
		float hiding = Octaves(FullHearingHz, HidingHearingHz, _hidden);
		float dazed = Octaves(FullHearingHz, DazedHearingHz, _daze);
		_worldMuffle.CutoffHz = Mathf.Min(hiding, dazed);
	}

	private static float Octaves(float fromHz, float toHz, float weight)
	{
		return Mathf.Exp(Mathf.Lerp(Mathf.Log(fromHz), Mathf.Log(toHz), weight));
	}

	private void UpdateHeadPosition(float delta)
	{
		// The head eases between standing, crouching and lying height, and drops to the floor when the player dies.
		float targetHeight = IsDead ? FallenHeadHeight
			: IsLyingDown ? LyingHeadHeight
			: IsCrouching ? CrouchingHeadHeight
			: StandingHeadHeight;
		_headHeight = Mathf.Lerp(_headHeight, targetHeight, CrouchTransitionSpeed * delta);
		Head.Position = new Vector3(0f, _headHeight, 0f);
	}
}
