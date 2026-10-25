using System;
using Godot;

/// <summary>
/// A close-up of a keypad for typing a code with the number keys. Backspace deletes a digit; the code is
/// entered as soon as it is complete. The interact key steps away.
/// </summary>
public partial class KeypadScreen : Control
{
	[Export] public Label Display { get; set; }
	[Export] public AudioStreamPlayer KeySound { get; set; }

	private Player _player;
	private Action<string> _onEntered;
	private string _typed = "";
	private int _length;

	public void Open(Player player, int length, Action<string> onEntered)
	{
		_player = player;
		_length = length;
		_onEntered = onEntered;
		_typed = "";
		UpdateDisplay();
		Visible = true;
		player.LockControls();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible)
			return;

		if (@event.IsActionPressed("interact") || @event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Close();
			return;
		}

		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;

		GetViewport().SetInputAsHandled();
		if (key.Keycode == Key.Backspace && _typed.Length > 0)
		{
			_typed = _typed[..^1];
		}
		else if (DigitOf(key) is { } digit)
		{
			_typed += digit;
			KeySound.Play();
		}
		UpdateDisplay();

		if (_typed.Length == _length)
		{
			string code = _typed;
			Close();
			_onEntered(code);
		}
	}

	private void Close()
	{
		Visible = false;
		_player.UnlockControls();
	}

	private void UpdateDisplay()
	{
		Display.Text = string.Join(' ', _typed.PadRight(_length, '_').ToCharArray());
	}

	private static char? DigitOf(InputEventKey key)
	{
		Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
		if (code >= Key.Key0 && code <= Key.Key9)
			return (char)('0' + (code - Key.Key0));
		if (code >= Key.Kp0 && code <= Key.Kp9)
			return (char)('0' + (code - Key.Kp0));
		return null;
	}
}
