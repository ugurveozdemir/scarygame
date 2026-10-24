using System.Collections.Generic;
using Godot;

/// <summary>
/// A chain-link fence along a line of corner points: a mesh panel between posts every couple of metres,
/// drawn with one multimesh for the panels and one for the posts, and a solid wall along each side so nobody
/// passes through. The panels are stretched slightly so each side ends exactly on its corner.
/// The wire rattles where someone walks into it.
/// </summary>
[Tool]
public partial class Fence : Node3D
{
	/// <summary>The modular fence model the panel and post meshes are taken from.</summary>
	[Export] public PackedScene Model { get; set; }
	[Export] public string PanelMesh { get; set; } = "modular_chainlink_fence_double";
	[Export] public string PostMesh { get; set; } = "modular_chainlink_fence_post";
	/// <summary>How long one panel is; it reaches from its post along the local -X axis.</summary>
	[Export] public float PanelLength { get; set; } = 1.91f;
	[Export] public float Height { get; set; } = 2.5f;
	/// <summary>Corners of the fence in this node's space, on the ground.</summary>
	[Export] public Vector3[] Corners { get; set; } = System.Array.Empty<Vector3>();
	[Export] public AudioStream RattleSound { get; set; }
	/// <summary>How close to the wire someone has to come to set it rattling.</summary>
	[Export] public float RattleDistance { get; set; } = 0.6f;

	private AudioStreamPlayer3D _rattle;

	public override void _Ready()
	{
		if (Model is null || Corners.Length < 2)
			return;

		Node3D model = Model.Instantiate<Node3D>();
		Mesh panel = model.GetNode<MeshInstance3D>(PanelMesh).Mesh;
		Mesh post = model.GetNode<MeshInstance3D>(PostMesh).Mesh;
		model.Free();

		var panels = new List<Transform3D>();
		var posts = new List<Transform3D> { new(Basis.Identity, Corners[0]) };
		var walls = new StaticBody3D { Name = "Walls" };
		// Walking into the fence means coming this close to a wall, so the area around the walls hears it.
		var touch = new Area3D { Name = "Touch", CollisionLayer = 0, CollisionMask = 2 };
		for (int i = 1; i < Corners.Length; i++)
			AddSide(Corners[i - 1], Corners[i], panels, posts, walls, touch);

		AddChild(Instances("Panels", panel, panels));
		AddChild(Instances("Posts", post, posts));
		AddChild(walls);
		AddChild(touch);
		if (Engine.IsEditorHint() || RattleSound is null)
			return;

		_rattle = new AudioStreamPlayer3D { Name = "Rattle", Stream = RattleSound, UnitSize = 3f, MaxDistance = 25f, Bus = "Sfx" };
		AddChild(_rattle);
		touch.BodyEntered += Rattle;
	}

	private void Rattle(Node3D body)
	{
		_rattle.GlobalPosition = body.GlobalPosition + Vector3.Up;
		_rattle.Play();
	}

	private void AddSide(Vector3 from, Vector3 to, List<Transform3D> panels, List<Transform3D> posts, StaticBody3D walls, Area3D touch)
	{
		Vector3 along = to - from;
		float length = along.Length();
		int count = Mathf.Max(1, Mathf.RoundToInt(length / PanelLength));
		Vector3 direction = along / length;
		// The panel's -X axis points back along the side, towards the previous post.
		Basis basis = new Basis(Vector3.Up, Mathf.Atan2(direction.Z, -direction.X))
			* Basis.FromScale(new Vector3(length / count / PanelLength, 1f, 1f));
		for (int i = 1; i <= count; i++)
		{
			Vector3 position = from + along * i / count;
			panels.Add(new Transform3D(basis, position));
			posts.Add(new Transform3D(Basis.Identity, position));
		}

		var placement = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(-direction.Z, direction.X)), (from + to) / 2f + Vector3.Up * Height / 2f);
		walls.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(length, Height, 0.1f) }, Transform = placement });
		touch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(length, Height, RattleDistance * 2f) }, Transform = placement });
	}

	private static MultiMeshInstance3D Instances(string name, Mesh mesh, List<Transform3D> transforms)
	{
		var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh };
		multimesh.InstanceCount = transforms.Count;
		for (int i = 0; i < transforms.Count; i++)
			multimesh.SetInstanceTransform(i, transforms[i]);
		return new MultiMeshInstance3D { Name = name, Multimesh = multimesh };
	}
}
