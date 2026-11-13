using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Arc band pointing up (rotated by DamageDirectionHud). Thick centre, tapered ends.
    public sealed class DamageWedgeGraphic : MaskableGraphic
    {
        public float Radius = 70, Thickness = 7, Span = 46;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); const int seg = 14; var c = color;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg, a = (90 - Span * .5f + Span * t) * Mathf.Deg2Rad;
                float w = Thickness * Mathf.Sin(t * Mathf.PI);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var edge = c; edge.a *= Mathf.Sin(t * Mathf.PI);
                vh.AddVert(dir * Radius, edge, Vector2.zero); vh.AddVert(dir * (Radius + w), edge, Vector2.zero);
                if (i > 0) { int k = i * 2; vh.AddTriangle(k - 2, k - 1, k + 1); vh.AddTriangle(k - 2, k + 1, k); }
            }
        }
    }
}
