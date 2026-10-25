using Godot;

/// <summary>
/// Something that is only there once the player has been reset at the gate, like a warning a previous trial
/// scrawled on a wall.
/// </summary>
public partial class ShowAfterTheLoop : Node3D
{
	public override void _Ready()
	{
		Visible = GameState.HasSeenTheLoop;
	}
}
