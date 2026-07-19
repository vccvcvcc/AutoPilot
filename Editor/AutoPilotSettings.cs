using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AutoPilot.Editor
{
	/// <summary>
	/// 自律ループのプロジェクト固有設定。パッケージにアセットを同梱しないよう、
	/// ScriptableObjectではなくEditorPrefsに保存する(マシンローカル設定)。
	/// </summary>
	public static class AutoPilotSettings
	{
		private const string StartupSceneKey = "AutoPilot.Bridge.StartupScene";
		private const string BakeFolderKey = "AutoPilot.Bake.Folder";

		/// <summary>
		/// runコマンドでPlayを開始する前に必ず開くシーンのパス(例: Assets/Scenes/Boot.unity)。
		/// 空なら現在開いているシーンのままPlayする。
		/// </summary>
		public static string StartupScenePath
		{
			get => EditorPrefs.GetString(StartupSceneKey, "");
			set => EditorPrefs.SetString(StartupSceneKey, value);
		}

		/// <summary>NavMesh一括ベイクの対象シーンフォルダ。</summary>
		public static string BakeScenesFolder
		{
			get => EditorPrefs.GetString(BakeFolderKey, "Assets/Scenes");
			set => EditorPrefs.SetString(BakeFolderKey, value);
		}

		[MenuItem("Tools/AutoPilot/Settings/Use Active Scene As Startup")]
		private static void UseActiveSceneAsStartup()
		{
			string path = EditorSceneManager.GetActiveScene().path;
			if (string.IsNullOrEmpty(path))
			{
				EditorUtility.DisplayDialog("AutoPilot", "アクティブシーンが未保存です。先に保存してください。", "OK");
				return;
			}
			StartupScenePath = path;
			Debug.Log($"[AutoPilot] Startup scene set to: {path}");
		}

		[MenuItem("Tools/AutoPilot/Settings/Clear Startup Scene (use current)")]
		private static void ClearStartupScene()
		{
			StartupScenePath = "";
			Debug.Log("[AutoPilot] Startup scene cleared; runs will play the currently open scene.");
		}

		[MenuItem("Tools/AutoPilot/Settings/Print Current Settings")]
		private static void PrintSettings()
		{
			Debug.Log($"[AutoPilot] StartupScene='{StartupScenePath}'  BakeFolder='{BakeScenesFolder}'");
		}
	}
}
