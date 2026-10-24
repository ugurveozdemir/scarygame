using System.Collections.Generic;
using Godot;

/// <summary>
/// Curls the fingers of each hand towards a grip by copying their rotations from the first frame of a gripping
/// animation, while the rest of the body keeps playing its own animation.
/// A strength of 0 leaves the animated fingers alone and 1 is the full grip.
/// </summary>
public partial class GripFingersModifier : SkeletonModifier3D
{
	private static readonly string[] FingerJoints =
	{
		"ThumbMetacarpal", "ThumbProximal", "ThumbDistal",
		"IndexProximal", "IndexIntermediate", "IndexDistal",
		"MiddleProximal", "MiddleIntermediate", "MiddleDistal",
		"RingProximal", "RingIntermediate", "RingDistal",
		"LittleProximal", "LittleIntermediate", "LittleDistal",
	};

	private readonly Dictionary<int, Quaternion> _leftGrip = new();
	private readonly Dictionary<int, Quaternion> _rightGrip = new();

	public float LeftStrength { get; set; }
	public float RightStrength { get; set; }

	public void UsePose(Animation gripAnimation)
	{
		ReadGrip(gripAnimation, "Left", _leftGrip);
		ReadGrip(gripAnimation, "Right", _rightGrip);
	}

	public override void _ProcessModificationWithDelta(double delta)
	{
		Curl(_leftGrip, LeftStrength);
		Curl(_rightGrip, RightStrength);
	}

	private void ReadGrip(Animation gripAnimation, string side, Dictionary<int, Quaternion> grip)
	{
		Skeleton3D skeleton = GetSkeleton();
		grip.Clear();
		foreach (string joint in FingerJoints)
		{
			string bone = side + joint;
			int track = gripAnimation.FindTrack($"%GeneralSkeleton:{bone}", Animation.TrackType.Rotation3D);
			if (track >= 0)
				grip[skeleton.FindBone(bone)] = gripAnimation.RotationTrackInterpolate(track, 0.0);
		}
	}

	private void Curl(Dictionary<int, Quaternion> grip, float strength)
	{
		if (strength <= 0f)
			return;

		Skeleton3D skeleton = GetSkeleton();
		foreach ((int bone, Quaternion rotation) in grip)
			skeleton.SetBonePoseRotation(bone, skeleton.GetBonePoseRotation(bone).Slerp(rotation, strength));
	}
}
