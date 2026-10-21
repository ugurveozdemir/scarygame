using Godot;

/// <summary>
/// Shrinks the given bones to nothing after the animation is applied, hiding those body parts.
/// The first-person body uses it to remove its head, so the camera can sit between the shoulders.
/// </summary>
public partial class HideBonesModifier : SkeletonModifier3D
{
	private static readonly Vector3 HiddenScale = Vector3.One * 0.001f;

	[Export] public string[] BoneNames { get; set; } = { "Neck" };

	public override void _ProcessModificationWithDelta(double delta)
	{
		Skeleton3D skeleton = GetSkeleton();
		foreach (string boneName in BoneNames)
			skeleton.SetBonePoseScale(skeleton.FindBone(boneName), HiddenScale);
	}
}
