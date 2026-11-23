using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Industrial two-position switch for the options console (PANTALLA COMPLETA). It replaces the old lever art on an
    /// existing Toggle at run time: the lever was a "lever down" sprite as the Toggle background plus a "lever up" sprite
    /// as the checkmark, so with the toggle ON both levers were drawn at once (a ghost copy under the raised lever).
    /// The Toggle, its listeners, navigation and the saved setting are untouched; only the presentation is rebuilt here.
    /// Built in code (no scene or prefab edits). Animation uses unscaled time so it works in the pause menu; nothing is
    /// allocated per frame (Images are only recoloured while the knob travels, the label text changes only on a change).
    [RequireComponent(typeof(Toggle))]
    public sealed class OptionsSwitchVisual : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        const float HousingW = 118, HousingH = 112, TrackW = 92, TrackH = 40, KnobW = 46, KnobH = 32, Travel = 21, CenterY = 28;
        const string ContainerName = "SwitchVisual";
        static readonly Color HousingFill = new Color(.03f, .04f, .05f, .96f), RimIdle = new Color(.24f, .29f, .31f), TrackOff = new Color(.012f, .018f, .022f), TrackOn = new Color(.03f, .2f, .24f);
        static readonly Color KnobRim = new Color(.9f, .94f, .96f), KnobBody = new Color(.5f, .55f, .6f), KnobLight = new Color(.78f, .83f, .87f), Grip = new Color(.09f, .1f, .11f), LampOff = new Color(.15f, .18f, .19f);
        static readonly Color DefaultCyan = new Color(.12f, .82f, .91f), DefaultOrange = new Color(1f, .6f, .2f);

        Toggle toggle; IronRequiemUiTheme theme;
        IndustrialPanelGraphic housing; Image trackRim, trackFill, lamp; RectTransform knob; TMP_Text knobText;
        float t = -1; bool selected, shownOn;
        Color Cyan => theme ? theme.Cyan : DefaultCyan;
        Color Orange => theme ? theme.Orange : DefaultOrange;

        void Awake()
        {
            toggle = GetComponent<Toggle>(); theme = IronRequiemUiThemeLink.Load();
            HideLeverArt(); Build();
            toggle.onValueChanged.AddListener(OnChanged);
        }
        void OnEnable() { t = -1; selected = EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject; RefreshHousing(); }
        void OnDestroy() { if (toggle) toggle.onValueChanged.RemoveListener(OnChanged); }
        public void OnSelect(BaseEventData e) { selected = true; RefreshHousing(); }
        public void OnDeselect(BaseEventData e) { selected = false; RefreshHousing(); }
        void OnChanged(bool on) { if (theme) theme.Play(theme.Click, theme.ClickVolume); }

        /// The Toggle keeps its hit area (transparent, not culled) but draws nothing; the old checkmark is switched off.
        void HideLeverArt()
        {
            var check = toggle.graphic as Image; var box = toggle.targetGraphic as Image;
            toggle.graphic = null; toggle.transition = Selectable.Transition.None;
            if (check && check != box && check.gameObject != gameObject) check.gameObject.SetActive(false);
            if (!box) return;
            box.sprite = null; box.color = new Color(1, 1, 1, 0); box.raycastTarget = true; box.canvasRenderer.cullTransparentMesh = false;
            var r = box.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(HousingW, HousingH); r.anchoredPosition = new Vector2(0, CenterY); r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        }

        void Build()
        {
            if (transform.Find(ContainerName)) return;
            var root = Rect(ContainerName, transform, new Vector2(HousingW, HousingH), new Vector2(0, CenterY));
            housing = root.gameObject.AddComponent<IndustrialPanelGraphic>(); housing.raycastTarget = false;
            housing.SetStyle(IndustrialPanelGraphic.PanelStyle.Label); // same tape plate as the menu buttons
            var lampRect = Rect("Lamp", root, new Vector2(20, 20), new Vector2(0, 36));
            lamp = lampRect.gameObject.AddComponent<Image>(); lamp.sprite = theme ? theme.Lamp : null; lamp.raycastTarget = false;
            lampRect.gameObject.AddComponent<MenuLampFx>().Configure(MenuLampFx.Mode.Follow); // halo that follows the ON colour
            // recessed track
            trackRim = Box("TrackRim", root, new Vector2(TrackW + 4, TrackH + 4), new Vector2(0, -4), RimIdle);
            trackFill = Box("TrackFill", root, new Vector2(TrackW, TrackH), new Vector2(0, -4), TrackOff);
            // knob: rim, body, highlight, three grip grooves and the state text
            var k = Box("Knob", root, new Vector2(KnobW, KnobH), new Vector2(0, -4), KnobRim); knob = k.rectTransform;
            Box("Body", knob, new Vector2(KnobW - 4, KnobH - 4), Vector2.zero, KnobBody);
            Box("Light", knob, new Vector2(KnobW - 4, 3), new Vector2(0, KnobH * .5f - 3.5f), KnobLight);
            for (int i = 0; i < 2; i++) Box("Grip", knob, new Vector2(2, KnobH - 14), new Vector2(i == 0 ? -(KnobW * .5f - 8) : KnobW * .5f - 8, -1), Grip);
            var label = Rect("State", knob, new Vector2(KnobW - 18, KnobH - 4), new Vector2(0, -1));
            knobText = Text(label, "OFF", 13, new Color(.06f, .07f, .08f));
            shownOn = !toggle.isOn; Apply(toggle.isOn ? 1 : 0); RefreshHousing();
        }

        void Update()
        {
            float target = toggle.isOn ? 1 : 0;
            float next = t < 0 ? target : Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 9f);
            if (Mathf.Approximately(next, t)) return;
            t = next; Apply(t * t * (3 - 2 * t));
        }

        void Apply(float e)
        {
            if (!knob) return;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-Travel, Travel, e), -4);
            var cyan = Cyan;
            trackFill.color = Color.Lerp(TrackOff, TrackOn, e); trackRim.color = Color.Lerp(RimIdle, cyan, e); lamp.color = Color.Lerp(LampOff, cyan, e);
            bool on = toggle.isOn;
            if (on != shownOn) { shownOn = on; knobText.text = on ? "ON" : "OFF"; }
        }

        void RefreshHousing() { if (housing) housing.Configure(HousingFill, selected ? Orange : RimIdle, 14); }

        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)); go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = pos; return r;
        }
        static Image Box(string name, Transform parent, Vector2 size, Vector2 pos, Color c)
        {
            var img = Rect(name, parent, size, pos).gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false; return img;
        }
        TMP_Text Text(RectTransform r, string text, float size, Color c)
        {
            var label = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (theme && theme.BodyFont) label.font = theme.BodyFont;
            label.text = text; label.fontSize = size; label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center; label.color = c; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.characterSpacing = 2; label.enableAutoSizing = true; label.fontSizeMax = size; label.fontSizeMin = Mathf.Max(8, size * .6f);
            return label;
        }
    }
}
