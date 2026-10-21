using Godot;

/// <summary>
/// A locker the player can hide in, looking out through the slits in its door.
/// </summary>
public partial class Locker : StaticBody3D, IInteractable
{
	[Export] public Marker3D HidePoint { get; set; }
	[Export] public Marker3D ExitPoint { get; set; }

	private Player _occupant;

	private bool IsOccupied => _occupant is not null && _occupant.HidingSpot == this;

	public string InteractionPrompt => IsOccupied ? "Leave" : "Hide";

	public void Interact(Player player)
	{
		if (IsOccupied)
		{
			player.LeaveHidingSpot();
			return;
		}

		_occupant = player;
		player.EnterHidingSpot(this, HidePoint.GlobalTransform, ExitPoint.GlobalTransform);
	}
}
