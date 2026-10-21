using Godot;

/// <summary>
/// A locker the player can hide in, looking out through the slits in its door.
/// </summary>
public partial class Locker : StaticBody3D, IInteractable
{
	[Export] public Marker3D HidePoint { get; set; }
	[Export] public Marker3D ExitPoint { get; set; }

	private bool _isOccupied;

	public string InteractionPrompt => _isOccupied ? "Leave" : "Hide";

	public void Interact(Player player)
	{
		if (_isOccupied)
			player.LeaveHidingSpot(ExitPoint.GlobalTransform);
		else
			player.EnterHidingSpot(this, HidePoint.GlobalTransform);

		_isOccupied = !_isOccupied;
	}
}
