using Godot;

/// <summary>
/// The nurse station's intercom. Paging a far room chimes from the loudspeaker there, and the Sentinel goes to
/// see what made the sound, which draws it away from wherever the player wants to go.
/// </summary>
public partial class Intercom : StaticBody3D, IInteractable
{
	[Export] public Enemy Enemy { get; set; }
	/// <summary>The loudspeaker in the paged room.</summary>
	[Export] public AudioStreamPlayer3D Loudspeaker { get; set; }
	/// <summary>The handset's click and hiss at the nurse station.</summary>
	[Export] public AudioStreamPlayer3D HandsetSound { get; set; }
	[Export] public string PagedRoom { get; set; } = "the south rooms";
	/// <summary>How long the intercom needs before it can page again.</summary>
	[Export] public float CooldownSeconds { get; set; } = 25f;

	private float _cooldownLeft;

	public string InteractionPrompt => _cooldownLeft > 0f ? "Intercom (busy)" : $"Page {PagedRoom}";

	public override void _Process(double delta)
	{
		_cooldownLeft = Mathf.Max(_cooldownLeft - (float)delta, 0f);
	}

	public void Interact(Player player)
	{
		if (_cooldownLeft > 0f)
			return;

		_cooldownLeft = CooldownSeconds;
		HandsetSound.Play();
		Loudspeaker.Play();
		Enemy.HearNoise(Loudspeaker.GlobalPosition);
	}
}
