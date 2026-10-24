using Godot;

/// <summary>
/// Fades the screen to black when the player is caught, then restarts the level.
/// Keeps running while the game is paused, and so does its sound, while everything else falls silent.
/// </summary>
public partial class DeathScreen : Control
{
	[Export] public float FadeSeconds { get; set; } = 1.5f;
	[Export] public float HoldSeconds { get; set; } = 2f;
	/// <summary>The last heartbeats and the camcorder's signal breaking up.</summary>
	[Export] public AudioStreamPlayer DeathSound { get; set; }

	public void Play()
	{
		if (Visible)
			return;

		GetTree().Paused = true;
		Visible = true;
		DeathSound.Play();

		Tween tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 1f, FadeSeconds).From(0f);
		tween.TweenInterval(HoldSeconds);
		tween.TweenCallback(Callable.From(RestartLevel));
	}

	private void RestartLevel()
	{
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}
}
