using Godot;

/// <summary>
/// First-person controller: mouse look, walking, sprinting, crouching, head bob and hiding.
/// </summary>
public partial class Player : CharacterBody3D
{
	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 2.5f;
	[Export] public float SprintSpeed { get; set; } = 5f;
	[Export] public float CrouchSpeed { get; set; } = 1.3f;
	[Export] public float Acceleration { get; set; } = 10f;

	[ExportGroup("Noise")]
	[Export] public float WalkNoiseRadius { get; set; } = 3f;
	[Export] public float SprintNoiseRadius { get; set; } = 10f;

	[ExportGroup("Look")]
	[Export] public float MouseSensitivity { get; set; } = 0.002f;
	[Export] public float MaxPitchDegrees { get; set; } = 85f;

	[ExportGroup("Crouch")]
	[Export] public float StandingHeadHeight { get; set; } = 1.6f;
	[Export] public float CrouchingHeadHeight { get; set; } = 0.9f;
	[Export] public float CrouchTransitionSpeed { get; set; } = 8f;

	[ExportGroup("Head Bob")]
	[Export] public float BobFrequency { get; set; } = 4.5f;
	[Export] public float BobAmplitude { get; set; } = 0.04f;

	[ExportGroup("Hiding")]
	[Export] public float HidingTransitionSeconds { get; set; } = 0.4f;

	[ExportGroup("Being Grabbed")]
	[Export] public float GrabbedTurnSpeed { get; set; } = 8f;
	[Export] public float StaggerDeceleration { get; set; } = 6f;
	[Export] public float ShakeStrength { get; set; } = 0.03f;
	[Export] public float ShakeDecay { get; set; } = 2f;
	[Export] public float ChokeShake { get; set; } = 0.5f;
	[Export] public float ChokePitchDegrees { get; set; } = 5f;
	/// <summary>Parent of the points an attacker grabs; it sways along with the head.</summary>
	[Export] public Node3D GrabPoints { get; set; }
	[Export] public Node3D LeftShoulder { get; set; }
	[Export] public Node3D RightShoulder { get; set; }
	[Export] public Node3D ThroatLeft { get; set; }
	[Export] public Node3D ThroatRight { get; set; }
	[Export] public ColorRect ChokeOverlay { get; set; }

	[ExportGroup("Nodes")]
	[Export] public Node3D Head { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public CollisionShape3D StandingCollision { get; set; }
	[Export] public CollisionShape3D CrouchingCollision { get; set; }
	[Export] public RayCast3D CeilingCheck { get; set; }
	[Export] public Camcorder Camcorder { get; set; }
	[Export] public DeathScreen DeathScreen { get; set; }

	public bool IsCrouching { get; private set; }
	public IInteractable HidingSpot { get; private set; }
	public bool IsHiding => HidingSpot is not null;
	public bool IsMovingToOrFromHidingSpot => _hidingTween is not null && _hidingTween.IsRunning();
	public bool IsGrabbed => _grabLookTarget is not null;

	/// <summary>
	/// A jolt of camera shake from 0 to 1 that fades out by itself, e.g. on impact.
	/// </summary>
	public float ShakeAmount { get; set; }

	/// <summary>
	/// How far an attacker has pushed or pulled the player's upper body, in the player's own space.
	/// </summary>
	public Vector3 BodySway { get; set; }

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

			return HorizontalSpeed > WalkSpeed + 0.5f ? SprintNoiseRadius : WalkNoiseRadius;
		}
	}

	private float _bobPhase;
	private uint _collisionLayer;
	private uint _collisionMask;
	private Tween _hidingTween;
	private Transform3D _hidingExitPoint;
	private float _headHeight;
	private Node3D _grabLookTarget;
	private Vector3 _staggerVelocity;
	private float _chokeAmount;
	private ShaderMaterial _chokeMaterial;

	private float HorizontalSpeed => new Vector2(Velocity.X, Velocity.Z).Length();

	private float CurrentSpeed => IsCrouching ? CrouchSpeed
		: Input.IsActionPressed("sprint") ? SprintSpeed
		: WalkSpeed;

	public override void _Ready()
	{
		_collisionLayer = CollisionLayer;
		_collisionMask = CollisionMask;
		_headHeight = StandingHeadHeight;
		_chokeMaterial = (ShaderMaterial)ChokeOverlay.Material;
		ChokeAmount = 0f;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsGrabbed)
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
		if (IsGrabbed)
			UpdateGrabbedView((float)delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsHiding)
			return;

		float dt = (float)delta;
		if (IsGrabbed)
		{
			Stagger(dt);
			return;
		}

		UpdateCrouch(dt);
		Move(dt);
		UpdateHeadBob(dt);
	}

	public void EnterHidingSpot(IInteractable spot, Transform3D hidePoint, Transform3D exitPoint)
	{
		HidingSpot = spot;
		_hidingExitPoint = exitPoint;
		Velocity = Vector3.Zero;
		SetCollisionEnabled(false);
		MoveSmoothlyTo(hidePoint);
	}

	public void LeaveHidingSpot()
	{
		MoveSmoothlyTo(_hidingExitPoint).Finished += StopHiding;
	}

	/// <summary>
	/// Hands control over to an attacker: the player is knocked back by <paramref name="shove"/>,
	/// can no longer move and keeps looking at <paramref name="lookTarget"/>.
	/// </summary>
	public void GetGrabbed(Node3D lookTarget, Vector3 shove)
	{
		if (IsHiding)
		{
			// Dragged out of the hiding spot.
			_hidingTween?.Kill();
			GlobalTransform = _hidingExitPoint;
			StopHiding();
		}

		_grabLookTarget = lookTarget;
		_staggerVelocity = shove;
		ShakeAmount = 1f;
	}

	public void Die()
	{
		DeathScreen.Play();
	}

	private void StopHiding()
	{
		HidingSpot = null;
		SetCollisionEnabled(true);
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

		// A grabbed player is pulled upright, and the attacker rocks the head and grab points together.
		_headHeight = Mathf.Lerp(_headHeight, StandingHeadHeight, CrouchTransitionSpeed * delta);
		Head.Position = new Vector3(0f, _headHeight, 0f) + BodySway;
		GrabPoints.Position = BodySway;

		ShakeAmount = Mathf.MoveToward(ShakeAmount, 0f, ShakeDecay * delta);
		float shake = (ShakeAmount + ChokeAmount * ChokeShake) * ShakeStrength;
		Camera.Position = RandomDirection() * shake;
		Camera.Rotation = RandomDirection() * shake;
	}

	private static Vector3 RandomDirection()
	{
		return new Vector3(GD.Randf() - 0.5f, GD.Randf() - 0.5f, GD.Randf() - 0.5f) * 2f;
	}

	private Tween MoveSmoothlyTo(Transform3D target)
	{
		_hidingTween = CreateTween().SetTrans(Tween.TransitionType.Sine);
		_hidingTween.TweenProperty(this, "global_transform", target, HidingTransitionSeconds);
		return _hidingTween;
	}

	private void SetCollisionEnabled(bool enabled)
	{
		// A hidden player must not block or be seen by anything.
		CollisionLayer = enabled ? _collisionLayer : 0;
		CollisionMask = enabled ? _collisionMask : 0;
	}

	private void Look(Vector2 mouseDelta)
	{
		RotateY(-mouseDelta.X * MouseSensitivity);

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

	private void UpdateCrouch(float delta)
	{
		// Stay crouched while something is above the head, even if the key is released.
		IsCrouching = Input.IsActionPressed("crouch") || (IsCrouching && CeilingCheck.IsColliding());
		StandingCollision.Disabled = IsCrouching;
		CrouchingCollision.Disabled = !IsCrouching;

		float targetHeight = IsCrouching ? CrouchingHeadHeight : StandingHeadHeight;
		_headHeight = Mathf.Lerp(_headHeight, targetHeight, CrouchTransitionSpeed * delta);
		Head.Position = new Vector3(0f, _headHeight, 0f);
	}

	private void UpdateHeadBob(float delta)
	{
		bool isWalking = IsOnFloor() && HorizontalSpeed > 0.1f;
		_bobPhase += HorizontalSpeed * BobFrequency * delta;

		Vector3 targetOffset = isWalking
			? new Vector3(Mathf.Cos(_bobPhase * 0.5f) * BobAmplitude * 0.5f, Mathf.Sin(_bobPhase) * BobAmplitude, 0f)
			: Vector3.Zero;
		Camera.Position = Camera.Position.Lerp(targetOffset, 10f * delta);
	}
}
