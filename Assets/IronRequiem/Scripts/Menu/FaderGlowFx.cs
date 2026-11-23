using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Options console fader: the higher the lever, the stronger its light. The fill gets brighter, a halo grows around
    /// the lit column and behind the cap, and the read-out above brightens with it. Presentation only: the Slider, its
    /// listeners and the saved value are untouched. Unscaled time, so it also works in the pause menu.
    [RequireComponent(typeof(Slider))]
    public sealed class FaderGlowFx : MonoBehaviour
    {
        static readonly Color Dim = new Color(.05f, .30f, .36f, .85f), Bright = new Color(.55f, 1f, 1f, 1f), Halo = new Color(.13f, .84f, .92f);
        static readonly Color ValueDim = new Color(.16f, .50f, .56f), ValueBright = new Color(.62f, 1f, 1f);
        Slider slider; Image fill, columnGlow, capGlow; RectTransform columnRect, capRect; TMP_Text value;
        float shown = -1, seed;

        void Awake()
        {
            slider = GetComponent<Slider>(); seed = transform.parent ? transform.parent.GetSiblingIndex() * 7.3f : 0;
            if (slider.fillRect)
            {
                fill = slider.fillRect.GetComponent<Image>();
                columnGlow = MenuGlowSprites.Child("IR_FillGlow", slider.fillRect.parent, MenuGlowSprites.Soft, Halo);
                columnRect = columnGlow.rectTransform; columnRect.SetAsFirstSibling();
            }
            if (slider.handleRect)
            {
                capGlow = MenuGlowSprites.Child("IR_CapGlow", slider.handleRect.parent, MenuGlowSprites.Soft, Halo);
                capRect = capGlow.rectTransform; capRect.SetAsFirstSibling();
            }
            var block = transform.parent; var v = block ? block.Find("Value") : null; if (v) value = v.GetComponent<TMP_Text>();
        }

        void OnEnable() => shown = -1;

        void Update()
        {
            float target = slider.normalizedValue;
            shown = shown < 0 ? target : Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 4f);
            float v = shown, e = Mathf.Pow(v, .8f);
            // a lit tube is never perfectly steady: the shimmer grows with the level
            float shimmer = 1 + (Mathf.PerlinNoise(Time.unscaledTime * 9f, seed) - .5f) * .22f * v;
            if (fill) fill.color = Color.Lerp(Dim, Bright, e) * new Color(1, 1, 1, 1);
            if (columnRect)
            {
                columnRect.anchorMin = new Vector2(.5f, 0); columnRect.anchorMax = new Vector2(.5f, Mathf.Max(.02f, v)); columnRect.pivot = new Vector2(.5f, .5f);
                float w = Mathf.Lerp(18, 64, e);
                columnRect.offsetMin = new Vector2(-w * .5f, -14); columnRect.offsetMax = new Vector2(w * .5f, 14);
                var c = Halo; c.a = Mathf.Lerp(.05f, .85f, e) * shimmer; columnGlow.color = c;
            }
            if (capRect)
            {
                capRect.anchorMin = capRect.anchorMax = new Vector2(.5f, v); capRect.pivot = new Vector2(.5f, .5f);
                float s = Mathf.Lerp(70, 150, e); capRect.sizeDelta = new Vector2(s, s * .8f); capRect.anchoredPosition = Vector2.zero;
                var c = Halo; c.a = Mathf.Lerp(.04f, .6f, e) * shimmer; capGlow.color = c;
            }
            if (value) value.color = Color.Lerp(ValueDim, ValueBright, e);
        }
    }
}
