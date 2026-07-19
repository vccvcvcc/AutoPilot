using System;
using System.Collections;
using UnityEngine;

namespace AutoPilot.Core
{
	/// <summary>子タスクを順に実行する。子が失敗したら全体も失敗。</summary>
	public sealed class Sequence : BotTask
	{
		private readonly BotTask[] _children;
		private int _index;

		public Sequence(params BotTask[] children)
		{
			_children = children;
		}

		protected override void OnStart(BotContext ctx) => _index = 0;

		protected override BotStatus OnTick(BotContext ctx)
		{
			while (_index < _children.Length)
			{
				BotStatus status = _children[_index].Tick(ctx);
				if (status == BotStatus.Running)
					return BotStatus.Running;
				if (status == BotStatus.Failure)
					return BotStatus.Failure;
				_index++;
			}
			return BotStatus.Success;
		}

		protected override void OnStop(BotContext ctx, BotStatus status)
		{
			if (_index < _children.Length)
				_children[_index].Cancel(ctx);
		}
	}

	/// <summary>ファクトリで子を生成し続けて無限に繰り返す。Directorのフェーズ遷移によるCancelで終了する。</summary>
	public sealed class RepeatForever : BotTask
	{
		private readonly Func<BotTask> _factory;
		private BotTask _current;

		public RepeatForever(Func<BotTask> factory)
		{
			_factory = factory;
		}

		protected override BotStatus OnTick(BotContext ctx)
		{
			if (_current == null)
				_current = _factory();

			if (_current.Tick(ctx) != BotStatus.Running)
				_current = null;

			return BotStatus.Running;
		}

		protected override void OnStop(BotContext ctx, BotStatus status)
		{
			_current?.Cancel(ctx);
			_current = null;
		}
	}

	/// <summary>子の失敗をSuccessに変換する(失敗しても続行したい場面用)。</summary>
	public sealed class Try : BotTask
	{
		private readonly BotTask _child;

		public Try(BotTask child)
		{
			_child = child;
			Name = $"Try({child.Name})";
		}

		protected override BotStatus OnTick(BotContext ctx)
		{
			BotStatus status = _child.Tick(ctx);
			return status == BotStatus.Failure ? BotStatus.Success : status;
		}

		protected override void OnStop(BotContext ctx, BotStatus status) => _child.Cancel(ctx);
	}

	/// <summary>子に制限時間を課す。超過したら子をCancelしてFailureを返す。</summary>
	public sealed class Timeout : BotTask
	{
		private readonly BotTask _child;
		private readonly float _seconds;
		private float _deadline;

		public Timeout(BotTask child, float seconds)
		{
			_child = child;
			_seconds = seconds;
			Name = $"Timeout({child.Name}, {seconds}s)";
		}

		protected override void OnStart(BotContext ctx) => _deadline = ctx.Time + _seconds;

		protected override BotStatus OnTick(BotContext ctx)
		{
			BotStatus status = _child.Tick(ctx);
			if (status != BotStatus.Running)
				return status;
			if (ctx.Time >= _deadline)
			{
				ctx.Log.Warn($"{Name}: timed out");
				return BotStatus.Failure;
			}
			return BotStatus.Running;
		}

		protected override void OnStop(BotContext ctx, BotStatus status) => _child.Cancel(ctx);
	}

	/// <summary>
	/// 条件が真になるまで待つ。タイムアウト時はfailOnTimeoutに応じてFailure/Successを返す。
	/// (UnityEngine.WaitUntilと名前が衝突するためWaitForという名前にしている)
	/// </summary>
	public sealed class WaitFor : BotTask
	{
		private readonly Func<BotContext, bool> _predicate;
		private readonly float _timeoutSeconds;
		private readonly bool _failOnTimeout;
		private float _deadline;

		public WaitFor(Func<BotContext, bool> predicate, float timeoutSeconds = -1f, bool failOnTimeout = true)
		{
			_predicate = predicate;
			_timeoutSeconds = timeoutSeconds;
			_failOnTimeout = failOnTimeout;
		}

		protected override void OnStart(BotContext ctx)
		{
			_deadline = _timeoutSeconds > 0f ? ctx.Time + _timeoutSeconds : float.PositiveInfinity;
		}

		protected override BotStatus OnTick(BotContext ctx)
		{
			if (_predicate(ctx))
				return BotStatus.Success;
			if (ctx.Time >= _deadline)
				return _failOnTimeout ? BotStatus.Failure : BotStatus.Success;
			return BotStatus.Running;
		}
	}

	public sealed class WaitSeconds : BotTask
	{
		private readonly float _seconds;
		private float _deadline;

		public WaitSeconds(float seconds)
		{
			_seconds = seconds;
		}

		protected override void OnStart(BotContext ctx) => _deadline = ctx.Time + _seconds;

		protected override BotStatus OnTick(BotContext ctx)
			=> ctx.Time >= _deadline ? BotStatus.Success : BotStatus.Running;
	}

	/// <summary>1フレームで完了する副作用。</summary>
	public sealed class Do : BotTask
	{
		private readonly Action<BotContext> _action;

		public Do(Action<BotContext> action)
		{
			_action = action;
		}

		protected override BotStatus OnTick(BotContext ctx)
		{
			_action(ctx);
			return BotStatus.Success;
		}
	}

	/// <summary>ボタンを短時間押して離す。中断されても必ず離す。</summary>
	public sealed class TapButton : BotTask
	{
		private readonly BotButton _button;
		private readonly float _holdSeconds;
		private float _releaseAt;

		public TapButton(BotButton button, float holdSeconds = 0.12f)
		{
			_button = button;
			_holdSeconds = holdSeconds;
			Name = $"Tap({button})";
		}

		protected override void OnStart(BotContext ctx)
		{
			ctx.Activity = $"Tap {_button}";
			ctx.Controller.Press(_button);
			_releaseAt = ctx.Time + _holdSeconds;
		}

		protected override BotStatus OnTick(BotContext ctx)
			=> ctx.Time >= _releaseAt ? BotStatus.Success : BotStatus.Running;

		protected override void OnStop(BotContext ctx, BotStatus status) => ctx.Controller.Release(_button);
	}

	/// <summary>
	/// IEnumeratorで逐次シナリオを書くためのアダプタ。
	/// yield return null → 1フレーム待ち / float・int → 秒待ち / BotTask → 完了まで実行(失敗は伝播)。
	/// </summary>
	public sealed class EnumeratorTask : BotTask
	{
		private readonly Func<BotContext, IEnumerator> _factory;
		private IEnumerator _routine;
		private BotTask _child;
		private float _waitUntilTime;

		public EnumeratorTask(string name, Func<BotContext, IEnumerator> factory)
		{
			Name = name;
			_factory = factory;
		}

		protected override void OnStart(BotContext ctx)
		{
			_routine = _factory(ctx);
			_child = null;
			_waitUntilTime = -1f;
		}

		protected override BotStatus OnTick(BotContext ctx)
		{
			if (_child != null)
			{
				BotStatus childStatus = _child.Tick(ctx);
				if (childStatus == BotStatus.Running)
					return BotStatus.Running;
				_child = null;
				if (childStatus == BotStatus.Failure)
					return BotStatus.Failure;
			}

			if (_waitUntilTime > 0f)
			{
				if (ctx.Time < _waitUntilTime)
					return BotStatus.Running;
				_waitUntilTime = -1f;
			}

			while (true)
			{
				if (!_routine.MoveNext())
					return BotStatus.Success;

				object current = _routine.Current;

				if (current == null)
					return BotStatus.Running;

				if (current is BotTask task)
				{
					BotStatus status = task.Tick(ctx);
					if (status == BotStatus.Running)
					{
						_child = task;
						return BotStatus.Running;
					}
					if (status == BotStatus.Failure)
						return BotStatus.Failure;
					continue; // 子が即時Successなら次のyieldへ
				}

				if (current is float seconds)
				{
					_waitUntilTime = ctx.Time + seconds;
					return BotStatus.Running;
				}

				if (current is int intSeconds)
				{
					_waitUntilTime = ctx.Time + intSeconds;
					return BotStatus.Running;
				}

				return BotStatus.Running; // 未知のyield値は1フレーム待ち扱い
			}
		}

		protected override void OnStop(BotContext ctx, BotStatus status)
		{
			_child?.Cancel(ctx);
			_child = null;
			_routine = null;
		}
	}
}
