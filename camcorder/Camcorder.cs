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
	/// <summary>How charged the battery is when the level starts.</summary>
	[Export(PropertyHint.Range, "0,1")] public float StartCharge { get; set; } = 1f;
	[Export] public int MaxSpareBatteries { get; set; } = 10;

	/// <summary>Below this charge the camcorder beeps every <see cref="LowBatteryBeepSeconds"/>.</summary>
	[Export(PropertyHint.Range, "0,1")] public float LowCharge { get; set; } = 0.25f;
	[Export] public float LowBatteryBeepSeconds { get; set; } = 8f;

	[ExportGroup("Auto Gain")]
	/// <summary>Closer than this, the infrared light is turned down so nearby things don't burn out.</summary>
	[Export] public float AutoGainDistance { get; set; } = 2f;
	[Export(PropertyHint.Range, "0,1")] public float MinimumGain { get; set; } = 0.08f;
	[Export] public float GainAdjustSpeed { get; set; } = 8f;

	[ExportGroup("Nodes")]
	/// <summary>The player's view, which only shows what night vision reveals while it is on.</summary>
	[Export] public Camera3D Camera { get; set; }
	[Export] public Light3D InfraredLight { get; set; }
	[Export] public RayCast3D AutoGainProbe { get; set; }
	[Export] public ColorRect ScreenEffect { get; set; }

	[ExportGroup("Sounds")]
	[Export] public AudioStreamPlayer RaiseSound { get; set; }
	[Export] public AudioStreamPlayer LowerSound { get; set; }
	[Export] public AudioStreamPlayer NightVisionOnSound { get; set; }
	[Export] public AudioStreamPlayer NightVisionOffSound { get; set; }
	/// <summary>The faint whine of the night vision while it is on.</summary>
	[Export] public AudioStreamPlayer NightVisionHum { get; set; }
	[Export] public AudioStreamPlayer LowBatterySound { get; set; }
	/// <summary>The night vision dying when the battery runs out.</summary>
	[Export] public AudioStreamPlayer BatteryDeadSound { get; set; }
	[Export] public AudioStreamPlayer ReloadSound { get; set; }
	[Export] public AudioStreamPlayer PickUpBatterySound { get; set; }
	/// <summary>R pressed without a spare battery: just the empty click of the latch.</summary>
	[Export] public AudioStreamPlayer NoBatterySound { get; set; }
	/// <summary>The barely audible tape motor while recording.</summary>
	[Export] public AudioStreamPlayer TapeMotor { get; set; }
	/// <summary>The camcorder powering up when the level starts.</summary>
	[Export] public AudioStreamPlayer PowerOnSound { get; set; }

	public bool IsRaised { get; private set; }
	public bool IsNightVisionOn { get; private set; }
	public float Charge { get; private set; } = 1f;
	public int SpareBatteries { get; private set; }

	/// <summary>How many times faster than normal night vision drains the battery, like near the Remnant.</summary>
	public float DrainMultiplier { get; set; } = 1f;
	/// <summary>How badly the picture breaks up, from 0 for a clean picture to 1.</summary>
	public float Interference { get; set; }

	private ShaderMaterial _screenMaterial;
	private float _infraredEnergy;
	private float _gain = 1f;
	private float _lowBatteryBeepTimeLeft;

	public override void _Ready()
	{
		Charge = StartCharge;
		Camera.CullMask &= ~VisualLayers.Past;
		_screenMaterial = (ShaderMaterial)ScreenEffect.Material;
		_infraredEnergy = InfraredLight.LightEnergy;
		AutoGainProbe.TargetPosition = Vector3.Forward * AutoGainDistance;
		UpdateEffects();
		PowerOnSound.Play();
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
		_screenMaterial.SetShaderParameter("interference", Interference);
		if (!IsNightVisionOn)
			return;

		Charge = Mathf.Max(Charge - (float)delta * DrainMultiplier / BatteryLifeSeconds, 0f);
		if (Charge == 0f)
		{
			BatteryDeadSound.Play();
			SetNightVision(false, playSound: false);
			return;
		}

		WarnOfLowBattery((float)delta);
		UpdateAutoGain((float)delta);
	}

	public bool TryAddSpareBattery()
	{
		if (SpareBatteries >= MaxSpareBatteries)
			return false;

		SpareBatteries++;
		PickUpBatterySound.Play();
		return true;
	}

	private void SetRaised(bool raised)
	{
		IsRaised = raised;
		if (!IsRaised)
			IsNightVisionOn = false;
		(IsRaised ? RaiseSound : LowerSound).Play();
		UpdateEffects();
	}

	private void SetNightVision(bool enabled, bool playSound = true)
	{
		// Night vision only works through the raised camcorder and needs charge.
		bool wasOn = IsNightVisionOn;
		IsNightVisionOn = enabled && IsRaised && Charge > 0f;
		if (playSound && IsNightVisionOn != wasOn)
			(IsNightVisionOn ? NightVisionOnSound : NightVisionOffSound).Play();
		UpdateEffects();
	}

	private void ReloadBattery()
	{
		if (Charge == 1f)
			return;

		if (SpareBatteries == 0)
		{
			NoBatterySound.Play();
			return;
		}

		SpareBatteries--;
		Charge = 1f;
		_lowBatteryBeepTimeLeft = 0f;
		ReloadSound.Play();
	}

	private void WarnOfLowBattery(float delta)
	{
		if (Charge > LowCharge)
			return;

		// The first beep comes as soon as the charge gets low.
		_lowBatteryBeepTimeLeft -= delta;
		if (_lowBatteryBeepTimeLeft <= 0f)
		{
			LowBatterySound.Play();
			_lowBatteryBeepTimeLeft = LowBatteryBeepSeconds;
		}
	}

	private static void KeepPlaying(AudioStreamPlayer loop, bool playing)
	{
		if (playing && !loop.Playing)
			loop.Play();
		else if (!playing)
			loop.Stop();
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
		Camera.CullMask = IsNightVisionOn ? Camera.CullMask | VisualLayers.NightVisionOnly : Camera.CullMask & ~VisualLayers.NightVisionOnly;
		KeepPlaying(NightVisionHum, IsNightVisionOn);
		KeepPlaying(TapeMotor, IsRaised);

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
