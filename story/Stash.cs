using Godot;

/// <summary>
/// Something the player can search once, like a trash can a previous trial hid things in: spare batteries and
/// a note, which is read as soon as it is found.
/// </summary>
public partial class Stash : StaticBody3D, IInteractable
{
	[Export] public string Prompt { get; set; } = "Search";
	[Export] public int Batteries { get; set; } = 1;
	/// <summary>The note found inside, read on the spot. Its memo shows when it is put down.</summary>
	[Export] public Note NoteInside { get; set; }
	[Export] public AudioStreamPlayer3D RummageSound { get; set; }

	public string InteractionPrompt => Prompt;

	public void Interact(Player player)
	{
		// Searched once, the stash stops being something to interact with.
		CollisionLayer = 0;
		RummageSound.Play();
		for (int i = 0; i < Batteries; i++)
			player.Camcorder.TryAddSpareBattery();

		NoteInside?.Interact(player);
	}
}
