using Godot;

/// <summary>
/// A patient the clinic's experiments left half out of this world. The naked eye can't see it; it only shows up
/// through the camcorder's night vision. Once freed it drifts after the player anywhere in the basement, through
/// closed doors, and its touch hurts. Close by, it drains the camcorder's battery and breaks up its picture.
/// It can't find a player who hides, and it doesn't leave the basement.
/// </summary>
public partial class Remnant : Node3D
{
	private const string IdleAnimation = "ual2/Zombie_Idle";
	private const string WalkAnimation = "ual2/Zombie_Walk_Fwd";
	private const float WalkAnimationSpeed = 1.15f;

	[Export] public float Speed { get; set; } = 1.3f;
	[Export] public float TurnSpeed { get; set; } = 4f;
	/// <summary>Closer than this, its touch hurts the player.</summary>
	[Export] public float TouchDistance { get; set; } = 1.1f;
	[Export] public float TouchCooldownSeconds { get; set; } = 2.5f;
	/// <summary>The basement, where the player is within its reach.</summary>
	[Export] public Aabb Basement { get; set; } = new(new Vector3(-11.6f, -6.5f, -26.4f), new Vector3(23.2f, 4f, 40.7f));

	[ExportGroup("Interference")]
	/// <summary>Closer than this it starts to break up the camcorder's picture.</summary>
	[Export] public float InterferenceDistance { get; set; } = 9f;
	/// <summary>How many times faster night vision drains the battery with the Remnant right next to the player.</summary>
	[Export] public float MaxDrainMultiplier { get; set; } = 5f;

	[ExportGroup("Nodes")]
	[Export] public Player Player { get; set; }
	/// <summary>Where it lingers while the player is out of its reach.</summary>
	[Export] public Node3D Lair { get; set; }
	[Export] public NavigationAgent3D NavigationAgent { get; set; }
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	[Export] public AnimationLibrary Ual2Animations { get; set; }
	/// <summary>Replaces the body's own materials with a faint glow.</summary>
	[Export] public Material GhostMaterial { get; set; }
	[Export] public AudioStreamPlayer3D StaticSound { get; set; }
	[Export] public AudioStreamPlayer3D WhisperSound { get; set; }
	/// <summary>The cold chamber it bursts out of when it is freed.</summary>
	[Export] public AudioStreamPlayer3D ReleaseSound { get; set; }

	public bool IsFree { get; private set; }

	private float _touchCooldownLeft;

	private bool IsPlayerInReach => Basement.HasPoint(Player.GlobalPosition) && !Player.IsHiding && !Player.IsDead;
	private bool _wasInterfering;

	public override void _Ready()
	{
		AnimationPlayer.AddAnimationLibrary("ual2", Ual2Animations);
		VisualLayers.Assign(this, VisualLayers.NightVisionOnly);
		foreach (Node node in FindChildren("*", nameof(MeshInstance3D), owned: false))
		{
			var mesh = (MeshInstance3D)node;
			mesh.MaterialOverride = GhostMaterial;
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}
		Visible = false;
	}

	public void Release()
	{
		IsFree = true;
		Visible = true;
		ReleaseSound.Play();
		StaticSound.Play();
		WhisperSound.ProcessMode = ProcessModeEnum.Inherit;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsFree)
			return;

		float dt = (float)delta;
		Drift(IsPlayerInReach ? Player.GlobalPosition : Lair.GlobalPosition, dt);
		Touch(dt);
		Interfere();
	}

	private void Drift(Vector3 destination, float delta)
	{
		NavigationAgent.TargetPosition = destination;
		if (NavigationAgent.IsNavigationFinished())
		{
			AnimationPlayer.SpeedScale = 1f;
			AnimationPlayer.Play(IdleAnimation, 0.5f);
			return;
		}

		Vector3 next = NavigationAgent.GetNextPathPosition();
		Vector3 step = (next - GlobalPosition).LimitLength(Speed * delta);
		GlobalPosition += step;

		Vector3 flat = new(step.X, 0f, step.Z);
		if (!flat.IsZeroApprox())
		{
			float targetYaw = Mathf.Atan2(-flat.X, -flat.Z);
			Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, TurnSpeed * delta), 0f);
		}

		AnimationPlayer.SpeedScale = Speed / WalkAnimationSpeed;
		AnimationPlayer.Play(WalkAnimation, 0.5f);
	}

	private void Touch(float delta)
	{
		_touchCooldownLeft -= delta;
		if (_touchCooldownLeft > 0f || !IsPlayerInReach || GlobalPosition.DistanceTo(Player.GlobalPosition) > TouchDistance)
			return;

		_touchCooldownLeft = TouchCooldownSeconds;
		Player.TakeHit(this);
	}

	private void Interfere()
	{
		float distance = GlobalPosition.DistanceTo(Player.GlobalPosition);
		float closeness = IsPlayerInReach ? Mathf.Clamp(1f - distance / InterferenceDistance, 0f, 1f) : 0f;

		// Once the camcorder's picture is clean again, it is left alone for anything else that disturbs it.
		bool isInterfering = closeness > 0f;
		if (!isInterfering && !_wasInterfering)
			return;

		_wasInterfering = isInterfering;
		Player.Camcorder.Interference = closeness * closeness;
		Player.Camcorder.DrainMultiplier = Mathf.Lerp(1f, MaxDrainMultiplier, closeness);
	}
}
