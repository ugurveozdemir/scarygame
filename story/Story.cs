using Godot;

/// <summary>
/// Runs the story of a trial: waking up in the parking lot and the memos and events that move it along. The
/// pieces of the level do their own work; this only ties together what happens across them.
/// </summary>
public partial class Story : Node
{
	[ExportGroup("Prologue")]
	[Export] public Player Player { get; set; }
	[Export] public TitleCard TitleCard { get; set; }
	[Export] public float TitleSeconds { get; set; } = 3f;
	[Export] public float OpenEyesSeconds { get; set; } = 3f;
	/// <summary>How long the player lies looking at the sky before getting up.</summary>
	[Export] public float LieSeconds { get; set; } = 2.5f;
	[Export] public float GetUpSeconds { get; set; } = 1.6f;
	[Export] public float LyingPitchDegrees { get; set; } = 65f;
	[Export] public string FirstMemo { get; set; } = "";
	[Export] public string FirstMemoAfterTheLoop { get; set; } = "";

	[ExportGroup("Act 1")]
	/// <summary>The hideout's door, barred from the inside by the previous trial.</summary>
	[Export] public Door HideoutDoor { get; set; }
	[Export] public string BarredDoorMemo { get; set; } = "";

	public override void _Ready()
	{
		HideoutDoor.TriedWhileBarred += OnHideoutDoorTried;
		PlayPrologue();
	}

	private void PlayPrologue()
	{
		TitleCard.Cover($"VESPERA\nSLEEP & NEURO-REHABILITATION CLINIC\n\nSUBJECT 14 · TRIAL {GameState.Trial}");
		Player.LockControls();
		Player.LieDown();
		Player.Head.Rotation = new Vector3(Mathf.DegToRad(LyingPitchDegrees), 0f, 0f);

		// Eyes open on the night sky, then the player sits up and gets to their feet in one motion.
		Tween tween = CreateTween();
		tween.TweenInterval(TitleSeconds);
		tween.TweenCallback(Callable.From(() => TitleCard.Reveal(OpenEyesSeconds)));
		tween.TweenInterval(OpenEyesSeconds + LieSeconds);
		tween.TweenCallback(Callable.From(Player.GetUp));
		tween.TweenProperty(Player.Head, "rotation", Vector3.Zero, GetUpSeconds)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenCallback(Callable.From(() =>
		{
			Player.UnlockControls();
			Player.ObjectiveDisplay.Announce(GameState.HasSeenTheLoop ? FirstMemoAfterTheLoop : FirstMemo);
		}));
	}

	private void OnHideoutDoorTried()
	{
		HideoutDoor.TriedWhileBarred -= OnHideoutDoorTried;
		Player.ObjectiveDisplay.Announce(BarredDoorMemo);
	}
}
