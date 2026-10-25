using Godot;

/// <summary>
/// The keypad beside the lift on the ground floor. Using it brings up the keypad screen to type the code.
/// </summary>
public partial class Keypad : StaticBody3D, IInteractable
{
	[Export] public ServiceLift Lift { get; set; }
	[Export] public KeypadScreen Screen { get; set; }
	[Export] public AudioStreamPlayer3D AcceptSound { get; set; }
	[Export] public AudioStreamPlayer3D ErrorSound { get; set; }
	[Export] public string NoPowerCaption { get; set; } = "The keypad takes the code, but nothing moves. The lift has no power.";

	public string InteractionPrompt => "Use the keypad";

	public void Interact(Player player)
	{
		Screen.Open(player, Lift.Code.Length, code => OnCodeEntered(player, code));
	}

	private void OnCodeEntered(Player player, string code)
	{
		switch (Lift.TryCode(code))
		{
			case ServiceLift.CodeResult.Accepted:
				AcceptSound.Play();
				break;
			case ServiceLift.CodeResult.NoPower:
				AcceptSound.Play();
				player.Caption.Say(NoPowerCaption);
				break;
			default:
				ErrorSound.Play();
				break;
		}
	}
}
