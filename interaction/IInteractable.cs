/// <summary>
/// Something the player can use by looking at it and pressing the interact key.
/// </summary>
public interface IInteractable
{
	string InteractionPrompt { get; }

	/// <summary>Whether the player can use it right now, e.g. whether it is within arm's reach.</summary>
	bool CanInteract(Player player) => true;

	void Interact(Player player);
}
