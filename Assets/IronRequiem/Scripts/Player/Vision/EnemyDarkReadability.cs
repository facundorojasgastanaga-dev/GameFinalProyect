using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    /// Chat 11 · option C, passive readability in the dark (VISION_V01.md). No player input, no Light, no new resource.
    /// Every live enemy (EnemyController) gets its HardSurface fresnel rim raised according to how dark its surroundings
    /// are, so its silhouette reads against a dark background while the room keeps its mood.
    ///  - Darkness: ambient + the realtime point/spot lights reaching the enemy (analytic, every 0.25 s), smoothed.
    ///  - Rim: per renderer + material index MaterialPropertyBlock, merged in LateUpdate (Get → Set) with the block that
    ///    EnemyController rewrites every frame for the eye and the damage flash. Shared materials are never modified.
    ///  - Enemies whose body material has no rim keyword use a "_Vision" variant (VisionMaterialMap) on the live instance
    ///    only; its default rim is black, so it looks like the source wherever the runtime does not drive it.
    ///  - Dead or disabled enemies are released (rim back to the material value, original materials back). Debris, props
    ///    and anything without an EnemyController are never touched. The protected menu / hangar scenes are skipped.
    [DefaultExecutionOrder(900)]
    public sealed class EnemyDarkReadability : MonoBehaviour
    {
        public static EnemyDarkReadability Instance { get; private set; }
        // calibrated on 561 F0 spawn measurements: proxy 0.5 ≈ background value 15, proxy 1.6 ≈ 45 (the §L1.3 minimum)
        [SerializeField, Tooltip("Illuminance proxy at or below which an enemy counts as fully dark")] float darkIlluminance = .5f;
        [SerializeField, Tooltip("Illuminance proxy at or above which an enemy counts as lit")] float litIlluminance = 1.6f;
        [SerializeField, Min(.05f)] float sampleInterval = .25f;
        [SerializeField, Min(.2f)] float rescanInterval = 1f;
        [SerializeField, Min(.1f)] float smoothing = 3f;
        static readonly string[] ExcludedScenes = { "IntroMenu", "Room00_Hangar" };

        sealed class Slot { public Renderer Renderer; public int Index; public Material Variant, Original; }
        sealed class Entry
        {
            public EnemyController Enemy; public Health Health; public int SeenFrame; public bool Active, Excluded;
            public readonly List<Slot> Slots = new List<Slot>();
            public float Darkness = 1, Target = 1, NextSample; public bool Sampled; public UnityEngine.Events.UnityAction OnDie;
        }
        readonly List<Entry> entries = new List<Entry>(32);
        readonly Dictionary<EnemyController, Entry> byEnemy = new Dictionary<EnemyController, Entry>(32);
        readonly List<Light> lights = new List<Light>(32);
        MaterialPropertyBlock block; VisionMaterialMap variants; float nextScan, intensity = 1; bool always; int settingsVersion;
        readonly RaycastHit[] occluders = new RaycastHit[8];

        public int TrackedCount => entries.Count;
        public float DarknessOf(EnemyController enemy) => enemy && byEnemy.TryGetValue(enemy, out var e) ? e.Darkness : -1;
        public bool IsHighlighted(EnemyController enemy) => enemy && byEnemy.TryGetValue(enemy, out var e) && e.Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance) return;
            var go = new GameObject("EnemyDarkReadability"); DontDestroyOnLoad(go); go.AddComponent<EnemyDarkReadability>();
        }
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; block = new MaterialPropertyBlock(); variants = Resources.Load<VisionMaterialMap>(VisionMaterialMap.ResourcePath);
        }
        void OnDestroy() { for (int i = entries.Count - 1; i >= 0; i--) Release(entries[i]); entries.Clear(); byEnemy.Clear(); if (Instance == this) Instance = null; }

        /// Forces a rescan now (tests, scene loads).
        public void Rescan()
        {
            nextScan = Time.time + rescanInterval;
            foreach (var enemy in FindObjectsByType<EnemyController>())
                if (enemy && !byEnemy.ContainsKey(enemy) && Alive(enemy, enemy.GetComponent<Health>())) { var e = new Entry { Enemy = enemy, Health = enemy.GetComponent<Health>(), SeenFrame = Time.frameCount }; e.Excluded = IsExcluded(enemy); entries.Add(e); byEnemy.Add(enemy, e); }
            lights.Clear();
            foreach (var l in FindObjectsByType<Light>())
                if (l && l.isActiveAndEnabled && (l.type == LightType.Point || l.type == LightType.Spot || l.type == LightType.Directional)) lights.Add(l); // baked / mixed lights light the room too
        }
        static bool Alive(EnemyController enemy, Health health) => enemy.isActiveAndEnabled && (!health || health.CurrentHealth > 0);
        static bool IsExcluded(EnemyController enemy)
        {
            string scene = enemy.gameObject.scene.name;
            for (int i = 0; i < ExcludedScenes.Length; i++) if (scene == ExcludedScenes[i]) return true;
            return false;
        }

        void LateUpdate()
        {
            if (Time.time >= nextScan) Rescan();
            if (settingsVersion != RequiemSettings.VisionVersion) { settingsVersion = RequiemSettings.VisionVersion; intensity = RequiemSettings.VisionIntensity; always = RequiemSettings.VisionOutlinesAlways; }
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                // destroyed without OnDie (fall, scene unload): release whatever survived it (e.g. a detached cannon)
                if (!e.Enemy) { Release(e); entries.RemoveAt(i); byEnemy.Remove(e.Enemy); continue; }
                bool alive = Alive(e.Enemy, e.Health);
                if (!alive || e.Excluded || intensity <= 0) { if (e.Active) Release(e); if (!alive) { entries.RemoveAt(i); byEnemy.Remove(e.Enemy); } continue; }
                if (!e.Active) { if (Time.frameCount - e.SeenFrame < 2) continue; Activate(e); } // after EnemyController.Start captured its renderers
                if (Time.time >= e.NextSample) { e.NextSample = Time.time + sampleInterval; e.Target = DarknessAt(e.Enemy.transform); if (!e.Sampled) { e.Darkness = e.Target; e.Sampled = true; } }
                e.Darkness = Mathf.MoveTowards(e.Darkness, e.Target, smoothing * Time.deltaTime);
                { float dist = Distance(e); Apply(e, VisionMaterialRules.RimFor(always ? 1 : e.Darkness, intensity, dist), VisionMaterialRules.RimPowerFor(dist)); }
            }
        }

        /// Tools / captures: rescan, activate everything alive and apply the current target darkness immediately
        /// (no 2-frame wait, no smoothing). Gameplay never calls this.
        public void ApplyNow()
        {
            Rescan();
            settingsVersion = RequiemSettings.VisionVersion; intensity = RequiemSettings.VisionIntensity; always = RequiemSettings.VisionOutlinesAlways;
            foreach (var e in entries)
            {
                if (!e.Enemy || e.Excluded || !e.Enemy.isActiveAndEnabled || (e.Health && e.Health.CurrentHealth <= 0)) continue;
                if (intensity <= 0) { Release(e); continue; }
                if (!e.Active) Activate(e);
                e.Target = e.Darkness = DarknessAt(e.Enemy.transform); e.Sampled = true;
                { float dist = Distance(e); Apply(e, VisionMaterialRules.RimFor(always ? 1 : e.Darkness, intensity, dist), VisionMaterialRules.RimPowerFor(dist)); }
            }
        }

        static float Distance(Entry e) { var cam = Camera.main; return cam ? Vector3.Distance(cam.transform.position, e.Enemy.transform.position) : 0; }

        void Activate(Entry e)
        {
            e.Active = true; e.Slots.Clear();
            foreach (var r in e.Enemy.GetComponentsInChildren<Renderer>(true))
            {
                if (!VisionMaterialRules.IsBodyRenderer(r)) continue;
                var mats = r.sharedMaterials; Material[] swapped = null;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m]; if (!VisionMaterialRules.IsBodyMaterial(mat)) continue;
                    Material v = null;
                    if (!mat.IsKeywordEnabled("_RIM"))
                    {
                        v = variants ? variants.VariantFor(mat) : null; if (!v) continue;
                        if (swapped == null) swapped = (Material[])mats.Clone();
                        swapped[m] = v;
                    }
                    e.Slots.Add(new Slot { Renderer = r, Index = m, Variant = v, Original = v ? mat : null });
                }
                if (swapped != null) r.sharedMaterials = swapped;
            }
            if (e.Health) { var entry = e; e.OnDie = () => Release(entry); e.Health.OnDie += e.OnDie; }
        }
        void Release(Entry e)
        {
            if (!e.Active) return; e.Active = false;
            foreach (var s in e.Slots) ReleaseSlot(s);
            e.Slots.Clear();
            if (e.Health && e.OnDie != null) e.Health.OnDie -= e.OnDie; e.OnDie = null;
        }
        /// Clears our per-material-index block (null, as RobotHitFeedback does; writing "original" values back through a
        /// block re-converts colours and came back darker) and gives the original material back only where the vision
        /// variant is still on: damage stages that swapped materials meanwhile are kept.
        void ReleaseSlot(Slot s)
        {
            if (!s.Renderer) return;
            s.Renderer.SetPropertyBlock(null, s.Index);
            if (s.Variant)
            {
                var mats = s.Renderer.sharedMaterials;
                if (s.Index < mats.Length && mats[s.Index] == s.Variant) { mats[s.Index] = s.Original; s.Renderer.sharedMaterials = mats; }
            }
        }
        void Apply(Entry e, Color rim, float power)
        {
            var root = e.Enemy.transform;
            for (int i = e.Slots.Count - 1; i >= 0; i--)
            {
                var s = e.Slots[i];
                // a part that left the enemy (detached cannon turned pickup) is released at once and never driven again
                if (!s.Renderer || !s.Renderer.transform.IsChildOf(root)) { ReleaseSlot(s); e.Slots.RemoveAt(i); continue; }
                Write(s, rim, power);
            }
        }
        void Write(Slot s, Color rim, float power)
        {
            s.Renderer.GetPropertyBlock(block, s.Index); // keeps EnemyController's eye / flash values of this frame
            block.SetVector(VisionMaterialRules.RimColorId, rim); block.SetFloat(VisionMaterialRules.RimPowerId, power); // linear HDR, no colour-space conversion
            s.Renderer.SetPropertyBlock(block, s.Index);
        }

        float DarknessAt(Transform enemy) => 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(darkIlluminance, litIlluminance, Illuminance(enemy.position + Vector3.up * 1.2f, enemy)));

        /// Illuminance proxy at p: light probes when the scene has them (else the ambient), plus every point / spot /
        /// directional light that reaches p (inverse square, range window, spot cone) and is not blocked by a wall.
        public float Illuminance(Vector3 p, Transform ignore = null)
        {
            float sum;
            var probes = LightmapSettings.lightProbes; bool fromProbes = probes != null && probes.count > 0;
            if (fromProbes)
            {
                LightProbes.GetInterpolatedProbe(p, null, out var sh);
                sum = (.2126f * sh[0, 0] + .7152f * sh[1, 0] + .0722f * sh[2, 0]) * .886f; // DC term = average irradiance
            }
            else
            {
                Color amb = RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight
                    ? (RenderSettings.ambientSkyColor + RenderSettings.ambientEquatorColor + RenderSettings.ambientGroundColor) / 3f : RenderSettings.ambientLight;
                sum = amb.grayscale * RenderSettings.ambientIntensity;
            }
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i]; if (!l || !l.isActiveAndEnabled) continue;
                if (fromProbes && l.lightmapBakeType != LightmapBakeType.Realtime) continue; // already inside the probes (no double count)
                if (l.type == LightType.Directional)
                {
                    if (!Blocked(p - l.transform.forward * 60f, p, ignore)) sum += l.intensity * l.color.grayscale * .5f;
                    continue;
                }
                var to = p - l.transform.position; float d2 = to.sqrMagnitude, r2 = l.range * l.range; if (d2 >= r2) continue;
                float window = 1 - d2 / r2; float att = window * window / (1 + d2);
                if (l.type == LightType.Spot)
                {
                    float cos = Vector3.Dot(l.transform.forward, to / Mathf.Sqrt(Mathf.Max(d2, 1e-6f)));
                    float outer = Mathf.Cos(l.spotAngle * .5f * Mathf.Deg2Rad), inner = Mathf.Cos(l.innerSpotAngle * .5f * Mathf.Deg2Rad);
                    att *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(outer, Mathf.Max(inner, outer + 1e-3f), cos));
                }
                if (att * l.intensity < .01f || Blocked(l.transform.position, p, ignore)) continue;
                sum += l.intensity * l.color.grayscale * att;
            }
            return sum;
        }
        /// True if level geometry stands between a and b. Triggers, the enemy itself and anything that moves (rigidbodies,
        /// the player's CharacterController, other actors with Health, projectiles) never count as a wall, so a body
        /// crossing a lamp does not make the rim flicker.
        bool Blocked(Vector3 a, Vector3 b, Transform ignore)
        {
            var d = b - a; float len = d.magnitude; if (len < .05f) return false;
            int n = Physics.RaycastNonAlloc(a, d / len, occluders, len - .05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = occluders[i].collider;
                if (ignore && c.transform.IsChildOf(ignore)) continue;
                if (c.attachedRigidbody || c is CharacterController || c.GetComponentInParent<Health>()) continue;
                return true;
            }
            return false;
        }
    }
}
