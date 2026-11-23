using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem
{
    // Executions v02 (F2): "the piece comes to the hand". On Begin with PullDistance > 0 the executed robot is dragged toward
    // the player (direction -ToRobot) over PullSeconds with an ease-in-out. Not AI: nothing moves the robot by itself.
    // Same safe movement as ExecutionShove, mirrored: agent.Move while the agent is on the NavMesh (so it can never leave it),
    // otherwise a transform move that is refused when it would leave the NavMesh by more than 0.1 m. A SphereCast stops the
    // pull before walls (the player and loose debris are ignored) and it never closes below playerRadius + 0.25 m of the player.
    // The T-07 foot IK targets are carried along (they would otherwise stay planted and stretch the legs).
    [DefaultExecutionOrder(120)]
    [RequireComponent(typeof(EnemyExecutionSource))]
    public sealed class ExecutionPullIn : MonoBehaviour
    {
        const float MaxPull = .8f; // F1 section 2.1: the pull never exceeds 0.80 m.
        [SerializeField, Min(.05f)] float probeRadius = .35f;
        [SerializeField, Min(0)] float wallMargin = .05f;
        [SerializeField, Min(0)] float playerMargin = .25f;
        [SerializeField, Min(.05f)] float footReleaseSeconds = .25f;
        EnemyExecutionSource source; EnemyController enemy; Health health; T07FootPlacement feet;
        Transform player; float playerRadius = .4f;
        Vector3 direction, footOffset; float distance, seconds, elapsed, travelled, footRelease;
        bool pulling, subscribed, releasingFeet;
        readonly RaycastHit[] hits = new RaycastHit[16];
        public bool Pulling => pulling;
        public float Travelled => travelled;
        // Displacement of the last frame (metres, world); zero when not pulling.
        public Vector3 LastStep { get; private set; }

        void Awake() { source = GetComponent<EnemyExecutionSource>(); enemy = GetComponent<EnemyController>(); health = GetComponent<Health>(); }
        void OnEnable() { if (source) { source.BeatReached += OnBeat; subscribed = true; } }
        void OnDisable() { if (subscribed && source) source.BeatReached -= OnBeat; subscribed = false; Stop(); footOffset = Vector3.zero; releasingFeet = false; }

        void OnBeat(ExecutionBeatInfo info)
        {
            switch (info.Beat)
            {
                case ExecutionBeat.Begin:
                    Stop(); footOffset = Vector3.zero; releasingFeet = false; travelled = 0;
                    var flat = Vector3.ProjectOnPlane(-info.ToRobot, Vector3.up);
                    if (info.PullDistance <= .001f || flat.sqrMagnitude < 1e-4f) return;
                    direction = flat.normalized; distance = Mathf.Min(info.PullDistance, MaxPull); seconds = Mathf.Max(.05f, info.PullSeconds); elapsed = 0; pulling = true;
                    var pc = FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();
                    player = pc ? pc.transform : null; playerRadius = .4f;
                    if (pc) { var cc = pc.GetComponent<CharacterController>(); if (cc) playerRadius = cc.radius * Mathf.Max(pc.transform.lossyScale.x, pc.transform.lossyScale.z); }
                    if (!feet) feet = GetComponentInChildren<T07FootPlacement>(true);
                    break;
                case ExecutionBeat.Rip:
                    pulling = false; break; // The pull belongs to the grab; nothing moves the robot after the rip.
                case ExecutionBeat.End:
                case ExecutionBeat.Cancel:
                    Stop(); if (footOffset.sqrMagnitude > 0) { releasingFeet = true; footRelease = 0; } break;
            }
        }

        void Stop() { pulling = false; LastStep = Vector3.zero; }

        void Update()
        {
            LastStep = Vector3.zero;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            if (pulling)
            {
                elapsed += dt;
                float k = Mathf.Clamp01(elapsed / seconds), eased = k * k * (3 - 2 * k);
                float wanted = distance * eased - travelled;
                if (wanted > 1e-5f)
                {
                    float allowed = Mathf.Min(Clear(wanted), PlayerRoom());
                    if (allowed > 1e-5f)
                    {
                        var before = transform.position;
                        var step = direction * allowed;
                        var agent = enemy ? enemy.NavMeshAgent : null;
                        if (agent && agent.enabled && agent.isOnNavMesh) agent.Move(step);
                        else if (InsideNavMesh(transform.position + step)) transform.position += step;
                        else allowed = 0;
                        var flatStep = transform.position - before; flatStep.y = 0; LastStep = flatStep;
                        float moved = Mathf.Max(0, Vector3.Dot(LastStep, direction));
                        travelled += moved; footOffset += LastStep;
                        if (moved < allowed * .5f) allowed = 0; // The NavMesh edge stopped the agent.
                    }
                    if (allowed < wanted - 1e-4f) pulling = false; // Blocked: stop here instead of pushing through.
                }
                if (k >= 1) pulling = false;
            }
            if (releasingFeet)
            {
                footRelease += dt;
                if (footRelease >= footReleaseSeconds) { footOffset = Vector3.zero; releasingFeet = false; }
            }
            ApplyFeet();
        }

        // FootPlacement (order 100) writes absolute target positions every Update; this runs after it (order 120) and adds
        // the accumulated pull, so the planted feet travel with the body instead of staying behind.
        void ApplyFeet()
        {
            if (!feet || footOffset.sqrMagnitude < 1e-8f || (health && health.CurrentHealth <= 0)) return;
            var offset = releasingFeet ? footOffset * (1 - Mathf.SmoothStep(0, 1, footRelease / footReleaseSeconds)) : footOffset;
            var targets = feet.Targets;
            if (targets != null) for (int i = 0; i < targets.Length; i++) if (targets[i]) targets[i].position += offset;
        }

        bool InsideNavMesh(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas)) return true;
            // Scenes without a NavMesh under the robot (labs) cannot be validated: allow the move there.
            return !NavMesh.SamplePosition(transform.position, out hit, .5f, NavMesh.AllAreas);
        }

        float PlayerRoom()
        {
            if (!player) return float.MaxValue;
            var delta = player.position - transform.position; delta.y = 0;
            float closing = Vector3.Dot(delta, direction);
            if (closing <= 0) return float.MaxValue;
            return Mathf.Max(0, delta.magnitude - (playerRadius + playerMargin));
        }

        float Clear(float wanted)
        {
            var origin = transform.position + Vector3.up * (probeRadius + .35f);
            int count = Physics.SphereCastNonAlloc(origin, probeRadius, direction, hits, wanted + wallMargin, -1, QueryTriggerInteraction.Ignore);
            float nearest = wanted + wallMargin;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit.collider || hit.collider.transform.IsChildOf(transform) || hit.distance <= 0) continue;
                if (hit.collider.attachedRigidbody && !hit.collider.attachedRigidbody.isKinematic) continue; // Loose debris.
                if (player && hit.collider.transform.IsChildOf(player)) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            return Mathf.Clamp(nearest - wallMargin, 0, wanted);
        }
    }
}
