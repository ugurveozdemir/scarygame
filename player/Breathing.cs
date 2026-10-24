using Godot;

/// <summary>
/// The player's breathing. It grows heavier the longer they sprint and calms down slowly afterwards,
/// and fear makes it ragged too: it is as loud as the stronger of the two. Hiding, the player tries to hold it in,
/// and only shaky breaths through the nose escape.
/// </summary>
public partial class Breathing : Node
{
	[Export] public Player Player { get; set; }
	[Export] public AudioStreamPlayer Breath { get; set; }
	[Export] public AudioStreamPlayer HeldBreath { get; set; }
	[Export] public float HoldBlendSeconds { get; set; } = 0.6f;
	[Export] public float SecondsToExhaust { get; set; } = 5f;
	[Export] public float SecondsToRecover { get; set; } = 8f;

	/// <summary>How scared the player is, from 0 to 1, set by <see cref="Dread"/>.</summary>
	public float Fear { get; set; }

	private float _exertion;
	private float _holding;
	private float _volume;
	private float _heldVolume;

	public override void _Ready()
	{
		_volume = Breath.VolumeLinear;
		_heldVolume = HeldBreath.VolumeLinear;
	}

	public override void _Process(double delta)
	{
		bool isSprinting = Player.IsSprinting && !Player.IsGrabbed;
		float seconds = isSprinting ? SecondsToExhaust : SecondsToRecover;
		_exertion = Mathf.MoveToward(_exertion, isSprinting ? 1f : 0f, (float)delta / seconds);
		_holding = Mathf.MoveToward(_holding, Player.IsHiding ? 1f : 0f, (float)delta / HoldBlendSeconds);

		float strength = Mathf.Max(_exertion, Fear);
		Breath.VolumeLinear = _volume * strength * (1f - _holding);
		HeldBreath.VolumeLinear = _heldVolume * strength * _holding;
	}
}
