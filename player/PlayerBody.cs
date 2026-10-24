using Godot;

/// <summary>
/// The player's whole body as the world sees it. As in Unreal Engine's first-person template, the player never sees
/// it themselves: it only casts their shadow, while the first-person arms are drawn on their own.
/// It stands, walks, sprints and crouches along with the player.
/// </summary>
public partial class PlayerBody : Node3D
{
	private const string IdleAnimation = "ual1/Idle";
	private const string WalkAnimation = "ual1/Walk";
	private const string SprintAnimation = "ual1/Sprint";
	private const string CrouchIdleAnimation = "ual1/Crouch_Idle";
	private const string CrouchWalkAnimation = "ual1/Crouch_Fwd";

	[Export] public Player Player { get; set; }
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	[Export] public AnimationLibrary Animations { get; set; }
	[Export] public float AnimationBlendSeconds { get; set; } = 0.25f;

	public override void _Ready()
	{
		// It follows the player's blended transform every frame instead of the stepped physics one.
		TopLevel = true;
		AnimationPlayer.AddAnimationLibrary("ual1", Animations);
		foreach (Node mesh in FindChildren("*", nameof(MeshInstance3D), owned: false))
			((MeshInstance3D)mesh).CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
	}

	public override void _Process(double delta)
	{
		GlobalTransform = Player.GetInterpolatedTransform();
		bool isMoving = Player.HorizontalSpeed > 0.1f;
		string animation = Player.IsCrouching ? (isMoving ? CrouchWalkAnimation : CrouchIdleAnimation)
			: Player.IsSprinting ? SprintAnimation
			: isMoving ? WalkAnimation
			: IdleAnimation;
		AnimationPlayer.Play(animation, AnimationBlendSeconds);
	}
}
