using System;
using Godot;

/// <summary>
/// Something that won't open yet, like the locked gate or a lift without power. Trying it rattles it and the
/// player notes why. Once released it is no longer something to interact with.
/// </summary>
public partial class Lock : StaticBody3D, IInteractable
{
	[Export] public string Prompt { get; set; } = "Open";
	[Export(PropertyHint.MultilineText)] public string LockedCaption { get; set; } = "";
	[Export] public AudioStreamPlayer3D RattleSound { get; set; }

	public string InteractionPrompt => Prompt;

	/// <summary>Raised when the player tries the lock.</summary>
	public event Action Tried;

	public void Interact(Player player)
	{
		if (RattleSound is { Playing: false })
			RattleSound.Play();
		player.Caption.Say(LockedCaption);
		Tried?.Invoke();
	}

	public void Release()
	{
		CollisionLayer = 0;
	}
}
