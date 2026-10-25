using Godot;

/// <summary>
/// Gives the player a new objective the first time they walk into the area.
/// </summary>
public partial class ObjectiveTrigger : Area3D
{
	[Export] public string Objective { get; set; } = "";
	/// <summary>Whether the memo is the player's own, saved for the next trial, instead of one left by the last.</summary>
	[Export] public bool IsOwnMemo { get; set; }

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not Player player)
			return;

		if (IsOwnMemo)
			player.ObjectiveDisplay.AnnounceOwn(Objective);
		else
			player.ObjectiveDisplay.Announce(Objective);
		QueueFree();
	}
}
