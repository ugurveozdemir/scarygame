using Godot;

/// <summary>
/// Broken glass scattered over the floor in a box around the node, as one multimesh. Pair it with a "glass"
/// surface zone so stepping on it crunches.
/// </summary>
public partial class GlassShards : MultiMeshInstance3D
{
	[Export] public int Count { get; set; } = 60;
	[Export] public Vector2 AreaSize { get; set; } = new(2f, 2f);
	[Export] public Material Material { get; set; }

	public override void _Ready()
	{
		var shards = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			Mesh = new PrismMesh { Size = new Vector3(0.05f, 0.004f, 0.04f), Material = Material },
			InstanceCount = Count,
		};
		for (int i = 0; i < Count; i++)
		{
			float scale = (float)GD.RandRange(0.4, 1.6);
			var basis = new Basis(Vector3.Up, (float)GD.RandRange(0, Mathf.Tau)).Scaled(new Vector3(scale, 1f, scale));
			var position = new Vector3((float)GD.RandRange(-AreaSize.X / 2f, AreaSize.X / 2f), 0.002f,
				(float)GD.RandRange(-AreaSize.Y / 2f, AreaSize.Y / 2f));
			shards.SetInstanceTransform(i, new Transform3D(basis, position));
		}
		Multimesh = shards;
	}
}
