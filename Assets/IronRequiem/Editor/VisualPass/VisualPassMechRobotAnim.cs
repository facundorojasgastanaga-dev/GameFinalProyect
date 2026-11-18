using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Robot motion test pass for the T-07 Mech (17/11/2026). Writes servo-style clips and a copied
    // controller into their own folder and renders before/after sheets. It never edits the F8 clips,
    // the F8 controller, the production prefabs or any scene.
    //
    // Motion language (the "robot standard" this pass is judged against):
    //  R1 Servo moves: each key move is short (0.1-0.3 s), eases out with ~10 % overshoot, then HOLDS.
    //  R2 Sensor head: the head is gyro-stabilised in model space (no bob from steps or recoil) and
    //     scans in snaps (saccade + hold), never in a slow human sway.
    //  R3 No breathing: idle life comes from a low-amplitude servo hum and discrete weight re-settles.
    //  R4 Mass: every heel strike sends an under-damped jolt up the torso; the lowered cannon lags it.
    //  R5 Shot: recoil peaks within 2 frames of the Attack trigger (the shot is already fired), the
    //     chain is cannon -> shoulder -> chest, the head stays locked, then an under-damped re-centre.
    //  R6 Damage: knock-back jolt, then a servo stutter (held frames) and a quick sensor re-calibration.
    //  Feet keep the F8 IK and stride, so the T07AnimationDriver distance/stride contract is unchanged.
    public static partial class VisualPassMech
    {
        public const string RobotDir = "Assets/IronRequiem/Art/Enemies/T07MechRobotAnim";
        public const string RobotClipDir = RobotDir + "/Clips";
        public const string RobotController = RobotDir + "/T07MechRobot_Locomotion.controller";
        public const string RobotQueue = "Tools/IronRequiem/Requests/t07mech-robot-anim.txt";
        public const string RobotReports = "Tools/IronRequiem/Reports/T07MechRobotAnim";

        // ---------------------------------------------------------------- servo helpers
        static float RServo(float x) { x = Mathf.Clamp01(x); const float s = 1.70158f; x -= 1; return x * x * ((s + 1) * x + s) + 1; }

        // Unit impulse response: smooth rise to 1 in `rise` s, then a decaying oscillation.
        static float RKick(float t, float rise, float hz, float decay)
        {
            if (t <= 0) return 0;
            if (t < rise) { float u = t / rise; return u * u * (3 - 2 * u); }
            float s = t - rise; return Mathf.Exp(-decay * s) * Mathf.Cos(2 * Mathf.PI * hz * s);
        }

        // Keyed servo track: pairs (time, value); each change takes `move` with overshoot, then holds.
        static float RTrack(float t, float move, params float[] tv)
        {
            float v = tv[1];
            for (int i = 2; i + 1 < tv.Length; i += 2)
            {
                if (t < tv[i]) break;
                float u = (t - tv[i]) / move;
                v = Mathf.LerpUnclamped(tv[i - 1], tv[i + 1], u >= 1 ? 1 : RServo(u));
            }
            return v;
        }

        // Loop-safe hum: frequency rounded so a whole number of cycles fits the clip.
        static float RHum(float t, float len, float hz, float phase)
        {
            float f = Mathf.Max(1, Mathf.Round(hz * len)) / len; return Mathf.Sin(2 * Mathf.PI * (f * t + phase));
        }

        static void RLockHead(Rig r, Pose p, float yaw, float pitch) =>
            p.abs[r["Head"]] = Quaternion.AngleAxis(yaw, r.up) * Quaternion.AngleAxis(pitch, r.right) * r.restWorld[r["Head"]];

        // Cannon arm. The mech's cannon runs along its forearm, so `fore` is both the forearm and the barrel.
        // A rig whose cannon is mounted across the forearm (campaign T-07: forearm hangs, barrel points ahead)
        // sets rCannonPerp; `fore` is then the barrel direction and the forearm is solved below it, twisted so
        // the barrel (rest direction rBarrelRest) points along `fore`.
        static bool rCannonPerp; static Vector3 rBarrelRest = Vector3.forward;
        static void RCannonArm(Rig r, Pose p, Vector3 upperDir, Vector3 fore)
        {
            if (!rCannonPerp) { Arm(r, p, "Right", upperDir, fore); return; }
            var barrel = fore.normalized;
            var axis = Vector3.Cross(r.up, barrel); if (axis.sqrMagnitude < 1e-4f) axis = r.right; axis.Normalize();
            var forearm = Quaternion.AngleAxis(90, axis) * barrel;
            Arm(r, p, "Right", upperDir, forearm);
            var la = r["Right_LowerArm"]; var hand = r["Right_Hand"];
            var cur = p.abs[la] * Quaternion.Inverse(r.restWorld[la]) * rBarrelRest;
            var twist = Quaternion.FromToRotation(Vector3.ProjectOnPlane(cur, forearm), Vector3.ProjectOnPlane(barrel, forearm));
            p.abs[la] = twist * p.abs[la]; p.abs[hand] = twist * p.abs[hand];
        }

        // ---------------------------------------------------------------- poses
        static Pose RIdle(Rig r, float u)
        {
            const float T = 4f; float t = u * T, L = r.legLen;
            float shift = RTrack(t, .3f, 0, 0, .9f, 1, 2.4f, -.6f, 3.5f, 0);
            float hum = RHum(t, T, 6, 0) * .35f + RHum(t, T, 9, .3f) * .2f;
            var p = new Pose();
            p.hipsOffset = r.up * (-.014f * L) + r.right * (.016f * L * shift);
            p.D(r, "Hips", 0, 0, -2.2f * shift);
            p.D(r, "Spine", 1.5f + hum * .3f, 0, 1.8f * shift);
            p.D(r, "Chest", 1f, 0, 0);
            p.D(r, "UpperChest", .5f + hum * .2f, 0, 0);
            foreach (var side in new[] { "Left", "Right" }) Leg(r, p, side, r.FootBase(side, 1.05f), 0, 0, 0);
            float yaw = RTrack(t, .16f, 0, 0, .5f, -30, 1.5f, 0, 2.1f, 26, 3.1f, 0);
            float pitch = RTrack(t, .16f, 0, 0, .5f, 4, 1.5f, 0, 2.1f, 4, 3.1f, 0);
            p.D(r, "Neck", 0, yaw * .35f, 0);
            RLockHead(r, p, yaw, pitch + hum * .15f);
            Arm(r, p, "Left", Aim(r, 1, .06f, .22f, "Left"), Aim(r, .75f, .5f, .1f, "Left"));
            // the lowered cannon pans with the sensor head (stays low: idle must not read as combat)
            float pan = RTrack(t, .22f, 0, 0, .55f, -1, 1.45f, 0, 2.15f, 1, 3.05f, 0);
            RCannonArm(r, p, Aim(r, 1, .25f, .3f + .04f * pan, "Right"), Aim(r, .55f - .08f * Mathf.Abs(pan), .8f, .05f + .1f * pan, "Right"));
            return p;
        }

        static Pose RCombatIdle(Rig r, float u)
        {
            const float T = 2.4f; float t = u * T, L = r.legLen;
            float hum = RHum(t, T, 7, 0) * .3f + RHum(t, T, 11, .4f) * .15f;
            float aimYaw = RTrack(t, .12f, 0, 0, .6f, .035f, 1.2f, -.02f, 1.8f, 0);
            float aimUp = RTrack(t, .12f, 0, 0, .9f, .02f, 1.8f, 0);
            var p = new Pose();
            p.hipsOffset = r.up * (-.05f * L);
            p.D(r, "Hips", 0, -6, 0);
            p.D(r, "Spine", 5 + hum * .3f, 0, 0);
            p.D(r, "Chest", 2, 6, 0);
            p.D(r, "UpperChest", 1 + hum * .2f, 0, 0);
            Leg(r, p, "Left", r.FootBase("Left", 1.2f) + r.fwd * (.07f * L), 0, 0, -8);
            Leg(r, p, "Right", r.FootBase("Right", 1.2f) - r.fwd * (.06f * L), 0, 0, 10);
            RLockHead(r, p, aimYaw * 57f, -2 + hum * .1f);
            RCannonArm(r, p, Aim(r, 1, .55f, .32f, "Right"), Aim(r, .05f - aimUp, 1, .02f + aimYaw, "Right"));
            Arm(r, p, "Left", Aim(r, 1, .2f, .28f, "Left"), Aim(r, .45f, .75f, 0, "Left"));
            return p;
        }

        static float rStride; // > 0: stride (model units) of the rig being written; 0 = mech default
        static Pose RWalk(Rig r, float ph, bool combat)
        {
            var g = combat
                ? new Gait { stride = rStride > 0 ? rStride : GaitStride(r), lift = .1f, bob = .014f, impact = .026f, sway = .014f, lean = 15, pelvisYaw = 4, chestYaw = -3, armSwing = 6, kneeSink = .11f, footWidth = 1.18f }
                : new Gait { stride = rStride > 0 ? rStride : GaitStride(r), lift = .13f, bob = .026f, impact = .026f, sway = .024f, lean = 4, pelvisYaw = 7, chestYaw = -7, armSwing = 12, kneeSink = .045f };
            var p = GaitPose(r, g, ph);
            float step = Mathf.Repeat(ph * 2, 1);
            float jolt = RKick(step * .7f, .03f, 3.5f, 7f);
            p.D(r, "Spine", 1.6f * jolt, 0, 0);
            p.D(r, "UpperChest", -1f * jolt, 0, 0);
            if (combat)
            {
                RLockHead(r, p, 0, -2);
                float sway = Mathf.Sin(2 * Mathf.PI * ph);
                RCannonArm(r, p, Aim(r, 1, .55f, .32f, "Right"), Aim(r, .06f + .05f * jolt, 1, .02f + .012f * sway, "Right"));
            }
            else
            {
                float scan = RTrack(ph, .1f, 0, 0, .1f, -14, .45f, 14, .8f, 0);
                RLockHead(r, p, scan, 4);
                RCannonArm(r, p, Aim(r, 1, .22f, .3f, "Right"), Aim(r, .58f + .12f * jolt, .78f, .06f, "Right"));
            }
            return p;
        }

        static Pose RAttack(Rig r, float t) // 0.7 s on the UpperBody layer; first frame == UpperBodyNeutral
        {
            var p = StandPose(r, 0, 0, 0, true, 0, 0);
            float k = RKick(t, .05f, 2.2f, 4.5f) * (1 - Smooth(Mathf.Clamp01((t - .58f) / .12f)));
            p.D(r, "Spine", -8 * k, 0, 0);
            p.D(r, "Chest", -4 * k, 10 * k, 0);
            p.D(r, "UpperChest", -3 * k, 0, 0);
            RLockHead(r, p, 0, 0);
            RCannonArm(r, p, Aim(r, 1, .55f - .42f * k, .32f + .08f * k, "Right"), Aim(r, .05f - .75f * k, 1, .02f + .06f * k, "Right"));
            return p;
        }

        static Pose RHit(Rig r, float t) // 0.6 s
        {
            // reads differently from the shot: the shot rocks the torso straight back, a hit twists and
            // rolls it sideways, then the servos stutter (held frames) and the sensor re-calibrates
            float tq = t > .1f && t < .4f ? .1f + Mathf.Floor((t - .1f) / .07f) * .07f : t; // servo stutter
            float k = RKick(tq, .04f, 2.0f, 5f) * (1 - Smooth(Mathf.Clamp01((t - .5f) / .1f)));
            var p = StandPose(r, .02f + .04f * Mathf.Max(0, k), -7 * k, 0, true, 0, 0);
            p.D(r, "Spine", 0, -6 * k, 10 * k);
            p.D(r, "Chest", 0, 16 * k, 6 * k);
            p.D(r, "UpperChest", -2 * k, 0, 0);
            float shake = RTrack(t, .04f, 0, 0, .42f, 10, .47f, -8, .52f, 5, .56f, 0);
            RLockHead(r, p, 20 * k + shake, -8 * k);
            RCannonArm(r, p, Aim(r, 1, .55f - .2f * k, .32f + .18f * k, "Right"), Aim(r, .05f + .45f * k, 1 - .25f * k, .02f + .36f * k, "Right")); // swings out, clear of the thigh
            Arm(r, p, "Left", Aim(r, 1, .08f - .1f * k, .24f + .25f * k, "Left"), Aim(r, .7f - .3f * k, .55f, .1f + .25f * k, "Left"));
            return p;
        }

        // ---------------------------------------------------------------- writing (same sampler as F8, own folder)
        static AnimationClip RWrite(Rig r, string name, float seconds, bool loop, Func<float, Pose> at,
            string dir = null, Func<Transform, string> pathOf = null, ICollection<Transform> skip = null)
        {
            dir ??= RobotClipDir;
            EnsureFolder(dir);
            var path = $"{dir}/{name}.anim";
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
                    local[t] = lq; if (Quaternion.Angle(lq, r.restLocal[t]) > .05f && (skip == null || !skip.Contains(t))) animated.Add(t);
                }
                samples.Add((local, pose.hipsOffset));
            }
            foreach (var t in animated)
            {
                var bpath = pathOf != null ? pathOf(t) : AnimationUtility.CalculateTransformPath(t, r.root);
                var cs = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                Quaternion prev = samples[0].local[t];
                for (int f = 0; f < frames; f++)
                {
                    var q = samples[f].local[t]; if (Quaternion.Dot(prev, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); prev = q;
                    float time = f / Fps; cs[0].AddKey(time, q.x); cs[1].AddKey(time, q.y); cs[2].AddKey(time, q.z); cs[3].AddKey(time, q.w);
                }
                for (int a = 0; a < 4; a++)
                {
                    // linear tangents keep the servo holds and snaps crisp (no auto-tangent smoothing)
                    for (int i = 0; i < cs[a].length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(cs[a], i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(cs[a], i, AnimationUtility.TangentMode.Linear);
                    }
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bpath, typeof(Transform), "m_LocalRotation." + "xyzw"[a]), cs[a]);
                }
            }
            {
                var hips = r["Hips"];
                var bpath = pathOf != null ? pathOf(hips) : AnimationUtility.CalculateTransformPath(hips, r.root);
                var cs = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                for (int f = 0; f < frames; f++)
                {
                    var lp = r.restLocalPos[hips] + hips.parent.InverseTransformVector(r.root.TransformVector(samples[f].hips));
                    for (int a = 0; a < 3; a++) cs[a].AddKey(f / Fps, lp[a]);
                }
                for (int a = 0; a < 3; a++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bpath, typeof(Transform), "m_LocalPosition." + "xyz"[a]), cs[a]);
            }
            var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, s);
            clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip);
            return clip;
        }

        internal static void RobotRun(string op)
        {
            if (op == "clips") RobotBuildClips();
            else if (op == "labs") RobotBuildLabs();
            else if (op == "play-before") RobotPlay("Before");
            else if (op == "play-after") RobotPlay("After");
            else if (op == "inspect") RobotInspect();
            else if (op == "showcase") RobotBuildShowcase();
            else if (op == "showcase-shot") RobotShowcaseShot();
            else if (op.StartsWith("open:")) RobotOpenScene(op.Substring(5).Trim());
            else if (op == "showcase-check") RobotShowcaseCheck();
            else if (op.StartsWith("t07-")) T07Run(op.Substring(4));
            else if (op.StartsWith("saw-")) SawRun(op.Substring(4));
            else throw new InvalidOperationException("Unknown fixed operation: " + op);
        }

        // Close-up views of the peak frames (shot and hit) to look for cannon/torso interpenetration.
        [MenuItem("Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - inspect peaks")]
        static void RobotInspect()
        {
            Directory.CreateDirectory(RobotReports);
            var views = new[] { new Vector3(0, 0, 1), new Vector3(0, 1, .2f), new Vector3(1, 0, 0), new Vector3(-1, 0, 0) };
            RViews($"{RobotClipDir}/R_CannonAttack.anim", $"{RobotClipDir}/R_CombatIdle.anim", new[] { 0f, .033f, .066f, .1f, .2f }, views, $"{RobotReports}/inspect_attack.png");
            RViews($"{RobotClipDir}/R_HitReaction.anim", null, new[] { 0f, .05f, .1f, .15f, .25f }, views, $"{RobotReports}/inspect_hit.png");
            RViews($"{RobotClipDir}/R_Idle.anim", null, new[] { 0f, .8f, 1.2f, 2.4f, 2.8f }, views, $"{RobotReports}/inspect_idle.png");
        }

        static void RViews(string clipPath, string basePath, float[] times, Vector3[] views, string outPng,
            string prefabPath = null, Func<GameObject, GameObject> modelOf = null, Action<GameObject> afterSample = null, float height = 1.08f)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            var baseClip = basePath != null ? AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath) : null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath ?? VisualPrefab);
            if (!clip || !prefab) throw new InvalidOperationException("Missing clip or prefab: " + clipPath);
            var pru = new PreviewRenderUtility(); const int W = 320, H = 320;
            var sheet = new Texture2D(W * times.Length, H * views.Length, TextureFormat.RGB24, false);
            try
            {
                var go = pru.InstantiatePrefabInScene(prefab);
                var model = modelOf != null ? modelOf(go) : go.transform.Find("Model").gameObject;
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
                pru.camera.fieldOfView = 30; pru.camera.nearClipPlane = .01f; pru.camera.farClipPlane = 50;
                pru.camera.clearFlags = CameraClearFlags.SolidColor; pru.camera.backgroundColor = new Color(.42f, .45f, .5f);
                pru.lights[0].intensity = 1.3f; pru.lights[0].transform.rotation = Quaternion.Euler(40, -30, 0); pru.lights[1].intensity = .7f;
                pru.ambientColor = new Color(.35f, .35f, .4f);
                for (int v = 0; v < views.Length; v++)
                    for (int f = 0; f < times.Length; f++)
                    {
                        if (baseClip) baseClip.SampleAnimation(model, 0);
                        clip.SampleAnimation(model, Mathf.Min(times[f], clip.length));
                        afterSample?.Invoke(go);
                        float k = height / 1.08f;
                        var c = new Vector3(0, .68f * k, 0);
                        pru.camera.transform.position = c + views[v].normalized * 1.5f * k; pru.camera.transform.LookAt(c, Mathf.Abs(views[v].normalized.y) > .9f ? Vector3.forward : Vector3.up);
                        pru.BeginStaticPreview(new Rect(0, 0, W, H)); pru.Render(true); var tex = pru.EndStaticPreview();
                        var rt = RenderTexture.GetTemporary(W, H, 0); Graphics.Blit(tex, rt);
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        sheet.ReadPixels(new Rect(0, 0, W, H), f * W, (views.Length - 1 - v) * H); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    }
                sheet.Apply(); File.WriteAllBytes(outPng, sheet.EncodeToPNG());
            }
            finally { pru.Cleanup(); UnityEngine.Object.DestroyImmediate(sheet); }
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - build clips + sheets")]
        static void RobotBuildClips()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab);
            var wrap = UnityEngine.Object.Instantiate(prefab); var go = wrap.transform.Find("Model").gameObject;
            go.transform.SetParent(null, false); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
            UnityEngine.Object.DestroyImmediate(wrap);
            var log = new StringBuilder("[T07 robot anim]\n");
            try
            {
                var r = new Rig(go);
                var clips = new Dictionary<string, AnimationClip>
                {
                    ["Idle"] = RWrite(r, "R_Idle", 4f, true, u => RIdle(r, u)),
                    ["CombatIdle"] = RWrite(r, "R_CombatIdle", 2.4f, true, u => RCombatIdle(r, u)),
                    ["PatrolWalk"] = RWrite(r, "R_PatrolWalk", 1.4f, true, u => RWalk(r, u, false)),
                    ["CombatAdvance"] = RWrite(r, "R_CombatAdvance", 1.4f, true, u => RWalk(r, u, true)),
                    ["CannonAttack"] = RWrite(r, "R_CannonAttack", .7f, false, u => RAttack(r, u * .7f)),
                    ["HitReaction"] = RWrite(r, "R_HitReaction", .6f, false, u => RHit(r, u * .6f)),
                };
                foreach (var kv in clips) log.AppendLine($"  {kv.Value.name} len={kv.Value.length:0.###} curves={AnimationUtility.GetCurveBindings(kv.Value).Length}");

                // controller: copy of the F8 controller (same graph, parameters and mask), motions remapped
                if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(RobotController))
                    if (!AssetDatabase.CopyAsset(Controller, RobotController)) throw new InvalidOperationException("Controller copy failed");
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(RobotController);
                var swaps = new List<string>();
                Motion Map(Motion m, string owner)
                {
                    if (m is AnimationClip c)
                    {
                        string key = c.name switch
                        {
                            "Mech_Idle" or "R_Idle" => "Idle",
                            "Mech_CombatIdle" or "R_CombatIdle" => "CombatIdle",
                            "Mech_Walk" or "R_PatrolWalk" or "R_CombatAdvance" => owner == "AlertedTree" ? "CombatAdvance" : "PatrolWalk",
                            "Mech_CannonAttack" or "R_CannonAttack" => "CannonAttack",
                            "Mech_HitReaction" or "R_HitReaction" => "HitReaction",
                            _ => null
                        };
                        if (key != null) { swaps.Add($"{owner}: {c.name} -> {clips[key].name}"); return clips[key]; }
                        return m;
                    }
                    if (m is BlendTree bt)
                    {
                        var ch = bt.children; for (int i = 0; i < ch.Length; i++) ch[i].motion = Map(ch[i].motion, bt.name); bt.children = ch; EditorUtility.SetDirty(bt);
                    }
                    return m;
                }
                var layers = ctrl.layers;
                foreach (var layer in layers)
                    foreach (var st in layer.stateMachine.states) { st.state.motion = Map(st.state.motion, st.state.name); EditorUtility.SetDirty(st.state); }
                ctrl.layers = layers; EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                foreach (var s in swaps) log.AppendLine("  swap " + s);

                // before / after sheets with the same renderer
                Directory.CreateDirectory(RobotReports);
                string Old(string n) => $"{ClipDir}/{n}.anim"; string New(string n) => $"{RobotClipDir}/{n}.anim";
                var pairs = new (string tag, string before, string after, string baseBefore, string baseAfter)[]
                {
                    ("idle", Old("Mech_Idle"), New("R_Idle"), null, null),
                    ("combat_idle", Old("Mech_CombatIdle"), New("R_CombatIdle"), null, null),
                    ("patrol_walk", Old("Mech_Walk"), New("R_PatrolWalk"), null, null),
                    ("chase_walk", Old("Mech_Walk"), New("R_CombatAdvance"), null, null),
                    ("attack", Old("Mech_CannonAttack"), New("R_CannonAttack"), Old("Mech_CombatIdle"), New("R_CombatIdle")),
                    ("hit", Old("Mech_HitReaction"), New("R_HitReaction"), null, null),
                };
                foreach (var pr in pairs)
                {
                    RSheet(pr.before, pr.baseBefore, $"{RobotReports}/before_{pr.tag}.png", 12);
                    RSheet(pr.after, pr.baseAfter, $"{RobotReports}/after_{pr.tag}.png", 12);
                }
                log.AppendLine($"controller={RobotController} sheets={RobotReports}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                Directory.CreateDirectory(RobotReports);
                File.WriteAllText(RobotReports + "/build-log.txt", log.ToString());
                Debug.Log(log.ToString());
            }
        }

        static void RSheet(string clipPath, string basePath, string outPng, int frames,
            string prefabPath = null, Func<GameObject, GameObject> modelOf = null, Action<GameObject> afterSample = null, float height = 1.08f)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            var baseClip = basePath != null ? AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath) : null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath ?? VisualPrefab);
            if (!clip || !prefab) throw new InvalidOperationException("Missing clip or prefab for sheet: " + clipPath);
            var pru = new PreviewRenderUtility(); const int W = 256, H = 320;
            var sheet = new Texture2D(W * frames, H * 2, TextureFormat.RGB24, false);
            try
            {
                var go = pru.InstantiatePrefabInScene(prefab);
                var model = modelOf != null ? modelOf(go) : go.transform.Find("Model").gameObject;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * .3f * height / 1.08f;
                var fm = new Material(Shader.Find("Universal Render Pipeline/Unlit")); fm.color = new Color(.18f, .2f, .22f); floor.GetComponent<Renderer>().sharedMaterial = fm;
                pru.AddSingleGO(floor);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
                pru.camera.fieldOfView = 26; pru.camera.nearClipPlane = .01f; pru.camera.farClipPlane = 50;
                pru.camera.clearFlags = CameraClearFlags.SolidColor; pru.camera.backgroundColor = new Color(.42f, .45f, .5f);
                pru.lights[0].intensity = 1.3f; pru.lights[0].transform.rotation = Quaternion.Euler(40, -30, 0); pru.lights[1].intensity = .7f;
                pru.ambientColor = new Color(.35f, .35f, .4f);
                for (int view = 0; view < 2; view++)
                    for (int f = 0; f < frames; f++)
                    {
                        float t = clip.length * f / frames;
                        if (baseClip) baseClip.SampleAnimation(model, 0);
                        clip.SampleAnimation(model, t);
                        afterSample?.Invoke(go);
                        float k = height / 1.08f;
                        Vector3 c = new Vector3(0, .5f * k, 0);
                        var dir = view == 0 ? Vector3.right : (Vector3.forward + Vector3.right * .35f).normalized;
                        pru.camera.transform.position = c + dir * 3.2f * k + Vector3.up * .15f * k; pru.camera.transform.LookAt(c);
                        pru.BeginStaticPreview(new Rect(0, 0, W, H));
                        pru.Render(true);
                        var tex = pru.EndStaticPreview();
                        var rt = RenderTexture.GetTemporary(W, H, 0); Graphics.Blit(tex, rt);
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        sheet.ReadPixels(new Rect(0, 0, W, H), f * W, (1 - view) * H); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    }
                sheet.Apply();
                File.WriteAllBytes(outPng, sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(fm);
            }
            finally { pru.Cleanup(); UnityEngine.Object.DestroyImmediate(sheet); }
        }
    }

    // File queue for the open Editor: one fixed operation per request file, no arbitrary code.
    [InitializeOnLoad]
    static class T07MechRobotAnimQueue
    {
        static T07MechRobotAnimQueue() { EditorApplication.update += Tick; }
        static void Tick()
        {
            VisualPassMech.RobotManualTick();
            var playTag = SessionState.GetString(VisualPassMech.RobotPlayKey, "");
            if (playTag != "")
            {
                if (EditorApplication.isPlaying) { VisualPassMech.RobotCaptureTick(playTag); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (SessionState.GetBool(VisualPassMech.RobotPlayKey + ".done", false)) VisualPassMech.RobotPlayFinish("ok play-" + playTag.ToLowerInvariant());
                else if (VisualPassMech.RobotPlayTimedOut()) VisualPassMech.RobotPlayFinish("timeout play-" + playTag.ToLowerInvariant());
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(VisualPassMech.RobotQueue)) return;
            var op = File.ReadAllText(VisualPassMech.RobotQueue).Trim(); File.Delete(VisualPassMech.RobotQueue);
            Directory.CreateDirectory(VisualPassMech.RobotReports);
            try { VisualPassMech.RobotRun(op); File.WriteAllText(VisualPassMech.RobotReports + "/status.txt", $"{(op.StartsWith("play") ? "started" : "ok")} {op} {DateTime.Now:O}"); }
            catch (Exception e) { File.WriteAllText(VisualPassMech.RobotReports + "/status.txt", e.ToString()); Debug.LogException(e); }
        }
    }
}
