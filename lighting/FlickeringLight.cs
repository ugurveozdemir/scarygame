using Godot;

/// <summary>
/// Makes a light flicker like a dying fluorescent tube by randomly changing its energy.
/// It buzzes as brightly as it shines and crackles each time it comes back on.
/// </summary>
public partial class FlickeringLight : OmniLight3D
{
	[Export] public float MinInterval { get; set; } = 0.05f;
	[Export] public float MaxInterval { get; set; } = 0.4f;
	[Export(PropertyHint.Range, "0,1")] public float OffChance { get; set; } = 0.3f;
	[Export] public AudioStreamPlayer3D Buzz { get; set; }
	[Export] public AudioStreamPlayer3D Crackle { get; set; }

	private float _baseEnergy;
	private float _buzzVolume;
	private float _timeUntilChange;

	public override void _Ready()
	{
		_baseEnergy = LightEnergy;
		_buzzVolume = Buzz.VolumeLinear;
	}

	public override void _Process(double delta)
	{
		_timeUntilChange -= (float)delta;
		if (_timeUntilChange > 0f)
			return;

		_timeUntilChange = (float)GD.RandRange(MinInterval, MaxInterval);
		bool wasOff = LightEnergy == 0f;
		LightEnergy = GD.Randf() < OffChance ? 0f : _baseEnergy * (float)GD.RandRange(0.6, 1.0);

		Buzz.VolumeLinear = _buzzVolume * LightEnergy / _baseEnergy;
		if (wasOff && LightEnergy > 0f)
			Crackle.Play();
	}
}
