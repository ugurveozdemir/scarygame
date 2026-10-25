using Godot;

/// <summary>
/// A line of text at the bottom of the screen for what the player notices, like a door that won't open.
/// It stays long enough to read and then fades away.
/// </summary>
public partial class Caption : Label
{
	[Export] public float SecondsPerWord { get; set; } = 0.3f;
	[Export] public float MinSeconds { get; set; } = 2.5f;
	[Export] public float FadeSeconds { get; set; } = 0.4f;

	private Tween _tween;

	public void Say(string text)
	{
		Text = text;
		float holdSeconds = Mathf.Max(MinSeconds, text.Split(' ').Length * SecondsPerWord);

		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenProperty(this, "modulate:a", 1f, FadeSeconds).From(0f);
		_tween.TweenInterval(holdSeconds);
		_tween.TweenProperty(this, "modulate:a", 0f, FadeSeconds);
	}
}
