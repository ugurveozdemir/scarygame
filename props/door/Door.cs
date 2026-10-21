using Godot;

/// <summary>
/// A hinged door that swings open away from whoever opens it. The body's origin is the hinge.
/// </summary>
public partial class Door : AnimatableBody3D, IInteractable
{
	[Export] public float OpenAngleDegrees { get; set; } = 100f;
	[Export] public float SwingSeconds { get; set; } = 0.6f;

	private float _openAngle;
	private Basis _closedBasis;
	private Tween _swingTween;

	public bool IsOpen { get; private set; }
	public string InteractionPrompt => IsOpen ? "Close" : "Open";

	public override void _Ready()
	{
		_closedBasis = Basis;
	}

	public void Interact(Player player)
	{
		if (IsOpen)
			Close();
		else
			Open(player);
	}

	public void Open(Node3D opener)
	{
		IsOpen = true;
		SwingTo(Mathf.DegToRad(OpenAngleDegrees) * SwingDirectionAwayFrom(opener));
	}

	public void Close()
	{
		IsOpen = false;
		SwingTo(0f);
	}

	private void SwingTo(float targetAngle)
	{
		_swingTween?.Kill();
		_swingTween = CreateTween()
			.SetProcessMode(Tween.TweenProcessMode.Physics)
			.SetTrans(Tween.TransitionType.Sine);
		_swingTween.TweenMethod(Callable.From<float>(SetOpenAngle), _openAngle, targetAngle, SwingSeconds);
	}

	private float SwingDirectionAwayFrom(Node3D opener)
	{
		// A positive angle swings the door towards its local -Z side.
		Vector3 toOpener = opener.GlobalPosition - GlobalPosition;
		return GlobalBasis.Z.Dot(toOpener) > 0f ? 1f : -1f;
	}

	private void SetOpenAngle(float angle)
	{
		_openAngle = angle;
		Basis = _closedBasis.Rotated(Vector3.Up, angle);
	}
}
