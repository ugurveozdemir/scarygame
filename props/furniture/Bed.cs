using Godot;

/// <summary>
/// A hospital bed the player can crawl under to hide, watching the room from floor level.
/// Getting under is an animation made in Blender (tools/player_animations.py): the player drops flat alongside
/// the bed and slides sideways under it in one go, pushing off the floor with the left hand while the right one
/// holds the camera, then turns their head to watch the room, the hand staying on the floor while they hide. They first walk up to where it starts, from
/// wherever they are. The open side is the bed's -X side; an enemy checking the bed kneels there to look underneath.
/// </summary>
public partial class Bed : StaticBody3D, IHidingSpot
{
	// When things happen in the hiding animation, in its own seconds (Blender frame / 30).
	private const float GetDownSeconds = 2f / 30f;
	private const float SlideSeconds = 16f / 30f;
	private const float BumpSeconds = 22f / 30f;
	private const float UnderSeconds = 30f / 30f;

	[ExportGroup("Moving")]
	/// <summary>Walking pace used to time the walk up to the bed when the player is standing still.</summary>
	[Export] public float ApproachSpeed { get; set; } = 1.6f;
	[Export] public float MinApproachSeconds { get; set; } = 0.3f;
	[Export] public float CrawlOutSeconds { get; set; } = 0.9f;
	[Export] public float GetUpSeconds { get; set; } = 0.5f;

	[ExportGroup("Sounds")]
	/// <summary>Clothes dragging over the floor.</summary>
	[Export] public AudioStreamPlayer3D CrawlSound { get; set; }
	/// <summary>The frame creaking as a shoulder bumps into it.</summary>
	[Export] public AudioStreamPlayer3D CreakSound { get; set; }

	[ExportGroup("Nodes")]
	[Export] public PlayerAnimation HideAnimation { get; set; }
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
		StopMove();
		_occupant.Hit -= OnHit;
		_occupant.PullOutOfHiding();
		_occupant = null;
	}

	private void TakeIn(Player player)
	{
		BeginMove(player);
		player.Hit += OnHit;
		// Lying down at once: the animation moves the view down itself.
		float zero = _move.PlayAfterWalking(HideAnimation, 0f, ApproachSpeed, MinApproachSeconds,
			() => player.LieDown(instantly: true));

		_move.At(zero + HideAnimation.RealSeconds(GetDownSeconds), () => CrawlSound.Play());
		_move.At(zero + HideAnimation.RealSeconds(SlideSeconds), () => CrawlSound.Play());
		_move.At(zero + HideAnimation.RealSeconds(BumpSeconds), () => CreakSound.Play());
		_move.At(zero + HideAnimation.RealSeconds(UnderSeconds), () =>
		{
			_occupant = player;
			player.StartHiding(this, ExitPoint.GlobalTransform);
		});
		_move.FinishAt(zero + HideAnimation.RealSeconds(HideAnimation.Length));
	}

	private void LetOut(Player player)
	{
		BeginMove(player);
		HideAnimation.Stop();
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
		StopMove();
		_player.Hit -= OnHit;
		_player.GetUp();
	}

	private void StopMove()
	{
		_move?.Kill();
		HideAnimation.Stop();
	}
}
