using Godot;

/// <summary>
/// Gives the player a new objective the first time they walk into the area.
/// </summary>
public partial class ObjectiveTrigger : Area3D
{
	[Export] public string Objective { get; set; } = "";

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not Player player)
			return;

		player.ObjectiveDisplay.Announce(Objective);
		QueueFree();
	}
}
