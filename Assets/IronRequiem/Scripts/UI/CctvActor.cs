using UnityEngine;

namespace IronRequiem
{
    /// The Carroñero that only the security camera sees (12/11/2026): crouched next to capsule C-04, it reaches into
    /// the panel and fiddles with the cables, as if it were the one that woke the protagonist up. Drives CarroneroMotion.
    public sealed class CctvActor : MonoBehaviour
    {
        [SerializeField] CarroneroMotion motion;
        [SerializeField] float period = 3.6f;
        public void Configure(CarroneroMotion pose) => motion = pose;
        void Update()
        {
            if (!motion) return;
            float t = Time.unscaledTime / period, k = t - Mathf.Floor(t);
            motion.CrouchTarget = .55f + .25f * Mathf.Sin(t * 2.1f);
            motion.ReachTarget = k < .65f ? 1f : .3f;           // reaching into the panel, then pulling back to look
            motion.DigTarget = k < .55f ? 1f : 0f;              // fiddling with the cables
        }
    }
}
