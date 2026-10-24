using Godot;

/// <summary>
/// A hinged door that swings open away from whoever opens it. The body's origin is the hinge.
/// The player opens and closes it by hand without stopping, like in real life: to open it the hand grips the
/// lever handle, presses it down and pushes; to close it the hand pulls the handle on the near side, or pushes
/// the door flat-handed when it swings away. The hand rides along until the door moves out of reach.
/// Only a door within arm's reach can be used.
/// </summary>
public partial class Door : AnimatableBody3D, IInteractable
{
	/// <summary>How the player takes hold of the door: the grip point, where it will be, which hand holds it and the lever it is on, if any.</summary>
	private readonly record struct Grip(Node3D Point, Transform3D Pose, ViewHands.Side Side, Node3D Lever);

	[Export] public float OpenAngleDegrees { get; set; } = 100f;
	[Export] public float SwingSeconds { get; set; } = 0.9f;

	[ExportGroup("Hand")]
	[Export] public float HandReachSeconds { get; set; } = 0.4f;
	/// <summary>How tightly the fingers close around the lever, from 0 for an open hand to 1 for a fist.</summary>
	[Export(PropertyHint.Range, "0,1")] public float HandleCurl { get; set; } = 0.75f;
	[Export] public float LeverPressDegrees { get; set; } = 35f;
	[Export] public float LeverPressSeconds { get; set; } = 0.15f;
	/// <summary>Where a flat hand may push the door, in door space: X along the door and Y up.</summary>
	[Export] public Rect2 PushArea { get; set; } = new(0.3f, 0.9f, 0.6f, 0.6f);

	[ExportGroup("Nodes")]
	/// <summary>Grip points on the lever handles; each lever turns about its own Z axis.</summary>
	[Export] public Node3D FrontGrip { get; set; }
	[Export] public Node3D BackGrip { get; set; }
	[Export] public Node3D PushPoint { get; set; }

	private float _openAngle;
	private Basis _closedBasis;
	private Tween _swingTween;
	private Tween _handTween;

	public bool IsOpen { get; private set; }
	public string InteractionPrompt => IsOpen ? "Close" : "Open";

	private bool IsBusy => _handTween is not null && _handTween.IsRunning();

	public override void _Ready()
	{
		_closedBasis = Basis;
		// Fingers reach over the lever towards the door and curl under it, palm down.
		FrontGrip.Basis = ViewHands.Pose(Vector3.Back, Vector3.Down);
		BackGrip.Basis = ViewHands.Pose(Vector3.Forward, Vector3.Down);
	}

	public bool CanInteract(Player player)
	{
		return !IsBusy && ChooseGrip(player) is not null;
	}

	public void Interact(Player player)
	{
		if (IsBusy || ChooseGrip(player) is not Grip grip)
			return;

		bool opening = !IsOpen;
		// Only the push point moves; a handle's grip point is already where its pose says.
		grip.Point.GlobalTransform = grip.Pose;
		player.Hands.Reach(grip.Side, grip.Point, HandReachSeconds, grip.Lever is null ? 0f : HandleCurl);

		// One timeline: the hand closes on the handle, presses the lever if the door is latched and swings the door
		// while still holding on. The lever springs back once the door is unlatched.
		_handTween = CreateTween().SetParallel();
		float touch = HandReachSeconds * 0.9f;
		float swingStart = touch;
		if (opening && grip.Lever is not null)
		{
			TurnLever(grip.Lever, LeverPressDegrees, touch);
			swingStart += LeverPressSeconds * 0.7f;
			TurnLever(grip.Lever, 0f, swingStart + LeverPressSeconds);
		}
		_handTween.TweenCallback(Callable.From(() =>
		{
			if (opening)
				Open(player);
			else
				Close();
		})).SetDelay(swingStart);
		_handTween.TweenCallback(Callable.From(() => player.Hands.Withdraw(grip.Side))).SetDelay(swingStart + SwingSeconds);
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
	/// The handle on the player's side of the door if the hand reaches it, otherwise a flat-handed push on the
	/// nearest spot of the door if the door is going to swing away from the player. Null when out of reach.
	/// </summary>
	private Grip? ChooseGrip(Player player)
	{
		Vector3 head = ToLocal(player.Head.GlobalPosition);
		float playerSide = head.Z >= 0f ? 1f : -1f;
		Node3D handle = playerSide > 0f ? BackGrip : FrontGrip;
		Grip? handleGrip = GripFor(player, handle, handle.GlobalTransform, handle.GetParent<Node3D>());
		if (handleGrip is not null)
			return handleGrip;

		// A latched door only opens by its handle, and a flat hand can't pull.
		if (!IsOpen || ClosesTowards(playerSide))
			return null;

		// The spot of the door in front of the player's chest.
		Vector3 nearest = new(
			Mathf.Clamp(head.X, PushArea.Position.X, PushArea.End.X),
			Mathf.Clamp(head.Y - 0.4f, PushArea.Position.Y, PushArea.End.Y),
			playerSide * 0.03f);
		// Fingers up and palm on the door.
		Transform3D pushPose = GlobalTransform * new Transform3D(ViewHands.Pose(Vector3.Up, new Vector3(0f, 0f, -playerSide)), nearest);
		return GripFor(player, PushPoint, pushPose, null);
	}

	/// <summary>A grip with the hand on the same side of the player as the grip, or null if that hand can't reach it.</summary>
	private static Grip? GripFor(Player player, Node3D point, Transform3D pose, Node3D lever)
	{
		bool onPlayersLeft = player.ToLocal(pose.Origin).X < 0f;
		ViewHands.Side side = onPlayersLeft ? ViewHands.Side.Left : ViewHands.Side.Right;
		return player.Hands.CanReach(side, pose) ? new Grip(point, pose, side, lever) : null;
	}

	/// <summary>Whether closing the door moves it towards the given side of it: +1 for its +Z side, -1 for -Z.</summary>
	private bool ClosesTowards(float side)
	{
		// A positive angle swings the door towards its local -Z side, so closing moves it back towards +Z.
		return Mathf.Sign(_openAngle) == side;
	}

	private void TurnLever(Node3D lever, float degrees, float delay)
	{
		_handTween.TweenProperty(lever, "rotation:z", Mathf.DegToRad(degrees), LeverPressSeconds)
			.SetDelay(delay).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void SwingTo(float targetAngle)
	{
		_swingTween?.Kill();
		_swingTween = CreateTween()
			.SetProcessMode(Tween.TweenProcessMode.Physics)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
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
