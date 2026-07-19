using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoPilot.Core
{
	public static class Tasks
	{
		public static BotTask Sequence(params BotTask[] tasks) => new SequenceTask(tasks);
		public static BotTask Timeout(BotTask child, float seconds) => new TimeoutTask(child, seconds);
		public static BotTask WaitSeconds(float seconds) => new WaitSecondsTask(seconds);
		public static BotTask Do(Action<BotContext> action) => new DoTask(action);
	}

	public sealed class SequenceTask : BotTask
	{
		private readonly BotTask[] _tasks;
		private int _index;
		public SequenceTask(BotTask[] tasks) => _tasks = tasks;

		public override void OnStart(BotContext ctx)
		{
			_index = 0;
			_tasks[_index].OnStart(ctx);
		}

		public override BotTaskStatus OnTick(BotContext ctx)
		{
			while (true)
			{
				var status = _tasks[_index].OnTick(ctx);
				if (status == BotTaskStatus.Running)
					return BotTaskStatus.Running;

				_tasks[_index].OnStop(ctx, status);
				if (status == BotTaskStatus.Failure)
					return BotTaskStatus.Failure;

				_index++;
				if (_index >= _tasks.Length)
					return BotTaskStatus.Success;
				_tasks[_index].OnStart(ctx);
			}
		}

		public override void OnStop(BotContext ctx, BotTaskStatus status)
		{
			if (_index < _tasks.Length)
				_tasks[_index].OnStop(ctx, status);
		}

		public override void Cancel(BotContext ctx)
		{
			if (_index < _tasks.Length)
				_tasks[_index].Cancel(ctx);
		}
	}

	public sealed class TimeoutTask : BotTask
	{
		private readonly BotTask _child;
		private readonly float _seconds;
		private float _startTime;
		public TimeoutTask(BotTask child, float seconds)
		{
			_child = child;
			_seconds = seconds;
		}

		public override void OnStart(BotContext ctx)
		{
			_startTime = Time.time;
			_child.OnStart(ctx);
		}

		public override BotTaskStatus OnTick(BotContext ctx)
		{
			if (Time.time - _startTime > _seconds)
			{
				_child.Cancel(ctx);
				_child.OnStop(ctx, BotTaskStatus.Failure);
				return BotTaskStatus.Failure;
			}
			var status = _child.OnTick(ctx);
			if (status != BotTaskStatus.Running)
				_child.OnStop(ctx, status);
			return status;
		}

		public override void OnStop(BotContext ctx, BotTaskStatus status)
		{
			_child.OnStop(ctx, status);
		}

		public override void Cancel(BotContext ctx)
		{
			_child.Cancel(ctx);
		}
	}

	public sealed class WaitSecondsTask : BotTask
	{
		private readonly float _seconds;
		private float _startTime;
		public WaitSecondsTask(float seconds) => _seconds = seconds;

		public override void OnStart(BotContext ctx) => _startTime = Time.time;

		public override BotTaskStatus OnTick(BotContext ctx) => Time.time - _startTime >= _seconds ? BotTaskStatus.Success : BotTaskStatus.Running;
	}
}
