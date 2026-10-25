using System;
using Godot;

/// <summary>
/// The chief physician's console for the building's magnetic locks. Releasing them opens the gate, the fire
/// door and everything else, and sets off the reset siren.
/// </summary>
public partial class LockConsole : StaticBody3D, IInteractable
{
	[Export] public Label3D ScreenText { get; set; }
	[Export] public string ReleasedText { get; set; } = "";
	[Export] public AudioStreamPlayer3D ConfirmSound { get; set; }

	public bool IsReleased { get; private set; }

	public string InteractionPrompt => "Release the magnetic locks";

	public event Action Released;

	public void Interact(Player player)
	{
		IsReleased = true;
		CollisionLayer = 0;
		ScreenText.Text = ReleasedText;
		ConfirmSound.Play();
		Released?.Invoke();
	}
}
