using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// <summary>
    /// Segmented radial fist-charge meter (6 annular-sector "petals") drawn as procedural uGUI geometry.
    ///
    /// Why custom geometry instead of sprites:
    /// - No new textures or atlas entries; uses the default white texture, so it batches into the same
    ///   draw call as the rest of the Canvas content that shares material/texture.
    /// - Crisp at any resolution / canvas scale (outline and glow widths are in canvas pixels).
    /// - Per-vertex control over the radial fill front (straight arc parallel to the inner edge), the
    ///   split/compact gap, the burst offset and the rounded corners; sprites + Image.fillMethod cannot
    ///   express a radial-from-inner fill on a separated, moving petal.
    /// Cost: the mesh is rebuilt only while something animates (charging, closing/draining, full pulse).
    /// At rest nothing is dirtied. Because a rebuild re-batches the whole Canvas it lives in, the
    /// coordinator should place this graphic under its own nested Canvas (no GraphicRaycaster needed)
    /// if the HUD canvas holds many other elements.
    ///
    /// Zero managed allocation per frame: buffers are allocated only when segments/subdivisions change,
    /// trig tables for corners and petal centres are precomputed, VertexHelper is reused by uGUI.
    /// </summary>
    public sealed class FistChargeRadialGraphic : MaskableGraphic
    {
        const int CornerSteps = 3; // samples per rounded corner = CornerSteps + 1
        const float TwoPi = Mathf.PI * 2f;

        [Header("Layout")]
        [SerializeField, Range(3, 12)] int segments = 6;
        [SerializeField, Range(0f, 40f)] float gapDegrees = 20f;
        [SerializeField, Range(.1f, .8f)] float innerRatioCompact = .29f;
        [SerializeField, Range(.1f, .8f)] float innerRatioSplit = .38f;
        [SerializeField, Range(0f, .25f)] float burst = .08f;
        [Tooltip("Corner radius as a fraction of the petal radial thickness (clamped to fit).")]
        [SerializeField, Range(0f, .5f)] float cornerRounding = .14f;
        [SerializeField, Range(0f, 8f)] float outlinePx = 3f;
        [SerializeField, Range(0f, 16f)] float glowPx = 7f;
        [SerializeField, Range(2, 16)] int arcSubdivisions = 8;

        [Header("Idle")]
        [SerializeField] bool idleGhost = true;
        [SerializeField, Range(0f, .12f)] float ghostAlpha = .1f;

        [Header("Timing (unscaled seconds)")]
        [SerializeField, Range(.005f, .3f)] float levelSmoothing = .06f;
        [SerializeField, Range(.02f, .5f)] float openCloseSeconds = .12f;
        [SerializeField, Range(.02f, .5f)] float burstSeconds = .18f;
        [SerializeField, Range(.05f, 1f)] float drainSeconds = .23f; // + openCloseSeconds ~= .35 s total
        [SerializeField, Range(0f, 8f)] float pulseHz = 3f;
        [SerializeField, Range(0f, .1f)] float pulseScale = .04f;

        static readonly float[] CornerCos = new float[CornerSteps + 1];
        static readonly float[] CornerSin = new float[CornerSteps + 1];

        static FistChargeRadialGraphic()
        {
            for (int k = 0; k <= CornerSteps; k++)
            {
                float phi = k / (float)CornerSteps * Mathf.PI * .5f;
                CornerCos[k] = Mathf.Cos(phi);
                CornerSin[k] = Mathf.Sin(phi);
            }
        }

        // Inputs
        bool inCharging, inFull;
        float inLevel;
        // Animation state
        float shown, splitT, burstT, ghostW = 1f, pulse, drainRate;
        bool wasCharging;
        int stepFrame = -1, visibleSegments;
        // Last values pushed to the mesh
        float lastShown = -1f, lastSplit = -1f, lastBurst = -1f, lastGhost = -1f, lastPulse = -1f;

        // Buffers (allocated on config change only)
        float[] pieceFill, pieceEdge, centerSin, centerCos;
        Vector2[] perim, normals;
        int builtSegments, builtSubdiv;

        public bool Driven { get; set; }
        public float ShownLevel => shown;
        public bool IsSplit => splitT > .5f;
        public float Burst01 => EaseOutCubic(burstT);
        public int VisibleSegments => visibleSegments;

        int PerimeterCount => 4 * (CornerSteps + 1) + 2 * (builtSubdiv - 1);

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            EnsureBuffers();
            ComputePieces();
        }

        public void SetState(bool charging, float level01, bool full)
        {
            inCharging = charging;
            inLevel = Mathf.Clamp01(level01);
            inFull = full;
            Step();
        }

        void LateUpdate()
        {
            // Self-step when nobody drives us this frame (keeps close/drain running if the driver stops).
            Step();
        }

        void Step()
        {
            if (stepFrame == Time.frameCount) return;
            stepFrame = Time.frameCount;
            EnsureBuffers();

            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            bool charging = inCharging;

            if (charging)
            {
                float k = 1f - Mathf.Exp(-dt / levelSmoothing);
                shown += (inLevel - shown) * k;
                if (Mathf.Abs(inLevel - shown) < 1e-4f) shown = inLevel;
                drainRate = 0f;
            }
            else
            {
                if (wasCharging && shown > 0f) drainRate = shown / drainSeconds;
                // Hold the level while the petals close; drain counter-clockwise once compact.
                if (splitT <= 0f && shown > 0f)
                {
                    if (drainRate <= 0f) drainRate = 1f / drainSeconds;
                    shown = Mathf.MoveTowards(shown, 0f, drainRate * dt);
                }
            }
            wasCharging = charging;

            splitT = Mathf.MoveTowards(splitT, charging ? 1f : 0f, dt / openCloseSeconds);
            bool full = charging && inFull;
            burstT = Mathf.MoveTowards(burstT, full ? 1f : 0f, dt / (full ? burstSeconds : openCloseSeconds));
            ghostW = Mathf.MoveTowards(ghostW, idleGhost && !charging && shown <= 0f ? 1f : 0f, dt / .2f);
            pulse = burstT > 0f ? Mathf.Sin(Time.unscaledTime * TwoPi * pulseHz) * EaseOutCubic(burstT) : 0f;

            ComputePieces();

            if (Changed(shown, lastShown) || Changed(splitT, lastSplit) || Changed(burstT, lastBurst) ||
                Changed(ghostW, lastGhost) || Changed(pulse, lastPulse))
            {
                lastShown = shown; lastSplit = splitT; lastBurst = burstT; lastGhost = ghostW; lastPulse = pulse;
                SetVerticesDirty();
            }
        }

        static bool Changed(float a, float b) => Mathf.Abs(a - b) > 1e-5f;

        void ComputePieces()
        {
            if (pieceFill == null) return;
            // Lead > 0 lets the faint outline of the next petal appear just before it starts filling.
            float lead = inCharging ? .35f : .1f;
            int vis = 0;
            for (int i = 0; i < builtSegments; i++)
            {
                float x = shown * builtSegments - i;
                float f = Mathf.Clamp01(x);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x + lead) / lead));
                if (shown <= 0f) e = 0f;
                pieceFill[i] = f;
                pieceEdge[i] = e;
                if (e > .01f || f > 0f) vis++;
            }
            visibleSegments = vis;
        }

        void EnsureBuffers()
        {
            int seg = Mathf.Clamp(segments, 3, 12);
            int sub = Mathf.Clamp(arcSubdivisions, 2, 16);
            if (pieceFill != null && builtSegments == seg && builtSubdiv == sub) return;
            builtSegments = seg;
            builtSubdiv = sub;
            pieceFill = new float[seg];
            pieceEdge = new float[seg];
            centerSin = new float[seg];
            centerCos = new float[seg];
            float pitch = TwoPi / seg;
            for (int i = 0; i < seg; i++)
            {
                // Petal 0 centred at 12 o'clock, indices increase clockwise.
                centerSin[i] = Mathf.Sin(i * pitch);
                centerCos[i] = Mathf.Cos(i * pitch);
            }
            int p = PerimeterCount;
            perim = new Vector2[seg * p];
            normals = new Vector2[seg * p];
        }

        // ---------- Geometry ----------

        // Per-populate scratch (fields, not allocations).
        float gHalf, gIn, gOut, gRc;
        Vector2 gCenter;

        static float EaseInOut(float t) => t * t * (3f - 2f * t);
        static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }

        Vector2 Polar(int piece, float r, float alpha)
        {
            float ca = Mathf.Cos(alpha), sa = Mathf.Sin(alpha);
            float s = centerSin[piece] * ca + centerCos[piece] * sa; // sin(theta + alpha)
            float c = centerCos[piece] * ca - centerSin[piece] * sa; // cos(theta + alpha)
            return new Vector2(gCenter.x + s * r, gCenter.y + c * r);
        }

        float HalfAt(float r)
        {
            float d = Mathf.Min(r - gIn, gOut - r);
            float inset = 0f;
            if (d < gRc)
            {
                float q = gRc - Mathf.Max(d, 0f);
                inset = gRc - Mathf.Sqrt(Mathf.Max(0f, gRc * gRc - q * q));
            }
            return gHalf - inset / Mathf.Max(r, 1f);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            EnsureBuffers();
            if (!Application.isPlaying) ComputePieces();

            Rect rect = GetPixelAdjustedRect();
            gCenter = rect.center;
            float r0 = Mathf.Min(rect.width, rect.height) * .5f;
            float rBase = (r0 - glowPx - outlinePx * .5f) / (1f + burst + pulseScale);
            if (rBase <= 2f) return;

            int seg = builtSegments;
            float sE = EaseInOut(splitT);
            float bE = EaseOutCubic(burstT);
            float scale = 1f + pulseScale * pulse;
            float pitch = TwoPi / seg;
            float gap = Mathf.Min(gapDegrees * Mathf.Deg2Rad * sE, pitch * .8f);
            gHalf = (pitch - gap) * .5f;
            float push = burst * rBase * bE;
            gIn = (rBase * Mathf.Lerp(innerRatioCompact, innerRatioSplit, sE) + push) * scale;
            gOut = (rBase + push) * scale;
            float thick = gOut - gIn;
            gRc = Mathf.Min(cornerRounding * thick, thick * .5f, gHalf * gIn * .8f);
            gRc = Mathf.Max(gRc, 0.01f);

            Color acc = color;
            float baseA = acc.a;
            Color fillC = new Color(acc.r, acc.g, acc.b, .85f * baseA);
            Color lineC = Color.Lerp(acc, Color.white, .35f); lineC.a = baseA;
            Color haloC = new Color(acc.r, acc.g, acc.b, .35f * baseA);
            float ghost = idleGhost ? ghostAlpha * ghostW : 0f;
            float pulseGlow = 1f + .35f * pulse;

            int p = PerimeterCount;
            // Build perimeters + normals for petals that will draw anything.
            for (int i = 0; i < seg; i++)
                if (pieceEdge[i] > .001f || pieceFill[i] > 0f || ghost > .001f) BuildPerimeter(i, i * p);

            // Pass 1: halos (under everything).
            for (int i = 0; i < seg; i++)
            {
                float e = pieceEdge[i];
                if (e <= .001f || glowPx <= 0f) continue;
                Color hc = haloC;
                hc.a *= e * Mathf.Lerp(.45f, 1f, pieceFill[i]) * pulseGlow;
                Color hz = hc; hz.a = 0f;
                EmitStrip(vh, i * p, p, outlinePx * .5f, outlinePx * .5f + glowPx, hc, hz);
            }
            // Pass 2: fills (radial, inner edge -> outward, straight front).
            for (int i = 0; i < seg; i++)
                if (pieceFill[i] > 0f) EmitFill(vh, i, pieceFill[i], fillC);
            // Pass 3: outlines.
            for (int i = 0; i < seg; i++)
            {
                float a = Mathf.Max(pieceEdge[i], ghost);
                if (a <= .001f || outlinePx <= 0f) continue;
                Color lc = lineC; lc.a *= a;
                EmitStrip(vh, i * p, p, -outlinePx * .5f, outlinePx * .5f, lc, lc);
            }
        }

        void BuildPerimeter(int piece, int o)
        {
            int m = builtSubdiv;
            int n = 0;
            // Left side (counter-clockwise edge), inner -> outer, rounded corners.
            for (int k = 0; k <= CornerSteps; k++)
            {
                float r = gIn + gRc * (1f - CornerCos[k]);
                perim[o + n++] = Polar(piece, r, -(gHalf - gRc * (1f - CornerSin[k]) / r));
            }
            for (int k = CornerSteps; k >= 0; k--)
            {
                float r = gOut - gRc * (1f - CornerCos[k]);
                perim[o + n++] = Polar(piece, r, -(gHalf - gRc * (1f - CornerSin[k]) / r));
            }
            // Outer arc, left -> right.
            float hOut = gHalf - gRc / gOut;
            for (int j = 1; j < m; j++) perim[o + n++] = Polar(piece, gOut, -hOut + 2f * hOut * j / m);
            // Right side, outer -> inner.
            for (int k = 0; k <= CornerSteps; k++)
            {
                float r = gOut - gRc * (1f - CornerCos[k]);
                perim[o + n++] = Polar(piece, r, gHalf - gRc * (1f - CornerSin[k]) / r);
            }
            for (int k = CornerSteps; k >= 0; k--)
            {
                float r = gIn + gRc * (1f - CornerCos[k]);
                perim[o + n++] = Polar(piece, r, gHalf - gRc * (1f - CornerSin[k]) / r);
            }
            // Inner arc, right -> left.
            float hIn = gHalf - gRc / gIn;
            for (int j = 1; j < m; j++) perim[o + n++] = Polar(piece, gIn, hIn - 2f * hIn * j / m);

            // Loop is clockwise (y-up), so the outward normal is (-t.y, t.x).
            for (int j = 0; j < n; j++)
            {
                Vector2 a = perim[o + (j + n - 1) % n], b = perim[o + (j + 1) % n];
                float tx = b.x - a.x, ty = b.y - a.y;
                float len = Mathf.Sqrt(tx * tx + ty * ty);
                normals[o + j] = len > 1e-5f ? new Vector2(-ty / len, tx / len) : Vector2.zero;
            }
        }

        void EmitStrip(VertexHelper vh, int o, int n, float off1, float off2, Color32 c1, Color32 c2)
        {
            int start = vh.currentVertCount;
            for (int j = 0; j < n; j++)
            {
                Vector2 pt = perim[o + j], nm = normals[o + j];
                vh.AddVert(new Vector3(pt.x + nm.x * off1, pt.y + nm.y * off1, 0f), c1, Vector4.zero);
                vh.AddVert(new Vector3(pt.x + nm.x * off2, pt.y + nm.y * off2, 0f), c2, Vector4.zero);
            }
            for (int j = 0; j < n; j++)
            {
                int a = start + j * 2, b = start + ((j + 1) % n) * 2;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        void EmitFill(VertexHelper vh, int piece, float fill, Color32 c)
        {
            int m = builtSubdiv;
            float rF = gIn + (gOut - gIn) * fill;
            int rows = 0;
            int start = vh.currentVertCount;
            float lastR = -1f;
            // Row radii follow the rounded-corner samples so the fill matches the outline exactly.
            for (int s = 0; s < 2 * (CornerSteps + 1); s++)
            {
                float r = s <= CornerSteps
                    ? gIn + gRc * (1f - CornerCos[s])
                    : gOut - gRc * (1f - CornerCos[2 * CornerSteps + 1 - s]);
                if (r >= rF - 1e-3f) break;
                if (r <= lastR + 1e-3f) continue;
                EmitRow(vh, piece, r, m, c);
                lastR = r; rows++;
            }
            EmitRow(vh, piece, rF, m, c); // straight front parallel to the inner edge
            rows++;
            for (int row = 0; row < rows - 1; row++)
            {
                int a = start + row * (m + 1), b = a + m + 1;
                for (int j = 0; j < m; j++)
                {
                    vh.AddTriangle(a + j, b + j, b + j + 1);
                    vh.AddTriangle(a + j, b + j + 1, a + j + 1);
                }
            }
        }

        void EmitRow(VertexHelper vh, int piece, float r, int m, Color32 c)
        {
            float h = HalfAt(r);
            for (int j = 0; j <= m; j++)
            {
                Vector2 pt = Polar(piece, r, -h + 2f * h * j / m);
                vh.AddVert(new Vector3(pt.x, pt.y, 0f), c, Vector4.zero);
            }
        }
    }
}
