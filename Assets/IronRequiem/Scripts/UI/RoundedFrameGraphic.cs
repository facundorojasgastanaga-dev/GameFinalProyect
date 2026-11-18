using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Rounded rectangle drawn as UI geometry: either a solid fill or an outline of fixed thickness.
    /// Top and bottom corners can be rounded independently. No sprite, texture or material.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedFrameGraphic : MaskableGraphic
    {
        [SerializeField, Min(0)] float radius = 18;
        [SerializeField, Min(0)] float thickness = 3;
        [SerializeField] bool fill;
        [SerializeField] bool roundTop = true;
        [SerializeField] bool roundBottom = true;
        [SerializeField, Range(2, 16)] int segments = 8;
        static readonly Vector2[] Outer = new Vector2[4 * 17], Inner = new Vector2[4 * 17];

        protected override void Awake() { base.Awake(); useLegacyMeshGeneration = false; }

        public void Configure(float cornerRadius, float lineThickness, bool solid, bool top = true, bool bottom = true)
        {
            radius = Mathf.Max(0, cornerRadius); thickness = Mathf.Max(0, lineThickness); fill = solid; roundTop = top; roundBottom = bottom;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width <= 0 || r.height <= 0) return;
            float outerRadius = Mathf.Min(radius, r.width * .5f, r.height * .5f);
            int count = Path(Outer, r, outerRadius);
            if (fill)
            {
                // convex outline: a fan from the centre
                int centre = mesh.currentVertCount; mesh.AddVert(r.center, color, Vector2.zero);
                for (int i = 0; i < count; i++) mesh.AddVert(Outer[i], color, Vector2.zero);
                for (int i = 0; i < count; i++) mesh.AddTriangle(centre, centre + 1 + (i + 1) % count, centre + 1 + i);
                return;
            }
            float t = Mathf.Min(thickness, r.width * .5f, r.height * .5f);
            if (t <= 0) return;
            var innerRect = new Rect(r.x + t, r.y + t, r.width - 2 * t, r.height - 2 * t);
            Path(Inner, innerRect, Mathf.Max(0, outerRadius - t));
            int start = mesh.currentVertCount;
            for (int i = 0; i < count; i++) { mesh.AddVert(Outer[i], color, Vector2.zero); mesh.AddVert(Inner[i], color, Vector2.zero); }
            for (int i = 0; i < count; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % count * 2;
                mesh.AddTriangle(a, b, b + 1); mesh.AddTriangle(a, b + 1, a + 1);
            }
        }

        /// Counter-clockwise outline, the same point count for every corner (square corners repeat one point).
        int Path(Vector2[] points, Rect r, float cornerRadius)
        {
            int steps = Mathf.Clamp(segments, 2, 16), n = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                bool top = corner >= 2;
                float rad = (top ? roundTop : roundBottom) ? cornerRadius : 0;
                var centre = corner == 0 ? new Vector2(r.xMin + rad, r.yMin + rad) : corner == 1 ? new Vector2(r.xMax - rad, r.yMin + rad)
                    : corner == 2 ? new Vector2(r.xMax - rad, r.yMax - rad) : new Vector2(r.xMin + rad, r.yMax - rad);
                float startAngle = corner == 0 ? 180 : corner == 1 ? 270 : corner == 2 ? 0 : 90;
                for (int s = 0; s <= steps; s++)
                {
                    float a = (startAngle + 90f * s / steps) * Mathf.Deg2Rad;
                    points[n++] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                }
            }
            return n;
        }
    }
}
