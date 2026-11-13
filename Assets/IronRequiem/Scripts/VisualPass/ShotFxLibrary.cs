using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// T2: surface-aware impacts. Robots keep the weapon's own energy impact; the environment adds metal sparks or
    /// concrete dust on top of it. Loaded from Resources at startup and plugged into ImpactSurface.Resolve.
    [CreateAssetMenu(menuName = "Iron Requiem/Shot FX Library")]
    public sealed class ShotFxLibrary : ScriptableObject
    {
        public GameObject metalImpact;
        public GameObject dustImpact;
        [Min(.1f)] public float surfaceLifetime = 1.6f;
        [Tooltip("Material or object name fragments that count as metal (case-insensitive).")]
        public string[] metalKeywords = { "metal", "rust", "steel", "iron", "pipe", "grate", "rail", "door", "sill", "crate", "container", "barrel", "machine", "panel", "beam", "airlock", "turret", "shredder" };

        static ShotFxLibrary active;
        static readonly Dictionary<Collider, int> Cache = new Dictionary<Collider, int>(256); // 0 robot, 1 metal, 2 dust

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            active = Resources.Load<ShotFxLibrary>("IronRequiem/ShotFxLibrary");
            Cache.Clear();
            if (active) ImpactSurface.Resolve = Resolve;
        }

        static GameObject Resolve(Collider hit, GameObject fallback, out float lifetime)
        {
            lifetime = 0;
            if (!hit || !active) return fallback;
            if (!Cache.TryGetValue(hit, out int kind)) { kind = Classify(hit); Cache[hit] = kind; }
            if (kind != 0) lifetime = active.surfaceLifetime;
            return kind == 0 ? fallback : kind == 1 ? active.metalImpact ? active.metalImpact : fallback : active.dustImpact ? active.dustImpact : fallback;
        }

        static int Classify(Collider c)
        {
            if (c.GetComponentInParent<Damageable>() || c.GetComponentInParent<Health>()) return 0;
            var r = c.GetComponent<Renderer>() ?? c.GetComponentInParent<Renderer>();
            string a = (r && r.sharedMaterial ? r.sharedMaterial.name : "") + " " + (c.sharedMaterial ? c.sharedMaterial.name : "") + " " + c.name;
            a = a.ToLowerInvariant();
            foreach (var k in active.metalKeywords) if (a.Contains(k)) return 1;
            return 2;
        }
    }
}
