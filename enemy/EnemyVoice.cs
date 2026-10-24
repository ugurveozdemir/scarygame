using Godot;

/// <summary>
/// The enemy's breathing and cries. It always breathes loud enough to be tracked by ear, slow and heavy while
/// it walks around and ragged while it hunts, and it roars when it spots the player.
/// </summary>
public partial class EnemyVoice : Node
{
	[Export] public Enemy Enemy { get; set; }
	[Export] public AudioStreamPlayer3D CalmBreath { get; set; }
	[Export] public AudioStreamPlayer3D HuntingBreath { get; set; }
	[Export] public AudioStreamPlayer3D SpotRoar { get; set; }
	[Export] public float BreathBlendSeconds { get; set; } = 1f;

	private float _calmVolume;
	private float _huntingVolume;
	private float _hunting;

	public override void _Ready()
	{
		_calmVolume = CalmBreath.VolumeLinear;
		_huntingVolume = HuntingBreath.VolumeLinear;
		Enemy.StateChanged += OnStateChanged;
	}

	public override void _Process(double delta)
	{
		bool isHunting = Enemy.State is Enemy.EnemyState.Chase or Enemy.EnemyState.Strangling;
		_hunting = Mathf.MoveToward(_hunting, isHunting ? 1f : 0f, (float)delta / BreathBlendSeconds);
		CalmBreath.VolumeLinear = _calmVolume * (1f - _hunting);
		HuntingBreath.VolumeLinear = _huntingVolume * _hunting;
	}

	private void OnStateChanged(Enemy.EnemyState state)
	{
		if (state == Enemy.EnemyState.Chase)
			SpotRoar.Play();
	}
}
