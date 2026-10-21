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

	[ExportGroup("Auto Gain")]
	/// <summary>Closer than this, the infrared light is turned down so nearby things don't burn out.</summary>
	[Export] public float AutoGainDistance { get; set; } = 2f;
	[Export(PropertyHint.Range, "0,1")] public float MinimumGain { get; set; } = 0.08f;
	[Export] public float GainAdjustSpeed { get; set; } = 8f;

	[ExportGroup("Nodes")]
	[Export] public Light3D InfraredLight { get; set; }
	[Export] public RayCast3D AutoGainProbe { get; set; }
	[Export] public ColorRect ScreenEffect { get; set; }

	public bool IsRaised { get; private set; }
	public bool IsNightVisionOn { get; private set; }
	public float Charge { get; private set; } = 1f;
	public int SpareBatteries { get; private set; }

	private ShaderMaterial _screenMaterial;
	private float _infraredEnergy;
	private float _gain = 1f;

	public override void _Ready()
	{
		_screenMaterial = (ShaderMaterial)ScreenEffect.Material;
		_infraredEnergy = InfraredLight.LightEnergy;
		AutoGainProbe.TargetPosition = Vector3.Forward * AutoGainDistance;
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

		UpdateAutoGain((float)delta);
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

	private void UpdateAutoGain(float delta)
	{
		// Like a real camera, turn the infrared light down when something is right in front of the lens.
		float distance = AutoGainProbe.IsColliding()
			? AutoGainProbe.GlobalPosition.DistanceTo(AutoGainProbe.GetCollisionPoint())
			: AutoGainDistance;
		float targetGain = Mathf.Clamp(Mathf.Pow(distance / AutoGainDistance, 2f), MinimumGain, 1f);
		_gain = Mathf.Lerp(_gain, targetGain, GainAdjustSpeed * delta);
		InfraredLight.LightEnergy = _infraredEnergy * _gain;
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
