using System;
using Godot;

/// <summary>
/// The clinic's transmitter in the pump room under the fountain, whose signal does the resetting. Tearing out
/// its cables kills it for good.
/// </summary>
public partial class Transmitter : StaticBody3D, IInteractable
{
	[Export] public AudioStreamPlayer3D Hum { get; set; }
	[Export] public AudioStreamPlayer3D DestroySound { get; set; }
	/// <summary>The transmitter's own lights, which die with it.</summary>
	[Export] public Node3D[] Lights { get; set; } = [];
	/// <summary>A flash of sparks as the cables come out.</summary>
	[Export] public OmniLight3D Sparks { get; set; }

	public bool IsDestroyed { get; private set; }

	public string InteractionPrompt => "Tear out the cables";

	public event Action Destroyed;

	public void Interact(Player player)
	{
		IsDestroyed = true;
		CollisionLayer = 0;
		Hum.Stop();
		DestroySound.Play();
		foreach (Node3D light in Lights)
			light.Visible = false;

		Tween sparks = CreateTween();
		sparks.TweenProperty(Sparks, "light_energy", 0f, 1.2f).From(6f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		Destroyed?.Invoke();
	}
}
