using Godot;

/// <summary>
/// The lift's doors on one floor: two panels that slide apart, with a chime when they open.
/// </summary>
public partial class LiftLanding : Node3D
{
	[Export] public float OpenOffset { get; set; } = 0.58f;
	[Export] public float SlideSeconds { get; set; } = 1.4f;
	[Export] public Node3D LeftDoor { get; set; }
	[Export] public Node3D RightDoor { get; set; }
	[Export] public AudioStreamPlayer3D Chime { get; set; }
	[Export] public AudioStreamPlayer3D DoorSound { get; set; }

	public bool IsOpen { get; private set; }

	private Vector3 _leftClosed;
	private Vector3 _rightClosed;

	public override void _Ready()
	{
		_leftClosed = LeftDoor.Position;
		_rightClosed = RightDoor.Position;
	}

	/// <summary>Opens the doors; returns the tween so callers can wait for them.</summary>
	public Tween Open()
	{
		IsOpen = true;
		Chime.Play();
		return Slide(OpenOffset);
	}

	public Tween Close()
	{
		IsOpen = false;
		return Slide(0f);
	}

	private Tween Slide(float offset)
	{
		DoorSound.Play();
		// The doors slide along the landing's local X axis, away from each other.
		Tween tween = CreateTween().SetParallel().SetProcessMode(Tween.TweenProcessMode.Physics)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(LeftDoor, "position", _leftClosed + Vector3.Left * offset, SlideSeconds);
		tween.TweenProperty(RightDoor, "position", _rightClosed + Vector3.Right * offset, SlideSeconds);
		return tween;
	}
}
