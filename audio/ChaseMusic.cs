using Godot;

/// <summary>
/// Scores the chase: a sting when the enemy spots the player, then music that keeps going until it loses them
/// and fades out while it searches. Spotted again before the music has faded, the chase just carries on.
/// </summary>
public partial class ChaseMusic : Node
{
	[Export] public Enemy Enemy { get; set; }
	[Export] public AudioStreamPlayer Music { get; set; }
	[Export] public AudioStreamPlayer Sting { get; set; }
	[Export] public float FadeOutSeconds { get; set; } = 5f;

	private Tween _fade;
	private float _musicVolume;

	public override void _Ready()
	{
		_musicVolume = Music.VolumeLinear;
		Enemy.StateChanged += OnEnemyStateChanged;
	}

	private void OnEnemyStateChanged(Enemy.EnemyState state)
	{
		_fade?.Kill();
		bool isChasing = state is Enemy.EnemyState.Chase or Enemy.EnemyState.Strangling;
		if (isChasing)
		{
			if (!Music.Playing)
			{
				Sting.Play();
				Music.Play();
			}
			Music.VolumeLinear = _musicVolume;
			return;
		}

		_fade = CreateTween();
		_fade.TweenProperty(Music, "volume_linear", 0f, FadeOutSeconds);
		_fade.TweenCallback(Callable.From(Music.Stop));
	}
}
