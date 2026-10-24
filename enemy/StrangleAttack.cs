using System;
using Godot;

/// <summary>
/// The enemy's kill move: it lunges at the player and grabs them by both shoulders, shakes them,
/// then moves its hands to the player's throat and strangles them.
/// The hands are placed with inverse kinematics, so the move fits wherever the player stands.
/// Nothing jumps: the body and hands always blend from where they are towards where they should be.
/// </summary>
public partial class StrangleAttack : Node
{
	[Export] public float GrabDistance { get; set; } = 0.5f;
	[Export] public float LungeSeconds { get; set; } = 0.4f;
	[Export] public float ShoveSpeed { get; set; } = 1.5f;
	[Export] public float ShakeSeconds { get; set; } = 1.2f;
	[Export] public float ShakesPerSecond { get; set; } = 3f;
	[Export] public float ShakeDistance { get; set; } = 0.07f;
	[Export] public float MoveToThroatSeconds { get; set; } = 0.6f;
	/// <summary>Moving to the throat pushes the victim back to arm's length, so the arms stay in view.</summary>
	[Export] public float ThroatPushBack { get; set; } = 0.08f;
	[Export] public float ChokeSeconds { get; set; } = 3f;
	/// <summary>How much further the victim is lifted and pushed back by the end of the choke.</summary>
	[Export] public Vector3 ChokeLift { get; set; } = new(0f, 0.04f, 0.03f);
	[Export] public float HandTremble { get; set; } = 0.008f;

	[ExportGroup("Nodes")]
	[Export] public Node3D Body { get; set; }
	/// <summary>Where the grabbed player looks: between the enemy's chin and chest, so face and arms are both in view.</summary>
	[Export] public Node3D VictimLookTarget { get; set; }
	[Export] public Skeleton3D Skeleton { get; set; }
	[Export] public SkeletonModifier3D ArmsIK { get; set; }
	[Export] public LookAtModifier3D HeadLookAt { get; set; }
	[Export] public Node3D LeftHandTarget { get; set; }
	[Export] public Node3D RightHandTarget { get; set; }

	[ExportGroup("Sounds")]
	[Export] public AudioStreamPlayer3D LungeSound { get; set; }
	/// <summary>The hands slamming into the victim's shoulders.</summary>
	[Export] public AudioStreamPlayer3D GrabSound { get; set; }
	/// <summary>Clothes and bodies jostling while the victim is shaken.</summary>
	[Export] public AudioStreamPlayer3D ShakeSound { get; set; }
	/// <summary>The victim choking, from the hands reaching the throat until they pass out.</summary>
	[Export] public AudioStreamPlayer3D ChokeSound { get; set; }

	public bool IsLunging { get; private set; }

	private Player _victim;
	private Vector3 _lungeStart;
	private float _lungeWeight;
	private Vector3 _leftHandStart;
	private Vector3 _rightHandStart;
	private Node3D _leftHandGoal;
	private Node3D _rightHandGoal;
	private float _handWeight;
	private float _tremble;

	public override void _Process(double delta)
	{
		if (_victim is null)
			return;

		FollowVictim();
		PlaceHands();
	}

	public async void Begin(Player victim)
	{
		_victim = victim;
		_lungeStart = Body.GlobalPosition;
		victim.GetGrabbed(VictimLookTarget, FlatDirection(Body.GlobalPosition, victim.GlobalPosition) * ShoveSpeed);
		HeadLookAt.TargetNode = HeadLookAt.GetPathTo(victim.Head);

		// The arms start from where the running animation left them.
		LeftHandTarget.GlobalPosition = HandPosition("LeftHand");
		RightHandTarget.GlobalPosition = HandPosition("RightHand");

		// Facing each other, the enemy's left hand grabs the player's right side and vice versa.
		ReachFor(victim.RightShoulder, victim.LeftShoulder);
		IsLunging = true;
		LungeSound.Play();
		Tween lunge = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		lunge.TweenMethod(Callable.From<float>(weight => _lungeWeight = _handWeight = weight), 0f, 1f, LungeSeconds);
		lunge.TweenProperty(ArmsIK, "influence", 1f, LungeSeconds);
		lunge.TweenProperty(HeadLookAt, "influence", 1f, LungeSeconds);
		await ToSignal(lunge, Tween.SignalName.Finished);
		IsLunging = false;
		GrabSound.Play();
		ShakeSound.Play();

		await Animate(progress =>
		{
			float fade = 1f - progress;
			float push = Mathf.Sin(progress * ShakeSeconds * ShakesPerSecond * Mathf.Tau) * ShakeDistance * fade;
			victim.BodySway = new Vector3(0f, 0f, push);
		}, ShakeSeconds);

		ReachFor(victim.ThroatRight, victim.ThroatLeft);
		ChokeSound.Play();
		await Animate(progress =>
		{
			_handWeight = Mathf.SmoothStep(0f, 1f, progress);
			victim.BodySway = new Vector3(0f, 0f, ThroatPushBack * _handWeight);
		}, MoveToThroatSeconds);

		await Animate(progress =>
		{
			victim.ChokeAmount = progress;
			victim.BodySway = new Vector3(0f, 0f, ThroatPushBack) + ChokeLift * progress;
			_tremble = Mathf.Sin(progress * 60f) * HandTremble;
		}, ChokeSeconds);

		victim.Die();
	}

	private void FollowVictim()
	{
		// Stay at arm's length in front of the victim, even while they stagger back.
		Vector3 grabPosition = _victim.GlobalPosition + FlatDirection(_victim.GlobalPosition, Body.GlobalPosition) * GrabDistance;
		Body.GlobalPosition = _lungeStart.Lerp(grabPosition, _lungeWeight);
		Body.LookAt(new Vector3(_victim.GlobalPosition.X, Body.GlobalPosition.Y, _victim.GlobalPosition.Z), Vector3.Up);
	}

	private void ReachFor(Node3D leftHandGoal, Node3D rightHandGoal)
	{
		// Remember the starting points relative to the body, so the hands move along with it.
		_leftHandStart = Body.ToLocal(LeftHandTarget.GlobalPosition);
		_rightHandStart = Body.ToLocal(RightHandTarget.GlobalPosition);
		_leftHandGoal = leftHandGoal;
		_rightHandGoal = rightHandGoal;
		_handWeight = 0f;
	}

	private void PlaceHands()
	{
		Vector3 tremble = Vector3.Up * _tremble;
		LeftHandTarget.GlobalPosition = Body.ToGlobal(_leftHandStart).Lerp(_leftHandGoal.GlobalPosition, _handWeight) + tremble;
		RightHandTarget.GlobalPosition = Body.ToGlobal(_rightHandStart).Lerp(_rightHandGoal.GlobalPosition, _handWeight) - tremble;
	}

	private Vector3 HandPosition(string boneName)
	{
		return Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(Skeleton.FindBone(boneName)).Origin;
	}

	private SignalAwaiter Animate(Action<float> apply, float seconds)
	{
		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From(apply), 0f, 1f, seconds);
		return ToSignal(tween, Tween.SignalName.Finished);
	}

	private static Vector3 FlatDirection(Vector3 from, Vector3 to)
	{
		Vector3 direction = to - from;
		direction.Y = 0f;
		return direction.Normalized();
	}
}
