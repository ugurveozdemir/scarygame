using System.Collections.Generic;
using Godot;

/// <summary>
/// Poses the fingers of each hand between an open, flat hand and a grip by copying their rotations from the first
/// frames of two animations, while the rest of the arm keeps playing its own animation.
/// A strength of 0 is the open hand and 1 is the full grip.
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

	private readonly Dictionary<int, (Quaternion Open, Quaternion Grip)> _leftFingers = new();
	private readonly Dictionary<int, (Quaternion Open, Quaternion Grip)> _rightFingers = new();

	public float LeftStrength { get; set; }
	public float RightStrength { get; set; }

	public void UsePoses(Animation openAnimation, Animation gripAnimation)
	{
		ReadPoses(openAnimation, gripAnimation, "Left", _leftFingers);
		ReadPoses(openAnimation, gripAnimation, "Right", _rightFingers);
	}

	public override void _ProcessModificationWithDelta(double delta)
	{
		PoseFingers(_leftFingers, LeftStrength);
		PoseFingers(_rightFingers, RightStrength);
	}

	private void ReadPoses(Animation openAnimation, Animation gripAnimation, string side,
		Dictionary<int, (Quaternion Open, Quaternion Grip)> fingers)
	{
		Skeleton3D skeleton = GetSkeleton();
		fingers.Clear();
		foreach (string joint in FingerJoints)
		{
			string bone = side + joint;
			string path = $"%GeneralSkeleton:{bone}";
			int openTrack = openAnimation.FindTrack(path, Animation.TrackType.Rotation3D);
			int gripTrack = gripAnimation.FindTrack(path, Animation.TrackType.Rotation3D);
			if (openTrack >= 0 && gripTrack >= 0)
			{
				fingers[skeleton.FindBone(bone)] = (
					openAnimation.RotationTrackInterpolate(openTrack, 0.0),
					gripAnimation.RotationTrackInterpolate(gripTrack, 0.0));
			}
		}
	}

	private void PoseFingers(Dictionary<int, (Quaternion Open, Quaternion Grip)> fingers, float strength)
	{
		Skeleton3D skeleton = GetSkeleton();
		foreach ((int bone, (Quaternion open, Quaternion grip)) in fingers)
			skeleton.SetBonePoseRotation(bone, open.Slerp(grip, strength));
	}
}
