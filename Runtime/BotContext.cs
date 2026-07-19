namespace AutoPilot.Core
{
	/// <summary>タスクとセンサーが共有する実行コンテキスト。</summary>
	public sealed class BotContext
	{
		public Blackboard Blackboard;
		public IVirtualController Controller;
		public BotLog Log;

		/// <summary>実行中タスクが毎Tick更新する「今なにをしているか」の説明。デバッグ表示用。</summary>
		public string Activity;

		public float Time => UnityEngine.Time.time;
	}
}
