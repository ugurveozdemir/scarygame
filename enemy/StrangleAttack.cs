using System.Threading.Tasks;
using Godot;

/// <summary>
/// The enemy's kill move: it grabs the player by both shoulders, shakes them,
/// then moves its hands to the player's throat and strangles them.
/// The hands are placed with inverse kinematics, so the move fits wherever the player stands.
/// </summary>
public partial class StrangleAttack : Node
{
	[Export] public float GrabDistance { get; set; } = 0.5f;
	[Export] public float ReachSeconds { get; set; } = 0.35f;
	[Export] public float ShakeSeconds { get; set; } = 1f;
	[Export] public float MoveToThroatSeconds { get; set; } = 0.5f;
	[Export] public float ChokeSeconds { get; set; } = 3f;
	[Export] public float HandTremble { get; set; } = 0.008f;

	[ExportGroup("Nodes")]
	[Export] public Node3D Body { get; set; }
	[Export] public Node3D Head { get; set; }
	[Export] public SkeletonModifier3D ArmsIK { get; set; }
	[Export] public Node3D LeftHandTarget { get; set; }
	[Export] public Node3D RightHandTarget { get; set; }

	public async void Begin(Player victim)
	{
		victim.GetGrabbed(Head);
		StepInFrontOf(victim);

		// Facing each other, the enemy's left hand grabs the player's right side and vice versa.
		CreateTween().TweenProperty(ArmsIK, "influence", 1f, ReachSeconds);
		await MoveHandsTo(victim.RightShoulder, victim.LeftShoulder, ReachSeconds);
		await ShakeByTheShoulders(victim);
		await MoveHandsTo(victim.ThroatRight, victim.ThroatLeft, MoveToThroatSeconds);
		await Choke(victim);
		victim.Die();
	}

	private void StepInFrontOf(Player victim)
	{
		Vector3 fromVictim = Body.GlobalPosition - victim.GlobalPosition;
		fromVictim.Y = 0f;
		Body.GlobalPosition = victim.GlobalPosition + fromVictim.Normalized() * GrabDistance;
		Body.LookAt(victim.GlobalPosition, Vector3.Up);
	}

	private async Task MoveHandsTo(Node3D leftHandGoal, Node3D rightHandGoal, float seconds)
	{
		Vector3 leftStart = LeftHandTarget.GlobalPosition;
		Vector3 rightStart = RightHandTarget.GlobalPosition;
		Tween tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenMethod(Callable.From<float>(weight =>
		{
			LeftHandTarget.GlobalPosition = leftStart.Lerp(leftHandGoal.GlobalPosition, weight);
			RightHandTarget.GlobalPosition = rightStart.Lerp(rightHandGoal.GlobalPosition, weight);
		}), 0f, 1f, seconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task ShakeByTheShoulders(Player victim)
	{
		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(amount => victim.ShakeAmount = amount), 1f, 0.2f, ShakeSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task Choke(Player victim)
	{
		Vector3 leftOnThroat = LeftHandTarget.GlobalPosition;
		Vector3 rightOnThroat = RightHandTarget.GlobalPosition;
		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(amount =>
		{
			victim.ChokeAmount = amount;
			// The hands tremble as they squeeze.
			Vector3 tremble = Vector3.Up * Mathf.Sin(amount * 60f) * HandTremble;
			LeftHandTarget.GlobalPosition = leftOnThroat + tremble;
			RightHandTarget.GlobalPosition = rightOnThroat - tremble;
		}), 0f, 1f, ChokeSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}
}
