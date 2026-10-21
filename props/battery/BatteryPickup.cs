using Godot;

/// <summary>
/// A spare camcorder battery lying around the level.
/// </summary>
public partial class BatteryPickup : StaticBody3D, IInteractable
{
	public string InteractionPrompt => "Take battery";

	public void Interact(Player player)
	{
		if (player.Camcorder.TryAddSpareBattery())
			QueueFree();
	}
}
