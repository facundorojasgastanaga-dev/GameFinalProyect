using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronRequiem
{
    // Benchmark-only switches (Player command line "--vo-off a,b,c"): disables groups of the visual overhaul at start so
    // their cost can be measured in the SAME build. Does nothing without the argument. Groups:
    //   decals   VO_Decals root          props    VO_Props root (pipes, vents, cables, fixtures meshes)
    //   ambience VO_Ambience (dust, steam) refl    reflection probes      lprobes  light probe sampling on all renderers
    //   states   EnemyStateLights        trails   projectile trails (VO_Trail)   flicker LightFlicker
    public sealed class VisualOverhaulAblation : MonoBehaviour
    {
        public static string Off { get; private set; } = "";
        public static bool Has(string group) => Off.Contains("," + group + ",");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Read()
        {
            var a = Environment.GetCommandLineArgs(); Off = "";
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "--vo-off") Off = "," + a[i + 1] + ",";
        }
        void Start()
        {
            if (Off.Length == 0) return;
            void Root(string name, string group) { if (!Has(group)) return; var go = GameObject.Find(name); if (go) go.SetActive(false); }
            Root("VO_Decals", "decals"); Root("VO_Props", "props"); Root("VO_Ambience", "ambience");
            if (Has("refl")) foreach (var p in FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include)) p.enabled = false;
            if (Has("flicker")) foreach (var f in FindObjectsByType<LightFlicker>(FindObjectsInactive.Include)) f.enabled = false;
            Debug.Log("VisualOverhaulAblation off=" + Off);
        }
        void LateUpdate()
        {
            if (!Has("lprobes") && !Has("trails")) return;
            // cheap enough for a diagnostic build: every 30 frames
            if (Time.frameCount % 30 != 0) return;
            if (Has("lprobes")) foreach (var r in FindObjectsByType<Renderer>()) if (r.lightProbeUsage != LightProbeUsage.Off) r.lightProbeUsage = LightProbeUsage.Off;
            if (Has("trails")) foreach (var t in FindObjectsByType<TrailRenderer>()) if (t.name == "VO_Trail") t.enabled = false;
        }
    }
}
