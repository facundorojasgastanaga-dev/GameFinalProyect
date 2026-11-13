using UnityEngine;

namespace IronRequiem
{
    /// Stolen cannon = fusion with the player's arm module.
    /// - Seats the VO shell on the forearm socket by geometry: the shell's rear face sits on the socket (small overlap),
    ///   centred on the forearm axis, so it reads as part of the arm (no gap).
    /// - On equip, plays an absorb animation: the stolen piece floats ahead, is pulled into the forearm and locks
    ///   with sparks, a short camera kick and a scale punch.
    /// Runs after ProtagonistHandsPresentation (order 110). Built once; zero alloc per frame.
    [DefaultExecutionOrder(200)]
    public sealed class StolenShellFit : MonoBehaviour
    {
        [SerializeField] Vector3 socketToShellPosition;
        [SerializeField] Quaternion socketToShellRotation = Quaternion.identity;
        [SerializeField, Min(0)] float overlap = .02f;
        [SerializeField, Min(.05f)] float absorbSeconds = .42f;
        [SerializeField] Vector3 startOffset = new Vector3(.06f, .14f, .38f);
        [SerializeField] Vector3 startEuler = new Vector3(-28f, 18f, 45f);
        [SerializeField] Material sparkMaterial;
        ProtagonistHandsPresentation hands;
        Vector3 rearLocal; bool measured; Vector3 baseScale;
        float t = 1; bool locked = true;
        ParticleSystem sparks;

        public void Configure(Vector3 position, Quaternion rotation) { socketToShellPosition = position; socketToShellRotation = rotation; }
        public void ConfigureSparks(Material material) => sparkMaterial = material;

        void Awake()
        {
            baseScale = transform.localScale;
            Measure();
            var go = new GameObject("VP_FusionSparks"); go.transform.SetParent(transform, false); go.layer = gameObject.layer;
            sparks = go.AddComponent<ParticleSystem>(); sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = sparks.main; m.playOnAwake = false; m.loop = false; m.duration = .1f; m.maxParticles = 40;
            m.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .3f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(.006f, .014f); m.startColor = new Color(2.2f, 1.1f, 2.4f);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = .6f;
            var e = sparks.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 32) });
            var s = sparks.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = .05f;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = sparkMaterial; r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .03f; r.maxParticleSize = .02f; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Rear-centre of all shell meshes in shell-local space (one-time).
        void Measure()
        {
            bool any = false; var b = new Bounds();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds; var toShell = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toShell.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (any) { rearLocal = new Vector3(b.center.x, b.center.y, b.min.z); measured = true; }
        }

        void OnEnable()
        {
            if (!hands) hands = FindAnyObjectByType<ProtagonistHandsPresentation>();
            t = 0; locked = false;
        }

        void LateUpdate()
        {
            if (!hands || !hands.RightForearmReplaced) return;
            var socket = hands.RightModuleSocket; if (!socket) return;
            var rot = socket.rotation * socketToShellRotation;
            Vector3 seatedPos = measured
                ? socket.position - socket.rotation * Vector3.forward * overlap - rot * Vector3.Scale(rearLocal, transform.lossyScale)
                : socket.position + socket.rotation * socketToShellPosition;

            if (t >= 1) { transform.SetPositionAndRotation(seatedPos, rot); return; }

            t = Mathf.Min(1, t + Time.deltaTime / absorbSeconds);
            // Hover briefly (first 30%), then get pulled in fast (ease-in), lock at t=1.
            float pull = t < .3f ? 0 : Mathf.Pow((t - .3f) / .7f, 2.2f);
            float hover = Mathf.Sin(t * 22f) * .006f * (1 - pull);
            var from = seatedPos + socket.rotation * (startOffset + new Vector3(0, hover, 0));
            transform.position = Vector3.Lerp(from, seatedPos, pull);
            transform.rotation = Quaternion.Slerp(rot * Quaternion.Euler(startEuler), rot, pull);
            transform.localScale = baseScale * Mathf.Lerp(.55f, 1f, pull);
            if (t >= 1 && !locked) Lock(socket);
        }

        void Lock(Transform socket)
        {
            locked = true; transform.localScale = baseScale;
            if (sparks) { sparks.transform.SetPositionAndRotation(socket.position, socket.rotation); sparks.Play(); }
            var cam = Camera.main; if (cam) DistanceCameraImpulse.Kick(cam, new Vector3(1.4f, -.6f, .8f), new Vector3(0, -.006f, -.02f));
            GameFeel.HitStop(.035f);
        }

        void OnDisable() { transform.localScale = baseScale; t = 1; }
    }
}
