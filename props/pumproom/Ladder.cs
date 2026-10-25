using System;
using Godot;

/// <summary>
/// A ladder the player climbs in one go: the screen fades out, and fades back in with the player at the other
/// end. Without a destination, climbing it only raises <see cref="Climbed"/>, like the way out at the end.
/// </summary>
public partial class Ladder : StaticBody3D, IInteractable
{
	[Export] public string Prompt { get; set; } = "Climb";
	[Export] public Marker3D Destination { get; set; }
	[Export] public TitleCard Fader { get; set; }
	[Export] public float FadeSeconds { get; set; } = 0.6f;
	[Export] public AudioStreamPlayer ClimbSound { get; set; }

	public string InteractionPrompt => Prompt;

	public event Action Climbed;

	public void Interact(Player player)
	{
		ClimbSound?.Play();
		if (Destination is null)
		{
			Climbed?.Invoke();
			return;
		}

		player.LockControls();
		Tween tween = Fader.FadeIn("", FadeSeconds);
		tween.TweenCallback(Callable.From(() =>
		{
			player.Teleport(Destination.GlobalPosition - player.GlobalPosition);
			player.Rotation = new Vector3(0f, Destination.GlobalRotation.Y, 0f);
			player.Head.Rotation = Vector3.Zero;
			Fader.Reveal(FadeSeconds).TweenCallback(Callable.From(player.UnlockControls));
			Climbed?.Invoke();
		}));
	}

	public void Enable()
	{
		CollisionLayer = 8;
	}
}
