using System;

namespace AutoPilot.Core
{
	public enum BotTaskStatus
	{
		Running,
		Success,
		Failure
	}

	public abstract class BotTask
	{
		public virtual void OnStart(BotContext ctx) { }
		public abstract BotTaskStatus OnTick(BotContext ctx);
		public virtual void OnStop(BotContext ctx, BotTaskStatus status) { }
		public virtual void Cancel(BotContext ctx) { }
	}

	public sealed class DoTask : BotTask
	{
		private readonly Action<BotContext> _action;
		public DoTask(Action<BotContext> action) => _action = action;
		public override BotTaskStatus OnTick(BotContext ctx)
		{
			_action?.Invoke(ctx);
			return BotTaskStatus.Success;
		}
	}
}
