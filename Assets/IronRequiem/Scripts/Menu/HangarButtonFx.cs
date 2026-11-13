using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Iron Requiem button look, shared by every screen (main menu, options, credits, pause, death): dark cyan plate
    /// at rest; when selected (mouse hover selects, so mouse and pad agree) it turns into the orange "armed" state
    /// with a left accent bar and the label nudged right. Press gives a short squash; hover and click play the theme
    /// sounds (unscaled time and listener-pause safe, so it works in the pause menu). Presentation only.
    [RequireComponent(typeof(Selectable))]
    public sealed class HangarButtonFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] IndustrialPanelGraphic face;
        [SerializeField] TMP_Text label;
        [SerializeField] Graphic accent, glow;
        [SerializeField] IronRequiemUiTheme theme;
        [SerializeField] bool backButton;
        [SerializeField] Color restFill = new Color(.02f, .04f, .05f, .72f), restRim = new Color(.1f, .28f, .32f, .9f);
        [SerializeField] Color hotFill = new Color(.09f, .04f, .012f, .92f), hotRim = new Color(1f, .55f, .14f, 1f);
        [SerializeField] Color restText = new Color(.78f, .86f, .88f), hotText = new Color(1f, .9f, .78f);
        [SerializeField] float nudge = 12;
        [SerializeField] bool armedWhenIdle;
        Selectable selectable; bool selected, pressed; float k = -1, press, enabledAt; Vector2 labelBase; Vector3 baseScale = Vector3.one;
        /// The default button (JUGAR) looks armed while nothing at all is selected, like the reference screen.
        public void SetDefault(bool value) => armedWhenIdle = value;

        public void Configure(IndustrialPanelGraphic plate, TMP_Text text, Graphic bar, Graphic halo) { face = plate; label = text; accent = bar; glow = halo; }
        public void ConfigureTheme(IronRequiemUiTheme shared, bool isBack) { theme = shared; backButton = isBack; }

        Color RestFill => theme ? theme.ButtonRestFill : restFill;
        Color RestRim => theme ? theme.ButtonRestRim : restRim;
        Color HotFill => theme ? theme.ButtonHotFill : hotFill;
        Color HotRim => theme ? theme.ButtonHotRim : hotRim;
        Color RestText => theme ? theme.ButtonRestText : restText;
        Color HotText => theme ? theme.ButtonHotText : hotText;

        void Awake() { selectable = GetComponent<Selectable>(); if (label) labelBase = label.rectTransform.anchoredPosition; baseScale = transform.localScale; }
        void OnEnable() { selected = EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject; k = -1; press = 0; pressed = false; enabledAt = Time.unscaledTime; transform.localScale = baseScale; }

        bool Interactable => !selectable || selectable.interactable;
        public void OnPointerEnter(PointerEventData e) { if (Interactable && EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject); }
        public void OnSelect(BaseEventData e)
        {
            // the selection a menu makes when it opens is silent; hover / pad navigation afterwards ticks
            if (!selected && theme && Interactable && Time.unscaledTime - enabledAt > .15f) theme.Play(theme.Hover, theme.HoverVolume);
            selected = true;
        }
        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerDown(PointerEventData e) { if (Interactable) pressed = true; }
        public void OnPointerUp(PointerEventData e) => pressed = false;
        public void OnPointerClick(PointerEventData e) { if (Interactable && e.button == PointerEventData.InputButton.Left) Clicked(); }
        public void OnSubmit(BaseEventData e) { if (Interactable) { Clicked(); press = 1; } }
        void Clicked() { if (theme) theme.Play(backButton && theme.Back ? theme.Back : theme.Click, theme.ClickVolume); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            press = Mathf.MoveTowards(press, pressed ? 1 : 0, dt * (pressed ? 18 : 6));
            float squash = theme ? theme.ButtonPressScale : .97f;
            transform.localScale = baseScale * Mathf.Lerp(1, squash, press);
            bool idle = armedWhenIdle && EventSystem.current && EventSystem.current.currentSelectedGameObject == null;
            float target = (selected || idle) && Interactable ? 1 : 0;
            float prev = k;
            k = k < 0 ? target : Mathf.MoveTowards(k, target, dt * 7);
            if (Mathf.Approximately(prev, k)) return;
            float e = k * k * (3 - 2 * k);
            if (face) face.Configure(Color.Lerp(RestFill, HotFill, e), Color.Lerp(RestRim, HotRim, e), theme ? theme.ButtonCorner : 14);
            if (label) { label.color = Color.Lerp(RestText, HotText, e); label.rectTransform.anchoredPosition = labelBase + new Vector2(nudge * e, 0); }
            if (accent) { var c = accent.color; c.a = e; accent.color = c; accent.rectTransform.localScale = new Vector3(1, Mathf.Lerp(.3f, 1, e), 1); }
            if (glow) { var c = glow.color; c.a = .14f * e; glow.color = c; }
        }
    }
}
