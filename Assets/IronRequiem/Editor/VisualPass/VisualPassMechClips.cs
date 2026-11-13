using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // F8 clips: procedural, hand-tuned key poses for the mech rig, written as generic Transform clips.
    // Legs are solved with 2-bone IK against explicit foot paths, so planted feet never slide when the
    // T07AnimationDriver advances "Cycle" by distance travelled / stride (stride = GaitStride * scale).
    public static partial class VisualPassMech
    {
        public const string ClipDir = Dir + "/Clips";
        public const string Controller = Dir + "/T07Mech_Locomotion.controller";
        public const string Mask = Dir + "/T07Mech_UpperBody.mask";
        const float Fps = 30;

        // ---------------------------------------------------------------- rig
        public sealed class Rig
        {
            public Transform root; public Dictionary<string, Transform> b = new();
            public Dictionary<Transform, Quaternion> restWorld = new(), restLocal = new();
            public Dictionary<Transform, Vector3> restPos = new(), restLocalPos = new();
            public List<Transform> order = new();
            public Vector3 up = Vector3.up, fwd, right;
            public float legLen, hipHeight, thighLen, shinLen, ankleHeight, halfWidth; public Vector3 center;
            public Vector3 FootBase(string side, float widthScale = 1) => center + right * ((side == "Left" ? -1 : 1) * halfWidth * widthScale) + up * ankleHeight;
            public Rig(GameObject go)
            {
                root = go.transform;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root) continue; if (!b.ContainsKey(t.name)) b[t.name] = t;
                    order.Add(t); restWorld[t] = t.rotation; restLocal[t] = t.localRotation; restPos[t] = t.position; restLocalPos[t] = t.localPosition;
                }
                // lateral axis from hips + shoulders (feet are staggered in the export's rest pose)
                right = Vector3.ProjectOnPlane(P("Right_UpperLeg") - P("Left_UpperLeg") + P("Right_UpperArm") - P("Left_UpperArm"), up).normalized;
                fwd = Vector3.Cross(right, up);
                halfWidth = Mathf.Abs(Vector3.Dot(P("Right_Foot") - P("Left_Foot"), right)) * .5f;
                center = Vector3.ProjectOnPlane(P("Hips"), up);
                thighLen = (P("Left_LowerLeg") - P("Left_UpperLeg")).magnitude; shinLen = (P("Left_Foot") - P("Left_LowerLeg")).magnitude;
                legLen = thighLen + shinLen; hipHeight = P("Hips").y; ankleHeight = P("Left_Foot").y;
            }
            public Transform this[string n] => b[n];
            public Vector3 P(string n) => restPos[b[n]];
        }

        public static float GaitStride(Rig r) => r.legLen * 1.5f;       // full cycle (two steps) in model units

        // A pose: per-bone char-space deltas (inherited down the chain) or absolute root-space rotations.
        sealed class Pose
        {
            public readonly Dictionary<Transform, Quaternion> delta = new(), abs = new();
            public Vector3 hipsOffset;
            public void D(Rig r, string n, Quaternion q) { var t = r[n]; delta[t] = delta.TryGetValue(t, out var o) ? q * o : q; }
            public void D(Rig r, string n, float pitch, float yaw = 0, float roll = 0) =>
                D(r, n, Quaternion.AngleAxis(yaw, r.up) * Quaternion.AngleAxis(pitch, r.right) * Quaternion.AngleAxis(roll, r.fwd));
        }

        // Forward kinematics: world (root-space) rotation and position of every bone for a pose.
        static (Dictionary<Transform, Quaternion> rot, Dictionary<Transform, Vector3> pos) Solve(Rig r, Pose p)
        {
            var rot = new Dictionary<Transform, Quaternion>(); var pos = new Dictionary<Transform, Vector3>();
            foreach (var t in r.order)
            {
                var parent = t.parent; bool top = parent == r.root;
                Quaternion parentRot = top ? Quaternion.identity : rot[parent];
                Quaternion parentRest = top ? Quaternion.identity : r.restWorld[parent];
                Quaternion world;
                if (p.abs.TryGetValue(t, out var a)) world = a;
                else
                {
                    world = parentRot * Quaternion.Inverse(parentRest) * r.restWorld[t];
                    if (p.delta.TryGetValue(t, out var d)) world = (parentRot * Quaternion.Inverse(parentRest)) * d * Quaternion.Inverse(parentRot * Quaternion.Inverse(parentRest)) * world;
                }
                rot[t] = world;
                Vector3 parentPos = top ? Vector3.zero : pos[parent];
                Vector3 restOffset = r.restPos[t] - (top ? Vector3.zero : r.restPos[parent]);
                pos[t] = top ? r.restPos[t] : parentPos + parentRot * Quaternion.Inverse(parentRest) * restOffset;
                if (t == r["Hips"]) pos[t] += p.hipsOffset;
            }
            return (rot, pos);
        }

        // Two-bone leg IK toward an ankle target, knee bending forward; then foot pitch about the ankle.
        static void Leg(Rig r, Pose p, string side, Vector3 ankle, float footPitch, float toeBend, float yaw)
        {
            var fk = Solve(r, p);
            var up = r[side + "_UpperLeg"]; var lo = r[side + "_LowerLeg"]; var ft = r[side + "_Foot"];
            Vector3 H = fk.pos[up];
            Vector3 d = ankle - H; float dist = Mathf.Min(d.magnitude, (r.thighLen + r.shinLen) * .999f);
            Vector3 dir = d.normalized;
            float a = Mathf.Acos(Mathf.Clamp((r.thighLen * r.thighLen + dist * dist - r.shinLen * r.shinLen) / (2 * r.thighLen * dist), -1, 1)) * Mathf.Rad2Deg;
            var yawQ = Quaternion.AngleAxis(yaw, r.up);
            Vector3 axis = yawQ * r.right;
            Vector3 thighDir = Quaternion.AngleAxis(-a, axis) * dir;
            Vector3 knee = H + thighDir * r.thighLen; Vector3 shinDir = (H + dir * dist - knee).normalized;
            Vector3 restThigh = (r.P(side + "_LowerLeg") - r.P(side + "_UpperLeg")).normalized;
            Vector3 restShin = (r.P(side + "_Foot") - r.P(side + "_LowerLeg")).normalized;
            var q1 = Quaternion.FromToRotation(yawQ * restThigh, thighDir) * yawQ;
            p.abs[up] = q1 * r.restWorld[up];
            var q2 = Quaternion.FromToRotation(q1 * restShin, shinDir) * q1;
            p.abs[lo] = q2 * r.restWorld[lo];
            p.abs[ft] = Quaternion.AngleAxis(footPitch, axis) * yawQ * r.restWorld[ft];
            p.abs[r[side + "_Toes"]] = Quaternion.AngleAxis(footPitch + toeBend, axis) * yawQ * r.restWorld[r[side + "_Toes"]];
        }

        // Arm: aim the upper arm and forearm along root-space directions (keeps rest twist as much as possible).
        static void Arm(Rig r, Pose p, string side, Vector3 upperDir, Vector3 foreDir)
        {
            var fk = Solve(r, p);
            var ua = r[side + "_UpperArm"]; var la = r[side + "_LowerArm"]; var hand = r[side + "_Hand"];
            Vector3 restU = (r.P(side + "_LowerArm") - r.P(side + "_UpperArm")).normalized;
            Vector3 restF = (r.P(side + "_Hand") - r.P(side + "_LowerArm")).normalized;
            var parentDelta = fk.rot[ua.parent] * Quaternion.Inverse(r.restWorld[ua.parent]);
            var q1 = Quaternion.FromToRotation(parentDelta * restU, upperDir.normalized) * parentDelta;
            p.abs[ua] = q1 * r.restWorld[ua];
            var q2 = Quaternion.FromToRotation(q1 * restF, foreDir.normalized) * q1;
            p.abs[la] = q2 * r.restWorld[la];
            p.abs[hand] = q2 * r.restWorld[hand];
        }

        static Vector3 Aim(Rig r, float down, float forward, float outward, string side)
        {
            float s = side == "Left" ? -1 : 1;
            return (-r.up * down + r.fwd * forward + r.right * outward * s).normalized;
        }

        // ---------------------------------------------------------------- gait
        sealed class Gait
        {
            public float stride, lift = .14f, stance = .62f, bob = .022f, impact = .014f, sway = .022f, lean = 6, pelvisYaw = 7, chestYaw = -9;
            public float armSwing = 16, kneeSink = .04f, footWidth = 1f, heavy = 1f; public bool disarmed;
        }

        static float Smooth(float x) => x * x * (3 - 2 * x);
        static float Pulse(float x, float w) => Mathf.Exp(-(x * x) / (w * w));

        // Foot path for one leg at local phase q (0 = heel strike).
        static void FootAt(Rig r, Gait g, float q, out float z, out float y, out float pitch, out float toe)
        {
            float S = g.stride * .5f; // step length
            if (q < g.stance)
            {
                float u = q / g.stance; z = Mathf.Lerp(S * .5f, -S * .5f, u); y = 0;
                // heel strike toes-up -> slap flat in the first 12 %, heel-off roll over the last 25 %
                pitch = u < .12f ? Mathf.Lerp(-12, 0, Smooth(u / .12f)) : u > .75f ? Mathf.Lerp(0, 22, Smooth((u - .75f) / .25f)) : 0;
                toe = u > .75f ? -pitch * .9f : 0;
            }
            else
            {
                float u = (q - g.stance) / (1 - g.stance);
                float e = Smooth(Smooth(u));                                  // heavy: slow lift-off, fast reach, firm plant
                z = Mathf.Lerp(-S * .5f, S * .5f, e);
                y = g.lift * r.legLen * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, .85f)), 1.4f);
                pitch = Mathf.Lerp(22, -12, Smooth(u)) * (u < .5f ? 1 : 1) + 8 * Mathf.Sin(Mathf.PI * u);
                toe = Mathf.Lerp(-18, 0, Smooth(Mathf.Clamp01(u * 2)));
            }
        }

        static Pose GaitPose(Rig r, Gait g, float p)
        {
            var pose = new Pose();
            float L = r.legLen;
            // pelvis: dip on each impact, rise at mid-stance, sway over the stance leg
            float step = Mathf.Repeat(p * 2, 1);
            float y = -g.bob * L * (.5f + .5f * Mathf.Cos(2 * Mathf.PI * (step - .1f))) - g.impact * L * Pulse(step - .04f, .05f) - g.kneeSink * L;
            float x = -g.sway * L * Mathf.Sin(2 * Mathf.PI * p);
            pose.hipsOffset = r.up * y + r.right * x;
            float yaw = g.pelvisYaw * Mathf.Sin(2 * Mathf.PI * p);            // right hip forward as the right leg swings
            float roll = 2.5f * Mathf.Sin(2 * Mathf.PI * p) * g.heavy;
            pose.D(r, "Hips", 0, yaw, roll);
            // torso: lean, counter-twist, absorb impacts
            float absorb = 2.2f * Pulse(step - .06f, .06f) * g.heavy;
            pose.D(r, "Spine", g.lean * .45f + absorb, -yaw * .6f, -roll * .7f);
            pose.D(r, "Chest", g.lean * .35f, g.chestYaw * Mathf.Sin(2 * Mathf.PI * p) * .5f - yaw * .2f, -roll * .3f);
            pose.D(r, "UpperChest", g.lean * .2f - absorb * .5f, g.chestYaw * Mathf.Sin(2 * Mathf.PI * p) * .5f, 0);
            pose.D(r, "Neck", -g.lean * .5f, 0, 0);
            pose.D(r, "Head", -g.lean * .4f - absorb * .6f + 1.2f * Mathf.Sin(4 * Mathf.PI * p), -g.chestYaw * .4f * Mathf.Sin(2 * Mathf.PI * p), 0);
            // legs
            foreach (var side in new[] { "Left", "Right" })
            {
                float q = Mathf.Repeat(p - (side == "Left" ? 0 : .5f), 1);
                FootAt(r, g, q, out var z, out var fy, out var pitch, out var toe);
                float s = side == "Left" ? -1 : 1;
                var ankle = r.FootBase(side, g.footWidth) + r.fwd * z + r.up * fy;
                // roll over the ball (heel-off) or the heel (strike) instead of spinning about the ankle
                float footLen = Vector3.Dot(r.P(side + "_Toes") - r.P(side + "_Foot"), r.fwd);
                float rad = pitch * Mathf.Deg2Rad;
                if (pitch > 0 && fy <= 0) ankle += r.up * footLen * Mathf.Sin(rad) - r.fwd * footLen * (1 - Mathf.Cos(rad));
                else if (pitch < 0 && fy <= 0) ankle += r.up * (.35f * footLen) * Mathf.Sin(-rad);
                Leg(r, pose, side, ankle, pitch, toe, yaw * .5f);
            }
            // shoulders and arms
            float swing = Mathf.Sin(2 * Mathf.PI * p);                        // + : left leg forward
            pose.D(r, "Left_Shoulder", 0, 0, 0);
            Arm(r, pose, "Left", Aim(r, 1, -.18f * swing * g.armSwing / 16 + .05f, .22f, "Left"), Aim(r, .78f, .5f - .25f * swing * g.armSwing / 16, .1f, "Left"));
            if (g.disarmed)
            {
                float flop = Mathf.Sin(2 * Mathf.PI * (p + .1f));
                Arm(r, pose, "Right", Aim(r, 1, .1f * flop, .28f, "Right"), Aim(r, 1, .15f + .25f * flop, .2f, "Right"));
            }
            else
            {
                float bobAim = .03f * Mathf.Sin(4 * Mathf.PI * (p - .08f));
                Arm(r, pose, "Right", Aim(r, 1, .55f, .32f, "Right"), Aim(r, .08f + bobAim, 1, .02f, "Right"));
            }
            return pose;
        }

        // ---------------------------------------------------------------- other clips
        static Pose StandPose(Rig r, float crouch, float lean, float t, bool aim, float scan, float breathe)
        {
            var pose = new Pose(); float L = r.legLen;
            float b = Mathf.Sin(2 * Mathf.PI * t);
            float shift = Mathf.Sin(2 * Mathf.PI * t * .5f);
            pose.hipsOffset = r.up * (-crouch * L - breathe * .006f * L * (.5f + .5f * b)) + r.right * (.012f * L * shift);
            pose.D(r, "Hips", 0, 1.5f * shift, -1.2f * shift);
            pose.D(r, "Spine", lean * .5f + breathe * .8f * b, -1f * shift, 1f * shift);
            pose.D(r, "Chest", lean * .3f + breathe * .7f * b, 0, 0);
            pose.D(r, "UpperChest", lean * .2f - breathe * .5f * b, 0, 0);
            float look = scan * Mathf.Sin(2 * Mathf.PI * t) * Mathf.Abs(Mathf.Sin(2 * Mathf.PI * t));
            pose.D(r, "Neck", -lean * .5f, look * .4f, 0);
            pose.D(r, "Head", -lean * .4f - breathe * .6f * b, look * .6f, 0);
            foreach (var side in new[] { "Left", "Right" })
            {
                float s = side == "Left" ? -1 : 1;
                var ankle = r.FootBase(side, 1 + crouch * 2.5f) + r.fwd * ((side == "Left" ? .04f : -.03f) * L * crouch * 4);
                Leg(r, pose, side, ankle, 0, 0, s * 6 * crouch * 4);
            }
            Arm(r, pose, "Left", Aim(r, 1, .08f + .02f * b, .24f, "Left"), Aim(r, .7f, .55f, .1f, "Left"));
            if (aim) Arm(r, pose, "Right", Aim(r, 1, .55f, .32f, "Right"), Aim(r, .05f + .01f * b, 1, .02f, "Right"));
            else Arm(r, pose, "Right", Aim(r, 1, .25f, .3f, "Right"), Aim(r, .55f + .02f * b, .8f, .05f, "Right"));
            return pose;
        }

        static Pose TurnPose(Rig r, float p)
        {
            var pose = StandPose(r, .025f, 4, p, true, 0, .6f);
            float L = r.legLen; var g = new Gait();
            foreach (var side in new[] { "Left", "Right" })
            {
                float q = Mathf.Repeat(p - (side == "Left" ? 0 : .5f), 1);
                float lift = q > .55f ? Mathf.Sin(Mathf.PI * (q - .55f) / .45f) * .07f * L : 0;
                float s = side == "Left" ? -1 : 1;
                var ankle = r.FootBase(side) + r.up * lift;
                Leg(r, pose, side, ankle, lift > 0 ? 6 : 0, 0, 0);
            }
            pose.hipsOffset += r.up * (-.012f * L * Pulse(Mathf.Repeat(p * 2, 1) - .05f, .07f));
            return pose;
        }

        static Pose AttackPose(Rig r, float t) // additive over UpperBodyNeutral: first frame == neutral
        {
            var pose = StandPose(r, .0f, 0, 0, true, 0, 0);
            float k = t < .06f ? 0 : Mathf.Exp(-(t - .06f) * 9) * Mathf.Min(1, (t - .06f) / .025f);
            pose.D(r, "Spine", -4 * k, 0, 0); pose.D(r, "Chest", -3 * k, 3 * k, 0); pose.D(r, "UpperChest", -2 * k, 0, 0);
            Arm(r, pose, "Right", Aim(r, 1, .55f - .25f * k, .32f + .05f * k, "Right"), Aim(r, .05f - .35f * k, 1, .02f, "Right"));
            pose.D(r, "Head", 2 * k, 0, 0);
            return pose;
        }

        static Pose HitPose(Rig r, float t)
        {
            float k = t < .05f ? t / .05f : Mathf.Exp(-(t - .05f) * 6) * (1 + .3f * Mathf.Sin((t - .05f) * 30) * Mathf.Exp(-(t - .05f) * 8));
            var pose = StandPose(r, .02f + .03f * k, -10 * k, 0, true, 0, 0);
            pose.D(r, "Chest", 0, 6 * k, 4 * k); pose.D(r, "Head", -8 * k, -10 * k, 0);
            return pose;
        }

        static Pose DeathPose(Rig r, float t)
        {
            float L = r.legLen;
            float kneel = Smooth(Mathf.Clamp01(t / .55f));                  // buckle onto the knees
            float topple = Smooth(Mathf.Clamp01((t - .45f) / .75f));        // fall forward
            var pose = new Pose();
            float drop = kneel * (r.hipHeight - r.ankleHeight - r.shinLen * .35f) + topple * (r.shinLen * .5f);
            pose.hipsOffset = -r.up * drop + r.fwd * (.18f * L * kneel + .35f * L * topple);
            pose.D(r, "Hips", 15 * kneel + 68 * topple, 6 * topple, 9 * topple);
            pose.D(r, "Spine", 10 * kneel + 8 * topple, 0, 0); pose.D(r, "Chest", 8 * kneel, -4 * topple, 0);
            pose.D(r, "Head", 15 * kneel + 10 * topple, 12 * topple, 0);
            foreach (var side in new[] { "Left", "Right" })
            {
                float s = side == "Left" ? -1 : 1;
                pose.D(r, side + "_UpperLeg", -(80 * kneel - 30 * topple), 0, s * 4);
                pose.D(r, side + "_LowerLeg", 110 * kneel - 60 * topple, 0, 0);
                pose.D(r, side + "_Foot", -25 * kneel, 0, 0);
            }
            Arm(r, pose, "Left", Aim(r, 1 - .6f * topple, .2f + .9f * topple, .35f, "Left"), Aim(r, .8f - .6f * topple, .5f + .6f * topple, .2f, "Left"));
            Arm(r, pose, "Right", Aim(r, 1 - .5f * topple, .4f + .7f * topple, .4f, "Right"), Aim(r, .3f, 1, .1f, "Right"));
            return pose;
        }

        // ---------------------------------------------------------------- writing
        static AnimationClip Write(Rig r, string name, float seconds, bool loop, Func<float, Pose> at)
        {
            EnsureFolder(ClipDir);
            var path = $"{ClipDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.frameRate = Fps;
            int frames = Mathf.Max(2, Mathf.RoundToInt(seconds * Fps)) + 1;
            var animated = new HashSet<Transform>();
            var samples = new List<(Dictionary<Transform, Quaternion> local, Vector3 hips)>();
            for (int f = 0; f < frames; f++)
            {
                float u = (float)f / (frames - 1); if (loop && f == frames - 1) u = 0;
                var pose = at(u);
                var fk = Solve(r, pose);
                var local = new Dictionary<Transform, Quaternion>();
                foreach (var t in r.order)
                {
                    var parentRot = t.parent == r.root ? Quaternion.identity : fk.rot[t.parent];
                    var lq = Quaternion.Inverse(parentRot) * fk.rot[t];
                    local[t] = lq; if (Quaternion.Angle(lq, r.restLocal[t]) > .05f) animated.Add(t);
                }
                samples.Add((local, pose.hipsOffset));
            }
            var hips = r["Hips"];
            foreach (var t in animated)
            {
                var path2 = AnimationUtility.CalculateTransformPath(t, r.root);
                var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
                Quaternion prev = samples[0].local[t];
                for (int f = 0; f < frames; f++)
                {
                    var q = samples[f].local[t]; if (Quaternion.Dot(prev, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); prev = q;
                    float time = f / Fps; cx.AddKey(time, q.x); cy.AddKey(time, q.y); cz.AddKey(time, q.z); cw.AddKey(time, q.w);
                }
                foreach (var (c, prop) in new[] { (cx, "x"), (cy, "y"), (cz, "z"), (cw, "w") })
                {
                    for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path2, typeof(Transform), "m_LocalRotation." + prop), c);
                }
            }
            {
                var path2 = AnimationUtility.CalculateTransformPath(hips, r.root);
                var cs = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                for (int f = 0; f < frames; f++)
                {
                    var lp = r.restLocalPos[hips] + hips.parent.InverseTransformVector(r.root.TransformVector(samples[f].hips));
                    for (int a = 0; a < 3; a++) cs[a].AddKey(f / Fps, lp[a]);
                }
                for (int a = 0; a < 3; a++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path2, typeof(Transform), "m_LocalPosition." + "xyz"[a]), cs[a]);
            }
            var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, s);
            clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip);
            return clip;
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 3 build clips + controller")]
        static void BuildClips()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab);
            var wrap = UnityEngine.Object.Instantiate(prefab); var go = wrap.transform.Find("Model").gameObject;
            go.transform.SetParent(null, false); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
            UnityEngine.Object.DestroyImmediate(wrap);
            var log = new StringBuilder("[F8 clips]\n");
            try
            {
                var r = new Rig(go);
                log.AppendLine($"legLen={r.legLen:0.###} hip={r.hipHeight:0.###} ankle={r.ankleHeight:0.###} fwd={r.fwd} stride={GaitStride(r):0.###}");
                var armed = new Gait { stride = GaitStride(r) };
                var disarmed = new Gait { stride = GaitStride(r), lean = 12, disarmed = true, sway = .03f, pelvisYaw = 9, armSwing = 22, bob = .028f };
                var clips = new Dictionary<string, AnimationClip>
                {
                    ["Walk"] = Write(r, "Mech_Walk", 1.4f, true, p => GaitPose(r, armed, p)),
                    ["DisarmedWalk"] = Write(r, "Mech_DisarmedWalk", 1.2f, true, p => GaitPose(r, disarmed, p)),
                    ["Idle"] = Write(r, "Mech_Idle", 4f, true, t => StandPose(r, .012f, 3, t, false, 9, 1)),
                    ["CombatIdle"] = Write(r, "Mech_CombatIdle", 2.4f, true, t => StandPose(r, .045f, 9, t, true, 4, 1.4f)),
                    ["Turn"] = Write(r, "Mech_Turn", 1.1f, true, p => TurnPose(r, p)),
                    ["CannonAttack"] = Write(r, "Mech_CannonAttack", .6f, false, t => AttackPose(r, t * .6f)),
                    ["UpperBodyNeutral"] = Write(r, "Mech_UpperBodyNeutral", .5f, true, t => StandPose(r, 0, 0, 0, true, 0, 0)),
                    ["HitReaction"] = Write(r, "Mech_HitReaction", .55f, false, t => HitPose(r, t * .55f)),
                    ["Death"] = Write(r, "Mech_Death", 1.5f, false, t => DeathPose(r, t * 1.5f)),
                };
                // upper body mask (generic transform paths)
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(Mask);
                if (!mask) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, Mask); }
                mask.AddTransformPath(go.transform, true);
                var upper = new HashSet<Transform>(r["Spine"].GetComponentsInChildren<Transform>(true));
                for (int i = 0; i < mask.transformCount; i++)
                {
                    var p = mask.GetTransformPath(i); var t = string.IsNullOrEmpty(p) ? go.transform : go.transform.Find(p);
                    mask.SetTransformActive(i, t && upper.Contains(t));
                }
                EditorUtility.SetDirty(mask);
                // controller: same graph and parameters as T07_Locomotion, clips and mask swapped
                if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller))
                    AssetDatabase.CopyAsset("Assets/IronRequiem/Art/Enemies/T07Animated/T07_Locomotion.controller", Controller);
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
                int swapped = 0;
                Motion Map(Motion m)
                {
                    if (m is AnimationClip c)
                    {
                        var key = c.name.StartsWith("Mech_") ? c.name.Substring(5) : c.name;
                        if (clips.TryGetValue(key, out var nc)) { swapped++; return nc; }
                        log.AppendLine("  unmapped clip " + c.name); return m;
                    }
                    if (m is BlendTree bt)
                    {
                        var ch = bt.children; for (int i = 0; i < ch.Length; i++) ch[i].motion = Map(ch[i].motion); bt.children = ch; EditorUtility.SetDirty(bt);
                    }
                    return m;
                }
                var layers = ctrl.layers;
                for (int li = 0; li < layers.Length; li++)
                {
                    foreach (var st in layers[li].stateMachine.states) { st.state.motion = Map(st.state.motion); EditorUtility.SetDirty(st.state); }
                    if (layers[li].avatarMask) layers[li].avatarMask = mask;
                }
                ctrl.layers = layers; EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                log.AppendLine($"clips={clips.Count} swapped motions={swapped} controller={Controller}");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Debug.Log(log.ToString()); }
        }

        // ---------------------------------------------------------------- preview contact sheets (edit mode)
        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 4 preview sheets")]
        static void Preview()
        {
            foreach (var n in new[] { "Mech_Walk", "Mech_DisarmedWalk", "Mech_Idle", "Mech_CombatIdle", "Mech_Turn", "Mech_CannonAttack", "Mech_HitReaction", "Mech_Death" })
                Sheet(n, n == "Mech_Walk" ? 12 : 8);
        }

        static void Sheet(string clipName, int frames)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipDir}/{clipName}.anim");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab);
            if (!clip || !prefab) { Debug.LogWarning("[F8 preview] missing " + clipName); return; }
            var pru = new PreviewRenderUtility(); const int W = 256, H = 320;
            var sheet = new Texture2D(W * frames, H * 2, TextureFormat.RGB24, false);
            try
            {
                var go = pru.InstantiatePrefabInScene(prefab);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * .3f;
                var fm = new Material(Shader.Find("Universal Render Pipeline/Unlit")); fm.color = new Color(.18f, .2f, .22f); floor.GetComponent<Renderer>().sharedMaterial = fm;
                pru.AddSingleGO(floor);
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(); smr.updateWhenOffscreen = true;
                pru.camera.fieldOfView = 26; pru.camera.nearClipPlane = .01f; pru.camera.farClipPlane = 50;
                pru.camera.clearFlags = CameraClearFlags.SolidColor; pru.camera.backgroundColor = new Color(.42f, .45f, .5f);
                pru.lights[0].intensity = 1.3f; pru.lights[0].transform.rotation = Quaternion.Euler(40, -30, 0); pru.lights[1].intensity = .7f;
                pru.ambientColor = new Color(.35f, .35f, .4f);
                for (int view = 0; view < 2; view++)
                    for (int f = 0; f < frames; f++)
                    {
                        float t = clip.length * f / frames;
                        clip.SampleAnimation(go.transform.Find("Model").gameObject, t);
                        Vector3 c = new Vector3(0, .5f, 0);
                        var dir = view == 0 ? Vector3.right : (Vector3.forward + Vector3.right * .35f).normalized;
                        pru.camera.transform.position = c + dir * 3.2f + Vector3.up * .15f; pru.camera.transform.LookAt(c);
                        pru.BeginStaticPreview(new Rect(0, 0, W, H));
                        pru.Render(true);
                        var tex = pru.EndStaticPreview();
                        var rt = RenderTexture.GetTemporary(W, H, 0); Graphics.Blit(tex, rt);
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        sheet.ReadPixels(new Rect(0, 0, W, H), f * W, (1 - view) * H); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    }
                sheet.Apply();
                System.IO.Directory.CreateDirectory("Tools/IronRequiem/Bridge");
                System.IO.File.WriteAllBytes($"Tools/IronRequiem/Bridge/sheet_{clipName}.png", sheet.EncodeToPNG());
                Debug.Log($"[F8 preview] {clipName} ok");
            }
            finally { pru.Cleanup(); UnityEngine.Object.DestroyImmediate(sheet); }
        }
    }
}
