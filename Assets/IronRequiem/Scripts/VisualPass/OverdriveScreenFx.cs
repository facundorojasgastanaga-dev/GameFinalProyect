using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// OVERDRIVE screen state: orange edge vignette that pulses faster as the module nears detonation.
    /// Edge-only sprite, never covers the centre of the view. Zero alloc.
    [RequireComponent(typeof(Image))]
    public sealed class OverdriveScreenFx : MonoBehaviour
    {
        [SerializeField] Color color = new Color(1f, .42f, .05f, 1f);
        [SerializeField, Range(0, 1)] float minAlpha = .10f, maxAlpha = .32f;
        [SerializeField, Min(.1f)] float fadeSeconds = .25f;
        StolenWeaponCarrier carrier;
        Image image;
        float weight;

        void Awake() { image = GetComponent<Image>(); image.raycastTarget = false; Paint(0); }
        void Start() { carrier = FindAnyObjectByType<StolenWeaponCarrier>(); }

        void LateUpdate()
        {
            var held = carrier ? carrier.Held : null;
            bool armed = held && held.Overdrive && held.Overdrive.IsArmed;
            weight = Mathf.MoveTowards(weight, armed ? 1 : 0, Time.unscaledDeltaTime / fadeSeconds);
            if (weight <= 0) { if (image.enabled) image.enabled = false; return; }
            if (!image.enabled) image.enabled = true;
            float danger = armed ? Mathf.Clamp01(1 - held.Overdrive.RemainingDangerSeconds / Mathf.Max(.1f, held.Overdrive.CriticalGraceDuration)) : 1;
            float hz = Mathf.Lerp(2.2f, 7f, danger);
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * hz * 6.2832f);
            Paint(weight * Mathf.Lerp(minAlpha, maxAlpha, pulse) * Mathf.Lerp(.75f, 1.15f, danger));
        }

        void Paint(float a) { var c = color; c.a = a; image.color = c; }
    }
}
