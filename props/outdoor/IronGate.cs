using Godot;

/// <summary>
/// The sliding iron gate of the grounds, held shut by a magnetic lock. Released from the chief physician's
/// console, the lock drops and a motor rolls the gate aside along the fence.
/// </summary>
public partial class IronGate : AnimatableBody3D
{
	[Export] public float SlideDistance { get; set; } = 3.2f;
	[Export] public float SlideSeconds { get; set; } = 4f;
	[Export] public Lock Lock { get; set; }
	[Export] public AudioStreamPlayer3D ReleaseSound { get; set; }
	[Export] public AudioStreamPlayer3D MotorSound { get; set; }

	public bool IsOpen { get; private set; }

	public void Open()
	{
		if (IsOpen)
			return;

		IsOpen = true;
		Lock.Release();
		ReleaseSound.Play();
		MotorSound.Play();

		Vector3 open = Position + Basis.X * SlideDistance;
		Tween tween = CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(this, "position", open, SlideSeconds);
	}
}
