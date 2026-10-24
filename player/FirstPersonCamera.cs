using Godot;

/// <summary>
/// The player's eyes, built like Unreal Engine's first-person camera: it sits at eye height and turns exactly with
/// the mouse, so the crosshair always covers what the player aims at. It follows the body's blended position, so
/// it stays smooth at any frame rate. On top of that it only adds a head bob that dips the eyes with each footstep,
/// never turning them, and shakes for impacts and choking. The camcorder, the first-person arms and the interaction
/// ray are its children, so they move with it.
/// </summary>
public partial class FirstPersonCamera : Camera3D
{
	private const float BobSmoothSpeed = 10f;
	private const float FovSmoothSpeed = 5f;
	private const float ShakeFrequency = 20f;

	[Export] public Player Player { get; set; }

	[ExportGroup("Shake")]
	// Maximum shake offset in metres; the same value is the maximum shake angle in radians.
	[Export] public float ShakeStrength { get; set; } = 0.03f;
	[Export] public float ShakeDecay { get; set; } = 2f;
	[Export] public float ChokeShake { get; set; } = 0.5f;

	private float _bob;
	private float _fovBoost;
	private float _shake;
	private float _shakeTime;

	public override void _Ready()
	{
		// The camera places itself every frame instead of inheriting the body's stepped physics transform.
		TopLevel = true;
		// Keeping the horizontal field of view fixed is how Unreal Engine measures it.
		KeepAspect = KeepAspectEnum.Width;
	}

	/// <summary>Adds a shake impulse from 0 to 1, e.g. when the player is grabbed.</summary>
	public void AddShake(float amount)
	{
		_shake = Mathf.Clamp(_shake + amount, 0f, 1f);
	}

	/// <summary>Called by the player after it has updated the head for this frame.</summary>
	public void UpdateView(float delta)
	{
		UpdateBob(delta);
		UpdateFov(delta);
		_shake = Mathf.MoveToward(_shake, 0f, ShakeDecay * delta);
		_shakeTime += delta * ShakeFrequency;

		Transform3D eye = Player.GetInterpolatedTransform() * new Transform3D(Basis.Identity, Player.Head.Position);
		Basis look = eye.Basis * Basis.FromEuler(new Vector3(Player.GetInterpolatedPitch(), 0f, 0f));
		GlobalTransform = new Transform3D(look, eye.Origin) * Shake();
		GlobalPosition += Vector3.Up * _bob;
	}

	private void UpdateBob(float delta)
	{
		// The eyes are lowest as a foot lands and back at eye height halfway through the step.
		FirstPersonSettings settings = Player.Settings;
		float target = 0f;
		if (Player.IsWalking)
		{
			float weight = Mathf.Clamp(Player.HorizontalSpeed / settings.SprintSpeed, 0f, 1f);
			target = -settings.BobHeight * weight * 0.5f * (1f + Mathf.Cos(Player.StepProgress * Mathf.Tau));
		}
		_bob = Mathf.Lerp(_bob, target, Smoothing(BobSmoothSpeed, delta));
	}

	private void UpdateFov(float delta)
	{
		float target = Player.IsSprinting && !Player.IsGrabbed && !Player.AreControlsLocked ? Player.Settings.SprintFovBoost : 0f;
		_fovBoost = Mathf.Lerp(_fovBoost, target, Smoothing(FovSmoothSpeed, delta));
		Fov = Player.Settings.FieldOfView + _fovBoost;
	}

	/// <summary>A wobble made of a few out-of-step sine waves, as strong as the current shake and choking.</summary>
	private Transform3D Shake()
	{
		float shake = (_shake + Player.ChokeAmount * ChokeShake) * ShakeStrength;
		if (shake == 0f)
			return Transform3D.Identity;

		float x = Mathf.Sin(_shakeTime) * 0.6f + Mathf.Sin(_shakeTime * 0.47f + 1.3f) * 0.4f;
		float y = Mathf.Sin(_shakeTime * 1.13f + 2.1f) * 0.6f + Mathf.Sin(_shakeTime * 0.61f + 0.4f) * 0.4f;
		float z = Mathf.Sin(_shakeTime * 0.83f + 3.2f) * 0.6f + Mathf.Sin(_shakeTime * 0.37f + 2.5f) * 0.4f;
		return new Transform3D(Basis.FromEuler(new Vector3(y, x, z) * shake), new Vector3(x, y, 0f) * shake);
	}

	private static float Smoothing(float speed, float delta)
	{
		return 1f - Mathf.Exp(-speed * delta);
	}
}
