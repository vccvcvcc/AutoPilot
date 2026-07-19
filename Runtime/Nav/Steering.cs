using AutoPilot.Core;
using UnityEngine;

namespace AutoPilot.Nav
{
	public static class Steering
	{
		/// <summary>世界方向ベクトルを、カメラのforward/right平面基底に射影してスティック値へ変換する。</summary>
		public static Vector2 WorldDirectionToStick(BotContext ctx, Vector3 worldDir)
		{
			Transform cam = ctx.Blackboard.Get(Keys.CameraTransform);
			if (cam == null)
				return new Vector2(worldDir.x, worldDir.z); // カメラ不明なら世界座標をそのまま使う

			Vector3 forward = cam.forward;
			forward.y = 0f;
			forward.Normalize();
			Vector3 right = cam.right;
			right.y = 0f;
			right.Normalize();

			var stick = new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, forward));
			return stick.sqrMagnitude < 0.0001f ? new Vector2(worldDir.x, worldDir.z) : stick.normalized;
		}
	}

	/// <summary>
	/// NavMeshに頼らず、指定した世界方向へ一定時間歩く。
	/// NavMeshが焼かれていないシーンでのフォールバック移動用。
	/// 進捗が止まったら一度ジャンプを試み、それでも動けなければ早期にSuccessで抜ける
	/// (呼び出し側が次の方向を選び直す前提)。
	/// </summary>
	public sealed class WalkDirection : BotTask
	{
		private const float ProgressCheckInterval = 1f;
		private const float MinProgressDistance = 0.3f;

		private readonly Vector3 _worldDir;
		private readonly float _duration;

		private float _endAt;
		private Vector3 _lastCheckPos;
		private float _nextProgressCheckAt;
		private bool _triedJump;
		private float _jumpReleaseAt;

		public WalkDirection(Vector3 worldDir, float duration)
		{
			_worldDir = new Vector3(worldDir.x, 0f, worldDir.z).normalized;
			_duration = duration;
			Name = $"WalkDirection({_worldDir.x:F2}, {_worldDir.z:F2})";
		}

		protected override void OnStart(BotContext ctx)
		{
			_endAt = ctx.Time + _duration;
			_lastCheckPos = ctx.Blackboard.Get(Keys.PlayerPosition);
			_nextProgressCheckAt = ctx.Time + ProgressCheckInterval;
			_triedJump = false;
			_jumpReleaseAt = 0f;
		}

		protected override BotStatus OnTick(BotContext ctx)
		{
			if (!ctx.Blackboard.Get(Keys.PlayerExists))
				return BotStatus.Running;

			if (_jumpReleaseAt > 0f && ctx.Time >= _jumpReleaseAt)
			{
				ctx.Controller.Release(BotButton.South);
				_jumpReleaseAt = 0f;
			}

			if (ctx.Time >= _endAt)
				return BotStatus.Success;

			ctx.Activity = $"WalkDirection: ({_worldDir.x:F2}, {_worldDir.z:F2}) for {_endAt - ctx.Time:F1}s more";
			ctx.Controller.SetLeftStick(Steering.WorldDirectionToStick(ctx, _worldDir));

			if (ctx.Time >= _nextProgressCheckAt)
			{
				_nextProgressCheckAt = ctx.Time + ProgressCheckInterval;
				Vector3 pos = ctx.Blackboard.Get(Keys.PlayerPosition);
				bool moved = (pos - _lastCheckPos).sqrMagnitude >= MinProgressDistance * MinProgressDistance;
				_lastCheckPos = pos;

				if (!moved)
				{
					if (!_triedJump)
					{
						_triedJump = true;
						ctx.Log.Info($"{Name}: blocked, trying a jump");
						ctx.Controller.Press(BotButton.South);
						_jumpReleaseAt = ctx.Time + 0.2f;
					}
					else
					{
						ctx.Log.Info($"{Name}: still blocked, ending this leg early");
						return BotStatus.Success;
					}
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
	}
}
