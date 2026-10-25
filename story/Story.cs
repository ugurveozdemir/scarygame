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
	[Export] public LockConsole LockConsole { get; set; }
	[Export] public Note CaseFile { get; set; }

	[ExportGroup("Act 4")]
	[Export] public IronGate Gate { get; set; }
	[Export] public Door FireDoor { get; set; }
	[Export] public AudioStreamPlayer Siren { get; set; }
	[Export] public float SirenFadeSeconds { get; set; } = 3f;
	/// <summary>The lamps on the grounds, which turn red when the alarm sounds.</summary>
	[Export] public Light3D[] GroundLamps { get; set; } = [];
	[Export] public Color AlarmColor { get; set; } = new(1f, 0.12f, 0.08f);
	[Export] public AlarmLight[] AlarmLights { get; set; } = [];
	[Export] public WorldEnvironment World { get; set; }
	/// <summary>How much thicker the fog gets once the alarm sounds.</summary>
	[Export] public float AlarmFogMultiplier { get; set; } = 1.8f;
	[Export] public float FogChangeSeconds { get; set; } = 20f;
	/// <summary>Where the Sentinel walks its rounds once it is sent out onto the grounds.</summary>
	[Export] public Node3D GroundsPatrolRoute { get; set; }
	/// <summary>Where the Sentinel comes out of the building, out of the player's sight.</summary>
	[Export] public Node3D SentinelExit { get; set; }
	[Export] public string LocksMemo { get; set; } = "";
	/// <summary>The memo instead of <see cref="LocksMemo"/> once the player has read their case file.</summary>
	[Export] public string LocksMemoKnowingTheTruth { get; set; } = "";

	[ExportGroup("The Loop")]
	[Export] public Area3D GateResetZone { get; set; }
	[Export] public SpotLight3D Floodlight { get; set; }
	[Export] public float FloodlightEnergy { get; set; } = 40f;
	[Export] public AudioStreamPlayer ResetWhine { get; set; }
	[Export] public ColorRect WhiteFlash { get; set; }

	[ExportGroup("Signal Lost")]
	[Export] public Transmitter Transmitter { get; set; }
	[Export] public string TransmitterMemo { get; set; } = "";
	/// <summary>The ladder up out of the drainage tunnel, outside the fence.</summary>
	[Export] public Ladder WayOut { get; set; }

	private bool _hasBeenUpstairs;
	private bool _isEnding;

	public override void _Ready()
	{
		HideoutDoor.TriedWhileBarred += OnHideoutDoorTried;
		Trial13Tape.Finished += OnTapeWatched;
		MainBreaker.Thrown += OnBreakerThrown;
		Lift.DoorsOpened += OnLiftDoorsOpened;
		Lift.Arrived += OnLiftArrived;
		LockConsole.Released += OnLocksReleased;
		GateResetZone.BodyEntered += OnGateCrossed;
		Transmitter.Destroyed += OnTransmitterDestroyed;
		WayOut.Climbed += PlaySignalLostEnding;
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

	/// <summary>
	/// Every magnetic lock opens and the reset siren starts: the lamps outside turn red, the fog rolls in, the lift
	/// loses its power and the Sentinel is sent out onto the grounds.
	/// </summary>
	private void OnLocksReleased()
	{
		Gate.Open();
		FireDoor.Unbar();
		HideoutDoor.Unbar();
		Lift.HasPower = false;

		Siren.Play();
		CreateTween().TweenProperty(Siren, "volume_linear", Siren.VolumeLinear, SirenFadeSeconds).From(0f);
		foreach (Light3D lamp in GroundLamps)
			CreateTween().TweenProperty(lamp, "light_color", AlarmColor, SirenFadeSeconds);
		foreach (AlarmLight light in AlarmLights)
			light.Sound();
		ThickenFog();

		if (!Sentinel.IsHunting)
			Sentinel.GlobalTransform = SentinelExit.GlobalTransform;
		Sentinel.ChangePatrolRoute(GroundsPatrolRoute);

		Player.ObjectiveDisplay.AnnounceOwn(CaseFile.WasRead ? LocksMemoKnowingTheTruth : LocksMemo);
	}

	private void ThickenFog()
	{
		// The environment is a shared resource, so this level gets its own copy before changing it.
		var environment = (Environment)World.Environment.Duplicate();
		World.Environment = environment;
		Tween tween = CreateTween().SetParallel();
		tween.TweenProperty(environment, "fog_density", environment.FogDensity * AlarmFogMultiplier, FogChangeSeconds);
		tween.TweenProperty(environment, "volumetric_fog_density", environment.VolumetricFogDensity * AlarmFogMultiplier,
			FogChangeSeconds);
	}

	/// <summary>
	/// Through the open gate the floodlight flares, the whine rises and the trial ends the way every trial ends:
	/// the player wakes in the parking lot again, one trial later.
	/// </summary>
	private void OnGateCrossed(Node3D body)
	{
		if (body != Player || !Gate.IsOpen || _isEnding)
			return;

		_isEnding = true;
		Player.LockControls();
		Player.CameraRig.AddShake(0.6f);
		ResetWhine.Play();
		Siren.Stop();

		Tween tween = CreateTween().SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
		tween.TweenProperty(Floodlight, "light_energy", FloodlightEnergy, 3.5f).From(0f);
		tween.Parallel().TweenProperty(WhiteFlash, "color:a", 1f, 4.5f).From(0f);
		tween.TweenCallback(Callable.From(() =>
		{
			TitleCard.Cover($"TRIAL {GameState.Trial} · COMPLETE\nSUBJECT COMPLIANT\n\nRESETTING");
			WhiteFlash.Color = WhiteFlash.Color with { A = 0f };
		}));
		tween.TweenInterval(4f);
		tween.TweenCallback(Callable.From(() => TitleCard.Lines.Text = "THE LOOP\n\nENDING 1 OF 2"));
		tween.TweenInterval(3.5f);
		tween.TweenCallback(Callable.From(() =>
		{
			GameState.StartNextTrial(reachedTheGate: true);
			GetTree().ReloadCurrentScene();
		}));
	}

	/// <summary>The transmitter dies: the siren stops and the camcorder's picture breaks up one last time.</summary>
	private void OnTransmitterDestroyed()
	{
		Siren.Stop();
		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(amount => Player.Camcorder.Interference = amount), 1f, 0f, 2.5f);
		tween.TweenCallback(Callable.From(() => Player.ObjectiveDisplay.AnnounceOwn(TransmitterMemo)));
	}

	/// <summary>Out of the drain and outside the fence, with nothing left to reset the player.</summary>
	private void PlaySignalLostEnding()
	{
		if (_isEnding)
			return;

		_isEnding = true;
		Player.LockControls();
		Tween tween = TitleCard.FadeIn("SIGNAL LOST", 2.5f);
		tween.TweenInterval(3f);
		tween.TweenCallback(Callable.From(() =>
			TitleCard.Lines.Text = "SIGNAL LOST\n\nThe memos stop.\nFor the first time in three years, you remember yesterday."));
		tween.TweenInterval(6f);
		tween.TweenCallback(Callable.From(() => TitleCard.Lines.Text = "SIGNAL LOST\n\nENDING 2 OF 2"));
		tween.TweenInterval(4f);
		tween.TweenCallback(Callable.From(() =>
		{
			GameState.StartOver();
			GetTree().ReloadCurrentScene();
		}));
	}

	/// <summary>The lift's chime carries through the floor: the Sentinel comes to see who called it.</summary>
	private void OnLiftDoorsOpened(LiftLanding landing)
	{
		if (landing == Lift.GroundLanding)
			Sentinel.HearNoise(landing.GlobalPosition);
	}
}
