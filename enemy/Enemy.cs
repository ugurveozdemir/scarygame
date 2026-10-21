using System.Linq;
using Godot;

/// <summary>
/// A stalker that walks a patrol route, investigates noises, chases the player on sight
/// and searches where it lost them. Hiding right in front of it does not work.
/// When it catches the player it strangles them.
/// </summary>
public partial class Enemy : CharacterBody3D
{
	public enum EnemyState { Patrol, Investigate, Chase, Strangling }

	private const uint SightCollisionMask = 0b11; // world and player layers
	private const float DestinationChangeThreshold = 0.3f;
	private const string IdleAnimation = "ual2/Zombie_Idle";
	private const string WalkAnimation = "ual2/Zombie_Walk_Fwd";
	private const string JogAnimation = "ual1/Jog_Fwd";
	private const string SprintAnimation = "ual1/Sprint";
	private const string StrangleAnimation = "ual1/Idle"; // upright base pose; IK moves the arms

	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 1.15f;
	[Export] public float InvestigateSpeed { get; set; } = 3.2f;
	[Export] public float RunSpeed { get; set; } = 4.6f;
	[Export] public float TurnSpeed { get; set; } = 8f;

	[ExportGroup("Animation")]
	[Export] public AnimationPlayer AnimationPlayer { get; set; }
	/// <summary>Quaternius animation libraries retargeted onto the humanoid skeleton, added as "ual1" and "ual2".</summary>
	[Export] public AnimationLibrary Ual1Animations { get; set; }
	[Export] public AnimationLibrary Ual2Animations { get; set; }
	[Export] public float AnimationBlendSeconds { get; set; } = 0.3f;

	[ExportGroup("Senses")]
	[Export] public float SightRange { get; set; } = 14f;
	[Export] public float FieldOfViewDegrees { get; set; } = 110f;
	[Export(PropertyHint.Range, "0,1")] public float CrouchedSightMultiplier { get; set; } = 0.5f;
	/// <summary>Distance at which the enemy lunges at the player it is chasing.</summary>
	[Export] public float CatchDistance { get; set; } = 1.5f;
	/// <summary>A player who hides within this many seconds of being seen is caught in the hiding spot.</summary>
	[Export] public float HidingWitnessSeconds { get; set; } = 1f;

	[ExportGroup("Behaviour")]
	[Export] public float PatrolWaitSeconds { get; set; } = 2f;
	[Export] public float LookAroundSeconds { get; set; } = 4f;
	[Export] public float LookAroundTurnSpeed { get; set; } = 1.2f;

	[ExportGroup("Nodes")]
	[Export] public Player Player { get; set; }
	[Export] public Node3D PatrolRoute { get; set; }
	[Export] public Node3D Eyes { get; set; }
	[Export] public NavigationAgent3D NavigationAgent { get; set; }
	[Export] public RayCast3D DoorCheck { get; set; }
	[Export] public StrangleAttack StrangleAttack { get; set; }

	public EnemyState State { get; private set; } = EnemyState.Patrol;

	private Node3D[] _patrolPoints;
	private int _patrolIndex;
	private float _waitTimeLeft;
	private Vector3 _lastKnownPlayerPosition;
	private float _secondsSincePlayerSeen = float.PositiveInfinity;
	private bool _sawPlayerHide;

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
		if (State == EnemyState.Strangling)
			return;

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
		}

		if (!IsOnFloor())
			Velocity += GetGravity() * dt;
		OpenDoorsInTheWay();
		MoveAndSlide();
	}

	private void UpdateSenses(float delta)
	{
		if (CanSeePlayer())
		{
			_secondsSincePlayerSeen = 0f;
			_lastKnownPlayerPosition = Player.GlobalPosition;
			State = EnemyState.Chase;
			return;
		}

		_secondsSincePlayerSeen += delta;
		if (!Player.IsHiding)
			_sawPlayerHide = false;
		else if (_secondsSincePlayerSeen < HidingWitnessSeconds)
			_sawPlayerHide = true;

		if (State != EnemyState.Chase && CanHearPlayer())
			StartInvestigating(Player.GlobalPosition);
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

	private void StartInvestigating(Vector3 position)
	{
		State = EnemyState.Investigate;
		SetDestination(position);
		_waitTimeLeft = LookAroundSeconds;
	}

	private void Investigate(float delta)
	{
		if (!NavigationAgent.IsNavigationFinished())
		{
			FollowPath(InvestigateSpeed, delta);
			return;
		}

		// Arrived: look around for a while, then go back to the patrol route.
		StopMoving();
		RotateY(LookAroundTurnSpeed * delta);
		_waitTimeLeft -= delta;
		if (_waitTimeLeft <= 0f)
			State = EnemyState.Patrol;
	}

	private void Chase(float delta)
	{
		// A player seen hiding is dragged out of the hiding spot.
		Vector3 target = _sawPlayerHide ? Player.GlobalPosition : _lastKnownPlayerPosition;
		MoveTowards(target, RunSpeed, delta);

		bool knowsWherePlayerIs = _secondsSincePlayerSeen == 0f || _sawPlayerHide;
		if (knowsWherePlayerIs && GlobalPosition.DistanceTo(Player.GlobalPosition) <= CatchDistance)
			StartStrangling();
		else if (!knowsWherePlayerIs && NavigationAgent.IsNavigationFinished())
			StartInvestigating(_lastKnownPlayerPosition);
	}

	private void StartStrangling()
	{
		State = EnemyState.Strangling;
		StopMoving();
		StrangleAttack.Begin(Player);
	}

	private void UpdateAnimation()
	{
		bool isMoving = new Vector2(Velocity.X, Velocity.Z).Length() > 0.1f;
		// While lunging it keeps running into the grab instead of stopping dead.
		string animation = State == EnemyState.Strangling ? (StrangleAttack.IsLunging ? SprintAnimation : StrangleAnimation)
			: !isMoving ? IdleAnimation
			: State == EnemyState.Chase ? SprintAnimation
			: State == EnemyState.Investigate ? JogAnimation
			: WalkAnimation;
		AnimationPlayer.Play(animation, AnimationBlendSeconds);
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
		if ((-GlobalBasis.Z).AngleTo(flatToPlayer) > Mathf.DegToRad(FieldOfViewDegrees) / 2f)
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

	private void OpenDoorsInTheWay()
	{
		if (DoorCheck.GetCollider() is Door { IsOpen: false } door)
			door.Open(this);
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

	private void StopMoving()
	{
		Velocity = new Vector3(0f, Velocity.Y, 0f);
	}

	private void FaceDirection(Vector3 direction, float delta)
	{
		if (direction.IsZeroApprox())
			return;

		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, TurnSpeed * delta), 0f);
	}
}
