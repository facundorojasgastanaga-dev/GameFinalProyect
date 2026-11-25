using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronRequiem
{
    /// 24/11: replaces the template's full-screen pulsing critical-health vignette (FeedbackFlashHUD) with a message on
    /// one part of the screen that looks broken: a dark plate under the banners with procedural cracks (more with every
    /// hit while critical), an RGB-split title and short glitch bursts (slice jitter, flicker, dropout).
    /// Built at runtime under the game HUD canvas; nothing is saved in any scene. The per-hit damage flash is kept.
    public sealed class CriticalDamageMessage : MonoBehaviour
    {
        const string Title = "INTEGRIDAD CRÍTICA", Subtitle = "CASCO FRACTURADO · SISTEMAS DAÑADOS";
        static readonly Color TitleColor = new Color(1f, .36f, .2f), TextColor = new Color(.9f, .86f, .8f), PlateColor = new Color(.05f, .035f, .03f, .8f);

        Health health; CanvasGroup group; RectTransform root, body; CrackGraphic cracks; TMP_Text title, ghostRed, ghostCyan;
        Vector2 home; bool shown; float alpha, glitchUntil, nextGlitch, punch; System.Random random = new System.Random(11);

        static CriticalDamageMessage active; static int failures;
        /// The HUD can appear after the scene load (session flow): a tiny watcher retries once a second while no message
        /// exists, and stops searching as soon as one is attached.
        sealed class Watcher : MonoBehaviour
        {
            float next;
            void Update() { if (active || Time.unscaledTime < next) return; next = Time.unscaledTime + 1; TryAttach(); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<Watcher>()) return;
            var go = new GameObject("CriticalDamageMessageWatcher") { hideFlags = HideFlags.HideInHierarchy }; DontDestroyOnLoad(go); go.AddComponent<Watcher>();
        }

        static void TryAttach()
        {
            var player = FindAnyObjectByType<PlayerCharacterController>(); if (!player) return;
            var playerHealth = player.GetComponent<Health>(); if (!playerHealth) return;
            Canvas canvas = null;
            foreach (var mb in FindObjectsByType<MonoBehaviour>())
            {
                if (!mb || mb.GetType().Name != "FeedbackFlashHUD") continue;
                // the template's critical vignette is replaced by this message (the per-hit flash stays)
                var field = mb.GetType().GetField("CriticaHealthVignetteMaxAlpha"); if (field != null) field.SetValue(mb, 0f);
                var c = mb.GetComponentInParent<Canvas>(); if (c) canvas = c.rootCanvas;
            }
            // campaign sessions: the game HUD is the canvas holding the heat bar / mechanic HUD / screen edge
            if (!canvas) { var hud = (Component)FindAnyObjectByType<WeaponHeatHud>() ?? (Component)FindAnyObjectByType<MechanicHud>(); if (hud) { var c = hud.GetComponentInParent<Canvas>(); if (c) canvas = c.rootCanvas; } }
            if (!canvas) { var fx = FindAnyObjectByType<SlideGuardScreenFx>(); if (fx) { var c = fx.GetComponentInParent<Canvas>(); if (c) canvas = c.rootCanvas; } }
            if (!canvas)
            {
                if (failures++ < 3) Debug.Log("[CriticalDamageMessage] no game HUD canvas yet (player " + player.name + ", scene " + player.gameObject.scene.name + ")");
                return;
            }
            var existing = canvas.GetComponentInChildren<CriticalDamageMessage>(true); if (existing) { active = existing; return; }
            var font = canvas.GetComponentInChildren<TMP_Text>(true);
            var go = new GameObject("CriticalDamageMessage", typeof(RectTransform)); go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            active = go.AddComponent<CriticalDamageMessage>(); active.Build(playerHealth, font ? font.font : TMP_Settings.defaultFontAsset);
        }

        void Build(Health owner, TMP_FontAsset font)
        {
            health = owner; health.OnDamaged += OnDamaged;
            root = (RectTransform)transform; root.anchorMin = root.anchorMax = new Vector2(.5f, 1); root.pivot = new Vector2(.5f, 1);
            root.sizeDelta = new Vector2(560, 92); home = new Vector2(0, -205); root.anchoredPosition = home;
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            body = Rect("Body", root, Vector2.zero, Vector2.one);
            var plate = body.gameObject.AddComponent<Image>(); plate.color = PlateColor; plate.raycastTarget = false;
            Bar(body, 1); Bar(body, 0);
            title = Text(body, "Title", Title, font, 34, TitleColor, new Vector2(0, .42f), new Vector2(1, .95f));
            ghostRed = Text(body, "GhostRed", Title, font, 34, new Color(1, .1f, .1f, .55f), new Vector2(0, .42f), new Vector2(1, .95f));
            ghostCyan = Text(body, "GhostCyan", Title, font, 34, new Color(.2f, .9f, 1f, .45f), new Vector2(0, .42f), new Vector2(1, .95f));
            ghostRed.transform.SetSiblingIndex(title.transform.GetSiblingIndex()); ghostCyan.transform.SetSiblingIndex(title.transform.GetSiblingIndex());
            Text(body, "Subtitle", Subtitle, font, 16, TextColor, new Vector2(0, .08f), new Vector2(1, .42f));
            // the cracks run past the plate edges, like a broken glass panel
            var crackRect = Rect("Cracks", body, new Vector2(-.08f, -.35f), new Vector2(1.08f, 1.35f));
            cracks = crackRect.gameObject.AddComponent<CrackGraphic>(); cracks.color = new Color(1f, .9f, .82f, .8f); cracks.raycastTarget = false;
            SetGhosts(0);
        }
        void OnDestroy() { if (health) health.OnDamaged -= OnDamaged; }

        void OnDamaged(float amount, GameObject source)
        {
            if (!shown || !health || health.CurrentHealth <= 0) return;
            if (cracks.ImpactCount < 7) cracks.AddImpact(new Vector2(Random(.1f, .9f), Random(.15f, .85f)), .35f);
            Glitch(.28f); punch = 1;
        }

        void Update()
        {
            if (!health) { Destroy(gameObject); return; }
            bool critical = health.CurrentHealth > 0 && health.IsCritical();
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (critical && !shown) { shown = true; cracks.Clear(); cracks.AddImpact(new Vector2(.18f, .55f), .6f); cracks.AddImpact(new Vector2(.82f, .4f), .45f); Glitch(.45f); punch = 1; nextGlitch = now + Random(1.2f, 2.6f); }
            if (!critical && shown && alpha <= .01f) { shown = false; cracks.Clear(); }
            alpha = Mathf.MoveTowards(alpha, critical ? 1 : 0, dt * (critical ? 5f : 2.5f));
            if (alpha <= 0 && !critical) { group.alpha = 0; return; }
            if (critical && now >= nextGlitch) { Glitch(Random(.08f, .22f)); nextGlitch = now + Random(1.2f, 3f); }
            bool glitching = now < glitchUntil;
            // broken-display flicker: a steady weak pulse, hard dropouts while glitching
            float flicker = .9f + .1f * Mathf.Sin(now * 12.6f);
            if (glitching && random.NextDouble() < .35) flicker *= Random(.15f, .6f);
            group.alpha = alpha * flicker;
            punch = Mathf.MoveTowards(punch, 0, dt * 4f);
            float s = 1 + .07f * punch * punch; body.localScale = new Vector3(s, s, 1);
            root.anchoredPosition = home + (glitching ? new Vector2(Random(-6, 6), Random(-2, 2)) : Vector2.zero);
            SetGhosts(glitching ? Random(3, 9) : 1.5f);
        }

        void Glitch(float seconds) => glitchUntil = Mathf.Max(glitchUntil, Time.unscaledTime + seconds);
        void SetGhosts(float offset)
        {
            ghostRed.rectTransform.anchoredPosition = new Vector2(-offset, offset * .25f);
            ghostCyan.rectTransform.anchoredPosition = new Vector2(offset, -offset * .25f);
        }
        float Random(float a, float b) => a + (float)random.NextDouble() * (b - a);

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform; r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }
        static void Bar(RectTransform parent, float y)
        {
            var r = Rect(y > .5f ? "TopBar" : "BottomBar", parent, new Vector2(0, y), new Vector2(1, y)); r.sizeDelta = new Vector2(0, 3);
            var img = r.gameObject.AddComponent<Image>(); img.color = TitleColor; img.raycastTarget = false;
        }
        static TMP_Text Text(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Color color, Vector2 min, Vector2 max)
        {
            var r = Rect(name, parent, min, max); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = color; t.characterSpacing = 6;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; return t;
        }
    }
}
