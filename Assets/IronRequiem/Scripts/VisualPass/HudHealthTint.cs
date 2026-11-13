using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Colors the health fill by ratio and pulses softly below the critical ratio. Zero alloc.
    public sealed class HudHealthTint : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] Graphic frame;
        [SerializeField] Color healthy = new Color(1f, .52f, .12f, 1);
        [SerializeField] Color hurt = new Color(.95f, .16f, .06f, 1);
        [SerializeField, Range(0, 1)] float critical = .25f;
        Color frameBase;

        public void Configure(Image fillImage, Graphic frameGraphic) { fill = fillImage; frame = frameGraphic; }
        void OnEnable() { if (frame) frameBase = frame.color; }

        void LateUpdate()
        {
            if (!fill) return;
            float r = fill.fillAmount;
            Color c = Color.Lerp(hurt, healthy, Mathf.InverseLerp(critical, .7f, r));
            float pulse = 0;
            if (r <= critical && r > 0) pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 7f);
            fill.color = Color.Lerp(c, Color.white, pulse * .25f);
            if (frame) frame.color = Color.Lerp(frameBase, new Color(.5f, .05f, .03f, frameBase.a), pulse * .6f);
        }
    }
}
