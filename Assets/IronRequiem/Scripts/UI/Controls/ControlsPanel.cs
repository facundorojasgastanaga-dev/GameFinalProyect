using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IronRequiem
{
    /// "CONTROLES" panel (mockup A): two columns of actions with two rebindable slots each, tabs for keyboard/mouse,
    /// gamepad and settings. Built at run time as its own overlay so no scene or prefab is edited. Esc cancels a capture
    /// first and closes the panel second; Esc and P (pause) are never bindable.
    public sealed class ControlsPanel : MonoBehaviour
    {
        enum Tab { Keyboard, Gamepad, Settings }

        static ControlsPanel instance;
        IronRequiemUiTheme theme;
        TMP_FontAsset font;
        RectTransform columnA, columnB, contentRect;
        TMP_Text status, subtitle, capturingLabel;
        GameObject conflictBar;
        TMP_Text conflictText;
        Button swapButton;
        readonly List<Button> tabButtons = new List<Button>();
        Tab tab = Tab.Keyboard;
        InputActionRebindingExtensions.RebindingOperation operation;
        TMP_Text waitingText;
        int backConsumedFrame = -1;
        // pending conflict
        InputAction pendingAction, pendingOther; int pendingIndex, pendingOtherIndex; string pendingPath;

        public static bool IsOpen => instance && instance.gameObject.activeSelf;

        public static void Open()
        {
            if (!instance) instance = Build();
            instance.tab = Tab.Keyboard;
            instance.gameObject.SetActive(true);
            instance.Rebuild();
            PauseMenuBackHook.Override = instance.HandleBack;
            if (EventSystem.current && instance.tabButtons.Count > 0) EventSystem.current.SetSelectedGameObject(instance.tabButtons[0].gameObject);
        }

        public static void Close()
        {
            if (!instance) return;
            instance.CancelCapture();
            instance.gameObject.SetActive(false);
            if (PauseMenuBackHook.Override == (System.Func<bool>)instance.HandleBack) PauseMenuBackHook.Override = null;
        }

        void OnDestroy() { if (instance == this) instance = null; CancelCapture(); if (PauseMenuBackHook.Override == (System.Func<bool>)HandleBack) PauseMenuBackHook.Override = null; }

        // Esc: first cancels a running capture or a pending conflict, then closes the panel.
        bool HandleBack()
        {
            if (!IsOpen) return false;
            handledFrame = Time.frameCount;
            if (Time.frameCount == backConsumedFrame) return true; // the capture was just cancelled by this very key press
            if (pendingAction != null) { ClearConflict(); return true; }
            Close(); return true;
        }

        // ---------------------------------------------------------------- construction

        static ControlsPanel Build()
        {
            var go = new GameObject("IR_ControlsPanel");
            var panel = go.AddComponent<ControlsPanel>();
            panel.theme = IronRequiemUiThemeLink.Load();
            panel.font = panel.theme ? panel.theme.BodyFont : null;
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
            var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            panel.BuildLayout(go.GetComponent<RectTransform>());
            return panel;
        }

        Color Cyan => theme ? theme.Cyan : new Color(.12f, .82f, .91f);
        Color Orange => theme ? theme.Orange : new Color(1, .6f, .2f);
        Color Steel => theme ? theme.Steel : new Color(.82f, .86f, .9f);
        Color Dim => theme ? theme.TextDim : new Color(.56f, .66f, .69f);
        Color Danger => theme ? theme.Danger : new Color(1, .3f, .2f);

        void BuildLayout(RectTransform root)
        {
            var backdrop = Child("Backdrop", root); Stretch(backdrop);
            var bg = backdrop.gameObject.AddComponent<Image>(); bg.color = theme ? theme.Backdrop : new Color(.008f, .016f, .024f, .92f);

            var panel = Child("Panel", root); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(1500, 900);
            var frame = panel.gameObject.AddComponent<IndustrialPanelGraphic>();
            frame.Configure(theme ? theme.PanelFill : new Color(.03f, .05f, .065f, .96f), theme ? theme.PanelRim : new Color(.1f, .42f, .48f, 1), 14);

            Text("Title", panel, "CONTROLES", 36, Steel, new Vector2(40, -28), new Vector2(600, 46), TextAlignmentOptions.Left, true);
            subtitle = Text("Subtitle", panel, "", 18, Dim, new Vector2(40, -76), new Vector2(1420, 28), TextAlignmentOptions.Left, false);

            string[] names = { "TECLADO Y RATÓN", "MANDO", "AJUSTES" };
            for (int i = 0; i < 3; i++)
            {
                int t = i; var b = MakeButton(panel, "Tab" + i, names[i], new Vector2(40 + i * 250, -120), new Vector2(240, 44), () => { CancelCapture(); ClearConflict(); tab = (Tab)t; Rebuild(); });
                tabButtons.Add(b);
            }

            var viewport = Child("Viewport", panel); viewport.anchorMin = new Vector2(0, 0); viewport.anchorMax = new Vector2(1, 1);
            viewport.offsetMin = new Vector2(40, 130); viewport.offsetMax = new Vector2(-40, -180);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var content = Child("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
            contentRect = content;
            columnA = Column("ColumnA", content, 0f, .5f); columnB = Column("ColumnB", content, .5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.scrollSensitivity = 40; scroll.movementType = ScrollRect.MovementType.Clamped;

            conflictBar = Child("Conflict", panel).gameObject;
            var cr = (RectTransform)conflictBar.transform; cr.anchorMin = new Vector2(0, 0); cr.anchorMax = new Vector2(1, 0); cr.pivot = new Vector2(.5f, 0); cr.offsetMin = new Vector2(40, 84); cr.offsetMax = new Vector2(-40, 128);
            var cimg = conflictBar.AddComponent<Image>(); cimg.color = new Color(.1f, .03f, .02f, .96f);
            conflictText = Text("Text", cr, "", 20, new Color(1, .7f, .65f), new Vector2(14, -6), new Vector2(900, 32), TextAlignmentOptions.Left, false);
            swapButton = MakeButton(cr, "Swap", "INTERCAMBIAR", new Vector2(1010, -2), new Vector2(210, 40), ApplySwap);
            MakeButton(cr, "Cancel", "CANCELAR", new Vector2(1236, -2), new Vector2(190, 40), ClearConflict);
            conflictBar.SetActive(false);

            MakeButton(panel, "Reset", "RESTABLECER TODO", new Vector2(40, -846), new Vector2(300, 44), () => { CancelCapture(); ClearConflict(); ControlsRebinder.ResetAll(InputSystem.actions); SetStatus("Controles restablecidos"); Rebuild(); });
            status = Text("Status", panel, "", 18, Dim, new Vector2(380, -854), new Vector2(740, 28), TextAlignmentOptions.Center, false);
            MakeButton(panel, "Back", "VOLVER", new Vector2(1220, -846), new Vector2(240, 44), Close, true);
        }

        static RectTransform Child(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); return rt;
        }
        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        // Two half-width columns; each sizes itself to its rows and the scroll content takes the taller one (see Rebuild).
        RectTransform Column(string name, RectTransform parent, float x0, float x1)
        {
            var rt = Child(name, parent); rt.anchorMin = new Vector2(x0, 1); rt.anchorMax = new Vector2(x1, 1); rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(x0 == 0 ? 0 : 24, 0); rt.offsetMax = new Vector2(x1 == 1 ? 0 : -24, 0);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 4; v.childForceExpandWidth = true; v.childForceExpandHeight = false; v.childControlWidth = true; v.childControlHeight = true; v.childAlignment = TextAnchor.UpperLeft;
            rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        void FitContent()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(columnA); LayoutRebuilder.ForceRebuildLayoutImmediate(columnB);
            contentRect.sizeDelta = new Vector2(0, Mathf.Max(columnA.rect.height, columnB.rect.height));
        }

        TMP_Text Text(string name, RectTransform parent, string value, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align, bool title)
        {
            var rt = Child(name, parent); rt.anchoredPosition = pos; rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); t.text = value; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
            if (font) t.font = font;
            if (title) t.characterSpacing = theme ? theme.TitleSpacing * 2 : 8; else if (theme) t.characterSpacing = theme.BodySpacing * .5f;
            return t;
        }

        Button MakeButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick, bool hot = false)
        {
            var rt = Child(name, parent); rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>(); img.color = hot ? new Color(.09f, .04f, .012f, .96f) : new Color(.02f, .04f, .05f, .9f);
            var outline = rt.gameObject.AddComponent<Outline>(); outline.effectColor = hot ? Orange : new Color(.1f, .28f, .32f, .95f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var colors = b.colors; colors.highlightedColor = new Color(1.5f, 1.25f, 1f, 1); colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.8f, .8f, .8f, 1); b.colors = colors;
            var t = Text("Label", rt, label, 20, hot ? new Color(1, .9f, .78f) : new Color(.78f, .86f, .88f), Vector2.zero, size, TextAlignmentOptions.Center, false);
            Stretch((RectTransform)t.transform);
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        // ---------------------------------------------------------------- content

        void Rebuild()
        {
            ClearColumn(columnA); ClearColumn(columnB);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                var o = tabButtons[i].GetComponent<Outline>(); o.effectColor = i == (int)tab ? Orange : new Color(.1f, .28f, .32f, .95f);
                tabButtons[i].GetComponentInChildren<TMP_Text>().color = i == (int)tab ? new Color(1, .9f, .78f) : new Color(.78f, .86f, .88f);
            }
            subtitle.text = tab == Tab.Settings ? "Ajustes de entrada" :
                "Clic en una casilla y pulsa la nueva tecla · Esc cancela · Esc y P (pausa) son fijas · Rueda: más acciones";
            if (tab == Tab.Settings) { BuildSettings(); FitContent(); return; }

            string group = tab == Tab.Keyboard ? ControlHints.KeyboardGroup : ControlHints.GamepadGroup;
            string lastCategory = null; RectTransform target = columnA;
            if (tab == Tab.Gamepad) { Header(columnA, ControlsRebinder.Movement); Fixed(columnA, "Mover", "STICK IZQ."); Fixed(columnA, "Mirar", "STICK DER."); lastCategory = ControlsRebinder.Movement; }
            foreach (var row in ControlsRebinder.Rows)
            {
                if (tab == Tab.Gamepad && row.Part != null) continue;
                target = row.Category == ControlsRebinder.Combat ? columnB : columnA;
                if (row.Category != lastCategory) { Header(target, row.Category); lastCategory = row.Category; }
                Row(target, row, group);
            }
            FitContent();
        }

        static void ClearColumn(RectTransform column)
        {
            for (int i = column.childCount - 1; i >= 0; i--) { var c = column.GetChild(i); c.SetParent(null, false); Destroy(c.gameObject); }
        }

        void Header(RectTransform column, string text)
        {
            var rt = Child("Header", column); rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            var t = Text("T", rt, text, 18, Orange, new Vector2(0, -8), new Vector2(600, 24), TextAlignmentOptions.Left, true);
            Stretch((RectTransform)t.transform); ((RectTransform)t.transform).offsetMin = new Vector2(4, 0);
        }

        void Fixed(RectTransform column, string label, string value)
        {
            var rt = Child("Fixed_" + label, column); rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); h.childForceExpandWidth = false; h.childControlWidth = true; h.childControlHeight = true; h.padding = new RectOffset(8, 8, 3, 3); h.spacing = 8;
            var name = SlotLabel(rt, label, 18, Color.white); FlexLabel(name);
            var v = SlotLabel(rt, value, 17, Dim); v.gameObject.AddComponent<LayoutElement>().preferredWidth = 190; v.alignment = TextAlignmentOptions.Center;
        }

        static void FlexLabel(TMP_Text t) { var le = t.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.preferredWidth = 0; le.minWidth = 0; }

        TMP_Text SlotLabel(RectTransform parent, string text, float size, Color color)
        {
            var rt = Child("L", parent); var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = color; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.MidlineLeft; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis; if (font) t.font = font; return t;
        }

        void Row(RectTransform column, ControlsRebinder.Row row, string group)
        {
            var action = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/" + row.Action, false) : null;
            var rt = Child("Row_" + row.Label, column); rt.gameObject.AddComponent<LayoutElement>().preferredHeight = row.Hint != null ? 56 : 44;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); h.childForceExpandWidth = false; h.childControlWidth = true; h.childControlHeight = true; h.padding = new RectOffset(8, 8, 3, 3); h.spacing = 8;
            var name = SlotLabel(rt, row.Hint != null ? row.Label + "\n<size=13><color=#8fa8b0>" + row.Hint + "</color></size>" : row.Label, 18, Color.white);
            FlexLabel(name);
            for (int slot = 0; slot < ControlsRebinder.Slots; slot++)
            {
                int index = action != null ? ControlsRebinder.SlotIndex(action, group, row.Part, slot) : -1;
                string label = action != null ? ControlsRebinder.Display(action, index) : "?";
                var b = MakeButton(rt, "Slot" + slot, label, Vector2.zero, new Vector2(190, 38), null);
                b.gameObject.AddComponent<LayoutElement>().preferredWidth = 190;
                var text = b.GetComponentInChildren<TMP_Text>(); text.color = label == "—" ? Dim : Cyan;
                if (action == null || index < 0) { b.interactable = false; continue; }
                var captured = text; var a = action; int i = index; string g = group;
                b.onClick.AddListener(() => StartCapture(a, i, g, captured));
            }
        }

        // ---------------------------------------------------------------- capture and conflicts

        void StartCapture(InputAction action, int index, string group, TMP_Text label)
        {
            CancelCapture(); ClearConflict();
            waitingText = label; label.text = "PULSA TECLA…"; label.color = Orange; SetStatus("");
            operation = ControlsRebinder.Begin(action, index, group, path =>
            {
                operation = null;
                if (!this || string.IsNullOrEmpty(path)) { Rebuild(); return; }
                var asset = InputSystem.actions;
                if (ControlsRebinder.FindConflict(asset, group, action, index, path, out var other, out int otherIndex))
                {
                    pendingAction = action; pendingIndex = index; pendingPath = path; pendingOther = other; pendingOtherIndex = otherIndex;
                    conflictText.text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant() + " ya la usa «" + ControlsRebinder.Label(other.name, other.bindings[otherIndex].isPartOfComposite ? other.bindings[otherIndex].name : null) + "». ¿Intercambiar las dos?";
                    swapButton.gameObject.SetActive(ControlsRebinder.CanSwap(action, index)); conflictBar.SetActive(true); Rebuild(); return;
                }
                ControlsRebinder.Assign(asset, action, index, path); SetStatus("Guardado"); Rebuild();
            }, () => { operation = null; backConsumedFrame = Time.frameCount; if (this) Rebuild(); });
        }

        void CancelCapture()
        {
            if (operation == null) return;
            var op = operation; operation = null;
            op.Cancel(); // OnCancel disposes and re-enables the action
        }

        void ApplySwap()
        {
            if (pendingAction == null) return;
            ControlsRebinder.Swap(InputSystem.actions, pendingAction, pendingIndex, pendingPath, pendingOther, pendingOtherIndex);
            ClearConflict(); SetStatus("Intercambiadas"); Rebuild();
        }

        void ClearConflict()
        {
            pendingAction = pendingOther = null; if (conflictBar) conflictBar.SetActive(false);
        }

        void SetStatus(string text) { if (status) status.text = text; }

        int handledFrame = -1;

        // Main menu has no pause manager to route Esc here: when nobody called HandleBack this frame, handle it.
        void LateUpdate()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsOpen && handledFrame != Time.frameCount) HandleBack();
        }

        void Update()
        {
            if (waitingText && operation != null) waitingText.alpha = .45f + .55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.5f));
            else if (waitingText) { waitingText.alpha = 1; waitingText = null; }
        }

#if UNITY_EDITOR
        /// Editor-only helper for documentation captures: "list", "wait", "conflict", "reset", "gamepad", "settings".
        public static void Preview(string mode)
        {
            Open(); var p = instance; if (mode != "conflict") p.ClearConflict();
            var melee = InputSystem.actions.FindAction("Player/Melee", true); int slot = ControlsRebinder.SlotIndex(melee, ControlHints.KeyboardGroup, null, 0);
            switch (mode)
            {
                case "wait":
                    TMP_Text label = null;
                    foreach (Transform row in p.columnB) if (row.name == "Row_Golpe / carga") label = row.Find("Slot0").GetComponentInChildren<TMP_Text>();
                    p.StartCapture(melee, slot, ControlHints.KeyboardGroup, label); break;
                case "conflict":
                    var crouch = InputSystem.actions.FindAction("Player/Crouch", true);
                    p.pendingAction = melee; p.pendingIndex = slot; p.pendingPath = "<Keyboard>/c"; p.pendingOther = crouch; p.pendingOtherIndex = ControlsRebinder.SlotIndex(crouch, ControlHints.KeyboardGroup, null, 0);
                    p.conflictText.text = "C ya la usa «Agacharse / Slide». ¿Intercambiar las dos?"; p.conflictBar.SetActive(true); break;
                case "reset": ControlsRebinder.ResetAll(InputSystem.actions); p.SetStatus("Controles restablecidos"); p.Rebuild(); break;
                case "gamepad": p.tab = Tab.Gamepad; p.Rebuild(); break;
                case "settings": p.tab = Tab.Settings; p.Rebuild(); break;
            }
        }
#endif

        // ---------------------------------------------------------------- settings tab

        void BuildSettings()
        {
            Header(columnA, "ENTRADA");
            Toggle(columnA, "Invertir eje Y", () => { var h = FindAnyObjectByType<Unity.FPS.Gameplay.PlayerInputHandler>(); return RequiemSettings.InvertY(h ? h.InvertYAxis : false); }, v => { RequiemSettings.SetInvertY(v); var p = FindAnyObjectByType<Unity.FPS.Gameplay.PlayerInputHandler>(); if (p) p.InvertYAxis = v; }, "SÍ", "NO");
            Header(columnA, "VISIÓN");
            Toggle(columnA, "Resaltar enemigos en la oscuridad", () => RequiemSettings.VisionIntensity > 0, v => RequiemSettings.SetVisionIntensity(v ? 1 : 0), "SÍ", "NO");
            Toggle(columnA, "Contornos siempre activos", () => RequiemSettings.VisionOutlinesAlways, RequiemSettings.SetVisionOutlinesAlways, "SÍ", "NO");
            Header(columnB, "NOTAS");
            Note(columnB, "La sensibilidad del ratón está en OPCIONES.");
            Note(columnB, "Esc y P siempre pausan; no se pueden reasignar.");
            Note(columnB, "Visión: el borde de los enemigos vivos se ilumina más cuanto más oscuro está su entorno.");
        }

        void Toggle(RectTransform column, string label, System.Func<bool> get, System.Action<bool> set, string on, string off)
        {
            var rt = Child("Toggle_" + label, column); rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); h.childForceExpandWidth = false; h.childControlWidth = true; h.childControlHeight = true; h.padding = new RectOffset(8, 8, 3, 3); h.spacing = 8;
            FlexLabel(SlotLabel(rt, label, 18, Color.white));
            var b = MakeButton(rt, "Value", get() ? on : off, Vector2.zero, new Vector2(170, 38), null);
            b.gameObject.AddComponent<LayoutElement>().preferredWidth = 170;
            var t = b.GetComponentInChildren<TMP_Text>(); t.color = Cyan;
            b.onClick.AddListener(() => { set(!get()); t.text = get() ? on : off; SetStatus("Guardado"); });
        }

        void Note(RectTransform column, string text)
        {
            var rt = Child("Note", column); rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var t = SlotLabel(rt, text, 17, Dim); Stretch((RectTransform)t.transform); t.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}
