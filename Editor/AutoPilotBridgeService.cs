using System;
using System.Globalization;
using System.IO;
using AutoPilot.Core;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace AutoPilot.Editor
{
	[Serializable]
	internal class BridgeCommand
	{
		public string id;
		public string action;           // "run" | "stop" | "bake"
		public int durationSeconds = 180;
	}

	[Serializable]
	internal class BridgeStatus
	{
		public string state;            // bridge-loaded / refreshing / compile-error / playing / stopping / done / baking
		public string commandId;
		public string message;
		public string reportPath;
		public string utc;
	}

	/// <summary>
	/// 外部プロセス(CLIエージェント)からUnityエディタを操作するためのファイルベースブリッジ。
	///
	///   &lt;projectRoot&gt;/AutoPilotBridge/command.json … 外部が書くコマンド
	///   &lt;projectRoot&gt;/AutoPilotBridge/status.json  … ブリッジが書く現在状態
	///   &lt;projectRoot&gt;/AutoPilotBridge/Reports/     … ランタイムのBotReporterが書くレポート
	///
	/// runコマンド: AssetDatabase.Refresh → コンパイル完了待ち
	///   → エラーあり: status=compile-error(全文付き)で終了
	///   → エラーなし: AutoPilot有効化してPlay開始 → durationSeconds経過でPlay停止 → status=done
	///
	/// Playモード出入りのドメインリロードで静的状態が消えるため、状態機械はSessionStateに置く。
	/// このクラスはゲーム非依存: 有効化フラグ/セッションキーは AutoPilotHandshake、
	/// 起動シーン/ベイク対象は AutoPilotSettings を参照する。
	/// </summary>
	[InitializeOnLoad]
	public static class AutoPilotBridgeService
	{
		private const string StateKey = "AutoPilot.Bridge.State";
		private const string EndAtKey = "AutoPilot.Bridge.EndAtUtc";
		private const string ErrorsKey = "AutoPilot.Bridge.CompileErrors";
		private const string LastProcessedKey = "AutoPilot.Bridge.LastProcessedId";

		private static double _nextPollAt;

		static AutoPilotBridgeService()
		{
			EditorApplication.update += Poll;
			CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;

			// リロード直後はSessionStateがまだ読めないことがあるため、delayCallで1フレーム遅らせて
			// 生存通知する(Play突入時のリロードで "playing" を誤って上書きしないように)。
			EditorApplication.delayCall += () =>
			{
				if (SessionState.GetString(StateKey, "idle") == "idle")
					WriteStatus("bridge-loaded", "bridge is alive");
			};
		}

		private static string BridgeDir =>
			Path.GetFullPath(Path.Combine(Application.dataPath, "..", AutoPilotHandshake.BridgeDirName));

		private static string CommandPath => Path.Combine(BridgeDir, "command.json");
		private static string StatusPath => Path.Combine(BridgeDir, "status.json");

		private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
		{
			foreach (CompilerMessage message in messages)
			{
				if (message.type == CompilerMessageType.Error)
				{
					string collected = SessionState.GetString(ErrorsKey, "");
					SessionState.SetString(ErrorsKey, collected + message.message + "\n");
				}
			}
		}

		private static void Poll()
		{
			if (EditorApplication.timeSinceStartup < _nextPollAt)
				return;
			_nextPollAt = EditorApplication.timeSinceStartup + 0.5;

			string state = SessionState.GetString(StateKey, "idle");
			switch (state)
			{
				case "idle":
					CheckForCommand();
					break;

				case "waitCompile":
					if (EditorApplication.isCompiling || EditorApplication.isUpdating)
						return;
					string errors = SessionState.GetString(ErrorsKey, "");
					if (!string.IsNullOrEmpty(errors))
					{
						SessionState.SetString(StateKey, "idle");
						WriteStatus("compile-error", errors);
						return;
					}
					StartPlay();
					break;

				case "play":
					if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
					{
						SessionState.SetString(StateKey, "idle");
						WriteStatus("done", "play mode ended early");
						return;
					}
					if (DateTime.UtcNow >= ReadEndAt())
					{
						EditorApplication.ExitPlaymode();
						SessionState.SetString(StateKey, "finalize");
						WriteStatus("stopping", "duration elapsed");
					}
					break;

				case "finalize":
					if (EditorApplication.isPlaying)
						return;
					SessionState.SetString(StateKey, "idle");
					WriteStatus("done", "report ready");
					break;
			}
		}

		private static void CheckForCommand()
		{
			if (!File.Exists(CommandPath))
				return;

			BridgeCommand command;
			try
			{
				command = JsonUtility.FromJson<BridgeCommand>(File.ReadAllText(CommandPath));
			}
			catch (Exception)
			{
				return; // 書き込み途中などの不完全なJSONは次のPollで再試行
			}

			if (command == null || string.IsNullOrEmpty(command.id))
				return;
			if (command.id == SessionState.GetString(LastProcessedKey, ""))
				return;

			SessionState.SetString(LastProcessedKey, command.id);

			if (command.action == "run")
			{
				SessionState.SetString(AutoPilotHandshake.CommandIdSessionKey, command.id);
				SessionState.SetString(ErrorsKey, "");
				SessionState.SetString(EndAtKey,
					DateTime.UtcNow.AddSeconds(Mathf.Max(30, command.durationSeconds)).ToString("o"));
				SessionState.SetString(StateKey, "waitCompile");
				WriteStatus("refreshing", "importing & compiling");
				AssetDatabase.Refresh();
			}
			else if (command.action == "stop")
			{
				if (EditorApplication.isPlaying)
					EditorApplication.ExitPlaymode();
				SessionState.SetString(StateKey, "idle");
				WriteStatus("done", "stopped by command");
			}
			else if (command.action == "bake")
			{
				WriteStatus("baking", "baking NavMesh for all scenes in configured folder (editor will block)");
				try
				{
					AutoPilotNavMeshBaker.BakeAllScenesInFolder();
					WriteStatus("done", "bake finished");
				}
				catch (Exception e)
				{
					WriteStatus("done", "bake failed: " + e.Message);
				}
			}
		}

		private static void StartPlay()
		{
			// 起動シーンが設定されていれば必ずそこからPlayする(ベイク等で別シーンが開いていても安全)
			string startupScene = AutoPilotSettings.StartupScenePath;
			if (!string.IsNullOrEmpty(startupScene) && File.Exists(startupScene))
			{
				UnityEngine.SceneManagement.Scene active =
					UnityEngine.SceneManagement.SceneManager.GetActiveScene();
				if (active.path != startupScene)
					UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
						startupScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
			}

			EditorPrefs.SetBool(AutoPilotHandshake.EnableOnPlayPrefKey, true);
			SessionState.SetString(StateKey, "play");
			WriteStatus("playing", "entering play mode");
			EditorApplication.EnterPlaymode();
		}

		private static DateTime ReadEndAt()
		{
			string raw = SessionState.GetString(EndAtKey, "");
			return DateTime.TryParse(raw, null, DateTimeStyles.RoundtripKind, out DateTime endAt)
				? endAt
				: DateTime.UtcNow;
		}

		private static void WriteStatus(string state, string message)
		{
			try
			{
				Directory.CreateDirectory(BridgeDir);
				string commandId = SessionState.GetString(AutoPilotHandshake.CommandIdSessionKey, "");
				var status = new BridgeStatus
				{
					state = state,
					commandId = commandId,
					message = message,
					reportPath = string.IsNullOrEmpty(commandId)
						? ""
						: Path.Combine(BridgeDir, "Reports", $"report_{commandId}.json").Replace('\\', '/'),
					utc = DateTime.UtcNow.ToString("o"),
				};
				File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
			}
			catch (Exception)
			{
				// status書き込み失敗でエディタを巻き込まない
			}
		}
	}
}
