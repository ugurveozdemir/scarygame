using Godot;

/// <summary>
/// Shows the player's objective near the top of the screen for a few seconds whenever it changes. Objectives are
/// memos typed into the camcorder's on-screen display, so they appear one letter at a time with a beep: at first
/// the ones a previous trial left behind, later the player's own, saved for the next trial.
/// </summary>
public partial class ObjectiveDisplay : Label
{
	[Export] public float FadeSeconds { get; set; } = 1f;
	[Export] public float HoldSeconds { get; set; } = 7f;
	[Export] public float LettersPerSecond { get; set; } = 40f;
	[Export] public AudioStreamPlayer Beep { get; set; }

	private Tween _tween;

	/// <summary>Shows a memo left by the previous trial.</summary>
	public void Announce(string objective)
	{
		TypeMemo($"MEMO · TRIAL {GameState.Trial - 1}", objective);
	}

	/// <summary>Shows a memo the player saves for the next trial, past the point any earlier trial reached.</summary>
	public void AnnounceOwn(string objective)
	{
		TypeMemo($"NEW MEMO · SAVED FOR TRIAL {GameState.Trial + 1}", objective);
	}

	private void TypeMemo(string header, string memo)
	{
		// Invariant, so a Turkish system locale doesn't turn "i" into a dotted "İ".
		Text = $"{header}\n{memo.ToUpperInvariant()}";
		Beep.Play();

		_tween?.Kill();
		_tween = CreateTween().SetParallel();
		_tween.TweenProperty(this, "modulate:a", 1f, FadeSeconds * 0.3f).From(0f);
		_tween.TweenProperty(this, "visible_ratio", 1f, Text.Length / LettersPerSecond).From(0f);
		_tween.Chain().TweenInterval(HoldSeconds);
		_tween.Chain().TweenProperty(this, "modulate:a", 0f, FadeSeconds);
	}
}
