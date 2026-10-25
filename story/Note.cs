using Godot;

/// <summary>
/// A piece of paper the player can pick up and read. The first time it is read it can leave a memo behind.
/// After the player has been through the loop once, a note can say something else.
/// </summary>
public partial class Note : StaticBody3D, IInteractable
{
	[Export] public string Title { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";
	/// <summary>What the note says instead once the player has been reset at the gate. Empty keeps the text.</summary>
	[Export(PropertyHint.MultilineText)] public string TextAfterTheLoop { get; set; } = "";
	/// <summary>The memo shown after reading the note for the first time. Empty shows none.</summary>
	[Export] public string Memo { get; set; } = "";
	/// <summary>Whether the memo is the player's own, saved for the next trial, instead of one left by the last.</summary>
	[Export] public bool IsOwnMemo { get; set; }

	private bool _wasRead;

	public string InteractionPrompt => "Read";

	public bool WasRead => _wasRead;

	public void Interact(Player player)
	{
		string text = GameState.HasSeenTheLoop && TextAfterTheLoop != "" ? TextAfterTheLoop : Text;
		player.NoteReader.Open(Title, text, () => OnRead(player));
	}

	private void OnRead(Player player)
	{
		bool isFirstRead = !_wasRead;
		_wasRead = true;
		if (!isFirstRead || Memo == "")
			return;

		if (IsOwnMemo)
			player.ObjectiveDisplay.AnnounceOwn(Memo);
		else
			player.ObjectiveDisplay.Announce(Memo);
	}
}
