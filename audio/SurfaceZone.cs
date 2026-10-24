using Godot;

/// <summary>
/// Marks where the ground sounds different from the body it belongs to, like a paved path lying on the lawn
/// or a vent cut into the building. <see cref="Footsteps"/> prefer a zone's surface over the floor's own.
/// Zones live on the "zones" physics layer.
/// </summary>
public partial class SurfaceZone : Area3D
{
	/// <summary>The surface name, which picks the footstep sounds, like "paving" or "duct".</summary>
	[Export] public StringName Surface { get; set; } = "concrete";
}
