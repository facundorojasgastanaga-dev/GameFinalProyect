using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Indicator lamp of the menu consoles: a halo that spills light on the plate around it, and a fault now and then
    /// (a quick stutter, a sag, or the busy blinking of a network lamp). In Follow mode the lamp colour belongs to someone
    /// else (the fullscreen switch) and only the halo is added. Unscaled time; no allocation per frame.
    [RequireComponent(typeof(Image))]
    public sealed class MenuLampFx : MonoBehaviour
    {
        public enum Mode { Steady, Pulse, Activity, Follow }
        [SerializeField] Mode mode = Mode.Steady;
        Image lamp, halo, spill; Color baseColor; bool built;
        float level = 1, nextFault, faultEnd, nextStep; bool lit = true; int faultKind;

        public void Configure(Mode value) => mode = value;

        void Awake() { Build(); }
        void Build()
        {
            if (built) return; built = true;
            lamp = GetComponent<Image>(); baseColor = lamp.color;
            var size = ((RectTransform)transform).rect.size; if (size.x < 1) size = new Vector2(22, 22);
            spill = MenuGlowSprites.Child("IR_Spill", transform, MenuGlowSprites.Soft, baseColor); spill.rectTransform.sizeDelta = size * 6f; spill.rectTransform.SetAsFirstSibling();
            halo = MenuGlowSprites.Child("IR_Halo", transform, MenuGlowSprites.Soft, baseColor); halo.rectTransform.sizeDelta = size * 2.6f; halo.rectTransform.SetSiblingIndex(1);
        }

        void OnEnable() { nextFault = Time.unscaledTime + Random.Range(1.5f, 6f); faultEnd = 0; lit = true; level = 1; }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime, raw = 1;
            if (mode == Mode.Follow)
            {
                // brightness of whatever colour the owner gave the lamp
                var c = lamp.color; float b = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                Glow(c, Mathf.InverseLerp(.25f, .9f, b)); return;
            }
            if (mode == Mode.Pulse) raw = .72f + .28f * Mathf.Sin(now * 2.4f);
            if (mode == Mode.Activity)
            {
                // data traffic: irregular short blinks
                if (now >= nextStep) { lit = Random.value > .38f; nextStep = now + Random.Range(.05f, lit ? .32f : .12f); }
                raw = lit ? 1 : .18f;
            }
            if (now >= nextFault && faultEnd <= 0) { faultKind = Random.Range(0, 3); faultEnd = now + (faultKind == 2 ? Random.Range(.5f, 1.1f) : Random.Range(.18f, .45f)); nextStep = 0; }
            if (faultEnd > 0)
            {
                if (now >= faultEnd) { faultEnd = 0; nextFault = now + Random.Range(3.5f, 11f); }
                else if (faultKind == 2) raw *= .35f + .1f * Mathf.Sin(now * 40f);                       // sag
                else { if (now >= nextStep) { lit = Random.value > .5f; nextStep = now + Random.Range(.03f, .09f); } raw *= lit ? 1.15f : .06f; } // stutter
            }
            // a filament does not snap off: quick rise, short decay
            level = raw > level ? raw : Mathf.Lerp(level, raw, 1 - Mathf.Exp(-dt * 30));
            var col = baseColor * Mathf.Lerp(.22f, 1f, Mathf.Clamp01(level)); col.a = baseColor.a; lamp.color = col;
            Glow(baseColor, level);
        }

        void Glow(Color c, float k)
        {
            if (!halo) return;
            c.a = Mathf.Clamp01(.62f * k); halo.color = c;
            c.a = Mathf.Clamp01(.16f * k); spill.color = c;
        }
    }
}
