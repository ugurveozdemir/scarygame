using Godot;

/// <summary>
/// The player's handheld camcorder. Raising it shows the recording HUD, and its night vision
/// lets the player see in the dark at the cost of battery charge.
/// </summary>
public partial class Camcorder : Node3D
{
	private readonly record struct ScreenLook(float Grain, float Vignette, float Scanlines, float ChromaticAberration);

	private static readonly ScreenLook NakedEyeLook = new(Grain: 0.03f, Vignette: 0.3f, Scanlines: 0f, ChromaticAberration: 0f);
	private static readonly ScreenLook CamcorderLook = new(Grain: 0.06f, Vignette: 0.4f, Scanlines: 0.08f, ChromaticAberration: 0.0015f);
	private static readonly ScreenLook NightVisionLook = new(Grain: 0.15f, Vignette: 0.6f, Scanlines: 0.08f, ChromaticAberration: 0.0015f);

	[Export] public float BatteryLifeSeconds { get; set; } = 120f;
	[Export] public int MaxSpareBatteries { get; set; } = 10;
	[Export] public Light3D InfraredLight { get; set; }
	[Export] public ColorRect ScreenEffect { get; set; }

	public bool IsRaised { get; private set; }
	public bool IsNightVisionOn { get; private set; }
	public float Charge { get; private set; } = 1f;
	public int SpareBatteries { get; private set; }

	private ShaderMaterial _screenMaterial;

	public override void _Ready()
	{
		_screenMaterial = (ShaderMaterial)ScreenEffect.Material;
		UpdateEffects();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("toggle_camcorder"))
			SetRaised(!IsRaised);
		else if (@event.IsActionPressed("toggle_night_vision"))
			SetNightVision(!IsNightVisionOn);
		else if (@event.IsActionPressed("reload_battery"))
			ReloadBattery();
	}

	public override void _Process(double delta)
	{
		if (!IsNightVisionOn)
			return;

		Charge = Mathf.Max(Charge - (float)delta / BatteryLifeSeconds, 0f);
		if (Charge == 0f)
			SetNightVision(false);
	}

	public bool TryAddSpareBattery()
	{
		if (SpareBatteries >= MaxSpareBatteries)
			return false;

		SpareBatteries++;
		return true;
	}

	private void SetRaised(bool raised)
	{
		IsRaised = raised;
		if (!IsRaised)
			IsNightVisionOn = false;
		UpdateEffects();
	}

	private void SetNightVision(bool enabled)
	{
		// Night vision only works through the raised camcorder and needs charge.
		IsNightVisionOn = enabled && IsRaised && Charge > 0f;
		UpdateEffects();
	}

	private void ReloadBattery()
	{
		if (SpareBatteries == 0 || Charge == 1f)
			return;

		SpareBatteries--;
		Charge = 1f;
	}

	private void UpdateEffects()
	{
		InfraredLight.Visible = IsNightVisionOn;

		ScreenLook look = IsNightVisionOn ? NightVisionLook
			: IsRaised ? CamcorderLook
			: NakedEyeLook;
		_screenMaterial.SetShaderParameter("night_vision", IsNightVisionOn);
		_screenMaterial.SetShaderParameter("grain_amount", look.Grain);
		_screenMaterial.SetShaderParameter("vignette_strength", look.Vignette);
		_screenMaterial.SetShaderParameter("scanline_strength", look.Scanlines);
		_screenMaterial.SetShaderParameter("chromatic_aberration", look.ChromaticAberration);
	}
}
