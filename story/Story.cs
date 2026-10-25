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

	[ExportGroup("Act 2")]
	[Export] public TapeRecording Trial13Tape { get; set; }
	[Export] public string TapeMemo { get; set; } = "";
	[Export] public MainBreaker MainBreaker { get; set; }
	/// <summary>The lights that burst when the breaker sends the power to the lift.</summary>
	[Export] public Light3D[] BasementLights { get; set; } = [];
	[Export] public AudioStream LightBurstSound { get; set; }
	[Export] public float SecondsBetweenBursts { get; set; } = 0.3f;
	[Export] public Remnant Remnant { get; set; }
	[Export] public string BreakerMemo { get; set; } = "";
	[Export] public ServiceLift Lift { get; set; }
	[Export] public Enemy Sentinel { get; set; }

	[ExportGroup("Act 3")]
	/// <summary>The first memo of the player's own, when they reach the floor no earlier trial reached.</summary>
	[Export] public string UpperFloorMemo { get; set; } = "";

	private bool _hasBeenUpstairs;

	public override void _Ready()
	{
		HideoutDoor.TriedWhileBarred += OnHideoutDoorTried;
		Trial13Tape.Finished += OnTapeWatched;
		MainBreaker.Thrown += OnBreakerThrown;
		Lift.DoorsOpened += OnLiftDoorsOpened;
		Lift.Arrived += OnLiftArrived;
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

	private void OnTapeWatched()
	{
		Trial13Tape.Finished -= OnTapeWatched;
		Player.ObjectiveDisplay.Announce(TapeMemo);
	}

	/// <summary>The lights burst one after another, the lift gets its power and the Remnant gets out.</summary>
	private void OnBreakerThrown()
	{
		Lift.HasPower = true;
		Tween tween = CreateTween();
		foreach (Light3D light in BasementLights)
		{
			tween.TweenCallback(Callable.From(() => BurstLight(light)));
			tween.TweenInterval(SecondsBetweenBursts);
		}
		tween.TweenCallback(Callable.From(Remnant.Release));
		tween.TweenInterval(2f);
		tween.TweenCallback(Callable.From(() => Player.ObjectiveDisplay.Announce(BreakerMemo)));
	}

	private void BurstLight(Light3D light)
	{
		light.Visible = false;
		light.ProcessMode = ProcessModeEnum.Disabled;
		foreach (Node child in light.GetChildren())
		{
			if (child is AudioStreamPlayer3D sound)
				sound.Stop();
		}

		var pop = new AudioStreamPlayer3D { Stream = LightBurstSound, Bus = "Sfx", UnitSize = 5f, MaxDistance = 40f };
		light.GetParent().AddChild(pop);
		pop.GlobalPosition = light.GlobalPosition;
		pop.Finished += pop.QueueFree;
		pop.Play();
	}

	private void OnLiftArrived(bool isUpstairs)
	{
		if (!isUpstairs || _hasBeenUpstairs)
			return;

		_hasBeenUpstairs = true;
		Player.ObjectiveDisplay.AnnounceOwn(UpperFloorMemo);
	}

	/// <summary>The lift's chime carries through the floor: the Sentinel comes to see who called it.</summary>
	private void OnLiftDoorsOpened(LiftLanding landing)
	{
		if (landing == Lift.GroundLanding)
			Sentinel.HearNoise(landing.GlobalPosition);
	}
}
