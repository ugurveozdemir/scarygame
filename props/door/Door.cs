using Godot;

/// <summary>
/// A hinged door that swings open away from whoever opens it. The body's origin is the hinge.
/// The player opens and closes it by hand without stopping: the hand pushes or pulls the door
/// and rides along for a moment while the door starts to move.
/// </summary>
public partial class Door : AnimatableBody3D, IInteractable
{
	[Export] public float OpenAngleDegrees { get; set; } = 100f;
	[Export] public float SwingSeconds { get; set; } = 0.6f;

	[ExportGroup("Hand")]
	[Export] public float HandReachSeconds { get; set; } = 0.25f;
	[Export] public float HandHoldSeconds { get; set; } = 0.3f;
	/// <summary>Handle position in door space; its Z is mirrored to whichever side the player is on.</summary>
	[Export] public Vector3 HandlePosition { get; set; } = new(0.84f, 1.05f, 0.06f);
	/// <summary>Further away than this, the hand pushes the door where it is nearest instead of using the handle.</summary>
	[Export] public float HandleReach { get; set; } = 1.1f;
	/// <summary>How tightly the fingers close around the handle, from 0 for an open hand to 1 for a fist.</summary>
	[Export(PropertyHint.Range, "0,1")] public float HandleCurl { get; set; } = 0.7f;
	[Export] public Node3D GripPoint { get; set; }

	private float _openAngle;
	private Basis _closedBasis;
	private Tween _swingTween;
	private Tween _handTween;

	public bool IsOpen { get; private set; }
	public string InteractionPrompt => IsOpen ? "Close" : "Open";

	public override void _Ready()
	{
		_closedBasis = Basis;
	}

	public void Interact(Player player)
	{
		if (_handTween is not null && _handTween.IsRunning())
			return;

		(ViewHands.Side side, float curl) = PlaceGripFor(player);
		_ = player.Hands.Reach(side, GripPoint, HandReachSeconds, curl);

		// The door starts moving as the hand touches it, and the hand rides along for a moment.
		_handTween = CreateTween();
		_handTween.TweenInterval(HandReachSeconds * 0.8f);
		_handTween.TweenCallback(Callable.From(() =>
		{
			if (IsOpen)
				Close();
			else
				Open(player);
		}));
		_handTween.TweenInterval(HandHoldSeconds);
		_handTween.TweenCallback(Callable.From(() => { _ = player.Hands.Withdraw(side); }));
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

	/// <summary>
	/// Puts the grip on the handle on the player's side of the door, or flat on the nearest spot of the door
	/// if the handle is out of reach. Returns the hand that should do it and how tightly it closes.
	/// </summary>
	private (ViewHands.Side, float) PlaceGripFor(Player player)
	{
		Vector3 head = ToLocal(player.Head.GlobalPosition);
		float playerSide = head.Z >= 0f ? 1f : -1f;
		Vector3 handle = HandlePosition with { Z = HandlePosition.Z * playerSide };
		Vector3 intoDoor = new(0f, 0f, -playerSide);

		if (ToGlobal(handle).DistanceTo(player.Head.GlobalPosition) <= HandleReach)
		{
			// Fingers reach over the handle, palm down.
			GripPoint.Position = handle;
			GripPoint.Basis = ViewHands.Pose(intoDoor, Vector3.Down);
			return (HandSide(player), HandleCurl);
		}

		// Push the door with a flat hand, fingers up and palm on the door.
		GripPoint.Position = new Vector3(Mathf.Clamp(head.X, 0.2f, 0.9f), Mathf.Clamp(head.Y - 0.5f, 0.9f, 1.4f), handle.Z);
		GripPoint.Basis = ViewHands.Pose(Vector3.Up, intoDoor);
		return (HandSide(player), 0f);
	}

	/// <summary>The hand on the same side of the player as the grip point.</summary>
	private ViewHands.Side HandSide(Player player)
	{
		bool gripIsOnPlayersLeft = player.ToLocal(GripPoint.GlobalPosition).X < 0f;
		return gripIsOnPlayersLeft ? ViewHands.Side.Left : ViewHands.Side.Right;
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
