using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// Visual pass 12 · F5 · combat presentation of the Carroñero (Map01 variant only, see 01e_CARRONERO.md, level
    /// "Mínimo que se nota", A1 to A7). Replaces the pose written by CarroneroMotion on Carronero_Map01.prefab, where
    /// CarroneroMotion stays on the object but disabled. Pose only: it reads SawBotCombat, Health and the transform and
    /// never writes gameplay state.
    ///   A1 gait by distance with a stride that grows with speed, world-locked stance foot (analytic two-bone IK, no raycast)
    ///   A2 per-individual phase, cadence and amplitude
    ///   A3 attack anticipation inside SawBotCombat.Preparation (wind-up, hold, strike)
    ///   A4 sustained sawing identical to the legacy contact pose, plus recovery
    ///   A5 additive hit reaction from Health.OnDamaged
    ///   A6 blend when the bot re-enters a camera or changes LOD tier
    ///   A7 bank into turns, head leads, chest counter-rotates
    /// The blade capsule SawBotCombat tests for contact hangs from the saw forearm: from the end of the preparation
    /// on, hips, chest and saw arm use the same formulas as CarroneroMotion so the contact volume does not move.
    /// Coexistence with CarroneroMotion: while that component is enabled (by anyone) this one does not pose; if a
    /// script drives its CrouchTarget/DigTarget/ReachTarget on a combat bot, this component hands the pose over to it
    /// and takes it back when the targets return to zero.
    [DefaultExecutionOrder(150)]
    public sealed class CarroneroCombatMotion : MonoBehaviour
    {
        public enum AttackStage { None, Windup, Hold, Strike, Sustain, Recover }

        [Header("Rig (written by VisualOverhaulCarronero.Apply)")]
        [SerializeField] Transform hips;
        [SerializeField] Transform spine, spine2, neck, head;
        [SerializeField] Transform leftUpLeg, leftLeg, leftFoot, rightUpLeg, rightLeg, rightFoot;
        [SerializeField] Transform sawArm, sawForeArm, otherArm, saw;
        [SerializeField] Vector3 sawAxisLocal = Vector3.right;
        [Tooltip("One SkinnedMeshRenderer per LOD level, index = LOD.")]
        [SerializeField] SkinnedMeshRenderer[] lodViews;

        [Header("A1 gait")]
        [SerializeField, Min(.1f)] float strideBase = .6f;       // metres per cycle at 0 m/s
        [SerializeField, Min(0)] float strideGain = .139f;       // extra metres per cycle per m/s: 1.38 m at 5.6, 1.60 m at 7.2
        [SerializeField, Range(.2f, .8f)] float dutyWalk = .6f, dutyRun = .34f;
        [SerializeField, Min(0)] float stepHeight = .1f, bob = .03f, footBias = .03f;   // .1 keeps the knee above 40° (bind 65°)
        [SerializeField] float lean = 8, toeOff = 28, armSwing = 14, sawArmSwing = 6, twist = 5;
        [SerializeField] float farLegSwing = 26, farKneeLift = 30;
        [Header("A2 individual variation")]
        [SerializeField, Range(0, .2f)] float cadenceJitter = .08f;
        [SerializeField, Range(0, .3f)] float amplitudeJitter = .1f;
        [Header("A3 attack anticipation (fractions of SawBotCombat.Preparation)")]
        [SerializeField, Range(.1f, .5f)] float windupFraction = .286f;
        [SerializeField, Range(.5f, .95f)] float strikeFraction = .83f;
        [SerializeField] float windupArm = -115, windupForeArm = -150, chestWind = 12, windupLean = -4;
        [SerializeField, Min(0)] float hipsDrop = .04f, hipsBack = .05f;
        [Header("A4 contact pose (same values as CarroneroMotion) and recovery")]
        [SerializeField] float attackRaise = -38;
        [SerializeField] float attackSaw = 14, sawingRate = 18, attackLean = 6, idleSpin = 720, attackSpin = 2200;
        [SerializeField, Min(.05f)] float recoverSeconds = .25f;
        [SerializeField, Min(.01f)] float reboundSeconds = .08f;
        [SerializeField] float reboundDegrees = 10;
        [Header("A5 hit reaction")]
        [SerializeField] float hitChest = 12;
        [SerializeField] float hitHead = 14;
        [SerializeField, Min(0)] float hitGive = .03f, hitInterval = .18f;
        [Header("A7 turns")]
        [SerializeField] float bankMax = 12;
        [SerializeField, Min(1)] float bankRate = 300;
        [SerializeField] float headLead = 20, chestCounter = 6;
        [SerializeField, Min(.005f)] float turnSmoothing = .04f;
        [SerializeField, Range(0, 1), Tooltip("Share of the bank kept when pivoting on the spot")] float pivotBank = .75f;
        [SerializeField, Min(0), Tooltip("Hips sink this much (m) at full turn rate: weight")] float turnCrouch = .035f;
        [SerializeField, Tooltip("Forward lean (deg) while pivoting on the spot")] float pivotLean = 4;
        [SerializeField, Min(.01f), Tooltip("Seconds the lean takes to build and settle")] float turnSettle = .09f;
        [Header("A6 camera and LOD")]
        [SerializeField, Min(0)] float reenterBlend = .2f;
        [Tooltip("Highest LOD level that still solves the foot IK.")]
        [SerializeField, Range(-1, 3)] int ikMaxLod = 1;
        [Tooltip("Highest LOD level that also poses Spine2 and Neck.")]
        [SerializeField, Range(-1, 3)] int fineMaxLod = 0;
        [Tooltip("From this LOD level on only hips and legs are posed.")]
        [SerializeField, Range(0, 4)] int legsOnlyFromLod = 3;
        [Tooltip("From this LOD level on the pose is written every other frame.")]
        [SerializeField, Range(0, 4)] int halfRateFromLod = 3;

        // ---- read-only state for tests and diagnostics
        public float Phase => phase;
        public float Speed => speed;
        public float CycleMeters => cycleDist;
        public float CadenceHz => cycleDist > 0 ? speed / cycleDist : 0;
        public float BankDegrees => bank;
        public float BlendWeight => blendW;
        public float SawArmPitch => armPitch;
        public bool YieldingToLegacy => yielding;
        public int LodTier { get; private set; } = -1;
        public int LastPosedFrame { get; private set; } = -1;
        public bool LeftFootPlanted { get; private set; }
        public bool RightFootPlanted { get; private set; }
        public AttackStage Stage { get; private set; }

        const int BHips = 0, BSpine = 1, BSpine2 = 2, BNeck = 3, BHead = 4, BLUp = 5, BLLow = 6, BLFoot = 7, BRUp = 8, BRLow = 9, BRFoot = 10, BSawArm = 11, BSawFore = 12, BOther = 13, BoneCount = 14;
        const int ModeFull = 0, ModeIk = 1, ModeFk = 2, ModeLegs = 3;
        const int Posed = 0, Hold = 1, Skip = 2;
        const float LockSpeed = 10, IdleSpeed = 8;

        struct Leg
        {
            public int up, low, foot;
            public Quaternion upFix, lowFix, plantRot;
            public Vector3 hipInHips, restLocal, pole, plant, liftLocal;
            public float l1, l2, liftPhase, offset, prevP;
            public bool stance;
        }

        static int spawned;

        readonly Transform[] bones = new Transform[BoneCount];
        readonly Quaternion[] rest = new Quaternion[BoneCount], restLocal = new Quaternion[BoneCount], fromLocal = new Quaternion[BoneCount];
        readonly bool[] has = new bool[BoneCount];
        SkinnedMeshRenderer[] views; int[] viewTier; int lastView;
        SawBotCombat combat; Health health; EnemyController enemy; CarroneroMotion legacy;
        bool hasCombat, hasHealth, hasEnemy, hasLegacy, hasSaw, bound, live, ikReady, subscribed;
        Leg legL, legR; bool legsValid;
        Quaternion sawRest; Vector3 hipsRestRoot, fromHipsLocal, lastPos, lastFwd, moveDir;
        float phase, cadence = 1, amp = 1, seed, speed, cycleDist = 1, turn, turnEase, bank, idle = 1, spin, sawSide = -1;
        float armPitch, forePitch, cock, engage, leanAtk, spinRate, lockW, lockYaw;
        float attackStart, recoverStart, prep = .35f, armFrom, foreFrom, leanFrom, cockFrom, engageFrom, spinFrom;
        float hitStart = -10, nextHit, hitX, hitZ = -1;
        float blendStart, blendW = 1, lastPoseTime = -1, handBackAt;
        int parity, lastMode = -1;
        bool moving, wasAttacking, unseen = true, blending, yielding, handedOver;

        void Awake()
        {
            combat = GetComponent<SawBotCombat>(); health = GetComponent<Health>(); enemy = GetComponent<EnemyController>(); legacy = GetComponent<CarroneroMotion>();
            hasCombat = combat; hasHealth = health; hasEnemy = enemy; hasLegacy = legacy;
            // A2: golden-ratio sequence so two bots created together never share a phase, plus a little noise
            int n = spawned++;
            phase = Mathf.Repeat(n * .618034f + Random.Range(-.03f, .03f), 1f);
            cadence = 1f + Random.Range(-cadenceJitter, cadenceJitter); amp = 1f + Random.Range(-amplitudeJitter, amplitudeJitter);
            seed = Random.value * 10f; parity = n & 1; spinRate = idleSpin;
            Bind();
        }

        void Start() => live = true;

        void OnEnable()
        {
            if (hasHealth && !subscribed) { health.OnDamaged += Damaged; subscribed = true; }
            unseen = true; legsValid = false; idle = 1; lastMode = -1; lastPoseTime = -1;
            lastPos = transform.position; lastFwd = transform.forward; speed = 0; turn = 0;
        }

        void OnDisable()
        {
            if (subscribed && health) health.OnDamaged -= Damaged;
            subscribed = false; LeftFootPlanted = RightFootPlanted = false; LodTier = -1;
        }

        // ------------------------------------------------------------------ binding
        void Bind()
        {
            if (!hips) AutoBind();
            if (!hips || !leftUpLeg || !leftLeg || !rightUpLeg || !rightLeg) return;
            bones[BHips] = hips; bones[BSpine] = spine; bones[BSpine2] = spine2; bones[BNeck] = neck; bones[BHead] = head;
            bones[BLUp] = leftUpLeg; bones[BLLow] = leftLeg; bones[BLFoot] = leftFoot; bones[BRUp] = rightUpLeg; bones[BRLow] = rightLeg; bones[BRFoot] = rightFoot;
            bones[BSawArm] = sawArm; bones[BSawFore] = sawForeArm; bones[BOther] = otherArm;
            transform.GetPositionAndRotation(out Vector3 rootPos, out Quaternion rootRot);
            var inv = Quaternion.Inverse(rootRot);
            for (int i = 0; i < BoneCount; i++)
            {
                var b = bones[i]; has[i] = b; rest[i] = Quaternion.identity; restLocal[i] = Quaternion.identity;
                if (!has[i]) continue;
                rest[i] = inv * b.rotation; restLocal[i] = b.localRotation;
            }
            hipsRestRoot = inv * (hips.position - rootPos);
            hasSaw = saw; sawRest = hasSaw ? saw.localRotation : Quaternion.identity;
            if (has[BSawArm]) sawSide = (inv * (sawArm.position - rootPos)).x < 0 ? -1f : 1f;
            ikReady = has[BLFoot] && has[BRFoot]
                && BindLeg(ref legL, BLUp, BLLow, BLFoot, 0f, rootPos, inv)
                && BindLeg(ref legR, BRUp, BRLow, BRFoot, .5f, rootPos, inv);
            BindViews();
            lastPos = rootPos; lastFwd = rootRot * Vector3.forward; moveDir = lastFwd;
            bound = true;
        }

        /// Fallback when the component was added without the editor tool: same bone names the tool uses.
        void AutoBind()
        {
            var info = GetComponentInChildren<CarroneroRigInfo>(true); if (!info) return;
            hips = info.Bone("Hips"); spine = info.Bone("Spine1"); if (!spine) spine = info.Bone("Spine");
            spine2 = info.Bone("Spine2"); neck = info.Bone("Neck"); head = info.Bone("Head");
            leftUpLeg = info.Bone("LeftUpLeg"); leftLeg = info.Bone("LeftLeg"); leftFoot = info.Bone("LeftFoot");
            rightUpLeg = info.Bone("RightUpLeg"); rightLeg = info.Bone("RightLeg"); rightFoot = info.Bone("RightFoot");
            sawArm = info.Bone("LeftArm"); sawForeArm = info.Bone("LeftForeArm"); otherArm = info.Bone("RightArm");
            saw = info.saw; sawAxisLocal = info.sawAxisLocal;
        }

        bool BindLeg(ref Leg leg, int up, int low, int foot, float offset, Vector3 rootPos, Quaternion inv)
        {
            leg.up = up; leg.low = low; leg.foot = foot; leg.offset = offset; leg.stance = false; leg.plantRot = Quaternion.identity;
            Vector3 h = inv * (bones[up].position - rootPos), k = inv * (bones[low].position - rootPos), a = inv * (bones[foot].position - rootPos);
            leg.l1 = (k - h).magnitude; leg.l2 = (a - k).magnitude;
            float reach = (a - h).magnitude;
            if (leg.l1 < .01f || leg.l2 < .01f || reach < .01f) return false;
            Vector3 dn = (a - h) / reach, a0 = (k - h) / leg.l1, b0 = (a - k) / leg.l2;
            Vector3 pole = (k - h) - dn * Vector3.Dot(k - h, dn);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.forward - dn * dn.z; // straight leg at bind: bend forwards
            pole.Normalize();
            Vector3 n0 = Vector3.Cross(dn, pole);
            leg.pole = pole; leg.restLocal = a;
            leg.upFix = Quaternion.Inverse(Quaternion.LookRotation(a0, n0)) * rest[up];
            leg.lowFix = Quaternion.Inverse(Quaternion.LookRotation(b0, n0)) * rest[low];
            leg.hipInHips = Quaternion.Inverse(hips.rotation) * (bones[up].position - hips.position);
            return true;
        }

        void BindViews()
        {
            int count = 0;
            if (lodViews == null || lodViews.Length == 0)
            {
                // not authored: read the first enabled LODGroup that carries skinned renderers (allocates once, at Awake)
                foreach (var group in GetComponentsInChildren<LODGroup>(false))
                {
                    if (!group.enabled) continue;
                    var lods = group.GetLODs(); var found = new SkinnedMeshRenderer[lods.Length]; int any = 0;
                    for (int i = 0; i < lods.Length; i++)
                        foreach (var r in lods[i].renderers) if (r is SkinnedMeshRenderer smr) { found[i] = smr; any++; break; }
                    if (any > 0) { lodViews = found; break; }
                }
                if (lodViews == null || lodViews.Length == 0) lodViews = GetComponentsInChildren<SkinnedMeshRenderer>(false);
            }
            for (int i = 0; i < lodViews.Length; i++) if (lodViews[i]) count++;
            views = new SkinnedMeshRenderer[count]; viewTier = new int[count];
            for (int i = 0, k = 0; i < lodViews.Length; i++) if (lodViews[i]) { views[k] = lodViews[i]; viewTier[k] = i; k++; }
            lastView = 0;
        }

        // ------------------------------------------------------------------ events
        void Damaged(float amount, GameObject source)
        {
            float now = Time.time;
            if (now < nextHit || health.CurrentHealth <= 0f) return;
            nextHit = now + hitInterval; hitStart = now;
            Vector3 push = source ? transform.position - source.transform.position : -transform.forward; push.y = 0f;
            if (push.sqrMagnitude < 1e-4f) push = -transform.forward;
            push = Quaternion.Inverse(transform.rotation) * push.normalized;
            hitX = push.x; hitZ = push.z;
        }

        /// True while CarroneroMotion owns the pose. Never lets both components write the same bones.
        bool YieldToLegacy()
        {
            bool on = legacy.enabled, scavenging = legacy.CrouchTarget > 0f || legacy.DigTarget > 0f || legacy.ReachTarget > 0f;
            if (on)
            {
                // enabled by someone else: theirs until they disable it. Enabled by this component: back once the targets rest.
                if (scavenging || !handedOver) handBackAt = Time.time + .6f;
                else if (Time.time >= handBackAt) { legacy.enabled = false; handedOver = false; on = false; }
            }
            else if (scavenging) { legacy.enabled = true; handedOver = true; handBackAt = Time.time + .6f; on = true; }
            if (!on) { yielding = false; return false; }
            yielding = true; unseen = true; legsValid = false; LodTier = -1; LeftFootPlanted = RightFootPlanted = false;
            lastPos = transform.position; speed = 0f; turn = 0f;
            return true;
        }

        // ------------------------------------------------------------------ frame
        void LateUpdate()
        {
            if (!bound || !live) return;
            float dt = Time.deltaTime; if (dt <= 0f) return;
            if (hasLegacy && YieldToLegacy()) return;
            float now = Time.time;
            transform.GetPositionAndRotation(out Vector3 pos, out Quaternion rot);
            bool dead = hasHealth && health.CurrentHealth <= 0f;

            // timing runs on and off camera so the gait never restarts
            Vector3 step = pos - lastPos; step.y = 0f; lastPos = pos;
            float dist = dead ? 0f : step.magnitude, instant = dist / dt;
            speed = Mathf.Lerp(speed, instant, 1f - Mathf.Exp(-10f * dt));
            if (dist > 1e-4f) moveDir = step / dist;
            moving = speed > (moving ? .2f : .35f);
            cycleDist = (strideBase + strideGain * (speed < 9f ? speed : 9f)) / cadence;
            if (moving) { phase += dist / cycleDist; if (phase >= 1f) phase -= Mathf.Floor(phase); }
            Vector3 fwd = rot * Vector3.forward;
            float yaw = Mathf.Atan2(lastFwd.z * fwd.x - lastFwd.x * fwd.z, lastFwd.x * fwd.x + lastFwd.z * fwd.z) * Mathf.Rad2Deg; lastFwd = fwd;
            turn = Mathf.Lerp(turn, yaw / dt, 1f - Mathf.Exp(-dt / turnSmoothing));

            bool attacking = hasCombat && !dead && combat.IsAttacking;
            if (attacking != wasAttacking)
            {
                wasAttacking = attacking; armFrom = armPitch; foreFrom = forePitch; leanFrom = leanAtk;
                if (attacking) { attackStart = now; prep = Mathf.Max(.1f, combat.Preparation); Stage = AttackStage.Windup; }
                else { recoverStart = now; cockFrom = cock; engageFrom = engage; spinFrom = spinRate; Stage = AttackStage.Recover; }
            }
            if (Stage == AttackStage.Recover && now - recoverStart >= recoverSeconds) Stage = AttackStage.None;

            // camera gate: nothing is posed while no skinned renderer is drawn
            int tier = VisibleTier();
            LodTier = tier;
            if (tier < 0) { unseen = true; LeftFootPlanted = RightFootPlanted = false; return; }
            if (tier >= halfRateFromLod && ((Time.frameCount + parity) & 1) != 0) { LeftFootPlanted = RightFootPlanted = false; return; }
            bool legsOnly = tier >= legsOnlyFromLod, ik = ikReady && !legsOnly && tier <= ikMaxLod;
            int mode = legsOnly ? ModeLegs : ik ? (tier <= fineMaxLod && has[BSpine2] && has[BNeck] ? ModeFull : ModeIk) : ModeFk;
            if (unseen || mode != lastMode)
            {
                // A6: never jump from the stale pose to the new one; bones this tier does not pose go back to bind
                if (reenterBlend > 0f) BeginBlend(now); else ReleaseHeld(mode);
            }
            unseen = false; lastMode = mode;
            float poseDt = lastPoseTime >= 0f ? Mathf.Min(now - lastPoseTime, .1f) : dt; lastPoseTime = now;

            float move = Mathf.Clamp01(speed / 3f), a = phase * (2f * Mathf.PI), sinA = Mathf.Sin(a);
            float sawWave = Mathf.Sin(now * sawingRate + seed);
            EvaluateAttack(attacking, now, dt, sinA * sawArmSwing * move, sawWave);
            lockW = Mathf.MoveTowards(lockW, attacking ? 1f : 0f, dt * LockSpeed);
            float free = 1f - lockW, hit = HitEnvelope(now), hx = hitX * hit, hz = hitZ * hit;
            float turnNorm = Mathf.Clamp(turn / bankRate, -1f, 1f) * free;
            turnEase = Mathf.Lerp(turnEase, turnNorm, 1f - Mathf.Exp(-poseDt / turnSettle)); // eased in and out: mass, not a servo
            float turnAbs = Mathf.Abs(turnEase);
            bank = -bankMax * turnEase * (pivotBank + (1f - pivotBank) * move);
            float duty = Mathf.Lerp(dutyWalk, dutyRun, Mathf.InverseLerp(1.8f, 5f, speed));
            float bodyPitch = lean * move + leanAtk + pivotLean * turnAbs * (1f - move), chestYaw = chestWind * cock * sawSide, gaitTwist = sinA * twist * move * amp;
            float counter = -chestCounter * turnNorm;

            // hips: bob lowest at mid stance, wind-up drop and pull back, hit give (vertical only while sawing)
            float bobY = -bob * amp * move * Mathf.Cos(2f * a - 2f * Mathf.PI * duty) + Mathf.Sin(now * 3f + seed) * .006f;
            var offset = new Vector3(hx * hitGive * free, bobY - hipsDrop * cock - hit * hitGive * lockW - turnCrouch * turnAbs, hz * hitGive * free - hipsBack * cock);
            Vector3 hipsPos = pos + rot * (hipsRestRoot + offset);
            Quaternion hipsRot = rot * Quaternion.Euler(bodyPitch, chestYaw * .25f, bank) * rest[BHips];
            hips.SetPositionAndRotation(hipsPos, hipsRot);

            if (mode != ModeLegs)
            {
                bool fine = mode == ModeFull;
                float headYaw = Mathf.Sin(now * 1.7f + seed) * 6f * (1f - move) * free + headLead * turnNorm + HeadLock(attacking, pos, rot, fwd) * lockW;
                if (has[BSpine])
                {
                    // coarse tiers carry the flinch on Spine1, and never while the saw is engaged (it would move the blade)
                    float kick = fine ? 0f : hitChest * free;
                    bones[BSpine].rotation = rot * Quaternion.Euler(bodyPitch + kick * hz, gaitTwist + chestYaw * .5f + (fine ? counter * .5f : counter), bank * .6f - kick * hx) * rest[BSpine];
                }
                if (fine)
                {
                    float kick = hitChest * (1f - .5f * lockW), pump = Stage == AttackStage.Sustain ? sawWave * 3f : 0f;
                    bones[BSpine2].rotation = rot * Quaternion.Euler(bodyPitch + pump + kick * hz, gaitTwist + chestYaw + counter, bank * .6f - kick * hx) * rest[BSpine2];
                    bones[BNeck].rotation = rot * Quaternion.Euler(bodyPitch * .6f + hitHead * .7f * hz, headYaw * .5f, -hitHead * .5f * hx) * rest[BNeck];
                }
                if (has[BHead]) bones[BHead].rotation = rot * Quaternion.Euler(bodyPitch * .3f + hitHead * hz, headYaw, -hitHead * hx) * rest[BHead];
                if (has[BSawArm]) bones[BSawArm].rotation = rot * Quaternion.Euler(armPitch, chestYaw * .8f, 0f) * rest[BSawArm];
                if (has[BSawFore]) bones[BSawFore].rotation = rot * Quaternion.Euler(forePitch, chestYaw * .8f, 0f) * rest[BSawFore];
                if (has[BOther]) bones[BOther].rotation = rot * Pitch(-sinA * armSwing * move * amp - 15f * cock + 20f * (1f - cock) * engage - 10f * hit) * rest[BOther];
            }

            bool lockedL = false, lockedR = false;
            if (ik)
            {
                idle = Mathf.MoveTowards(idle, moving && instant > .3f ? 0f : 1f, dt * IdleSpeed);
                if (idle >= 1f) legsValid = false;
                var inv = new Quaternion(-rot.x, -rot.y, -rot.z, rot.w);
                Vector3 dirLocal = inv * moveDir;
                if (idle < 1f && !legsValid) { InitLeg(ref legL, pos, rot, dirLocal, duty, move); InitLeg(ref legR, pos, rot, dirLocal, duty, move); legsValid = true; }
                lockedL = PoseLeg(ref legL, pos, rot, inv, hipsPos, hipsRot, dirLocal, duty, move);
                lockedR = PoseLeg(ref legR, pos, rot, inv, hipsPos, hipsRot, dirLocal, duty, move);
            }
            else
            {
                legsValid = false; idle = 1f;
                LegFk(BLUp, BLLow, a, move, rot); LegFk(BRUp, BRLow, a + Mathf.PI, move, rot);
            }

            if (hasSaw && mode != ModeLegs)
            {
                spin = Mathf.Repeat(spin + (dead ? 0f : spinRate) * poseDt, 360f);
                saw.localRotation = sawRest * Quaternion.AngleAxis(spin, sawAxisLocal);
            }

            if (blending) ApplyBlend(now, mode);
            LeftFootPlanted = lockedL && !blending; RightFootPlanted = lockedR && !blending;
            LastPosedFrame = Time.frameCount;
        }

        /// Index of the LOD level being drawn, or -1 when no skinned renderer is enabled and visible.
        int VisibleTier()
        {
            int n = views.Length; if (n == 0) return 0;
            // the level seen last frame is almost always the one seen now: one test instead of four
            if (lastView < n) { var v = views[lastView]; if (v && v.enabled && v.isVisible) return viewTier[lastView]; }
            for (int i = 0; i < n; i++)
            {
                if (i == lastView) continue;
                var v = views[i];
                if (v && v.enabled && v.isVisible) { lastView = i; return viewTier[i]; }
            }
            return -1;
        }

        // ------------------------------------------------------------------ A3 / A4
        /// Saw arm, chest and saw speed over the attack. The clock runs one and a half frames ahead: SawBotCombat tests
        /// the blade in Update with the pose written here in the previous LateUpdate, so the contact pose has to be in
        /// place when its preparation ends, not one frame later.
        void EvaluateAttack(bool attacking, float now, float dt, float runArm, float sawWave)
        {
            if (attacking)
            {
                float tau = now + 1.5f * dt - attackStart, w1 = prep * windupFraction, w2 = prep * strikeFraction;
                float contactArm = attackRaise + sawWave * attackSaw, contactFore = attackRaise + sawWave * attackSaw * 1.5f;
                if (tau < w1)
                {
                    float k = EaseOut(tau / w1); Stage = AttackStage.Windup;
                    armPitch = Mathf.Lerp(armFrom, windupArm, k); forePitch = Mathf.Lerp(foreFrom, windupForeArm, k);
                    cock = k; engage = k; leanAtk = Mathf.Lerp(leanFrom, windupLean, k); spinRate = idleSpin;
                }
                else if (tau < w2)
                {
                    float tremor = Mathf.Sin(now * 71f + seed) * 1.6f; Stage = AttackStage.Hold;
                    armPitch = windupArm + tremor; forePitch = windupForeArm + tremor * 1.5f;
                    cock = 1f; engage = 1f; leanAtk = windupLean; spinRate = Mathf.Lerp(idleSpin, attackSpin, (tau - w1) / (w2 - w1));
                }
                else if (tau < prep)
                {
                    float u = (tau - w2) / (prep - w2), k = u * u; Stage = AttackStage.Strike;
                    armPitch = Mathf.Lerp(windupArm, contactArm, k); forePitch = Mathf.Lerp(windupForeArm, contactFore, k);
                    cock = 1f - k; engage = 1f; leanAtk = Mathf.Lerp(windupLean, attackLean, k); spinRate = attackSpin;
                }
                else
                {
                    Stage = AttackStage.Sustain;
                    armPitch = contactArm; forePitch = contactFore; cock = 0f; engage = 1f; leanAtk = attackLean; spinRate = attackSpin;
                }
                return;
            }
            if (Stage == AttackStage.Recover)
            {
                float r = now - recoverStart, whole = Mathf.Clamp01(r / recoverSeconds);
                if (r < reboundSeconds)
                {
                    float k = EaseOut(r / reboundSeconds);
                    armPitch = armFrom - reboundDegrees * k; forePitch = foreFrom - reboundDegrees * k;
                    leanAtk = Mathf.Lerp(leanFrom, -3f, k); cock = cockFrom * (1f - k);
                }
                else
                {
                    float k = Smooth((r - reboundSeconds) / Mathf.Max(.01f, recoverSeconds - reboundSeconds));
                    armPitch = Mathf.Lerp(armFrom - reboundDegrees, runArm, k); forePitch = Mathf.Lerp(foreFrom - reboundDegrees, runArm, k);
                    leanAtk = Mathf.Lerp(-3f, 0f, k); cock = 0f;
                }
                engage = engageFrom * (1f - whole); spinRate = Mathf.Lerp(spinFrom, idleSpin, whole);
                return;
            }
            Stage = AttackStage.None; armPitch = forePitch = runArm; cock = 0f; engage = 0f; leanAtk = 0f; spinRate = idleSpin;
        }

        float HeadLock(bool attacking, Vector3 pos, Quaternion rot, Vector3 fwd)
        {
            if (!attacking || !hasEnemy) return lockYaw;
            var target = enemy.KnownDetectedTarget; if (!target) return lockYaw;
            Vector3 to = target.transform.position - pos, right = rot * Vector3.right;
            lockYaw = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(to, right), Vector3.Dot(to, fwd)) * Mathf.Rad2Deg, -45f, 45f);
            return lockYaw;
        }

        // ------------------------------------------------------------------ A5
        /// 0..1: up in two frames, held until 0.07 s, back by 0.18 s with a small rebound.
        float HitEnvelope(float now)
        {
            float t = now - hitStart; if (t < 0f || t >= .18f) return 0f;
            if (t <= .07f) return Mathf.Clamp01((t + .012f) / .03f);
            float u = (t - .07f) / .11f, d = 1f - u;
            return d * d * Mathf.Cos(Mathf.PI * u);
        }

        // ------------------------------------------------------------------ A1
        void InitLeg(ref Leg leg, Vector3 pos, Quaternion rot, Vector3 dirLocal, float duty, float move)
        {
            float p = phase + leg.offset; if (p >= 1f) p -= 1f;
            leg.prevP = p;
            Vector3 home = leg.restLocal; home.z += footBias * move;
            if (p < duty) { leg.stance = true; leg.plant = pos + rot * home + moveDir * ((.5f * duty - p) * cycleDist); leg.plantRot = rot; }
            else { leg.stance = false; leg.liftPhase = duty; leg.liftLocal = home - dirLocal * (.5f * duty * cycleDist); leg.liftLocal.y = leg.restLocal.y; }
        }

        /// Places one foot and solves its leg. Returns true when the ankle is locked to the ground point and reached it.
        bool PoseLeg(ref Leg leg, Vector3 pos, Quaternion rot, Quaternion inv, Vector3 hipsPos, Quaternion hipsRot, Vector3 dirLocal, float duty, float move)
        {
            Vector3 restWorld = pos + rot * leg.restLocal, target = restWorld;
            Quaternion footRot = rot; float toe = 0f; bool locked = false;
            if (idle < 1f)
            {
                float p = phase + leg.offset; if (p >= 1f) p -= 1f;
                bool wrapped = p < leg.prevP; leg.prevP = p;
                Vector3 home = leg.restLocal; home.z += footBias * move;
                if (wrapped)
                {
                    // touch-down: where the foot would be had it landed exactly at phase 0, half a stance ahead
                    leg.stance = true; leg.plantRot = rot;
                    leg.plant = pos + rot * home + moveDir * ((.5f * duty - p) * cycleDist);
                }
                if (leg.stance && p >= duty)
                {
                    leg.stance = false; leg.liftPhase = p;
                    leg.liftLocal = inv * (leg.plant - pos); leg.liftLocal.y = leg.restLocal.y;
                }
                if (leg.stance)
                {
                    target = new Vector3(leg.plant.x, restWorld.y, leg.plant.z); footRot = leg.plantRot; locked = idle <= 0f;
                }
                else
                {
                    float s = Mathf.Clamp01((p - leg.liftPhase) / Mathf.Max(.001f, 1f - leg.liftPhase));
                    Vector3 local = Vector3.LerpUnclamped(leg.liftLocal, home + dirLocal * (.5f * duty * cycleDist), s * s * (3f - 2f * s));
                    local.y = leg.restLocal.y + stepHeight * amp * (.4f + .6f * move) * Mathf.Sin(Mathf.PI * s);
                    target = pos + rot * local;
                    toe = toeOff * move * Mathf.Sin(Mathf.PI * Mathf.Min(1f, s / .6f)); // heel up right after lift-off
                }
                if (idle > 0f)
                {
                    target = Vector3.LerpUnclamped(target, restWorld, idle); toe *= 1f - idle;
                    if (idle > .5f) footRot = rot;
                }
            }

            // analytic two-bone solve; the knee keeps the side it has in the bind pose
            Vector3 hip = hipsPos + hipsRot * leg.hipInHips, d = target - hip;
            float len = d.magnitude, reachMax = (leg.l1 + leg.l2) * .985f, reachMin = Mathf.Abs(leg.l1 - leg.l2) + .02f;
            if (len < 1e-5f) { d = Vector3.down; len = 1f; }
            Vector3 dn = d / len;
            bool reached = true;
            if (len > reachMax) { len = reachMax; reached = false; } else if (len < reachMin) { len = reachMin; reached = false; }
            Vector3 pole = rot * leg.pole, side = pole - dn * Vector3.Dot(pole, dn);
            float sm = side.magnitude;
            if (sm < 1e-4f) { side = rot * Vector3.forward; side -= dn * Vector3.Dot(side, dn); sm = side.magnitude; if (sm < 1e-4f) { side = rot * Vector3.right; sm = 1f; } }
            side /= sm;
            float cosH = Mathf.Clamp((leg.l1 * leg.l1 + len * len - leg.l2 * leg.l2) / (2f * leg.l1 * len), -1f, 1f), sinH = Mathf.Sqrt(1f - cosH * cosH);
            Vector3 upDir = dn * cosH + side * sinH, lowDir = (dn * len - upDir * leg.l1) / leg.l2, normal = Vector3.Cross(dn, side);
            bones[leg.up].rotation = Quaternion.LookRotation(upDir, normal) * leg.upFix;
            bones[leg.low].rotation = Quaternion.LookRotation(lowDir, normal) * leg.lowFix;
            bones[leg.foot].rotation = (toe != 0f ? footRot * Pitch(toe) : footRot) * rest[leg.foot];
            return locked && reached;
        }

        /// Far LODs: the old swing-and-lift legs, at the new cadence, without IK.
        void LegFk(int up, int low, float a, float move, Quaternion rot)
        {
            float swing = Mathf.Sin(a) * farLegSwing * move, lift = Mathf.Max(0f, Mathf.Cos(a)) * farKneeLift * move;
            bones[up].rotation = rot * Pitch(-swing - lift * .5f) * rest[up];
            bones[low].rotation = rot * Pitch(-swing + lift * .6f) * rest[low];
        }

        // ------------------------------------------------------------------ A6
        void BeginBlend(float now)
        {
            blendStart = now; blending = true; blendW = 0f;
            for (int i = 0; i < BoneCount; i++) if (has[i]) fromLocal[i] = bones[i].localRotation;
            fromHipsLocal = hips.localPosition;
        }

        /// Eases every bone from the pose it had when the bot came back into view (or changed LOD tier) to the pose just
        /// written. Bones a tier does not pose return to their bind rotation; bones it leaves alone are not touched.
        void ApplyBlend(float now, int mode)
        {
            float w = (now - blendStart) / reenterBlend;
            if (w >= 1f) { blending = false; blendW = 1f; ReleaseHeld(mode); return; }
            w = w * w * (3f - 2f * w); blendW = w;
            for (int i = 0; i < BoneCount; i++)
            {
                if (!has[i]) continue;
                int m = BoneMode(i, mode); if (m == Skip) continue;
                var b = bones[i];
                b.localRotation = Quaternion.SlerpUnclamped(fromLocal[i], m == Posed ? b.localRotation : restLocal[i], w);
            }
            hips.localPosition = Vector3.LerpUnclamped(fromHipsLocal, hips.localPosition, w);
        }

        void ReleaseHeld(int mode)
        {
            for (int i = 0; i < BoneCount; i++) if (has[i] && BoneMode(i, mode) == Hold) bones[i].localRotation = restLocal[i];
        }

        static int BoneMode(int bone, int mode)
        {
            switch (bone)
            {
                case BSpine2: case BNeck: return mode == ModeFull ? Posed : Hold;
                case BLFoot: case BRFoot: return mode <= ModeIk ? Posed : Hold;
                case BHips: case BLUp: case BLLow: case BRUp: case BRLow: return Posed;
                default: return mode == ModeLegs ? Skip : Posed;
            }
        }

        // ------------------------------------------------------------------ helpers
        static Quaternion Pitch(float degrees)
        {
            float h = degrees * (Mathf.Deg2Rad * .5f);
            return new Quaternion(Mathf.Sin(h), 0f, 0f, Mathf.Cos(h));
        }
        static float EaseOut(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
