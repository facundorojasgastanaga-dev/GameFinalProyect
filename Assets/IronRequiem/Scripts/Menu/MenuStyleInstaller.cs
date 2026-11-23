using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronRequiem
{
    /// 20/11/2026 — brings the options console and the credits screen to the look of the main menu (riveted graphite
    /// sheet, tape plates, ivory title lettering, cyan sector line, orange accents) and adds the light effects: fader
    /// glow that grows with the lever, console lamps with a halo and faults, lit menu buttons and the worn title.
    /// Everything is applied at run time when a scene loads: no scene or prefab is edited, and the Sliders, Toggle,
    /// Buttons, their listeners, navigation and saved settings are the same objects as before. Idempotent.
    static class MenuStyleInstaller
    {
        static readonly Color SheetFill = new Color(.125f, .14f, .155f, .98f), SheetRim = new Color(.035f, .04f, .045f, 1);
        static readonly Color TapeFill = new Color(.03f, .034f, .04f, .94f), TapeEdge = new Color(.22f, .24f, .25f, 1);
        static readonly Color Ivory = new Color(.91f, .89f, .80f), Dim = new Color(.57f, .63f, .63f), Cyan = new Color(.13f, .84f, .92f), Orange = new Color(1f, .40f, .08f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded;
            Apply();
        }
        static void OnLoaded(Scene scene, LoadSceneMode mode) => Apply();

        public static void Apply()
        {
            var theme = IronRequiemUiThemeLink.Load();
            foreach (var fit in Object.FindObjectsByType<ScreenFitPanel>(FindObjectsInactive.Include))
                if (fit.name == "OptionsConsole") Console((RectTransform)fit.transform, theme);
            foreach (var group in Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include))
                if (group.name == "CreditsPanel") Credits((RectTransform)group.transform, theme);
            foreach (var panel in Object.FindObjectsByType<IndustrialPanelGraphic>(FindObjectsInactive.Include))
                if (panel.name == "ConsoleFrame") Pause((RectTransform)panel.transform, theme);
            foreach (var fx in Object.FindObjectsByType<MenuPolishButtonFx>(FindObjectsInactive.Include))
                if (!fx.GetComponent<MenuButtonLightFx>()) fx.gameObject.AddComponent<MenuButtonLightFx>();
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
                if (text.name == "Title" && text.text != null && (text.text.Contains("REQUIEM") || text.text.Contains("PAUSA")) && !text.GetComponent<TitleWornFx>())
                    text.gameObject.AddComponent<TitleWornFx>().Configure(SheetFill);
        }

        // ------------------------------------------------------------------ helpers
        static void Place(RectTransform r, Vector2 size, Vector2 pos) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = pos; r.localScale = Vector3.one; r.localRotation = Quaternion.identity; }
        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)); go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform; r.SetParent(parent, false); return r;
        }
        static IndustrialPanelGraphic Panel(string name, Transform parent, IndustrialPanelGraphic.PanelStyle style, Color fill, Color rim, float cut)
        {
            var r = Child(name, parent); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; r.SetAsFirstSibling();
            var p = r.gameObject.AddComponent<IndustrialPanelGraphic>(); p.SetStyle(style); p.Configure(fill, rim, cut); p.raycastTarget = false; return p;
        }
        static TMP_Text Label(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, Vector2 dim, Vector2 pos)
        {
            var r = Child(name, parent); Place(r, dim, pos);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>(); if (font) t.font = font;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.MidlineLeft; t.characterSpacing = 3; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
        static void Tint(Transform t, Color c) { var text = t ? t.GetComponent<TMP_Text>() : null; if (text) text.color = c; }
        static Image Rule(string name, Transform parent, Vector2 size, Vector2 pos, Color c)
        {
            var r = Child(name, parent); Place(r, size, pos); var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false; return i;
        }

        // ------------------------------------------------------------------ options console
        static void Console(RectTransform console, IronRequiemUiTheme theme)
        {
            var plate = console.Find("IndustrialConsole"); if (!plate || plate.Find("IR_Sheet")) return;
            var heading = console.Find("OptionsHeading") ? console.Find("OptionsHeading").GetComponent<TMP_Text>() : null;
            var body = theme && theme.BodyFont ? theme.BodyFont : heading ? heading.font : null;

            // the brushed-steel plate becomes the same riveted graphite sheet as the menu frame (it carries its own hazard tape)
            var steel = plate.GetComponent<Image>(); if (steel) steel.enabled = false;
            Panel("IR_Sheet", plate, IndustrialPanelGraphic.PanelStyle.Sheet, SheetFill, SheetRim, 14);
            var hazard = plate.Find("HazardStrip"); if (hazard) hazard.gameObject.SetActive(false);
            var recess = plate.Find("ControlsPlate");
            if (recess) { var ri = recess.GetComponent<Image>(); if (ri) ri.enabled = false; Panel("IR_Recess", recess, IndustrialPanelGraphic.PanelStyle.Label, TapeFill, TapeEdge, 8);
                var plateLabel = recess.Find("PlateLabel"); if (plateLabel) plateLabel.gameObject.SetActive(false); } // it ran under the read-outs

            // heading in the menu's lettering: cyan sector line over an ivory title
            if (heading)
            {
                heading.text = "AJUSTES"; if (theme && theme.TitleFont) heading.font = theme.TitleFont; heading.fontStyle = FontStyles.Normal; heading.enableAutoSizing = false; heading.fontSize = 27; heading.color = Ivory; heading.characterSpacing = 3;
                heading.alignment = TextAlignmentOptions.MidlineLeft; heading.textWrappingMode = TextWrappingModes.NoWrap;
                Place(heading.rectTransform, new Vector2(620, 34), new Vector2(-190, 200));
                var sector = Label("IR_Sector", console, "UNIDAD C-04  /  CONSOLA DE PLANTA", body, 13, Cyan, new Vector2(620, 18), new Vector2(-190, 226));
                sector.transform.SetSiblingIndex(heading.transform.GetSiblingIndex());
            }

            // status lamps: real halos, each with its own behaviour
            Lamp(plate.Find("LampPWR"), new Color(.3f, 1f, .45f), MenuLampFx.Mode.Steady);
            Lamp(plate.Find("LampSRV"), Orange, MenuLampFx.Mode.Pulse);
            Lamp(plate.Find("LampNET"), Cyan, MenuLampFx.Mode.Activity);

            // faders: read-out drawn over its little screen (it was hidden behind it), light that follows the lever
            foreach (var slider in console.GetComponentsInChildren<Slider>(true))
            {
                var block = slider.transform.parent; if (!block) continue;
                var lcd = block.Find("ValueLcd"); var value = block.Find("Value");
                if (lcd && value)
                {
                    if (lcd.GetSiblingIndex() > value.GetSiblingIndex()) lcd.SetSiblingIndex(value.GetSiblingIndex());
                    var li = lcd.GetComponent<Image>(); if (li) li.color = new Color(.012f, .02f, .024f, .97f);
                }
                Tint(block.Find("Label"), Ivory);
                if (!slider.GetComponent<FaderGlowFx>()) slider.gameObject.AddComponent<FaderGlowFx>();
            }
            foreach (var toggle in console.GetComponentsInChildren<Toggle>(true)) Tint(toggle.transform.Find("FullscreenLabel"), Ivory);

            // monitor and help plate
            var bezel = plate.Find("CctvMonitor/Bezel"); if (bezel) { var bi = bezel.GetComponent<Image>(); if (bi) bi.color = new Color(.60f, .63f, .66f); }
            Tint(plate.Find("MonitorPlate"), Dim);
            var help = console.Find("ControlsHelp");
            if (help) { var hp = help.GetComponent<IndustrialPanelGraphic>(); if (hp) { hp.SetStyle(IndustrialPanelGraphic.PanelStyle.Label); hp.Configure(TapeFill, TapeEdge, 8); } }

            // the old loose lamp of VOLVER sat outside the halved button; the tape buttons carry their own indicator
            foreach (var t in console.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "BackLamp") continue;
                t.gameObject.SetActive(false);
                // pause menu buttons kept a wide left margin for that lamp: give the label its room back
                foreach (var l in t.parent.GetComponentsInChildren<TMP_Text>(true)) if (l.margin.x > 40) { var m = l.margin; m.x = 22; l.margin = m; }
            }
        }

        static void Lamp(Transform lamp, Color color, MenuLampFx.Mode mode)
        {
            if (!lamp) return;
            var image = lamp.GetComponent<Image>(); if (!image) return;
            image.color = color; Tint(lamp.Find("LampLabel"), Dim);
            if (!lamp.GetComponent<MenuLampFx>()) lamp.gameObject.AddComponent<MenuLampFx>().Configure(mode);
        }

        // ------------------------------------------------------------------ pause menu
        /// The pause screen was a wide, almost empty sheet with everything centred. It becomes a compact console in the
        /// main menu's layout: left-aligned lettering, worn title, orange rule, numbered tape buttons with their lights,
        /// status lamps and a key hint. Same Buttons and listeners; MenuShellMotion keeps owning the panel positions.
        static void Pause(RectTransform frame, IronRequiemUiTheme theme)
        {
            var title = frame.Find("Title") ? frame.Find("Title").GetComponent<TMP_Text>() : null;
            var home = frame.Find("Home") as RectTransform;
            if (!title || !home || title.text == null || !title.text.Contains("PAUSA") || frame.Find("IR_PauseRule")) return;
            var body = theme && theme.BodyFont ? theme.BodyFont : null;
            const float K = 1.22f, W = 540 * K;                       // the main menu's proportions, a little larger
            Vector2 P(float x, float y) => new Vector2(x, y) * K;
            frame.sizeDelta = P(640, 600);

            var sector = frame.Find("SectorLabel") ? frame.Find("SectorLabel").GetComponent<TMP_Text>() : null;
            if (sector) { sector.fontSize = 14 * K; sector.color = Cyan; sector.characterSpacing = 3; sector.alignment = TextAlignmentOptions.MidlineLeft; sector.text = "UNIDAD C-04  /  SISTEMA DE COMBATE"; Place(sector.rectTransform, new Vector2(W, 22 * K), P(0, 262)); }
            title.enableAutoSizing = false; title.fontSize = 74 * K; title.color = Ivory; title.characterSpacing = 2; title.alignment = TextAlignmentOptions.MidlineLeft; title.textWrappingMode = TextWrappingModes.NoWrap;
            Place(title.rectTransform, new Vector2(W, 90 * K), P(0, 204));
            var subtitle = frame.Find("Subtitle") ? frame.Find("Subtitle").GetComponent<TMP_Text>() : null;
            if (subtitle)
            {
                if (body) subtitle.font = body;
                subtitle.enableAutoSizing = false; subtitle.fontSize = 15 * K; subtitle.color = new Color(.62f, .67f, .66f); subtitle.characterSpacing = 2; subtitle.alignment = TextAlignmentOptions.MidlineLeft;
                subtitle.text = "SISTEMAS EN ESPERA. LA PARTIDA NO AVANZA."; Place(subtitle.rectTransform, new Vector2(W, 24 * K), P(0, 146));
            }
            Rule("IR_PauseRule", frame, new Vector2(W, 1), P(0, 120), new Color(.19f, .29f, .30f, .9f));
            Rule("IR_PauseAccent", frame, new Vector2(108 * K, 3), P(-216, 120), Orange);

            // buttons: same width as the lettering, numbered, with an indicator that carries the light
            float dy = -home.anchoredPosition.y, y = 58 * K; int number = 1;
            foreach (var button in home.GetComponentsInChildren<Button>(true))
            {
                var r = (RectTransform)button.transform; if (r.parent != home) continue;
                Place(r, new Vector2(W, 66 * K), new Vector2(0, y + dy)); y -= 80 * K;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label) { var m = label.margin; m.x = 74 * K; m.z = 44 * K; label.margin = m; label.alignment = TextAlignmentOptions.MidlineLeft; label.fontSizeMax = 25 * K; label.fontSize = 25 * K; }
                var index = Label("PolishIndex", r, number.ToString("00"), body, 14 * K, Dim, new Vector2(36, 25) * K, Vector2.zero);
                index.alignment = TextAlignmentOptions.Center; index.characterSpacing = 0;
                var ir = index.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0, .5f); ir.anchoredPosition = new Vector2(36 * K, 0);
                var light = Rule("PolishIndicator", r, new Vector2(4 * K, 18 * K), Vector2.zero, Orange);
                light.rectTransform.anchorMin = light.rectTransform.anchorMax = new Vector2(1, .5f); light.rectTransform.anchoredPosition = new Vector2(-23 * K, 0);
                if (!button.GetComponent<MenuButtonLightFx>()) button.gameObject.AddComponent<MenuButtonLightFx>();
                number++;
            }

            // status row: lamps on the left, the fixed keys on the right
            var lamp = theme ? theme.Lamp : null; string[] names = { "PWR", "SRV", "NET" };
            Color[] colors = { new Color(.3f, 1f, .45f), Orange, Cyan }; MenuLampFx.Mode[] modes = { MenuLampFx.Mode.Steady, MenuLampFx.Mode.Pulse, MenuLampFx.Mode.Activity };
            for (int i = 0; i < names.Length; i++)
            {
                var image = Rule("IR_Lamp" + names[i], frame, new Vector2(18, 18) * K, P(-258 + i * 56, -196), colors[i]); image.sprite = lamp;
                var caption = Label("LampLabel", image.transform, names[i], body, 11 * K, Dim, new Vector2(44, 16) * K, P(0, -20)); caption.alignment = TextAlignmentOptions.Center; caption.characterSpacing = 1;
                image.gameObject.AddComponent<MenuLampFx>().Configure(modes[i]);
            }
            var hint = Label("IR_PauseHint", frame, "<color=#FF6614>[ESC]</color> / <color=#FF6614>[P]</color>  REANUDAR", body, 13 * K, Dim, new Vector2(300, 20) * K, P(120, -200));
            hint.alignment = TextAlignmentOptions.MidlineRight; hint.characterSpacing = 1;
            var footer = frame.Find("Footer") ? frame.Find("Footer").GetComponent<TMP_Text>() : null;
            if (footer) { footer.fontSize = 12 * K; footer.color = Dim; footer.alignment = TextAlignmentOptions.MidlineLeft; Place(footer.rectTransform, new Vector2(W, 20 * K), P(0, -262)); }
        }

        // ------------------------------------------------------------------ credits
        static void Credits(RectTransform panel, IronRequiemUiTheme theme)
        {
            if (panel.Find("IR_CreditsRule")) return;
            // same box as the home buttons, inside the menu frame (which now stays visible on this screen). MenuShellMotion
            // owns the panel position (it slides the panel in from its authored spot), so the content is shifted instead.
            float dy = -136 - panel.anchoredPosition.y;
            panel.sizeDelta = new Vector2(520, 410); panel.localScale = Vector3.one;
            var title = panel.Find("CreditsTitle") ? panel.Find("CreditsTitle").GetComponent<TMP_Text>() : null;
            var body = theme && theme.BodyFont ? theme.BodyFont : null;
            Label("IR_CreditsSector", panel, "REGISTRO  /  EQUIPO DE DESARROLLO", body, 13, Cyan, new Vector2(520, 18), new Vector2(0, 178 + dy));
            if (title)
            {
                title.enableAutoSizing = false; title.fontSize = 32; title.color = Ivory; title.characterSpacing = 4;
                title.alignment = TextAlignmentOptions.MidlineLeft; title.textWrappingMode = TextWrappingModes.NoWrap;
                Place(title.rectTransform, new Vector2(520, 44), new Vector2(0, 146 + dy));
            }
            Rule("IR_CreditsRule", panel, new Vector2(520, 1), new Vector2(0, 116 + dy), new Color(.19f, .29f, .30f, .9f));
            Rule("IR_CreditsAccent", panel, new Vector2(108, 3), new Vector2(-206, 116 + dy), Orange);
            var text = panel.Find("CreditsText") ? panel.Find("CreditsText").GetComponent<TMP_Text>() : null;
            if (text)
            {
                text.enableAutoSizing = false; text.fontSize = 17; text.color = new Color(.72f, .76f, .75f); text.lineSpacing = 10; text.characterSpacing = 1;
                text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate;
                Place(text.rectTransform, new Vector2(520, 190), new Vector2(0, 2 + dy));
            }
            var back = panel.Find("CreditsBack") as RectTransform; if (back) Place(back, new Vector2(520, 66), new Vector2(0, -154 + dy));
        }
    }
}
