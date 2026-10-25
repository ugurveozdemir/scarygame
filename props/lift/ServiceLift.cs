using System;
using Godot;

/// <summary>
/// The service lift between the ground floor and the sleep ward. It needs power from the basement's main
/// breaker, and on the ground floor the code typed into the keypad. Inside, a button takes the car to the other
/// floor: the doors close, the car hums and shakes, and they open on the other landing. The car is the same
/// room on both floors, so the ride moves whoever is inside by exactly one storey.
/// </summary>
public partial class ServiceLift : Node3D
{
	[Export] public string Code { get; set; } = "";
	[Export] public float RideSeconds { get; set; } = 6f;
	[Export] public LiftLanding GroundLanding { get; set; }
	[Export] public LiftLanding UpperLanding { get; set; }
	/// <summary>The middle of the car on the ground floor; the upper floor's car is the same room one storey up.</summary>
	[Export] public Node3D GroundCar { get; set; }
	[Export] public Vector3 CarSize { get; set; } = new(1.9f, 2.4f, 1.9f);
	[Export] public AudioStreamPlayer3D RideSound { get; set; }

	public enum CodeResult { Accepted, Wrong, NoPower }

	public bool HasPower { get; set; }
	public bool IsUpstairs { get; private set; }
	public bool IsMoving { get; private set; }

	/// <summary>Raised when the doors open on a floor, which anyone nearby can hear.</summary>
	public event Action<LiftLanding> DoorsOpened;
	/// <summary>Raised when the car arrives on a floor, with whether it is the upper one.</summary>
	public event Action<bool> Arrived;

	private LiftLanding CurrentLanding => IsUpstairs ? UpperLanding : GroundLanding;
	private Vector3 StoreyOffset => UpperLanding.GlobalPosition - GroundLanding.GlobalPosition;

	/// <summary>Someone typed <paramref name="code"/> into the ground floor's keypad, which calls the car down.</summary>
	public CodeResult TryCode(string code)
	{
		if (code != Code)
			return CodeResult.Wrong;
		if (!HasPower)
			return CodeResult.NoPower;

		Call(GroundLanding);
		return CodeResult.Accepted;
	}

	/// <summary>Brings the car to <paramref name="landing"/> and opens its doors.</summary>
	public void Call(LiftLanding landing)
	{
		if (IsMoving || !HasPower)
			return;

		if (landing != CurrentLanding)
			MoveCar(null);
		else if (!landing.IsOpen)
			OpenDoors();
	}

	/// <summary>Takes the car, and <paramref name="player"/> if they are in it, to the other floor.</summary>
	public void Ride(Player player)
	{
		if (!IsMoving && HasPower)
			MoveCar(player);
	}

	private async void MoveCar(Player passenger)
	{
		IsMoving = true;
		if (CurrentLanding.IsOpen)
			await ToSignal(CurrentLanding.Close(), Tween.SignalName.Finished);

		RideSound.Play();
		passenger?.CameraRig.AddShake(0.25f);
		await ToSignal(GetTree().CreateTimer(RideSeconds * 0.5f), SceneTreeTimer.SignalName.Timeout);

		// Halfway there, whoever is in the car is moved to the same spot in the car on the other floor.
		Vector3 offset = IsUpstairs ? -StoreyOffset : StoreyOffset;
		if (passenger is not null && IsInCar(passenger))
			passenger.Teleport(offset);
		RideSound.GlobalPosition += offset;
		IsUpstairs = !IsUpstairs;

		await ToSignal(GetTree().CreateTimer(RideSeconds * 0.5f), SceneTreeTimer.SignalName.Timeout);
		passenger?.CameraRig.AddShake(0.2f);
		IsMoving = false;
		Arrived?.Invoke(IsUpstairs);
		OpenDoors();
	}

	private void OpenDoors()
	{
		CurrentLanding.Open();
		DoorsOpened?.Invoke(CurrentLanding);
	}

	private bool IsInCar(Player player)
	{
		Vector3 groundPosition = IsUpstairs ? player.GlobalPosition - StoreyOffset : player.GlobalPosition;
		Vector3 local = GroundCar.ToLocal(groundPosition + Vector3.Up * 0.5f);
		return new Aabb(-CarSize / 2f, CarSize).HasPoint(local);
	}
}
