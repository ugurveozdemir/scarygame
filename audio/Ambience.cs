using System.Linq;
using Godot;

/// <summary>
/// The background sound of wherever the listener is: the building's hum indoors and the night outdoors.
/// Indoors the night stays faintly audible, coming in through the open windows and doors.
/// </summary>
public partial class Ambience : Node
{
	/// <summary>The area covering the inside of the building.</summary>
	[Export] public Area3D Indoors { get; set; }
	[Export] public AudioStreamPlayer BuildingTone { get; set; }
	[Export] public AudioStreamPlayer Night { get; set; }
	[Export(PropertyHint.Range, "0,1")] public float NightVolumeIndoors { get; set; } = 0.15f;
	[Export] public float BlendSeconds { get; set; } = 1.5f;

	private float _buildingToneVolume;
	private float _nightVolume;
	private float _indoors;
	private bool _hasStarted;

	public override void _Ready()
	{
		_buildingToneVolume = BuildingTone.VolumeLinear;
		_nightVolume = Night.VolumeLinear;
	}

	public override void _Process(double delta)
	{
		float target = IsListenerIndoors() ? 1f : 0f;
		// Start where the listener is instead of fading in from outdoors.
		_indoors = _hasStarted ? Mathf.MoveToward(_indoors, target, (float)delta / BlendSeconds) : target;
		_hasStarted = true;

		BuildingTone.VolumeLinear = _buildingToneVolume * _indoors;
		Night.VolumeLinear = _nightVolume * Mathf.Lerp(1f, NightVolumeIndoors, _indoors);
	}

	private bool IsListenerIndoors()
	{
		var query = new PhysicsPointQueryParameters3D
		{
			Position = GetViewport().GetCamera3D().GlobalPosition,
			CollideWithAreas = true,
			CollideWithBodies = false,
			CollisionMask = Indoors.CollisionLayer,
		};
		return Indoors.GetWorld3D().DirectSpaceState.IntersectPoint(query)
			.Any(hit => hit["collider"].AsGodotObject() == Indoors);
	}
}
