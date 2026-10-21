using Godot;

/// <summary>
/// A hinged door that swings open away from the player. The body's origin is the hinge.
/// </summary>
public partial class Door : AnimatableBody3D, IInteractable
{
	[Export] public float OpenAngleDegrees { get; set; } = 100f;
	[Export] public float SwingSeconds { get; set; } = 0.6f;

	private bool _isOpen;
	private float _openAngle;
	private Basis _closedBasis;
	private Tween _swingTween;

	public string InteractionPrompt => _isOpen ? "Close" : "Open";

	public override void _Ready()
	{
		_closedBasis = Basis;
	}

	public void Interact(Player player)
	{
		_isOpen = !_isOpen;
		float targetAngle = _isOpen ? Mathf.DegToRad(OpenAngleDegrees) * SwingDirectionAwayFrom(player) : 0f;

		_swingTween?.Kill();
		_swingTween = CreateTween()
			.SetProcessMode(Tween.TweenProcessMode.Physics)
			.SetTrans(Tween.TransitionType.Sine);
		_swingTween.TweenMethod(Callable.From<float>(SetOpenAngle), _openAngle, targetAngle, SwingSeconds);
	}

	private float SwingDirectionAwayFrom(Node3D player)
	{
		// A positive angle swings the door towards its local -Z side.
		Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
		return GlobalBasis.Z.Dot(toPlayer) > 0f ? 1f : -1f;
	}

	private void SetOpenAngle(float angle)
	{
		_openAngle = angle;
		Basis = _closedBasis.Rotated(Vector3.Up, angle);
	}
}
