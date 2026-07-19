using System;

namespace AutoPilot.Core
{
	public sealed class BotLog
	{
		public event Action<string, string> MessageLogged;
		private readonly string _source;

		public BotLog(string source) => _source = source;

		public void Info(string message) => MessageLogged?.Invoke("info", $"[{_source}] {message}");
		public void Warn(string message) => MessageLogged?.Invoke("warn", $"[{_source}] {message}");
		public void Error(string message) => MessageLogged?.Invoke("error", $"[{_source}] {message}");
	}
}
