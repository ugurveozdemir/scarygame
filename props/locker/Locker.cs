using System;
using Godot;

/// <summary>
/// A locker the player can hide in, looking out through the slits in its door.
/// Getting in and out is one continuous motion with overlapping steps: the player walks up while reaching
/// for the handle, pulls the door open while stepping out of its way, steps in turning around and pulls
/// the door shut behind them.
/// </summary>
public partial class Locker : StaticBody3D, IInteractable
{
	private const float ApproachPitchDegrees = -15f;
	private const float InsidePitchDegrees = -20f;
	private const float ExitPitchDegrees = 0f;

	[ExportGroup("Door")]
	[Export] public float DoorOpenAngleDegrees { get; set; } = 100f;
	/// <summary>How far the hand pulls the door before letting it swing the rest of the way.</summary>
	[Export(PropertyHint.Range, "0,1")] public float DoorPullAmount { get; set; } = 0.3f;
	[Export] public float PullSeconds { get; set; } = 0.4f;
	[Export] public float SwingOpenSeconds { get; set; } = 0.5f;
	[Export] public float CloseSeconds { get; set; } = 0.55f;
	[Export] public float PushOpenSeconds { get; set; } = 0.5f;

	[ExportGroup("Walking")]
	/// <summary>Walking pace used to time the walk up to the locker when the player is standing still.</summary>
	[Export] public float ApproachSpeed { get; set; } = 1.6f;
	[Export] public float MinApproachSeconds { get; set; } = 0.3f;
	[Export] public float StepInSeconds { get; set; } = 0.75f;
	[Export] public float StepOutSeconds { get; set; } = 0.65f;

	[ExportGroup("Nodes")]
	[Export] public Node3D Door { get; set; }
	[Export] public Node3D OutsideHandle { get; set; }
	[Export] public Node3D InsideGrip { get; set; }
	[Export] public Marker3D ApproachPoint { get; set; }
	/// <summary>Where the player steps back to while pulling the door, out of its way.</summary>
	[Export] public Marker3D PullPoint { get; set; }
	[Export] public Marker3D HidePoint { get; set; }
	[Export] public Marker3D ExitPoint { get; set; }

	private Player _occupant;
	private float _doorOpenness;
	private Player _player;
	private Tween _sequence;
	private Transform3D _walkStart;

	private bool IsOccupied => _occupant is not null && _occupant.HidingSpot == this;

	public string InteractionPrompt => IsOccupied ? "Leave" : "Hide";

	public void Interact(Player player)
	{
		if (IsOccupied)
			LetOut(player);
		else
			TakeIn(player);
	}

	private void TakeIn(Player player)
	{
		BeginSequence(player);
		float time = 0f;
		if (_doorOpenness == 0f)
		{
			// Walk up while already reaching for the handle, carrying on from however the player was moving.
			Vector3 velocity = new(player.Velocity.X, 0f, player.Velocity.Z);
			float distance = player.GlobalPosition.DistanceTo(ApproachPoint.GlobalPosition);
			float approach = Mathf.Max(1.5f * distance / Mathf.Max(velocity.Length(), ApproachSpeed), MinApproachSeconds);
			Walk(ApproachPoint, ApproachPitchDegrees, time, approach, velocity);
			At(approach * 0.3f, () => player.Hands.Reach(ViewHands.Side.Right, OutsideHandle, approach * 0.7f));
			time += approach;

			// Pull the door while stepping out of its way, then let it swing open by itself.
			Walk(PullPoint, ApproachPitchDegrees, time, PullSeconds);
			SwingDoor(DoorPullAmount, time, PullSeconds);
			time += PullSeconds;
			At(time, () => player.Hands.Withdraw(ViewHands.Side.Right));
			SwingDoor(1f, time, SwingOpenSeconds);
		}

		// Step in while the door is still swinging, turning around and reaching back for it.
		Walk(HidePoint, InsidePitchDegrees, time, StepInSeconds);
		At(time + StepInSeconds * 0.6f, () => player.Hands.Reach(ViewHands.Side.Right, InsideGrip, StepInSeconds * 0.4f));
		time += StepInSeconds;
		At(time, () =>
		{
			_occupant = player;
			player.StartHiding(this, ExitPoint.GlobalTransform);
		});
		SwingDoor(0f, time, CloseSeconds);
		time += CloseSeconds;
		At(time, () => player.Hands.Withdraw(ViewHands.Side.Right));
		At(time, FinishSequence);
	}

	private void LetOut(Player player)
	{
		BeginSequence(player);
		float reach = player.Hands.ReachSeconds;
		At(0f, () => player.Hands.Reach(ViewHands.Side.Right, InsideGrip, reach));

		// Push the door open and step out right behind it.
		SwingDoor(1f, reach, PushOpenSeconds);
		At(reach + PushOpenSeconds * 0.4f, () => player.Hands.Withdraw(ViewHands.Side.Right));
		float stepOut = reach + PushOpenSeconds * 0.3f;
		At(stepOut, player.StopHiding);
		Walk(ExitPoint, ExitPitchDegrees, stepOut, StepOutSeconds);
		At(stepOut + StepOutSeconds, FinishSequence);
	}

	private void BeginSequence(Player player)
	{
		_player = player;
		player.LockControls();
		player.Grabbed += AbortSequence;
		// A parallel tween is a timeline: every step starts after its own delay, so steps can overlap.
		_sequence = CreateTween().SetParallel().SetProcessMode(Tween.TweenProcessMode.Physics);
	}

	private void FinishSequence()
	{
		_player.Grabbed -= AbortSequence;
		_player.UnlockControls();
	}

	private void AbortSequence()
	{
		// The enemy grabbed the player and takes over from here.
		_sequence.Kill();
		_player.Grabbed -= AbortSequence;
	}

	private void At(float time, Action action)
	{
		_sequence.TweenCallback(Callable.From(action)).SetDelay(time);
	}

	/// <summary>
	/// Walks the player to <paramref name="point"/>, starting at <paramref name="startVelocity"/> and ending at rest.
	/// The path is a cubic Hermite curve, so a player who was already walking curves smoothly into it.
	/// </summary>
	private void Walk(Node3D point, float headPitchDegrees, float start, float seconds, Vector3 startVelocity = default)
	{
		At(start, () => _walkStart = _player.GlobalTransform);
		_sequence.TweenMethod(Callable.From<float>(t =>
		{
			Transform3D end = point.GlobalTransform;
			float t2 = t * t;
			float t3 = t2 * t;
			Vector3 position = (2f * t3 - 3f * t2 + 1f) * _walkStart.Origin
				+ (t3 - 2f * t2 + t) * seconds * startVelocity
				+ (3f * t2 - 2f * t3) * end.Origin;
			// The player only turns around the vertical axis, so blending the yaw is enough
			// and always yields a clean rotation.
			float yaw = Mathf.LerpAngle(_walkStart.Basis.GetEuler().Y, end.Basis.GetEuler().Y, Mathf.SmoothStep(0f, 1f, t));
			_player.GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw), position);
		}), 0f, 1f, seconds).SetDelay(start);
		_sequence.TweenProperty(_player.Head, "rotation:x", Mathf.DegToRad(headPitchDegrees), seconds)
			.SetDelay(start).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void SwingDoor(float openness, float start, float seconds)
	{
		// A negative angle swings the door outwards, towards the locker's front (-Z).
		float angle = -Mathf.DegToRad(DoorOpenAngleDegrees) * openness;
		_sequence.TweenProperty(Door, "rotation:y", angle, seconds)
			.SetDelay(start).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		At(start + seconds, () => _doorOpenness = openness);
	}
}
