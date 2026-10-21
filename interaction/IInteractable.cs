	/// <summary>
/// Something the player can use by looking at it and pressing the interact key.
/// </summary>
public interface IInteractable
{
	string InteractionPrompt { get; }

	void Interact(Player player);
}
