using Godot;

/// <summary>
/// The player's own arms, seen in first person only while a hand reaches for something, like a locker door.
/// The arms belong to a headless body around the camera. IK moves a hand from below the view to a grip point
/// and keeps it there, so the hand follows the grip as it moves.
/// </summary>
public partial class ViewHands : Node3D
{
	public enum Side { Left, Right }

	private sealed class Hand
	{
		public Node3D Target;
		public Node3D Rest;
		public Node3D Grip;
		public float Weight;
		public Tween Tween;
	}

	[Export] public float ReachSeconds { get; set; } = 0.3f;
	[Export] public float WithdrawSeconds { get; set; } = 0.25f;
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
		_left = new Hand { Target = LeftHandTarget, Rest = LeftRest };
		_right = new Hand { Target = RightHandTarget, Rest = RightRest };
		AnimationPlayer.AddAnimationLibrary("ual1", ExtraAnimations);
		AnimationPlayer.Play(BaseAnimation);
		GripFingers.UsePose(AnimationPlayer.GetAnimation(GripAnimation));
		Visible = false;
	}

	public override void _Process(double delta)
	{
		PlaceHand(_left);
		PlaceHand(_right);
		Visible = _left.Weight > 0f || _right.Weight > 0f;
	}

	/// <summary>Raises a hand into view until it holds <paramref name="grip"/>, then keeps following it.</summary>
	public SignalAwaiter Reach(Side side, Node3D grip)
	{
		Hand hand = HandOn(side);
		hand.Grip = grip;
		return MoveHand(hand, 1f, ReachSeconds);
	}

	/// <summary>Lowers a hand back out of view.</summary>
	public SignalAwaiter Withdraw(Side side)
	{
		return MoveHand(HandOn(side), 0f, WithdrawSeconds);
	}

	public void HideImmediately()
	{
		foreach (Hand hand in new[] { _left, _right })
		{
			hand.Tween?.Kill();
			hand.Weight = 0f;
		}
	}

	private Hand HandOn(Side side)
	{
		return side == Side.Left ? _left : _right;
	}

	private void PlaceHand(Hand hand)
	{
		Vector3 grip = hand.Grip?.GlobalPosition ?? hand.Rest.GlobalPosition;
		hand.Target.GlobalPosition = hand.Rest.GlobalPosition.Lerp(grip, hand.Weight);
	}

	private SignalAwaiter MoveHand(Hand hand, float weight, float seconds)
	{
		hand.Tween?.Kill();
		hand.Tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		hand.Tween.TweenMethod(Callable.From<float>(value => hand.Weight = value), hand.Weight, weight, seconds);
		return ToSignal(hand.Tween, Tween.SignalName.Finished);
	}
}
