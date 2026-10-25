using Godot;

/// <summary>
/// Tally marks scratched into a wall, one for every trial before this one, in groups of five with the fifth
/// struck across the other four. The wall faces its local +Z.
/// </summary>
public partial class TallyMarks : Node3D
{
	[Export] public float MarkLength { get; set; } = 0.3f;
	[Export] public float MarkSpacing { get; set; } = 0.06f;
	[Export] public float GroupSpacing { get; set; } = 0.12f;
	[Export] public Material Material { get; set; }

	public override void _Ready()
	{
		var mark = new BoxMesh { Size = new Vector3(0.018f, MarkLength, 0.004f), Material = Material };
		int count = GameState.Trial - 1;
		float groupWidth = 3 * MarkSpacing;
		for (int i = 0; i < count; i++)
		{
			int group = i / 5;
			int inGroup = i % 5;
			float groupStart = group * (groupWidth + GroupSpacing);
			bool isStrike = inGroup == 4;

			var instance = new MeshInstance3D { Mesh = mark };
			if (isStrike)
			{
				instance.Position = new Vector3(groupStart + groupWidth / 2f, 0f, 0f);
				// Long enough to cross all four marks diagonally.
				instance.Scale = new Vector3(1f, 1.15f, 1f);
				instance.RotateZ(-Mathf.Atan2(groupWidth, MarkLength));
			}
			else
			{
				instance.Position = new Vector3(groupStart + inGroup * MarkSpacing, (float)GD.RandRange(-0.01, 0.01), 0f);
				instance.RotateZ((float)GD.RandRange(-0.06, 0.06));
			}
			AddChild(instance);
		}
	}
}
