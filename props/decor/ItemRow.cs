using Godot;

/// <summary>
/// A row of small boxes standing on a shelf along the node's local X axis, a little crooked like things put
/// back by hand: tape cases in the archive, medicine boxes in the pharmacy.
/// </summary>
public partial class ItemRow : Node3D
{
	[Export] public int Count { get; set; } = 20;
	[Export] public Vector3 ItemSize { get; set; } = new(0.028f, 0.075f, 0.1f);
	[Export] public float Gap { get; set; } = 0.004f;
	[Export] public Material Material { get; set; }

	public override void _Ready()
	{
		var item = new BoxMesh { Size = ItemSize, Material = Material };
		for (int i = 0; i < Count; i++)
		{
			var instance = new MeshInstance3D { Mesh = item };
			instance.Position = new Vector3(i * (ItemSize.X + Gap), ItemSize.Y / 2f, (float)GD.RandRange(-0.008, 0.008));
			instance.RotateY((float)GD.RandRange(-0.08, 0.08));
			AddChild(instance);
		}
	}
}
