using Godot;

/// <summary>
/// A locker the player can hide in, looking out through the slits in its door.
/// Getting in and out is one continuous motion with overlapping steps: the player walks up while reaching
/// for the handle, pulls the door open while stepping out of its way, steps in turning around and pulls
/// the door shut behind them. An enemy checking the locker tears the door open.
/// </summary>
public partial class Locker : StaticBody3D, IHidingSpot
{
	private const float ApproachPitchDegrees = -15f;
	private const float InsidePitchDegrees = -20f;
	private const float ExitPitchDegrees = 0f;

	// The door's front is towards -Z and its hinge towards +X. The player pulls the handle with the fingers
	// towards the door and the palm facing the hinge, holds the door's edge from inside, and pushes it flat-handed.
	private static readonly Basis HandlePose = ViewHands.Pose(Vector3.Back, Vector3.Right);
	private static readonly Basis EdgePose = ViewHands.Pose(Vector3.Forward, Vector3.Left);
	private static readonly Basis PushPose = ViewHands.Pose(Vector3.Up, Vector3.Forward);

	[ExportGroup("Door")]
	[Export] public float DoorOpenAngleDegrees { get; set; } = 100f;
	/// <summary>How far the hand pulls the door before letting it swing the rest of the way.</summary>
	[Export(PropertyHint.Range, "0,1")] public float DoorPullAmount { get; set; } = 0.3f;
	[Export] public float PullSeconds { get; set; } = 0.4f;
	[Export] public float SwingOpenSeconds { get; set; } = 0.5f;
	[Export] public float CloseSeconds { get; set; } = 0.55f;
	[Export] public float PushOpenSeconds { get; set; } = 0.5f;
	[Export] public float TearOpenSeconds { get; set; } = 0.15f;

	/// <summary>How tightly the fingers close around the handle and the door's edge, from 0 for an open hand to 1 for a fist.</summary>
	[Export(PropertyHint.Range, "0,1")] public float HandleCurl { get; set; } = 0.7f;

	[ExportGroup("Walking")]
	/// <summary>Walking pace used to time the walk up to the locker when the player is standing still.</summary>
	[Export] public float ApproachSpeed { get; set; } = 1.6f;
	[Export] public float MinApproachSeconds { get; set; } = 0.3f;
	[Export] public float StepInSeconds { get; set; } = 0.75f;
	[Export] public float StepOutSeconds { get; set; } = 0.65f;

	[ExportGroup("Sounds")]
	[Export] public AudioStreamPlayer3D LatchSound { get; set; }
	[Export] public AudioStreamPlayer3D CreakSound { get; set; }
	/// <summary>The door clicking shut, pulled closed gently from inside.</summary>
	[Export] public AudioStreamPlayer3D ShutSound { get; set; }
	/// <summary>The door torn open and slamming into the locker next to it.</summary>
	[Export] public AudioStreamPlayer3D TearOpenSound { get; set; }
	/// <summary>The player's shoulder knocking against the sheet metal while squeezing in.</summary>
	[Export] public AudioStreamPlayer3D BumpSound { get; set; }

	[ExportGroup("Nodes")]
	[Export] public Node3D Door { get; set; }
	[Export] public Node3D OutsideHandle { get; set; }
	[Export] public Node3D InsideGrip { get; set; }
	[Export] public Marker3D ApproachPoint { get; set; }
	/// <summary>Where the player steps back to while pulling the door, out of its way.</summary>
	[Export] public Marker3D PullPoint { get; set; }
	[Export] public Marker3D HidePoint { get; set; }
	[Export] public Marker3D ExitPoint { get; set; }
	[Export] public Node3D SearchPoint { get; set; }
	[Export] public Node3D LookPoint { get; set; }

	private Player _occupant;
	private float _doorOpenness;
	private Player _player;
	private ScriptedMove _move;
	private Tween _doorTween;

	public bool IsOccupied => _occupant is not null && _occupant.HidingSpot == this;

	public string InteractionPrompt => IsOccupied ? "Leave" : "Hide";

	public override void _Ready()
	{
		OutsideHandle.Basis = HandlePose;
		AddToGroup(IHidingSpot.Group);
	}

	public void Interact(Player player)
	{
		if (IsOccupied)
			LetOut(player);
		else
			TakeIn(player);
	}

	public void Reveal()
	{
		TearOpenSound.Play();
		SwingDoorNow(1f, TearOpenSeconds);
	}

	public void Conceal()
	{
		CreakSound.Play();
		SwingDoorNow(0f, CloseSeconds).TweenCallback(Callable.From(() => ShutSound.Play()));
	}

	public void DragOutOccupant()
	{
		_move?.Kill();
		_occupant.Hit -= OnHit;
		_occupant.PullOutOfHiding();
		_occupant = null;
	}

	private void TakeIn(Player player)
	{
		BeginMove(player);
		player.Hit += OnHit;
		float time = 0f;
		if (_doorOpenness == 0f)
		{
			// Walk up while already reaching for the handle, carrying on from however the player was moving.
			Vector3 velocity = new(player.Velocity.X, 0f, player.Velocity.Z);
			float distance = player.GlobalPosition.DistanceTo(ApproachPoint.GlobalPosition);
			float approach = Mathf.Max(1.5f * distance / Mathf.Max(velocity.Length(), ApproachSpeed), MinApproachSeconds);
			_move.Walk(ApproachPoint, ApproachPitchDegrees, time, approach, velocity);
			_move.At(approach * 0.3f, () => player.Hands.Reach(ViewHands.Side.Right, OutsideHandle, approach * 0.7f, HandleCurl));
			time += approach;

			// Pull the door while stepping out of its way, then let it swing open by itself.
			_move.Walk(PullPoint, ApproachPitchDegrees, time, PullSeconds);
			_move.At(time, () =>
			{
				LatchSound.Play();
				CreakSound.Play();
			});
			SwingDoor(DoorPullAmount, time, PullSeconds);
			time += PullSeconds;
			_move.At(time, () => player.Hands.Withdraw(ViewHands.Side.Right));
			SwingDoor(1f, time, SwingOpenSeconds);
		}

		// Step in while the door is still swinging, turning around and reaching back for it.
		_move.Walk(HidePoint, InsidePitchDegrees, time, StepInSeconds);
		_move.At(time + StepInSeconds * 0.5f, () => BumpSound.Play());
		_move.At(time + StepInSeconds * 0.6f, () =>
		{
			InsideGrip.Basis = EdgePose;
			player.Hands.Reach(ViewHands.Side.Right, InsideGrip, StepInSeconds * 0.4f, HandleCurl);
		});
		time += StepInSeconds;
		_move.At(time, () =>
		{
			_occupant = player;
			player.StartHiding(this, ExitPoint.GlobalTransform);
		});
		_move.At(time, () => CreakSound.Play());
		SwingDoor(0f, time, CloseSeconds);
		time += CloseSeconds;
		_move.At(time, () => ShutSound.Play());
		_move.At(time, () => player.Hands.Withdraw(ViewHands.Side.Right));
		_move.FinishAt(time);
	}

	private void LetOut(Player player)
	{
		BeginMove(player);
		float reach = player.Hands.ReachSeconds;
		_move.At(0f, () =>
		{
			InsideGrip.Basis = PushPose;
			player.Hands.Reach(ViewHands.Side.Right, InsideGrip, reach);
		});

		// Push the door open and step out right behind it.
		_move.At(reach, () =>
		{
			LatchSound.Play();
			CreakSound.Play();
		});
		SwingDoor(1f, reach, PushOpenSeconds);
		_move.At(reach + PushOpenSeconds * 0.4f, () => player.Hands.Withdraw(ViewHands.Side.Right));
		float stepOut = reach + PushOpenSeconds * 0.3f;
		_move.At(stepOut, player.StopHiding);
		_move.Walk(ExitPoint, ExitPitchDegrees, stepOut, StepOutSeconds);
		_move.FinishAt(stepOut + StepOutSeconds);
		_move.At(stepOut + StepOutSeconds, () => player.Hit -= OnHit);
	}

	private void BeginMove(Player player)
	{
		_player = player;
		_doorTween?.Kill();
		_move = new ScriptedMove(this, player);
	}

	/// <summary>The player was hit while getting in or out, which cuts the move short.</summary>
	private void OnHit()
	{
		_move.Kill();
		_player.Hit -= OnHit;
		_doorOpenness = Door.Rotation.Y / -Mathf.DegToRad(DoorOpenAngleDegrees);
	}

	private void SwingDoor(float openness, float start, float seconds)
	{
		_move.Tween.TweenProperty(Door, "rotation:y", DoorAngle(openness), seconds)
			.SetDelay(start).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_move.At(start + seconds, () => _doorOpenness = openness);
	}

	/// <summary>Swings the door right away, outside any move of the player's.</summary>
	private Tween SwingDoorNow(float openness, float seconds)
	{
		_doorTween?.Kill();
		_doorOpenness = openness;
		_doorTween = CreateTween();
		_doorTween.TweenProperty(Door, "rotation:y", DoorAngle(openness), seconds)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		return _doorTween;
	}

	private float DoorAngle(float openness)
	{
		// A negative angle swings the door outwards, towards the locker's front (-Z).
		return -Mathf.DegToRad(DoorOpenAngleDegrees) * openness;
	}
}
