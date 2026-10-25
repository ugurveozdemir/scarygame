using Godot;

/// <summary>
/// A black screen with a few lines of text, faded over the game at the start of a trial and at its endings.
/// </summary>
public partial class TitleCard : Control
{
	[Export] public Label Lines { get; set; }

	/// <summary>Covers the screen with <paramref name="text"/> at once, as if the picture had not come on yet.</summary>
	public void Cover(string text)
	{
		Lines.Text = text;
		Modulate = Colors.White;
		Visible = true;
	}

	/// <summary>Fades the card out to reveal the game. Returns the tween so callers can wait for it.</summary>
	public Tween Reveal(float seconds)
	{
		Tween tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, seconds);
		tween.TweenCallback(Callable.From(Hide));
		return tween;
	}

	/// <summary>Fades the card in over the game with <paramref name="text"/>.</summary>
	public Tween FadeIn(string text, float seconds)
	{
		Lines.Text = text;
		Visible = true;
		Tween tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 1f, seconds).From(0f);
		return tween;
	}
}
