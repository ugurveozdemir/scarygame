using Godot;

/// <summary>
/// A plank wedged across a door from the inside. Lifting it off leans it against the wall and frees the door.
/// </summary>
public partial class DoorBar : StaticBody3D, IInteractable
{
	[Export] public Door Door { get; set; }
	/// <summary>Where the plank ends up, leaning against the wall.</summary>
	[Export] public Node3D RestPoint { get; set; }
	[Export] public float LiftSeconds { get; set; } = 0.8f;
	[Export] public AudioStreamPlayer3D LiftSound { get; set; }

	public string InteractionPrompt => "Lift the bar";

	public override void _Ready()
	{
		Door.IsBarred = true;
	}

	public void Interact(Player player)
	{
		CollisionLayer = 0;
		Door.Unbar();
		LiftSound.Play();

		// Up off its brackets first, then over to the wall.
		Vector3 lifted = Position + Vector3.Up * 0.15f;
		Tween tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(this, "position", lifted, LiftSeconds * 0.3f);
		tween.TweenProperty(this, "global_transform", RestPoint.GlobalTransform, LiftSeconds * 0.7f);
	}
}
