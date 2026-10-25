using System;
using Godot;

/// <summary>
/// Trial 13's last tape, played back on a monitor in the archive. It is filmed live through a camera in the
/// level as it is now; what was different that night, the code taped next to the lift and the surgeon in the
/// doorway, is only on the past layer. The camera walks a path of markers like the hand that held it, films the
/// code, cuts to later that night and ends on the floor.
/// </summary>
public partial class TapeRecording : Node3D
{
	private const string SprintAnimation = "ual1/Sprint";
	private const float SprintAnimationSpeed = 4.6f;

	[Export] public string TapeName { get; set; } = "TRIAL 13";
	/// <summary>The timecode the tape starts at, in seconds.</summary>
	[Export] public float StartTimecodeSeconds { get; set; } = 9667f;
	/// <summary>How much later that night the second part of the tape was recorded, in seconds.</summary>
	[Export] public float CutTimecodeSeconds { get; set; } = 3725f;
	[Export] public float ZoomFov { get; set; } = 32f;
	[Export] public float StepSeconds { get; set; } = 0.55f;

	[ExportGroup("Picture")]
	[Export] public SubViewport Picture { get; set; }
	/// <summary>Carries the camera along the path; the camera itself only shakes.</summary>
	[Export] public Node3D Operator { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public Label Overlay { get; set; }
	[Export] public GeometryInstance3D Screen { get; set; }
	/// <summary>The markers the camera walks through: Corridor, LobbyDoor, Lobby, Keypad, Waiting, Turn and Fallen.</summary>
	[Export] public Node3D Path { get; set; }

	[ExportGroup("Surgeon")]
	[Export] public Node3D Surgeon { get; set; }
	[Export] public AnimationPlayer SurgeonAnimations { get; set; }
	[Export] public AnimationLibrary Ual1Animations { get; set; }
	[Export] public Node3D SurgeonStart { get; set; }
	[Export] public Node3D SurgeonEnd { get; set; }

	[ExportGroup("Sound")]
	[Export] public AudioStreamPlayer3D Steps { get; set; }
	[Export] public AudioStreamPlayer3D Breath { get; set; }
	[Export] public AudioStreamPlayer3D Roar { get; set; }
	[Export] public AudioStreamPlayer3D Hit { get; set; }
	[Export] public AudioStreamPlayer3D Snow { get; set; }

	public bool IsPlaying { get; private set; }

	public event Action Finished;

	private ShaderMaterial _screenMaterial;
	private float _timecode;
	private float _shake;
	private float _stepTimeLeft;
	private float _defaultFov;
	private Vector3 _lastOperatorPosition;

	public override void _Ready()
	{
		_screenMaterial = (ShaderMaterial)Screen.MaterialOverride;
		_screenMaterial.SetShaderParameter("picture", Picture.GetTexture());
		_defaultFov = Camera.Fov;
		SurgeonAnimations.AddAnimationLibrary("ual1", Ual1Animations);
		VisualLayers.Assign(Surgeon, VisualLayers.Past);
		foreach (Node node in Surgeon.FindChildren("*", nameof(MeshInstance3D), owned: false))
			((MeshInstance3D)node).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		Surgeon.Visible = false;
		Picture.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
		Overlay.Visible = false;
	}

	public void Play()
	{
		IsPlaying = true;
		_timecode = StartTimecodeSeconds;
		_shake = 0f;
		Camera.Fov = _defaultFov;
		Camera.Rotation = Vector3.Zero;
		Operator.GlobalTransform = Marker("Corridor");
		_lastOperatorPosition = Operator.GlobalPosition;
		Picture.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		Overlay.Visible = true;
		SetSnow(0f);
		Breath.Play();

		Tween tween = CreateTween();
		// Down the corridor and into the lift lobby, up to the code taped beside the keypad.
		WalkTo(tween, "LobbyDoor", 3f);
		WalkTo(tween, "Lobby", 2.5f);
		WalkTo(tween, "Keypad", 2f);
		tween.TweenProperty(Camera, "fov", ZoomFov, 1.2f).SetTrans(Tween.TransitionType.Sine);
		tween.TweenInterval(3.5f);

		// Later that night, back at the lift. Running footsteps; the camera whips round to the doorway.
		tween.TweenCallback(Callable.From(() => CutTo("Waiting")));
		tween.TweenInterval(2.5f);
		tween.TweenCallback(Callable.From(RunAtTheCamera));
		WalkTo(tween, "Turn", 0.5f);
		tween.TweenInterval(1.1f);
		tween.TweenCallback(Callable.From(() =>
		{
			Hit.Play();
			_shake = 1f;
		}));
		WalkTo(tween, "Fallen", 0.45f, Tween.TransitionType.Quad, Tween.EaseType.In);
		tween.TweenInterval(1.5f);
		tween.TweenCallback(Callable.From(EndOfTape));
	}

	public override void _Process(double delta)
	{
		if (!IsPlaying)
			return;

		float dt = (float)delta;
		_timecode += dt;
		Overlay.Text = $"PLAY ▶  SUBJECT 14 · {TapeName}\n{TimeSpan.FromSeconds(_timecode):hh\\:mm\\:ss}";

		// A hand-held camera never stops moving, and shakes hard when the person holding it is hit.
		_shake = Mathf.MoveToward(_shake, 0f, dt * 0.8f);
		float time = Time.GetTicksMsec() / 1000f;
		float wobble = 0.012f + _shake * 0.25f;
		Camera.Rotation = new Vector3(
			Mathf.Sin(time * 1.3f) * wobble + Mathf.Sin(time * 17f) * _shake * 0.1f,
			Mathf.Sin(time * 0.9f + 1f) * wobble + Mathf.Sin(time * 23f) * _shake * 0.1f,
			Mathf.Sin(time * 0.7f + 2f) * wobble * 0.5f);

		PlayFootsteps(dt);
	}

	private void WalkTo(Tween tween, string marker, float seconds,
		Tween.TransitionType transition = Tween.TransitionType.Sine, Tween.EaseType ease = Tween.EaseType.InOut)
	{
		tween.TweenProperty(Operator, "global_transform", Marker(marker), seconds).SetTrans(transition).SetEase(ease);
	}

	private void CutTo(string marker)
	{
		Operator.GlobalTransform = Marker(marker);
		_lastOperatorPosition = Operator.GlobalPosition;
		Camera.Fov = _defaultFov;
		_timecode += CutTimecodeSeconds;
		SetSnow(1f);
		Snow.Play();
		Tween tween = CreateTween();
		tween.TweenInterval(0.5f);
		tween.TweenCallback(Callable.From(() =>
		{
			SetSnow(0f);
			Snow.Stop();
		}));
	}

	private void RunAtTheCamera()
	{
		Surgeon.GlobalTransform = SurgeonStart.GlobalTransform;
		Surgeon.Visible = true;
		Roar.Play();

		float distance = SurgeonStart.GlobalPosition.DistanceTo(SurgeonEnd.GlobalPosition);
		const float speed = 5f;
		SurgeonAnimations.SpeedScale = speed / SprintAnimationSpeed;
		SurgeonAnimations.Play(SprintAnimation);
		Tween tween = CreateTween();
		tween.TweenProperty(Surgeon, "global_position", SurgeonEnd.GlobalPosition, distance / speed);
	}

	private void EndOfTape()
	{
		SetSnow(1f);
		Snow.Play();
		Breath.Stop();
		Surgeon.Visible = false;
		SurgeonAnimations.Stop();
		Overlay.Visible = false;

		Tween tween = CreateTween();
		tween.TweenInterval(2f);
		tween.TweenCallback(Callable.From(() =>
		{
			Snow.Stop();
			Picture.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			IsPlaying = false;
			Finished?.Invoke();
		}));
	}

	private void PlayFootsteps(float delta)
	{
		Vector3 position = Operator.GlobalPosition;
		float speed = position.DistanceTo(_lastOperatorPosition) / delta;
		_lastOperatorPosition = position;
		_stepTimeLeft -= delta;
		if (speed < 0.4f || _stepTimeLeft > 0f)
			return;

		Steps.Play();
		_stepTimeLeft = StepSeconds;
	}

	private void SetSnow(float amount)
	{
		_screenMaterial.SetShaderParameter("snow", amount);
	}

	private Transform3D Marker(string name)
	{
		return Path.GetNode<Node3D>(name).GlobalTransform;
	}
}
