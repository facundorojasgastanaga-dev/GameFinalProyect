using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Procedural fracture lines for UI (no textures): jagged cracks radiating from impact points, with branches, drawn
    /// as thin quads plus a dark offset shadow so they read on any background. AddImpact adds a fracture; Clear resets.
    /// The mesh is rebuilt only when the cracks change.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrackGraphic : MaskableGraphic
    {
        [SerializeField, Min(.5f)] float width = 1.6f;
        [SerializeField] Color shadow = new Color(0, 0, 0, .55f);
        readonly List<Vector4> segments = new List<Vector4>(256); // x0, y0, x1, y1 in normalised rect space (0..1)
        System.Random random = new System.Random(7);

        public int ImpactCount { get; private set; }

        public void Clear() { segments.Clear(); ImpactCount = 0; SetVerticesDirty(); }

        /// A fracture from a normalised point (0..1 in the rect): 3-5 main cracks, each with a branch or two.
        public void AddImpact(Vector2 at, float reach = .55f)
        {
            ImpactCount++;
            int arms = 3 + random.Next(3);
            float baseAngle = (float)random.NextDouble() * Mathf.PI * 2;
            for (int a = 0; a < arms; a++)
            {
                float angle = baseAngle + a * Mathf.PI * 2 / arms + (float)(random.NextDouble() - .5) * .9f;
                Crack(at, angle, reach * (.55f + (float)random.NextDouble() * .6f), 2);
            }
            SetVerticesDirty();
        }

        void Crack(Vector2 from, float angle, float length, int branches)
        {
            var p = from; int steps = 5 + random.Next(4); float step = length / steps;
            for (int i = 0; i < steps; i++)
            {
                angle += (float)(random.NextDouble() - .5) * .8f; // jagged
                var q = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 2.6f) * step; // rect is wide: stretch y
                segments.Add(new Vector4(p.x, p.y, q.x, q.y)); p = q;
                if (branches > 0 && random.NextDouble() < .3) Crack(p, angle + (random.NextDouble() < .5 ? .9f : -.9f), length * .45f, branches - 1);
                if (p.x < -.05f || p.x > 1.05f || p.y < -.2f || p.y > 1.2f) break;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            foreach (var s in segments)
            {
                var a = new Vector2(r.xMin + s.x * r.width, r.yMin + s.y * r.height);
                var b = new Vector2(r.xMin + s.z * r.width, r.yMin + s.w * r.height);
                Quad(vh, a + new Vector2(1.2f, -1.2f), b + new Vector2(1.2f, -1.2f), width + 1, shadow);
                Quad(vh, a, b, width, color);
            }
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, float w, Color32 c)
        {
            var d = b - a; if (d.sqrMagnitude < 1e-4f) return;
            var n = new Vector2(-d.y, d.x).normalized * (w * .5f); int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector4.zero); vh.AddVert(a + n, c, Vector4.zero); vh.AddVert(b + n, c, Vector4.zero); vh.AddVert(b - n, c, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
