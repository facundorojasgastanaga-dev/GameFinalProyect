using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    /// 12/11/2026 v3 — smooth zone changes for CampaignFlow. Every stage scene carries its own RenderSettings (fog,
    /// ambient light, reflection intensity) and may carry global post-processing Volumes and ambience AudioSources.
    /// Switching the active scene used to snap all of that in one frame; these helpers capture the settings and blend
    /// them, fade a scene's global volumes and its looping ambience in or out. No allocation per frame (buffers reused).
    public static class ZoneEnvironment
    {
        public struct Settings
        {
            public bool fog; public FogMode fogMode; public Color fogColor; public float fogDensity, fogStart, fogEnd;
            public AmbientMode ambientMode; public Color ambientSky, ambientEquator, ambientGround; public float ambientIntensity, reflectionIntensity;
        }

        public static Settings Capture() => new Settings
        {
            fog = RenderSettings.fog, fogMode = RenderSettings.fogMode, fogColor = RenderSettings.fogColor, fogDensity = RenderSettings.fogDensity,
            fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance, ambientMode = RenderSettings.ambientMode,
            ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor, ambientGround = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity, reflectionIntensity = RenderSettings.reflectionIntensity,
        };

        /// Applies a blend of two captured settings (t 0..1). Fog without fog is treated as zero-density fog of the same
        /// colour, so turning fog on or off fades instead of popping.
        public static void Apply(in Settings a, in Settings b, float t)
        {
            t = Mathf.Clamp01(t);
            bool anyFog = a.fog || b.fog;
            RenderSettings.fog = anyFog && (t < 1 ? true : b.fog);
            var mode = b.fog ? b.fogMode : a.fogMode; RenderSettings.fogMode = mode;
            RenderSettings.fogColor = Color.Lerp(a.fog ? a.fogColor : b.fogColor, b.fog ? b.fogColor : a.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(a.fog ? a.fogDensity : 0, b.fog ? b.fogDensity : 0, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(a.fogStart, b.fogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(a.fog ? a.fogEnd : 1000, b.fog ? b.fogEnd : 1000, t);
            // flat/trilight colours blend; the mode itself switches once (trilight with equal colours looks flat)
            RenderSettings.ambientMode = t < .5f ? a.ambientMode : b.ambientMode;
            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(Equator(a), Equator(b), t);
            RenderSettings.ambientGroundColor = Color.Lerp(Ground(a), Ground(b), t);
            RenderSettings.ambientLight = RenderSettings.ambientSkyColor;
            RenderSettings.ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t);
            RenderSettings.reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t);
        }
        static Color Equator(in Settings s) => s.ambientMode == AmbientMode.Trilight ? s.ambientEquator : s.ambientSky;
        static Color Ground(in Settings s) => s.ambientMode == AmbientMode.Trilight ? s.ambientGround : s.ambientSky;

        // ---- global volumes and looping ambience of one scene
        public sealed class SceneFade
        {
            readonly List<Volume> volumes = new List<Volume>(); readonly List<float> weights = new List<float>();
            readonly List<AudioSource> sources = new List<AudioSource>(); readonly List<float> levels = new List<float>();
            static readonly List<GameObject> roots = new List<GameObject>();

            /// Records the scene's global volumes and the AudioSources that are playing (or play on awake) in a loop.
            public void Bind(Scene scene)
            {
                volumes.Clear(); weights.Clear(); sources.Clear(); levels.Clear();
                if (!scene.IsValid() || !scene.isLoaded) return;
                scene.GetRootGameObjects(roots);
                foreach (var r in roots)
                {
                    foreach (var v in r.GetComponentsInChildren<Volume>(true)) if (v.isGlobal) { volumes.Add(v); weights.Add(v.weight); }
                    foreach (var s in r.GetComponentsInChildren<AudioSource>(true)) if (s.loop && (s.isPlaying || s.playOnAwake)) { sources.Add(s); levels.Add(s.volume); }
                }
                roots.Clear();
            }

            /// 0 = silent and no post contribution, 1 = as authored.
            public void Set(float k)
            {
                k = Mathf.Clamp01(k);
                for (int i = 0; i < volumes.Count; i++) if (volumes[i]) volumes[i].weight = weights[i] * k;
                for (int i = 0; i < sources.Count; i++) if (sources[i]) sources[i].volume = levels[i] * k;
            }
            public int Volumes => volumes.Count;
            public int Sources => sources.Count;
        }
    }
}
