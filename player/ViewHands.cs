using Godot;

/// <summary>
/// The player's own arms, seen in first person only while a hand reaches for something, like a door.
/// The arms belong to a life-sized, headless body whose eyes are at the camera, so the hands look as big and as
/// far away as real hands would. The body only turns left and right with the view: looking up or down bends the
/// neck, not the shoulders. IK moves a hand from below the view to a grip point and keeps it there, so the
/// hand follows the grip as it moves. A hand always starts from where it is, so moving between grips never jumps.
/// A grip point also says how the hand is held: its -Z axis is where the fingers point and its -Y axis is where
/// the palm faces, see <see cref="Pose"/>.
/// A hand only reaches as far as a real arm does. The player leans towards a grip that is a little further away
/// (<see cref="Lean"/>), and a hand lets go of a grip that moves out of reach, like a door swinging away.
/// </summary>
public partial class ViewHands : Node3D
{
	public enum Side { Left, Right }

	private sealed class Hand
	{
		public Side Side;
		public Node3D Target;
		public Node3D Rest;
		public int UpperArmBone;
		/// <summary>Where the wrist was when the current move started, in the body's space so it moves along with the player.</summary>
		public Vector3 From;
		public Quaternion FromRotation = Quaternion.Identity;
		public Node3D To;
		public float Progress = 1f;
		public bool IsOut;
		public Tween Tween;
		/// <summary>How far the fingers curl towards a grip, from the start of this move to its end.</summary>
		public float FromCurl;
		public float ToCurl;
		/// <summary>The part of a move the fingers curl in, so they close on arrival and let go on leaving.</summary>
		public Vector2 CurlWindow = new(0.5f, 1f);
		public float Curl;

		public bool IsHolding => To != Rest;
	}

	[Export] public float ReachSeconds { get; set; } = 0.4f;
	[Export] public float WithdrawSeconds { get; set; } = 0.35f;
	/// <summary>Animation whose first frame provides the curled, gripping finger pose.</summary>
	[Export] public string GripAnimation { get; set; } = "Idle_Torch";
	/// <summary>How far the fingers of a hand that holds nothing curl, so they hang loose instead of straight.</summary>
	[Export(PropertyHint.Range, "0,1")] public float RelaxedCurl { get; set; } = 0.35f;
	/// <summary>How far a wrist comfortably reaches from its shoulder, as a share of the arm's length; the elbow stays a little bent.</summary>
	[Export(PropertyHint.Range, "0.5,1")] public float ReachRatio { get; set; } = 0.92f;
	/// <summary>How far the upper body leans towards a grip beyond the arm's comfortable reach.</summary>
	[Export] public float MaxLean { get; set; } = 0.25f;
	[Export] public float LeanSharpness { get; set; } = 8f;
	/// <summary>Where the wrist sits relative to a grip point, in the grip's space: behind and above the palm.</summary>
	[Export] public Vector3 WristOffset { get; set; } = new(0f, 0.03f, 0.09f);

	[ExportGroup("Nodes")]
	[Export] public AnimationLibrary Animations { get; set; }
	[Export] public Skeleton3D Skeleton { get; set; }
	[Export] public GripFingersModifier GripFingers { get; set; }
	[Export] public Node3D LeftHandTarget { get; set; }
	[Export] public Node3D RightHandTarget { get; set; }
	/// <summary>Where a palm hangs at the body's side when the hand is not reaching for anything.</summary>
	[Export] public Node3D LeftRest { get; set; }
	[Export] public Node3D RightRest { get; set; }

	private Hand _left;
	private Hand _right;
	private float _reach;

	/// <summary>How far the player leans towards what a hand reaches for, in global space. The player moves the head by it.</summary>
	public Vector3 Lean { get; private set; }

	/// <summary>
	/// A grip point orientation for a hand with its fingers pointing along <paramref name="fingers"/> and its palm
	/// facing along <paramref name="palm"/>, both in the grip's parent space and perpendicular to each other.
	/// </summary>
	public static Basis Pose(Vector3 fingers, Vector3 palm)
	{
		Vector3 back = -fingers;
		Vector3 up = -palm;
		return new Basis(up.Cross(back), up, back);
	}

	public override void _Ready()
	{
		_left = new Hand { Side = Side.Left, Target = LeftHandTarget, Rest = LeftRest, To = LeftRest, UpperArmBone = Skeleton.FindBone("LeftUpperArm") };
		_right = new Hand { Side = Side.Right, Target = RightHandTarget, Rest = RightRest, To = RightRest, UpperArmBone = Skeleton.FindBone("RightUpperArm") };
		_reach = ArmLength("Left") * ReachRatio;

		// Hanging arms: fingers down, palms towards the body.
		LeftRest.Basis = Pose(Vector3.Down, Vector3.Right);
		RightRest.Basis = Pose(Vector3.Down, Vector3.Left);

		// The body keeps its symmetric rest pose; IK places both arms and only the fingers come from an animation.
		GripFingers.UsePose(Animations.GetAnimation(GripAnimation));
		HideImmediately();
	}

	public override void _Process(double delta)
	{
		FollowEyes();
		Vector3 lean = Vector3.Zero;
		foreach (Hand hand in new[] { _left, _right })
		{
			// A grip that has moved away since the hand took hold of it slips out of the hand.
			if (hand.IsHolding && hand.Progress >= 1f && !CanReach(hand.Side, hand.To.GlobalTransform))
				Withdraw(hand.Side);

			Vector3 wrist = WristOf(hand);
			PlaceHand(hand, wrist);
			Vector3 needed = LeanToReach(hand.Side, ToGlobal(wrist));
			if (needed.LengthSquared() > lean.LengthSquared())
				lean = needed;
		}

		Lean = Lean.Lerp(lean, 1f - Mathf.Exp(-LeanSharpness * (float)delta));
		GripFingers.LeftStrength = _left.Curl;
		GripFingers.RightStrength = _right.Curl;
		Visible = _left.IsOut || _right.IsOut;
	}

	/// <summary>
	/// Moves a hand into view until it holds <paramref name="grip"/>, then keeps following it until it is withdrawn
	/// or moves out of reach. <paramref name="curl"/> is how tightly the fingers close around it: 0 is an open hand,
	/// 1 a fist.
	/// </summary>
	public void Reach(Side side, Node3D grip, float? seconds = null, float curl = 0f)
	{
		Hand hand = HandOn(side);
		hand.IsOut = true;
		MoveHand(hand, grip, seconds ?? ReachSeconds, curl, new Vector2(0.5f, 1f));
	}

	/// <summary>Lets go of whatever the hand holds and lowers it out of view.</summary>
	public void Withdraw(Side side, float? seconds = null)
	{
		Hand hand = HandOn(side);
		if (!hand.IsHolding)
			return;

		MoveHand(hand, hand.Rest, seconds ?? WithdrawSeconds, RelaxedCurl, new Vector2(0f, 0.4f));
		hand.Tween.TweenCallback(Callable.From(() => { hand.IsOut = false; }));
	}

	/// <summary>Whether the hand on <paramref name="side"/> can hold a grip at <paramref name="grip"/>, leaning in if it has to.</summary>
	public bool CanReach(Side side, Transform3D grip)
	{
		return UnleanedShoulder(side).DistanceTo(WristAt(grip)) <= _reach + MaxLean;
	}

	public void HideImmediately()
	{
		foreach (Hand hand in new[] { _left, _right })
		{
			hand.Tween?.Kill();
			hand.To = hand.Rest;
			hand.Progress = 1f;
			hand.ToCurl = RelaxedCurl;
			hand.Curl = RelaxedCurl;
			hand.IsOut = false;
		}
		Lean = Vector3.Zero;
	}

	/// <summary>Keeps the body's eyes at the camera, upright and facing where the camera faces.</summary>
	private void FollowEyes()
	{
		Transform3D eyes = GetParent<Node3D>().GlobalTransform;
		float yaw = eyes.Basis.GetEuler().Y;
		GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw), eyes.Origin);
	}

	private Hand HandOn(Side side)
	{
		return side == Side.Left ? _left : _right;
	}

	/// <summary>Where the shoulder would be if the player stood up straight, in global space.</summary>
	private Vector3 UnleanedShoulder(Side side)
	{
		Transform3D pose = Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(HandOn(side).UpperArmBone);
		return pose.Origin - Lean;
	}

	private float ArmLength(string side)
	{
		Vector3 shoulder = Skeleton.GetBoneGlobalRest(Skeleton.FindBone(side + "UpperArm")).Origin;
		Vector3 elbow = Skeleton.GetBoneGlobalRest(Skeleton.FindBone(side + "LowerArm")).Origin;
		Vector3 wrist = Skeleton.GetBoneGlobalRest(Skeleton.FindBone(side + "Hand")).Origin;
		return (shoulder.DistanceTo(elbow) + elbow.DistanceTo(wrist)) * Skeleton.GlobalBasis.Scale.X;
	}

	/// <summary>How far the player has to lean so the wrist at <paramref name="wrist"/>, in global space, is within comfortable reach.</summary>
	private Vector3 LeanToReach(Side side, Vector3 wrist)
	{
		Vector3 toWrist = wrist - UnleanedShoulder(side);
		float beyondReach = toWrist.Length() - _reach;
		return beyondReach <= 0f ? Vector3.Zero : toWrist.Normalized() * Mathf.Min(beyondReach, MaxLean);
	}

	private Vector3 WristAt(Transform3D grip)
	{
		return grip.Origin + grip.Basis.Orthonormalized() * WristOffset;
	}

	/// <summary>Where the wrist wants to be now, in the body's space.</summary>
	private Vector3 WristOf(Hand hand)
	{
		Vector3 wrist = ToLocal(WristAt(hand.To.GlobalTransform));
		return hand.From.Lerp(wrist, hand.Progress);
	}

	private void PlaceHand(Hand hand, Vector3 wrist)
	{
		Basis grip = (GlobalBasis.Inverse() * hand.To.GlobalBasis).Orthonormalized();
		Quaternion rotation = hand.FromRotation.Slerp(grip.GetRotationQuaternion(), hand.Progress);
		hand.Target.Transform = new Transform3D(new Basis(rotation), wrist);

		float window = Mathf.SmoothStep(hand.CurlWindow.X, hand.CurlWindow.Y, hand.Progress);
		hand.Curl = Mathf.Lerp(hand.FromCurl, hand.ToCurl, window);
	}

	private void MoveHand(Hand hand, Node3D destination, float seconds, float curl, Vector2 curlWindow)
	{
		hand.From = hand.Target.Position;
		hand.FromRotation = hand.Target.Basis.GetRotationQuaternion();
		hand.FromCurl = hand.Curl;
		hand.ToCurl = curl;
		hand.CurlWindow = curlWindow;
		hand.To = destination;
		hand.Progress = 0f;
		hand.Tween?.Kill();
		hand.Tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		hand.Tween.TweenMethod(Callable.From<float>(value => hand.Progress = value), 0f, 1f, seconds);
	}
}
