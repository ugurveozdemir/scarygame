using Godot;

/// <summary>
/// The player's own arms, seen in first person only while a hand reaches for something, like a door.
/// The arms belong to a headless body around the camera. IK moves a hand from below the view to a grip point
/// and keeps it there, so the hand follows the grip as it moves. A hand always starts from where it is,
/// so moving between grips never jumps.
/// A grip point also says how the hand is held: its -Z axis is where the fingers point and its -Y axis is where
/// the palm faces, see <see cref="Pose"/>.
/// The arms are only a little longer than a real reach, so for a far grip the whole view model grows around the
/// camera until the elbow can stay comfortably bent. It looks the same as a closer reach would, but touches the grip.
/// </summary>
public partial class ViewHands : Node3D
{
	public enum Side { Left, Right }

	private sealed class Hand
	{
		public Node3D Target;
		public Node3D Rest;
		public int UpperArmBone;
		/// <summary>Where the wrist was when the current move started, in the camera's space so it moves along with the view.</summary>
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
	}

	[Export] public float ReachSeconds { get; set; } = 0.3f;
	[Export] public float WithdrawSeconds { get; set; } = 0.3f;
	[Export] public string BaseAnimation { get; set; } = "ual1/Idle";
	/// <summary>Animation whose first frame provides the curled, gripping finger pose.</summary>
	[Export] public string GripAnimation { get; set; } = "ual1/Idle_Torch";
	/// <summary>How much of its length an arm stretches to reach a far grip, the rest stays as a bend in the elbow.</summary>
	[Export(PropertyHint.Range, "0.5,1")] public float ReachRatio { get; set; } = 0.8f;
	/// <summary>The largest the view model grows to reach a far grip; beyond that the arm stretches.</summary>
	[Export] public float MaxScale { get; set; } = 2.4f;
	/// <summary>Where the wrist sits relative to a grip point, in the grip's space: behind and above the palm.</summary>
	[Export] public Vector3 WristOffset { get; set; } = new(0f, 0.03f, 0.09f);

	[ExportGroup("Nodes")]
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	[Export] public AnimationLibrary ExtraAnimations { get; set; }
	[Export] public Skeleton3D Skeleton { get; set; }
	[Export] public GripFingersModifier GripFingers { get; set; }
	[Export] public Node3D LeftHandTarget { get; set; }
	[Export] public Node3D RightHandTarget { get; set; }
	/// <summary>Where a hand waits below the view when it is not reaching for anything.</summary>
	[Export] public Node3D LeftRest { get; set; }
	[Export] public Node3D RightRest { get; set; }

	private Hand _left;
	private Hand _right;
	private float _reach;

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
		_left = new Hand { Target = LeftHandTarget, Rest = LeftRest, To = LeftRest, UpperArmBone = Skeleton.FindBone("LeftUpperArm") };
		_right = new Hand { Target = RightHandTarget, Rest = RightRest, To = RightRest, UpperArmBone = Skeleton.FindBone("RightUpperArm") };
		_reach = ArmLength("Left") * ReachRatio;
		AnimationPlayer.AddAnimationLibrary("ual1", ExtraAnimations);
		AnimationPlayer.Play(BaseAnimation);
		GripFingers.UsePose(AnimationPlayer.GetAnimation(GripAnimation));
		Visible = false;
	}

	public override void _Process(double delta)
	{
		Vector3 leftWrist = WristOf(_left);
		Vector3 rightWrist = WristOf(_right);
		float scale = Mathf.Max(ScaleToReach(leftWrist, Side.Left), ScaleToReach(rightWrist, Side.Right));
		Scale = Vector3.One * scale;

		PlaceHand(_left, leftWrist / scale);
		PlaceHand(_right, rightWrist / scale);
		GripFingers.LeftStrength = _left.Curl;
		GripFingers.RightStrength = _right.Curl;
		Visible = _left.IsOut || _right.IsOut;
	}

	/// <summary>
	/// Moves a hand into view until it holds <paramref name="grip"/>, then keeps following it.
	/// <paramref name="curl"/> is how tightly the fingers close around it: 0 is an open hand, 1 a fist.
	/// </summary>
	public SignalAwaiter Reach(Side side, Node3D grip, float? seconds = null, float curl = 0f)
	{
		Hand hand = HandOn(side);
		hand.IsOut = true;
		return MoveHand(hand, grip, seconds ?? ReachSeconds, curl, new Vector2(0.5f, 1f));
	}

	/// <summary>Lowers a hand back out of view.</summary>
	public SignalAwaiter Withdraw(Side side, float? seconds = null)
	{
		Hand hand = HandOn(side);
		SignalAwaiter finished = MoveHand(hand, hand.Rest, seconds ?? WithdrawSeconds, 0f, new Vector2(0f, 0.5f));
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
			hand.ToCurl = 0f;
			hand.Curl = 0f;
			hand.IsOut = false;
		}
	}

	private Hand HandOn(Side side)
	{
		return side == Side.Left ? _left : _right;
	}

	private Vector3 ShoulderOf(Side side)
	{
		Transform3D pose = Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(HandOn(side).UpperArmBone);
		return ToLocal(pose.Origin);
	}

	private float ArmLength(string side)
	{
		Vector3 shoulder = Skeleton.GetBoneGlobalRest(Skeleton.FindBone(side + "UpperArm")).Origin;
		Vector3 elbow = Skeleton.GetBoneGlobalRest(Skeleton.FindBone(side + "LowerArm")).Origin;
		Vector3 wrist = Skeleton.GetBoneGlobalRest(Skeleton.FindBone(side + "Hand")).Origin;
		return (shoulder.DistanceTo(elbow) + elbow.DistanceTo(wrist)) * Skeleton.GlobalBasis.Scale.X;
	}

	/// <summary>Where the wrist wants to be now, in the camera's space.</summary>
	private Vector3 WristOf(Hand hand)
	{
		Transform3D grip = GetParent<Node3D>().GlobalTransform.AffineInverse() * hand.To.GlobalTransform;
		Vector3 wrist = grip.Origin + grip.Basis.Orthonormalized() * WristOffset * Scale.X;
		return hand.From.Lerp(wrist, hand.Progress);
	}

	/// <summary>
	/// The smallest view model scale, at least 1, with which the wrist at <paramref name="wrist"/> is within
	/// the comfortable reach of its shoulder. The wrist is then at wrist / scale in this node's space.
	/// </summary>
	private float ScaleToReach(Vector3 wrist, Side side)
	{
		Vector3 shoulder = ShoulderOf(side);
		float a = wrist.LengthSquared();
		float b = wrist.Dot(shoulder);
		float c = shoulder.LengthSquared() - _reach * _reach;
		if (a < 0.0001f)
			return 1f;

		// Solves |wrist * u - shoulder| = reach for the largest u, where u is 1 / scale.
		float discriminant = b * b - a * c;
		float u = discriminant < 0f ? b / a : (b + Mathf.Sqrt(discriminant)) / a;
		return u <= 0f ? MaxScale : Mathf.Clamp(1f / u, 1f, MaxScale);
	}

	private void PlaceHand(Hand hand, Vector3 wrist)
	{
		Basis grip = (GetParent<Node3D>().GlobalTransform.AffineInverse() * hand.To.GlobalTransform).Basis.Orthonormalized();
		Quaternion rotation = hand.FromRotation.Slerp(grip.GetRotationQuaternion(), hand.Progress);
		hand.Target.Transform = new Transform3D(new Basis(rotation), wrist);

		float window = Mathf.SmoothStep(hand.CurlWindow.X, hand.CurlWindow.Y, hand.Progress);
		hand.Curl = Mathf.Lerp(hand.FromCurl, hand.ToCurl, window);
	}

	private SignalAwaiter MoveHand(Hand hand, Node3D destination, float seconds, float curl, Vector2 curlWindow)
	{
		hand.From = Transform * hand.Target.Position;
		hand.FromRotation = hand.Target.Basis.GetRotationQuaternion();
		hand.FromCurl = hand.Curl;
		hand.ToCurl = curl;
		hand.CurlWindow = curlWindow;
		hand.To = destination;
		hand.Progress = 0f;
		hand.Tween?.Kill();
		hand.Tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		hand.Tween.TweenMethod(Callable.From<float>(value => hand.Progress = value), 0f, 1f, seconds);
		return ToSignal(hand.Tween, Tween.SignalName.Finished);
	}
}
