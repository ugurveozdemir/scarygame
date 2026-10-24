using Godot;

/// <summary>
/// How scared the player is, heard from inside their head. The closer the enemy and the more it hunts them,
/// the faster and harder their heart pounds and the more ragged their breathing. When it spots them, they gasp.
/// </summary>
public partial class Dread : Node
{
	[Export] public Player Player { get; set; }
	[Export] public Enemy Enemy { get; set; }
	[Export] public Breathing Breathing { get; set; }
	/// <summary>A single heartbeat, played once per beat so the pace can change freely.</summary>
	[Export] public AudioStreamPlayer Heartbeat { get; set; }
	[Export] public AudioStreamPlayer Gasp { get; set; }

	/// <summary>Within this distance the enemy starts to frighten the player, more the closer it gets.</summary>
	[Export] public float FearDistance { get; set; } = 14f;
	[Export] public float CalmBeatSeconds { get; set; } = 1.1f;
	[Export] public float PanicBeatSeconds { get; set; } = 0.42f;
	[Export] public float RiseSeconds { get; set; } = 1.5f;
	[Export] public float FadeSeconds { get; set; } = 8f;

	private float _fear;
	private float _heartbeatVolume;
	private float _timeToNextBeat;

	public override void _Ready()
	{
		_heartbeatVolume = Heartbeat.VolumeLinear;
		Enemy.StateChanged += OnEnemyStateChanged;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		float target = TargetFear();
		_fear = Mathf.MoveToward(_fear, target, dt / (target > _fear ? RiseSeconds : FadeSeconds));
		Breathing.Fear = _fear;

		_timeToNextBeat -= dt;
		if (_fear > 0.05f && _timeToNextBeat <= 0f)
		{
			Heartbeat.VolumeLinear = _heartbeatVolume * _fear;
			Heartbeat.Play();
			_timeToNextBeat = Mathf.Lerp(CalmBeatSeconds, PanicBeatSeconds, _fear);
		}
	}

	private float TargetFear()
	{
		if (Enemy.State is Enemy.EnemyState.Chase or Enemy.EnemyState.Strangling)
			return 1f;

		float closeness = 1f - Player.GlobalPosition.DistanceTo(Enemy.GlobalPosition) / FearDistance;
		return Mathf.Clamp(closeness, 0f, 1f);
	}

	private void OnEnemyStateChanged(Enemy.EnemyState state)
	{
		if (state == Enemy.EnemyState.Chase)
			Gasp.Play();
	}
}
