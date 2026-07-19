using AutoPilot.Core;
using UnityEngine;
using UnityEngine.AI;

namespace AutoPilot.Nav
{
	/// <summary>
	/// NavMesh経路を計算し、次のコーナーへの方向をカメラ相対のスティック入力に変換して目的地まで歩く。
	/// 進捗ウォッチドッグを内蔵:一定時間目的地への残距離が縮まらなければ、
	/// 一度だけジャンプで復旧を試み、それでも駄目ならFailureを返す。
	/// </summary>
	public sealed class NavigateTo : BotTask
	{
		private const float RepathInterval = 1f;
		private const float CornerReachedRadius = 0.4f;
		private const float ProgressEpsilon = 0.2f;

		private readonly Vector3 _destination;
		private readonly float _acceptRadius;
		private readonly float _stuckTimeout;

		private NavMeshPath _path;
		private float _nextRepathAt;
		private int _cornerIndex;
		private float _bestRemaining;
		private float _lastProgressAt;
		private bool _triedJumpRecovery;
		private float _jumpReleaseAt;

		public NavigateTo(Vector3 destination, float acceptRadius = 0.8f, float stuckTimeout = 4f)
		{
			_destination = destination;
			_acceptRadius = acceptRadius;
			_stuckTimeout = stuckTimeout;
			Name = $"NavigateTo({destination.x:F1}, {destination.z:F1})";
		}

		protected override void OnStart(BotContext ctx)
		{
			_path = new NavMeshPath();
			_nextRepathAt = 0f;
			_cornerIndex = 0;
			_bestRemaining = float.PositiveInfinity;
			_lastProgressAt = ctx.Time;
			_triedJumpRecovery = false;
			_jumpReleaseAt = 0f;
		}

		protected override BotStatus OnTick(BotContext ctx)
		{
			// ロード直後などプレイヤーが一時的に存在しない間は待つ(打ち切りは外側のTimeoutに任せる)
			if (!ctx.Blackboard.Get(Keys.PlayerExists))
				return BotStatus.Running;

			Vector3 pos = ctx.Blackboard.Get(Keys.PlayerPosition);

			if (_jumpReleaseAt > 0f && ctx.Time >= _jumpReleaseAt)
			{
				ctx.Controller.Release(BotButton.South);
				_jumpReleaseAt = 0f;
			}

			if (FlatDistance(pos, _destination) <= _acceptRadius)
			{
				ctx.Controller.SetLeftStick(Vector2.zero);
				return BotStatus.Success;
			}

			if (ctx.Time >= _nextRepathAt)
			{
				_nextRepathAt = ctx.Time + RepathInterval;
				if (!NavMesh.CalculatePath(SnapToNavMesh(pos), SnapToNavMesh(_destination), NavMesh.AllAreas, _path)
					|| _path.corners.Length == 0)
				{
					ctx.Log.Warn($"{Name}: no NavMesh path found");
					return BotStatus.Failure;
				}
				_cornerIndex = 0;
			}

			Vector3[] corners = _path.corners;
			while (_cornerIndex < corners.Length - 1 && FlatDistance(pos, corners[_cornerIndex]) < CornerReachedRadius)
				_cornerIndex++;
			Vector3 target = corners[Mathf.Min(_cornerIndex, corners.Length - 1)];

			Vector3 worldDir = target - pos;
			worldDir.y = 0f;
			if (worldDir.sqrMagnitude < 0.0001f)
			{
				worldDir = _destination - pos;
				worldDir.y = 0f;
			}
			worldDir.Normalize();

			ctx.Controller.SetLeftStick(Steering.WorldDirectionToStick(ctx, worldDir));

			float remaining = FlatDistance(pos, _destination);
			ctx.Activity = $"NavigateTo: {remaining:F1}m left (corner {_cornerIndex + 1}/{corners.Length})";
			if (remaining < _bestRemaining - ProgressEpsilon)
			{
				_bestRemaining = remaining;
				_lastProgressAt = ctx.Time;
			}
			else if (ctx.Time - _lastProgressAt > _stuckTimeout)
			{
				if (!_triedJumpRecovery)
				{
					_triedJumpRecovery = true;
					_lastProgressAt = ctx.Time;
					ctx.Log.Info($"{Name}: stuck, trying a jump");
					ctx.Controller.Press(BotButton.South);
					_jumpReleaseAt = ctx.Time + 0.2f;
				}
				else
				{
					ctx.Log.Warn($"{Name}: stuck for {_stuckTimeout}s after recovery, giving up");
					return BotStatus.Failure;
				}
			}

			return BotStatus.Running;
		}

		protected override void OnStop(BotContext ctx, BotStatus status)
		{
			ctx.Controller.SetLeftStick(Vector2.zero);
			if (_jumpReleaseAt > 0f)
			{
				ctx.Controller.Release(BotButton.South);
				_jumpReleaseAt = 0f;
			}
		}

		private static Vector3 SnapToNavMesh(Vector3 position)
		{
			return NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)
				? hit.position
				: position;
		}

		private static float FlatDistance(Vector3 a, Vector3 b)
		{
			float dx = a.x - b.x;
			float dz = a.z - b.z;
			return Mathf.Sqrt(dx * dx + dz * dz);
		}
	}

	public static class NavUtil
	{
		/// <summary>originの周囲radius内でNavMesh上の到達可能な点をランダムに探す。</summary>
		public static bool TryGetRandomReachablePoint(Vector3 origin, float radius, out Vector3 result)
		{
			for (int i = 0; i < 8; i++)
			{
				Vector2 offset = Random.insideUnitCircle * radius;
				Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);
				if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
				{
					result = hit.position;
					return true;
				}
			}
			result = origin;
			return false;
		}
	}
}
