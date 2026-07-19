using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AutoPilot.Core
{
	public sealed class BotReporter : ISensor, IDisposable
	{
		private readonly string _path;
		private readonly string _sessionId;
		private readonly BotLog _log;
		private readonly StringBuilder _buffer = new StringBuilder();
		private float _lastFlush;

		public BotReporter(string path, string sessionId, BotLog log)
		{
			_path = path;
			_sessionId = sessionId;
			_log = log;
			_lastFlush = Time.time;
		}

		public void Tick(Blackboard blackboard)
		{
			var phase = blackboard.Get(Keys.Phase, NormalizedPhase.Unknown);
			var activity = blackboard.Get(Keys.Activity, "-");
			var pos = blackboard.Get(Keys.PlayerPosition, Vector3.zero);
			_buffer.AppendLine($"{{\"time\":{Time.time:F2},\"phase\":\"{phase}\",\"activity\":\"{activity}\",\"player\":{{\"x\":{pos.x:F2},\"y\":{pos.y:F2},\"z\":{pos.z:F2}}}}}");
			if (Time.time - _lastFlush > 2f)
				Flush();
		}

		public void Dispose() => Flush();

		private void Flush()
		{
			try
			{
				var dir = Path.GetDirectoryName(_path);
				if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
				var payload = $"{{\"sessionId\":\"{_sessionId}\",\"events\":[{_buffer.ToString().TrimEnd()}]}}";
				File.WriteAllText(_path, payload, Encoding.UTF8);
				_lastFlush = Time.time;
				_log?.Info($"Wrote report {_path}");
			}
			catch (Exception e)
			{
				_log?.Warn($"Failed to write report: {e.Message}");
			}
		}
	}
}
