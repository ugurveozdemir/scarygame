using Godot;

/// <summary>
/// The enemy's breathing and cries. It always breathes loud enough to be tracked by ear, slow and heavy while
/// it walks around and ragged while it hunts. It roars when it spots the player, grunts when a noise makes it
/// suspicious, snorts while it looks around for them and growls when it gives up the search after a chase.
/// On its rounds it now and then whistles a tuneless melody to itself.
/// </summary>
public partial class EnemyVoice : Node
{
	[Export] public Enemy Enemy { get; set; }
	[Export] public AudioStreamPlayer3D CalmBreath { get; set; }
	[Export] public AudioStreamPlayer3D HuntingBreath { get; set; }
	[Export] public AudioStreamPlayer3D SpotRoar { get; set; }
	[Export] public AudioStreamPlayer3D SuspiciousGrunt { get; set; }
	[Export] public AudioStreamPlayer3D SearchSnort { get; set; }
	[Export] public AudioStreamPlayer3D GiveUpGrowl { get; set; }
	[Export] public AudioStreamPlayer3D PatrolWhistle { get; set; }
	[Export] public float MinWhistleSeconds { get; set; } = 25f;
	[Export] public float MaxWhistleSeconds { get; set; } = 60f;
	[Export] public float BreathBlendSeconds { get; set; } = 1f;
	[Export] public float MinSnortSeconds { get; set; } = 2.5f;
	[Export] public float MaxSnortSeconds { get; set; } = 5f;

	private float _calmVolume;
	private float _huntingVolume;
	private float _hunting;
	private Enemy.EnemyState _previousState;
	private bool _hasChased;
	private float _timeToNextSnort;
	private float _timeToNextWhistle;

	public override void _Ready()
	{
		_calmVolume = CalmBreath.VolumeLinear;
		_huntingVolume = HuntingBreath.VolumeLinear;
		_previousState = Enemy.State;
		_timeToNextWhistle = (float)GD.RandRange(MinWhistleSeconds, MaxWhistleSeconds);
		Enemy.StateChanged += OnStateChanged;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		bool isHunting = Enemy.State is Enemy.EnemyState.Chase or Enemy.EnemyState.Strangling;
		_hunting = Mathf.MoveToward(_hunting, isHunting ? 1f : 0f, dt / BreathBlendSeconds);
		CalmBreath.VolumeLinear = _calmVolume * (1f - _hunting);
		HuntingBreath.VolumeLinear = _huntingVolume * _hunting;

		// Standing still while investigating means it has arrived and is looking around.
		bool isLookingAround = Enemy.State == Enemy.EnemyState.Investigate && Enemy.Velocity.LengthSquared() < 0.01f;
		_timeToNextSnort -= dt;
		if (isLookingAround && _timeToNextSnort <= 0f)
		{
			SearchSnort.Play();
			_timeToNextSnort = (float)GD.RandRange(MinSnortSeconds, MaxSnortSeconds);
		}

		_timeToNextWhistle -= dt;
		if (Enemy.State == Enemy.EnemyState.Patrol && _timeToNextWhistle <= 0f)
		{
			PatrolWhistle.Play();
			_timeToNextWhistle = (float)GD.RandRange(MinWhistleSeconds, MaxWhistleSeconds);
		}
	}

	private void OnStateChanged(Enemy.EnemyState state)
	{
		if (state != Enemy.EnemyState.Patrol)
			PatrolWhistle.Stop();

		switch (state)
		{
			case Enemy.EnemyState.Chase:
				SpotRoar.Play();
				_hasChased = true;
				break;
			case Enemy.EnemyState.Investigate when _previousState == Enemy.EnemyState.Patrol:
				SuspiciousGrunt.Play();
				break;
			case Enemy.EnemyState.Patrol when _hasChased:
				GiveUpGrowl.Play();
				_hasChased = false;
				break;
		}
		_previousState = state;
	}
}
