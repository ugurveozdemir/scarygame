using Godot;

/// <summary>
/// A button of the service lift: inside the car it sends the car to the other floor, on a landing it calls
/// the car there.
/// </summary>
public partial class LiftButton : StaticBody3D, IInteractable
{
	[Export] public ServiceLift Lift { get; set; }
	/// <summary>The landing this button calls the car to; empty for the button inside the car.</summary>
	[Export] public LiftLanding CallsTo { get; set; }
	[Export] public AudioStreamPlayer3D PressSound { get; set; }
	[Export] public string NoPowerCaption { get; set; } = "Nothing happens. The lift has no power.";

	public string InteractionPrompt => CallsTo is not null ? "Call the lift"
		: Lift.IsUpstairs ? "Go down" : "Go up";

	public void Interact(Player player)
	{
		PressSound.Play();
		if (!Lift.HasPower)
			player.Caption.Say(NoPowerCaption);
		else if (CallsTo is not null)
			Lift.Call(CallsTo);
		else
			Lift.Ride(player);
	}
}
