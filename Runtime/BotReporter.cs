using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AutoPilot.Core
{
	[Serializable]
	public class BotReportEvent
	{
		public float t;       // Time.time
		public string type;   // phase / objective / info / warn / error / unity-error
		public string value;
	}

	[Serializable]
	public class BotReportData
	{
		public string sessionId;
		public string startedAtUtc;
		public string updatedAtUtc;
		public bool completed;
		public int errorCount;
		public int warnCount;
		public float totalDistanceMoved;
		public string lastPhase;
		public string lastObjective;
		public List<BotReportEvent> timeline = new List<BotReportEvent>();
	}

	/// <summary>
	/// セッションの実行結果を構造化JSONとして書き出すレポーター。
	/// Sensorとして最後に登録し、Blackboardの変化(フェーズ・目標)、BotLogの全メッセージ、
	/// Unityの例外/エラーログ、移動量を記録して5秒ごとにフラッシュする
	/// (クラッシュしても直近5秒までの記録が残る)。
	/// 外部プロセス(自動修正ループ)はこのファイルを読んで結果判定を行う。
	/// </summary>
	public sealed class BotReporter : ISensor, IDisposable
	{
		private const float FlushInterval = 5f;
		private const int TimelineSoftCap = 1500; // 超過後はinfoイベントを間引く
		private const float TeleportThreshold = 20f; // これ以上の瞬間移動は移動量に数えない

		private readonly string _filePath;
		private readonly BotLog _log;
		private readonly BlackboardKey<string> _objectiveKey;
		private readonly BotReportData _data = new BotReportData();

		private NormalizedPhase _lastPhase = NormalizedPhase.Unknown;
		private string _lastObjective;
		private Vector3 _lastPosition;
		private bool _hasPosition;
		private float _nextFlushAt;
		private bool _disposed;

		public BotReporter(string filePath, string sessionId, BotLog log, BlackboardKey<string> objectiveKey = null)
		{
			_filePath = filePath;
			_log = log;
			_objectiveKey = objectiveKey;
			_data.sessionId = sessionId;
			_data.startedAtUtc = DateTime.UtcNow.ToString("o");

			_log.MessageLogged += OnBotLog;
			Application.logMessageReceived += OnUnityLog;

			Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
			Flush();
		}

		public void Tick(Blackboard blackboard)
		{
			NormalizedPhase phase = blackboard.Get(Keys.Phase, NormalizedPhase.Unknown);
			if (phase != _lastPhase)
			{
				_lastPhase = phase;
				_data.lastPhase = phase.ToString();
				AddEvent("phase", phase.ToString());
			}

			if (_objectiveKey != null)
			{
				string objective = blackboard.Get(_objectiveKey);
				if (objective != null && objective != _lastObjective)
				{
					_lastObjective = objective;
					_data.lastObjective = objective;
					AddEvent("objective", objective);
				}
			}

			if (blackboard.Get(Keys.PlayerExists))
			{
				Vector3 position = blackboard.Get(Keys.PlayerPosition);
				if (_hasPosition)
				{
					float delta = Vector3.Distance(position, _lastPosition);
					if (delta < TeleportThreshold)
						_data.totalDistanceMoved += delta;
				}
				_lastPosition = position;
				_hasPosition = true;
			}

			if (Time.unscaledTime >= _nextFlushAt)
			{
				_nextFlushAt = Time.unscaledTime + FlushInterval;
				Flush();
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;
			_disposed = true;
			_log.MessageLogged -= OnBotLog;
			Application.logMessageReceived -= OnUnityLog;
			_data.completed = true;
			Flush();
		}

		private void OnBotLog(string level, string message)
		{
			if (level == "error")
				_data.errorCount++;
			else if (level == "warn")
				_data.warnCount++;
			AddEvent(level, message);
		}

		private void OnUnityLog(string condition, string stackTrace, LogType type)
		{
			if (type != LogType.Exception && type != LogType.Error)
				return;
			if (condition.StartsWith("[AutoPilot")) // BotLog経由分は二重計上しない
				return;
			_data.errorCount++;
			AddEvent("unity-error", condition);
		}

		private void AddEvent(string type, string value)
		{
			if (_data.timeline.Count >= TimelineSoftCap && type == "info")
				return;
			_data.timeline.Add(new BotReportEvent { t = Time.time, type = type, value = value });
		}

		private void Flush()
		{
			try
			{
				_data.updatedAtUtc = DateTime.UtcNow.ToString("o");
				File.WriteAllText(_filePath, JsonUtility.ToJson(_data, true));
			}
			catch (Exception)
			{
				// レポート書き込み失敗でボット本体を止めない
			}
		}
	}
}
