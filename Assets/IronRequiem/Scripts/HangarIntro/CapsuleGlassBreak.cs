using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronRequiem
{
    /// Room00_Hangar capsule canopy that the protagonist breaks from the inside (12/11/2026).
    /// Crack(): crack web + dry crack sound + a few sparks. Shatter(): the canopy disappears and pooled shards fly out
    /// with simple physics, bounce and settle, then switch off after a few seconds; dust, sparks and a loud glass sound.
    /// Broken(): the end state without effects (used by the cinematic skip / restart). Shards live under this object, so
    /// they disappear with the sector when it unloads. The menu capsule does not use this component.
    /// 20/11/2026: the crack is no longer a flat picture. A web is generated on the same quad (thin straight fractures
    /// that taper from the impact point, side branches, polygonal rings between them, a crushed centre, a dark twin
    /// line for depth); it shoots out from the fist on impact and keeps creeping in small jumps until the second blow.
    /// The shatter adds a burst of fine glass grit. Scene objects are reused; nothing is saved.
    public sealed class CapsuleGlassBreak : MonoBehaviour
    {
        [SerializeField] Renderer canopy;
        [SerializeField] Renderer crack;
        [SerializeField] Rigidbody[] shards = new Rigidbody[0];
        [SerializeField] Transform impact;               // inner face of the glass where the fist lands
        [SerializeField] ParticleSystem dust, sparks;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip crackClip, shatterClip;
        [SerializeField, Min(.5f)] float shardLifetime = 6f;
        [SerializeField] Vector2 shardSpeed = new Vector2(1.8f, 4.2f);
        Vector3[] homePos; Quaternion[] homeRot; float shatteredAt = -1; bool parked;

        public bool IsShattered => shatteredAt >= 0 || !canopy || !canopy.enabled;
        public bool IsCracked => crack && crack.enabled;
        public int ActiveShards { get { int n = 0; foreach (var s in shards) if (s && s.gameObject.activeSelf) n++; return n; } }

        [SerializeField] Collider[] ignore = new Collider[0];   // capsule colliders the shards start inside of
        public void ConfigureIgnore(Collider[] colliders) => ignore = colliders ?? new Collider[0];
        public void Configure(Renderer glass, Renderer crackDecal, Rigidbody[] pieces, Transform hitPoint, ParticleSystem dustFx, ParticleSystem sparkFx, AudioSource source, AudioClip crackSfx, AudioClip shatterSfx)
        { canopy = glass; crack = crackDecal; shards = pieces; impact = hitPoint; dust = dustFx; sparks = sparkFx; audioSource = source; crackClip = crackSfx; shatterClip = shatterSfx; }

        void Awake()
        {
            homePos = new Vector3[shards.Length]; homeRot = new Quaternion[shards.Length];
            for (int i = 0; i < shards.Length; i++)
            {
                if (!shards[i]) continue;
                homePos[i] = shards[i].transform.localPosition; homeRot[i] = shards[i].transform.localRotation;
                shards[i].gameObject.SetActive(false);
            }
            BuildWeb(); BuildGrit();
            if (crack) crack.enabled = false;
            if (canopy) canopy.enabled = true;
            foreach (var s in shards)
            {
                if (!s) continue; var c = s.GetComponent<Collider>(); if (!c) continue;
                foreach (var other in ignore) if (other) Physics.IgnoreCollision(c, other, true);
            }
        }

        void OnDestroy() { if (webMesh) Destroy(webMesh); if (webMaterial) Destroy(webMaterial); }

        public void Crack()
        {
            if (IsShattered || IsCracked) return;
            if (crack) crack.enabled = true;
            if (webMesh) { StopAllCoroutines(); StartCoroutine(Grow()); }
            if (sparks && impact) { sparks.transform.position = impact.position; sparks.Emit(6); }
            if (grit && impact) { grit.transform.position = impact.position; grit.Emit(10); }
            if (audioSource && crackClip) audioSource.PlayOneShot(crackClip, 1f);
        }

        public void Shatter()
        {
            if (shatteredAt >= 0) return;
            shatteredAt = Time.time; parked = false;
            StopAllCoroutines();
            if (canopy) canopy.enabled = false;
            if (crack) crack.enabled = false;
            var centre = impact ? impact.position : transform.position;
            var outward = canopy ? -canopy.transform.forward : transform.forward; // capsule front (-Z of the pod)
            for (int i = 0; i < shards.Length; i++)
            {
                var s = shards[i]; if (!s) continue;
                var tr = s.transform; tr.localPosition = homePos[i]; tr.localRotation = homeRot[i];
                s.gameObject.SetActive(true); s.isKinematic = false;
                // deterministic spread per shard: away from the impact and out of the capsule
                float h = Mathf.Repeat(i * .618034f, 1f);
                var away = (tr.position - centre); away = away.sqrMagnitude > 1e-4f ? away.normalized : outward;
                var dir = (outward * 1.2f + away + Vector3.up * (.15f + .35f * h)).normalized;
                s.linearVelocity = dir * Mathf.Lerp(shardSpeed.x, shardSpeed.y, h);
                s.angularVelocity = new Vector3(Mathf.Sin(i * 1.7f), Mathf.Cos(i * 2.3f), Mathf.Sin(i * .9f)) * 9f;
            }
            if (dust) { dust.transform.position = centre; dust.Emit(40); }
            if (sparks) { sparks.transform.position = centre; sparks.Emit(18); }
            if (grit) { grit.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(outward, Vector3.up)); grit.Emit(70); }
            if (audioSource && shatterClip) audioSource.PlayOneShot(shatterClip, 1f);
        }

        /// End state without effects: canopy gone, no shards (they would have settled and switched off).
        public void Broken()
        {
            StopAllCoroutines();
            shatteredAt = Time.time - shardLifetime - 1; if (canopy) canopy.enabled = false; if (crack) crack.enabled = false;
            Park();
        }

        void Update()
        {
            if (shatteredAt < 0 || parked) return;
            if (Time.time - shatteredAt >= shardLifetime) Park();
        }

        void Park()
        {
            parked = true;
            foreach (var s in shards) if (s) { s.isKinematic = true; s.gameObject.SetActive(false); }
        }

        // ------------------------------------------------------------------ fine glass grit (shatter)
        ParticleSystem grit;
        void BuildGrit()
        {
            if (!sparks) return;
            grit = Instantiate(sparks, sparks.transform.parent); grit.name = "GlassGrit";
            grit.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = grit.main; main.playOnAwake = false; main.loop = false; main.maxParticles = 220;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.3f); main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 3.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.006f, .02f); main.gravityModifier = 1.6f;
            // glints, not streaks: the spark renderer stretches along the velocity, which read as an explosion of lines
            var view = grit.GetComponent<ParticleSystemRenderer>(); if (view) { view.renderMode = ParticleSystemRenderMode.Billboard; }
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.85f, .97f, 1f, .95f), new Color(.55f, .85f, 1f, .8f));
            var shape = grit.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 42; shape.radius = .22f; shape.rotation = Vector3.zero;
            var emission = grit.emission; emission.enabled = false;
        }

        // ------------------------------------------------------------------ crack web
        Mesh webMesh; Material webMaterial; Color32[] baseColors, shownColors; float[] reach; float maxReach;
        readonly List<Vector3> verts = new List<Vector3>(); readonly List<Color32> cols = new List<Color32>(); readonly List<int> tris = new List<int>(); readonly List<float> dists = new List<float>();
        static readonly Color32 Bright = new Color32(226, 243, 255, 255), Dark = new Color32(6, 14, 20, 255);

        void BuildWeb()
        {
            var filter = crack ? crack.GetComponent<MeshFilter>() : null; if (!filter || !crack.sharedMaterial) return;
            var rng = new System.Random(4071);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const int count = 13;
            var lines = new List<Vector2>[count]; var lengths = new float[count];
            for (int i = 0; i < count; i++)
            {
                float angle = (i + R(-.45f, .45f)) / count * Mathf.PI * 2, length = i % 5 == 1 ? R(.5f, .7f) : R(.14f, .46f);
                var pts = new List<Vector2> { Vector2.zero }; float d = 0, a = angle;
                while (d < length)
                {
                    float step = Mathf.Min(R(.045f, .085f), length - d + .001f);
                    a = Mathf.Lerp(a, angle, .5f) + R(-.13f, .13f);            // nearly straight, with small kinks
                    pts.Add(pts[pts.Count - 1] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * step); d += step;
                }
                lines[i] = pts; lengths[i] = d;
                Stroke(pts, 0, .0046f, .0008f, 205, 95, true);
                // side branches
                int branches = rng.NextDouble() < .75 ? (rng.NextDouble() < .4 ? 2 : 1) : 0;
                for (int b = 0; b < branches; b++)
                {
                    int at = Mathf.Clamp(Mathf.RoundToInt(pts.Count * R(.3f, .7f)), 1, pts.Count - 2);
                    var origin = pts[at]; var dir = (pts[at + 1] - pts[at - 1]).normalized; float turn = (rng.NextDouble() < .5 ? -1 : 1) * R(.38f, .72f);
                    float ba = Mathf.Atan2(dir.y, dir.x) + turn, bl = R(.08f, .2f), startDist = origin.magnitude;
                    var bp = new List<Vector2> { origin }; float bd = 0;
                    while (bd < bl) { float step = Mathf.Min(R(.035f, .06f), bl - bd + .001f); ba += R(-.16f, .16f); bp.Add(bp[bp.Count - 1] + new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * step); bd += step; }
                    Stroke(bp, startDist, .0026f, .0006f, 160, 70, false);
                }
            }
            // polygonal rings: straight bridges between neighbouring fractures, denser near the fist
            // (incomplete on purpose: a full set of rings reads as a drawn spider web)
            float[] radii = { .05f, .11f, .2f, .31f }; float[] chance = { .8f, .5f, .3f, .14f };
            for (int r = 0; r < radii.Length; r++)
                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count; if (rng.NextDouble() > chance[r]) continue;
                    float ra = radii[r] * R(.7f, 1.3f), rb = radii[r] * R(.7f, 1.3f);
                    if (ra > lengths[i] || rb > lengths[j]) continue;
                    Vector2 a = Along(lines[i], ra), b = Along(lines[j], rb), mid = (a + b) * .5f * R(.9f, .98f);
                    var bridge = new List<Vector2> { a, mid, b };
                    Stroke(bridge, radii[r], .0024f, .0016f, 150, 120, false, radii[r]);
                }
            // crushed centre: a small frosted fan and loose slivers around it
            {
                int n = verts.Count; verts.Add(Vector3.zero); cols.Add(Tint(Bright, 150)); dists.Add(0);
                for (int i = 0; i < 9; i++) { float a = i / 9f * Mathf.PI * 2, rad = R(.016f, .034f); verts.Add(new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, 0)); cols.Add(Tint(Bright, 70)); dists.Add(0); }
                for (int i = 0; i < 9; i++) { tris.Add(n); tris.Add(n + 1 + (i + 1) % 9); tris.Add(n + 1 + i); }
                for (int i = 0; i < 16; i++)
                {
                    float a = R(0, Mathf.PI * 2), rad = R(.03f, .1f), s = R(.006f, .016f), rot = R(0, Mathf.PI * 2);
                    var c = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad; int k = verts.Count; byte alpha = (byte)R(35, 95);
                    for (int q = 0; q < 3; q++) { float qa = rot + q * 2.1f + R(-.4f, .4f); verts.Add(c + new Vector2(Mathf.Cos(qa), Mathf.Sin(qa)) * s * R(.5f, 1f)); cols.Add(Tint(Bright, alpha)); dists.Add(rad); }
                    tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                }
            }
            webMesh = new Mesh { name = "CapsuleCrackWeb", hideFlags = HideFlags.HideAndDontSave };
            webMesh.SetVertices(verts); webMesh.SetTriangles(tris, 0);
            var uv = new Vector2[verts.Count]; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(.5f, .5f);
            webMesh.uv = uv; webMesh.RecalculateBounds();
            baseColors = cols.ToArray(); shownColors = new Color32[baseColors.Length]; reach = dists.ToArray();
            maxReach = 0; foreach (var d in reach) maxReach = Mathf.Max(maxReach, d);
            verts.Clear(); cols.Clear(); tris.Clear(); dists.Clear();
            // same shader as the old decal, without its picture: vertex colours only, both faces
            webMaterial = new Material(crack.sharedMaterial) { name = "CapsuleCrackWeb (runtime)", hideFlags = HideFlags.HideAndDontSave };
            if (webMaterial.HasProperty("_BaseMap")) webMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            webMaterial.mainTexture = Texture2D.whiteTexture;
            if (webMaterial.HasProperty("_BaseColor")) webMaterial.SetColor("_BaseColor", Color.white);
            if (webMaterial.HasProperty("_Cull")) webMaterial.SetFloat("_Cull", 0);
            filter.sharedMesh = webMesh; crack.sharedMaterial = webMaterial;
            Reveal(0);
        }

        static Color32 Tint(Color32 c, byte a) { c.a = a; return c; }

        static Vector2 Along(List<Vector2> pts, float distance)
        {
            float d = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector2.Distance(pts[i - 1], pts[i]);
                if (d + seg >= distance) return Vector2.Lerp(pts[i - 1], pts[i], seg > 1e-5f ? (distance - d) / seg : 0);
                d += seg;
            }
            return pts[pts.Count - 1];
        }

        /// A tapering ribbon along the polyline, with an optional dark twin beside it (the far edge of the fracture).
        void Stroke(List<Vector2> pts, float startDist, float widthA, float widthB, byte alphaA, byte alphaB, bool twin, float fixedDist = -1)
        {
            float total = 0; for (int i = 1; i < pts.Count; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
            if (total < 1e-4f) return;
            for (int pass = twin ? 0 : 1; pass < 2; pass++)
            {
                float d = 0; int first = verts.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (i > 0) d += Vector2.Distance(pts[i - 1], pts[i]);
                    float k = d / total, w = Mathf.Lerp(widthA, widthB, k);
                    var dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized; var n = new Vector2(-dir.y, dir.x);
                    var centre = pass == 0 ? pts[i] + n * (w * 1.15f) : pts[i];
                    float half = (pass == 0 ? w * .9f : w) * .5f;
                    byte a = (byte)Mathf.Lerp(alphaA, alphaB, k); if (pass == 0) a = (byte)(a * .5f);
                    var col = Tint(pass == 0 ? Dark : Bright, a);
                    verts.Add(centre + n * half); verts.Add(centre - n * half); cols.Add(col); cols.Add(col);
                    float reachHere = fixedDist >= 0 ? fixedDist : startDist + d; dists.Add(reachHere); dists.Add(reachHere);
                }
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    int a = first + i * 2;
                    tris.Add(a); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
                }
            }
        }

        /// Shows the web up to a distance from the fist, with a short soft tip.
        void Reveal(float radius)
        {
            if (!webMesh) return;
            for (int i = 0; i < baseColors.Length; i++)
            {
                var c = baseColors[i]; c.a = (byte)(c.a * Mathf.Clamp01((radius - reach[i]) / .03f)); shownColors[i] = c;
            }
            webMesh.colors32 = shownColors;
        }

        IEnumerator Grow()
        {
            // the fracture shoots out of the fist, then creeps on in small jumps while the glass still holds
            float first = Mathf.Min(maxReach, .36f), t = 0;
            while (t < .09f) { t += Time.deltaTime; Reveal(Mathf.Lerp(.02f, first, Mathf.Sqrt(Mathf.Clamp01(t / .09f)))); yield return null; }
            float shown = first; int step = 0;
            while (shown < maxReach + .03f)
            {
                yield return new WaitForSeconds(.16f + .23f * Mathf.Repeat(step * .618f, 1f));
                float from = shown; shown = Mathf.Min(maxReach + .03f, shown + .05f + .07f * Mathf.Repeat(step * .414f, 1f)); step++;
                for (float k = 0; k < 1; k += Time.deltaTime / .05f) { Reveal(Mathf.Lerp(from, shown, k)); yield return null; }
                Reveal(shown);
            }
        }
    }
}
