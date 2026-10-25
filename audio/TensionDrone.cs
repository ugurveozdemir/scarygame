using Godot;

/// <summary>
/// A low, uneasy drone under everything. It swells while the enemy searches nearby and makes way for the chase
/// music while it hunts the player.
/// </summary>
public partial class TensionDrone : Node
{
	[Export] public Enemy Enemy { get; set; }
	[Export] public Node3D Player { get; set; }
	[Export] public AudioStreamPlayer Drone { get; set; }
	/// <summary>How loud the drone is while nothing is happening, relative to its scene level.</summary>
	[Export(PropertyHint.Range, "0,1")] public float CalmVolume { get; set; } = 0.35f;
	/// <summary>Within this distance a searching enemy makes the drone swell.</summary>
	[Export] public float SearchDistance { get; set; } = 20f;
	[Export] public float BlendSeconds { get; set; } = 3f;

	private float _volume;
	private float _level;

	public override void _Ready()
	{
		_volume = Drone.VolumeLinear;
		_level = CalmVolume;
	}

	public override void _Process(double delta)
	{
		_level = Mathf.MoveToward(_level, TargetLevel(), (float)delta / BlendSeconds);
		Drone.VolumeLinear = _volume * _level;
	}

	private float TargetLevel()
	{
		switch (Enemy.State)
		{
			case Enemy.EnemyState.Chase or Enemy.EnemyState.Attack:
				return 0f;
			case Enemy.EnemyState.Investigate or Enemy.EnemyState.Search when Player.GlobalPosition.DistanceTo(Enemy.GlobalPosition) < SearchDistance:
				return 1f;
			default:
				return CalmVolume;
		}
	}
}
