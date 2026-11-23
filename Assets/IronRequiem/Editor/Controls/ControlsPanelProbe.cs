using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem.Editor
{
    /// Play-Mode helpers for the controls panel captures (docs only; never edits scenes or assets).
    static class ControlsPanelProbe
    {
        const string Menu = "Tools/Iron Requiem/Controls/Probe/";
        static bool Guard() { if (!Application.isPlaying) { Debug.LogWarning("Controls probe needs Play Mode"); return false; } return true; }
        [MenuItem(Menu + "Panel list")] static void List() { if (Guard()) ControlsPanel.Preview("list"); }
        [MenuItem(Menu + "Panel waiting key")] static void Wait() { if (Guard()) ControlsPanel.Preview("wait"); }
        [MenuItem(Menu + "Panel conflict")] static void Conflict() { if (Guard()) ControlsPanel.Preview("conflict"); }
        [MenuItem(Menu + "Panel reset")] static void Reset() { if (Guard()) ControlsPanel.Preview("reset"); }
        [MenuItem(Menu + "Panel gamepad")] static void Pad() { if (Guard()) ControlsPanel.Preview("gamepad"); }
        [MenuItem(Menu + "Panel settings")] static void Settings() { if (Guard()) ControlsPanel.Preview("settings"); }
        [MenuItem(Menu + "Close panel")] static void Close() { if (Guard()) ControlsPanel.Close(); }
        [MenuItem(Menu + "Rebind Melee to V (HUD capture)")]
        static void MeleeV()
        {
            if (!Guard()) return;
            var a = InputSystem.actions.FindAction("Player/Melee", true);
            ControlsRebinder.Assign(InputSystem.actions, a, ControlsRebinder.SlotIndex(a, "Keyboard&Mouse", null, 0), "<Keyboard>/v");
        }
        [MenuItem(Menu + "Open pause options")]
        static void PauseOptions()
        {
            if (!Guard()) return;
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                var n = m.GetType().Name;
                if (n == "InGameMenuManager") m.GetType().GetMethod("SetPauseMenuActivation")?.Invoke(m, new object[] { true });
            }
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (m.GetType().Name == "RequiemMenuView") { m.GetType().GetMethod("OpenOptions")?.Invoke(m, null); Debug.Log("[PauseProbe] options opened"); }
        }
        [MenuItem(Menu + "Load IntroMenu (Play only)")]
        static void LoadIntro()
        {
            if (!Guard()) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene("IntroMenu");
        }
        [MenuItem(Menu + "Open main menu options")]
        static void MainOptions()
        {
            if (!Guard()) return;
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (m.GetType().Name == "RequiemMenuView") { m.GetType().GetMethod("OpenOptions")?.Invoke(m, null); Debug.Log("[MainProbe] options opened"); }
        }
        [MenuItem(Menu + "Dump Melee bindings")]
        static void DumpMelee()
        {
            if (!Guard()) return;
            var a = InputSystem.actions.FindAction("Player/Melee", true);
            for (int i = 0; i < a.bindings.Count; i++) Debug.Log("[MeleeDump] " + i + " path='" + a.bindings[i].path + "' override='" + a.bindings[i].overridePath + "' groups='" + a.bindings[i].groups + "' display='" + ControlsRebinder.Display(a, i) + "'");
        }
        [MenuItem(Menu + "Reset all controls")] static void ResetAll() { if (Guard()) ControlsRebinder.ResetAll(InputSystem.actions); }
    }
}
