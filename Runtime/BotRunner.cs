using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AutoPilot.Core
{
	public sealed class BotRunner : MonoBehaviour
	{
		private const int RecentLogCapacity = 8;
		public static bool OverlayVisible = true;

		public SensorHub Sensors { get; private set; }
		public Director Director { get; private set; }
		public BotContext Context { get; private set; }
		public BlackboardKey<string> OverlayObjectiveKey;

		private static readonly BotButton[] AllButtons = (BotButton[])Enum.GetValues(typeof(BotButton));
		private readonly Queue<string> _recentLogs = new Queue<string>();
		private GUIStyle _overlayStyle;

		public static BotRunner Create(IVirtualController controller, string name = "AutoPilot")
		{
			var go = new GameObject(name);
			DontDestroyOnLoad(go);
			var runner = go.AddComponent<BotRunner>();
			runner.Sensors = new SensorHub();
			runner.Director = new Director();
			runner.Context = new BotContext
			{
				Blackboard = new Blackboard(),
				Controller = controller,
				Log = new BotLog(name),
			};
			runner.Sensors.SetLog(runner.Context.Log);
			runner.Context.Log.MessageLogged += runner.OnLogMessage;
			return runner;
		}

		private void Update()
		{
			if (Context == null)
				return;
			Sensors.Tick(Context.Blackboard);
			Director.Tick(Context);
		}

		private void OnLogMessage(string level, string message)
		{
			_recentLogs.Enqueue($"[{level}] {message}");
			while (_recentLogs.Count > RecentLogCapacity)
				_recentLogs.Dequeue();
		}

		private void OnGUI()
		{
			if (!OverlayVisible || Context == null)
				return;

			if (_overlayStyle == null)
			{
				_overlayStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 13,
					wordWrap = true,
				};
				_overlayStyle.normal.textColor = Color.white;
			}

			var sb = new StringBuilder(512);
			sb.Append("AutoPilot  phase=").Append(Context.Blackboard.Get(Keys.Phase, NormalizedPhase.Unknown));
			string task = Director.CurrentTaskName;
			sb.Append("  task=").Append(task ?? "(idle)");
			sb.AppendLine();
			if (!string.IsNullOrEmpty(Context.Activity))
				sb.Append("activity: ").AppendLine(Context.Activity);
			if (OverlayObjectiveKey != null)
				sb.Append("objective: ").AppendLine(Context.Blackboard.Get(OverlayObjectiveKey, "-"));
			if (Context.Controller != null)
			{
				sb.Append("stick L(").Append(Context.Controller.LeftStick.x.ToString("F2")).Append(", ")
					.Append(Context.Controller.LeftStick.y.ToString("F2")).Append(") buttons:");
				bool any = false;
				foreach (BotButton button in AllButtons)
				{
					if (Context.Controller.IsPressed(button))
					{
						sb.Append(' ').Append(button);
						any = true;
					}
				}
				if (!any)
					sb.Append(" -");
				sb.AppendLine();
			}
			sb.AppendLine("---- recent log ----");
			foreach (string line in _recentLogs)
				sb.AppendLine(line);

			GUI.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
			GUILayout.BeginArea(new Rect(10f, 10f, 600f, 280f), GUI.skin.box);
			GUILayout.Label(sb.ToString(), _overlayStyle);
			GUILayout.EndArea();
		}

		private void OnDestroy()
		{
			Sensors?.DisposeAll();
			(Context?.Controller as IDisposable)?.Dispose();
		}
	}
}
