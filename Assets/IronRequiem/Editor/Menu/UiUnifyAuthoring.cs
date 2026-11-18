using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// v5 (13/11/2026) — one UI style for every screen. Creates the shared theme asset (palette, fonts, plate sprites,
    /// interface sounds) and applies it, idempotently, to the main menu, options, credits, pause, death screen, round
    /// and alert banners, contextual prompts, the cinematic skip prompt and the HUD of the campaign session and the
    /// prologue scenes. It also turns the options console into a large, centred overlay (about 76 % of the screen
    /// height, with a dimmed backdrop, a big VOLVER button and a CONTROLES plate) in the main menu and the pause menu.
    /// Presentation only: menu logic, listeners and RequiemSettings persistence are untouched.
    public static class UiUnifyAuthoring
    {
        public const string ThemePath = "Assets/IronRequiem/Settings/UI/IR_UiTheme.asset";
        const string Intro = "Assets/FPS/Scenes/IntroMenu.unity";
        const string Session = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string Hangar = "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity";
        const string Corridor = "Assets/IronRequiem/Scenes/Campaign/Room00b_AlarmCorridor.unity";
        const string UiDir = "Assets/IronRequiem/Art/Menu/UI";
        static readonly Vector2 ConsoleSize = new Vector2(1060, 520);
        static StringBuilder log;

        [MenuItem("Tools/Iron Requiem/Menu/Unify UI style (all screens)")]
        public static void Build()
        {
            log = new StringBuilder("UI unify " + DateTime.Now + "\n");
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene open: " + SceneManager.GetSceneAt(i).path);
                var scenes = new[] { Session, Hangar, Corridor, Intro }.Where(File.Exists).ToArray(); // intro last: it stays open
                var backup = "Tools/IronRequiem/Backups/" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "-ui-unify";
                Directory.CreateDirectory(backup);
                foreach (var f in scenes) { File.Copy(f, Path.Combine(backup, Path.GetFileName(f)), true); File.Copy(f + ".meta", Path.Combine(backup, Path.GetFileName(f) + ".meta"), true); }
                if (File.Exists(ThemePath)) { File.Copy(ThemePath, Path.Combine(backup, "IR_UiTheme.asset"), true); }
                log.AppendLine("backup " + backup);
                var theme = EnsureTheme(log);
                foreach (var path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Apply(scene, theme, log);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[UiUnify] ok\n" + log);
            }
            catch (Exception e) { Debug.LogError("[UiUnify] " + e); log.AppendLine("ERROR " + e); }
            Directory.CreateDirectory("Tools/IronRequiem/Reports/HangarIntro");
            File.WriteAllText("Tools/IronRequiem/Reports/HangarIntro/ui_unify.txt", log.ToString());
        }

        // ------------------------------------------------------------------ theme
        static T Find<T>(string filter, string exactName) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid); var a = AssetDatabase.LoadAssetAtPath<T>(p);
                if (a && a.name == exactName) return a;
            }
            return null;
        }
        public static IronRequiemUiTheme EnsureTheme(StringBuilder sb)
        {
            var theme = AssetDatabase.LoadAssetAtPath<IronRequiemUiTheme>(ThemePath);
            if (!theme)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                theme = ScriptableObject.CreateInstance<IronRequiemUiTheme>(); AssetDatabase.CreateAsset(theme, ThemePath);
                sb.AppendLine("theme created " + ThemePath);
            }
            if (!theme.TitleFont) theme.TitleFont = Find<TMP_FontAsset>("Roboto-Black SDF t:TMP_FontAsset", "Roboto-Black SDF");
            if (!theme.BodyFont) theme.BodyFont = Find<TMP_FontAsset>("LiberationSans SDF t:TMP_FontAsset", "LiberationSans SDF");
            if (!theme.TitleFont) theme.TitleFont = theme.BodyFont;
            if (!theme.Plate) theme.Plate = AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/IR_UI_Plate.png");
            if (!theme.Hazard) theme.Hazard = AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/IR_UI_Hazard.png");
            if (!theme.Lamp) theme.Lamp = AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/IR_UI_Lamp.png");
            if (!theme.Hover) theme.Hover = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/Pickups/Pickup_Weapon_Small.wav");
            if (!theme.Click) theme.Click = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/UI/Notification_Snap.wav");
            if (!theme.Back) theme.Back = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/UI/Notification_End.wav");
            if (!theme.Output)
            {
                var groups = AssetDatabase.LoadAllAssetsAtPath("Assets/FPS/Audio/MainAudioMixer.mixer").OfType<AudioMixerGroup>().ToArray();
                theme.Output = groups.FirstOrDefault(g => g.name == "UI") ?? groups.FirstOrDefault(g => g.name == "SFX") ?? groups.FirstOrDefault(g => g.name == "Master");
            }
            EditorUtility.SetDirty(theme);
            sb.AppendLine($"theme fonts title={(theme.TitleFont ? theme.TitleFont.name : "-")} body={(theme.BodyFont ? theme.BodyFont.name : "-")} sounds hover={(theme.Hover ? theme.Hover.name : "-")} click={(theme.Click ? theme.Click.name : "-")} back={(theme.Back ? theme.Back.name : "-")} output={(theme.Output ? theme.Output.name : "-")}");
            return theme;
        }

        // ------------------------------------------------------------------ apply
        public static void Apply(Scene scene, IronRequiemUiTheme theme, StringBuilder sb)
        {
            log = sb;
            var roots = scene.GetRootGameObjects();
            int overlays = 0;
            foreach (var view in roots.SelectMany(g => g.GetComponentsInChildren<RequiemMenuView>(true))) if (OptionsOverlay(view, theme)) overlays++;
            var canvases = roots.SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();
            int texts = 0, buttons = 0;
            foreach (var canvas in canvases)
            {
                if (IsTemplateMenu(canvas)) { log.AppendLine("  skip unused template menu " + PathOf(canvas.transform)); continue; }
                foreach (var b in canvas.GetComponentsInChildren<Button>(true)) if (StyleButton(b, theme)) buttons++;
                foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true)) if (StyleText(t, theme)) texts++;
            }
            foreach (var lab in roots.SelectMany(g => g.GetComponentsInChildren<CombatLabMenu>(true))) DeathScreen(lab, theme);
            foreach (var plate in roots.SelectMany(g => g.GetComponentsInChildren<HudBannerPlate>(true))) Banner(plate, theme);
            foreach (var cine in roots.SelectMany(g => g.GetComponentsInChildren<HangarIntroCinematic>(true))) SkipPrompt(cine, theme);
            foreach (var hud in roots.SelectMany(g => g.GetComponentsInChildren<FistChargeHud>(true))) FistHud(hud, theme);
            log.AppendLine($"{scene.path}: canvases={canvases.Length} options overlays={overlays} buttons={buttons} texts={texts}");
        }

        static string PathOf(Transform t) { var p = t.name; while (t.parent) { t = t.parent; p = t.name + "/" + p; } return p; }

        /// The FPS template pause menu ("Pause/Options", sliders and toggles) that the industrial pause replaced: its
        /// manager is not industrial and the session never opens it. Left as is so nothing it references breaks.
        static bool IsTemplateMenu(Canvas canvas)
        {
            var manager = canvas.GetComponentInParent<InGameMenuManager>(true);
            return manager && !manager.UseIndustrialMenu && manager.MenuRoot == canvas.gameObject;
        }

        static RectTransform Child(Transform parent, string name)
        {
            var t = parent.Find(name) as RectTransform; if (t) return t;
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        static void Stretch(RectTransform r, float pad = 0) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = new Vector2(-pad, -pad); r.offsetMax = new Vector2(pad, pad); r.localScale = Vector3.one; r.localRotation = Quaternion.identity; }
        static void Place(RectTransform r, Vector2 size, Vector2 pos) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = pos; r.localScale = Vector3.one; r.localRotation = Quaternion.identity; }
        static T Comp<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
        static TextMeshProUGUI Label(RectTransform r, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, float spacing)
        {
            var t = Comp<TextMeshProUGUI>(r.gameObject); if (font) t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.characterSpacing = spacing; t.raycastTarget = false; t.richText = true; t.enableWordWrapping = false;
            return t;
        }

        // ------------------------------------------------------------------ options overlay
        static bool OptionsOverlay(RequiemMenuView view, IronRequiemUiTheme theme)
        {
            var so = new SerializedObject(view);
            var options = so.FindProperty("options").objectReferenceValue as GameObject;
            var back = so.FindProperty("optionsBack").objectReferenceValue as Button;
            if (!options || !back) { log.AppendLine("  options: skip " + PathOf(view.transform) + " (incomplete)"); return false; }
            var panel = (RectTransform)options.transform;
            // full-screen layer: the industrial shell when there is one, otherwise the canvas itself
            Transform shell = panel.parent; while (shell && shell.name != "IndustrialShell" && !shell.GetComponent<Canvas>()) shell = shell.parent;
            if (!shell) { log.AppendLine("  options: no shell for " + PathOf(view.transform)); return false; }
            if (panel.parent != shell) panel.SetParent(shell, false);
            var fade = shell.Find("HG_Fade"); panel.SetSiblingIndex(fade ? Mathf.Max(0, fade.GetSiblingIndex()) : shell.childCount - 1);
            if (fade && panel.GetSiblingIndex() > fade.GetSiblingIndex()) fade.SetAsLastSibling();
            Stretch(panel);
            // console: the existing 1060x520 content, wrapped once
            var console = panel.Find("OptionsConsole") as RectTransform;
            if (!console)
            {
                var children = new List<Transform>(); for (int i = 0; i < panel.childCount; i++) children.Add(panel.GetChild(i));
                console = Child(panel, "OptionsConsole");
                foreach (var c in children) if (c.name != "OptionsBackdrop") c.SetParent(console, false);
            }
            Place(console, ConsoleSize, Vector2.zero);
            var fit = Comp<ScreenFitPanel>(console.gameObject); fit.Configure(.76f, .92f);
            console.localScale = Vector3.one * Mathf.Min(1080 * .76f / ConsoleSize.y, 1920 * .92f / ConsoleSize.x); // 1920x1080 canvas; refit at runtime
            // dimmed backdrop over the menu behind (blocks clicks to the home buttons underneath)
            var backdrop = Child(panel, "OptionsBackdrop"); backdrop.SetAsFirstSibling(); Stretch(backdrop, 120);
            var bi = Comp<Image>(backdrop.gameObject); bi.sprite = null; bi.color = theme.Backdrop; bi.raycastTarget = true;
            // VOLVER: larger target, lamp kept clear of the label
            var br = (RectTransform)back.transform; Place(br, new Vector2(380, 72), new Vector2(-240, -204));
            var lamp = br.Find("BackLamp") as RectTransform; if (lamp) lamp.anchoredPosition = new Vector2(-160, 0);
            var bl = back.GetComponentInChildren<TMP_Text>(true); if (bl) { bl.text = "VOLVER"; bl.fontSize = 30; }
            var fx = back.GetComponent<HangarButtonFx>(); if (fx) fx.ConfigureTheme(theme, true);
            // CONTROLES plate under the CCTV monitor
            var help = Child(console, "ControlsHelp"); Place(help, new Vector2(440, 84), new Vector2(255, -212));
            var hp = Comp<IndustrialPanelGraphic>(help.gameObject); hp.Configure(new Color(.02f, .04f, .05f, .9f), theme.PanelRim, 10); hp.raycastTarget = false;
            var title = Child(help, "Title"); Place(title, new Vector2(410, 18), new Vector2(0, 28));
            Label(title, "CONTROLES · MÓDULO DE COMBATE", theme.BodyFont, 12, theme.Cyan, TextAlignmentOptions.MidlineLeft, theme.BodySpacing);
            var body = Child(help, "Body"); Place(body, new Vector2(410, 56), new Vector2(0, -10));
            string k(string key) => $"<color=#{ColorUtility.ToHtmlStringRGB(theme.Orange)}>{key}</color>";
            Label(body, $"{k("[F]")} GOLPE   {k("MANTENER [F]")} CARGAR\n{k("[M1]")} GOLPE SIN ARMA   {k("[E]")} INTERACTUAR\n{k("[ESC]")} VOLVER", theme.BodyFont, 14, theme.Steel, TextAlignmentOptions.TopLeft, 2).lineSpacing = 4;
            EditorUtility.SetDirty(view);
            log.AppendLine($"  options overlay {PathOf(panel)} console scale {console.localScale.x:0.00} (fits 76% height)");
            return true;
        }

        // ------------------------------------------------------------------ buttons
        static bool IsBack(Button b, TMP_Text label) => b.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0 || (label && label.text.Trim().StartsWith("VOLVER", StringComparison.OrdinalIgnoreCase));
        static bool StyleButton(Button b, IronRequiemUiTheme theme)
        {
            var r = (RectTransform)b.transform; if (r.rect.width < 60 || r.rect.height < 30) return false; // close crosses, tiny toggles
            var label = b.GetComponentInChildren<TMP_Text>(true);
            var fx = b.GetComponent<HangarButtonFx>();
            if (!fx)
            {
                var face = b.GetComponent<IndustrialPanelGraphic>();
                if (!face) { var fr = Child(r, "IndustrialButtonFace"); fr.SetAsFirstSibling(); Stretch(fr); face = Comp<IndustrialPanelGraphic>(fr.gameObject); }
                face.raycastTarget = face.gameObject == b.gameObject;
                var image = b.GetComponent<Image>(); if (image) { image.color = new Color(1, 1, 1, 0); image.raycastTarget = true; }
                var glow = Child(r, "HG_Glow"); Stretch(glow); glow.SetSiblingIndex(face.transform == r ? 0 : face.transform.GetSiblingIndex() + 1);
                var gi = Comp<Image>(glow.gameObject); gi.sprite = null; gi.color = new Color(theme.Orange.r, theme.Orange.g * .85f, theme.Orange.b * .5f, 0); gi.raycastTarget = false;
                var accent = Child(r, "HG_Accent"); accent.anchorMin = new Vector2(0, .14f); accent.anchorMax = new Vector2(0, .86f); accent.pivot = new Vector2(.5f, .5f); accent.sizeDelta = new Vector2(6, 0); accent.anchoredPosition = new Vector2(4, 0); accent.SetAsLastSibling();
                var ai = Comp<Image>(accent.gameObject); ai.sprite = null; ai.color = new Color(theme.Orange.r, theme.Orange.g, theme.Orange.b, 0); ai.raycastTarget = false;
                if (label) label.transform.SetSiblingIndex(accent.GetSiblingIndex());
                fx = b.gameObject.AddComponent<HangarButtonFx>(); fx.Configure(face, label, ai, gi);
                b.transition = Selectable.Transition.None;
            }
            fx.ConfigureTheme(theme, IsBack(b, label));
            // v6: a fully transparent hit image is culled by its CanvasRenderer and then ignored by the GraphicRaycaster
            // (the death-screen buttons could not be clicked). Keep the mesh so the button still receives the pointer.
            var hit = b.GetComponent<Image>(); if (hit && hit.color.a < .01f) { hit.enabled = true; hit.canvasRenderer.cullTransparentMesh = false; hit.raycastTarget = true; EditorUtility.SetDirty(hit.canvasRenderer); EditorUtility.SetDirty(hit); }
            if (label) { label.font = theme.TitleFont ? theme.TitleFont : label.font; label.characterSpacing = Mathf.Max(label.characterSpacing, theme.BodySpacing + 1); label.color = theme.ButtonRestText; if (label.fontSize < 24) label.fontSize = 24; label.enableWordWrapping = false;
                // hangar signature on every button: label left of centre after the accent bar
                var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.pivot = new Vector2(.5f, .5f); lr.offsetMin = lr.offsetMax = Vector2.zero; lr.localScale = Vector3.one;
                label.alignment = TextAlignmentOptions.Left; label.margin = new Vector4(r.Find("BackLamp") ? 64 : 36, 0, 12, 0); label.enableAutoSizing = true; label.fontSizeMax = Mathf.Max(label.fontSizeMax, label.fontSize); label.fontSizeMin = 18; }
            EditorUtility.SetDirty(fx);
            return true;
        }

        // ------------------------------------------------------------------ text
        static float Dist(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        static bool StyleText(TMP_Text t, IronRequiemUiTheme theme)
        {
            if (t.GetComponentInParent<Button>(true)) return false; // button labels: StyleButton
            bool title = t.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 || t.fontSize >= 40;
            var font = title ? theme.TitleFont : theme.BodyFont; if (!font) return false;
            bool changed = t.font != font;
            if (changed)
            {
                if (title && !t.enableAutoSizing) { t.fontSizeMax = t.fontSize; t.fontSizeMin = Mathf.Round(t.fontSize * .6f); t.enableAutoSizing = true; } // the title face is wider
                t.font = font;
            }
            t.characterSpacing = title ? Mathf.Max(t.characterSpacing, theme.TitleSpacing) : Mathf.Max(t.characterSpacing, theme.BodySpacing);
            // snap near-palette colours onto the theme so whites, oranges and cyans match across screens
            var c = t.color; float a = c.a; Color snapped = c;
            if (c.r > .93f && c.g > .9f && c.b > .82f) snapped = theme.Text;                     // white / warm white
            else if (c.r > .9f && c.g > .42f && c.g < .7f && c.b < .3f) snapped = theme.Orange;     // template / hangar oranges
            else if (c.r < .5f && c.g > .7f && c.b > .8f) snapped = theme.Cyan;                     // cyans
            else if (Dist(c, theme.Steel) < .2f) snapped = theme.Steel;
            snapped.a = a; t.color = snapped;
            EditorUtility.SetDirty(t);
            return true;
        }

        // ------------------------------------------------------------------ death screen
        static void DeathScreen(CombatLabMenu lab, IronRequiemUiTheme theme)
        {
            var death = new SerializedObject(lab).FindProperty("deathPanel").objectReferenceValue as GameObject; if (!death) return;
            var root = (RectTransform)death.transform;
            var bg = root.GetComponent<Image>(); if (bg) { bg.color = theme.Backdrop; bg.sprite = null; }
            var console = Child(root, "DeathConsole"); console.SetAsFirstSibling(); Place(console, new Vector2(860, 470), new Vector2(0, 20));
            var face = Comp<IndustrialPanelGraphic>(console.gameObject); face.Configure(theme.PanelFill, new Color(.55f, .16f, .1f, 1), 22); face.raycastTarget = false;
            var hz = Child(console, "HazardStrip"); Place(hz, new Vector2(780, 14), new Vector2(0, 205));
            var hi = Comp<Image>(hz.gameObject); hi.sprite = theme.Hazard; hi.type = Image.Type.Tiled; hi.color = Color.white; hi.raycastTarget = false;
            var sector = Child(console, "SectorLabel"); Place(sector, new Vector2(760, 26), new Vector2(0, 176));
            Label(sector, "// SEÑAL VITAL PERDIDA · UNIDAD C-04", theme.BodyFont, 16, theme.Danger, TextAlignmentOptions.Center, theme.BodySpacing + 1);
            var titleT = root.Find("Title") as RectTransform;
            if (titleT) { Place(titleT, new Vector2(820, 90), new Vector2(0, 118)); var t = titleT.GetComponent<TMP_Text>(); if (t) { t.font = theme.TitleFont; t.fontSize = 64; t.color = theme.Text; t.characterSpacing = theme.TitleSpacing; t.alignment = TextAlignmentOptions.Center; } }
            var sub = Child(console, "Subtitle"); Place(sub, new Vector2(760, 28), new Vector2(0, 56));
            Label(sub, "EL CHASIS NO RESPONDE · REINICIO DISPONIBLE", theme.BodyFont, 16, theme.TextDim, TextAlignmentOptions.Center, theme.BodySpacing);
            var restart = root.Find("RestartButton") as RectTransform; if (restart) Place(restart, new Vector2(520, 68), new Vector2(0, -24));
            var menu = root.Find("MenuButton") as RectTransform; if (menu) Place(menu, new Vector2(520, 68), new Vector2(0, -110));
            foreach (var b in root.GetComponentsInChildren<Button>(true)) StyleButton(b, theme);
            var main = new SerializedObject(lab).FindProperty("mainPanel").objectReferenceValue as GameObject;
            if (main) { var mi = main.GetComponent<Image>(); if (mi) mi.color = theme.Backdrop; }
            log.AppendLine("  death screen restyled " + PathOf(root));
        }

        // ------------------------------------------------------------------ banners, prompts, HUD
        static void Banner(HudBannerPlate banner, IronRequiemUiTheme theme)
        {
            var so = new SerializedObject(banner);
            var label = so.FindProperty("label").objectReferenceValue as TMP_Text; var plate = so.FindProperty("plate").objectReferenceValue as Graphic;
            var canvas = banner.GetComponentInParent<Canvas>(true); canvas = canvas ? canvas.rootCanvas : null;
            if (canvas)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f; scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; EditorUtility.SetDirty(scaler); }
            }
            if (plate is IndustrialPanelGraphic ip) { ip.Configure(theme.PanelFill, theme.PanelRim, 14); Place(ip.rectTransform, new Vector2(900, 62), new Vector2(0, -112)); ip.rectTransform.anchorMin = ip.rectTransform.anchorMax = new Vector2(.5f, 1); ip.rectTransform.anchoredPosition = new Vector2(0, -112); }
            if (label) { var lr = label.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(.5f, 1); lr.pivot = new Vector2(.5f, .5f); lr.sizeDelta = new Vector2(860, 52); lr.anchoredPosition = new Vector2(0, -114); label.font = theme.TitleFont; label.fontSize = 26; label.color = theme.Text; label.characterSpacing = theme.BodySpacing + 1; label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 26; }
            EditorUtility.SetDirty(banner);
            log.AppendLine("  banner " + PathOf(banner.transform));
        }

        static void SkipPrompt(HangarIntroCinematic cine, IronRequiemUiTheme theme)
        {
            var so = new SerializedObject(cine);
            var prompt = so.FindProperty("skipPrompt").objectReferenceValue as CanvasGroup; var fill = so.FindProperty("skipFill").objectReferenceValue as Image;
            if (!prompt) return;
            var r = (RectTransform)prompt.transform;
            var plate = Child(r, "IR_PromptPlate"); plate.SetAsFirstSibling(); Stretch(plate, 10);
            var face = Comp<IndustrialPanelGraphic>(plate.gameObject); face.Configure(new Color(.02f, .04f, .05f, .82f), theme.PanelRim, 10); face.raycastTarget = false;
            if (fill) { fill.color = theme.Orange; EditorUtility.SetDirty(fill); }
            foreach (var t in r.GetComponentsInChildren<TMP_Text>(true)) { t.font = theme.BodyFont; t.color = theme.Text; t.characterSpacing = theme.BodySpacing; }
            log.AppendLine("  skip prompt " + PathOf(r));
        }

        /// Thin ring sprite for radial gauges (fist charge). Generated once (pure white, soft edges), then reused.
        static Sprite RingSprite()
        {
            const string path = UiDir + "/IR_UI_Ring.png";
            if (!File.Exists(path))
            {
                const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = x + .5f - n * .5f, dy = y + .5f - n * .5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(62f - d) * Mathf.Clamp01(d - 49f); // 13 px band, 1 px soft edges
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
                }
                tex.SetPixels32(px); File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                log.AppendLine("  created " + path);
            }
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti && (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled))
            { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void FistHud(FistChargeHud hud, IronRequiemUiTheme theme)
        {
            var ring = RingSprite();
            foreach (var name in new[] { "Track", "Fill", "FullFlash" })
            {
                var img = hud.transform.Find(name)?.GetComponent<Image>(); if (!img || !ring) continue;
                img.sprite = ring; img.preserveAspect = true;
                if (name == "Fill") { img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Radial360; img.fillOrigin = (int)Image.Origin360.Top; img.fillClockwise = true; }
                else img.type = Image.Type.Simple;
                if (name == "Track") img.color = new Color(theme.Steel.r, theme.Steel.g, theme.Steel.b, .22f);
                EditorUtility.SetDirty(img);
            }
            var so = new SerializedObject(hud);
            so.FindProperty("chargeColor").colorValue = new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, .92f);
            so.FindProperty("fullColor").colorValue = new Color(.88f, 1f, 1f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            var label = hud.transform.Find("Label")?.GetComponent<TMP_Text>(); if (label) { label.font = theme.TitleFont; label.color = theme.Cyan; label.characterSpacing = theme.BodySpacing + 1; }
        }
    }
}
