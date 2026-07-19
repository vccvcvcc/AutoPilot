using AutoPilot.Core;
using UnityEditor;

namespace AutoPilot.Editor
{
	/// <summary>
	/// Play時にAutoPilotを起動するかどうかのトグル(EditorPrefs)。
	/// ゲーム側ブートは AutoPilotHandshake.EnableOnPlayPrefKey を読んで自分を起動する。
	/// </summary>
	public static class AutoPilotRunControl
	{
		private const string MenuPath = "Tools/AutoPilot/Enable On Play";

		[MenuItem(MenuPath)]
		private static void Toggle()
		{
			bool enabled = !EditorPrefs.GetBool(AutoPilotHandshake.EnableOnPlayPrefKey, false);
			EditorPrefs.SetBool(AutoPilotHandshake.EnableOnPlayPrefKey, enabled);
		}

		[MenuItem(MenuPath, true)]
		private static bool Validate()
		{
			Menu.SetChecked(MenuPath, EditorPrefs.GetBool(AutoPilotHandshake.EnableOnPlayPrefKey, false));
			return true;
		}
	}
}
