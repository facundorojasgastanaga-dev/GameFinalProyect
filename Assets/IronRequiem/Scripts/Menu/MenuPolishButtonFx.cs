using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IronRequiem
{
    /// Main-menu-only feedback. The button's hit rectangle never moves or scales.
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class MenuPolishButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        public enum VisualState { Rest, Hover, Selected, Pressed, Disabled }
        [SerializeField] IndustrialPanelGraphic plate;
        [SerializeField] TMP_Text label;
        [SerializeField] UnityEngine.UI.Graphic indicator;
        [SerializeField] IronRequiemUiTheme theme;
        [SerializeField] bool primary, back;
        UnityEngine.UI.Button button;
        Vector2 origin;
        bool hover, selected, held;
        float blend = -1, pressUntil, enabledAt;
        VisualState previous = (VisualState)(-1);
        static float lastNavigation, lastConfirmation;
        static readonly Color Graphite = new Color(.033f, .041f, .046f, .96f);
        static readonly Color Ivory = new Color(.91f, .89f, .80f);
        static readonly Color Orange = new Color(1, .40f, .08f);
        static readonly Color Cyan = new Color(.13f, .84f, .92f);
        public VisualState State { get; private set; }
        public bool IsPrimary => primary;

        public void Configure(IndustrialPanelGraphic face, TMP_Text text, UnityEngine.UI.Graphic light,
            IronRequiemUiTheme audioTheme, bool isPrimary, bool isBack)
        { plate = face; label = text; indicator = light; theme = audioTheme; primary = isPrimary; back = isBack; }

        void Awake()
        {
            button = GetComponent<UnityEngine.UI.Button>();
            if (button.targetGraphic) button.targetGraphic.raycastTarget = true;
            if (label) origin = label.rectTransform.anchoredPosition;
            button.onClick.AddListener(Confirm);
        }
        void OnEnable() { enabledAt = Time.unscaledTime; blend = -1; previous = (VisualState)(-1); }
        void OnDisable() { hover = selected = held = false; pressUntil = 0; if (label) label.rectTransform.anchoredPosition = origin; }
        void OnDestroy() { if (button) button.onClick.RemoveListener(Confirm); }
        bool Enabled => button && button.IsInteractable();
        public void OnPointerEnter(PointerEventData e)
        {
            if (!Enabled) return;
            hover = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnPointerExit(PointerEventData e) => hover = false;
        public void OnSelect(BaseEventData e)
        {
            if (!selected && Enabled && Time.unscaledTime - enabledAt > .2f && Time.unscaledTime - lastNavigation > .075f)
            { lastNavigation = Time.unscaledTime; if (theme) theme.Play(theme.Hover, theme.HoverVolume * .65f); }
            selected = true;
        }
        public void OnDeselect(BaseEventData e) { selected = false; held = false; }
        public void OnPointerDown(PointerEventData e) { if (Enabled && e.button == PointerEventData.InputButton.Left) held = true; }
        public void OnPointerUp(PointerEventData e) => held = false;
        public void OnSubmit(BaseEventData e) { if (Enabled) pressUntil = Time.unscaledTime + .10f; }
        void Confirm()
        {
            if (!Enabled || Time.unscaledTime - lastConfirmation < .08f) return;
            lastConfirmation = Time.unscaledTime; pressUntil = Time.unscaledTime + .10f;
            if (theme) theme.Play(back && theme.Back ? theme.Back : theme.Click, theme.ClickVolume * .8f);
        }
        void Update()
        {
            State = !Enabled ? VisualState.Disabled : held || Time.unscaledTime < pressUntil ? VisualState.Pressed :
                hover ? VisualState.Hover : selected ? VisualState.Selected : VisualState.Rest;
            float target = State == VisualState.Disabled || State == VisualState.Rest ? 0 : 1;
            float next = blend < 0 ? target : Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime * 9);
            if (previous == State && Mathf.Approximately(next, blend)) return;
            blend = next; previous = State;
            float k = blend * blend * (3 - 2 * blend);
            bool disabled = State == VisualState.Disabled, pressed = State == VisualState.Pressed;
            Color fill = primary ? Color.Lerp(new Color(.24f,.09f,.025f,.98f), Orange, k) : Color.Lerp(Graphite, new Color(.085f,.12f,.13f,.99f), k);
            Color edge = Color.Lerp(primary ? Orange : new Color(.29f,.34f,.35f), State == VisualState.Selected ? Cyan : Orange, k);
            Color text = primary ? Color.Lerp(Ivory, Graphite, k) : Ivory;
            if (pressed) { fill = new Color(.65f,.23f,.035f); edge = Ivory; text = Ivory; }
            if (disabled) { fill = new Color(.045f,.05f,.052f,.82f); edge = new Color(.15f,.18f,.18f); text = new Color(.42f,.45f,.44f); }
            if (plate) plate.Configure(fill, edge, 8);
            if (label)
            {
                label.color = text;
                label.rectTransform.anchoredPosition = origin + new Vector2(pressed ? 2 : k * 4, 0);
            }
            if (indicator) indicator.color = disabled ? new Color(.19f,.22f,.22f) : pressed ? Ivory : State == VisualState.Selected ? Cyan : Orange;
        }
    }
}
