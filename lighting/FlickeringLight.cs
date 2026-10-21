using Godot;

/// <summary>
/// Makes a light flicker like a dying fluorescent tube by randomly changing its energy.
/// </summary>
public partial class FlickeringLight : OmniLight3D
{
	[Export] public float MinInterval { get; set; } = 0.05f;
	[Export] public float MaxInterval { get; set; } = 0.4f;
	[Export(PropertyHint.Range, "0,1")] public float OffChance { get; set; } = 0.3f;

	private float _baseEnergy;
	private float _timeUntilChange;

	public override void _Ready()
	{
		_baseEnergy = LightEnergy;
	}

	public override void _Process(double delta)
	{
		_timeUntilChange -= (float)delta;
		if (_timeUntilChange > 0f)
			return;

		_timeUntilChange = (float)GD.RandRange(MinInterval, MaxInterval);
		LightEnergy = GD.Randf() < OffChance ? 0f : _baseEnergy * (float)GD.RandRange(0.6, 1.0);
	}
}
