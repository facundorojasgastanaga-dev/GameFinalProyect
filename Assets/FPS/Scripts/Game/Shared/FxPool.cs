using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.Game
{
    /// IRON REQUIEM T2: shared pool for short-lived combat objects (projectiles, impact VFX, smoke).
    /// Spawn reuses an inactive instance of the same prefab or creates one the first time; nothing is destroyed
    /// in combat. Particle systems and trails are cleared and replayed on reuse.
    public static class FxPool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> Free = new Dictionary<GameObject, Stack<GameObject>>();
        static bool hooked;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime = 0)
        {
            if (!prefab) return null;
            if (!hooked) { SceneManager.sceneUnloaded += _ => Prune(); hooked = true; }
            if (!Free.TryGetValue(prefab, out var stack)) Free[prefab] = stack = new Stack<GameObject>(16);
            GameObject go = null;
            while (stack.Count > 0 && !go) go = stack.Pop();
            PooledFx tag;
            if (!go)
            {
                go = Object.Instantiate(prefab, position, rotation);
                tag = go.GetComponent<PooledFx>() ?? go.AddComponent<PooledFx>();
                tag.Key = prefab;
            }
            else
            {
                tag = go.GetComponent<PooledFx>();
                go.transform.SetPositionAndRotation(position, rotation);
                go.SetActive(true);
                tag.Restart();
            }
            tag.Until = lifetime > 0 ? Time.time + lifetime : 0;
            return go;
        }

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, float lifetime = 0) where T : Component
        {
            var go = Spawn(prefab ? prefab.gameObject : null, position, rotation, lifetime);
            return go ? go.GetComponent<T>() : null;
        }

        /// Returns a pooled object; anything not spawned by the pool is destroyed as before.
        public static void Release(GameObject go)
        {
            if (!go) return;
            var tag = go.GetComponent<PooledFx>();
            if (!tag || !tag.Key) { Object.Destroy(go); return; }
            if (!go.activeSelf) return;
            go.SetActive(false);
            if (!Free.TryGetValue(tag.Key, out var stack)) Free[tag.Key] = stack = new Stack<GameObject>(16);
            stack.Push(go);
        }

        public static bool IsPooled(GameObject go) { var t = go ? go.GetComponent<PooledFx>() : null; return t && t.Key; }

        static void Prune() { foreach (var s in Free.Values) { var keep = s.ToArray(); s.Clear(); for (int i = keep.Length - 1; i >= 0; i--) if (keep[i]) s.Push(keep[i]); } }
    }

    public sealed class PooledFx : MonoBehaviour
    {
        [System.NonSerialized] public GameObject Key;
        [System.NonSerialized] public float Until;
        ParticleSystem[] systems; TrailRenderer[] trails;

        void Awake() { systems = GetComponentsInChildren<ParticleSystem>(true); trails = GetComponentsInChildren<TrailRenderer>(true); }

        public void Restart()
        {
            for (int i = 0; i < trails.Length; i++) if (trails[i]) trails[i].Clear();
            for (int i = 0; i < systems.Length; i++) if (systems[i]) { systems[i].Clear(true); if (systems[i].main.playOnAwake) systems[i].Play(true); }
        }

        void Update() { if (Until > 0 && Time.time >= Until) FxPool.Release(gameObject); }
    }

    /// Picks the impact effect for a hit surface. IronRequiem registers the resolver at startup; without it the
    /// projectile's own ImpactVfx is used.
    public static class ImpactSurface
    {
        public delegate GameObject Resolver(Collider hit, GameObject fallback, out float lifetime);
        public static Resolver Resolve;
    }
}
