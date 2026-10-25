using System;
using Godot;

/// <summary>
/// Holds a note, file or scrap of paper up in front of the player. The world goes on while they read, but they
/// can't move until they put it down again with the interact key.
/// </summary>
public partial class NoteReader : Control
{
	[Export] public Player Player { get; set; }
	[Export] public Label TitleLabel { get; set; }
	[Export] public Label BodyLabel { get; set; }
	[Export] public AudioStreamPlayer PaperSound { get; set; }

	private Action _onClosed;

	public void Open(string title, string text, Action onClosed = null)
	{
		TitleLabel.Text = title;
		BodyLabel.Text = text;
		_onClosed = onClosed;
		Visible = true;
		PaperSound.Play();
		Player.LockControls();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible || !(@event.IsActionPressed("interact") || @event.IsActionPressed("ui_cancel")))
			return;

		GetViewport().SetInputAsHandled();
		Close();
	}

	private void Close()
	{
		Visible = false;
		PaperSound.Play();
		Player.UnlockControls();

		Action onClosed = _onClosed;
		_onClosed = null;
		onClosed?.Invoke();
	}
}
