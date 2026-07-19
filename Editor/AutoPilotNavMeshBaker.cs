using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoPilot.Editor
{
	/// <summary>
	/// 指定フォルダ配下のシーンへNavMeshを一括ベイクするツール。
	/// オブジェクトがNavigation Staticに設定されていないプロジェクトでも歩けるよう、
	/// 非トリガーCollider/Terrainを持つオブジェクトへ一時的にNavigation Staticを付与してベイクし、
	/// ベイク後にフラグを元へ戻してから保存する(シーン差分はNavMesh参照のみになる)。
	/// 対象フォルダは Tools > AutoPilot > Settings で設定(既定 Assets/Scenes)。
	/// </summary>
	public static class AutoPilotNavMeshBaker
	{
		[MenuItem("Tools/AutoPilot/Bake NavMesh For Active Scene")]
		private static void BakeActiveScene()
		{
			BakeScene(SceneManager.GetActiveScene().path);
		}

		[MenuItem("Tools/AutoPilot/Bake NavMesh For All Scenes In Folder")]
		public static void BakeAllScenesInFolder()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return;

			string folder = AutoPilotSettings.BakeScenesFolder;
			string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { folder });
			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				if (EditorUtility.DisplayCancelableProgressBar(
					"AutoPilot NavMesh Baker", path, (float)i / Mathf.Max(1, guids.Length)))
					break;
				BakeScene(path);
			}
			EditorUtility.ClearProgressBar();
		}

		private static void BakeScene(string scenePath)
		{
			if (string.IsNullOrEmpty(scenePath))
			{
				Debug.LogWarning("[AutoPilot] Active scene is not saved; cannot bake");
				return;
			}

			Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

			var modified = new List<KeyValuePair<GameObject, StaticEditorFlags>>();
			foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
			{
				if (!IsWalkableCandidate(go))
					continue;
				StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
				if ((flags & StaticEditorFlags.NavigationStatic) != 0)
					continue;
				modified.Add(new KeyValuePair<GameObject, StaticEditorFlags>(go, flags));
				GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.NavigationStatic);
			}

			try
			{
				NavMeshBuilder.BuildNavMesh();
			}
			finally
			{
				foreach (KeyValuePair<GameObject, StaticEditorFlags> entry in modified)
				{
					if (entry.Key != null)
						GameObjectUtility.SetStaticEditorFlags(entry.Key, entry.Value);
				}
			}

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log($"[AutoPilot] Baked NavMesh: {scenePath} (temporarily flagged {modified.Count} objects)");
		}

		private static bool IsWalkableCandidate(GameObject go)
		{
			Collider collider = go.GetComponent<Collider>();
			if (collider != null && !collider.isTrigger)
				return true;
			return go.GetComponent<Terrain>() != null;
		}
	}
}
