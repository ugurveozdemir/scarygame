using Godot;

/// <summary>
/// A key lying around, which the player pockets. Things locked with it check the player's keys by name.
/// </summary>
public partial class KeyPickup : StaticBody3D, IInteractable
{
	[Export] public string KeyName { get; set; } = "";
	[Export] public string FoundCaption { get; set; } = "";
	[Export] public AudioStreamPlayer3D PickUpSound { get; set; }

	public string InteractionPrompt => $"Take the {KeyName} key";

	public void Interact(Player player)
	{
		player.Keys.Add(KeyName);
		player.Caption.Say(FoundCaption);
		PickUpSound.Play();
		CollisionLayer = 0;
		foreach (Node child in GetChildren())
		{
			if (child is Node3D visual and not AudioStreamPlayer3D)
				visual.Visible = false;
		}
	}
}
