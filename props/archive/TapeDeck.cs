using Godot;

/// <summary>
/// The archive's video deck with a tape already in it. Pressing play shows the recording on the monitor.
/// </summary>
public partial class TapeDeck : StaticBody3D, IInteractable
{
	[Export] public string TapeLabel { get; set; } = "";
	[Export] public TapeRecording Recording { get; set; }
	[Export] public AudioStreamPlayer3D InsertSound { get; set; }

	public string InteractionPrompt => Recording.IsPlaying ? "Playing" : $"Play the tape: {TapeLabel}";

	public void Interact(Player player)
	{
		if (Recording.IsPlaying)
			return;

		InsertSound.Play();
		Recording.Play();
	}
}
