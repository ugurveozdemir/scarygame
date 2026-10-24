using Godot;

/// <summary>
/// Finds the interactable the player is looking at, shows its prompt and uses it on key press.
/// </summary>
public partial class Interactor : RayCast3D
{
	[Export] public Player Player { get; set; }
	[Export] public Label PromptLabel { get; set; }

	private IInteractable _target;

	public override void _PhysicsProcess(double delta)
	{
		_target = FindTarget();
		PromptLabel.Text = _target is null ? "" : $"[E] {_target.InteractionPrompt}";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_target is null || !@event.IsActionPressed("interact"))
			return;

		_target.Interact(Player);
		GetViewport().SetInputAsHandled();
	}

	private IInteractable FindTarget()
	{
		if (Player.IsGrabbed || Player.AreControlsLocked)
			return null;

		// While hiding, the only thing the player can use is their hiding spot.
		if (Player.IsHiding)
			return Player.HidingSpot;

		return GetCollider() as IInteractable;
	}
}
