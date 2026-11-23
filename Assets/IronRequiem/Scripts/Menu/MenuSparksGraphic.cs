using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Tiny UI spark emitter for the menu title: a fixed pool of streaks that fly out, fall and cool from white-yellow to
    /// orange, plus an optional scan line that sweeps a rectangle once. Drawn as one mesh; nothing is allocated per frame.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuSparksGraphic : MaskableGraphic
    {
        const int Max = 56; const float Gravity = -1150;
        readonly Vector2[] pos = new Vector2[Max], vel = new Vector2[Max]; readonly float[] life = new float[Max], span = new float[Max], size = new float[Max];
        int alive; bool wasDrawing; Rect scanRect; float scanT = -1;
        static readonly Color Hot = new Color(1f, .96f, .72f), Cool = new Color(1f, .42f, .06f), Scan = new Color(.13f, .84f, .92f);

        public void Emit(Vector2 at, int count, float power = 1)
        {
            for (int i = 0; i < Max && count > 0; i++)
            {
                if (life[i] > 0) continue;
                pos[i] = at; vel[i] = new Vector2(Random.Range(-270f, 270f), Random.Range(-80f, 330f)) * power;
                span[i] = life[i] = Random.Range(.3f, .85f); size[i] = Random.Range(1.6f, 2.8f); count--;
            }
        }

        /// One cyan line sweeping the rectangle from top to bottom.
        public void Sweep(Rect area) { scanRect = area; scanT = 0; }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f); alive = 0;
            for (int i = 0; i < Max; i++)
            {
                if (life[i] <= 0) continue;
                life[i] -= dt; vel[i].y += Gravity * dt; vel[i].x *= 1 - 1.2f * dt; pos[i] += vel[i] * dt;
                if (life[i] > 0) alive++;
            }
            if (scanT >= 0) { scanT += dt / .55f; if (scanT >= 1) scanT = -1; }
            bool drawing = alive > 0 || scanT >= 0;
            if (drawing || wasDrawing) SetVerticesDirty();
            wasDrawing = drawing;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < Max; i++)
            {
                if (life[i] <= 0) continue;
                float t = 1 - life[i] / span[i]; var c = Color.Lerp(Hot, Cool, t); c.a = Mathf.Sqrt(1 - t);
                float speed = vel[i].magnitude; var dir = speed > 1 ? vel[i] / speed : Vector2.down;
                var along = dir * Mathf.Clamp(speed * .035f, 2.5f, 17f); var side = new Vector2(-dir.y, dir.x) * size[i] * .5f;
                var tail = c; tail.a = 0;
                int n = vh.currentVertCount;
                vh.AddVert(pos[i] - side, c, Vector2.zero); vh.AddVert(pos[i] + side, c, Vector2.zero);
                vh.AddVert(pos[i] - along + side, tail, Vector2.zero); vh.AddVert(pos[i] - along - side, tail, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
            if (scanT >= 0)
            {
                float y = Mathf.Lerp(scanRect.yMax, scanRect.yMin, scanT); var c = Scan; c.a = .55f * Mathf.Sin(scanT * Mathf.PI);
                var soft = c; soft.a = 0;
                ThermalGaugeGraphic.Quad(vh, new Vector2(scanRect.xMin, y + 1.2f), new Vector2(scanRect.xMin, y - 1.2f), new Vector2(scanRect.xMax, y - 1.2f), new Vector2(scanRect.xMax, y + 1.2f), c);
                int n = vh.currentVertCount; // trail above the line
                vh.AddVert(new Vector2(scanRect.xMin, y + 1.2f), new Color(c.r, c.g, c.b, c.a * .35f), Vector2.zero); vh.AddVert(new Vector2(scanRect.xMax, y + 1.2f), new Color(c.r, c.g, c.b, c.a * .35f), Vector2.zero);
                vh.AddVert(new Vector2(scanRect.xMax, y + 26), soft, Vector2.zero); vh.AddVert(new Vector2(scanRect.xMin, y + 26), soft, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}
