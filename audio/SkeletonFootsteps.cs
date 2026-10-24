using Godot;

/// <summary>
/// Plays a footstep whenever an animated character's foot lands, so the steps stay in time with any animation
/// and any blend between them. A foot lands when it stops coming down. Its height is measured from the body,
/// so walking down a slope doesn't count as the foot coming down. Faster steps are louder.
/// </summary>
public partial class SkeletonFootsteps : Node
{
	private static readonly string[] FootBones = { "LeftFoot", "RightFoot" };
	// Smoothing the foot's speed hides frames where the animation hasn't moved yet.
	private const float SpeedSmoothing = 40f;

	private sealed class Foot
	{
		public int Bone;
		public float Height;
		public float DownSpeed;
		public bool IsComingDown;
		public float SecondsSinceLanding;
	}

	[Export] public CharacterBody3D Body { get; set; }
	[Export] public Skeleton3D Skeleton { get; set; }
	[Export] public Footsteps Footsteps { get; set; }

	/// <summary>A foot moving down faster than this, in metres per second, is coming down for a step.</summary>
	[Export] public float DescendSpeed { get; set; } = 0.3f;
	/// <summary>A foot coming down has landed once it slows below this.</summary>
	[Export] public float LandedSpeed { get; set; } = 0.1f;
	/// <summary>A foot that just landed can bounce a little; it can't land again this soon.</summary>
	[Export] public float MinStepSeconds { get; set; } = 0.3f;

	[ExportGroup("Loudness")]
	[Export] public float SlowSpeed { get; set; } = 1f;
	[Export] public float FastSpeed { get; set; } = 4.5f;
	[Export] public float SlowStepVolumeDb { get; set; } = -6f;
	[Export] public float FastStepVolumeDb { get; set; } = 2f;

	private readonly Foot[] _feet = new Foot[FootBones.Length];

	public override void _Ready()
	{
		for (int i = 0; i < FootBones.Length; i++)
		{
			_feet[i] = new Foot { Bone = Skeleton.FindBone(FootBones[i]) };
			_feet[i].Height = HeightOf(_feet[i]);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (dt <= 0f)
			return;

		foreach (Foot foot in _feet)
		{
			float height = HeightOf(foot);
			float downSpeed = (foot.Height - height) / dt;
			foot.Height = height;
			foot.DownSpeed = Mathf.Lerp(foot.DownSpeed, downSpeed, 1f - Mathf.Exp(-SpeedSmoothing * dt));
			foot.SecondsSinceLanding += dt;

			if (foot.DownSpeed > DescendSpeed)
			{
				foot.IsComingDown = true;
			}
			else if (foot.IsComingDown && foot.DownSpeed < LandedSpeed)
			{
				foot.IsComingDown = false;
				if (foot.SecondsSinceLanding >= MinStepSeconds)
					Footsteps.Step(StepVolumeDb());
				foot.SecondsSinceLanding = 0f;
			}
		}
	}

	private float HeightOf(Foot foot)
	{
		Vector3 ankle = Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(foot.Bone).Origin;
		return ankle.Y - Body.GlobalPosition.Y;
	}

	private float StepVolumeDb()
	{
		float speed = new Vector2(Body.Velocity.X, Body.Velocity.Z).Length();
		float pace = Mathf.Clamp(Mathf.InverseLerp(SlowSpeed, FastSpeed, speed), 0f, 1f);
		return Mathf.Lerp(SlowStepVolumeDb, FastStepVolumeDb, pace);
	}
}
