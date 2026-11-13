using UnityEngine;

namespace IronRequiem
{
    /// Scrap points shared by the hangar scavengers, plus group alerting: hitting one Carroñero provokes it and
    /// every other member within alertRadius. Points are reserved so two bots never dig the same spot.
    public sealed class ScavengerGroup : MonoBehaviour
    {
        [SerializeField] Transform[] scrapPoints = new Transform[0];
        [SerializeField] ScavengerBehaviour[] members = new ScavengerBehaviour[0];
        [SerializeField, Min(1)] float alertRadius = 14f;
        ScavengerBehaviour[] owners;

        public int MemberCount => members.Length;
        public int ProvokedCount { get { int n = 0; foreach (var m in members) if (m && !m.IsScavenging) n++; return n; } }
        public Transform[] ScrapPoints => scrapPoints;

        public void Configure(Transform[] points, ScavengerBehaviour[] bots, float radius)
        {
            scrapPoints = points; members = bots; alertRadius = radius;
        }

        void Awake() => owners = new ScavengerBehaviour[scrapPoints.Length];

        /// Picks a free point away from the current one (deterministic per bot seed, no allocation).
        public int Reserve(ScavengerBehaviour bot, int current, int seed)
        {
            if (owners == null || owners.Length != scrapPoints.Length) owners = new ScavengerBehaviour[scrapPoints.Length];
            Release(bot);
            int n = scrapPoints.Length; if (n == 0) return -1;
            int start = Mathf.Abs(seed) % n;
            for (int i = 0; i < n; i++)
            {
                int k = (start + i) % n;
                if (k == current || owners[k] || !scrapPoints[k]) continue;
                owners[k] = bot; return k;
            }
            if (current >= 0 && current < n) { owners[current] = bot; return current; }
            return -1;
        }

        public void Release(ScavengerBehaviour bot)
        {
            if (owners == null) return;
            for (int i = 0; i < owners.Length; i++) if (owners[i] == bot) owners[i] = null;
        }

        public Transform Point(int index) => index >= 0 && index < scrapPoints.Length ? scrapPoints[index] : null;

        /// One member was damaged: it and every member near it switch to combat against the attacker.
        public void Alert(ScavengerBehaviour source, GameObject attacker)
        {
            float r2 = alertRadius * alertRadius;
            foreach (var m in members)
                if (m && m.IsScavenging && (m == source || (m.transform.position - source.transform.position).sqrMagnitude <= r2))
                    m.Provoke(attacker);
        }
    }
}
