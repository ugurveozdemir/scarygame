using Godot;

/// <summary>
/// Everything that shapes how the first-person view and movement feel, kept in one resource so it can be tuned
/// while playing (F1). The defaults follow Unreal Engine's first-person template, slowed down to a horror game's pace.
/// </summary>
[GlobalClass]
public partial class FirstPersonSettings : Resource
{
	[ExportGroup("View")]
	/// <summary>Horizontal field of view in degrees, measured the way Unreal Engine measures it.</summary>
	[Export(PropertyHint.Range, "60,120")] public float FieldOfView { get; set; } = 90f;
	/// <summary>Degrees the view turns for each pixel the mouse moves.</summary>
	[Export(PropertyHint.Range, "0.01,0.5")] public float MouseSensitivity { get; set; } = 0.1f;
	[Export(PropertyHint.Range, "45,89")] public float MaxPitchDegrees { get; set; } = 85f;
	[Export] public float EyeHeight { get; set; } = 1.65f;
	[Export] public float CrouchEyeHeight { get; set; } = 0.9f;
	/// <summary>How quickly the eyes ease between standing and crouching height.</summary>
	[Export] public float CrouchTransitionSpeed { get; set; } = 8f;

	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 2.5f;
	[Export] public float SprintSpeed { get; set; } = 5f;
	[Export] public float CrouchSpeed { get; set; } = 1.3f;
	/// <summary>How quickly the player speeds up, in metres per second squared.</summary>
	[Export] public float Acceleration { get; set; } = 15f;
	/// <summary>How quickly the player stops once no movement key is held, in metres per second squared.</summary>
	[Export] public float BrakingDeceleration { get; set; } = 18f;
	/// <summary>The share of the ground acceleration and braking the player still has in the air.</summary>
	[Export(PropertyHint.Range, "0,1")] public float AirControl { get; set; } = 0.2f;

	[ExportGroup("Head Bob")]
	/// <summary>Distance covered with each footstep.</summary>
	[Export] public float StrideLength { get; set; } = 1.4f;
	/// <summary>How far the eyes dip as a foot lands at sprint speed; slower steps dip them less.</summary>
	[Export] public float BobHeight { get; set; } = 0.03f;
	/// <summary>Degrees the field of view widens at sprint speed.</summary>
	[Export] public float SprintFovBoost { get; set; } = 0f;
}
