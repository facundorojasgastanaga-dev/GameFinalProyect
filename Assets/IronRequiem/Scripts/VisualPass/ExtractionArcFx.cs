using UnityEngine;

namespace IronRequiem
{
    /// Execution / extraction: electric arc, sparks and small metal chunks at the wrench moment.
    /// Two pooled rigs built once in Awake; no lights, no per-frame allocation.
    public sealed class ExtractionArcFx : MonoBehaviour
    {
        [SerializeField] Material arcMaterial;
        [SerializeField] Material sparkMaterial;
        [SerializeField] Material debrisMaterial;
        [SerializeField] Mesh debrisMesh;
        [SerializeField, Min(.05f)] float arcSeconds = .26f;
        [SerializeField, Min(0)] float extraDelay = .02f;
        const int Points = 9, Pool = 2;

        sealed class Rig
        {
            public Transform root; public LineRenderer a, b; public ParticleSystem sparks, debris;
            public Vector3 from, to; public float start = -1, fire = -1;
        }
        readonly Rig[] rigs = new Rig[Pool];
        readonly Vector3[] buffer = new Vector3[Points];
        StolenWeaponCarrier carrier; ProtagonistHandsPresentation hands;
        int next;

        public void Configure(Material arc, Material spark, Material chunk, Mesh mesh) { arcMaterial = arc; sparkMaterial = spark; debrisMaterial = chunk; debrisMesh = mesh; }

        void Awake()
        {
            carrier = GetComponent<StolenWeaponCarrier>(); hands = GetComponent<ProtagonistHandsPresentation>();
            for (int i = 0; i < Pool; i++) rigs[i] = Build(i);
        }
        void OnEnable() { if (carrier) carrier.Executed += OnExecuted; }
        void OnDisable() { if (carrier) carrier.Executed -= OnExecuted; }

        void OnExecuted(EnemyExecutionSource target, Vector3 grip)
        {
            if (!target) return;
            var r = rigs[next]; next = (next + 1) % Pool;
            r.from = grip; r.to = target.transform.position + Vector3.up * 1.1f;
            if ((r.to - r.from).sqrMagnitude < .04f) r.to = r.from + target.transform.up * .6f;
            r.fire = Time.time + (hands ? hands.DisarmYankTime : .12f) + extraDelay; r.start = -1;
        }

        void Update()
        {
            for (int i = 0; i < Pool; i++)
            {
                var r = rigs[i];
                if (r.fire > 0 && Time.time >= r.fire)
                {
                    r.fire = -1; r.start = Time.time; r.root.position = r.from;
                    r.root.rotation = Quaternion.LookRotation((r.from - r.to).normalized + Vector3.up * .3f);
                    r.sparks.Play(); r.debris.Play(); r.a.enabled = r.b.enabled = true;
                    GameFeel.HitStop(.06f);
                }
                if (r.start < 0) continue;
                float age = (Time.time - r.start) / arcSeconds;
                if (age >= 1) { r.start = -1; r.a.enabled = r.b.enabled = false; continue; }
                float width = .045f * (1 - age);
                Zap(r.a, r.from, r.to, .16f, width); Zap(r.b, r.from, Vector3.Lerp(r.from, r.to, .55f) + Random.insideUnitSphere * .25f, .12f, width * .6f);
            }
        }

        void Zap(LineRenderer line, Vector3 from, Vector3 to, float jitter, float width)
        {
            for (int i = 0; i < Points; i++)
            {
                float t = i / (Points - 1f);
                buffer[i] = Vector3.Lerp(from, to, t) + (i == 0 || i == Points - 1 ? Vector3.zero : Random.insideUnitSphere * jitter * Mathf.Sin(t * Mathf.PI));
            }
            line.SetPositions(buffer); line.widthMultiplier = width;
        }

        Rig Build(int index)
        {
            var r = new Rig();
            r.root = new GameObject("VP_ExtractionArc_" + index).transform;
            r.root.SetParent(null, false); DontDestroyOnLoad(r.root.gameObject);
            r.a = Line(r.root, "ArcA"); r.b = Line(r.root, "ArcB");
            r.sparks = Burst(r.root, "Sparks", sparkMaterial, 26, .22f, 9f, .025f, new Color(1.6f, 1.8f, 3f), false);
            r.debris = Burst(r.root, "Chunks", debrisMaterial, 6, .9f, 4.5f, .07f, new Color(.25f, .23f, .22f), true);
            return r;
        }

        LineRenderer Line(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var l = go.AddComponent<LineRenderer>();
            l.positionCount = Points; l.useWorldSpace = true; l.sharedMaterial = arcMaterial; l.enabled = false;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; l.receiveShadows = false;
            l.numCapVertices = 0; l.textureMode = LineTextureMode.Stretch;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(.75f, .88f, 1f), 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            l.colorGradient = g;
            return l;
        }

        ParticleSystem Burst(Transform parent, string name, Material mat, short count, float life, float speed, float size, Color tint, bool mesh)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = .1f; m.maxParticles = count;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * .5f, life); m.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            m.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size); m.startColor = tint; m.gravityModifier = mesh ? 1.6f : .9f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.scalingMode = ParticleSystemScalingMode.Local;
            if (mesh) { m.startRotation3D = true; m.startRotationX = m.startRotationY = m.startRotationZ = new ParticleSystem.MinMaxCurve(0, 6.28f); }
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 38; s.radius = .03f;
            var col = ps.colorOverLifetime; col.enabled = !mesh;
            if (!mesh) { var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.4f, .6f, 1f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); col.color = g; }
            var rnd = go.GetComponent<ParticleSystemRenderer>(); rnd.sharedMaterial = mat; rnd.maxParticleSize = .04f;
            rnd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rnd.receiveShadows = false;
            if (mesh && debrisMesh) { rnd.renderMode = ParticleSystemRenderMode.Mesh; rnd.mesh = debrisMesh; }
            else { rnd.renderMode = ParticleSystemRenderMode.Stretch; rnd.velocityScale = .035f; rnd.lengthScale = 1.5f; }
            return ps;
        }

        void OnDestroy() { foreach (var r in rigs) if (r != null && r.root) Destroy(r.root.gameObject); }
    }
}
