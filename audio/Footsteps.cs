using Godot;

/// <summary>
/// Plays a footstep that matches the ground under it. The ground is the surface of a <see cref="SurfaceZone"/>
/// around the feet if there is one, otherwise the "surface" metadata of the floor below.
/// Place it at the walker's feet; the walker decides when a foot lands.
/// </summary>
public partial class Footsteps : AudioStreamPlayer3D
{
	private const uint WorldLayer = 1;
	private const uint ZonesLayer = 1 << 4;

	/// <summary>Footstep sounds for each surface name, like "tile" or "grass".</summary>
	[Export] public Godot.Collections.Dictionary<StringName, AudioStream> Surfaces { get; set; } = new();
	/// <summary>Used where the ground doesn't say what it is.</summary>
	[Export] public StringName DefaultSurface { get; set; } = "concrete";

	public void Step(float volumeDb)
	{
		AudioStream sound = Surfaces.TryGetValue(SurfaceUnderFeet(), out AudioStream surfaceSound)
			? surfaceSound
			: Surfaces[DefaultSurface];

		// Changing the stream stops the step that is still ringing out, so only change it on a new surface.
		if (Stream != sound)
			Stream = sound;
		VolumeDb = volumeDb;
		Play();
	}

	private StringName SurfaceUnderFeet()
	{
		PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
		Vector3 feet = GlobalPosition;

		var zoneQuery = new PhysicsPointQueryParameters3D
		{
			Position = feet + Vector3.Up * 0.1f,
			CollideWithAreas = true,
			CollideWithBodies = false,
			CollisionMask = ZonesLayer,
		};
		foreach (Godot.Collections.Dictionary hit in space.IntersectPoint(zoneQuery))
		{
			if (hit["collider"].AsGodotObject() is SurfaceZone zone)
				return zone.Surface;
		}

		var floorQuery = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * 0.3f, feet + Vector3.Down * 0.5f, WorldLayer);
		Godot.Collections.Dictionary floor = space.IntersectRay(floorQuery);
		if (floor.Count > 0 && floor["collider"].AsGodotObject() is Node floorNode)
			return floorNode.GetMeta("surface", DefaultSurface).AsStringName();

		return DefaultSurface;
	}
}
