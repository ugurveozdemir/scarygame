using System;
using Godot;

/// <summary>
/// Procedural first-person camera effects, inspired by Cinemachine: head bob while walking and sprinting,
/// a wider field of view at sprint speed, a lean into strafes, a small roll and yaw lag when turning the view,
/// and shake for impacts and choking. The effects live on a rig between the head and the camera, so the view,
/// the interaction ray and the view hands all move together.
/// </summary>
public partial class PlayerCamera : Node3D
{
	/// <summary>Raised when a foot lands, which is when the bob reaches its lowest point.</summary>
	public event Action Stepped;

	[ExportGroup("Nodes")]
	[Export] public Player Player { get; set; }
	[Export] public Camera3D Camera { get; set; }

	[ExportGroup("Head Bob")]
	[Export] public float WalkStepsPerSecond { get; set; } = 1.5f;
	[Export] public float SprintStepsPerSecond { get; set; } = 2.4f;
	[Export] public float BobHeight { get; set; } = 0.04f;
	[Export] public float BobSway { get; set; } = 0.02f;
	[Export] public float BobRollDegrees { get; set; } = 0.6f;

	[ExportGroup("Movement Sway")]
	[Export] public float StrafeLean { get; set; } = 0.025f;
	[Export] public float StrafeRollDegrees { get; set; } = 1.5f;
	[Export] public float SprintFovBoost { get; set; } = 4f;

	[ExportGroup("Look Sway")]
	[Export] public float LookRollDegrees { get; set; } = 2f;
	[Export] public float LookYawLagDegrees { get; set; } = 1.2f;

	[ExportGroup("Shake")]
	// Maximum shake offset in metres; the same value is the maximum shake angle in radians.
	[Export] public float ShakeStrength { get; set; } = 0.03f;
	[Export] public float ShakeDecay { get; set; } = 2f;
	[Export] public float ChokeShake { get; set; } = 0.5f;

	private const float BobSmoothSpeed = 10f;
	private const float LookSmoothSpeed = 8f;
	private const float FovSmoothSpeed = 5f;
	private const float LookReferenceSpeed = 3f;
	private const float ShakeFrequency = 20f;

	private Vector3 _basePosition;
	private Vector3 _baseRotation;
	private float _baseFov;
	private float _bobPhase;
	private Vector3 _bobOffset;
	private float _bobRoll;
	private float _previousYaw;
	private float _lookWeight;
	private float _shake;
	private float _shakeTime;

	public override void _Ready()
	{
		_basePosition = Position;
		_baseRotation = Rotation;
		_baseFov = Camera.Fov;
		_previousYaw = Player.Rotation.Y;
	}

	/// <summary>Called by the player after it has updated the head and body for this frame.</summary>
	public void UpdateCamera(float delta)
	{
		UpdateBob(delta);
		UpdateLookSway(delta);
		UpdateFov(delta);
		UpdateShake(delta);
		ApplyTransform();
	}

	/// <summary>Adds a shake impulse from 0 to 1, e.g. when the player is grabbed.</summary>
	public void AddShake(float amount)
	{
		_shake = Mathf.Clamp(_shake + amount, 0f, 1f);
	}

	private void UpdateBob(float delta)
	{
		bool isWalking = !Player.IsGrabbed && Player.IsOnFloor() && Player.HorizontalSpeed > 0.1f;
		int steps = StepCount(_bobPhase);
		_bobPhase += StepsPerSecond(Player.HorizontalSpeed) * Mathf.Tau * delta;
		if (isWalking && StepCount(_bobPhase) != steps)
			Stepped?.Invoke();

		// Bob and sway grow with speed, so walking is calm and sprinting is pronounced.
		float weight = Mathf.Clamp(Player.HorizontalSpeed / Player.SprintSpeed, 0f, 1f);
		Vector3 targetOffset = Vector3.Zero;
		float targetRoll = 0f;
		if (isWalking)
		{
			targetOffset = new Vector3(
				Mathf.Cos(_bobPhase * 0.5f) * BobSway * weight,
				Mathf.Sin(_bobPhase) * BobHeight * weight,
				0f);
			targetRoll = Mathf.Cos(_bobPhase * 0.5f) * BobRollDegrees * weight;
		}

		// A strafing player leans into the movement.
		float strafe = 0f;
		if (isWalking)
		{
			Vector3 localVelocity = Player.GlobalBasis.Inverse() * Player.Velocity;
			strafe = Mathf.Clamp(localVelocity.X / Player.SprintSpeed, -1f, 1f);
		}
		targetOffset.X += strafe * StrafeLean;
		targetRoll += -strafe * StrafeRollDegrees;

		float blend = Smoothing(BobSmoothSpeed, delta);
		_bobOffset = _bobOffset.Lerp(targetOffset, blend);
		_bobRoll = Mathf.Lerp(_bobRoll, targetRoll, blend);
	}

	private void UpdateLookSway(float delta)
	{
		// Scripted moves and the enemy's grab rotate the body for us; they should not bank the camera.
		bool canLook = !Player.IsGrabbed && !Player.AreControlsLocked;
		float yaw = Player.Rotation.Y;
		float target = 0f;
		if (canLook && delta > 0f)
		{
			float yawSpeed = Mathf.Wrap(yaw - _previousYaw, -Mathf.Pi, Mathf.Pi) / delta;
			target = Mathf.Clamp(yawSpeed / LookReferenceSpeed, -1f, 1f);
		}
		_previousYaw = yaw;
		_lookWeight = Mathf.Lerp(_lookWeight, target, Smoothing(LookSmoothSpeed, delta));
	}

	private void UpdateFov(float delta)
	{
		bool isSprinting = !Player.IsGrabbed && !Player.AreControlsLocked && Player.IsSprinting;
		float targetFov = _baseFov + (isSprinting ? SprintFovBoost : 0f);
		Camera.Fov = Mathf.Lerp(Camera.Fov, targetFov, Smoothing(FovSmoothSpeed, delta));
	}

	private void UpdateShake(float delta)
	{
		_shake = Mathf.MoveToward(_shake, 0f, ShakeDecay * delta);
		_shakeTime += delta * ShakeFrequency;
	}

	private void ApplyTransform()
	{
		float shake = (_shake + Player.ChokeAmount * ChokeShake) * ShakeStrength;
		float shakeX = Mathf.Sin(_shakeTime) * 0.6f
			+ Mathf.Sin(_shakeTime * 0.47f + 1.3f) * 0.4f;
		float shakeY = Mathf.Sin(_shakeTime * 1.13f + 2.1f) * 0.6f
			+ Mathf.Sin(_shakeTime * 0.61f + 0.4f) * 0.4f;
		float shakeZ = Mathf.Sin(_shakeTime * 0.83f + 3.2f) * 0.6f
			+ Mathf.Sin(_shakeTime * 0.37f + 2.5f) * 0.4f;

		Position = _basePosition + _bobOffset + new Vector3(shakeX, shakeY, 0f) * shake;
		float roll = Mathf.DegToRad(_bobRoll + _lookWeight * LookRollDegrees) + shakeZ * shake;
		float yaw = Mathf.DegToRad(-_lookWeight * LookYawLagDegrees) + shakeX * shake;
		Rotation = _baseRotation + new Vector3(shakeY * shake, yaw, roll);
	}

	/// <summary>
	/// Like a real walker, the player takes longer strides rather than only quicker ones as they speed up.
	/// Below walking speed the cadence falls off gently, so creeping and crouching still take small steps.
	/// </summary>
	private float StepsPerSecond(float speed)
	{
		if (speed <= Player.WalkSpeed)
			return WalkStepsPerSecond * Mathf.Sqrt(speed / Player.WalkSpeed);

		float sprint = Mathf.Clamp(Mathf.InverseLerp(Player.WalkSpeed, Player.SprintSpeed, speed), 0f, 1f);
		return Mathf.Lerp(WalkStepsPerSecond, SprintStepsPerSecond, sprint);
	}

	/// <summary>How many times the bob has passed its lowest point, where sin(phase) is -1.</summary>
	private static int StepCount(float phase)
	{
		return Mathf.FloorToInt((phase + Mathf.Pi / 2f) / Mathf.Tau);
	}

	private static float Smoothing(float speed, float delta)
	{
		return 1f - Mathf.Exp(-speed * delta);
	}
}
