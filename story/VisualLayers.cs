using Godot;

/// <summary>
/// The render layers that decide who sees what. The player's eyes see the world and the enemies; night vision
/// also shows what the naked eye can't; recordings of earlier trials show the world as it was back then.
/// </summary>
public static class VisualLayers
{
	public const uint World = 1 << 0;
	public const uint Enemies = 1 << 1;
	/// <summary>Only visible through the camcorder's night vision, like the Remnant.</summary>
	public const uint NightVisionOnly = 1 << 2;
	/// <summary>Only in recordings of earlier trials, like a note that has since been torn off.</summary>
	public const uint Past = 1 << 10;

	/// <summary>Puts every mesh under <paramref name="root"/> on <paramref name="layers"/>.</summary>
	public static void Assign(Node root, uint layers)
	{
		if (root is VisualInstance3D visual)
			visual.Layers = layers;
		foreach (Node child in root.GetChildren())
			Assign(child, layers);
	}
}
