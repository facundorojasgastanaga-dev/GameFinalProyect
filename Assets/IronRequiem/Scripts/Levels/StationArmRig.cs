using UnityEngine;

namespace IronRequiem
{
    /// v5 montage station: one mechanical arm (base yaw + shoulder + elbow + wrist gripper) posed by an analytic
    /// two-bone solve towards a world target. Rigid segments only; driven every frame by WeaponMountStation, which owns
    /// the timing (pure function of the sequence time). Zero GC.
    /// v7 (18/11): the old solve rotated the upper arm by -a about Cross(dir, up), which DROPPED the elbow (the
    /// opposite of its comment) and threw it outward to |x| ~2.3, into the posts and the Map01 side walls. The elbow
    /// is now chosen on the solve circle: the bend hint (station frame = parent; up, toward the station centre and
    /// its back) when it fits, otherwise the nearest angle that keeps the elbow inside the station's own volume
    /// (|x| <= elbowMaxSide, between the gate plane and the back clearance, under the arm beam). The limit is found by
    /// bisection, so the elbow moves continuously with the target. A cheap capsule check per beam (non-trigger,
    /// non-station, non-player colliders) turns the elbow further round the circle, eased, if something is still in
    /// the way. The gripper keeps a caller-provided up, so the jaws straddle what they hold instead of taking an
    /// arbitrary roll when the approach is vertical.
    /// 19/11 segmented art: optional hinge-aligned segments (the bend axis is the segment's local X) and a grip length
    /// (the solve reaches the wrist, the gripper sits that far ahead along the approach). Both default off.
    /// 19/11 refinement (model wrist, default off): the claw keeps its head on the tool tip as in the model (wristLocal),
    /// the elbow is pulled toward where the model puts it for that claw pose (elbowLocal), and the claw rolls about its
    /// own axis to face the tool. Without it the claw took the caller's roll and bent up to 145 deg against the tool,
    /// so it read as detached from the arm.
    public sealed class StationArmRig : MonoBehaviour
    {
        [SerializeField] Transform yaw, upper, fore, gripper;
        [SerializeField, Min(.1f)] float upperLength = 1.3f, foreLength = 1.2f;
        [SerializeField] Transform jawA, jawB;
        [SerializeField] float jawOpen = .07f, jawClosed = .025f;
        [Header("v7 elbow: bend hint and limits in the station frame (the parent)")]
        [Tooltip("Preferred bend, mirrored per side: x toward the station centre, y up, z toward the back of the station.")]
        [SerializeField] Vector3 elbowHint = new Vector3(.3f, .6f, 1f);
        [Tooltip("Largest |local x| of the elbow centre (posts at 2.3, Map01 side walls at 2.0, elbow half size .09).")]
        [SerializeField, Min(.2f)] float elbowMaxSide = 1.7f;
        [Tooltip("Local z range of the elbow centre: just behind the gate plane .. clearance from a back wall at 0.6.")]
        [SerializeField] Vector2 elbowDepth = new Vector2(.02f, .42f);
        [Tooltip("Highest local y of the elbow centre (the arm beam underside is at 3.31).")]
        [SerializeField] float elbowMaxHeight = 3.08f;
        [Tooltip("Colliders the beams keep clear of (triggers, the station itself and the player are ignored). Nothing = no check.")]
        [SerializeField] LayerMask obstacles = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0)] float beamRadius = .085f;
        [Header("Segmented art (19/11): hinge-aligned segments and a gripper ahead of the wrist (both off = v7)")]
        [Tooltip("Turn the upper and fore segments about their bend axis (local X = hinge axis) instead of world up, so hinge "
            + "visuals (shoulder disc, elbow cylinder) stay on their axis and never roll when a segment points straight down.")]
        [SerializeField] bool hingeAligned;
        [Tooltip("Distance from the wrist (end of the fore segment) to the grip point, along the approach. The solve "
            + "reaches the wrist; the gripper sits this far ahead of it, so the jaws (not the wrist) land on the target.")]
        [SerializeField, Min(0)] float gripLength;
        [Header("Model wrist (19/11 refinement, off = the 19/11 behaviour above)")]
        [Tooltip("The claw head stays on the tool tip as in the model; the elbow and the claw roll follow the model's own tool direction.")]
        [SerializeField] bool modelWrist;
        [Tooltip("Tool tip (wrist) and elbow relative to the grip point, in the gripper frame, as in the model (metres).")]
        [SerializeField] Vector3 wristLocal, elbowLocal;
        [Tooltip("How far the preferred elbow turns from elbowHint toward the model's elbow for the current claw pose (0..1).")]
        [SerializeField, Range(0, 1)] float modelElbowWeight = .8f;
        [Tooltip("How much the claw rolls about its axis to face the tool (0 = the caller's roll, 1 = the model's relation).")]
        [SerializeField, Range(0, 1)] float rollToTool = 1f;
        [Tooltip("Tong jaws: each jaw turns about its pivot (the claw head, where the jaw transform sits) by this many degrees "
            + "open / closed instead of sliding sideways. 0 = slide (jawOpen / jawClosed).")]
        [SerializeField, Min(0)] float jawHingeOpen, jawHingeClosed;

        const float SolveStep = 7.5f * Mathf.Deg2Rad, AvoidStep = 15f * Mathf.Deg2Rad;
        static readonly Collider[] Hits = new Collider[8];
        Vector3 restTarget; bool rested; Transform frame;
        float lastAngle, avoidAngle; int avoidFrame = -1;
        Vector3 lastBend = Vector3.right;
        public Transform Gripper => gripper;
        public float Reach => upperLength + foreLength;
        /// World position of the elbow joint as last posed.
        public Vector3 Elbow => fore ? fore.position : transform.position;
        public void Configure(Transform yawPivot, Transform upperArm, Transform foreArm, Transform grip, float upperLen, float foreLen, Transform a, Transform b)
        { yaw = yawPivot; upper = upperArm; fore = foreArm; gripper = grip; upperLength = upperLen; foreLength = foreLen; jawA = a; jawB = b; }
        void Awake()
        {
            if (gripper) { restTarget = gripper.position; rested = true; }
            frame = transform.parent ? transform.parent : transform;
        }
        public Vector3 RestTarget => rested ? restTarget : gripper.position;

        /// Points the gripper at target (world), its jaws facing along approach; grip 0 = open, 1 = closed.
        public void Pose(Vector3 target, Vector3 approach, float grip) => Pose(target, approach, grip, Vector3.up);
        /// Clears the per-frame solver memory (elbow side preference and wall-avoidance offset) before a rest pose.
        public void ResetSolver() { lastAngle = 0; avoidAngle = 0; avoidFrame = -1; }

        /// Same, with the gripper's up (the jaws open across it): the caller passes the up of what it holds.
        public void Pose(Vector3 target, Vector3 approach, float grip, Vector3 gripperUp)
        {
            if (!yaw || !upper || !fore || !gripper) return;
            if (!frame) frame = transform.parent ? transform.parent : transform;
            var aim = approach.sqrMagnitude > 1e-6f ? approach.normalized : Vector3.zero;
            if (modelWrist && aim != Vector3.zero && wristLocal != Vector3.zero)
            {
                // the claw pose drives the wrist: its head on the tool tip, the elbow toward the model's, then the claw
                // rolls about its own axis toward the tool actually reached (two passes: the roll moves the wrist a little)
                var rot = Quaternion.LookRotation(aim, GripperUp(aim, gripperUp));
                Vector3 end = target, elbow = target;
                for (int pass = 0; pass < 2; pass++)
                {
                    Solve(target + rot * wristLocal, target + rot * elbowLocal, out elbow, out end);
                    if (rollToTool <= 0) break;
                    var have = Vector3.ProjectOnPlane(elbow - end, aim); var want = Vector3.ProjectOnPlane(rot * (elbowLocal - wristLocal), aim);
                    // no roll is defined while the tool lies along the claw axis: fade the correction out there
                    float defined = Mathf.Min(have.magnitude / Mathf.Max(1e-4f, (elbow - end).magnitude), want.magnitude / Mathf.Max(1e-4f, (elbowLocal - wristLocal).magnitude));
                    float w = rollToTool * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .35f, defined));
                    if (w <= 0) break;
                    rot = Quaternion.AngleAxis(Vector3.SignedAngle(want, have, aim) * w, aim) * rot;
                }
                gripper.rotation = rot; gripper.position = end - rot * wristLocal;
            }
            else
            {
                var wrist = gripLength > 0 && aim != Vector3.zero ? target - aim * gripLength : target;   // solve for the wrist
                Solve(wrist, null, out _, out var end);
                var look = aim != Vector3.zero ? aim : (end - fore.position).normalized;
                gripper.position = end + look * gripLength;
                gripper.rotation = Quaternion.LookRotation(look, GripperUp(look, gripperUp));
            }
            if (jawHingeOpen > 0)
            {   // tongs: the halves turn about the pin at the claw head (local Y), so the claw never comes apart
                float a = Mathf.Lerp(jawHingeOpen, jawHingeClosed, Mathf.Clamp01(grip));
                if (jawA) jawA.localRotation = Quaternion.AngleAxis(-a, Vector3.up);
                if (jawB) jawB.localRotation = Quaternion.AngleAxis(a, Vector3.up);
            }
            else
            {
                float jaw = Mathf.Lerp(jawOpen, jawClosed, Mathf.Clamp01(grip));
                if (jawA) jawA.localPosition = new Vector3(-jaw, 0, jawA.localPosition.z);
                if (jawB) jawB.localPosition = new Vector3(jaw, 0, jawB.localPosition.z);
            }
        }

        // two-bone solve of the shoulder-elbow-wrist chain toward `target` (the wrist). The elbow takes the bend hint,
        // turned toward `preferElbow` (world) by modelElbowWeight when given, within the station limits.
        void Solve(Vector3 target, Vector3? preferElbow, out Vector3 elbow, out Vector3 end)
        {
            var flat = Vector3.ProjectOnPlane(target - yaw.position, Vector3.up);
            if (flat.sqrMagnitude > 1e-6f) yaw.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            var shoulder = upper.position; var toTarget = target - shoulder; float raw = toTarget.magnitude;
            float d = Mathf.Clamp(raw, Mathf.Abs(upperLength - foreLength) + .01f, upperLength + foreLength - .001f);
            var dir = raw > 1e-5f ? toTarget / raw : -frame.up;
            float cosA = Mathf.Clamp((upperLength * upperLength + d * d - foreLength * foreLength) / (2 * upperLength * d), -1, 1);
            // every elbow that keeps both segment lengths lies on this circle (centre on the shoulder-target line)
            var circle = new Circle { centre = shoulder + dir * (upperLength * cosA), radius = upperLength * Mathf.Sqrt(Mathf.Max(0, 1 - cosA * cosA)) };
            float inward = frame.InverseTransformPoint(shoulder).x > 0 ? -1 : 1;
            var prefer = frame.TransformDirection(new Vector3(elbowHint.x * inward, elbowHint.y, elbowHint.z));
            if (preferElbow.HasValue && modelElbowWeight > 0)
            {
                var toModel = preferElbow.Value - circle.centre;
                if (toModel.sqrMagnitude > 1e-6f && prefer.sqrMagnitude > 1e-6f) prefer = Vector3.Slerp(prefer.normalized, toModel.normalized, modelElbowWeight);
            }
            var u = Vector3.ProjectOnPlane(prefer, dir);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.ProjectOnPlane(frame.up, dir);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.ProjectOnPlane(frame.forward, dir);
            circle.u = u.normalized; circle.v = Vector3.Cross(dir, circle.u);
            end = shoulder + dir * d;
            elbow = Avoid(shoulder, end, circle, SolveAngle(circle));
            var upperDir = (elbow - shoulder).normalized; var foreDir = (end - elbow).normalized;
            if (hingeAligned)
            {   // local X = bend axis (kept from the last bent pose while the arm is straight)
                var bend = Vector3.Cross(upperDir, foreDir);
                if (bend.sqrMagnitude > 1e-5f) lastBend = bend.normalized;
                upper.rotation = Quaternion.LookRotation(upperDir, Vector3.Cross(upperDir, lastBend));
                fore.position = elbow; fore.rotation = Quaternion.LookRotation(foreDir, Vector3.Cross(foreDir, lastBend));
            }
            else
            {
                upper.rotation = Quaternion.LookRotation(upperDir, Vector3.up);
                fore.position = elbow; fore.rotation = Quaternion.LookRotation(foreDir, Vector3.up);
            }
        }

        struct Circle
        {
            public Vector3 centre, u, v; public float radius;
            public Vector3 At(float angle) => centre + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
        }

        // The bend hint itself when it fits the limits; otherwise the nearest valid angle on either side, bisected
        // to the limit (continuous in the target), preferring the side used last frame so the elbow never flips.
        float SolveAngle(Circle c)
        {
            if (Excess(c.At(0)) <= 0) return lastAngle = 0;
            float best = float.NaN, bestCost = float.MaxValue;
            for (int side = -1; side <= 1; side += 2)
            {
                float previous = 0;
                for (int k = 1; k <= 24; k++)
                {
                    float a = side * k * SolveStep;
                    if (Excess(c.At(a)) > 0) { previous = a; continue; }
                    float lo = previous, hi = a;
                    for (int i = 0; i < 10; i++) { float mid = (lo + hi) * .5f; if (Excess(c.At(mid)) > 0) lo = mid; else hi = mid; }
                    float cost = Mathf.Abs(hi) + .5f * Mathf.Abs(hi - lastAngle);
                    if (cost < bestCost) { bestCost = cost; best = hi; }
                    break;
                }
            }
            if (float.IsNaN(best))
            {   // nothing on the circle fits (target far outside the station): least excess
                float least = float.MaxValue; best = 0;
                for (int k = -24; k <= 24; k++) { float a = k * SolveStep, e = Excess(c.At(a)); if (e < least - 1e-5f) { least = e; best = a; } }
            }
            return lastAngle = best;
        }

        // how far a point is outside the elbow volume (station frame), 0 inside
        float Excess(Vector3 world)
        {
            var p = frame.InverseTransformPoint(world);
            return Mathf.Max(0, Mathf.Abs(p.x) - elbowMaxSide) + Mathf.Max(0, elbowDepth.x - p.z) + Mathf.Max(0, p.z - elbowDepth.y)
                + Mathf.Max(0, p.y - elbowMaxHeight);
        }

        // Keeps the solved elbow unless a beam overlaps an outside collider; then turns it to the nearest clear angle
        // inside the limits. The offset is eased (once per frame) so an obstacle appearing or clearing never pops it.
        Vector3 Avoid(Vector3 shoulder, Vector3 end, Circle c, float angle)
        {
            float want = 0;
            if (obstacles.value != 0 && Blocked(shoulder, c.At(angle), end))
            {
                bool found = false;
                for (int k = 1; k <= 6 && !found; k++)
                    for (int s = 1; s >= -1 && !found; s -= 2)
                    {
                        float a = angle + s * k * AvoidStep; var e = c.At(a);
                        if (Excess(e) <= 0 && !Blocked(shoulder, e, end)) { want = a - angle; found = true; }
                    }
            }
            if (avoidFrame != Time.frameCount)
            {
                avoidFrame = Time.frameCount;
                avoidAngle = Mathf.Lerp(avoidAngle, want, 1 - Mathf.Exp(-14f * Time.deltaTime));
                if (want == 0 && Mathf.Abs(avoidAngle) < 1e-3f) avoidAngle = 0;
            }
            return c.At(angle + avoidAngle);
        }
        bool Blocked(Vector3 shoulder, Vector3 elbow, Vector3 end) => Overlaps(shoulder, elbow) || Overlaps(elbow, end);
        bool Overlaps(Vector3 a, Vector3 b)
        {
            int n = Physics.OverlapCapsuleNonAlloc(a, b, beamRadius, Hits, obstacles, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i];
                if (!c || c.transform.IsChildOf(frame)) continue;                                        // the station's own posts
                if (c is CharacterController || c.GetComponentInParent<CharacterController>()) continue; // the player
                return true;
            }
            return false;
        }

        // the requested up, except when it is (nearly) along the approach: then the jaws' top turns toward the gate
        // side, blended in smoothly so a vertical approach never gets an arbitrary roll
        Vector3 GripperUp(Vector3 look, Vector3 hint)
        {
            var h = hint.sqrMagnitude > 1e-6f ? hint.normalized : Vector3.up;
            float along = Mathf.Abs(Vector3.Dot(h, look)); along *= along; along *= along;
            var up = Vector3.ProjectOnPlane(h - frame.forward * along, look);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(frame.right, look);
            return up;
        }
    }
}
