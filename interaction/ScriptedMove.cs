using System;
using Godot;

/// <summary>
/// A timeline of steps that move the player along a scripted path, like getting into a hiding spot.
/// Every step starts after its own delay, so steps can overlap. The player can't walk, look or interact
/// until the move finishes.
/// </summary>
public class ScriptedMove
{
	private readonly Player _player;
	private Transform3D _walkStart;

	public ScriptedMove(Node owner, Player player)
	{
		_player = player;
		player.LockControls();
		// A parallel tween is a timeline: every step starts after its own delay.
		Tween = owner.CreateTween().SetParallel().SetProcessMode(Tween.TweenProcessMode.Physics);
	}

	/// <summary>The underlying timeline, for steps that move something other than the player.</summary>
	public Tween Tween { get; }

	public void At(float time, Action action)
	{
		Tween.TweenCallback(Callable.From(action)).SetDelay(time);
	}

	/// <summary>Gives the player their controls back at <paramref name="time"/>.</summary>
	public void FinishAt(float time)
	{
		At(time, _player.UnlockControls);
	}

	public void Kill()
	{
		Tween.Kill();
	}

	/// <summary>
	/// Walks the player to where <paramref name="animation"/> stands them at <paramref name="fromSeconds"/>,
	/// carrying on from however they were moving, at least at <paramref name="pace"/> metres per second, then
	/// plays it from there; <paramref name="onStart"/> runs just before. Returns the time on this timeline at which
	/// the animation's own second zero falls, to time steps by the animation.
	/// </summary>
	public float PlayAfterWalking(PlayerAnimation animation, float fromSeconds, float pace, float minSeconds,
		Action onStart = null)
	{
		Transform3D start = animation.PlayerTransformAt(_player, fromSeconds);
		Vector3 velocity = new(_player.Velocity.X, 0f, _player.Velocity.Z);
		float distance = _player.GlobalPosition.DistanceTo(start.Origin);
		float seconds = Mathf.Max(1.5f * distance / Mathf.Max(velocity.Length(), pace), minSeconds);
		Walk(start, animation.PitchDegreesAt(fromSeconds), 0f, seconds, velocity);
		At(seconds, () =>
		{
			onStart?.Invoke();
			animation.Play(_player, fromSeconds);
		});
		return seconds - animation.RealSeconds(fromSeconds);
	}

	/// <summary>
	/// Walks the player to <paramref name="point"/>, starting at <paramref name="startVelocity"/> and ending at rest.
	/// The path is a cubic Hermite curve, so a player who was already walking curves smoothly into it.
	/// </summary>
	public void Walk(Node3D point, float headPitchDegrees, float start, float seconds, Vector3 startVelocity = default)
	{
		Walk(() => point.GlobalTransform, headPitchDegrees, start, seconds, startVelocity);
	}

	/// <summary>Walks the player to stand at <paramref name="end"/>, like <see cref="Walk(Node3D, float, float, float, Vector3)"/>.</summary>
	public void Walk(Transform3D end, float headPitchDegrees, float start, float seconds, Vector3 startVelocity = default)
	{
		Walk(() => end, headPitchDegrees, start, seconds, startVelocity);
	}

	private void Walk(Func<Transform3D> destination, float headPitchDegrees, float start, float seconds, Vector3 startVelocity)
	{
		At(start, () => _walkStart = _player.GlobalTransform);
		Tween.TweenMethod(Callable.From<float>(t =>
		{
			Transform3D end = destination();
			float t2 = t * t;
			float t3 = t2 * t;
			Vector3 position = (2f * t3 - 3f * t2 + 1f) * _walkStart.Origin
				+ (t3 - 2f * t2 + t) * seconds * startVelocity
				+ (3f * t2 - 2f * t3) * end.Origin;
			// The player only turns around the vertical axis, so blending the yaw is enough
			// and always yields a clean rotation.
			float yaw = Mathf.LerpAngle(_walkStart.Basis.GetEuler().Y, end.Basis.GetEuler().Y, Mathf.SmoothStep(0f, 1f, t));
			_player.GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw), position);
		}), 0f, 1f, seconds).SetDelay(start);
		Tween.TweenProperty(_player.Head, "rotation:x", Mathf.DegToRad(headPitchDegrees), seconds)
			.SetDelay(start).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}
}
