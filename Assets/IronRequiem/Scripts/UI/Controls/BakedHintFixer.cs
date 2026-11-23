using System.Collections.Generic;
using TMPro;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    /// Plates baked into scenes (the CONTROLES plate of the options console, for example) spell the default keys in rich
    /// text: "<color=#FF9933>[F]</color> GOLPE". Editing those scenes is blocked while they have unsaved user changes, so the
    /// legacy tokens are rewritten at run time from the live bindings instead. Only texts that carry that orange rich-text
    /// marker are touched, so texts already built from ControlHints (HUD, prompts) are never rewritten a second time.
    /// The main menu is included since 20/11 (its options console shows the same plate).
    static class BakedHintFixer
    {
        const string Marker = "<color=#FF9933>[";
        static readonly Dictionary<TMP_Text, string> originals = new Dictionary<TMP_Text, string>();
        static int appliedVersion = -1;

        sealed class Poller : MonoBehaviour
        {
            float next;
            void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + .75f;
                Scan();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            originals.Clear(); appliedVersion = -1;
            if (Object.FindAnyObjectByType<Poller>()) return;
            var go = new GameObject("IR_BakedHintFixer") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go); go.AddComponent<Poller>();
        }

        static void Scan()
        {
            bool versionChanged = appliedVersion != ControlHints.Version;
            appliedVersion = ControlHints.Version;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                if (!text) continue;
                if (!originals.TryGetValue(text, out var original))
                {
                    if (string.IsNullOrEmpty(text.text) || !text.text.Contains(Marker)) continue;
                    originals[text] = original = text.text;
                    text.text = ControlHints.ResolveLegacy(original);
                }
                else if (versionChanged) text.text = ControlHints.ResolveLegacy(original);
            }
        }
    }
}
