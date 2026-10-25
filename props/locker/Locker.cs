using Godot;

/// <summary>
/// A locker the player can hide in, looking out through the slits in its door.
/// Getting in is an animation made in Blender (tools/locker_animations.py): the player pulls the door open
/// by its handle, steps in turning around, hooks their fingers into a vent slit and pulls the door shut while
/// backing in. The player first walks up to where the animation starts, from wherever they are.
/// Getting out pushes the door open with the hand. An enemy checking the locker tears the door open.
/// </summary>
public partial class Locker : StaticBody3D, IHidingSpot
{
	private const float ExitPitchDegrees = 0f;

	// When things happen in the hiding animation, in seconds (Blender frame / 30).
	private const float LatchSeconds = 19f / 30f;
	/// <summary>The door is fully open and the player starts stepping in; an open door skips to here.</summary>
	private const float StepInSeconds = 48f / 30f;
	private const float BumpSeconds = 52f / 30f;
	private const float PullShutSeconds = 60f / 30f;
	private const float ShutSeconds = 76f / 30f;

	// The door's front is towards -Z: the player pushes it open flat-handed from inside.
	private static readonly Basis PushPose = ViewHands.Pose(Vector3.Up, Vector3.Forward);

	[ExportGroup("Door")]
	/// <summary>How far the door swings open; the hiding animation opens it this far too.</summary>
	[Export] public float DoorOpenAngleDegrees { get; set; } = 80f;
	[Export] public float CloseSeconds { get; set; } = 0.55f;
	[Export] public float PushOpenSeconds { get; set; } = 0.5f;
	[Export] public float TearOpenSeconds { get; set; } = 0.15f;

	[ExportGroup("Walking")]
	/// <summary>Walking pace used to time the walk up to the locker when the player is standing still.</summary>
	[Export] public float ApproachSpeed { get; set; } = 1.6f;
	[Export] public float MinApproachSeconds { get; set; } = 0.3f;
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
	[Export] public PlayerAnimation HideAnimation { get; set; }
	[Export] public Node3D InsideGrip { get; set; }
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

		// Walk up to where the animation starts, carrying on from however the player was moving.
		float from = _doorOpenness == 0f ? 0f : StepInSeconds;
		Transform3D start = HideAnimation.PlayerTransformAt(player, from);
		Vector3 velocity = new(player.Velocity.X, 0f, player.Velocity.Z);
		float distance = player.GlobalPosition.DistanceTo(start.Origin);
		float approach = Mathf.Max(1.5f * distance / Mathf.Max(velocity.Length(), ApproachSpeed), MinApproachSeconds);
		_move.Walk(start, HideAnimation.PitchDegreesAt(from), 0f, approach, velocity);
		_move.At(approach, () => HideAnimation.Play(player, from));

		// The rest follows the animation, timed from its start.
		float zero = approach - HideAnimation.RealSeconds(from);
		if (from < LatchSeconds)
		{
			_move.At(zero + HideAnimation.RealSeconds(LatchSeconds), () =>
			{
				LatchSound.Play();
				CreakSound.Play();
			});
		}
		_move.At(zero + HideAnimation.RealSeconds(BumpSeconds), () => BumpSound.Play());
		_move.At(zero + HideAnimation.RealSeconds(PullShutSeconds), () => CreakSound.Play());
		_move.At(zero + HideAnimation.RealSeconds(ShutSeconds), () =>
		{
			ShutSound.Play();
			_doorOpenness = 0f;
			_occupant = player;
			player.StartHiding(this, ExitPoint.GlobalTransform);
		});
		_move.FinishAt(zero + HideAnimation.RealSeconds(HideAnimation.Length));
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
		HideAnimation.Stop();
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
