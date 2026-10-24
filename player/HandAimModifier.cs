using Godot;

/// <summary>
/// Turns each hand to match its target node after the arm IK has put the wrist in place.
/// A target's -Z axis is the direction the fingers point and its -Y axis is the direction the palm faces,
/// so an unrotated target is a flat hand with the palm down. Which bone axes those are is read from the
/// skeleton's rest pose, so any humanoid works.
/// </summary>
public partial class HandAimModifier : SkeletonModifier3D
{
	private sealed class HandAim
	{
		public int Hand;
		public int Parent;
		public Node3D Target;
		/// <summary>The hand's fingers, palm and their cross product in the bone's own space, as columns.</summary>
		public Basis RestFrame;
	}

	[Export] public Node3D LeftTarget { get; set; }
	[Export] public Node3D RightTarget { get; set; }

	private HandAim _left;
	private HandAim _right;

	public override void _Ready()
	{
		_left = CreateAim("Left", LeftTarget);
		_right = CreateAim("Right", RightTarget);
	}

	public override void _ProcessModificationWithDelta(double delta)
	{
		TurnHand(_left);
		TurnHand(_right);
	}

	private HandAim CreateAim(string side, Node3D target)
	{
		Skeleton3D skeleton = GetSkeleton();
		int hand = skeleton.FindBone(side + "Hand");
		Transform3D rest = skeleton.GetBoneGlobalRest(hand);
		Vector3 fingers = (RestOrigin(skeleton, side + "MiddleProximal") - rest.Origin).Normalized();
		Vector3 thumb = RestOrigin(skeleton, side + "ThumbProximal") - rest.Origin;
		thumb -= fingers * thumb.Dot(fingers);

		// The palm side follows from which side the thumb is on, mirrored between the hands.
		Vector3 palm = (side == "Left" ? fingers.Cross(thumb) : thumb.Cross(fingers)).Normalized();
		Basis toBone = rest.Basis.Orthonormalized().Inverse();
		Vector3 boneFingers = toBone * fingers;
		Vector3 bonePalm = toBone * palm;

		return new HandAim
		{
			Hand = hand,
			Parent = skeleton.GetBoneParent(hand),
			Target = target,
			RestFrame = new Basis(boneFingers, bonePalm, boneFingers.Cross(bonePalm)),
		};
	}

	private void TurnHand(HandAim aim)
	{
		Skeleton3D skeleton = GetSkeleton();
		Basis target = skeleton.GlobalBasis.Orthonormalized().Inverse() * aim.Target.GlobalBasis.Orthonormalized();
		Vector3 fingers = -target.Z;
		Vector3 palm = -target.Y;
		Basis wanted = new Basis(fingers, palm, fingers.Cross(palm)) * aim.RestFrame.Transposed();
		Basis parent = skeleton.GetBoneGlobalPose(aim.Parent).Basis.Orthonormalized();
		skeleton.SetBonePoseRotation(aim.Hand, (parent.Inverse() * wanted).GetRotationQuaternion());
	}

	private static Vector3 RestOrigin(Skeleton3D skeleton, string bone)
	{
		return skeleton.GetBoneGlobalRest(skeleton.FindBone(bone)).Origin;
	}
}
