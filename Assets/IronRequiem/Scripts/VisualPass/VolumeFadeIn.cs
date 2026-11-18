using UnityEngine;
using UnityEngine.Rendering;

namespace IronRequiem
{
    // Fades a room's sector grade volume in when the room loads, so the colour grade does not pop.
    // 17/11/2026 (MAP_FX_AUDIT M1): the fade target is the authored weight, captured in Awake before OnEnable zeroes it,
    // and written directly (IronRequiem.Runtime references SRP Core; no reflection). In the campaign CampaignFlow's
    // ZoneEnvironment.SceneFade drives the weight itself: it Claims this component, which then stays out of the way
    // (previously both wrote the weight and SceneFade recorded the zeroed weight as "authored", leaving the grade at 0).
    public sealed class VolumeFadeIn : MonoBehaviour
    {
        [SerializeField, Min(.05f)] float seconds = 1.2f;
        Volume volume; float authored = -1, t = 1; bool claimed;
        public float AuthoredWeight { get { Capture(); return authored; } }
        void Capture() { if (authored >= 0) return; volume = GetComponent<Volume>(); authored = volume ? volume.weight : 1; }
        /// Another system takes over the weight: stop fading and return the authored weight to blend towards.
        public float Claim() { Capture(); claimed = true; t = 1; return authored; }
        void Awake() => Capture();
        void OnEnable()
        {
            Capture(); t = 1;
            if (claimed || !volume) return;
            t = 0; volume.weight = 0;
        }
        void Update()
        {
            if (t >= 1 || !volume) return;
            t = Mathf.Min(1, t + Time.unscaledDeltaTime / seconds);
            volume.weight = authored * (t * t * (3 - 2 * t));
        }
    }
}
