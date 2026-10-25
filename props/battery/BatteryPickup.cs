using Godot;

/// <summary>
/// A spare camcorder battery lying around the level. One on the floor is picked up with an animation made in
/// Blender (tools/player_animations.py): the player squats, takes it, glances at it and pockets it. The battery
/// looks the same from every side, so the animation first turns to face the player. One on a table is just taken.
/// </summary>
public partial class BatteryPickup : StaticBody3D, IInteractable
{
	/// <summary>How far above the player's feet a battery still counts as lying on the floor.</summary>
	private const float FloorHeight = 0.3f;

	/// <summary>Walking pace used to time the walk up to the battery when the player is standing still.</summary>
	[Export] public float ApproachSpeed { get; set; } = 1.6f;
	[Export] public float MinApproachSeconds { get; set; } = 0.3f;

	[ExportGroup("Nodes")]
	[Export] public PlayerAnimation PickupAnimation { get; set; }
	[Export] public Node3D Mesh { get; set; }

	private Player _player;
	private ScriptedMove _move;
	private Transform3D _meshAtRest;

	public string InteractionPrompt => "Take battery";

	public override void _Ready()
	{
		_meshAtRest = Mesh.Transform;
	}

	public void Interact(Player player)
	{
		if (!player.Camcorder.CanAddSpareBattery)
			return;

		if (GlobalPosition.Y - player.GlobalPosition.Y > FloorHeight)
			Take(player);
		else
			PickUp(player);
	}

	private void PickUp(Player player)
	{
		_player = player;
		_move = new ScriptedMove(this, player);
		player.Hit += OnHit;

		Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
		PickupAnimation.GlobalRotation = new Vector3(0f, Mathf.Atan2(-toPlayer.X, -toPlayer.Z), 0f);
		float zero = _move.PlayAfterWalking(PickupAnimation, 0f, ApproachSpeed, MinApproachSeconds);

		float end = zero + PickupAnimation.RealSeconds(PickupAnimation.Length);
		_move.FinishAt(end);
		_move.At(end, () =>
		{
			player.Hit -= OnHit;
			Mesh.Transform = _meshAtRest;
			Take(player);
		});
	}

	private void Take(Player player)
	{
		if (player.Camcorder.TryAddSpareBattery())
			QueueFree();
	}

	/// <summary>The player was hit while picking it up, which drops the battery back where it was.</summary>
	private void OnHit()
	{
		_move.Kill();
		PickupAnimation.Stop();
		Mesh.Transform = _meshAtRest;
		_player.Hit -= OnHit;
	}
}
