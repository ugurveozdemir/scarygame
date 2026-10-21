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

	[ExportGroup("Nodes")]
	[Export] public Node3D Head { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public CollisionShape3D StandingCollision { get; set; }
	[Export] public CollisionShape3D CrouchingCollision { get; set; }
	[Export] public RayCast3D CeilingCheck { get; set; }
	[Export] public Camcorder Camcorder { get; set; }

	public IInteractable HidingSpot { get; private set; }
	public bool IsHiding => HidingSpot is not null;
	public bool IsMovingToOrFromHidingSpot => _hidingTween is not null && _hidingTween.IsRunning();

	private bool _isCrouching;
	private float _bobPhase;
	private uint _collisionLayer;
	private uint _collisionMask;
	private Tween _hidingTween;

	private float CurrentSpeed => _isCrouching ? CrouchSpeed
		: Input.IsActionPressed("sprint") ? SprintSpeed
		: WalkSpeed;

	public override void _Ready()
	{
		_collisionLayer = CollisionLayer;
		_collisionMask = CollisionMask;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
			Look(motion.Relative);
		else if (@event.IsActionPressed("ui_cancel"))
			Input.MouseMode = Input.MouseModeEnum.Visible;
		else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsHiding)
			return;

		float dt = (float)delta;
		UpdateCrouch(dt);
		Move(dt);
		UpdateHeadBob(dt);
	}

	public void EnterHidingSpot(IInteractable spot, Transform3D hidePoint)
	{
		HidingSpot = spot;
		Velocity = Vector3.Zero;
		SetCollisionEnabled(false);
		MoveSmoothlyTo(hidePoint);
	}

	public void LeaveHidingSpot(Transform3D exitPoint)
	{
		MoveSmoothlyTo(exitPoint).Finished += () =>
		{
			HidingSpot = null;
			SetCollisionEnabled(true);
		};
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
		_isCrouching = Input.IsActionPressed("crouch") || (_isCrouching && CeilingCheck.IsColliding());
		StandingCollision.Disabled = _isCrouching;
		CrouchingCollision.Disabled = !_isCrouching;

		float targetHeight = _isCrouching ? CrouchingHeadHeight : StandingHeadHeight;
		Vector3 headPosition = Head.Position;
		headPosition.Y = Mathf.Lerp(headPosition.Y, targetHeight, CrouchTransitionSpeed * delta);
		Head.Position = headPosition;
	}

	private void UpdateHeadBob(float delta)
	{
		float speed = new Vector2(Velocity.X, Velocity.Z).Length();
		bool isWalking = IsOnFloor() && speed > 0.1f;
		_bobPhase += speed * BobFrequency * delta;

		Vector3 targetOffset = isWalking
			? new Vector3(Mathf.Cos(_bobPhase * 0.5f) * BobAmplitude * 0.5f, Mathf.Sin(_bobPhase) * BobAmplitude, 0f)
			: Vector3.Zero;
		Camera.Position = Camera.Position.Lerp(targetOffset, 10f * delta);
	}
}
