using System;
using System.Collections.Generic;

namespace AutoPilot.Core
{
	/// <summary>
	/// 周回ボットの最上位。Blackboardの正規化フェーズを監視し、
	/// フェーズごとに登録されたハンドラタスクを起動・中断する。
	/// - フェーズが変わったら実行中タスクをCancelし、入力を中立化して次のハンドラへ
	/// - ハンドラが成功したら、同じフェーズに留まる間は再実行しない
	/// - ハンドラが失敗したら、クールダウン後に再実行する
	/// </summary>
	public sealed class Director
	{
		public float RetryCooldown = 3f;

		private readonly Dictionary<NormalizedPhase, Func<BotTask>> _handlers =
			new Dictionary<NormalizedPhase, Func<BotTask>>();

		private NormalizedPhase _currentPhase = NormalizedPhase.Unknown;
		private BotTask _currentTask;
		private bool _completedForPhase;
		private float _retryAt;
		private float _taskStartedAt;

		/// <summary>デバッグ表示用。</summary>
		public string CurrentTaskName => _currentTask != null ? _currentTask.Name : null;
		public float TaskElapsedSeconds => _currentTask != null ? UnityEngine.Time.time - _taskStartedAt : 0f;

		public void SetHandler(NormalizedPhase phase, Func<BotTask> factory)
		{
			_handlers[phase] = factory;
		}

		public void Tick(BotContext ctx)
		{
			NormalizedPhase phase = ctx.Blackboard.Get(Keys.Phase, NormalizedPhase.Unknown);

			if (phase != _currentPhase)
			{
				ctx.Log.Info($"Phase: {_currentPhase} -> {phase}");
				if (_currentTask != null)
				{
					_currentTask.Cancel(ctx);
					_currentTask = null;
				}
				ctx.Controller?.NeutralAll();
				_currentPhase = phase;
				_completedForPhase = false;
				_retryAt = 0f;
			}

			if (_currentTask == null)
			{
				if (_completedForPhase || ctx.Time < _retryAt)
					return;
				if (!_handlers.TryGetValue(phase, out Func<BotTask> factory))
					return;

				_currentTask = factory();
				_taskStartedAt = UnityEngine.Time.time;
				ctx.Activity = null;
				ctx.Log.Info($"Start '{_currentTask.Name}' for phase {phase}");
			}

			BotStatus status;
			try
			{
				status = _currentTask.Tick(ctx);
			}
			catch (Exception e)
			{
				ctx.Log.Error($"Task '{_currentTask.Name}' threw: {e}");
				_currentTask = null;
				_retryAt = ctx.Time + RetryCooldown;
				ctx.Controller?.NeutralAll();
				return;
			}

			if (status == BotStatus.Running)
				return;

			ctx.Log.Info($"Task '{_currentTask.Name}' finished: {status}");
			_currentTask = null;
			ctx.Controller?.NeutralAll();

			if (status == BotStatus.Success)
				_completedForPhase = true;
			else
				_retryAt = ctx.Time + RetryCooldown;
		}
	}
}
