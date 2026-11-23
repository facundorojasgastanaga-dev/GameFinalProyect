using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem.EditorTools
{
    /// Play-only, read-only helpers for the menu style pass: dump the live UI hierarchy and open the menu screens.
    static class MenuRuntimeProbe
    {
        const string Menu = "Tools/Iron Requiem/Menu/Runtime/";

        [MenuItem(Menu + "Dump live UI")]
        static void Dump()
        {
            if (!EditorApplication.isPlaying) return;
            var sb = new StringBuilder();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Where(c => c.isRootCanvas))
            {
                sb.AppendLine($"=== Canvas {canvas.name} mode={canvas.renderMode} sort={canvas.sortingOrder} active={canvas.gameObject.activeInHierarchy}");
                Walk(canvas.transform, 0, sb);
            }
            Directory.CreateDirectory("Tools/IronRequiem/Reports/MenuStyle");
            File.WriteAllText("Tools/IronRequiem/Reports/MenuStyle/live_ui.txt", sb.ToString());
            Debug.Log("[MenuProbe] dumped " + sb.Length);
        }

        static void Walk(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 11) return;
            var r = t as RectTransform; var line = new StringBuilder(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " [off]"));
            if (r) line.Append($" a={r.anchorMin}-{r.anchorMax} p={r.anchoredPosition} s={r.sizeDelta} sc={r.localScale.x:0.##}");
            foreach (var c in t.GetComponents<Component>())
            {
                switch (c)
                {
                    case RectTransform _: case CanvasRenderer _: break;
                    case Image img: line.Append($" [Image {(img.sprite ? img.sprite.name : "-")} {ColorUtility.ToHtmlStringRGBA(img.color)} {img.type}{(img.enabled ? "" : " disabled")}]"); break;
                    case TMP_Text tx: line.Append($" [TMP '{tx.text.Replace("\n", "\\n")}' {(tx.font ? tx.font.name : "-")} {tx.fontSize} {ColorUtility.ToHtmlStringRGBA(tx.color)} auto={tx.enableAutoSizing} wrap={tx.textWrappingMode} ovf={tx.overflowMode}]"); break;
                    default: line.Append(" [" + c.GetType().Name + "]"); break;
                }
            }
            sb.AppendLine(line.ToString());
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), depth + 1, sb);
        }

        static void Call(string method)
        {
            if (!EditorApplication.isPlaying) return;
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (m.GetType().Name == "RequiemMenuView") m.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(m, null);
        }
        [MenuItem(Menu + "Load Map01 (Play only)")]
        static void LoadMap()
        {
            if (!EditorApplication.isPlaying) return;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        }
        [MenuItem(Menu + "Open pause")]
        static void OpenPause()
        {
            if (!EditorApplication.isPlaying) return;
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (m.GetType().Name == "InGameMenuManager" && m.isActiveAndEnabled && (bool)(m.GetType().GetField("UseIndustrialMenu")?.GetValue(m) ?? false))
                    m.GetType().GetMethod("SetPauseMenuActivation")?.Invoke(m, new object[] { true });
        }

        // Game-view captures of the wake-up cinematic at fixed cinematic times (Tools/IronRequiem/Bridge/intro_tX.XX.png).
        static readonly float[] IntroMarks = { 1.5f, 2.9f, 3.3f, 3.55f, 3.86f, 4.03f, 4.25f, 4.9f, 5.42f, 5.62f, 5.72f, 6.0f, 15.2f, 16.2f, 17.0f };
        static int introNext;
        [MenuItem(Menu + "Capture wake-up cinematic")]
        static void CaptureIntro()
        {
            if (!EditorApplication.isPlaying) return;
            introNext = 0; EditorApplication.update -= IntroTick; EditorApplication.update += IntroTick; Debug.Log("[MenuProbe] intro capture armed");
        }
        static void IntroTick()
        {
            if (!EditorApplication.isPlaying || introNext >= IntroMarks.Length) { EditorApplication.update -= IntroTick; return; }
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); if (!cine || !cine.IsPlaying) return;
            var director = cine.GetComponent<UnityEngine.Playables.PlayableDirector>(); if (!director) director = Object.FindAnyObjectByType<UnityEngine.Playables.PlayableDirector>();
            if (!director || director.time < IntroMarks[introNext]) return;
            ScreenCapture.CaptureScreenshot($"Tools/IronRequiem/Bridge/intro_t{IntroMarks[introNext].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}.png");
            Debug.Log($"[MenuProbe] intro capture t={director.time:0.00} wakeAt={cine.WakeAt:0.00}"); introNext++;
        }

        [MenuItem(Menu + "Open options")] static void Options() => Call("OpenOptions");
        [MenuItem(Menu + "Open credits")] static void Credits() => Call("OpenCredits");
        [MenuItem(Menu + "Show home")] static void Home() => Call("ShowHome");
    }
}
