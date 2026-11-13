using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Unity.Profiling;

namespace Unity.FPS.AI
{
    public class DetectionModule : MonoBehaviour
    {
        [Tooltip("The point representing the source of target-detection raycasts for the enemy AI")]
        public Transform DetectionSourcePoint;

        [Tooltip("The max distance at which the enemy can see targets")]
        public float DetectionRange = 20f;

        [Tooltip("The max distance at which the enemy can attack its target")]
        public float AttackRange = 10f;

        [Tooltip("Time before an enemy abandons a known target that it can't see anymore")]
        public float KnownTargetTimeout = 4f;

        [Tooltip("Optional animator for OnShoot animations")]
        public Animator Animator;
        public bool UseExternalDamagePresentation;

        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;

        public GameObject KnownDetectedTarget { get; private set; }
        public bool IsTargetInAttackRange { get; private set; }
        public bool IsSeeingTarget { get; private set; }
        public bool HadKnownTarget { get; private set; }

        protected float TimeLastSeenTarget = Mathf.NegativeInfinity;

        ActorsManager m_ActorsManager;
        RaycastHit[] m_VisibilityHits = new RaycastHit[16];
        static readonly ProfilerMarker s_DetectionMarker = new ProfilerMarker("IRON.AI.TargetDetection");
        public bool UseNonAllocQueries { get; set; } = true;

        [Tooltip("Seconds between visibility raycasts (0 = every frame). Large groups of weak enemies can use ~0.1.")]
        public float DetectionInterval = 0f;
        float m_NextDetectionTime;

        const string k_AnimAttackParameter = "Attack";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        protected virtual void Start()
        {
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, DetectionModule>(m_ActorsManager, this);
            if (DetectionInterval > 0f) m_NextDetectionTime = Time.time + Random.value * DetectionInterval; // spread a horde over frames
        }

        // Iron Requiem (12/11/2026): neutral enemies (hangar scavengers) skip sight detection until provoked.
        public bool Suppressed { get; set; }

        // Makes the target known as if it had just been seen (used to alert a group when one member is hit).
        public void ForceKnownTarget(GameObject target)
        {
            if (!target) return;
            KnownDetectedTarget = target;
            TimeLastSeenTarget = Time.time;
        }

        public virtual void HandleTargetDetection(Actor actor, Collider[] selfColliders)
        {
            if (Suppressed) return;
            using (s_DetectionMarker.Auto()) HandleTargetDetectionInternal(actor, selfColliders);
        }

        void HandleTargetDetectionInternal(Actor actor, Collider[] selfColliders)
        {
            // Handle known target detection timeout
            if (KnownDetectedTarget && !IsSeeingTarget && (Time.time - TimeLastSeenTarget) > KnownTargetTimeout)
            {
                KnownDetectedTarget = null;
            }

            // Throttled: keep the last visibility result, only refresh the attack range
            if (DetectionInterval > 0f && Time.time < m_NextDetectionTime)
            {
                IsTargetInAttackRange = KnownDetectedTarget != null &&
                                        (KnownDetectedTarget.transform.position - transform.position).sqrMagnitude <= AttackRange * AttackRange;
                return;
            }
            if (DetectionInterval > 0f) m_NextDetectionTime = Time.time + DetectionInterval;

            // Find the closest visible hostile actor
            float sqrDetectionRange = DetectionRange * DetectionRange;
            IsSeeingTarget = false;
            float closestSqrDistance = Mathf.Infinity;
            foreach (Actor otherActor in m_ActorsManager.Actors)
            {
                if (otherActor.Affiliation != actor.Affiliation)
                {
                    float sqrDistance = (otherActor.transform.position - DetectionSourcePoint.position).sqrMagnitude;
                    if (sqrDistance < sqrDetectionRange && sqrDistance < closestSqrDistance)
                    {
                        // Check for obstructions
                        Vector3 direction = (otherActor.AimPoint.position - DetectionSourcePoint.position).normalized;
                        RaycastHit[] hits;
                        int hitCount;
                        if (UseNonAllocQueries)
                        {
                            hitCount = Physics.RaycastNonAlloc(DetectionSourcePoint.position, direction, m_VisibilityHits,
                                DetectionRange, -1, QueryTriggerInteraction.Ignore);
                            if (hitCount == m_VisibilityHits.Length)
                            {
                                // Saturated non-alloc buffers are unordered: retain the full-query result.
                                hits = Physics.RaycastAll(DetectionSourcePoint.position, direction, DetectionRange, -1, QueryTriggerInteraction.Ignore);
                                hitCount = hits.Length;
                                if (hits.Length < 256) m_VisibilityHits = new RaycastHit[System.Math.Max(m_VisibilityHits.Length * 2, hits.Length + 1)];
                            }
                            else hits = m_VisibilityHits;
                        }
                        else
                        {
                            hits = Physics.RaycastAll(DetectionSourcePoint.position, direction, DetectionRange, -1, QueryTriggerInteraction.Ignore);
                            hitCount = hits.Length;
                        }
                        RaycastHit closestValidHit = new RaycastHit();
                        closestValidHit.distance = Mathf.Infinity;
                        bool foundValidHit = false;
                        for (int i = 0; i < hitCount; i++)
                        {
                            var hit = hits[i];
                            if (System.Array.IndexOf(selfColliders, hit.collider) < 0 && hit.distance < closestValidHit.distance)
                            {
                                closestValidHit = hit;
                                foundValidHit = true;
                            }
                        }

                        if (foundValidHit)
                        {
                            Actor hitActor = closestValidHit.collider.GetComponentInParent<Actor>();
                            if (hitActor == otherActor)
                            {
                                IsSeeingTarget = true;
                                closestSqrDistance = sqrDistance;

                                TimeLastSeenTarget = Time.time;
                                KnownDetectedTarget = otherActor.AimPoint.gameObject;
                            }
                        }
                    }
                }
            }

            IsTargetInAttackRange = KnownDetectedTarget != null &&
                                    Vector3.Distance(transform.position, KnownDetectedTarget.transform.position) <=
                                    AttackRange;

            // Detection events
            if (!HadKnownTarget &&
                KnownDetectedTarget != null)
            {
                OnDetect();
            }

            if (HadKnownTarget &&
                KnownDetectedTarget == null)
            {
                OnLostTarget();
            }

            // Remember if we already knew a target (for next frame)
            HadKnownTarget = KnownDetectedTarget != null;
        }

        public virtual void OnLostTarget() => onLostTarget?.Invoke();

        public virtual void OnDetect() => onDetectedTarget?.Invoke();

        public virtual void OnDamaged(GameObject damageSource)
        {
            TimeLastSeenTarget = Time.time;
            var actor = damageSource ? damageSource.GetComponentInParent<Actor>() : null;
            KnownDetectedTarget = UseExternalDamagePresentation && actor && actor.AimPoint ? actor.AimPoint.gameObject : damageSource;

            if (Animator && !UseExternalDamagePresentation)
            {
                Animator.SetTrigger(k_AnimOnDamagedParameter);
            }
        }

        public virtual void OnAttack()
        {
            if (Animator)
            {
                Animator.SetTrigger(k_AnimAttackParameter);
            }
        }
    }
}
