using Godot;

/// <summary>
/// A first-person move authored in Blender, like getting into a locker. The animated scene holds the player's
/// body, an eye for the view and twins of the prop's moving parts, all animated together, so a hand never
/// slips off a handle. While it plays, the player's camera sits on the eye, the prop's parts copy their twins
/// and the animated body stands in for the player's own arms.
/// The animated scene is posed in the prop's space, so it sits at the prop's origin.
/// </summary>
public partial class PlayerAnimation : Node3D
{
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	[Export] public StringName AnimationName { get; set; }
	/// <summary>The animated scene's root, shown only while the animation plays.</summary>
	[Export] public Node3D AnimatedScene { get; set; }
	/// <summary>The player's root in the animated scene; the eye and the body hang from it.</summary>
	[Export] public Node3D AnimatedRoot { get; set; }
	/// <summary>Where the player looks from. Its -Y axis is the view direction, as Blender's -Z becomes -Y.</summary>
	[Export] public Node3D Eye { get; set; }
	/// <summary>The prop's moving parts, each following the animated twin at the same index.</summary>
	[Export] public Node3D[] Parts { get; set; } = [];
	[Export] public Node3D[] AnimatedParts { get; set; } = [];
	/// <summary>
	/// How many times faster than authored the animation plays. Times passed in and out are in the animation's
	/// own seconds; <see cref="RealSeconds"/> turns them into seconds of play.
	/// </summary>
	[Export] public float Speed { get; set; } = 1f;

	private Player _player;
	private Animation _animation;

	public bool IsPlaying => _player is not null;

	public float Length => (float)_animation.Length;

	public float RealSeconds(float animationSeconds)
	{
		return animationSeconds / Speed;
	}

	public override void _Ready()
	{
		_animation = AnimationPlayer.GetAnimation(AnimationName);
		// Advanced by hand, so the view is placed after the body has moved in the same frame.
		AnimationPlayer.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
		AnimatedScene.Visible = false;
	}

	/// <summary>Where <paramref name="player"/> stands, facing the view, <paramref name="seconds"/> into the animation.</summary>
	public Transform3D PlayerTransformAt(Player player, float seconds)
	{
		return PlayerTransform(EyeAt(seconds), player.StandingHeadHeight);
	}

	/// <summary>How far the view looks up (positive) or down <paramref name="seconds"/> into the animation, in degrees.</summary>
	public float PitchDegreesAt(float seconds)
	{
		return Mathf.RadToDeg(Pitch(EyeAt(seconds)));
	}

	public void Play(Player player, float fromSeconds = 0f)
	{
		_player = player;
		AnimatedScene.Visible = true;
		AnimationPlayer.Play(AnimationName);
		AnimationPlayer.Seek(fromSeconds, true);
		Apply();
	}

	/// <summary>Stops where it is, leaving the prop's parts as they are.</summary>
	public void Stop()
	{
		AnimationPlayer.Stop();
		AnimatedScene.Visible = false;
		_player = null;
	}

	public override void _Process(double delta)
	{
		if (!IsPlaying)
			return;

		AnimationPlayer.Advance(delta * Speed);
		Apply();
		if (!AnimationPlayer.IsPlaying())
			Stop();
	}

	private void Apply()
	{
		Transform3D eye = Eye.GlobalTransform;
		_player.GlobalTransform = PlayerTransform(eye, _player.StandingHeadHeight);
		_player.Head.Rotation = new Vector3(Pitch(eye), 0f, 0f);
		for (int i = 0; i < Parts.Length; i++)
			Parts[i].Transform = AnimatedParts[i].Transform;
	}

	/// <summary>Samples the eye's global transform from the animation's tracks without playing it.</summary>
	private Transform3D EyeAt(float seconds)
	{
		Transform3D root = new(new Basis(RotationAt(AnimatedRoot, seconds)), PositionAt(AnimatedRoot, seconds));
		Transform3D eye = new(new Basis(RotationAt(Eye, seconds)), PositionAt(Eye, seconds));
		return AnimatedScene.GlobalTransform * root * eye;
	}

	// A node that never moves in the animation has no track for it, so it keeps its own value.
	private Vector3 PositionAt(Node3D node, float seconds)
	{
		int track = Track(node, Animation.TrackType.Position3D);
		return track < 0 ? node.Position : _animation.PositionTrackInterpolate(track, seconds);
	}

	private Quaternion RotationAt(Node3D node, float seconds)
	{
		int track = Track(node, Animation.TrackType.Rotation3D);
		return track < 0 ? node.Quaternion : _animation.RotationTrackInterpolate(track, seconds);
	}

	private int Track(Node3D node, Animation.TrackType type)
	{
		NodePath path = AnimationPlayer.GetNode(AnimationPlayer.RootNode).GetPathTo(node);
		return _animation.FindTrack(path, type);
	}

	private static Vector3 ViewDirection(Transform3D eye)
	{
		return -eye.Basis.Y.Normalized();
	}

	private static float Pitch(Transform3D eye)
	{
		return Mathf.Asin(Mathf.Clamp(ViewDirection(eye).Y, -1f, 1f));
	}

	/// <summary>The player standing under the eye, turned the way it looks; the player's forward is -Z.</summary>
	private static Transform3D PlayerTransform(Transform3D eye, float headHeight)
	{
		Vector3 view = ViewDirection(eye);
		float yaw = Mathf.Atan2(-view.X, -view.Z);
		return new Transform3D(new Basis(Vector3.Up, yaw), eye.Origin - Vector3.Up * headHeight);
	}
}
