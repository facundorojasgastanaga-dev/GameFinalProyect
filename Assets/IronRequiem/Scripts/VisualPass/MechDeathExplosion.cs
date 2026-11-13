using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // Unified enemy death (reference: mech T-07). After an optional overload delay the body is hidden and its
    // prebuilt chunks are thrown outward in every direction with a spark burst. Pieces are inactive children of the
    // bones/renderers in the prefab, so nothing is instantiated at death; they are unparented on launch and keep
    // flying after the enemy object is destroyed. Per-enemy tuning: force (size), center offset, spark colour, delay.
    [RequireComponent(typeof(Health))]
    public sealed class MechDeathExplosion : MonoBehaviour
    {
        [SerializeField] Renderer body;
        [SerializeField] Renderer cannon;
        [SerializeField] DetachableCannon cannonJoint;
        [SerializeField] Transform center;
        [SerializeField] MechDebrisPiece[] pieces;
        [Tooltip("Source renderer of each piece (same order). A piece is skipped if its source is already gone (detached plate, severed arm).")]
        [SerializeField] Renderer[] pieceSources;
        [Tooltip("Extra renderers hidden when the body blows apart.")]
        [SerializeField] Renderer[] hideOnExplode;
        [SerializeField, Min(0)] float delay = .28f;            // matches RobotBodyBurst overload
        [SerializeField] Vector2 speed = new Vector2(7, 13);
        [SerializeField] Vector2 lift = new Vector2(4, 9);
        [SerializeField] Vector2 spinDegrees = new Vector2(220, 720);
        [SerializeField, Min(.1f)] float force = 1;
        [SerializeField] Vector3 centerOffset;
        [SerializeField, Min(1)] float pieceSeconds = 600;
        [SerializeField] Color sparkColor = new Color(2.4f, 1.1f, .35f);
        [SerializeField] Material sparkMaterial;
        [SerializeField, Min(0)] int sparkCount = 40;
        [SerializeField] LayerMask groundMask = 1;
        Health health; float timer = -1; bool exploded; ParticleSystem sparks; bool[] alive;
        public bool Exploded => exploded;
        public int PieceCount => pieces != null ? pieces.Length : 0;

        public void Configure(Renderer bodyRenderer, Renderer cannonRenderer, DetachableCannon joint, Transform core, MechDebrisPiece[] chunks)
        { body = bodyRenderer; cannon = cannonRenderer; cannonJoint = joint; center = core; pieces = chunks; pieceSources = new Renderer[0]; hideOnExplode = new Renderer[0]; }

        public void ConfigureGeneric(Transform core, MechDebrisPiece[] chunks, Renderer[] sources, Renderer[] hide, float overloadDelay, float sizeForce, Vector3 offset, float seconds, Color sparkTint, Material spark)
        {
            body = null; cannon = null; cannonJoint = null; center = core; pieces = chunks; pieceSources = sources; hideOnExplode = hide;
            delay = overloadDelay; force = sizeForce; centerOffset = offset; pieceSeconds = seconds; sparkColor = sparkTint; sparkMaterial = spark;
        }

        void Awake()
        {
            health = GetComponent<Health>(); alive = new bool[pieces != null ? pieces.Length : 0];
            if (sparkMaterial && sparkCount > 0)
            {
                var go = new GameObject("VP_DeathSparks"); go.transform.SetParent(transform, false);
                sparks = go.AddComponent<ParticleSystem>(); sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var m = sparks.main; m.playOnAwake = false; m.loop = false; m.maxParticles = sparkCount; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .7f); m.startSpeed = new ParticleSystem.MinMaxCurve(6 * force, 16 * force);
                m.startSize = new ParticleSystem.MinMaxCurve(.03f * force, .07f * force); m.startColor = sparkColor; m.gravityModifier = 1.2f;
                var e = sparks.emission; e.rateOverTime = 0;
                var s = sparks.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = .25f * force;
                var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = sparkMaterial; r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = .04f; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
        }
        void OnEnable() { if (health) health.OnDie += Died; }
        void OnDisable() { if (health) health.OnDie -= Died; }
        // Snapshot which parts still exist at the moment of death (other death presentations may hide the body
        // before the overload delay ends; detached plates / severed arms are already gone).
        void Died()
        {
            if (exploded || timer >= 0) return;
            for (int i = 0; i < alive.Length; i++)
            {
                var src = pieceSources != null && i < pieceSources.Length ? pieceSources[i] : null;
                alive[i] = !src || (src.enabled && src.gameObject.activeInHierarchy);
            }
            timer = 0; if (delay <= 0) Explode();
        }

        void Update()
        {
            if (timer < 0 || exploded) return;
            timer += Time.deltaTime; if (timer < delay) return;
            Explode();
        }

        public void Explode()
        {
            if (exploded) return; exploded = true;
            Vector3 c = (center ? center.position : transform.position + Vector3.up * 1.4f) + transform.rotation * centerOffset;
            bool cannonFlies = cannon && (!cannonJoint || !cannonJoint.IsDetached);
            // keep chunks out of the player's face: when the camera is close, nothing is thrown toward it
            var cam = Camera.main; Vector3 toCam = Vector3.zero; float camDist = float.MaxValue;
            if (cam) { toCam = Vector3.ProjectOnPlane(cam.transform.position - c, Vector3.up); camDist = toCam.magnitude; if (camDist > .01f) toCam /= camDist; }
            float avoid = Mathf.Clamp01(1 - camDist / 16f);
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i]; if (!p) continue;
                if (p.name.Contains("Cannon") && cannon && !cannonFlies) continue;
                if (timer >= 0 && i < alive.Length && !alive[i]) continue;
                var t = p.transform; t.SetParent(null, true);
                var outward = Vector3.ProjectOnPlane(t.position - c, Vector3.up);
                if (outward.sqrMagnitude < .0001f) outward = Random.insideUnitSphere; outward.y = 0; outward.Normalize();
                outward = (outward + Random.insideUnitSphere * .45f); outward.y = 0; outward.Normalize();
                float toward = Vector3.Dot(outward, toCam);
                if (avoid > 0 && toward > -.2f) outward = (outward - toCam * (toward + .6f) * 2f).normalized;
                // heavy chunks fly less far (a 3 m turret head should topple, not sail across the room)
                var pr = p.GetComponent<Renderer>(); float sizeK = pr ? Mathf.Clamp(1.1f / Mathf.Max(.1f, pr.bounds.size.magnitude), .22f, 1f) : 1;
                var v = (outward * Random.Range(speed.x, speed.y) * Mathf.Lerp(1, .5f, avoid) * sizeK + Vector3.up * Random.Range(lift.x, lift.y) * Mathf.Lerp(.6f, 1, sizeK)) * Mathf.Sqrt(force);
                p.Launch(v, Random.onUnitSphere * Random.Range(spinDegrees.x, spinDegrees.y), groundMask, pieceSeconds);
            }
            if (body) body.enabled = false;
            if (cannonFlies) cannon.enabled = false;
            if (hideOnExplode != null) foreach (var r in hideOnExplode) if (r) r.enabled = false;
            if (sparks) { sparks.transform.SetParent(null, true); sparks.transform.position = c; sparks.Emit(sparkCount); Destroy(sparks.gameObject, 1.2f); }
        }
    }
}
