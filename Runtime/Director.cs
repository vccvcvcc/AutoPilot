using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoPilot.Core
{
	public sealed class Director
	{
		private readonly Dictionary<NormalizedPhase, Func<BotTask>> _handlers = new Dictionary<NormalizedPhase, Func<BotTask>>();
		private BotTask _currentTask;
		private NormalizedPhase _currentPhase;
		private float _taskStartedAt;
		private readonly Queue<NormalizedPhase> _phaseQueue = new Queue<NormalizedPhase>();

		public string CurrentTaskName => _currentTask?.GetType().Name;
		public float TaskElapsedSeconds => Time.time - _taskStartedAt;

		public void SetHandler(NormalizedPhase phase, Func<BotTask> factory)
		{
			_handlers[phase] = factory;
		}

		public void Tick(BotContext ctx)
		{
			var phase = ctx.Blackboard.Get(Keys.Phase, NormalizedPhase.Unknown);
			if (phase != _currentPhase)
			{
				if (_currentTask != null)
				{
					_currentTask.Cancel(ctx);
					_currentTask.OnStop(ctx, BotTaskStatus.Failure);
				}
				_currentPhase = phase;
				_currentTask = null;
				if (_handlers.TryGetValue(phase, out var factory))
				{
					_currentTask = factory();
					_taskStartedAt = Time.time;
					_currentTask.OnStart(ctx);
				}
			}

			if (_currentTask == null)
				return;

			var status = _currentTask.OnTick(ctx);
			if (status != BotTaskStatus.Running)
			{
				_currentTask.OnStop(ctx, status);
				_currentTask = null;
			}
		}
	}
}
