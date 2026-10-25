using Godot;

/// <summary>
/// Shows the player's objective near the top of the screen for a few seconds whenever it changes.
/// </summary>
public partial class ObjectiveDisplay : Label
{
	[Export] public float FadeSeconds { get; set; } = 1f;
	[Export] public float HoldSeconds { get; set; } = 6f;

	private Tween _tween;

	public void Announce(string objective)
	{
		// Invariant, so a Turkish system locale doesn't turn "i" into a dotted "İ".
		Text = $"OBJECTIVE\n{objective.ToUpperInvariant()}";

		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenProperty(this, "modulate:a", 1f, FadeSeconds).From(0f);
		_tween.TweenInterval(HoldSeconds);
		_tween.TweenProperty(this, "modulate:a", 0f, FadeSeconds);
	}
}
