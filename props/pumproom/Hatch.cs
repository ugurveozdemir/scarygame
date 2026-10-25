using Godot;

/// <summary>
/// A padlocked maintenance hatch in the ground. With the right key it swings open onto a ladder down.
/// </summary>
public partial class Hatch : StaticBody3D, IInteractable
{
	[Export] public string KeyName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string LockedCaption { get; set; } = "";
	[Export] public Node3D Lid { get; set; }
	[Export] public float OpenAngleDegrees { get; set; } = 110f;
	[Export] public Ladder LadderDown { get; set; }
	[Export] public AudioStreamPlayer3D RattleSound { get; set; }
	[Export] public AudioStreamPlayer3D OpenSound { get; set; }

	public string InteractionPrompt => "Open the hatch";

	public void Interact(Player player)
	{
		if (!player.Keys.Contains(KeyName))
		{
			RattleSound.Play();
			player.Caption.Say(LockedCaption);
			return;
		}

		CollisionLayer = 0;
		OpenSound.Play();
		Tween tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(Lid, "rotation_degrees:x", OpenAngleDegrees, 1f);
		tween.TweenCallback(Callable.From(LadderDown.Enable));
	}
}
