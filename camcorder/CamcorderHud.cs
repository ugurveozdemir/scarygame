using System;
using Godot;

/// <summary>
/// Draws the camcorder overlay: frame corners, blinking REC indicator, timecode,
/// battery level and the night-vision label.
/// </summary>
public partial class CamcorderHud : Control
{
	private const float Margin = 40f;
	private const float CornerLength = 50f;
	private const float LineWidth = 3f;
	private const int FontSize = 26;
	private const int BatterySegments = 4;
	private static readonly Vector2 BatterySize = new(64f, 28f);
	private static readonly Color HudColor = new(1f, 1f, 1f, 0.85f);
	private static readonly Color WarningColor = new(0.9f, 0.1f, 0.1f);

	[Export] public Camcorder Camcorder { get; set; }

	private Font _font;
	private double _recordingSeconds;

	public override void _Ready()
	{
		_font = GetThemeDefaultFont();
	}

	public override void _Process(double delta)
	{
		Visible = Camcorder.IsRaised;
		if (!Visible)
			return;

		_recordingSeconds += delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawFrameCorners();
		DrawRecordingIndicator();
		DrawBattery();
		if (Camcorder.IsNightVisionOn)
			DrawText("NIGHT VISION", new Vector2(Margin + 25f, Size.Y - Margin - 25f), HudColor);
	}

	private void DrawFrameCorners()
	{
		Rect2 frame = new(Vector2.One * Margin, Size - Vector2.One * Margin * 2f);
		Vector2[] corners = { frame.Position, new(frame.End.X, frame.Position.Y), frame.End, new(frame.Position.X, frame.End.Y) };
		foreach (Vector2 corner in corners)
		{
			// Both lines of a corner point toward the centre of the screen.
			Vector2 toCenter = (frame.GetCenter() - corner).Sign();
			DrawLine(corner, corner + new Vector2(toCenter.X * CornerLength, 0f), HudColor, LineWidth);
			DrawLine(corner, corner + new Vector2(0f, toCenter.Y * CornerLength), HudColor, LineWidth);
		}
	}

	private void DrawRecordingIndicator()
	{
		Vector2 dotCenter = new(Margin + 40f, Margin + 40f);
		bool isBlinkVisible = (int)(_recordingSeconds * 2) % 2 == 0;
		if (isBlinkVisible)
			DrawCircle(dotCenter, 10f, WarningColor);

		DrawText("REC", dotCenter + new Vector2(20f, 9f), HudColor);
		DrawText(TimeSpan.FromSeconds(_recordingSeconds).ToString(@"hh\:mm\:ss"), dotCenter + new Vector2(-15f, 50f), HudColor);
	}

	private void DrawBattery()
	{
		Rect2 body = new(new Vector2(Size.X - Margin - 40f - BatterySize.X, Margin + 26f), BatterySize);
		DrawRect(body, HudColor, filled: false, width: LineWidth);
		DrawRect(new Rect2(body.End.X, body.Position.Y + BatterySize.Y * 0.3f, 5f, BatterySize.Y * 0.4f), HudColor);

		int litSegments = Mathf.CeilToInt(Camcorder.Charge * BatterySegments);
		Color segmentColor = litSegments <= 1 ? WarningColor : HudColor;
		float segmentWidth = (BatterySize.X - 8f) / BatterySegments;
		for (int i = 0; i < litSegments; i++)
		{
			Vector2 segmentPosition = body.Position + new Vector2(4f + i * segmentWidth, 4f);
			DrawRect(new Rect2(segmentPosition, segmentWidth - 2f, BatterySize.Y - 8f), segmentColor);
		}

		DrawText($"x{Camcorder.SpareBatteries}", body.Position + new Vector2(14f, BatterySize.Y + 32f), HudColor);
	}

	private void DrawText(string text, Vector2 baseline, Color color)
	{
		DrawString(_font, baseline, text, HorizontalAlignment.Left, -1, FontSize, color);
	}
}
