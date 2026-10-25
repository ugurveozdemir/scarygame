using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// A stalker that walks a patrol route, investigates noises, chases the player on sight and punches them when
/// it catches up. Where it loses them it checks a few hiding spots nearby, tearing lockers open and kneeling
/// to look under beds, and drags out whoever it finds. Hiding right in front of it does not work.
/// </summary>
public partial class Enemy : CharacterBody3D
{
	public enum EnemyState { Patrol, Investigate, Chase, Attack, Search }

	private readonly record struct Punch(string Animation, float ImpactSeconds);

	private const uint SightCollisionMask = 0b11; // world and player layers
	private const float DestinationChangeThreshold = 0.3f;
	private const float MaxStraightWalkSeconds = 1f;
	private const string IdleAnimation = "ual2/Zombie_Idle";
	private const string WalkAnimation = "ual2/Zombie_Walk_Fwd";
	private const string JogAnimation = "ual1/Jog_Fwd";
	private const string SprintAnimation = "ual1/Sprint";
	private const string OpenLockerAnimation = "ual1/Interact";
	private const string LookUnderBedAnimation = "ual1/Fixing_Kneeling";
	// The speeds the walking animations were made for, so the feet don't slide at other speeds.
	private const float WalkAnimationSpeed = 1.15f;
	private const float JogAnimationSpeed = 3.2f;
	private const float SprintAnimationSpeed = 4.6f;
	// When the fist is furthest out, measured from the animations.
	private static readonly Punch[] Punches = { new("ual1/Punch_Jab", 0.24f), new("ual1/Punch_Cross", 0.3f) };

	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 1.0f;
	[Export] public float InvestigateSpeed { get; set; } = 2.8f;
	[Export] public float RunSpeed { get; set; } = 4.0f;
	[Export] public float TurnSpeed { get; set; } = 8f;

	[ExportGroup("Animation")]
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	/// <summary>Quaternius animation libraries retargeted onto the humanoid skeleton, added as "ual1" and "ual2".</summary>
	[Export] public AnimationLibrary Ual1Animations { get; set; }
	[Export] public AnimationLibrary Ual2Animations { get; set; }
	[Export] public float AnimationBlendSeconds { get; set; } = 0.3f;
	/// <summary>Kneeling down and getting up again take longer than switching between walks.</summary>
	[Export] public float KneelBlendSeconds { get; set; } = 0.6f;
	[Export] public float HeadTurnSeconds { get; set; } = 0.4f;

	[ExportGroup("Senses")]
	[Export] public float SightRange { get; set; } = 18f;
	[Export] public float FieldOfViewDegrees { get; set; } = 140f;
	[Export(PropertyHint.Range, "0,1")] public float CrouchedSightMultiplier { get; set; } = 0.65f;
	/// <summary>Closer than this it notices the player even behind its back, as long as nothing is in between.</summary>
	[Export] public float NoticeDistance { get; set; } = 2.5f;
	/// <summary>A player who hides within this many seconds of being seen is found in the hiding spot.</summary>
	[Export] public float HidingWitnessSeconds { get; set; } = 1.5f;

	[ExportGroup("Attack")]
	/// <summary>Distance at which it stops to throw a punch.</summary>
	[Export] public float AttackRange { get; set; } = 1.4f;
	/// <summary>The punch lands if the player is still within this distance and in front when the fist is out.</summary>
	[Export] public float HitReach { get; set; } = 1.9f;
	[Export] public float HitAngleDegrees { get; set; } = 70f;

	[ExportGroup("Behaviour")]
	[Export] public float PatrolWaitSeconds { get; set; } = 2f;
	[Export] public float LookAroundSeconds { get; set; } = 4f;
	[Export] public float LookAroundTurnSpeed { get; set; } = 1.2f;
	/// <summary>Hiding spots this close to where it lost the player are the ones it checks.</summary>
	[Export] public float SearchRadius { get; set; } = 8f;
	[Export] public int SpotsToCheckAfterChase { get; set; } = 2;
	[Export] public int SpotsToCheckAfterNoise { get; set; } = 1;
	/// <summary>How long into the locker animation the hand reaches the door.</summary>
	[Export] public float LockerReachSeconds { get; set; } = 0.7f;
	/// <summary>How long it takes to get down on one knee beside a bed.</summary>
	[Export] public float KneelSeconds { get; set; } = 0.8f;
	/// <summary>How long it stares into a hiding spot that turns out to be empty.</summary>
	[Export] public float StareSeconds { get; set; } = 1.5f;

	[ExportGroup("Nodes")]
	[Export] public Player Player { get; set; }
	[Export] public Node3D PatrolRoute { get; set; }
	[Export] public Node3D Eyes { get; set; }
	[Export] public NavigationAgent3D NavigationAgent { get; set; }
	[Export] public RayCast3D DoorCheck { get; set; }
	[Export] public LookAtModifier3D HeadLookAt { get; set; }
	/// <summary>The effort and the swing of a punch.</summary>
	[Export] public AudioStreamPlayer3D PunchSound { get; set; }

	public EnemyState State { get; private set; } = EnemyState.Patrol;
	public bool IsHunting => State is EnemyState.Chase or EnemyState.Attack;

	public event Action<EnemyState> StateChanged;
	/// <summary>Raised when it starts hunting the player, not again between its punches.</summary>
	public event Action PlayerSpotted;

	private Node3D[] _patrolPoints;
	private int _patrolIndex;
	private float _waitTimeLeft;
	private Vector3 _lastKnownPlayerPosition;
	private float _secondsSincePlayerSeen = float.PositiveInfinity;
	private bool _sawPlayerHide;
	private Vector3 _searchCenter;
	private int _spotsLeftToCheck;
	private readonly HashSet<IHidingSpot> _checkedSpots = new();
	private IHidingSpot _spotToCheck;
	private bool _isCheckingSpot;
	private float _straightWalkSeconds;
	private Tween _headTween;

	public override void _Ready()
	{
		_patrolPoints = PatrolRoute.GetChildren().OfType<Node3D>().ToArray();
		NavigationAgent.TargetPosition = GlobalPosition;
		AnimationPlayer.AddAnimationLibrary("ual1", Ual1Animations);
		AnimationPlayer.AddAnimationLibrary("ual2", Ual2Animations);
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateAnimation();
		if (Player.IsDead)
		{
			StopMoving();
			return;
		}

		float dt = (float)delta;
		UpdateSenses(dt);

		switch (State)
		{
			case EnemyState.Patrol:
				Patrol(dt);
				break;
			case EnemyState.Investigate:
				Investigate(dt);
				break;
			case EnemyState.Chase:
				Chase(dt);
				break;
			case EnemyState.Attack:
				StopMoving();
				FaceDirection(Player.GlobalPosition - GlobalPosition, dt);
				break;
			case EnemyState.Search:
				Search(dt);
				break;
		}

		if (!IsOnFloor())
			Velocity += GetGravity() * dt;
		OpenDoorsInTheWay();
		MoveAndSlide();
	}

	private void UpdateSenses(float delta)
	{
		if (!Player.IsHiding)
			_sawPlayerHide = false;

		if (CanSeePlayer())
		{
			_secondsSincePlayerSeen = 0f;
			_lastKnownPlayerPosition = Player.GlobalPosition;
			// A punch already thrown is finished first.
			if (State != EnemyState.Attack)
				ChangeState(EnemyState.Chase);
			return;
		}

		_secondsSincePlayerSeen += delta;
		if (Player.IsHiding && _secondsSincePlayerSeen < HidingWitnessSeconds)
			_sawPlayerHide = true;

		if (State is EnemyState.Patrol or EnemyState.Investigate && CanHearPlayer())
			StartInvestigating(Player.GlobalPosition, SpotsToCheckAfterNoise);
	}

	private void Patrol(float delta)
	{
		if (_patrolPoints.Length == 0 || _waitTimeLeft > 0f)
		{
			_waitTimeLeft -= delta;
			StopMoving();
			return;
		}

		MoveTowards(_patrolPoints[_patrolIndex].GlobalPosition, WalkSpeed, delta);
		if (NavigationAgent.IsNavigationFinished())
		{
			_patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
			_waitTimeLeft = PatrolWaitSeconds;
		}
	}

	/// <summary>Goes to <paramref name="position"/>, checks up to <paramref name="spotsToCheck"/> hiding spots around it and looks around.</summary>
	private void StartInvestigating(Vector3 position, int spotsToCheck)
	{
		ChangeState(EnemyState.Investigate);
		SetDestination(position);
		_searchCenter = position;
		_spotsLeftToCheck = spotsToCheck;
		_checkedSpots.Clear();
		_waitTimeLeft = LookAroundSeconds;
	}

	private void Investigate(float delta)
	{
		if (!NavigationAgent.IsNavigationFinished())
		{
			FollowPath(InvestigateSpeed, delta);
			return;
		}

		if (_spotsLeftToCheck > 0 && FindSpotToCheck() is { } spot)
		{
			_spotsLeftToCheck--;
			StartChecking(spot);
			return;
		}

		// Nothing more to check: look around for a while, then go back to the patrol route.
		StopMoving();
		RotateY(LookAroundTurnSpeed * delta);
		_waitTimeLeft -= delta;
		if (_waitTimeLeft <= 0f)
			ChangeState(EnemyState.Patrol);
	}

	private void Chase(float delta)
	{
		// A player seen hiding is pulled out of the hiding spot.
		if (_sawPlayerHide)
		{
			_sawPlayerHide = false;
			StartChecking(Player.HidingSpot);
			return;
		}

		MoveTowards(_lastKnownPlayerPosition, RunSpeed, delta);
		bool canSeePlayer = _secondsSincePlayerSeen == 0f;
		if (canSeePlayer && GlobalPosition.DistanceTo(Player.GlobalPosition) <= AttackRange)
			ThrowPunch();
		else if (!canSeePlayer && NavigationAgent.IsNavigationFinished())
			StartInvestigating(_lastKnownPlayerPosition, SpotsToCheckAfterChase);
	}

	private async void ThrowPunch()
	{
		ChangeState(EnemyState.Attack);
		StopMoving();
		TurnHeadTowards(Player.Head);
		Punch punch = Punches[GD.RandRange(0, Punches.Length - 1)];
		PlayAction(punch.Animation, AnimationBlendSeconds);
		PunchSound.Play();

		await Wait(punch.ImpactSeconds);
		if (CanHitPlayer())
			Player.TakeHit(this);

		await Wait((float)AnimationPlayer.CurrentAnimationLength - punch.ImpactSeconds);
		TurnHeadBack();
		if (State == EnemyState.Attack)
			ChangeState(EnemyState.Chase);
	}

	private void StartChecking(IHidingSpot spot)
	{
		_spotToCheck = spot;
		_checkedSpots.Add(spot);
		_straightWalkSeconds = 0f;
		SetDestination(spot.SearchPoint.GlobalPosition);
		ChangeState(EnemyState.Search);
	}

	private void Search(float delta)
	{
		Transform3D searchPoint = _spotToCheck.SearchPoint.GlobalTransform;
		if (_isCheckingSpot)
		{
			StopMoving();
			FaceDirection(-searchPoint.Basis.Z, delta);
			return;
		}

		if (!NavigationAgent.IsNavigationFinished())
		{
			FollowPath(InvestigateSpeed, delta);
			return;
		}

		// The navigation agent stops short of its target, so the last few centimetres are walked straight.
		// Something in the way only gets a moment before it checks from where it stands.
		_straightWalkSeconds += delta;
		if (_straightWalkSeconds > MaxStraightWalkSeconds || !WalkStraightTo(searchPoint.Origin, WalkSpeed, delta))
			CheckSpot(_spotToCheck);
	}

	/// <summary>
	/// Looks into <paramref name="spot"/>: tears the locker open or kneels to look under the bed. A player hiding
	/// there is dragged out and punched; an empty spot is closed again and the search goes on.
	/// </summary>
	private async void CheckSpot(IHidingSpot spot)
	{
		_isCheckingSpot = true;
		bool isLocker = spot is Locker;
		PlayAction(isLocker ? OpenLockerAnimation : LookUnderBedAnimation, isLocker ? AnimationBlendSeconds : KneelBlendSeconds);
		TurnHeadTowards(spot.LookPoint);

		await Wait(isLocker ? LockerReachSeconds : KneelSeconds);
		if (!IsStillChecking(spot))
			return;

		spot.Reveal();
		if (spot.IsOccupied)
		{
			spot.DragOutOccupant();
			_isCheckingSpot = false;
			TurnHeadBack();
			_secondsSincePlayerSeen = 0f;
			_lastKnownPlayerPosition = Player.GlobalPosition;
			ThrowPunch();
			return;
		}

		await Wait(StareSeconds);
		if (!IsStillChecking(spot))
			return;

		spot.Conceal();
		_isCheckingSpot = false;
		TurnHeadBack();
		AnimationPlayer.Play(IdleAnimation, isLocker ? AnimationBlendSeconds : KneelBlendSeconds);
		// Carry on searching: the next spot, or a look around.
		ChangeState(EnemyState.Investigate);
	}

	private bool IsStillChecking(IHidingSpot spot)
	{
		if (State == EnemyState.Search && _spotToCheck == spot)
			return true;

		// Something interrupted the check, like spotting the player elsewhere.
		_isCheckingSpot = false;
		TurnHeadBack();
		return false;
	}

	private IHidingSpot FindSpotToCheck()
	{
		IHidingSpot[] nearby = GetTree().GetNodesInGroup(IHidingSpot.Group).OfType<IHidingSpot>()
			.Where(spot => !_checkedSpots.Contains(spot)
				&& spot.SearchPoint.GlobalPosition.DistanceTo(_searchCenter) <= SearchRadius)
			.ToArray();
		return nearby.Length == 0 ? null : nearby[GD.RandRange(0, nearby.Length - 1)];
	}

	private void ChangeState(EnemyState state)
	{
		if (state == State)
			return;

		bool wasHunting = IsHunting;
		State = state;
		StateChanged?.Invoke(state);
		if (IsHunting && !wasHunting)
			PlayerSpotted?.Invoke();
	}

	private void UpdateAnimation()
	{
		// Punches and checking a hiding spot play their own animations.
		if (State == EnemyState.Attack || _isCheckingSpot)
			return;

		float speed = new Vector2(Velocity.X, Velocity.Z).Length();
		if (speed < 0.1f)
		{
			AnimationPlayer.SpeedScale = 1f;
			AnimationPlayer.Play(IdleAnimation, AnimationBlendSeconds);
			return;
		}

		bool isHurrying = State is EnemyState.Investigate or EnemyState.Search && speed > WalkSpeed;
		(string animation, float animationSpeed) = State == EnemyState.Chase ? (SprintAnimation, SprintAnimationSpeed)
			: isHurrying ? (JogAnimation, JogAnimationSpeed)
			: (WalkAnimation, WalkAnimationSpeed);
		AnimationPlayer.SpeedScale = speed / animationSpeed;
		AnimationPlayer.Play(animation, AnimationBlendSeconds);
	}

	private void PlayAction(string animation, float blendSeconds)
	{
		AnimationPlayer.SpeedScale = 1f;
		AnimationPlayer.Play(animation, blendSeconds);
		// Two punches in a row are the same animation, which has to start over.
		AnimationPlayer.Seek(0f);
	}

	private void TurnHeadTowards(Node3D target)
	{
		HeadLookAt.TargetNode = HeadLookAt.GetPathTo(target);
		TurnHead(1f);
	}

	private void TurnHeadBack()
	{
		TurnHead(0f);
	}

	private void TurnHead(float influence)
	{
		_headTween?.Kill();
		_headTween = CreateTween();
		_headTween.TweenProperty(HeadLookAt, "influence", influence, HeadTurnSeconds);
	}

	private SignalAwaiter Wait(float seconds)
	{
		// A tween belongs to this node, so a wait never finishes once the level is reloaded.
		Tween wait = CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics);
		wait.TweenInterval(seconds);
		return ToSignal(wait, Tween.SignalName.Finished);
	}

	private bool CanSeePlayer()
	{
		Vector3 eyePosition = Eyes.GlobalPosition;
		Vector3 playerHead = Player.Head.GlobalPosition;
		Vector3 toPlayer = playerHead - eyePosition;
		float sightRange = Player.IsCrouching ? SightRange * CrouchedSightMultiplier : SightRange;
		if (Player.IsHiding || toPlayer.Length() > sightRange)
			return false;

		Vector3 flatToPlayer = new(toPlayer.X, 0f, toPlayer.Z);
		bool isClose = flatToPlayer.Length() <= NoticeDistance;
		if (!isClose && (-GlobalBasis.Z).AngleTo(flatToPlayer) > Mathf.DegToRad(FieldOfViewDegrees) / 2f)
			return false;

		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var query = PhysicsRayQueryParameters3D.Create(eyePosition, playerHead, SightCollisionMask, exclude);
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 && hit["collider"].AsGodotObject() == Player;
	}

	private bool CanHearPlayer()
	{
		return GlobalPosition.DistanceTo(Player.GlobalPosition) <= Player.NoiseRadius;
	}

	private bool CanHitPlayer()
	{
		Vector3 toPlayer = Player.GlobalPosition - GlobalPosition;
		toPlayer.Y = 0f;
		return State == EnemyState.Attack && !Player.IsHiding && toPlayer.Length() <= HitReach
			&& (-GlobalBasis.Z).AngleTo(toPlayer) <= Mathf.DegToRad(HitAngleDegrees);
	}

	private void OpenDoorsInTheWay()
	{
		if (DoorCheck.GetCollider() is Door { IsOpen: false } door)
			door.Open(this, burst: State == EnemyState.Chase);
	}

	private void MoveTowards(Vector3 destination, float speed, float delta)
	{
		SetDestination(destination);
		FollowPath(speed, delta);
	}

	private void SetDestination(Vector3 destination)
	{
		// Re-planning the path every frame is wasteful, so only do it when the destination really moves.
		if (NavigationAgent.TargetPosition.DistanceTo(destination) > DestinationChangeThreshold)
			NavigationAgent.TargetPosition = destination;
	}

	private void FollowPath(float speed, float delta)
	{
		if (NavigationAgent.IsNavigationFinished())
		{
			StopMoving();
			return;
		}

		Vector3 direction = NavigationAgent.GetNextPathPosition() - GlobalPosition;
		direction.Y = 0f;
		direction = direction.Normalized();
		Velocity = new Vector3(direction.X * speed, Velocity.Y, direction.Z * speed);
		FaceDirection(direction, delta);
	}

	/// <summary>Walks straight at <paramref name="destination"/>. Returns false once it is there.</summary>
	private bool WalkStraightTo(Vector3 destination, float speed, float delta)
	{
		Vector3 toDestination = destination - GlobalPosition;
		toDestination.Y = 0f;
		if (toDestination.Length() < 0.1f)
		{
			StopMoving();
			return false;
		}

		Vector3 direction = toDestination.Normalized();
		float stepSpeed = Mathf.Min(speed, toDestination.Length() / delta);
		Velocity = new Vector3(direction.X * stepSpeed, Velocity.Y, direction.Z * stepSpeed);
		FaceDirection(direction, delta);
		return true;
	}

	private void StopMoving()
	{
		Velocity = new Vector3(0f, Velocity.Y, 0f);
	}

	private void FaceDirection(Vector3 direction, float delta)
	{
		direction.Y = 0f;
		if (direction.IsZeroApprox())
			return;

		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, TurnSpeed * delta), 0f);
	}
}
