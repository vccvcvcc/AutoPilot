using System;
using UnityEngine;

namespace AutoPilot.Core
{
	public sealed class BotLog
	{
		private readonly string _prefix;

		public bool Verbose = true;

		/// <summary>(level, message)。BotReporter等が記録用に購読する。</summary>
		public event Action<string, string> MessageLogged;

		public BotLog(string prefix)
		{
			_prefix = "[" + prefix + "] ";
		}

		public void Info(string message)
		{
			MessageLogged?.Invoke("info", message);
			if (Verbose)
				Debug.Log(_prefix + message);
		}

		public void Warn(string message)
		{
			MessageLogged?.Invoke("warn", message);
			Debug.LogWarning(_prefix + message);
		}

		public void Error(string message)
		{
			MessageLogged?.Invoke("error", message);
			Debug.LogError(_prefix + message);
		}
	}
}
