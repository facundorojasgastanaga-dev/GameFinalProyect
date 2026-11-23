using TMPro;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Adds a CONTROLES button next to VOLVER in the options screen of the pause menu and of the main menu (IntroMenu;
    /// the user approved it). The scene itself is not edited: the button is cloned at run time.
    /// RequiemMenuView lives in fps.UI and cannot see this assembly, so the hook is installed here when a scene loads.
    static class ControlsPanelInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded;
            Install();
        }
        static void OnLoaded(Scene scene, LoadSceneMode mode) => Install();

        // The pause menu and the player can appear after the scene loads (cinematics, spawned UI): keep trying until the
        // button exists, at a low rate.
        sealed class Retry : MonoBehaviour
        {
            float next;
            void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + .75f;
                Install();
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartRetry()
        {
            if (Object.FindAnyObjectByType<Retry>()) return;
            var go = new GameObject("IR_ControlsPanelInstaller") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go); go.AddComponent<Retry>();
        }

        public static void Install()
        {
            // Any options console (pause menu or main menu) is recognised by its FullscreenLabel toggle.
            foreach (var toggle in Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include))
            {
                if (!toggle.transform.Find("FullscreenLabel")) continue;
                for (var node = toggle.transform.parent; node; node = node.parent)
                {
                    Button back = null;
                    foreach (var b in node.GetComponentsInChildren<Button>(true))
                    {
                        var label = b.GetComponentInChildren<TMP_Text>(true);
                        if (label && label.text.Trim().ToUpperInvariant() == "VOLVER") { back = b; break; }
                    }
                    if (!back) continue;
                    if (back.transform.parent.Find("ControlsButton")) break;
                    var clone = Object.Instantiate(back.gameObject, back.transform.parent);
                    clone.name = "ControlsButton";
                    clone.transform.SetSiblingIndex(back.transform.GetSiblingIndex());
                    SplitSideBySide((RectTransform)back.transform, (RectTransform)clone.transform);
                    var cloneLabel = clone.GetComponentInChildren<TMP_Text>(true);
                    cloneLabel.text = "CONTROLES";
                    // Each button now has half the width: let the labels shrink to fit instead of overflowing.
                    foreach (var l in new[] { cloneLabel, back.GetComponentInChildren<TMP_Text>(true) })
                    {
                        l.enableAutoSizing = true; l.fontSizeMax = l.fontSize; l.fontSizeMin = 10; l.textWrappingMode = TextWrappingModes.NoWrap;
                    }
                    var button = clone.GetComponent<Button>();
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(ControlsPanel.Open);
                    break;
                }
            }
        }

        // Absolutely positioned consoles: VOLVER keeps the left half of its slot, CONTROLES takes the right half.
        static void SplitSideBySide(RectTransform original, RectTransform clone)
        {
            if (original.parent && original.parent.GetComponent<LayoutGroup>()) return; // a layout group places the sibling itself
            if (original.anchorMin != original.anchorMax) return;
            float w = original.rect.width, half = (w - 16f) / 2f, left = original.anchoredPosition.x - original.pivot.x * w;
            original.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, half);
            clone.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, half);
            original.anchoredPosition = new Vector2(left + original.pivot.x * half, original.anchoredPosition.y);
            clone.anchoredPosition = new Vector2(left + half + 16f + clone.pivot.x * half, original.anchoredPosition.y);
        }
    }
}
