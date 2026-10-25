using Godot;

/// <summary>
/// A security monitor showing what one of the clinic's hidden cameras sees, live, in black-and-white infrared
/// that sees further into the dark than the eye. It only renders while the player is close enough to watch it.
/// </summary>
public partial class SecurityFeed : Node
{
	[Export] public string CameraName { get; set; } = "";
	[Export] public float WatchDistance { get; set; } = 6f;
	[Export] public Player Player { get; set; }
	/// <summary>How much brighter the infrared camera sees the night than the eye.</summary>
	[Export] public float InfraredExposure { get; set; } = 5f;
	[Export] public SubViewport Picture { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public Label Overlay { get; set; }
	[Export] public GeometryInstance3D Screen { get; set; }

	private double _clockSeconds = 21 * 3600 + 4 * 60;

	public override void _Ready()
	{
		var material = (ShaderMaterial)Screen.MaterialOverride.Duplicate();
		material.SetShaderParameter("picture", Picture.GetTexture());
		material.SetShaderParameter("snow", 0.04f);
		material.SetShaderParameter("monochrome", true);
		Screen.MaterialOverride = material;

		var infrared = (Environment)GetViewport().World3D.Environment.Duplicate();
		infrared.TonemapExposure *= InfraredExposure;
		infrared.VolumetricFogEnabled = false;
		infrared.FogDensity *= 0.3f;
		Camera.Environment = infrared;
	}

	public override void _Process(double delta)
	{
		_clockSeconds += delta;
		bool isWatched = Player.GlobalPosition.DistanceTo(Screen.GlobalPosition) <= WatchDistance;
		Picture.RenderTargetUpdateMode = isWatched ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
		if (isWatched)
			Overlay.Text = $"{CameraName}\n{System.TimeSpan.FromSeconds(_clockSeconds):hh\\:mm\\:ss}";
	}
}
