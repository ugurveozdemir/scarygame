using System.Collections.Generic;
using Godot;

/// <summary>
/// Curls the fingers into a grip by copying their rotations from the first frame of a gripping animation,
/// while the rest of the body keeps playing its own animation.
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

	private readonly Dictionary<int, Quaternion> _gripRotations = new();

	public void UsePose(Animation gripAnimation)
	{
		Skeleton3D skeleton = GetSkeleton();
		_gripRotations.Clear();
		foreach (string side in new[] { "Left", "Right" })
		{
			foreach (string joint in FingerJoints)
			{
				string bone = side + joint;
				int track = gripAnimation.FindTrack($"%GeneralSkeleton:{bone}", Animation.TrackType.Rotation3D);
				if (track >= 0)
					_gripRotations[skeleton.FindBone(bone)] = gripAnimation.RotationTrackInterpolate(track, 0.0);
			}
		}
	}

	public override void _ProcessModificationWithDelta(double delta)
	{
		Skeleton3D skeleton = GetSkeleton();
		foreach ((int bone, Quaternion rotation) in _gripRotations)
			skeleton.SetBonePoseRotation(bone, rotation);
	}
}
