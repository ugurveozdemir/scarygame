using Godot;

/// <summary>
/// A red warning light that stays dark until the alarm sounds, then pulses like a rotating beacon.
/// </summary>
public partial class AlarmLight : OmniLight3D
{
	[Export] public float PulsesPerSecond { get; set; } = 1.2f;

	private float _baseEnergy;
	private float _time;

	public override void _Ready()
	{
		_baseEnergy = LightEnergy;
		Visible = false;
		SetProcess(false);
	}

	public void Sound()
	{
		Visible = true;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		float pulse = 0.5f + 0.5f * Mathf.Sin(_time * PulsesPerSecond * Mathf.Tau);
		LightEnergy = _baseEnergy * pulse * pulse;
	}
}
