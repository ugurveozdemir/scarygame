using Godot;

/// <summary>
/// A blindfolded patient standing still in the sleep ward. It can't see, but any noise it hears, a footstep
/// close by, someone running, broken glass underfoot, makes it lunge at where the noise came from and claw at
/// whatever is there. Then it stops where it is and stands still again. Creeping past crouched is safe;
/// bumping into one is not.
/// </summary>
public partial class Sleepwalker : CharacterBody3D
{
	private enum Mood { Still, Lunging, Clawing }

	private const string IdleAnimation = "ual2/Zombie_Idle";
	private const string RunAnimation = "ual1/Sprint";
	private const string ClawAnimation = "ual2/Zombie_Scratch";
	private const float RunAnimationSpeed = 4.6f;

	[Export] public float LungeSpeed { get; set; } = 4.2f;
	[Export] public float TurnSpeed { get; set; } = 10f;
	/// <summary>A lunge that doesn't get there in this long, like into a closed door, gives up.</summary>
	[Export] public float MaxLungeSeconds { get; set; } = 4f;
	[Export] public float ClawReach { get; set; } = 1.4f;
	/// <summary>How long into the claw animation the hands come down.</summary>
	[Export] public float ClawHitSeconds { get; set; } = 0.55f;
	/// <summary>Closer than this, it feels the player brush past even if they make no sound.</summary>
	[Export] public float TouchDistance { get; set; } = 0.75f;
	/// <summary>After a lunge it stands still for a moment before it listens again.</summary>
	[Export] public float CalmSeconds { get; set; } = 1.5f;
	/// <summary>Further from the player than this it stops swaying, which nobody could see anyway.</summary>
	[Export] public float AnimatedDistance { get; set; } = 25f;

	[ExportGroup("Nodes")]
	[Export] public Player Player { get; set; }
	[Export] public NavigationAgent3D NavigationAgent { get; set; }
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	[Export] public AnimationLibrary Ual1Animations { get; set; }
	[Export] public AnimationLibrary Ual2Animations { get; set; }
	/// <summary>Washes out the body's colours, like a hospital gown worn for years.</summary>
	[Export] public Material PaleOverlay { get; set; }
	[Export] public AudioStreamPlayer3D Shriek { get; set; }

	private Mood _mood = Mood.Still;
	private float _calmLeft;
	private float _lungeSecondsLeft;

	public override void _Ready()
	{
		AnimationPlayer.AddAnimationLibrary("ual1", Ual1Animations);
		AnimationPlayer.AddAnimationLibrary("ual2", Ual2Animations);
		// Only the skinned body; the blindfold keeps its own colour. A swaying body that casts shadows would make
		// every shadowed light around it redraw its shadows each frame, so it casts none.
		foreach (Node node in FindChildren("*", nameof(MeshInstance3D), owned: false))
		{
			var mesh = (MeshInstance3D)node;
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			mesh.VisibilityRangeEnd = AnimatedDistance;
			if (mesh.GetParent() is Skeleton3D)
				mesh.MaterialOverlay = PaleOverlay;
		}
		VisualLayers.Assign(this, VisualLayers.Enemies);

		// They sway out of step with each other.
		AnimationPlayer.Play(IdleAnimation);
		AnimationPlayer.Seek(GD.Randf() * AnimationPlayer.CurrentAnimationLength);
		Player.MadeNoise += OnNoise;
	}

	public override void _ExitTree()
	{
		Player.MadeNoise -= OnNoise;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_calmLeft -= dt;
		AnimationPlayer.Active = GlobalPosition.DistanceTo(Player.GlobalPosition) <= AnimatedDistance;

		if (_mood == Mood.Still && _calmLeft <= 0f && IsTouchingPlayer())
			Lunge(Player.GlobalPosition);
		else if (_mood == Mood.Lunging)
			FollowLunge(dt);

		if (!IsOnFloor())
			Velocity += GetGravity() * dt;
		MoveAndSlide();
	}

	private void OnNoise(Vector3 position, float radius)
	{
		if (_mood == Mood.Still && _calmLeft <= 0f && GlobalPosition.DistanceTo(position) <= radius)
			Lunge(position);
	}

	private void Lunge(Vector3 position)
	{
		_mood = Mood.Lunging;
		_lungeSecondsLeft = MaxLungeSeconds;
		NavigationAgent.TargetPosition = position;
		Shriek.Play();
		AnimationPlayer.SpeedScale = LungeSpeed / RunAnimationSpeed;
		AnimationPlayer.Play(RunAnimation, 0.15f);
	}

	private void FollowLunge(float delta)
	{
		_lungeSecondsLeft -= delta;
		if (CanReachPlayer())
		{
			Claw();
			return;
		}

		if (NavigationAgent.IsNavigationFinished() || _lungeSecondsLeft <= 0f)
		{
			StandStill();
			return;
		}

		Vector3 direction = NavigationAgent.GetNextPathPosition() - GlobalPosition;
		direction.Y = 0f;
		direction = direction.Normalized();
		Velocity = new Vector3(direction.X * LungeSpeed, Velocity.Y, direction.Z * LungeSpeed);
		Face(direction, TurnSpeed * delta);
	}

	private async void Claw()
	{
		_mood = Mood.Clawing;
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		Face(Player.GlobalPosition - GlobalPosition, turnAmount: 1f);
		AnimationPlayer.SpeedScale = 1f;
		AnimationPlayer.Play(ClawAnimation, 0.1f);

		await Wait(ClawHitSeconds);
		if (CanReachPlayer())
			Player.TakeHit(this);

		await Wait((float)AnimationPlayer.CurrentAnimationLength - ClawHitSeconds);
		StandStill();
	}

	private void StandStill()
	{
		_mood = Mood.Still;
		_calmLeft = CalmSeconds;
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		AnimationPlayer.SpeedScale = 1f;
		AnimationPlayer.Play(IdleAnimation, 0.4f);
	}

	private SignalAwaiter Wait(float seconds)
	{
		// A tween belongs to this node, so a wait never finishes once the level is reloaded.
		Tween wait = CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics);
		wait.TweenInterval(seconds);
		return ToSignal(wait, Tween.SignalName.Finished);
	}

	private bool IsTouchingPlayer()
	{
		return !Player.IsHiding && !Player.IsDead && GlobalPosition.DistanceTo(Player.GlobalPosition) <= TouchDistance;
	}

	private bool CanReachPlayer()
	{
		return !Player.IsHiding && !Player.IsDead && GlobalPosition.DistanceTo(Player.GlobalPosition) <= ClawReach;
	}

	/// <summary>Turns towards <paramref name="direction"/>, all the way for a <paramref name="turnAmount"/> of 1.</summary>
	private void Face(Vector3 direction, float turnAmount)
	{
		direction.Y = 0f;
		if (direction.IsZeroApprox())
			return;

		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, Mathf.Min(1f, turnAmount)), 0f);
	}
}
