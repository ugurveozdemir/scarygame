using Godot;

/// <summary>
/// A locker the player can hide in, looking out through the slits in its door.
/// The player opens and closes the door by hand on the way in and out.
/// </summary>
public partial class Locker : StaticBody3D, IInteractable
{
	private const float ApproachPitchDegrees = -15f;
	private const float InsidePitchDegrees = -20f;
	private const float ExitPitchDegrees = 0f;

	[Export] public float DoorOpenAngleDegrees { get; set; } = 65f;
	/// <summary>Seconds a full swing of the door takes.</summary>
	[Export] public float DoorSwingSeconds { get; set; } = 0.6f;
	/// <summary>How far the hand pulls the door open before letting it swing the rest of the way.</summary>
	[Export(PropertyHint.Range, "0,1")] public float DoorPullAmount { get; set; } = 0.4f;
	[Export] public float StepSeconds { get; set; } = 0.35f;
	[Export] public float StepInSeconds { get; set; } = 0.5f;

	[ExportGroup("Nodes")]
	[Export] public Node3D Door { get; set; }
	[Export] public Node3D OutsideHandle { get; set; }
	[Export] public Node3D InsideGrip { get; set; }
	[Export] public Marker3D ApproachPoint { get; set; }
	[Export] public Marker3D HidePoint { get; set; }
	[Export] public Marker3D ExitPoint { get; set; }

	private Player _occupant;
	private float _doorOpenness;

	private bool IsOccupied => _occupant is not null && _occupant.HidingSpot == this;

	public string InteractionPrompt => IsOccupied ? "Leave" : "Hide";

	public void Interact(Player player)
	{
		if (IsOccupied)
			LetOut(player);
		else
			TakeIn(player);
	}

	// Each step checks whether the player got grabbed meanwhile; the attacker then takes over.
	private async void TakeIn(Player player)
	{
		player.LockControls();
		if (_doorOpenness == 0f)
		{
			await player.MoveTo(ApproachPoint.GlobalTransform, ApproachPitchDegrees, StepSeconds);
			if (player.IsGrabbed) return;
			await player.Hands.Reach(ViewHands.Side.Right, OutsideHandle);
			if (player.IsGrabbed) return;
			await SwingDoor(DoorPullAmount);
			_ = player.Hands.Withdraw(ViewHands.Side.Right);
			await SwingDoor(1f);
			if (player.IsGrabbed) return;
		}

		await player.MoveTo(HidePoint.GlobalTransform, InsidePitchDegrees, StepInSeconds);
		if (player.IsGrabbed) return;
		_occupant = player;
		player.StartHiding(this, ExitPoint.GlobalTransform);

		await player.Hands.Reach(ViewHands.Side.Right, InsideGrip);
		if (player.IsGrabbed) return;
		await SwingDoor(0f);
		_ = player.Hands.Withdraw(ViewHands.Side.Right);
		player.UnlockControls();
	}

	private async void LetOut(Player player)
	{
		player.LockControls();
		await player.Hands.Reach(ViewHands.Side.Right, InsideGrip);
		if (player.IsGrabbed) return;
		await SwingDoor(1f);
		_ = player.Hands.Withdraw(ViewHands.Side.Right);
		if (player.IsGrabbed) return;

		player.StopHiding();
		await player.MoveTo(ExitPoint.GlobalTransform, ExitPitchDegrees, StepInSeconds);
		if (player.IsGrabbed) return;
		player.UnlockControls();
	}

	private SignalAwaiter SwingDoor(float openness)
	{
		float seconds = DoorSwingSeconds * Mathf.Abs(openness - _doorOpenness);
		_doorOpenness = openness;
		// A negative angle swings the door outwards, towards the locker's front (-Z).
		float angle = -Mathf.DegToRad(DoorOpenAngleDegrees) * openness;
		Tween tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(Door, "rotation:y", angle, seconds);
		return ToSignal(tween, Tween.SignalName.Finished);
	}
}
