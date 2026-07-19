using System.Collections.Generic;

namespace AutoPilot.Core
{
	public sealed class BotContext
	{
		public Blackboard Blackboard { get; set; }
		public IVirtualController Controller { get; set; }
		public BotLog Log { get; set; }
		public string Activity { get; set; }
		public Dictionary<string, object> Data { get; } = new Dictionary<string, object>();
	}
}
