namespace AutoPilot.Core
{
	public enum BotStatus
	{
		Running,
		Success,
		Failure,
	}

	/// <summary>
	/// 毎フレームTickされる協調型タスクの基底。
	/// Tick外部からの中断はCancel()で行い、OnStopで必ず後始末(入力の解放等)をする。
	/// </summary>
	public abstract class BotTask
	{
		public string Name { get; protected set; }

		private bool _running;

		protected BotTask()
		{
			Name = GetType().Name;
		}

		public BotStatus Tick(BotContext ctx)
		{
			if (!_running)
			{
				_running = true;
				OnStart(ctx);
			}

			BotStatus status = OnTick(ctx);

			if (status != BotStatus.Running)
			{
				_running = false;
				OnStop(ctx, status);
			}
			return status;
		}

		/// <summary>実行途中のタスクを中断する。未実行なら何もしない。</summary>
		public void Cancel(BotContext ctx)
		{
			if (_running)
			{
				_running = false;
				OnStop(ctx, BotStatus.Failure);
			}
		}

		protected virtual void OnStart(BotContext ctx) { }
		protected abstract BotStatus OnTick(BotContext ctx);
		protected virtual void OnStop(BotContext ctx, BotStatus status) { }
	}
}
