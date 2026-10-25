using System;
using Godot;

/// <summary>
/// The basement's main breaker. Throwing its lever sends the power to the service lift, which the basement's
/// old wiring can't take: every light down there bursts.
/// </summary>
public partial class MainBreaker : StaticBody3D, IInteractable
{
	[Export] public Node3D Lever { get; set; }
	[Export] public float ThrownAngleDegrees { get; set; } = -100f;
	[Export] public float ThrowSeconds { get; set; } = 0.35f;
	[Export] public AudioStreamPlayer3D ThrowSound { get; set; }

	public bool IsThrown { get; private set; }

	public string InteractionPrompt => "Throw the main breaker";

	public event Action Thrown;

	public void Interact(Player player)
	{
		IsThrown = true;
		CollisionLayer = 0;
		ThrowSound.Play();

		Tween tween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(Lever, "rotation_degrees:x", ThrownAngleDegrees, ThrowSeconds);
		tween.TweenCallback(Callable.From(() => Thrown?.Invoke()));
	}
}
