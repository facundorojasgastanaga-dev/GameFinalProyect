using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Plate behind a HUD message: hidden when text empty, short punch when text changes. Zero alloc.
    public sealed class HudBannerPlate : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Graphic plate;
        [SerializeField, Min(.05f)] float punchSeconds = .28f;
        [SerializeField] float punchScale = .14f;
        string last; float t = 1;

        public void Configure(TMP_Text text, Graphic backing) { label = text; plate = backing; }

        void LateUpdate()
        {
            if (!label || !plate) return;
            string s = label.text;
            bool show = label.isActiveAndEnabled && !string.IsNullOrEmpty(s);
            if (plate.enabled != show) plate.enabled = show;
            if (!ReferenceEquals(s, last)) { if (show && s != last) t = 0; last = s; }
            if (t < 1)
            {
                t = Mathf.Min(1, t + Time.unscaledDeltaTime / punchSeconds);
                float k = 1 + punchScale * (1 - t) * (1 - t);
                label.rectTransform.localScale = new Vector3(k, k, 1);
                plate.rectTransform.localScale = new Vector3(1 + (k - 1) * .4f, 1, 1);
            }
        }
    }
}
