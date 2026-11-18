using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Elliptical outline inscribed in its rect, drawn as UI geometry (no sprite, texture or material).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OvalFrameGraphic : MaskableGraphic
    {
        [SerializeField, Min(.5f)] float thickness = 3;
        [SerializeField, Range(16, 128)] int segments = 64;

        protected override void Awake() { base.Awake(); useLegacyMeshGeneration = false; }

        public void Configure(float lineThickness) { thickness = Mathf.Max(.5f, lineThickness); SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = GetPixelAdjustedRect();
            float ax = r.width * .5f, ay = r.height * .5f;
            float t = Mathf.Min(thickness, ax, ay);
            if (t <= 0) return;
            var c = r.center; int start = mesh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                mesh.AddVert(c + new Vector2(cos * ax, sin * ay), color, Vector2.zero);
                mesh.AddVert(c + new Vector2(cos * (ax - t), sin * (ay - t)), color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % segments * 2;
                mesh.AddTriangle(a, b, b + 1); mesh.AddTriangle(a, b + 1, a + 1);
            }
        }
    }
}
