using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// <summary>A single reusable UI mesh: visor glow and bounded sparks. Coordinates share the portrait's square canvas.</summary>
    public sealed class RobotPortraitOverlay : MaskableGraphic
    {
        const int Capacity = 12;
        struct Spark { public float age, life, width, length; public Vector2 velocity; public bool alive; }
        readonly Spark[] sparks = new Spark[Capacity];
        Vector2 crack = new Vector2(.7f, .36f);
        RobotPortraitSprites.Pose pose;
        float energy;
        uint seed = 32971;
        int alive;
        public int AliveCount => alive;
        public int BurstCount { get; private set; }
        public int LastBurstSize { get; private set; }
        public float Energy => energy;
        public Vector2 CrackAnchor => crack;

        protected override void Awake() { base.Awake(); raycastTarget = false; useLegacyMeshGeneration = false; }

        public void Present(RobotPortraitSprites.Pose nextPose, float glow, int damage)
        {
            Vector2 nextCrack;
            switch (nextPose)
            {
                case RobotPortraitSprites.Pose.Left: nextCrack = new Vector2(.59f, .42f); break;
                case RobotPortraitSprites.Pose.Right: nextCrack = new Vector2(.75f, .40f); break;
                case RobotPortraitSprites.Pose.Impact: nextCrack = new Vector2(.70f, .37f); break;
                case RobotPortraitSprites.Pose.Charge: nextCrack = new Vector2(.69f, .37f); break;
                default: nextCrack = new Vector2(.70f, .37f); break;
            }
            // The light scratch is higher on the cheek than the exposed d2/d3 circuit fracture.
            nextCrack.y += damage == 1 ? .10f : damage == 2 ? .025f : 0;
            if (pose == nextPose && crack == nextCrack && Mathf.Abs(energy - glow) < .001f) return;
            pose = nextPose; crack = nextCrack; energy = glow; SetVerticesDirty();
        }

        public void Burst(int damage)
        {
            if (damage <= 0) return;
            int count = damage == 1 ? 1 : damage == 2 ? 4 : 8;
            int emitted = 0;
            for (int i = 0; i < Capacity && emitted < count; i++)
            {
                if (sparks[i].alive) continue;
                float angle = Mathf.Lerp(-.45f, 1.05f, Next());
                sparks[i] = new Spark { alive = true, life = Mathf.Lerp(.14f, .28f, Next()),
                    width = Mathf.Lerp(.005f, .010f, Next()), length = Mathf.Lerp(.025f, .055f, Next()),
                    velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Lerp(.22f, .58f, Next()) };
                emitted++; alive++;
            }
            if (emitted == 0) return;
            BurstCount++; LastBurstSize = emitted; SetVerticesDirty();
        }

        public void Step(float dt)
        {
            if (dt <= 0 || alive == 0) return;
            for (int i = 0; i < Capacity; i++)
            {
                if (!sparks[i].alive) continue;
                sparks[i].age += dt;
                if (sparks[i].age >= sparks[i].life) { sparks[i].alive = false; alive--; }
            }
            SetVerticesDirty();
        }

        public void Clear(bool resetCounters = false)
        {
            bool dirty = alive > 0 || energy > 0;
            for (int i = 0; i < Capacity; i++) sparks[i].alive = false;
            alive = 0; energy = 0;
            if (resetCounters) { BurstCount = 0; LastBurstSize = 0; }
            if (dirty) SetVerticesDirty();
        }

        float Next() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 65535) / 65535f; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect(); float size = Mathf.Min(rect.width, rect.height);
            Vector2 origin = rect.center - Vector2.one * size * .5f;
            if (energy > .001f)
            {
                float x = pose == RobotPortraitSprites.Pose.Left ? .43f : pose == RobotPortraitSprites.Pose.Right ? .57f : .50f;
                var center = new Vector2(x, .525f);
                Line(mesh, origin, size, center - new Vector2(.205f, 0), center + new Vector2(.205f, 0), .065f,
                    new Color(.05f, .95f, 1, .08f * energy));
                Line(mesh, origin, size, center - new Vector2(.185f, 0), center + new Vector2(.185f, 0), .023f,
                    new Color(.35f, 1, 1, .48f * energy));
            }
            for (int i = 0; i < Capacity; i++)
            {
                var s = sparks[i]; if (!s.alive) continue;
                float t = s.age / s.life;
                Vector2 point = crack + s.velocity * s.age + new Vector2(0, -.45f * s.age * s.age);
                Vector2 tail = point - s.velocity.normalized * s.length * (1 - t * .45f);
                Line(mesh, origin, size, tail, point, s.width * (1 - t * .6f),
                    Color.Lerp(new Color(1, .84f, .22f, 1 - t), new Color(1, .24f, .025f, 0), t));
            }
        }

        static void Line(VertexHelper mesh, Vector2 origin, float size, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 delta = (to - from).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * (width * .5f);
            int n = mesh.currentVertCount;
            mesh.AddVert(origin + (from - normal) * size, tint, Vector2.zero);
            mesh.AddVert(origin + (from + normal) * size, tint, Vector2.zero);
            mesh.AddVert(origin + (to + normal) * size, tint, Vector2.zero);
            mesh.AddVert(origin + (to - normal) * size, tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
