using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace IronRequiem
{
    // Executions v02 (F2): additive rotation layer on the executed robot's EXISTING bones, driven only by the beats the
    // player side raises on EnemyExecutionSource (it never needs to know the player). Same pattern as RobotMotionAccents:
    // applied after the Animator in LateUpdate (order 160, after RobotMotionAccents 150) and removed in Update.
    // Channels are damped springs (stiffness ~22, damping ratio ~0.5) or held "servo" snaps; each channel moves at most
    // 25 degrees per frame. No allocation per frame, no GetComponent per frame. Never changes the Animator or any clip.
    // Sign conventions: Lean + = toward the player; Arm/Guard + = raised forward; Roll + = banks to the robot's right.
    [DefaultExecutionOrder(160)]
    [RequireComponent(typeof(EnemyExecutionSource))]
    public sealed class ExecutionReaction : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Profile
        {
            public float lean = 8, headYawToHand = 15, armRaise = 10;
            public float[] tugs = { -6, 4, -8 };
            public float tugInterval = .05f, twist = 3, hipsStruggle = .03f, hipsRip = .04f, headJitter = 2;
            public float armDrop = -35, armDropStiffness = 40, armDropDamping = .3f;
            public float pop = 4, back = -12, backDamping = .3f, headGlitch = 20;
            public float handoffLean = 4, handoffTwist, guard = 15, windUpGuardScale = 1.4f, armLimp = -20;
            public float strikeLean = -18, strikeHead = -25, strikeStiffness = 40;
            public float headYawToPlayer = 30, headServoHz;
            public float rollPulse, landLean, landRoll, landSink, fadeDelay = .09f, fadeSeconds = .25f;
            public float ripPull, ripTwist, ripRoll, ripSag; // Big Bot rip stagger (round 3)
            public bool sparksDuringStruggle = true;

            public static Profile For(ExecutionKind kind)
            {
                var p = new Profile();
                switch (kind)
                {
                    case ExecutionKind.SawBot:
                        p.back = -14; p.strikeLean = -20; p.handoffTwist = 6; p.handoffLean = 3;
                        break;
                    case ExecutionKind.BigBot:
                        p.lean = 0; p.armRaise = 10; p.headYawToHand = 20; p.headYawToPlayer = 20; p.headServoHz = 8;
                        p.rollPulse = 5; p.landLean = -4; p.landRoll = 6; p.landSink = .08f; p.tugs = new float[] { 5, -2, 6 }; /* round 2: the shoulder is a 3.5x lever: -14..-18 deg tugs plus a -7 deg landing lean sank the cannon ~0.6 m away from the hanging player; now the robot tries to LIFT the cannon he hangs from */ p.headGlitch = 20; p.guard = 55; p.handoffTwist = 8;
                        p.back = -14; p.ripPull = 5; p.ripTwist = 15; p.ripRoll = 8; p.ripSag = .12f; // round 3: rip stagger
                        p.handoffLean = 0; p.strikeLean = -15; p.strikeHead = 0; p.hipsStruggle = .0f; p.hipsRip = 0; p.sparksDuringStruggle = false;
                        p.fadeSeconds = .3f;
                        break;
                }
                return p;
            }
        }

        const int Lean = 0, Twist = 1, Roll = 2, HeadYaw = 3, HeadPitch = 4, Arm = 5, Guard = 6, Hips = 7, WheelY = 8, Count = 9;
        const float MaxDegreesPerFrame = 25;

        sealed class Slot { public Transform T; public Quaternion Rotation; public Vector3 Position; public bool Applied; }
        enum HeadMode { None, Hand, Player }

        // Not serialized: a serialized [Serializable] class is never null, so the per-kind defaults of Profile.For would be
        // frozen to the T-07 values when the prefab was authored. Always rebuilt from the kind in Awake.
        [System.NonSerialized] Profile profile;
        [SerializeField, Range(4, 40)] float defaultStiffness = 22;
        [SerializeField, Range(.1f, 1.5f)] float defaultDamping = .5f;

        EnemyExecutionSource source; Health health; ExecutionKind kind;
        Slot chest, head, armWeapon, armGuard, hips, wheelL, wheelR;
        bool headSharesChest;
        T07CannonSocket cannonSocket; SawBotCombat saw; ExecutionPullIn pull; Rig[] rigs; float[] rigWeights;
        float wheelSign = 1, wheelExtra;
        readonly float[] pos = new float[Count], vel = new float[Count], target = new float[Count], stiff = new float[Count], zeta = new float[Count], output = new float[Count];
        readonly bool[] direct = new bool[Count];
        bool active, subscribed, dying, dieHooked;
        ExecutionBeat beat; float beatTime, weight, fadeStart = -1, deadAt;
        Vector3 toPlayer = Vector3.forward, playerPosition, handPosition;
        HeadMode headMode; int tugIndex, freezeFrames; bool spark1, spark2, rollA, rollB, rollC; float nextServo, servoYaw;
        ParticleSystem breakSparks; RobotHitFeedback feedback; bool missingLogged;

        public bool IsActive => active;
        public ExecutionBeat CurrentBeat => beat;
        public float Weight => weight;

        void Awake()
        {
            source = GetComponent<EnemyExecutionSource>(); health = GetComponent<Health>(); kind = source.Kind;
            profile = Profile.For(kind);
            cannonSocket = GetComponentInChildren<T07CannonSocket>(true); saw = GetComponent<SawBotCombat>(); pull = GetComponent<ExecutionPullIn>();
            feedback = GetComponent<RobotHitFeedback>(); rigs = GetComponentsInChildren<Rig>(true); rigWeights = new float[rigs.Length];
            ResolveBones();
            if (kind == ExecutionKind.BigBot) foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) if (ps.name == "CannonBreak") { breakSparks = ps; break; }
            if (saw && saw.LeftWheel && saw.RightWheel)
            {
                wheelL = new Slot { T = saw.LeftWheel }; wheelR = new Slot { T = saw.RightWheel };
                wheelSign = Mathf.Sign(Vector3.Dot(saw.LeftWheel.parent.TransformDirection(Vector3.right), transform.right));
            }
        }

        // Bones are looked up by name, preferring the transforms that actually skin a mesh (the T-07 rig has a second
        // "Head" under ArticulationGuides_NoRig). Missing bones degrade to "no motion on that channel", logged once.
        void ResolveBones()
        {
            var skin = new System.Collections.Generic.HashSet<Transform>();
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var bone in smr.bones) if (bone) skin.Add(bone);
            var all = GetComponentsInChildren<Transform>(true);
            // Several rigs can reuse a name (the Mech has Head / Chest under ArticulationGuides_NoRig and Tripo duplicates):
            // score = skinned (+2), under AnimatedRig / T07Rig (+1), under a NoRig guide (-5); first best wins.
            Transform Find(params string[] names)
            {
                foreach (var n in names)
                {
                    Transform best = null; int bestScore = int.MinValue;
                    foreach (var t in all)
                    {
                        if (t.name != n) continue;
                        int score = skin.Contains(t) ? 2 : 0;
                        for (var p = t; p && p != transform; p = p.parent)
                        {
                            if (p.name == "AnimatedRig" || p.name == "T07Rig") score += 1;
                            if (p.name == "ArticulationGuides_NoRig") score -= 5;
                        }
                        if (score > bestScore) { bestScore = score; best = t; }
                    }
                    if (best && bestScore >= 0) return best;
                    if (best && n == names[names.Length - 1]) return best;
                }
                return null;
            }
            Slot Make(Transform t) => t ? new Slot { T = t } : null;
            string missing = "";
            switch (kind)
            {
                case ExecutionKind.SawBot:
                    chest = Make(Find("Body")); armWeapon = Make(Find("UpperArm_R")); armGuard = Make(Find("UpperArm_L"));
                    break;
                case ExecutionKind.BigBot:
                    chest = Make(Find("Torso", "Chest")); armWeapon = Make(Find("Shoulder_R", "UpperArm_R")); armGuard = Make(Find("Shoulder_L", "UpperArm_L"));
                    hips = Make(Find("Pelvis", "Hips")); head = chest; headSharesChest = true;
                    break;
                default: // T-07 shared rig and the Tripo Mech: both have Hips / Chest / Head / UpperArm_L (cannon side) / UpperArm_R.
                    chest = Make(Find("Chest", "UpperChest", "Spine")); head = Make(Find("Head", "Neck")); hips = Make(Find("Hips"));
                    armWeapon = Make(Find("UpperArm_L", "Left_UpperArm")); armGuard = Make(Find("UpperArm_R", "Right_UpperArm"));
                    break;
            }
            if (chest == null) missing += " chest"; if (armWeapon == null) missing += " weaponArm"; if (armGuard == null) missing += " guardArm";
            if (kind != ExecutionKind.SawBot && head == null) missing += " head";
            BoneReport = kind + " " + name.Replace("(Clone)", "") + ": chest=" + Path(chest) + " head=" + Path(head) + " weaponArm=" + Path(armWeapon) + " guardArm=" + Path(armGuard) + " hips=" + Path(hips);
            if (missing.Length > 0 && !missingLogged) { missingLogged = true; Debug.LogWarning("ExecutionReaction on " + name + " (" + kind + "): bones not found:" + missing + ". Those channels are skipped."); }
        }

        static readonly System.Collections.Generic.HashSet<string> LoggedReports = new System.Collections.Generic.HashSet<string>();
        public string BoneReport { get; private set; }
        string Path(Slot s) { if (s == null || !s.T) return "-"; var t = s.T; string r = t.name; for (var p = t.parent; p && p != transform; p = p.parent) r = p.name + "/" + r; return r; }
        Transform compJoint; bool diagLogged;
        // Degrees the layer currently adds to the chest bone (local) and whether the layer wrote this frame: for the F3 harness.
        public float LastChestDegrees { get; private set; }

        void OnEnable() { if (source) { source.BeatReached += OnBeat; subscribed = true; } }
        void Start() { if (health && !dieHooked) { health.OnDie += OnDie; dieHooked = true; } }
        void OnDisable()
        {
            if (subscribed && source) source.BeatReached -= OnBeat;
            subscribed = false; if (dieHooked && health) health.OnDie -= OnDie; dieHooked = false;
            Deactivate(true);
        }

        // Subscribed in Start so it runs after the drivers that cut the foot IK rig on death: this layer then holds the
        // rig weights and lets them fall over a quarter of a second instead of snapping the legs in one frame.
        void OnDie()
        {
            if (!active && !dying) return;
            if (source && (source.Delivered || source.InExecution) && rigs != null && rigs.Length > 0)
            {
                if (!dying) { for (int i = 0; i < rigs.Length; i++) rigWeights[i] = rigs[i] ? Mathf.Max(rigWeights[i], 0) : 0; }
                dying = true; deadAt = Time.time;
                for (int i = 0; i < rigs.Length; i++) if (rigs[i]) rigs[i].weight = rigWeights[i];
            }
        }

        float Flat(Vector3 v, out Vector3 dir) { v.y = 0; float m = v.magnitude; dir = m > 1e-4f ? v / m : Vector3.zero; return m; }
        float YawTo(Vector3 worldDirection)
        {
            var f = Vector3.ProjectOnPlane(transform.forward, Vector3.up); var d = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (f.sqrMagnitude < 1e-4f || d.sqrMagnitude < 1e-4f) return 0;
            return Vector3.SignedAngle(f, d, Vector3.up);
        }
        void Drive(int c, float t, float k, float z) { target[c] = t; stiff[c] = k; zeta[c] = z; direct[c] = false; }
        void Drive(int c, float t) => Drive(c, t, defaultStiffness, defaultDamping);
        void Snap(int c, float value) { pos[c] = value; vel[c] = 0; target[c] = value; direct[c] = true; }

        void OnBeat(ExecutionBeatInfo info)
        {
            if (info.Beat == ExecutionBeat.Cancel) { Deactivate(true); return; }
            if (info.Beat == ExecutionBeat.End) { if (active && fadeStart < 0) fadeStart = Time.time - profile.fadeDelay; return; }
            if (info.Beat == ExecutionBeat.Begin)
            {
                Deactivate(true); active = true; weight = 1; fadeStart = -1; dying = false;
                for (int i = 0; i < Count; i++) { pos[i] = vel[i] = output[i] = 0; target[i] = 0; stiff[i] = defaultStiffness; zeta[i] = defaultDamping; direct[i] = false; }
                headMode = HeadMode.None; wheelExtra = 0; compJoint = source.WeaponJoint; diagLogged = false;
                for (int i = 0; i < rigs.Length; i++) rigWeights[i] = rigs[i] ? rigs[i].weight : 0;
            }
            if (!active) return;
            beat = info.Beat; beatTime = Time.time; tugIndex = 0; spark1 = spark2 = rollA = rollB = rollC = false; nextServo = 0;
            playerPosition = info.PlayerPosition; handPosition = info.HandPosition;
            Flat(-info.ToRobot, out toPlayer);
            if (toPlayer == Vector3.zero) Flat(playerPosition - transform.position, out toPlayer);
            if (toPlayer == Vector3.zero) toPlayer = -transform.forward;
            if (kind == ExecutionKind.BigBot) EnterBig(info.Beat); else EnterArmed(info.Beat);
        }

        void EnterArmed(ExecutionBeat b)
        {
            var p = profile;
            switch (b)
            {
                case ExecutionBeat.Begin:
                    Drive(Lean, p.lean); Drive(Arm, p.armRaise); headMode = HeadMode.Hand;
                    break;
                case ExecutionBeat.Struggle:
                    headMode = HeadMode.None; Drive(Hips, -p.hipsStruggle, 30, .6f);
                    if (saw) saw.SetExecutionChain(2);
                    break;
                case ExecutionBeat.Rip:
                    freezeFrames = 2; headMode = HeadMode.None;
                    Snap(Lean, p.pop); Snap(HeadYaw, p.headGlitch * (YawTo(toPlayer) >= 0 ? 1 : -1));
                    Drive(Arm, p.armDrop, p.armDropStiffness, p.armDropDamping); Drive(Hips, -p.hipsRip, 30, .6f); Drive(Twist, 0);
                    if (saw) { saw.SetExecutionChain(0); pos[WheelY] = .02f; Drive(WheelY, 0, 40, .3f); }
                    break;
                case ExecutionBeat.Handoff:
                    headMode = HeadMode.Player; Drive(Lean, p.handoffLean); Drive(Arm, p.armLimp, defaultStiffness, .6f); Drive(Guard, p.guard); Drive(Hips, 0);
                    Drive(Twist, Mathf.Clamp(YawTo(toPlayer), -p.handoffTwist, p.handoffTwist));
                    break;
                case ExecutionBeat.WindUp:
                    Drive(Guard, p.guard * p.windUpGuardScale);
                    break;
                case ExecutionBeat.Strike:
                    headMode = HeadMode.None; Drive(Lean, p.strikeLean, p.strikeStiffness, .35f); Drive(HeadPitch, p.strikeHead, p.strikeStiffness, .35f);
                    Drive(HeadYaw, 0); Drive(Guard, 0, defaultStiffness, .6f); Drive(Twist, 0);
                    fadeStart = Time.time + p.fadeDelay; break;
                case ExecutionBeat.Recover:
                    if (fadeStart < 0) fadeStart = Time.time; break;
            }
        }

        void EnterBig(ExecutionBeat b)
        {
            var p = profile;
            switch (b)
            {
                case ExecutionBeat.Begin:
                    headMode = HeadMode.Player; Drive(Guard, 10); break;
                case ExecutionBeat.Grab:
                    // The player's weight lands on the shoulder: the body sinks and banks toward the player, then rebounds (zeta .35).
                    Snap(Lean, p.landLean); Drive(Lean, 0, defaultStiffness, .35f);
                    Snap(Roll, p.landRoll * (Vector3.Dot(toPlayer, transform.right) >= 0 ? 1 : -1)); Drive(Roll, 0, defaultStiffness, .35f);
                    Snap(Hips, -p.landSink); Drive(Hips, 0, defaultStiffness, .35f); break;
                case ExecutionBeat.Struggle:
                    headMode = HeadMode.None; break;
                case ExecutionBeat.Rip:
                {
                    // Round 3: a readable stagger. The yank pulls the torso toward the player and twists it to the torn side, the
                    // hips sag, then (after the 2-frame freeze) it reels back past upright (p.back, underdamped) and settles.
                    float side = YawTo(toPlayer) >= 0 ? 1 : -1;
                    freezeFrames = 2; headMode = HeadMode.None;
                    Snap(Lean, p.ripPull); Snap(HeadYaw, p.headGlitch * .4f * -side);
                    Snap(Twist, p.ripTwist * side); Drive(Twist, 0, 14, .35f);
                    Snap(Roll, p.ripRoll * side); Drive(Roll, 0, 16, .35f);
                    Snap(Hips, -p.ripSag); Drive(Hips, 0, 12, .5f);
                    Drive(Arm, p.armDrop, p.armDropStiffness, p.armDropDamping);
                }
                    break;
                case ExecutionBeat.Handoff:
                    headMode = HeadMode.Player; Drive(Arm, 0, defaultStiffness, .6f); Drive(Guard, p.guard, 14, .6f); Drive(Lean, 0);
                    Drive(Twist, Mathf.Clamp(YawTo(toPlayer), -p.handoffTwist, p.handoffTwist));
                    break;
                case ExecutionBeat.Strike:
                    headMode = HeadMode.None; Drive(Lean, p.strikeLean, p.strikeStiffness, .35f); Drive(HeadYaw, 0); Drive(Guard, 20, defaultStiffness, .6f); Drive(Twist, 0); Drive(Roll, 0);
                    fadeStart = Time.time + p.fadeDelay; break;
                case ExecutionBeat.Recover:
                    if (fadeStart < 0) fadeStart = Time.time; break;
            }
        }

        void Update()
        {
            Restore();
            if (cannonSocket && active && weight > .01f) cannonSocket.YieldAim(.2f);
        }

        void Restore()
        {
            RestoreSlot(chest); RestoreSlot(head); RestoreSlot(armWeapon); RestoreSlot(armGuard); RestoreSlot(hips); RestoreSlot(wheelL); RestoreSlot(wheelR);
        }
        static void RestoreSlot(Slot s)
        {
            if (s == null || !s.Applied) return;
            if (s.T) { s.T.localRotation = s.Rotation; s.T.localPosition = s.Position; }
            s.Applied = false;
        }
        static void Mark(Slot s) { if (s != null && !s.Applied && s.T) { s.Rotation = s.T.localRotation; s.Position = s.T.localPosition; s.Applied = true; } }

        void LateUpdate()
        {
            if (!active)
            {
                if (dying) EaseRigs();
                return;
            }
            float dt = Time.deltaTime;
            if (fadeStart >= 0)
            {
                float f = (Time.time - fadeStart) / Mathf.Max(.01f, profile.fadeSeconds);
                weight = 1 - Mathf.SmoothStep(0, 1, f);
                if (f >= 1) { Deactivate(false); return; }
            }
            if (dt > 0)
            {
                Schedule();
                if (freezeFrames > 0) { freezeFrames--; if (freezeFrames == 0) AfterFreeze(); }
                else Integrate(Mathf.Min(dt, 1f / 30f));
                if (saw && pull) { wheelExtra += -1.6f * Vector3.Dot(pull.LastStep, transform.forward) / .29f * Mathf.Rad2Deg; }
                if (saw && (!pull || !pull.Pulling)) wheelExtra *= Mathf.Exp(-6 * dt);
            }
            Apply();
            if (dying) EaseRigs();
        }

        void EaseRigs()
        {
            float k = Mathf.SmoothStep(0, 1, (Time.time - deadAt) / .25f);
            for (int i = 0; i < rigs.Length; i++) if (rigs[i]) rigs[i].weight = rigWeights[i] * (1 - k);
            if (k >= 1) dying = false;
        }

        void AfterFreeze()
        {
            var p = profile;
            Drive(Lean, p.back, defaultStiffness, p.backDamping); Drive(HeadYaw, 0);
        }

        // Time-based events inside the current beat.
        void Schedule()
        {
            var p = profile; float t = Time.time - beatTime;
            // coordinator: diagnostic log (Mech freeze investigation) removed for gameplay; BoneReport stays readable
            if (beat == ExecutionBeat.Struggle)
            {
                while (tugIndex < 3 && t >= tugIndex * p.tugInterval)
                {
                    float sign = tugIndex % 2 == 0 ? 1 : -1;
                    Snap(Arm, p.tugs[tugIndex]);
                    if (kind != ExecutionKind.BigBot) { Snap(Twist, sign * p.twist); Snap(HeadYaw, sign * p.headJitter); }
                    tugIndex++;
                }
                if (p.sparksDuringStruggle)
                {
                    if (!spark1 && t >= .08f) { spark1 = true; Sparks(3); }
                    if (!spark2 && t >= .13f) { spark2 = true; Sparks(3); }
                }
                if (kind == ExecutionKind.BigBot)
                {
                    if (!rollA && t >= 0) { rollA = true; Drive(Roll, p.rollPulse, 40, .4f); }
                    if (!rollB && t >= .08f) { rollB = true; Drive(Roll, -p.rollPulse, 40, .4f); }
                    if (!rollC && t >= .16f) { rollC = true; Drive(Roll, 0, 30, .6f); if (breakSparks) breakSparks.Emit(4); }
                }
            }
            if (headMode != HeadMode.None)
            {
                float yaw;
                if (headMode == HeadMode.Hand) yaw = Mathf.Clamp(YawTo(handPosition - (head != null && head.T ? head.T.position : transform.position)), -p.headYawToHand, p.headYawToHand);
                else
                {
                    if (p.headServoHz > 0) { if (Time.time >= nextServo) { nextServo = Time.time + 1f / p.headServoHz; servoYaw = Mathf.Clamp(YawTo(playerPosition - transform.position), -p.headYawToPlayer, p.headYawToPlayer); } yaw = servoYaw; }
                    else yaw = Mathf.Clamp(YawTo(playerPosition - transform.position), -p.headYawToPlayer, p.headYawToPlayer);
                }
                Drive(HeadYaw, yaw, 30, .6f);
            }
        }

        void Sparks(int count)
        {
            var ps = feedback ? feedback.Sparks : null; var joint = source.WeaponJoint;
            if (!ps) return;
            if (joint) ps.transform.position = joint.position;
            if (!ps.isPlaying) ps.Play(false);
            ps.Emit(count);
        }

        void Integrate(float dt)
        {
            for (int c = 0; c < Count; c++)
            {
                if (direct[c]) continue;
                float k = stiff[c], z = zeta[c];
                vel[c] += ((target[c] - pos[c]) * k * k - vel[c] * 2 * z * k) * dt;
                pos[c] += vel[c] * dt;
            }
        }

        void Apply()
        {
            if (weight <= .001f) return;
            // Per-frame limit: every angular channel moves at most MaxDegreesPerFrame from what it showed last frame.
            for (int c = 0; c < Count; c++)
            {
                float want = pos[c] * weight;
                if (c == Hips || c == WheelY) { output[c] = want; continue; }
                output[c] = output[c] + Mathf.Clamp(want - output[c], -MaxDegreesPerFrame, MaxDegreesPerFrame);
            }
            bool compensate = compJoint && kind != ExecutionKind.BigBot && (beat == ExecutionBeat.Begin || beat == ExecutionBeat.Grab || beat == ExecutionBeat.Struggle);
            Vector3 jointBase = compensate ? compJoint.position : Vector3.zero;
            // Servo life that does not depend on any Animator (the Mech's root Animator is disabled, so nothing else moves its bones).
            // Coordinator (executions-v02): a smooth circular sway instead of a per-frame zigzag (30 Hz jitter). Twist and roll move
            // in quadrature, so the chest turns a near-constant ~0.2 deg per frame at 60 FPS without any reversal flicker. Scales with
            // the layer weight only and is applied after the joint baseline, so the +-3 cm joint rule cancels only its translation.
            float servoPhase = Time.time * 9f;
            float tremor = Mathf.Sin(servoPhase) * 1.5f * weight, tremorRoll = Mathf.Cos(servoPhase) * 1.5f * weight, zig = Mathf.Sin(servoPhase + 1.3f) * .8f * weight;
            var up = transform.up; var axis = Vector3.Cross(up, toPlayer); if (axis.sqrMagnitude < 1e-4f) axis = transform.right; axis.Normalize();
            if (hips != null && hips.T && Mathf.Abs(output[Hips]) > 1e-5f) { Mark(hips); hips.T.position += up * output[Hips]; }
            if (chest != null && chest.T)
            {
                float scale = 1;
                var q = Quaternion.AngleAxis(output[Twist] + tremor, up) * Quaternion.AngleAxis(output[Lean], axis) * Quaternion.AngleAxis(output[Roll] + tremorRoll, transform.forward);
                if (headSharesChest) q = Quaternion.AngleAxis(output[HeadYaw] * .4f * scale, up) * q;
                if (!IsIdentity(q)) { Mark(chest); chest.T.rotation = q * chest.T.rotation; }
            }
            if (head != null && head.T && !headSharesChest)
            {
                var q = Quaternion.AngleAxis(output[HeadYaw] - zig, up) * Quaternion.AngleAxis(output[HeadPitch], axis);
                if (!IsIdentity(q)) { Mark(head); head.T.rotation = q * head.T.rotation; }
            }
            if (armWeapon != null && armWeapon.T) { Mark(armWeapon); armWeapon.T.rotation = Quaternion.AngleAxis(-(output[Arm] + zig * 1.5f), transform.right) * armWeapon.T.rotation; }
            if (armGuard != null && armGuard.T) { Mark(armGuard); armGuard.T.rotation = Quaternion.AngleAxis(-(output[Guard] - zig * 1.5f), transform.right) * armGuard.T.rotation; }
            LastChestDegrees = chest != null && chest.T && chest.Applied ? Quaternion.Angle(chest.Rotation, chest.T.localRotation) : 0;
            if (compensate)
            {
                // The joint the hand closes on may drift at most 3 cm from where the animation + pull leave it: whatever the
                // lean and the arm tugs add beyond that is cancelled by translating the root-most bone (the arm turns about the joint).
                var delta = compJoint.position - jointBase; var excess = delta - Vector3.ClampMagnitude(delta, .03f);
                var shift = hips != null ? hips : chest;
                if (excess.sqrMagnitude > 1e-8f && shift != null && shift.T && compJoint.IsChildOf(shift.T)) { Mark(shift); shift.T.position -= excess; }
            }
            if (wheelL != null && wheelR != null)
            {
                if (Mathf.Abs(wheelExtra) > .01f)
                {
                    Mark(wheelL); Mark(wheelR);
                    var spin = Quaternion.AngleAxis(wheelExtra * wheelSign * weight, Vector3.right);
                    wheelL.T.localRotation = wheelL.T.localRotation * spin; wheelR.T.localRotation = wheelR.T.localRotation * spin;
                }
                if (Mathf.Abs(output[WheelY]) > 1e-4f)
                {
                    Mark(wheelL); Mark(wheelR);
                    wheelL.T.position += up * output[WheelY]; wheelR.T.position += up * output[WheelY];
                }
            }
        }
        static bool IsIdentity(Quaternion q) => Mathf.Abs(q.w) > .999995f;

        // soft = fade the layer out (End); hard = remove at once (Cancel, disable, new Begin).
        void Deactivate(bool hard)
        {
            if (!active && !hard) return;
            active = false; weight = 0; fadeStart = -1; freezeFrames = 0; headMode = HeadMode.None; wheelExtra = 0;
            Restore();
            if (saw) saw.SetExecutionChain(-1);
            if (hard && dying) { dying = false; }
        }
    }
}
