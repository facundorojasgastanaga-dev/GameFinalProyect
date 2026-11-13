using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem
{
    /// Carroñero (horde saw bot) presentation: procedural scuttle gait, saw arm swing and spinning blade on the
    /// hard-skinned rig. No Animator: cheap enough for 20-30 at once. Gameplay stays in EnemyController/EnemyMobile/
    /// SawBotCombat; this only reads their state.
    [DefaultExecutionOrder(150)]
    public sealed class CarroneroMotion : MonoBehaviour
    {
        [SerializeField] Transform hips, spine, leftUpLeg, leftLeg, rightUpLeg, rightLeg, sawArm, sawForeArm, otherArm, head, saw;
        [SerializeField] Vector3 sawAxisLocal = Vector3.right;
        [SerializeField, Min(.1f)] float stride = .55f;
        [SerializeField] float legSwing = 26, kneeLift = 30, bob = .035f, idleSpin = 720, attackSpin = 2200;
        [SerializeField] float attackRaise = -38, attackSaw = 14;
        [Header("Scavenging (hangar)")]
        [SerializeField] float crouchDrop = .16f, crouchLean = 30, crouchThigh = 42, crouchShin = 34, reachArm = -62, digSwing = 26;
        // 0..1 targets set by ScavengerBehaviour; smoothed here so the pose never pops.
        public float CrouchTarget { get; set; }
        public float DigTarget { get; set; }
        public float ReachTarget { get; set; }
        float crouch, dig, reach;
        NavMeshAgent agent; SawBotCombat combat; Health health;
        Quaternion hipsRest, spineRest, luRest, llRest, ruRest, rlRest, saRest, sfRest, oaRest, headRest, sawRest;
        Vector3 hipsPos, lastPos; float phase, spin, speed, attackBlend, seed; bool bound;
        Renderer[] views;

        public void Configure(Transform h, Transform s, Transform lu, Transform ll, Transform ru, Transform rl, Transform sa, Transform sf, Transform oa, Transform hd, Transform sawBlade, Vector3 axis, float strideLength)
        { hips = h; spine = s; leftUpLeg = lu; leftLeg = ll; rightUpLeg = ru; rightLeg = rl; sawArm = sa; sawForeArm = sf; otherArm = oa; head = hd; saw = sawBlade; sawAxisLocal = axis; stride = strideLength; }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); combat = GetComponent<SawBotCombat>(); health = GetComponent<Health>();
            seed = Random.value * 10;
            if (agent) agent.avoidancePriority = 40 + Random.Range(0, 20); // hordes part around each other instead of queueing
            Bind();
        }

        void Bind()
        {
            if (!hips) return;
            var r = Quaternion.Inverse(transform.rotation);
            Quaternion Rel(Transform t) => t ? r * t.rotation : Quaternion.identity;
            hipsRest = Rel(hips); spineRest = Rel(spine); luRest = Rel(leftUpLeg); llRest = Rel(leftLeg); ruRest = Rel(rightUpLeg); rlRest = Rel(rightLeg);
            saRest = Rel(sawArm); sfRest = Rel(sawForeArm); oaRest = Rel(otherArm); headRest = Rel(head);
            sawRest = saw ? saw.localRotation : Quaternion.identity; hipsPos = hips.localPosition; lastPos = transform.position; bound = true;
            views = hips.root.GetComponentsInChildren<SkinnedMeshRenderer>(false);
        }

        void LateUpdate()
        {
            if (!bound) return;
            float dt = Time.deltaTime; if (dt <= 0) return;
            bool dead = health && health.CurrentHealth <= 0;
            var flat = Vector3.ProjectOnPlane(transform.position - lastPos, Vector3.up); lastPos = transform.position;
            float target = dead ? 0 : flat.magnitude / dt;
            speed = Mathf.Lerp(speed, target, 1 - Mathf.Exp(-10 * dt));
            phase = Mathf.Repeat(phase + (speed > .1f ? flat.magnitude / stride : dt * .4f), 1);
            attackBlend = Mathf.MoveTowards(attackBlend, combat && combat.IsAttacking && !dead ? 1 : 0, dt * 6);
            crouch = Mathf.MoveTowards(crouch, dead ? 0 : CrouchTarget, dt * 2.4f);
            dig = Mathf.MoveTowards(dig, dead ? 0 : DigTarget, dt * 4f);
            reach = Mathf.MoveTowards(reach, dead ? 0 : ReachTarget, dt * 3f);

            // off-screen bots keep their timing but skip posing (cheap hordes behind the player)
            bool seen = false; for (int i = 0; i < views.Length && !seen; i++) seen = views[i] && views[i].enabled && views[i].isVisible;
            if (!seen) return;
            float move = Mathf.Clamp01(speed / 3f), a = phase * Mathf.PI * 2;
            var root = transform.rotation; var right = Vector3.right;
            Quaternion About(float deg) => Quaternion.AngleAxis(deg, right);

            // body: bob twice per cycle, lean into the run, small idle breathing
            float digWave = Mathf.Sin(Time.time * 7.5f + seed) * dig;
            hips.localPosition = hipsPos + Vector3.up * (Mathf.Abs(Mathf.Sin(a)) * bob * move + Mathf.Sin(Time.time * 3 + seed) * .006f - crouchDrop * crouch);
            hips.rotation = root * About(8 * move + 6 * attackBlend + crouchLean * .4f * crouch) * hipsRest;
            if (spine) spine.rotation = root * About(8 * move + 6 * attackBlend + crouchLean * crouch + digWave * 4) * Quaternion.AngleAxis(Mathf.Sin(a) * 5 * move + digWave * 6, Vector3.up) * spineRest;

            Leg(leftUpLeg, leftLeg, luRest, llRest, a, move, root, crouch);
            Leg(rightUpLeg, rightLeg, ruRest, rlRest, a + Mathf.PI, move, root, crouch * .85f);

            // saw arm: hangs forward while running, raises and saws back and forth while attacking
            float sawing = Mathf.Sin(Time.time * 18 + seed) * attackSaw * attackBlend;
            if (sawArm) sawArm.rotation = root * About(attackRaise * attackBlend + sawing + Mathf.Sin(a) * 6 * move) * saRest;
            if (sawForeArm) sawForeArm.rotation = root * About(attackRaise * attackBlend + sawing * 1.5f + Mathf.Sin(a) * 6 * move) * sfRest;
            if (otherArm) otherArm.rotation = root * About(-Mathf.Sin(a) * 14 * move + reachArm * reach - digSwing * (.5f + .5f * digWave) * dig) * oaRest;
            if (head) head.rotation = root * About(Mathf.Sin(a * 2) * 3 * move + 12 * crouch) * Quaternion.AngleAxis(Mathf.Sin(Time.time * 1.7f + seed) * 6, Vector3.up) * headRest;

            if (saw)
            {
                spin = Mathf.Repeat(spin + (dead ? 0 : Mathf.Lerp(idleSpin, attackSpin, attackBlend)) * dt, 360);
                saw.localRotation = sawRest * Quaternion.AngleAxis(spin, sawAxisLocal);
            }
        }

        void Leg(Transform up, Transform low, Quaternion upRest, Quaternion lowRest, float a, float move, Quaternion root, float squat)
        {
            if (!up) return;
            float swing = Mathf.Sin(a) * legSwing * move;
            float lift = Mathf.Max(0, Mathf.Cos(a)) * kneeLift * move; // knee folds while the foot travels forward
            up.rotation = root * Quaternion.AngleAxis(-swing - lift * .5f - crouchThigh * squat, Vector3.right) * upRest;
            if (low) low.rotation = root * Quaternion.AngleAxis(-swing + lift * .6f + crouchShin * squat, Vector3.right) * lowRest;
        }
    }
}
