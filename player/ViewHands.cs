using Godot;

/// <summary>
/// The player's own arms, seen in first person only while a hand reaches for something, like a door.
/// The arms belong to a headless body around the camera. IK moves a hand from below the view to a grip point
/// and keeps it there, so the hand follows the grip as it moves. A hand always starts from where it is,
/// so moving between grips never jumps.
/// </summary>
public partial class ViewHands : Node3D
{
	public enum Side { Left, Right }

	private sealed class Hand
	{
		public Node3D Target;
		public Node3D Rest;
		/// <summary>Where the current move started, in this node's space so it moves along with the view.</summary>
		public Vector3 From;
		public Node3D To;
		public float Progress = 1f;
		public bool IsOut;
		public Tween Tween;
	}

	[Export] public float ReachSeconds { get; set; } = 0.3f;
	[Export] public float WithdrawSeconds { get; set; } = 0.3f;
	[Export] public string BaseAnimation { get; set; } = "ual1/Idle";
	/// <summary>Animation whose first frame provides the curled, gripping finger pose.</summary>
	[Export] public string GripAnimation { get; set; } = "ual1/Idle_Torch";

	[ExportGroup("Nodes")]
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	[Export] public AnimationLibrary ExtraAnimations { get; set; }
	[Export] public GripFingersModifier GripFingers { get; set; }
	[Export] public Node3D LeftHandTarget { get; set; }
	[Export] public Node3D RightHandTarget { get; set; }
	/// <summary>Where a hand waits below the view when it is not reaching for anything.</summary>
	[Export] public Node3D LeftRest { get; set; }
	[Export] public Node3D RightRest { get; set; }

	private Hand _left;
	private Hand _right;

	public override void _Ready()
	{
		_left = new Hand { Target = LeftHandTarget, Rest = LeftRest, To = LeftRest };
		_right = new Hand { Target = RightHandTarget, Rest = RightRest, To = RightRest };
		AnimationPlayer.AddAnimationLibrary("ual1", ExtraAnimations);
		AnimationPlayer.Play(BaseAnimation);
		GripFingers.UsePose(AnimationPlayer.GetAnimation(GripAnimation));
		Visible = false;
	}

	public override void _Process(double delta)
	{
		PlaceHand(_left);
		PlaceHand(_right);
		Visible = _left.IsOut || _right.IsOut;
	}

	/// <summary>Moves a hand into view until it holds <paramref name="grip"/>, then keeps following it.</summary>
	public SignalAwaiter Reach(Side side, Node3D grip, float? seconds = null)
	{
		Hand hand = HandOn(side);
		hand.IsOut = true;
		return MoveHand(hand, grip, seconds ?? ReachSeconds);
	}

	/// <summary>Lowers a hand back out of view.</summary>
	public SignalAwaiter Withdraw(Side side, float? seconds = null)
	{
		Hand hand = HandOn(side);
		SignalAwaiter finished = MoveHand(hand, hand.Rest, seconds ?? WithdrawSeconds);
		hand.Tween.TweenCallback(Callable.From(() => { hand.IsOut = false; }));
		return finished;
	}

	public void HideImmediately()
	{
		foreach (Hand hand in new[] { _left, _right })
		{
			hand.Tween?.Kill();
			hand.To = hand.Rest;
			hand.Progress = 1f;
			hand.IsOut = false;
		}
	}

	private Hand HandOn(Side side)
	{
		return side == Side.Left ? _left : _right;
	}

	private void PlaceHand(Hand hand)
	{
		hand.Target.Position = hand.From.Lerp(ToLocal(hand.To.GlobalPosition), hand.Progress);
	}

	private SignalAwaiter MoveHand(Hand hand, Node3D destination, float seconds)
	{
		hand.From = hand.Target.Position;
		hand.To = destination;
		hand.Progress = 0f;
		hand.Tween?.Kill();
		hand.Tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		hand.Tween.TweenMethod(Callable.From<float>(value => hand.Progress = value), 0f, 1f, seconds);
		return ToSignal(hand.Tween, Tween.SignalName.Finished);
	}
}
