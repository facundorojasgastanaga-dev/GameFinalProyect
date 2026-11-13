using System;
using UnityEngine;

namespace IronRequiem
{
    /// Procedural "wake up, break out and look around" performance for the rigged protagonist (Mixamo-style bones).
    /// Evaluate(t) is a pure function of the cinematic time: every call restores the bind pose and rebuilds the frame
    /// (body path, turn in place, analytic two-bone IK for planted feet / rail hand / punching and inspecting arm, head
    /// and torso aim, finger curl), so scrubbing, skipping or pausing the PlayableDirector gives the same pose.
    /// 12/11/2026 v2: knees solved analytically towards a pole that follows the body (never backwards), foot targets
    /// clamped to the leg length and below the hips; glass punches; hand inspection; head/torso turn to the conveyor
    /// and back to the scavengers with re-planted feet.
    /// 12/11/2026 v4 (v30): no servo/cannon beat (the visor shot cuts straight to a fist already driving into the glass);
    /// the capsule's cradle tilts forward (CradleTransform, shared with CapsuleRestraints); the exit has weight: left hand
    /// on the rail, right foot lifted OVER the capsule lip on an arc and planted, weight transferred, left foot over the
    /// lip, then standing up. Feet stay planted (cradle footrest or floor) with the hips lowered when a leg would
    /// over-extend. Head aim is spread Head 70 % / Neck 20 % / Spine2 10 % and clamped to believable neck ranges.
    [DefaultExecutionOrder(200)]
    public sealed class WakeUpRobotPose : MonoBehaviour
    {
        [Serializable]
        public struct Timing
        {
            public float fingersStart, fingersEnd, cannonStart, cannonEnd;   // cannon beat removed in v4 (start == end)
            public float punch1, punch2;                       // fist impacts on the canopy glass (crack, shatter)
            public float releaseAt, cablePop, cradleStart, cradleEnd;        // restraints open, back cables pop, cradle tilts
            public float leave, rightStepStart, rightStepEnd, transferEnd, railIn, railOut, leftStepStart, leftStepEnd, crouch, stand;
            public float handStart, handEnd;                   // inspects the right hand
            public float beltLook, turnStart, turnEnd, turnBackStart, turnBackEnd; // conveyor look and back to the scavengers
        }

        [SerializeField] Transform model;
        [SerializeField] Transform pod;            // capsule frame: model "up" is pod +Y, visor faces pod -Z
        [SerializeField] Transform railGrip;
        [SerializeField] Transform standPoint;
        [SerializeField] Transform lookTarget;     // scavengers
        [SerializeField] Transform beltTarget;     // conveyor in the back of the hangar
        [SerializeField] Transform glassImpact;    // inner face of the canopy where the fist lands
        [SerializeField] float soleOffset = .05f;
        [SerializeField, Range(30, 160)] float beltTurnDegrees = 105f;
        [SerializeField, Range(0, 120)] float maxWristRoll = 50f;   // v30: 80 -> 50, a mechanical wrist with a believable range
        [Header("Capsule (pod-local)")]
        [SerializeField] Vector3 cradlePivotLocal = new Vector3(0, -1.35f, .31f);
        [SerializeField, Range(0, 30)] float cradleTilt = 16f;      // degrees, top forward
        [SerializeField, Range(0, .3f)] float cradlePush = .1f;     // metres, horizontally out of the capsule
        [SerializeField] float lipLocalY = -1.42f, lipLocalZ = -.49f; // top front edge of the foot cap
        [SerializeField, Range(.02f, .2f)] float lipClearance = .08f; // sole above the lip edge at the top of the arc
        [Header("Neck limits (degrees, relative to the chest)")]
        [SerializeField] float neckYaw = 70f, neckPitchDown = 40f, neckPitchUp = 30f, neckRoll = 20f;
        [SerializeField] Timing timing = new Timing
        {
            // v4 (v30): the servo/cannon beat is gone; everything after the glass is ~2.6 s earlier than v3
            fingersStart = 2.4f, fingersEnd = 3.4f, cannonStart = 0f, cannonEnd = 0f,
            punch1 = 4.0f, punch2 = 5.6f,
            releaseAt = 6.3f, cablePop = 6.55f, cradleStart = 6.9f, cradleEnd = 8.2f,
            leave = 8.0f, railIn = 8.1f, railOut = 11.4f,
            rightStepStart = 8.9f, rightStepEnd = 10.1f, transferEnd = 10.9f, leftStepStart = 10.95f, leftStepEnd = 12.05f,
            crouch = 12.05f, stand = 13.6f,
            handStart = 14.0f, handEnd = 18.0f,
            beltLook = 18.0f, turnStart = 18.7f, turnEnd = 20.5f, turnBackStart = 22.0f, turnBackEnd = 24.0f
        };

        Transform[] all; Vector3[] bindPos; Quaternion[] bindRot;
        Transform hips, spine, spine1, spine2, neck, head;
        Transform lUp, lLow, lFoot, lToe, rUp, rLow, rFoot, rToe, lArm, lFore, lHand, rArm, rFore, rHand;
        Transform[] lFingers, rFingers; float[] fingerAngles;
        Vector3 root0, hipsOffsetModel, lFootModel, rFootModel, lToeModel, rToeModel, fwdModel, headFwdLocal, headUpLocal, chestFwdLocal, chestUpLocal;
        Quaternion rootRot0, lFootRotModel, rFootRotModel;
        float legLength, armLength;
        bool bound;
        readonly Vector3[] path = new Vector3[5];

        public Timing Times => timing;
        public Transform Model => model;
        public float StandTime => timing.stand;
        public Transform RightForearm => rFore;
        public Transform RightHand => rHand;
        public Transform Head => head;
        public Vector3 RightFootPlanted { get; private set; }
        public Vector3 LeftFootPlanted { get; private set; }
        public bool IsBound => bound;
        public Transform StandPoint => standPoint;
        public Transform RightFoot => rFoot;
        public Transform LeftFoot => lFoot;
        public Transform RightToe => rToe;
        public Transform LeftToe => lToe;
        public Transform Pod => pod;
        /// Floor height under the stand point (the cinematic floor).
        public float FloorY => standPoint ? standPoint.position.y : 0;
        /// Height of the foot bone and of the toe bone above the sole when the robot stands (rig constants).
        public float AnkleHeight { get; private set; }
        public float ToeHeight { get; private set; }
        /// Visor pulse request (0..1) while the robot studies its hand; read by the cinematic.
        public float Curiosity { get; private set; }

        public void Configure(Transform robot, Transform capsule, Transform grip, Transform stand, Transform look)
        { model = robot; pod = capsule; railGrip = grip; standPoint = stand; lookTarget = look; }
        public void ConfigureActing(Transform belt, Transform impact) { beltTarget = belt; glassImpact = impact; }

        /// Caches the bind pose. Call once, while the robot still lies in the capsule exactly as authored.
        public bool Bind()
        {
            if (bound) return true;
            if (!model || !pod || !standPoint) return false;
            all = model.GetComponentsInChildren<Transform>(true);
            hips = Bone("Hips"); spine = Bone("Spine"); spine1 = Bone("Spine1"); spine2 = Bone("Spine2"); neck = Bone("Neck"); head = Bone("Head");
            lUp = Bone("LeftUpLeg"); lLow = Bone("LeftLeg"); lFoot = Bone("LeftFoot"); lToe = Bone("LeftToeBase");
            rUp = Bone("RightUpLeg"); rLow = Bone("RightLeg"); rFoot = Bone("RightFoot"); rToe = Bone("RightToeBase");
            lArm = Bone("LeftArm"); lFore = Bone("LeftForeArm"); lHand = Bone("LeftHand");
            rArm = Bone("RightArm"); rFore = Bone("RightForeArm"); rHand = Bone("RightHand");
            lFingers = Fingers("Left"); rFingers = Fingers("Right");
            if (!hips || !lUp || !lLow || !lFoot || !rUp || !rLow || !rFoot) { Debug.LogWarning("[HangarIntro] robot rig incomplete", this); return false; }
            root0 = model.position; rootRot0 = model.rotation;
            var inv = Quaternion.Inverse(rootRot0);
            hipsOffsetModel = inv * (hips.position - root0);
            lFootModel = inv * (lFoot.position - root0); rFootModel = inv * (rFoot.position - root0);
            lFootRotModel = inv * lFoot.rotation; rFootRotModel = inv * rFoot.rotation;
            lToeModel = lToe ? inv * (lToe.position - root0) : lFootModel; rToeModel = rToe ? inv * (rToe.position - root0) : rFootModel;
            var fwdWorld = -pod.forward;                       // the model faces the capsule front
            fwdModel = inv * fwdWorld;
            if (head) { headFwdLocal = Quaternion.Inverse(head.rotation) * fwdWorld; headUpLocal = Quaternion.Inverse(head.rotation) * pod.up; }
            if (spine2) { chestFwdLocal = Quaternion.Inverse(spine2.rotation) * fwdWorld; chestUpLocal = Quaternion.Inverse(spine2.rotation) * pod.up; }
            legLength = Vector3.Distance(rUp.position, rLow.position) + Vector3.Distance(rLow.position, rFoot.position);
            armLength = rArm && rFore && rHand ? Vector3.Distance(rArm.position, rFore.position) + Vector3.Distance(rFore.position, rHand.position) : .6f;
            {
                var sr = StandRot; float toeMin = Mathf.Min(Mathf.Min((sr * lFootModel).y, (sr * rFootModel).y), Mathf.Min((sr * lToeModel).y, (sr * rToeModel).y));
                AnkleHeight = (sr * rFootModel).y - toeMin + soleOffset; ToeHeight = (sr * rToeModel).y - toeMin + soleOffset;
            }
            model.SetParent(transform, true);
            bindPos = new Vector3[all.Length]; bindRot = new Quaternion[all.Length];
            for (int i = 0; i < all.Length; i++) { bindPos[i] = all[i].localPosition; bindRot[i] = all[i].localRotation; }
            bound = true;
            Evaluate(0);
            return true;
        }

        Transform Bone(string name)
        {
            foreach (var t in all) { var n = t.name; int c = n.LastIndexOf(':'); if ((c >= 0 ? n.Substring(c + 1) : n) == name) return t; }
            return null;
        }

        Transform[] Fingers(string side)
        {
            string[] names = { "HandThumb1", "HandThumb2", "HandThumb3", "HandIndex1", "HandIndex2", "HandIndex3", "HandMiddle1", "HandMiddle2", "HandMiddle3", "HandRing1", "HandRing2", "HandRing3", "HandPinky1", "HandPinky2", "HandPinky3" };
            var r = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++) r[i] = Bone(side + names[i]);
            if (fingerAngles == null) fingerAngles = new[] { 12f, 18f, 14f, 48f, 58f, 38f, 50f, 60f, 40f, 50f, 58f, 38f, 46f, 54f, 36f };
            return r;
        }

        // ---- frames ----------------------------------------------------------------------------------------------
        Vector3 Facing { get { var f = Vector3.ProjectOnPlane(standPoint.forward, Vector3.up); return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward; } }
        Quaternion StandRot => Quaternion.LookRotation(-Facing, Vector3.up) * Quaternion.Inverse(pod.rotation) * rootRot0;
        Vector3 StandRoot
        {
            get
            {
                var rot = StandRot;
                float toe = Mathf.Min(Mathf.Min((rot * lFootModel).y, (rot * rFootModel).y), Mathf.Min((rot * lToeModel).y, (rot * rToeModel).y));
                var p = standPoint.position; p.y = standPoint.position.y - toe + soleOffset; return p;
            }
        }

        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));

        /// The capsule cradle (bed, footrest and restraints) tilts its top forward and slides out a little.
        /// Shared with CapsuleRestraints so the visible cradle and the robot lying on it move as one.
        public void CradleTransform(float t, out Quaternion rotation, out Vector3 pivot, out Vector3 offset)
        {
            float k = pod ? Smooth(timing.cradleStart, timing.cradleEnd, t) : 0;
            // a heavy mechanism: a small settle after the stop
            if (t > timing.cradleEnd) k += .03f * Mathf.Exp(-(t - timing.cradleEnd) * 5f) * Mathf.Sin((t - timing.cradleEnd) * 14f);
            rotation = pod ? Quaternion.AngleAxis(-cradleTilt * k, pod.right) : Quaternion.identity;
            pivot = pod ? pod.TransformPoint(cradlePivotLocal) : Vector3.zero;
            offset = pod && standPoint ? Facing * (cradlePush * k) : Vector3.zero;
        }

        static Vector3 Cradled(Vector3 p, Quaternion q, Vector3 pivot, Vector3 offset) => pivot + q * (p - pivot) + offset;

        /// Yaw of the body around the standing pivot: turn towards the conveyor and back to the scavengers.
        float BodyYaw(float t)
        {
            var T = timing;
            return beltTurnDegrees * (Smooth(T.turnStart, T.turnEnd, t) - Smooth(T.turnBackStart, T.turnBackEnd, t));
        }

        // feet re-plant one after the other while the body turns (right leads going, left leads coming back)
        float FootYaw(float t, bool right)
        {
            var T = timing; float goSpan = (T.turnEnd - T.turnStart) * .5f, backSpan = (T.turnBackEnd - T.turnBackStart) * .5f;
            float go = right ? Smooth(T.turnStart, T.turnStart + goSpan, t) : Smooth(T.turnStart + goSpan * .8f, T.turnEnd, t);
            float back = right ? Smooth(T.turnBackStart + backSpan * .8f, T.turnBackEnd, t) : Smooth(T.turnBackStart, T.turnBackStart + backSpan, t);
            return beltTurnDegrees * (go - back);
        }

        float FootLift(float t, bool right)
        {
            var T = timing; float goSpan = (T.turnEnd - T.turnStart) * .5f, backSpan = (T.turnBackEnd - T.turnBackStart) * .5f;
            float a = right ? Bump01(t, T.turnStart, T.turnStart + goSpan) : Bump01(t, T.turnStart + goSpan * .8f, T.turnEnd);
            float b = right ? Bump01(t, T.turnBackStart + backSpan * .8f, T.turnBackEnd) : Bump01(t, T.turnBackStart, T.turnBackStart + backSpan);
            return Mathf.Max(a, b);
        }

        /// Arm windows (cinematic seconds) in which each foot lands; FootContactFx fires on the real contact inside them.
        public void StepWindows(bool right, out float exitArm, out float exitDisarm, out float goArm, out float goDisarm, out float backArm, out float backDisarm)
        {
            var T = timing; float goSpan = (T.turnEnd - T.turnStart) * .5f, backSpan = (T.turnBackEnd - T.turnBackStart) * .5f;
            exitArm = (right ? T.rightStepStart : T.leftStepStart) + .2f; exitDisarm = (right ? T.rightStepEnd : T.leftStepEnd) + .6f;
            goArm = right ? T.turnStart : T.turnStart + goSpan * .8f; goDisarm = (right ? T.turnStart + goSpan : T.turnEnd) + .4f;
            backArm = right ? T.turnBackStart + backSpan * .8f : T.turnBackStart; backDisarm = (right ? T.turnBackEnd : T.turnBackStart + backSpan) + .4f;
        }

        static float Bump01(float t, float a, float b) { float k = Mathf.InverseLerp(a, b, t); return k <= 0 || k >= 1 ? 0 : Mathf.Sin(k * Mathf.PI); }

        /// Rebuilds the full pose for cinematic time t (seconds).
        public void Evaluate(float t)
        {
            if (!bound) return;
            for (int i = 0; i < all.Length; i++) { all[i].localPosition = bindPos[i]; all[i].localRotation = bindRot[i]; }
            var T = timing; var up = Vector3.up; var face = Facing; var right = Vector3.Cross(up, face);
            var standRot0 = StandRot; var standRoot0 = StandRoot;
            var hips0 = root0 + rootRot0 * hipsOffsetModel;
            var pivot = standRoot0 + standRot0 * hipsOffsetModel; pivot.y = standRoot0.y;
            float yaw = BodyYaw(t); var yawRot = Quaternion.AngleAxis(yaw, up);
            var standRot = yawRot * standRot0;
            var hipsStand = pivot + yawRot * (standRoot0 + standRot0 * hipsOffsetModel - pivot);
            var hipsStandStill = standRoot0 + standRot0 * hipsOffsetModel;
            Quaternion Lean(float deg, Quaternion r) => Quaternion.AngleAxis(deg, right) * r;

            CradleTransform(t, out var cq, out var cPivot, out var cOff);
            CradleTransform(T.cradleEnd + 2f, out var eq, out var ePivot, out var eOff);   // settled end of the tilt

            // body path: capsule (punches, cradle tilt) -> weight over the left foot -> right foot out -> weight on it ->
            // left foot out -> crouched on both feet -> upright (+ settle, turn)
            Vector3 hp; Quaternion hr;
            float punchLean = 5 * Punch(t, T.punch1) + 8 * Punch(t, T.punch2);
            if (t <= T.cradleEnd)
            {
                hp = Cradled(hips0, cq, cPivot, cOff) + face * (.03f * punchLean / 8f);
                hr = Quaternion.AngleAxis(punchLean, right) * cq * rootRot0;
            }
            else if (t <= T.stand)
            {
                var h0 = Cradled(hips0, eq, ePivot, eOff);
                var h1 = h0 + face * .10f - right * .05f - up * .02f;
                var h2 = Vector3.Lerp(h1, hipsStandStill, .45f) - up * .05f;
                var h3 = Vector3.Lerp(h1, hipsStandStill, .72f) - up * .15f + right * .04f;
                var h4 = hipsStandStill - up * .20f;
                var r0 = eq * rootRot0; var r1 = Quaternion.Slerp(r0, Lean(10, standRot0), .55f);
                var r2 = Lean(16, standRot0); var r3 = Lean(13, standRot0); var r4 = Lean(18, standRot0);
                float k0 = T.cradleEnd, k1 = T.rightStepStart, k2 = T.rightStepEnd, k3 = T.transferEnd, k4 = T.leftStepEnd, k5 = T.stand;
                if (t < k1) { float u = Mathf.InverseLerp(k0, k1, t); hp = Spline(h0, h0, h1, h2, u); hr = Quaternion.Slerp(r0, r1, Mathf.SmoothStep(0, 1, u)); }
                else if (t < k2) { float u = Mathf.InverseLerp(k1, k2, t); hp = Spline(h0, h1, h2, h3, u); hr = Quaternion.Slerp(r1, r2, Mathf.SmoothStep(0, 1, u)); }
                else if (t < k3) { float u = Mathf.InverseLerp(k2, k3, t); hp = Spline(h1, h2, h3, h4, u); hr = Quaternion.Slerp(r2, r3, Mathf.SmoothStep(0, 1, u)); }
                else if (t < k4) { float u = Mathf.InverseLerp(k3, k4, t); hp = Spline(h2, h3, h4, hipsStandStill, u); hr = Quaternion.Slerp(r3, r4, Mathf.SmoothStep(0, 1, u)); }
                else { float u = Smooth(k4, k5, t); u = u * u * (3 - 2 * u); hp = Vector3.Lerp(h4, hipsStandStill, u); hr = Quaternion.Slerp(r4, standRot0, u); }
            }
            else
            {
                float s = Mathf.Exp(-(t - T.stand) * 2f) * Mathf.Sin((t - T.stand) * 4f);   // heavy settle on the knees
                // weight shifts during the turn: hips sink a little at each re-plant
                float sink = .025f * (FootLift(t, true) + FootLift(t, false));
                hp = hipsStand - up * (.03f * s + sink); hr = standRot;
            }
            hp -= up * (.02f * Bump(t, T.rightStepEnd + .12f, .45f) + .05f * Bump(t, T.leftStepEnd + .12f, .5f));   // weight lands on each step
            model.rotation = hr;
            model.position = hp - hr * hipsOffsetModel;
            var bodyFwd = hr * fwdModel; bodyFwd = Vector3.ProjectOnPlane(bodyFwd, up).sqrMagnitude > .01f ? bodyFwd : face;
            var bodyFace = yawRot * face; var bodyRight = Vector3.Cross(up, bodyFace);

            // ---- foot targets: carried on the cradle footrest, then planted inside, an arc over the lip, the floor
            var lInner = Cradled(root0 + rootRot0 * lFootModel, eq, ePivot, eOff); var rInner = Cradled(root0 + rootRot0 * rFootModel, eq, ePivot, eOff);
            var lInnerRot = eq * rootRot0 * lFootRotModel; var rInnerRot = eq * rootRot0 * rFootRotModel;
            var lPlant0 = standRoot0 + standRot0 * lFootModel; var rPlant0 = standRoot0 + standRot0 * rFootModel;
            float ry = FootYaw(t, true), ly = FootYaw(t, false);
            var rPlant = pivot + Quaternion.AngleAxis(ry, up) * (rPlant0 - pivot) + up * (.12f * FootLift(t, true));
            var lPlant = pivot + Quaternion.AngleAxis(ly, up) * (lPlant0 - pivot) + up * (.12f * FootLift(t, false));
            var rPlantRot = Quaternion.AngleAxis(ry, up) * standRot0 * rFootRotModel; var lPlantRot = Quaternion.AngleAxis(ly, up) * standRot0 * lFootRotModel;
            RightFootPlanted = rPlant0; LeftFootPlanted = lPlant0;
            bool feetIk = t > T.cradleEnd;
            Vector3 rTarget = rPlant, lTarget = lPlant; Quaternion rRot = rPlantRot, lRot = lPlantRot;
            if (feetIk)
            {
                FootTarget(t, T.rightStepStart, T.rightStepEnd, rInner, rInnerRot, rPlant, rPlantRot, up, face, right, out rTarget, out rRot);
                FootTarget(t, T.leftStepStart, T.leftStepEnd, lInner, lInnerRot, lPlant, lPlantRot, up, face, right, out lTarget, out lRot);
                // never over-extend a leg: lower the hips until both planted feet are within reach
                float drop = Mathf.Max(Reach(rUp.position, rTarget, up), Reach(lUp.position, lTarget, up));
                if (drop > 0) model.position -= up * drop;
            }

            // spine curl while leaving and crouching
            float bend = 10 * Bump(t, (T.cradleEnd + T.leftStepEnd) * .5f, 4.4f);
            var bendRot = Quaternion.AngleAxis(bend / 3f, right);
            if (spine) spine.rotation = bendRot * spine.rotation;
            if (spine1) spine1.rotation = bendRot * spine1.rotation;
            if (spine2) spine2.rotation = bendRot * spine2.rotation;

            // arms: from the capsule's bind (A) pose to hanging relaxed once the robot starts to leave
            float relax = Smooth(T.leave - .2f, T.leave + 2.0f, t);
            if (relax > 0)
            {
                Relax(lArm, lFore, lHand, -bodyRight, bodyFace, up, relax);
                Relax(rArm, rFore, rHand, bodyRight, bodyFace, up, relax);
            }

            // head and torso: look at the glass, the step, the hand, the conveyor, then the scavengers
            Aim(t, up, face, right);

            // feet
            if (feetIk)
            {
                // a foot still standing inside the capsule keeps its knee high, clear of the capsule lip
                Leg(rUp, rLow, rFoot, rTarget, rRot, bodyFwd, bodyRight, t < T.rightStepStart + .3f ? .35f : 0);
                Leg(lUp, lLow, lFoot, lTarget, lRot, bodyFwd, -bodyRight, t < T.leftStepStart + .3f ? .35f : 0);
            }

            // left hand on the capsule rail while the weight comes off it (lets go if the body moved out of reach)
            if (railGrip && lArm && lFore && lHand)
            {
                float w = Smooth(T.railIn, T.railIn + .6f, t) * (1 - Smooth(T.railOut, T.railOut + .7f, t));
                float reach = Vector3.Distance(lArm.position, railGrip.position);
                w *= Mathf.Clamp01(Mathf.InverseLerp(armLength * 1.1f, armLength * .97f, reach));
                if (w > 0) TwoBone(lArm, lFore, lHand, Vector3.Lerp(lHand.position, railGrip.position, w), (lArm.position + railGrip.position) * .5f - bodyFwd * .45f - bodyRight * .35f - up * .2f, armLength);
            }
            // right arm: two punches on the glass (already driving at the cut), then the hand held up to the visor
            if (rArm && rFore && rHand && spine2)
            {
                var chest = spine2.position;
                Vector3 target = rHand.position; float w = 0;
                if (glassImpact)
                {
                    var wound = chest + bodyFwd * .1f + bodyRight * .22f - up * .02f;
                    var hit = glassImpact.position; var start = rHand.position;
                    float d1 = T.punch1 - .65f, d2 = T.punch2 - .35f;
                    if (t >= d1 && t < T.punch1) { float k = Mathf.InverseLerp(d1, T.punch1, t); target = Vector3.Lerp(start, hit, k * k); w = 1; }
                    else if (t >= T.punch1 && t < d2) { target = Vector3.Lerp(hit, wound, Smooth(T.punch1, T.punch1 + .6f, t)); w = 1; }
                    else if (t >= d2 && t < T.punch2) { float k = Mathf.InverseLerp(d2, T.punch2, t); target = Vector3.Lerp(wound, hit, k * k); w = 1; }
                    else if (t >= T.punch2) { target = Vector3.Lerp(hit, wound, Smooth(T.punch2, T.punch2 + .7f, t)); w = 1 - Smooth(T.releaseAt + .6f, T.leave + .2f, t); }
                }
                float handW = Smooth(T.handStart, T.handStart + 1.1f, t) * (1 - Smooth(T.handEnd - .8f, T.handEnd + .2f, t));
                if (handW > 0 && head)
                {
                    var study = head.position + bodyFwd * .42f - up * .16f + bodyRight * .12f + up * (.02f * Mathf.Sin(t * .9f));
                    target = Vector3.Lerp(target, study, handW); w = Mathf.Max(w, handW);
                }
                if (w > 0) TwoBone(rArm, rFore, rHand, Vector3.Lerp(rHand.position, target, w), (rArm.position + target) * .5f + bodyRight * .35f - up * .4f - bodyFwd * .1f, armLength);
                // palm and back: only the hand bone rolls about the forearm axis (the hand is a separate rigid piece)
                if (handW > 0 && rFore)
                {
                    float u = Mathf.Clamp01(Mathf.InverseLerp(T.handStart + .9f, T.handEnd - .9f, t));
                    float roll = WristRoll(u) * maxWristRoll * handW;
                    var axis = (rHand.position - rFore.position).normalized;
                    rHand.rotation = Quaternion.AngleAxis(roll, axis) * rHand.rotation;
                }
                Curiosity = handW;
            }

            // fingers: twitch, fist for the punches, open/close while studying; the left hand closes on the rail
            float curl = Curl(t);
            float grip = Smooth(T.railIn + .2f, T.railIn + .5f, t) * (1 - Smooth(T.railOut, T.railOut + .4f, t));
            CurlHand(rFingers, rHand, curl, true);
            CurlHand(lFingers, lHand, Mathf.Max(Mathf.Min(curl, .5f) * .7f, grip * .85f), false);
        }

        /// Foot target for time t: planted inside before its step, an arc over the capsule lip during it, the floor after.
        void FootTarget(float t, float stepStart, float stepEnd, Vector3 inner, Quaternion innerRot, Vector3 plant, Quaternion plantRot, Vector3 up, Vector3 face, Vector3 right, out Vector3 target, out Quaternion rot)
        {
            if (t <= stepStart) { target = inner; rot = innerRot; return; }
            if (t >= stepEnd) { target = plant; rot = plantRot; return; }
            float u = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(stepStart, stepEnd, t));
            // lip edge under this foot (pod-local x of the foot), in (along-face, lateral, height) coordinates
            var local = pod.InverseTransformPoint(inner);
            var edge = pod.TransformPoint(new Vector3(local.x, lipLocalY, lipLocalZ));
            float sEdge = Vector3.Dot(edge, face), sInner = Vector3.Dot(inner, face), sPlant = Vector3.Dot(plant, face);
            float rInner = Vector3.Dot(inner, right), rPlant = Vector3.Dot(plant, right);
            float clearY = edge.y + AnkleHeight + lipClearance;
            Vector3 P(float s, float r, float y) => face * s + right * r + up * y;
            path[0] = inner;
            path[1] = P(sInner + .05f, Mathf.Lerp(rInner, rPlant, .1f), Mathf.Max(inner.y + .12f, clearY - .06f));
            path[2] = P(Mathf.Max(sEdge - .04f, sInner + .12f), Mathf.Lerp(rInner, rPlant, .35f), clearY);
            path[3] = P(Mathf.Min(sEdge + .24f, Mathf.Lerp(sEdge, sPlant, .6f)), Mathf.Lerp(rInner, rPlant, .7f), clearY - .02f);
            path[4] = plant;
            target = Polyline(path, u);
            rot = Quaternion.Slerp(innerRot, plantRot, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .85f, u)));
        }

        // Catmull-Rom through the 5 path points, parametrised by chord length
        static Vector3 Polyline(Vector3[] p, float u)
        {
            float l0 = Vector3.Distance(p[0], p[1]), l1 = Vector3.Distance(p[1], p[2]), l2 = Vector3.Distance(p[2], p[3]), l3 = Vector3.Distance(p[3], p[4]);
            float total = Mathf.Max(1e-4f, l0 + l1 + l2 + l3), d = u * total;
            if (d <= l0) return Spline(p[0], p[0], p[1], p[2], l0 > 1e-5f ? d / l0 : 1);
            d -= l0; if (d <= l1) return Spline(p[0], p[1], p[2], p[3], l1 > 1e-5f ? d / l1 : 1);
            d -= l1; if (d <= l2) return Spline(p[1], p[2], p[3], p[4], l2 > 1e-5f ? d / l2 : 1);
            d -= l2; return Spline(p[2], p[3], p[4], p[4], l3 > 1e-5f ? Mathf.Clamp01(d / l3) : 1);
        }

        static Vector3 Spline(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return .5f * (2 * b + (c - a) * u + (2 * a - 5 * b + 4 * c - d) * u2 + (3 * b - a - 3 * c + d) * u3);
        }

        /// How much the hips must drop (along up) so the leg reaches the target at 97 % of its length.
        float Reach(Vector3 hipJoint, Vector3 target, Vector3 up)
        {
            float L = legLength * .97f; var v = hipJoint - target;
            if (v.sqrMagnitude <= L * L) return 0;
            float b = Vector3.Dot(v, up), disc = b * b - v.sqrMagnitude + L * L;
            return disc >= 0 ? Mathf.Max(0, b - Mathf.Sqrt(disc)) : Mathf.Max(0, b);
        }

        void Aim(float t, Vector3 up, Vector3 face, Vector3 right)
        {
            if (!head) return;
            var T = timing;
            // looks at the glass between the blows, then down at its feet while stepping out
            if (glassImpact) AimChain(glassImpact.position, .6f * Smooth(T.punch1 - 1.0f, T.punch1 - .4f, t) * (1 - Smooth(T.punch2 + .4f, T.cradleStart, t)), 50);
            float nod = 20 * Bump(t, (T.rightStepStart + T.leftStepEnd) * .5f, 3.4f);
            if (nod > 0)
            {
                if (neck) neck.rotation = Quaternion.AngleAxis(nod * .3f, right) * neck.rotation;
                head.rotation = Quaternion.AngleAxis(nod * .7f, right) * head.rotation;
            }
            // hand study: look at it with a curious head tilt
            float handW = Smooth(T.handStart + .2f, T.handStart + 1.2f, t) * (1 - Smooth(T.handEnd - .6f, T.handEnd + .1f, t));
            if (handW > 0 && rHand)
            {
                AimChain(rHand.position, .85f * handW, 70);
                var fwd = head.rotation * headFwdLocal;
                head.rotation = Quaternion.AngleAxis(13f * handW * Mathf.Sin((t - T.handStart) * .75f), fwd) * head.rotation;
            }
            // conveyor: head leads, then the chest, while the body turns on re-planted feet
            float beltW = Smooth(T.beltLook, T.beltLook + .9f, t) * (1 - Smooth(T.turnBackStart - .2f, T.turnBackStart + .7f, t));
            if (beltW > 0 && beltTarget)
            {
                if (spine2) AimBone(spine2, chestFwdLocal, beltTarget.position, .35f * Smooth(T.beltLook + .3f, T.turnStart + .4f, t) * beltW, 35);
                AimChain(beltTarget.position, .9f * beltW, 75);
            }
            // the scavengers: chest and head settle on them as the body turns back
            float scavW = Smooth(T.turnBackStart, T.turnBackStart + 1.2f, t);
            if (scavW > 0 && lookTarget)
            {
                if (spine2) AimBone(spine2, chestFwdLocal, lookTarget.position, .25f * scavW, 25);
                AimChain(lookTarget.position, .8f * scavW, 60);
            }
            ClampNeck();
        }

        /// Head aim spread over the chain: Spine2 10 %, Neck 20 %, Head 70 % (rotations only, never scaled).
        void AimChain(Vector3 target, float w, float maxDegrees)
        {
            if (w <= 0 || !head) return;
            var cur = head.rotation * headFwdLocal; var want = target - head.position;
            if (want.sqrMagnitude < 1e-4f) return;
            var limited = Vector3.RotateTowards(cur, want.normalized, maxDegrees * Mathf.Deg2Rad, 0);
            var full = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, limited), w);
            if (spine2) spine2.rotation = Quaternion.Slerp(Quaternion.identity, full, .1f) * spine2.rotation;
            if (neck) neck.rotation = Quaternion.Slerp(Quaternion.identity, full, .2f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, full, neck ? .7f : .9f) * head.rotation;
        }

        /// Keeps the head inside believable ranges relative to the chest: yaw, pitch (down/up) and roll.
        void ClampNeck()
        {
            if (!head || !spine2) return;
            var f = spine2.rotation * chestFwdLocal; var u = Vector3.ProjectOnPlane(spine2.rotation * chestUpLocal, f);
            if (u.sqrMagnitude < 1e-4f) return; u.Normalize();
            var r = Vector3.Cross(u, f);
            var h = head.rotation * headFwdLocal;
            float x = Vector3.Dot(h, r), y = Vector3.Dot(h, u), z = Vector3.Dot(h, f);
            float yaw = Mathf.Atan2(x, z) * Mathf.Rad2Deg, pitch = Mathf.Atan2(y, Mathf.Sqrt(x * x + z * z)) * Mathf.Rad2Deg;
            float cy = Mathf.Clamp(yaw, -neckYaw, neckYaw), cp = Mathf.Clamp(pitch, -neckPitchDown, neckPitchUp);
            if (Mathf.Abs(cy - yaw) > .01f || Mathf.Abs(cp - pitch) > .01f)
            {
                float ry = cy * Mathf.Deg2Rad, rp = cp * Mathf.Deg2Rad;
                var dir = r * (Mathf.Sin(ry) * Mathf.Cos(rp)) + u * Mathf.Sin(rp) + f * (Mathf.Cos(ry) * Mathf.Cos(rp));
                head.rotation = Quaternion.FromToRotation(h, dir) * head.rotation; h = dir;
            }
            // roll: the head's up against the chest's up, both seen along the head's forward axis
            var hu = Vector3.ProjectOnPlane(head.rotation * headUpLocal, h); var cu = Vector3.ProjectOnPlane(u, h);
            if (hu.sqrMagnitude < 1e-4f || cu.sqrMagnitude < 1e-4f) return;
            float roll = Vector3.SignedAngle(cu, hu, h);
            if (Mathf.Abs(roll) > neckRoll) head.rotation = Quaternion.AngleAxis(Mathf.Sign(roll) * neckRoll - roll, h) * head.rotation;
        }

        /// Rotates a bone so its forward axis turns towards a world point, limited to maxDegrees and blended by w.
        static void AimBone(Transform bone, Vector3 fwdLocal, Vector3 target, float w, float maxDegrees)
        {
            if (w <= 0 || !bone) return;
            var cur = bone.rotation * fwdLocal; var want = target - bone.position;
            if (want.sqrMagnitude < 1e-4f) return;
            var limited = Vector3.RotateTowards(cur, want.normalized, maxDegrees * Mathf.Deg2Rad, 0);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, limited), w) * bone.rotation;
        }

        // wrist roll profile over the hand study (u 0..1): -1 palm up, hold, +1 back of the hand, hold, ease to neutral
        static float WristRoll(float u)
        {
            if (u < .15f) return -Mathf.SmoothStep(0, 1, u / .15f);
            if (u < .35f) return -1;
            if (u < .55f) return Mathf.Lerp(-1, 1, Mathf.SmoothStep(0, 1, (u - .35f) / .2f));
            if (u < .8f) return 1;
            return 1 - Mathf.SmoothStep(0, 1, (u - .8f) / .2f);
        }

        static float Punch(float t, float impact) => Bump(t, impact + .08f, .8f);

        float Curl(float t)
        {
            var T = timing;
            if (t < T.fingersStart) return 0;
            float u = t - T.fingersStart, span = Mathf.Max(.5f, T.fingersEnd - T.fingersStart);
            float twitch = u < .35f ? Mathf.Sin(u / .35f * Mathf.PI) * .35f : 0;
            float fist = Smooth(.55f, .95f, u) * (1 - .6f * Smooth(span - .5f, span, u));
            float c = Mathf.Max(twitch, fist);
            // tight fist for the punches (closed before the cut to the glass)
            c = Mathf.Max(c, Smooth(T.punch1 - 1.3f, T.punch1 - .9f, t) * (1 - Smooth(T.punch2 + .5f, T.leave, t)));
            // studying the hand: two slow open/close cycles
            float h = Smooth(T.handStart + 1.0f, T.handStart + 1.5f, t) * (1 - Smooth(T.handEnd - .8f, T.handEnd - .2f, t));
            if (h > 0) c = Mathf.Lerp(c * .3f, .5f - .5f * Mathf.Cos((t - T.handStart - 1.0f) * 1.6f), h);
            return Mathf.Clamp01(c);
        }

        static float Bump(float t, float centre, float width)
        {
            float x = (t - centre) / (width * .5f);
            return Mathf.Abs(x) >= 1 ? 0 : .5f + .5f * Mathf.Cos(x * Mathf.PI);
        }

        static void Relax(Transform arm, Transform fore, Transform hand, Vector3 side, Vector3 face, Vector3 up, float w)
        {
            if (!arm || !fore || !hand) return;
            var upperDir = fore.position - arm.position;
            var wantUpper = (-up * .92f + side * .28f + face * .06f).normalized;
            arm.rotation = Quaternion.FromToRotation(upperDir, Vector3.Slerp(upperDir.normalized, wantUpper, w)) * arm.rotation;
            var lowerDir = hand.position - fore.position;
            var wantLower = (-up * .85f + face * .42f + side * .12f).normalized;
            fore.rotation = Quaternion.FromToRotation(lowerDir, Vector3.Slerp(lowerDir.normalized, wantLower, w)) * fore.rotation;
        }

        void Leg(Transform up, Transform low, Transform foot, Vector3 target, Quaternion footRot, Vector3 bodyFwd, Vector3 side, float poleUp)
        {
            // pole in front of the knee, following the body: knees never bend backwards
            var hip = up.position;
            var pole = (hip + target) * .5f + bodyFwd * (legLength * .6f) + Vector3.up * (legLength * poleUp) + side * .06f;
            TwoBone(up, low, foot, target, pole, legLength);
            foot.rotation = footRot;
        }

        /// Analytic two-bone IK: the middle joint is placed in the plane of the pole and the target, on the pole's side,
        /// so knees/elbows can never bend the wrong way; rotations only (no bone is moved or scaled).
        static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float chain)
        {
            Vector3 aP = a.position, bP = b.position, cP = c.position;
            float l1 = Vector3.Distance(aP, bP), l2 = Vector3.Distance(bP, cP);
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            var toT = target - aP; float d = toT.magnitude; if (d < 1e-4f) return;
            var n = toT / d;
            d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 1e-3f, (l1 + l2) * .995f);
            var p = pole - aP; p -= n * Vector3.Dot(p, n);
            if (p.sqrMagnitude < 1e-6f) { p = (bP - aP) - n * Vector3.Dot(bP - aP, n); if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(n, Vector3.right); }
            p.Normalize();
            float x = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1, 1);
            var knee = aP + n * (l1 * x) + p * (l1 * Mathf.Sqrt(1 - x * x));
            a.rotation = Quaternion.FromToRotation(bP - aP, knee - aP) * a.rotation;
            bP = b.position; cP = c.position;
            b.rotation = Quaternion.FromToRotation(cP - bP, aP + n * d - bP) * b.rotation;
        }

        void CurlHand(Transform[] fingers, Transform hand, float amount, bool rightHand)
        {
            if (amount <= .001f || !hand || fingers[6] == null || fingers[3] == null || fingers[12] == null) return;
            var f = (fingers[6].position - hand.position).normalized;
            var s = (fingers[3].position - fingers[12].position).normalized;
            var palm = rightHand ? Vector3.Cross(f, s) : Vector3.Cross(s, f);
            var axis = Vector3.Cross(f, palm); if (axis.sqrMagnitude < 1e-6f) return; axis.Normalize();
            for (int i = 0; i < fingers.Length; i++)
            {
                var b = fingers[i]; if (!b) continue;
                b.rotation = Quaternion.AngleAxis(fingerAngles[i] * amount, i < 3 ? (axis + palm * .3f).normalized : axis) * b.rotation;
            }
        }
    }
}
