using System;
using System.IO;
using AutoPilot.Core;
using AutoPilot.InputSim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoPilot.Adapter
{
	public static class SmokeTestAdapter
	{
		private static BotRunner _runner;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Bootstrap()
		{
			if (_runner != null || !IsEnabled())
				return;

			Application.runInBackground = true;
			_runner = BotRunner.Create(new VirtualGamepad(), "AutoPilot");
			_runner.Context.Blackboard.Set(Keys.Phase, NormalizedPhase.Boot);
			_runner.Context.Blackboard.Set(Keys.PlayerExists, true);
			_runner.Context.Blackboard.Set(Keys.PlayerPosition, Vector3.zero);
			_runner.Context.Log.Info("AutoPilot booted for smoke test");

			_runner.Sensors.Add(new SmokeTestPhaseSensor());
			_runner.Sensors.Add(new SmokeTestMovementSensor());
			string sessionId = GetSessionId();
			string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AutoPilotHandshake.BridgeDirName, "Reports", $"report_{sessionId}.json"));
			_runner.Sensors.Add(new BotReporter(reportPath, sessionId, _runner.Context.Log));
			_runner.Director.SetHandler(NormalizedPhase.Title, () => Tasks.Sequence(
				Tasks.Do(ctx => { ctx.Activity = "waiting for title"; }),
				Tasks.WaitSeconds(1f)
			));
			_runner.Director.SetHandler(NormalizedPhase.Playing, () => Tasks.Sequence(
				Tasks.Do(ctx => { ctx.Activity = "test play loop"; ctx.Controller?.NeutralAll(); }),
				Tasks.WaitSeconds(0.25f)
			));
			_runner.Director.SetHandler(NormalizedPhase.Loading, () => Tasks.Sequence(
				Tasks.Do(ctx => { ctx.Activity = "loading"; }),
				Tasks.WaitSeconds(0.5f)
			));
			_runner.Director.SetHandler(NormalizedPhase.Dialogue, () => Tasks.Sequence(
				Tasks.Do(ctx => { ctx.Activity = "dialogue"; ctx.Controller?.Press(BotButton.South); }),
				Tasks.WaitSeconds(0.25f)
			));
			_runner.Director.SetHandler(NormalizedPhase.Menu, () => Tasks.Sequence(
				Tasks.Do(ctx => { ctx.Activity = "menu"; ctx.Controller?.Press(BotButton.East); }),
				Tasks.WaitSeconds(0.25f)
			));
			_runner.Director.SetHandler(NormalizedPhase.Result, () => Tasks.Sequence(
				Tasks.Do(ctx => { ctx.Activity = "result"; }),
				Tasks.WaitSeconds(0.5f)
			));
			SceneManager.sceneLoaded += (_, __) => _runner.Context.Blackboard.Set(Keys.Phase, NormalizedPhase.Loading);
		}

		private static bool IsEnabled()
		{
			foreach (var arg in Environment.GetCommandLineArgs())
				if (arg == "-autopilot")
					return true;
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(AutoPilotHandshake.EnableOnPlayPrefKey, false);
#else
			return false;
#endif
		}

		private static string GetSessionId()
		{
#if UNITY_EDITOR
            string id = UnityEditor.SessionState.GetString(AutoPilotHandshake.CommandIdSessionKey, "");
            if (!string.IsNullOrEmpty(id)) return id;
#endif
			return DateTime.Now.ToString("yyyyMMdd_HHmmss");
		}
	}

	public sealed class SmokeTestPhaseSensor : ISensor
	{
		public void Tick(Blackboard blackboard)
		{
			var sceneName = SceneManager.GetActiveScene().name;
			if (sceneName.Contains("MainMenu") || sceneName.Contains("Title"))
				blackboard.Set(Keys.Phase, NormalizedPhase.Title);
			else if (sceneName.Contains("Loading"))
				blackboard.Set(Keys.Phase, NormalizedPhase.Loading);
			else if (sceneName.Contains("Menu"))
				blackboard.Set(Keys.Phase, NormalizedPhase.Menu);
			else
				blackboard.Set(Keys.Phase, NormalizedPhase.Playing);
		}
	}

	public sealed class SmokeTestMovementSensor : ISensor
	{
		public void Tick(Blackboard blackboard)
		{
			var phase = blackboard.Get(Keys.Phase, NormalizedPhase.Unknown);
			if (phase == NormalizedPhase.Playing)
			{
				blackboard.Set(Keys.PlayerExists, true);
				blackboard.Set(Keys.PlayerPosition, Vector3.zero);
			}
			blackboard.Set(Keys.Activity, "monitoring");
		}
	}
}
