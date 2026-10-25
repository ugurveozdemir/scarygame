using Godot;

/// <summary>
/// A row of MiniDV tape cases standing on a shelf along the node's local X axis, a little crooked like
/// tapes put back by hand.
/// </summary>
public partial class TapeRow : Node3D
{
	[Export] public int Count { get; set; } = 20;
	[Export] public float Spacing { get; set; } = 0.032f;
	[Export] public Material Material { get; set; }

	public override void _Ready()
	{
		var tapeCase = new BoxMesh { Size = new Vector3(0.028f, 0.075f, 0.1f), Material = Material };
		for (int i = 0; i < Count; i++)
		{
			var tape = new MeshInstance3D { Mesh = tapeCase };
			tape.Position = new Vector3(i * Spacing, 0.0375f, (float)GD.RandRange(-0.008, 0.008));
			tape.RotateY((float)GD.RandRange(-0.08, 0.08));
			AddChild(tape);
		}
	}
}
