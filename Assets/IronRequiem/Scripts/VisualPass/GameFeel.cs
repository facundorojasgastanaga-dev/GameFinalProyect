using UnityEngine;

namespace IronRequiem
{
    /// F4 game feel hub: player screen-shake scale (persisted, Options slider) and short hit-stop.
    /// Hit-stop never fights the pause menu: it only starts at normal speed and only restores its own time scale.
    public static class GameFeel
    {
        const string ShakeKey = "IR_ScreenShake";
        static float shake = -1, until, saved = -1, applied, slowSeconds, slowScale;
        static Runner runner;

        public static float ShakeScale
        {
            get { if (shake < 0) shake = Mathf.Clamp01(PlayerPrefs.GetFloat(ShakeKey, 1f)); return shake; }
            set { shake = Mathf.Clamp01(value); PlayerPrefs.SetFloat(ShakeKey, shake); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { shake = -1; until = 0; saved = -1; applied = 0; slowSeconds = 0; runner = null; }

        public static void HitStop(float seconds, float scale = .06f)
        {
            if (seconds <= 0) return;
            if (saved < 0)
            {
                if (Time.timeScale < .99f) return; // paused, slowed or already scripted
                if (!runner) { var go = new GameObject("VP_GameFeel"); Object.DontDestroyOnLoad(go); go.hideFlags = HideFlags.HideInHierarchy; runner = go.AddComponent<Runner>(); }
                saved = Time.timeScale; applied = scale; Time.timeScale = scale;
            }
            until = Mathf.Max(until, Time.unscaledTime + seconds);
        }

        /// v4 heavy punch: hit-stop followed by a short slow-motion tail (both restored to the saved scale).
        public static void HitStopThenSlow(float stopSeconds, float stopScale, float tailSeconds, float tailScale)
        {
            HitStop(stopSeconds, stopScale);
            if (saved >= 0 && tailSeconds > 0) { slowSeconds = tailSeconds; slowScale = Mathf.Clamp(tailScale, .05f, 1f); }
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (saved < 0 || Time.unscaledTime < until) return;
                if (slowSeconds > 0 && Mathf.Approximately(Time.timeScale, applied))
                { applied = slowScale; Time.timeScale = slowScale; until = Time.unscaledTime + slowSeconds; slowSeconds = 0; return; }
                slowSeconds = 0;
                if (Mathf.Approximately(Time.timeScale, applied)) Time.timeScale = saved;
                saved = -1;
            }
            void OnDestroy() { if (saved >= 0 && Mathf.Approximately(Time.timeScale, applied)) Time.timeScale = saved; saved = -1; }
        }
    }
}
