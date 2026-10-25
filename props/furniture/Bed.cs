using Godot;

/// <summary>
/// A hospital bed the player can crawl under to hide, watching the room from floor level.
/// Getting under is one continuous motion: the player walks up beside the bed, gets down on the floor and
/// crawls in sideways, turning to face out. Crawling out is the same in reverse. The open side is the
/// bed's -X side; an enemy checking the bed kneels there to look underneath.
/// </summary>
public partial class Bed : StaticBody3D, IHidingSpot
{
	private const float ApproachPitchDegrees = -25f;

	[ExportGroup("Moving")]
	/// <summary>Walking pace used to time the walk up to the bed when the player is standing still.</summary>
	[Export] public float ApproachSpeed { get; set; } = 1.6f;
	[Export] public float MinApproachSeconds { get; set; } = 0.3f;
	[Export] public float GetDownSeconds { get; set; } = 0.5f;
	[Export] public float CrawlInSeconds { get; set; } = 1.1f;
	[Export] public float CrawlOutSeconds { get; set; } = 0.9f;
	[Export] public float GetUpSeconds { get; set; } = 0.5f;

	[ExportGroup("Sounds")]
	/// <summary>Clothes dragging over the floor.</summary>
	[Export] public AudioStreamPlayer3D CrawlSound { get; set; }
	/// <summary>The frame creaking as a shoulder bumps into it.</summary>
	[Export] public AudioStreamPlayer3D CreakSound { get; set; }

	[ExportGroup("Nodes")]
	[Export] public Marker3D ApproachPoint { get; set; }
	[Export] public Marker3D HidePoint { get; set; }
	[Export] public Marker3D ExitPoint { get; set; }
	[Export] public Node3D SearchPoint { get; set; }
	[Export] public Node3D LookPoint { get; set; }

	private Player _occupant;
	private Player _player;
	private ScriptedMove _move;

	public bool IsOccupied => _occupant is not null && _occupant.HidingSpot == this;

	public string InteractionPrompt => IsOccupied ? "Crawl out" : "Hide under";

	public override void _Ready()
	{
		AddToGroup(IHidingSpot.Group);
	}

	public void Interact(Player player)
	{
		if (IsOccupied)
			LetOut(player);
		else
			TakeIn(player);
	}

	// An enemy only has to kneel down and look; there is nothing to open.
	public void Reveal() { }

	public void Conceal() { }

	public void DragOutOccupant()
	{
		_move?.Kill();
		_occupant.Hit -= OnHit;
		_occupant.PullOutOfHiding();
		_occupant = null;
	}

	private void TakeIn(Player player)
	{
		BeginMove(player);
		player.Hit += OnHit;

		// Walk up beside the bed, carrying on from however the player was moving.
		Vector3 velocity = new(player.Velocity.X, 0f, player.Velocity.Z);
		float distance = player.GlobalPosition.DistanceTo(ApproachPoint.GlobalPosition);
		float approach = Mathf.Max(1.5f * distance / Mathf.Max(velocity.Length(), ApproachSpeed), MinApproachSeconds);
		_move.Walk(ApproachPoint, ApproachPitchDegrees, 0f, approach, velocity);
		float time = approach;

		// Get down on the floor, then crawl in sideways while turning to face out.
		_move.At(time, () =>
		{
			player.LieDown();
			CrawlSound.Play();
		});
		time += GetDownSeconds * 0.5f;
		_move.Walk(HidePoint, 0f, time, CrawlInSeconds);
		_move.At(time + CrawlInSeconds * 0.4f, () => CreakSound.Play());
		_move.At(time + CrawlInSeconds * 0.6f, () => CrawlSound.Play());
		time += CrawlInSeconds;
		_move.At(time, () =>
		{
			_occupant = player;
			player.StartHiding(this, ExitPoint.GlobalTransform);
		});
		_move.FinishAt(time);
	}

	private void LetOut(Player player)
	{
		BeginMove(player);
		_move.At(0f, () => CrawlSound.Play());
		_move.Walk(ExitPoint, 0f, 0f, CrawlOutSeconds);
		// Stay hidden until clear of the bed, so nothing bumps into a player still lying under it.
		_move.At(CrawlOutSeconds, () =>
		{
			player.StopHiding();
			player.GetUp();
		});
		_move.FinishAt(CrawlOutSeconds + GetUpSeconds);
		_move.At(CrawlOutSeconds + GetUpSeconds, () => player.Hit -= OnHit);
	}

	private void BeginMove(Player player)
	{
		_player = player;
		_move = new ScriptedMove(this, player);
	}

	/// <summary>The player was hit while getting under, which cuts the move short.</summary>
	private void OnHit()
	{
		_move.Kill();
		_player.Hit -= OnHit;
		_player.GetUp();
	}
}
