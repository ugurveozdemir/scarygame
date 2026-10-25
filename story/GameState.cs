/// <summary>
/// What survives a reset of the level: which trial of Subject 14 this is. Every time the player is caught or
/// reaches the gate the clinic wipes their memory, and the next trial starts in the parking lot.
/// </summary>
public static class GameState
{
	public const int FirstTrial = 14;

	public static int Trial { get; private set; } = FirstTrial;

	/// <summary>True once the player has been through a whole trial and been reset at the gate.</summary>
	public static bool HasSeenTheLoop { get; private set; }

	public static void StartNextTrial(bool reachedTheGate)
	{
		Trial++;
		HasSeenTheLoop |= reachedTheGate;
	}

	public static void StartOver()
	{
		Trial = FirstTrial;
		HasSeenTheLoop = false;
	}
}
