using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronRequiem.EditorTools
{
    /// Read-only dump of every screen-space UI hierarchy (IntroMenu + campaign session) for the UI style pass.
    public static class UiProbe
    {
        [MenuItem("Tools/Iron Requiem/Menu/Probe UI hierarchy (read-only)")]
        static void Probe()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[UiProbe] stop play mode"); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning("[UiProbe] unsaved scene"); return; }
            var sb = new StringBuilder();
            foreach (var path in new[] { "Assets/IronRequiem/Scenes/RequiemCampaign.unity", "Assets/FPS/Scenes/IntroMenu.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine("##### " + path);
                foreach (var canvas in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
                {
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    sb.AppendLine($"=== Canvas {Path(canvas.transform)} mode={canvas.renderMode} sort={canvas.sortingOrder} active={canvas.gameObject.activeInHierarchy}" +
                        (scaler ? $" scaler={scaler.uiScaleMode} ref={scaler.referenceResolution} match={scaler.matchWidthOrHeight}" : " (no scaler)"));
                    Dump(canvas.transform, 0, sb);
                }
            }
            Directory.CreateDirectory("Tools/IronRequiem/Reports/HangarIntro");
            File.WriteAllText("Tools/IronRequiem/Reports/HangarIntro/ui_probe.txt", sb.ToString());
            Debug.Log("[UiProbe] wrote ui_probe.txt (" + sb.Length + " chars)");
        }
        static string Path(Transform t) { var p = t.name; while (t.parent) { t = t.parent; p = t.name + "/" + p; } return p; }
        static void Dump(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 9) return;
            var r = t as RectTransform; var line = new StringBuilder(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " [off]"));
            if (r) line.Append($" a={r.anchorMin}-{r.anchorMax} p={r.anchoredPosition} s={r.sizeDelta} sc={r.localScale.x:0.##}");
            foreach (var c in t.GetComponents<Component>())
            {
                switch (c)
                {
                    case RectTransform _: break;
                    case Image img: line.Append($" [Image {(img.sprite ? img.sprite.name : "-")} {ColorUtility.ToHtmlStringRGBA(img.color)} {img.type}]"); break;
                    case RawImage _: line.Append(" [RawImage]"); break;
                    case TMP_Text tx: line.Append($" [TMP '{tx.text.Replace("\n", "\\n")}' {(tx.font ? tx.font.name : "-")} {tx.fontSize} {ColorUtility.ToHtmlStringRGBA(tx.color)}]"); break;
                    case Text ut: line.Append($" [Text '{ut.text}']"); break;
                    case Button _: line.Append(" [Button]"); break;
                    case CanvasRenderer _: break;
                    default: line.Append(" [" + c.GetType().Name + "]"); break;
                }
            }
            sb.AppendLine(line.ToString());
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }
    }
}
