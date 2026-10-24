using Godot;

/// <summary>
/// A sound that happens now and then, at random intervals: a pipe knocking, a fence rattling in the wind,
/// something falling somewhere far off in the building.
/// </summary>
public partial class RandomSound : AudioStreamPlayer3D
{
	[Export] public float MinSeconds { get; set; } = 20f;
	[Export] public float MaxSeconds { get; set; } = 60f;

	private float _timeLeft;

	public override void _Ready()
	{
		_timeLeft = (float)GD.RandRange(MinSeconds, MaxSeconds);
	}

	public override void _Process(double delta)
	{
		_timeLeft -= (float)delta;
		if (_timeLeft > 0f)
			return;

		Play();
		_timeLeft = (float)GD.RandRange(MinSeconds, MaxSeconds);
	}
}
