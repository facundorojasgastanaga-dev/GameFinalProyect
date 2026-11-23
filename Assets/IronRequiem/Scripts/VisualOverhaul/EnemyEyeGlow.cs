using System;
using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace IronRequiem
{
    /// Visual pass F3 (21/11/2026), role 4 "robot lights". ONE manager draws the eyes of every combat robot with a single
    /// world-space ParticleSystem (no emission, SetParticles every frame: 1 renderer, 1 draw call, automatic billboard, no
    /// GameObject, Light or Renderer per robot). Same pattern as EnemyDarkReadability: a runtime singleton that scans
    /// EnemyController and acts without touching prefabs, scenes or shared materials.
    ///
    /// Eye = core particle (small, hot) + halo particle (large, soft) with the approved E_F1Eyes look. The material carries
    /// the HDR (MaxHdr = 4, additive, URP Particles/Unlit); the per-particle vertex colour carries the hue and its alpha the
    /// intensity (hdr / MaxHdr), so no value can exceed the cap. Sizes are metres of world space with explicit caps
    /// (EnemyEyeStyle) and are never read from lossyScale. Values are sanitised (NaN / Infinity never reach the GPU).
    ///
    /// State comes from the same read-only AI data as EnemyStateLights (HadKnownTarget, IsTargetInAttackRange,
    /// EnemyChargeFraction, the weapon's shots). The AI is never changed. See EnemyEyeStyle for the look per state.
    ///
    /// Eye anchors, resolved once per enemy from renderers that already exist:
    ///   T-07 / T-07 Mech  Head/Sphere (5 cm)               SawBot   CoreIndicator
    ///   HoverBot          EyeBall (bone EyeBall_C_Jnt, pushed 0.85 x half depth forward)
    ///   Turret            front of TurretTop's bounds (bone Turret_Head_C_Jnt), Large
    ///   BigBot            ONE eye: the highest active PowerIndicator of the torso (score = height + 0.5 x forward
    ///                     projection), pushed 0.2 m forward, Large. Its PowerIndicators stop receiving state colours.
    ///   Carronero         bone "Head" (middle of the bone and its first child, or 7 cm above it); its floating
    ///                     CoreIndicator renderer is hidden while the manager controls the eye.
    /// The original eye renderers receive the red through a MaterialPropertyBlock (like EnemyStateLights.Set), and the
    /// enemy's EnemyStateLights component is DISABLED while managed (it would keep writing teal / amber every frame) and
    /// enabled again on release (not on death: its OnEnable would subscribe to an OnDie that already fired).
    ///
    /// Not touched (protected zone): anything with ScavengerBehaviour / BridgeWalker / CctvActor in its parents, any
    /// object in the scene "IntroMenu", and enemies of "Room00_Hangar" until HangarIntroCinematic.Finished (read only).
    /// Off switches: EnemyEyeGlow.Enabled (static) and the ablation group "eyes" ("--vo-off eyes").
    [DefaultExecutionOrder(950)]
    public sealed class EnemyEyeGlow : MonoBehaviour
    {
        public static EnemyEyeGlow Instance { get; private set; }
        /// Global switch: false releases every enemy (originals, EnemyStateLights and hidden renderers restored).
        public static bool Enabled { get; set; } = true;

        public enum Kind { Generic, T07, SawBot, BigBot, Hover, Turret, Carronero }

        public struct EyeSnapshot
        {
            public EnemyEyeState State; public Kind Kind; public EnemyEyeSize Size;
            public float Hdr, CoreSize, HaloSize, Flash, HotSize;
            public string AnchorName; public Vector3 LocalOffset; // debug: bone / transform that carries the eye and the offset in its local space
            public Color Color;      // linear HDR colour of the core (hue x hdr), every channel <= EnemyEyeStyle.MaxHdr
            public Vector3 Position; // world position of the eye (before the small shift toward the camera)
            public bool Drawn;
        }

        [SerializeField, Tooltip("Optional real Light for the BigBot only. OFF: the rooms already use 8 of 8 lights.")] bool bigBotLight;
        [SerializeField, Min(.1f)] float rescanInterval = .5f;
        [SerializeField, Min(.05f)] float originalRefresh = .1f;
        [SerializeField, Min(8)] int maxEyes = 96;
        [SerializeField, Tooltip("Particle vertex colours are converted from gamma to linear by Unity's particle renderer. Turn off if the red looks purer than the door red.")] bool vertexColorsAreGamma = false; // false: hue passed as is (red or deeper red in either case, never pink). true: pre-compensate if Unity linearises vertex colour and the red looks too dark.
        [SerializeField, Tooltip("Carronero eye: metres in front of the Head bone along the root forward (root space)."), Range(-.2f, 1.2f)] float carroneroForward = .79f; // measured 21/11/2026: the Head bone sits 0.24 m behind the body centre, the lens 0.55 m in front of it
        [SerializeField, Tooltip("Carronero eye: metres above the Head bone along the root up (root space)."), Range(-.5f, .4f)] float carroneroUp = -.24f;
        [SerializeField, Tooltip("BigBot eye: metres below the top of the body bounds."), Range(0f, 2f)] float bigBotDown = 1.2f; // ESTIMATE
        [SerializeField, Tooltip("BigBot eye: metres in front of the root axis along the root forward."), Range(-1f, 2.5f)] float bigBotForward = 1.0f; // ESTIMATE
        [SerializeField, Tooltip("T07 Mech eye: metres below the top of the body bounds (visible head)."), Range(0f, 1.5f)] float mechDown = .47f; // ESTIMATE
        [SerializeField, Tooltip("T07 Mech eye: metres in front of the root axis along the root forward."), Range(-.5f, .8f)] float mechForward = .15f; // ESTIMATE
        [SerializeField, Tooltip("Turret eye: metres in front of the IrisBlade bone (the iris) along the root forward."), Range(-.5f, 1.5f)] float turretForward = .12f; // ESTIMATE
        [SerializeField, Tooltip("Turret eye: metres above the IrisBlade bone along the root up."), Range(-.5f, .5f)] float turretUp = 0f;

        static readonly string[] ExcludedScenes = { "IntroMenu" };
        const string HangarScene = "Room00_Hangar";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), BaseMapId = Shader.PropertyToID("_BaseMap"), EmissionId = Shader.PropertyToID("_EmissionColor");

        sealed class Original { public Renderer Renderer; public int Slot; }
        sealed class Entry
        {
            public EnemyController Enemy; public Health Health; public int SeenFrame;
            public bool Excluded, Active, Dead, Drawn, HasForced, NoAnchor, EslOff, HiddenWasEnabled;
            public Kind Kind; public EnemyEyeSize Size; public EnemyEyeState Forced;
            public Transform Anchor; public Vector3 LocalOffset; public string AnchorName;
            public EnemyStateLights Esl; public HangarIntroCinematic Cinematic;
            public Renderer Hidden;
            public readonly List<Original> Originals = new List<Original>(3);
            public WeaponController Weapon; public float LastAttack = -10f; public float RetryAt;
            public EnemyEyeState State; public float Hdr, Flash, CoreSize, HaloSize, HotSize; public Color Color; public Vector3 Position;
            public float NextOriginalWrite = -1f; public EnemyEyeState LastWrittenState = EnemyEyeState.Dead;
            public readonly UnityAction OnAttack; public readonly Action OnShot; public readonly UnityAction OnDie;
            public Entry() { OnAttack = () => LastAttack = Time.time; OnShot = () => LastAttack = Time.time; OnDie = () => Dead = true; }
            public void Bind(WeaponController w)
            {
                if (Weapon == w) return;
                if (Weapon) Weapon.OnShootProcessed -= OnShot;
                Weapon = w; if (Weapon) Weapon.OnShootProcessed += OnShot;
            }
        }

        readonly List<Entry> entries = new List<Entry>(32);
        readonly Dictionary<EnemyController, Entry> byEnemy = new Dictionary<EnemyController, Entry>(32);
        ParticleSystem system; ParticleSystemRenderer systemRenderer; ParticleSystem.Particle[] particles;
        Material material; Texture2D texture; MaterialPropertyBlock block; Camera cam;
        float nextScan; int drawn, lastDrawn; Light bigLight;

        public int TrackedCount => entries.Count;
        public int GlowingCount { get { int n = 0; for (int i = 0; i < entries.Count; i++) if (entries[i].Active && entries[i].Drawn && !entries[i].Dead) n++; return n; } }
        /// Particles written last frame (core + halo per eye).
        public int DrawnParticles => lastDrawn;
        /// Renderers owned by the manager: always 1 (0 if the shader is missing).
        public int RendererCount => systemRenderer ? 1 : 0;
        public ParticleSystemRenderer Renderer => systemRenderer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Enabled = true; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => Ensure();

        /// Creates the singleton if needed (also used by tests).
        public static EnemyEyeGlow Ensure()
        {
            if (Instance) return Instance;
            var go = new GameObject("EnemyEyeGlow"); DontDestroyOnLoad(go);
            return go.AddComponent<EnemyEyeGlow>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; block = new MaterialPropertyBlock();
            particles = new ParticleSystem.Particle[maxEyes * 3];
            BuildRenderer();
        }

        void BuildRenderer()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!shader) { Debug.LogWarning("EnemyEyeGlow: URP Particles/Unlit not found, eyes disabled."); enabled = false; return; }
            // soft round sprite: alpha = (1 - r)^2 (the approved mock-up profile); colour is white
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "EnemyEyeGlow_Sprite", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f, a = Mathf.Clamp01(1f - r); a *= a;
                    px[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(px); texture.Apply(false, true);
            material = new Material(shader) { name = "EnemyEyeGlow_Additive", hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 2f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.One); material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3100; material.SetOverrideTag("RenderType", "Transparent");
            material.SetTexture(BaseMapId, texture);
            material.SetColor(BaseColorId, new Color(EnemyEyeStyle.MaxHdr, EnemyEyeStyle.MaxHdr, EnemyEyeStyle.MaxHdr, 1f)); // HDR cap; the vertex alpha scales it down

            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = particles.Length;
            main.startLifetime = 1f; main.startSpeed = 0f; main.startSize = .1f; main.gravityModifier = 0f; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            systemRenderer = system.GetComponent<ParticleSystemRenderer>();
            systemRenderer.sharedMaterial = material; systemRenderer.renderMode = ParticleSystemRenderMode.Billboard; systemRenderer.alignment = ParticleSystemRenderSpace.View;
            systemRenderer.sortMode = ParticleSystemSortMode.None; systemRenderer.shadowCastingMode = ShadowCastingMode.Off; systemRenderer.receiveShadows = false;
            systemRenderer.lightProbeUsage = LightProbeUsage.Off; systemRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            systemRenderer.minParticleSize = 0f; systemRenderer.maxParticleSize = .5f; // screen fraction cap: another guard against a giant glow
            system.Play();
        }

        void OnDestroy()
        {
            for (int i = entries.Count - 1; i >= 0; i--) Release(entries[i], false);
            entries.Clear(); byEnemy.Clear();
            if (material) Destroy(material); if (texture) Destroy(texture);
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ public API (tests, tools)
        /// Rescan now and activate everything alive without the 2-frame wait (tests / captures). Gameplay never calls it.
        public void ApplyNow() { Rescan(); Tick(true); }

        public bool TryGetEye(EnemyController enemy, out EyeSnapshot snapshot)
        {
            snapshot = default;
            if (!enemy || !byEnemy.TryGetValue(enemy, out var e) || !e.Active) return false;
            snapshot = new EyeSnapshot { State = e.State, Kind = e.Kind, Size = e.Size, Hdr = e.Hdr, CoreSize = e.CoreSize, HaloSize = e.HaloSize, HotSize = e.HotSize, AnchorName = e.AnchorName, LocalOffset = e.LocalOffset, Flash = e.Flash, Color = e.Color, Position = e.Position, Drawn = e.Drawn };
            return true;
        }

        /// Test hook: forces the state of one enemy (null = natural AI state again).
        public void ForceState(EnemyController enemy, EnemyEyeState? state)
        {
            if (!enemy || !byEnemy.TryGetValue(enemy, out var e)) return;
            e.HasForced = state.HasValue; if (state.HasValue) e.Forced = state.Value;
        }
        /// Debug text for the capture harness: kind, anchor bone, local offset, world position, offset from the enemy root in
        /// ROOT axes (right / up / forward, metres) and the drawn sizes. Allocates; not for per-frame use.
        public string DescribeAnchors(EnemyController enemy)
        {
            if (!enemy || !byEnemy.TryGetValue(enemy, out var e)) return "no entry";
            if (!e.Active) return (e.Excluded ? "excluded" : "inactive") + (e.NoAnchor ? " (no anchor)" : "");
            var rel = enemy.transform.InverseTransformDirection(e.Position - enemy.transform.position);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "kind={0} state={1} anchor={2} local=({3:0.###};{4:0.###};{5:0.###}) world=({6:0.##};{7:0.##};{8:0.##}) rootRel(right;up;fwd)=({9:0.##};{10:0.##};{11:0.##}) core={12:0.###} halo={13:0.###} hot={14:0.###} hdr={15:0.##} hidden={16}",
                e.Kind, e.State, e.AnchorName, e.LocalOffset.x, e.LocalOffset.y, e.LocalOffset.z, e.Position.x, e.Position.y, e.Position.z, rel.x, rel.y, rel.z, e.CoreSize, e.HaloSize, e.HotSize, e.Hdr, e.Hidden ? e.Hidden.name : "-");
        }
        public bool IsManaged(EnemyController enemy) => enemy && byEnemy.TryGetValue(enemy, out var e) && e.Active && !e.Dead;
        public bool IsExcluded(EnemyController enemy) => enemy && byEnemy.TryGetValue(enemy, out var e) && e.Excluded;

        // ------------------------------------------------------------------ scanning
        public void Rescan()
        {
            nextScan = Time.unscaledTime + rescanInterval;
            foreach (var enemy in FindObjectsByType<EnemyController>())
            {
                if (!enemy || byEnemy.ContainsKey(enemy)) continue;
                var health = enemy.GetComponent<Health>();
                if (!Alive(enemy, health)) continue;
                var e = new Entry { Enemy = enemy, Health = health, SeenFrame = Time.frameCount };
                e.Excluded = IsProtected(enemy, e);
                entries.Add(e); byEnemy.Add(enemy, e);
            }
        }
        static bool Alive(EnemyController enemy, Health health) => enemy && enemy.isActiveAndEnabled && (!health || health.CurrentHealth > 0f);

        static bool IsProtected(EnemyController enemy, Entry e)
        {
            string scene = enemy.gameObject.scene.name;
            for (int i = 0; i < ExcludedScenes.Length; i++) if (scene == ExcludedScenes[i]) return true;
            if (enemy.GetComponentInParent<ScavengerBehaviour>(true) || enemy.GetComponentInParent<BridgeWalker>(true) || enemy.GetComponentInParent<CctvActor>(true)) return true;
            if (scene == HangarScene)
                foreach (var c in FindObjectsByType<HangarIntroCinematic>(FindObjectsInactive.Include))
                    if (c.gameObject.scene == enemy.gameObject.scene) { e.Cinematic = c; break; }
            return false;
        }

        // ------------------------------------------------------------------ frame
        void LateUpdate() => Tick(false);

        void Tick(bool immediate)
        {
            if (!system) return;
            if (!immediate && Time.unscaledTime >= nextScan) Rescan();
            bool allowed = Enabled && !VisualOverhaulAblation.Has("eyes");
            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;
            Vector3 camPos = cam ? cam.transform.position : Vector3.zero; float fov = cam ? cam.fieldOfView : 60f;
            drawn = 0; float time = Time.time;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (!e.Enemy) { Release(e, true); RemoveAt(i); continue; }
                if (e.Dead || !Alive(e.Enemy, e.Health)) { Release(e, e.Dead || (e.Health && e.Health.CurrentHealth <= 0f)); RemoveAt(i); continue; } // death: off at once
                e.Drawn = false;
                bool ok = allowed && !e.Excluded && (!e.Cinematic || e.Cinematic.Finished);
                if (!ok) { if (e.Active) Release(e, false); continue; }
                if (!e.Active)
                {
                    if (!immediate && Time.frameCount - e.SeenFrame < 2) continue;      // after EnemyController.Start captured its renderers
                    if (!e.Enemy.DetectionModule || Time.unscaledTime < e.RetryAt) continue;
                    if (!Activate(e)) { e.RetryAt = Time.unscaledTime + 2f; continue; }
                }
                Evaluate(e, time);
                if (!cam) continue;
                Vector3 pos = e.Anchor ? e.Anchor.TransformPoint(e.LocalOffset) : e.Position;
                if (!EnemyEyeStyle.IsFinite(pos)) continue;
                e.Position = pos;
                Vector3 to = camPos - pos; float dist = to.magnitude;
                e.CoreSize = EnemyEyeStyle.FinalCoreSize(e.State, e.Size, dist, fov); e.HaloSize = EnemyEyeStyle.FinalHaloSize(e.State, e.Size, dist, fov);
                if (dist > .02f) pos += to / dist * Mathf.Min(e.Size == EnemyEyeSize.Large ? .04f : .02f, dist * .25f); // out of the head mesh, so it is not clipped
                float haloFade = EnemyEyeStyle.HaloDistanceFade(dist), haloHdr = EnemyEyeStyle.HaloHdr(e.State, time) * haloFade;
                var hue = EnemyEyeStyle.Hue(e.State, e.Flash); if (vertexColorsAreGamma) hue = hue.gamma;
                e.HotSize = EnemyEyeStyle.FinalHotSize(e.State, e.Size, dist, fov);
                var hot = EnemyEyeStyle.HotHue; if (vertexColorsAreGamma) hot = hot.gamma;
                if (drawn + 3 > particles.Length) break;
                if (haloHdr > .01f && e.HaloSize > 0f) Put(drawn++, pos, e.HaloSize, hue, haloHdr / EnemyEyeStyle.MaxHdr);
                // far eyes keep reading red: past 10 m the core rises toward the cap (measured 21/11/2026: a patrol eye at 25 m came out a washed (172,100,103))
                float far = Mathf.Lerp(1f, 2.2f, Mathf.InverseLerp(10f, 22f, dist));
                if (e.CoreSize > 0f) Put(drawn++, pos, e.CoreSize, hue, Mathf.Min(EnemyEyeStyle.MaxHdr, e.Hdr * far) / EnemyEyeStyle.MaxHdr);
                if (e.HotSize > 0f) Put(drawn++, pos, e.HotSize, hot, EnemyEyeStyle.HotHdr(e.State, time) / EnemyEyeStyle.MaxHdr); // 3rd particle of the same system
                e.Drawn = true;
                WriteOriginalsIfDue(e);
            }
            system.SetParticles(particles, drawn);
            lastDrawn = drawn;
            UpdateBigBotLight();
        }

        void RemoveAt(int i) { var e = entries[i]; if (!ReferenceEquals(e.Enemy, null)) byEnemy.Remove(e.Enemy); entries.RemoveAt(i); }

        void Put(int index, Vector3 position, float size, Color hue, float alpha)
        {
            var p = new ParticleSystem.Particle();
            p.position = position; p.velocity = Vector3.zero; p.startSize = EnemyEyeStyle.Sane(size, .1f, .01f, 2f);
            p.startLifetime = 1f; p.remainingLifetime = 1f; p.rotation = 0f; p.randomSeed = 1u;
            p.startColor = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(hue.r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(hue.g) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(hue.b) * 255f),
                (byte)Mathf.RoundToInt(EnemyEyeStyle.Sane(alpha, 0f, 0f, 1f) * 255f));
            particles[index] = p;
        }

        // ------------------------------------------------------------------ state (same rules as EnemyStateLights, read only)
        void Evaluate(Entry e, float time)
        {
            EnemyEyeState s; float flash = 0f;
            if (e.HasForced) s = e.Forced;
            else
            {
                var enemy = e.Enemy; var det = enemy.DetectionModule;
                s = !det || !enemy.HadKnownTarget ? EnemyEyeState.Patrol : enemy.IsTargetInAttackRange ? EnemyEyeState.Attack : EnemyEyeState.Alert;
                if (s == EnemyEyeState.Attack)
                {
                    float t = ShotLeadTime(e, enemy);
                    if (t >= 0f) { s = EnemyEyeState.Warning; flash = EnemyEyeStyle.ShotFlash(t, EnemyEyeStyle.WarningLead); }
                }
            }
            e.State = s; e.Flash = flash; e.Hdr = EnemyEyeStyle.Hdr(s, time);
            e.Color = EnemyEyeStyle.EyeColor(s, time, flash);
        }

        /// Seconds since the 0.35 s warning window opened, or -1 when no shot is imminent.
        static float ShotLeadTime(Entry e, EnemyController enemy)
        {
            float lead = EnemyEyeStyle.WarningLead;
            if (enemy.EnemyChargeDuration > 0f)
            {
                float f = enemy.EnemyChargeFraction; if (f <= 0f) return -1f;
                float remaining = (1f - f) * enemy.EnemyChargeDuration;
                return remaining <= lead ? lead - remaining : -1f;
            }
            e.Bind(enemy.GetCurrentWeapon());
            if (!e.Weapon) return -1f;
            float since = Time.time - e.LastAttack, start = e.Weapon.DelayBetweenShots - lead;
            return since >= start && since < e.Weapon.DelayBetweenShots ? since - start : -1f;
        }

        // ------------------------------------------------------------------ activation
        bool Activate(Entry e)
        {
            var enemy = e.Enemy; var root = enemy.transform;
            e.Kind = Classify(enemy); e.Size = e.Kind == Kind.BigBot || e.Kind == Kind.Turret ? EnemyEyeSize.Large : EnemyEyeSize.Standard;
            e.Esl = enemy.GetComponent<EnemyStateLights>();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            e.Originals.Clear(); e.Hidden = null; Renderer eye = null; Transform bone = null; float push = 0f; Vector3 world = default;
            switch (e.Kind)
            {
                case Kind.T07:
                    foreach (var r in renderers) if (r.name == "Sphere" && r.transform.parent && r.transform.parent.name == "Head") { eye = r; break; }
                    if (eye && root.name.IndexOf("Mech", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // T07 Mech: its Head/Sphere (and Head bone) sit at chest height (rig of the T-07 reused on a smaller body). The eye goes
                        // on the visible head: root axis + top of the bounds minus mechDown, followed by the Head bone; the chest sphere is hidden.
                        bone = FindDeep(root, "Head") ?? eye.transform; e.Hidden = eye;
                        if (BodyBounds(renderers, out var mech)) world = new Vector3(root.position.x, mech.max.y - mechDown, root.position.z) + root.forward * mechForward; else bone = null;
                    }
                    else if (eye) { bone = eye.transform; world = eye.bounds.center; AddAllSlots(e, eye); }
                    break;
                case Kind.SawBot:
                    foreach (var r in renderers) if (r.name == "CoreIndicator") { eye = r; break; }
                    if (eye) { bone = eye.transform; world = eye.bounds.center; AddAllSlots(e, eye); }
                    break;
                case Kind.Hover:
                    foreach (var r in renderers) if (r.name == "EyeBall" && r.gameObject.activeInHierarchy) { eye = r; break; }
                    if (eye)
                    {
                        bone = FindDeep(root, "EyeBall_C_Jnt") ?? eye.transform; push = eye.bounds.extents.z * .85f;
                        world = eye.bounds.center + root.forward * push; AddAllSlots(e, eye);
                    }
                    break;
                case Kind.Turret:
                    foreach (var r in renderers) if (r.name == "TurretTop") { eye = r; break; }
                    if (eye)
                    {
                        // centred on the iris: the IrisBlade bone is the iris itself; pushed only turretForward metres out of the glass
                        var iris = FindDeep(root, "IrisBlade_C_Jnt");
                        if (iris) { bone = iris; world = iris.position + root.forward * turretForward + root.up * turretUp; }
                        else { bone = FindDeep(root, "Turret_Head_C_Jnt") ?? eye.transform; world = eye.bounds.center + root.forward * eye.bounds.extents.z * .6f; }
                        var mats = eye.sharedMaterials; // only the iris slots: the rest of the head must not turn red
                        for (int i = 0; i < mats.Length; i++) if (mats[i] && mats[i].name.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0) AddOriginal(e, eye, i);
                    }
                    break;
                case Kind.BigBot:
                    {
                        // ONE eye on the centre of the front face of the upper cab. Reference = Torso bone (it carries the eye); the
                        // position comes from the root axis (X/Z = root position, so the arms never pull it sideways) and from the top
                        // of the body bounds for the height (bigBotDown below the top, bigBotForward in front of the root axis).
                        bone = FindTorso(root);
                        if (BodyBounds(renderers, out var body) && bone) world = new Vector3(root.position.x, body.max.y - bigBotDown, root.position.z) + root.forward * bigBotForward;
                        else bone = null;
                        if (e.Esl)
                        {
                            // 21/11/2026: a steady dim red instead of the material's teal, so the BigBot speaks the same colour language as its eye
                            if (block == null) block = new MaterialPropertyBlock();
                            var dim = new Color(EnemyEyeStyle.BaseColor.r * .55f, EnemyEyeStyle.BaseColor.g * .55f, EnemyEyeStyle.BaseColor.b * .55f, 1f);
                            foreach (var l in e.Esl.Lights) if (l) { l.GetPropertyBlock(block); block.SetColor("_BaseColor", dim); block.SetColor("_EmissionColor", dim); l.SetPropertyBlock(block); }
                        }
                    }
                    break;
                case Kind.Carronero:
                    {
                        foreach (var r in renderers) if (r.name == "CoreIndicator") { e.Hidden = r; break; }
                        bone = FindDeep(root, "Head");
                        // in front of the red lens of the box-shaped head: Head bone + (forward, up) in root space (tunable fields)
                        if (bone) world = bone.position + root.forward * carroneroForward + root.up * carroneroUp;
                        else if (e.Hidden) { bone = e.Hidden.transform; world = e.Hidden.bounds.center; }
                    }
                    break;
                default:
                    if (e.Esl) foreach (var l in e.Esl.Lights) if (l && l.enabled && l.gameObject.activeInHierarchy) { eye = l; break; }
                    if (eye) { bone = eye.transform; world = eye.bounds.center; AddAllSlots(e, eye); }
                    break;
            }
            if (!bone || !EnemyEyeStyle.IsFinite(world)) { e.NoAnchor = true; return false; }
            e.NoAnchor = false; e.Anchor = bone; e.LocalOffset = bone.InverseTransformPoint(world); e.Position = world; e.AnchorName = bone.name;
            if (!EnemyEyeStyle.IsFinite(e.LocalOffset)) { e.NoAnchor = true; return false; }

            if (e.Esl && e.Esl.enabled) { e.Esl.enabled = false; e.EslOff = true; } // it would keep writing teal / amber over the red
            if (e.Hidden) { e.HiddenWasEnabled = e.Hidden.enabled; e.Hidden.enabled = false; }
            enemy.onAttack += e.OnAttack;
            if (e.Health) e.Health.OnDie += e.OnDie;
            e.Active = true; e.Dead = false; e.LastWrittenState = EnemyEyeState.Dead; e.NextOriginalWrite = -1f;
            return true;
        }

        static void AddOriginal(Entry e, Renderer r, int slot) => e.Originals.Add(new Original { Renderer = r, Slot = slot });
        /// Eye-only renderers: every material slot gets the red, per slot (the way EnemyController writes its own eye block).
        static void AddAllSlots(Entry e, Renderer r) { int n = Mathf.Max(1, r.sharedMaterials.Length); for (int i = 0; i < n; i++) AddOriginal(e, r, i); }

        static Kind Classify(EnemyController enemy)
        {
            var go = enemy.gameObject;
            if (go.GetComponent<EnemyTurret>()) return Kind.Turret;
            bool carronero = false, big = false, saw = false, t07 = false;
            foreach (var m in go.GetComponents<MonoBehaviour>())
            {
                if (!m) continue; string n = m.GetType().Name;
                if (n == "CarroneroMotion") carronero = true; else if (n == "BigBotCombat") big = true; else if (n == "SawBotCombat") saw = true; else if (n == "T07AnimationDriver") t07 = true;
            }
            if (carronero) return Kind.Carronero;
            if (big) return Kind.BigBot;
            if (t07) return Kind.T07;
            if (saw) return Kind.SawBot;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.name == "EyeBall") return Kind.Hover;
            return Kind.Generic;
        }

        /// Union of the visible body renderers (no particles, trails, lines or the VPD_ vision copies).
        static bool BodyBounds(Renderer[] renderers, out Bounds body)
        {
            bool any = false; body = default;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled || !r.gameObject.activeInHierarchy || r.name.StartsWith("VPD_", StringComparison.Ordinal)) continue;
                if (!any) { body = r.bounds; any = true; } else body.Encapsulate(r.bounds);
            }
            return any;
        }
        /// The animated Torso: the one under NewRobotRig when the prefab also carries an inactive duplicate rig.
        static Transform FindTorso(Transform root)
        {
            Transform any = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Torso") continue;
                if (!any) any = t;
                for (var p = t.parent; p; p = p.parent) if (p.name == "NewRobotRig" && t.gameObject.activeInHierarchy) return t;
            }
            return any;
        }
        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ original eye renderers (MaterialPropertyBlock)
        void WriteOriginalsIfDue(Entry e)
        {
            if (e.Originals.Count == 0) return;
            float now = Time.unscaledTime;
            if (e.State == e.LastWrittenState && now < e.NextOriginalWrite) return;
            e.LastWrittenState = e.State; e.NextOriginalWrite = now + originalRefresh; // EnemyController rewrites the eye block on detection events
            WriteOriginals(e, e.Color);
        }

        void WriteOriginals(Entry e, Color c)
        {
            foreach (var o in e.Originals)
            {
                if (!o.Renderer) continue;
                if (o.Slot >= 0) o.Renderer.GetPropertyBlock(block, o.Slot); else o.Renderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, c); block.SetColor(BaseColorId, c);
                if (o.Slot >= 0) o.Renderer.SetPropertyBlock(block, o.Slot); else o.Renderer.SetPropertyBlock(block);
            }
        }

        // ------------------------------------------------------------------ release
        void Release(Entry e, bool dead)
        {
            if (!e.Active) return; e.Active = false; e.Drawn = false;
            if (e.Enemy) e.Enemy.onAttack -= e.OnAttack;
            if (e.Health) e.Health.OnDie -= e.OnDie;
            e.Bind(null);
            if (dead)
            {
                WriteOriginals(e, Color.black); // off at once; EnemyStateLights stays disabled (its OnEnable would subscribe to an OnDie that already fired)
                return;
            }
            foreach (var o in e.Originals)
            {
                if (!o.Renderer) continue;
                if (o.Slot >= 0) o.Renderer.SetPropertyBlock(null, o.Slot); else o.Renderer.SetPropertyBlock(null);
            }
            if (e.Hidden) e.Hidden.enabled = e.HiddenWasEnabled;
            if (e.EslOff && e.Esl) e.Esl.enabled = true;
            e.EslOff = false;
        }

        // ------------------------------------------------------------------ optional BigBot light (off by default)
        void UpdateBigBotLight()
        {
            if (!bigBotLight) { if (bigLight) bigLight.enabled = false; return; }
            Entry best = null;
            for (int i = 0; i < entries.Count; i++) if (entries[i].Active && entries[i].Drawn && entries[i].Kind == Kind.BigBot) { best = entries[i]; break; }
            if (best == null) { if (bigLight) bigLight.enabled = false; return; }
            if (!bigLight)
            {
                var go = new GameObject("EnemyEyeGlow_BigBotLight"); go.transform.SetParent(transform, false);
                bigLight = go.AddComponent<Light>(); bigLight.type = LightType.Point; bigLight.shadows = LightShadows.None; bigLight.range = 5f; bigLight.color = EnemyEyeStyle.BaseColor;
            }
            bigLight.transform.position = best.Position; bigLight.intensity = Mathf.Min(EnemyEyeStyle.MaxHdr, best.Hdr) * .8f; bigLight.enabled = true;
        }
    }
}
