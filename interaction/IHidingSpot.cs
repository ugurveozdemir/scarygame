using Godot;

/// <summary>
/// A place the player can hide in. An enemy hunting for the player checks hiding spots near where it lost
/// them: it stands at the spot's search point, looks inside and drags out whoever is hiding there.
/// Hiding spots join the <see cref="Group"/> group so enemies can find them.
/// </summary>
public interface IHidingSpot : IInteractable
{
	const string Group = "hiding_spots";

	/// <summary>Where an enemy stands to check the spot, facing it.</summary>
	Node3D SearchPoint { get; }
	/// <summary>What an enemy looks at while checking the spot.</summary>
	Node3D LookPoint { get; }
	bool IsOccupied { get; }

	/// <summary>Opens the spot up to look inside, like a locker's door torn open.</summary>
	void Reveal();
	/// <summary>Closes the spot again after it turned out empty.</summary>
	void Conceal();
	/// <summary>Drags the hiding player out and leaves them standing in front of the spot.</summary>
	void DragOutOccupant();
}
