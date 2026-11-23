using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Light for the main-menu tape buttons, on top of MenuPolishButtonFx (which keeps owning colours, sounds and the
    /// label nudge): a glow around the edge of the armed button, a sheen that crosses it, and a halo on its indicator.
    /// JUGAR breathes softly even at rest. The hit rectangle never changes. Unscaled time.
    /// 20/11: also used by the pause buttons, which have no MenuPolishButtonFx; there the state comes from the selection.
    [RequireComponent(typeof(Selectable))]
    public sealed class MenuButtonLightFx : MonoBehaviour
    {
        static readonly Color Orange = new Color(1f, .42f, .08f), Cyan = new Color(.13f, .84f, .92f), Ivory = new Color(1f, .95f, .82f);
        MenuPolishButtonFx fx; Selectable selectable; Image edge, sheen, halo; RectTransform sheenRect; Graphic indicator;
        float k = -1, sheenAt = -1, nextSheen, width; bool built;

        void Awake() { Build(); }
        void Build()
        {
            if (built) return; built = true;
            fx = GetComponent<MenuPolishButtonFx>(); selectable = GetComponent<Selectable>();
            var old = transform.Find("IR_EdgeGlow"); if (old) Destroy(old.gameObject);           // a clone of a lit button rebuilds its own lights
            old = transform.Find("IR_Sheen"); if (old) Destroy(old.gameObject);
            edge = MenuGlowSprites.Child("IR_EdgeGlow", transform, MenuGlowSprites.Ring, Orange, true);
            var er = edge.rectTransform; er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one; er.offsetMin = -Vector2.one * MenuGlowSprites.RingInset; er.offsetMax = Vector2.one * MenuGlowSprites.RingInset; er.SetAsFirstSibling();
            // sheen: clipped to the button, drawn above the plate and below the label
            var clip = new GameObject("IR_Sheen", typeof(RectTransform), typeof(RectMask2D)); clip.layer = gameObject.layer;
            var cr = (RectTransform)clip.transform; cr.SetParent(transform, false); cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = new Vector2(3, 3); cr.offsetMax = new Vector2(-3, -3);
            var face = transform.Find("IndustrialButtonFace"); cr.SetSiblingIndex(face ? face.GetSiblingIndex() + 1 : 1);
            sheen = MenuGlowSprites.Child("Band", cr, MenuGlowSprites.Band, new Color(1, 1, 1, 0)); sheenRect = sheen.rectTransform;
            sheenRect.sizeDelta = new Vector2(110, 400); sheenRect.localRotation = Quaternion.Euler(0, 0, -22);
            var ind = transform.Find("PolishIndicator");
            if (ind)
            {
                indicator = ind.GetComponent<Graphic>();
                var h = ind.Find("IR_Halo"); if (h) Destroy(h.gameObject);
                halo = MenuGlowSprites.Child("IR_Halo", ind, MenuGlowSprites.Soft, Orange); halo.rectTransform.sizeDelta = new Vector2(34, 58);
            }
        }

        void OnEnable() { k = -1; sheenAt = -1; nextSheen = Time.unscaledTime + .35f; if (sheen) sheen.color = new Color(1, 1, 1, 0); }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            var state = MenuPolishButtonFx.VisualState.Rest; bool primary = false;
            if (fx) { state = fx.State; primary = fx.IsPrimary; }
            else
            {
                var system = UnityEngine.EventSystems.EventSystem.current;
                if (selectable && !selectable.IsInteractable()) state = MenuPolishButtonFx.VisualState.Disabled;
                else if (system && system.currentSelectedGameObject == gameObject) state = MenuPolishButtonFx.VisualState.Hover; // the tape turns orange when selected
            }
            bool armed = state == MenuPolishButtonFx.VisualState.Hover || state == MenuPolishButtonFx.VisualState.Selected || state == MenuPolishButtonFx.VisualState.Pressed;
            bool disabled = state == MenuPolishButtonFx.VisualState.Disabled;
            float target = disabled ? 0 : armed ? 1 : primary ? .38f : 0;
            k = k < 0 ? target : Mathf.MoveTowards(k, target, dt * 6);
            float pulse = .5f + .5f * Mathf.Sin(now * (armed ? 5.2f : 2.1f));
            var tint = primary || state != MenuPolishButtonFx.VisualState.Selected ? Orange : Cyan;
            if (state == MenuPolishButtonFx.VisualState.Pressed) tint = Ivory;
            tint.a = k * (state == MenuPolishButtonFx.VisualState.Pressed ? 1f : Mathf.Lerp(.5f, .8f, pulse)); edge.color = tint;
            if (halo && indicator) { var c = indicator.color; c.a = disabled ? 0 : Mathf.Lerp(.25f, .55f + .35f * pulse, k); halo.color = c; }

            // sheen: right away when the button arms, then every few seconds while it stays armed (JUGAR also at rest)
            width = ((RectTransform)transform).rect.width;
            bool wants = !disabled && (armed || primary);
            if (!wants) { sheenAt = -1; nextSheen = now + .15f; }
            else if (sheenAt < 0 && now >= nextSheen) sheenAt = now;
            if (sheenAt >= 0)
            {
                float p = (now - sheenAt) / .55f;
                if (p >= 1) { sheenAt = -1; nextSheen = now + (armed ? 2.6f : 5.5f); sheen.color = new Color(1, 1, 1, 0); }
                else
                {
                    sheenRect.anchoredPosition = new Vector2(Mathf.Lerp(-width * .5f - 90, width * .5f + 90, p), 0);
                    sheen.color = new Color(1, .97f, .9f, (armed ? .30f : .16f) * Mathf.Sin(p * Mathf.PI));
                }
            }
        }
    }
}
