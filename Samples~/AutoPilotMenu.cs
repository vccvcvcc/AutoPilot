#if UNITY_EDITOR
using UnityEditor;
using AutoPilot.Core;

namespace AutoPilot.Adapter
{
    public static class AutoPilotMenu
    {
        [MenuItem("Tools/AutoPilot/Enable On Play", false, 10)]
        public static void ToggleEnableOnPlay()
        {
            bool enabled = !EditorPrefs.GetBool(AutoPilotHandshake.EnableOnPlayPrefKey, false);
            EditorPrefs.SetBool(AutoPilotHandshake.EnableOnPlayPrefKey, enabled);
            Menu.SetChecked("Tools/AutoPilot/Enable On Play", enabled);
            ShowNotification(enabled ? "AutoPilot enabled on play" : "AutoPilot disabled on play");
        }

        [MenuItem("Tools/AutoPilot/Enable On Play", true)]
        public static bool ValidateToggleEnableOnPlay()
        {
            bool enabled = EditorPrefs.GetBool(AutoPilotHandshake.EnableOnPlayPrefKey, false);
            Menu.SetChecked("Tools/AutoPilot/Enable On Play", enabled);
            return true;
        }

        private static void ShowNotification(string message)
        {
            EditorApplication.delayCall += () => EditorUtility.DisplayDialog("AutoPilot", message, "OK");
        }
    }
}
#endif
