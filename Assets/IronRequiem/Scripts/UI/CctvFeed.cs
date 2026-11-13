using UnityEngine;

namespace IronRequiem
{
    /// A security camera that options monitors can show (12/11/2026). The camera stays disabled; CctvMonitor renders it
    /// by hand into a small render texture at a low frame rate, only while a monitor is visible.
    [RequireComponent(typeof(Camera))]
    public sealed class CctvFeed : MonoBehaviour
    {
        [SerializeField] string label = "CAM 04 · HANGAR C-04";
        Camera cam;
        static CctvFeed active;

        public string Label => label;
        public Camera Camera => cam ? cam : cam = GetComponent<Camera>();
        /// The feed of the currently loaded scenes, if any (the latest one enabled wins).
        public static CctvFeed Active => active;

        void Awake() { cam = GetComponent<Camera>(); cam.enabled = false; }
        void OnEnable() => active = this;
        void OnDisable() { if (active == this) active = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ClearActive() => active = null;
    }
}
