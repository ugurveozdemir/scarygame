using System.Linq;
using Godot;

/// <summary>
/// The background sound of wherever the listener is: the building's hum indoors and the night outdoors.
/// Indoors the night stays faintly audible, coming in through the open windows and doors.
/// Now and then an owl or a dog calls from somewhere far out in the dark.
/// </summary>
public partial class Ambience : Node
{
	/// <summary>The area covering the inside of the building.</summary>
	[Export] public Area3D Indoors { get; set; }
	[Export] public AudioStreamPlayer BuildingTone { get; set; }
	[Export] public AudioStreamPlayer Night { get; set; }
	[Export(PropertyHint.Range, "0,1")] public float NightVolumeIndoors { get; set; } = 0.15f;
	[Export] public float BlendSeconds { get; set; } = 1.5f;

	[ExportGroup("Distant Calls")]
	[Export] public AudioStreamPlayer3D DistantCall { get; set; }
	[Export] public float MinCallSeconds { get; set; } = 20f;
	[Export] public float MaxCallSeconds { get; set; } = 50f;
	/// <summary>How far from the listener a call comes from, in a random direction.</summary>
	[Export] public float CallDistance { get; set; } = 50f;

	private float _buildingToneVolume;
	private float _nightVolume;
	private float _callVolume;
	private float _timeToNextCall;
	private float _indoors;
	private bool _hasStarted;

	public override void _Ready()
	{
		_buildingToneVolume = BuildingTone.VolumeLinear;
		_nightVolume = Night.VolumeLinear;
		_callVolume = DistantCall.VolumeLinear;
		_timeToNextCall = (float)GD.RandRange(MinCallSeconds, MaxCallSeconds);
	}

	public override void _Process(double delta)
	{
		float target = IsListenerIndoors() ? 1f : 0f;
		// Start where the listener is instead of fading in from outdoors.
		_indoors = _hasStarted ? Mathf.MoveToward(_indoors, target, (float)delta / BlendSeconds) : target;
		_hasStarted = true;

		float outdoors = Mathf.Lerp(1f, NightVolumeIndoors, _indoors);
		BuildingTone.VolumeLinear = _buildingToneVolume * _indoors;
		Night.VolumeLinear = _nightVolume * outdoors;

		_timeToNextCall -= (float)delta;
		if (_timeToNextCall <= 0f)
		{
			CallFromTheDark(outdoors);
			_timeToNextCall = (float)GD.RandRange(MinCallSeconds, MaxCallSeconds);
		}
	}

	private void CallFromTheDark(float outdoors)
	{
		Vector3 listener = GetViewport().GetCamera3D().GlobalPosition;
		Vector3 direction = Vector3.Forward.Rotated(Vector3.Up, (float)GD.RandRange(0, Mathf.Tau));
		DistantCall.GlobalPosition = listener + direction * CallDistance + Vector3.Up * 5f;
		DistantCall.VolumeLinear = _callVolume * outdoors;
		DistantCall.Play();
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
