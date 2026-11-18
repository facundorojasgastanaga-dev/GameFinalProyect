using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Robot motion pass for the campaign SawBot (18/11/2026), same standard (R1-R6) as the T-07 passes,
    // adapted to a wheeled body with two saw arms. Writes its own clips and a copied controller; never
    // edits the SawBot clips/controller, the campaign prefabs or any existing scene. Death clips and the
    // code-driven wheels/chains are left alone.
    public static partial class VisualPassMech
    {
        public const string SawSource = "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab";
        public const string SawOldController = "Assets/IronRequiem/Art/Enemies/SawBotSawPolish/SawBot_SawPolish.controller";
        public const string SawRobotDir = "Assets/IronRequiem/Art/Enemies/SawBotRobotAnim";
        public const string SawRobotClipDir = SawRobotDir + "/Clips";
        public const string SawRobotController = SawRobotDir + "/SawBotRobot.controller";
        public const string SawRobotVariant = "Assets/IronRequiem/Prefabs/VisualPass/SawBotRobot/SawBot_RobotAnim_Test.prefab";
        public const string SawRobotReports = "Tools/IronRequiem/Reports/SawBotRobotAnim";
        public const string SawRobotShowcase = "Assets/IronRequiem/Scenes/SawBotRobotAnimShowcase.unity";
        static readonly Dictionary<string, string> SawOld = new()
        {
            ["Idle"] = "Assets/IronRequiem/Art/Enemies/SawBot/SawBot_Idle.anim",
            ["Drive"] = "Assets/IronRequiem/Art/Enemies/SawBot/SawBot_Drive.anim",
            ["Prepare"] = "Assets/IronRequiem/Art/Enemies/SawBotSustain/SawBot_ContactPrepare.anim",
            ["Hold"] = "Assets/IronRequiem/Art/Enemies/SawBotSustain/SawBot_SawHold.anim",
            ["Hit"] = "Assets/IronRequiem/Art/Enemies/SawBot/SawBot_Hit.anim",
        };

        internal static void SawRun(string op)
        {
            if (op == "inspect") SawInspect();
            else if (op == "clips") SawBuildClips();
            else if (op == "labs") RobotBuildLabsFor(SawSource, SawRobotVariant, SawRobotController, "SawBot_SawPolish", "Saw", SawRobotReports, false);
            else if (op == "play-before") RobotPlay("SawBefore");
            else if (op == "play-after") RobotPlay("SawAfter");
            else if (op == "showcase") SawBuildShowcase();
            else if (op == "showcase-shot") RobotShowcaseShotFor(SawRobotShowcase, SawRobotReports);
            else if (op == "showcase-check") { checkSamples = null; RobotPlayScene(SawRobotShowcase, "SawShowcaseCheck"); }
            else throw new InvalidOperationException("Unknown fixed SawBot operation: " + op);
        }

        const string SawMenu = "Tools/Iron Requiem/Visual Pass/SawBot robot anim (test) - ";
        [MenuItem(SawMenu + "build clips + sheets")] static void SawClipsMenu() => T07Menu_(SawBuildClips);
        [MenuItem(SawMenu + "build variant + labs")] static void SawLabsMenu() => T07Menu_(() => SawRun("labs"));
        [MenuItem(SawMenu + "Play lab AFTER (manual)")] static void SawPlayAfter() => RobotPlayScene(RobotLabPath("SawAfter"), "SawAfter");
        [MenuItem(SawMenu + "Play lab BEFORE (manual)")] static void SawPlayBefore() => RobotPlayScene(RobotLabPath("SawBefore"), "SawBefore");
        [MenuItem(SawMenu + "Build SHOWCASE scene")] static void SawShowcaseMenu() => T07Menu_(SawBuildShowcase);
        [MenuItem(SawMenu + "Play SHOWCASE (all clips)")] static void SawPlayShowcase() => RobotPlayScene(SawRobotShowcase, "SawShowcase");

        static Animator SawAnimator(GameObject inst)
        {
            var a = inst.GetComponentsInChildren<Animator>(true).FirstOrDefault(x => x.runtimeAnimatorController && x.runtimeAnimatorController.name.StartsWith("SawBot"))
                    ?? inst.GetComponentsInChildren<Animator>(true).FirstOrDefault();
            if (!a) throw new InvalidOperationException("No Animator under " + inst.name);
            return a;
        }

        // ---------------------------------------------------------------- tiny rig for the saw body
        sealed class SawRig
        {
            public Transform root, body, uaL, faL, uaR, faR;
            public readonly List<Transform> bones = new();
            public readonly Dictionary<Transform, Quaternion> restWorld = new(), restLocal = new();
            public readonly Dictionary<Transform, Vector3> restLocalPos = new();
            public Vector3 up = Vector3.up, fwd = Vector3.forward, right = Vector3.right;
            public SawRig(GameObject animGo)
            {
                root = animGo.transform;
                Transform F(string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n && t.parent && (n == "Body" ? t.parent.name == "Rig" : true))
                    ?? throw new InvalidOperationException("SawBot bone missing: " + n);
                body = F("Body"); uaL = F("UpperArm_L"); faL = F("Forearm_L"); uaR = F("UpperArm_R"); faR = F("Forearm_R");
                foreach (var t in new[] { body, uaL, faL, uaR, faR }) { bones.Add(t); restWorld[t] = t.rotation; restLocal[t] = t.localRotation; restLocalPos[t] = t.localPosition; }
                // facing of the robot itself (the Presentation child is turned 180 deg inside the prefab)
                var facing = root.root; up = facing.up; fwd = facing.forward; right = facing.right;
            }
        }

        // Pose: per-bone deltas about the robot's own axes (pitch about right, yaw about up, roll about forward),
        // inherited down the chain like the mech solver, plus a body offset in robot space.
        sealed class SawPose
        {
            public readonly Dictionary<Transform, Quaternion> d = new(); public Vector3 bodyOffset;
            // key pose taken from an existing clip (local rotations / body position); null = rest pose
            public Dictionary<Transform, Quaternion> baseLocal; public Vector3? baseBodyPos;
            public void D(SawRig r, Transform t, float pitch, float yaw = 0, float roll = 0)
            {
                var q = Quaternion.AngleAxis(yaw, r.up) * Quaternion.AngleAxis(pitch, r.right) * Quaternion.AngleAxis(roll, r.fwd);
                d[t] = d.TryGetValue(t, out var o) ? q * o : q;
            }
        }

        static Dictionary<Transform, Quaternion> SawLocal(SawRig r, SawPose p)
        {
            var world = new Dictionary<Transform, Quaternion>(); var local = new Dictionary<Transform, Quaternion>();
            var baseWorld = new Dictionary<Transform, Quaternion>();
            foreach (var t in r.bones)
            {
                bool hasParent = world.ContainsKey(t.parent);
                Quaternion parentNow = hasParent ? world[t.parent] : t.parent.rotation;
                Quaternion parentBase = hasParent ? baseWorld[t.parent] : t.parent.rotation;
                var bl = p.baseLocal != null && p.baseLocal.TryGetValue(t, out var b) ? b : r.restLocal[t];
                baseWorld[t] = parentBase * bl;
                var P = parentNow * Quaternion.Inverse(parentBase);
                var w = P * baseWorld[t];
                if (p.d.TryGetValue(t, out var dq)) w = P * dq * Quaternion.Inverse(P) * w;
                world[t] = w; local[t] = Quaternion.Inverse(parentNow) * w;
            }
            return local;
        }

        // Local rotations/body position of an existing clip at time t, sampled on the proxy and then reset.
        static (Dictionary<Transform, Quaternion> local, Vector3 body) SawSample(SawRig r, AnimationClip clip, float t)
        {
            clip.SampleAnimation(r.root.gameObject, Mathf.Clamp(t, 0, clip.length));
            var local = r.bones.ToDictionary(b => b, b => b.localRotation); var body = r.body.localPosition;
            foreach (var b in r.bones) { b.localRotation = r.restLocal[b]; b.localPosition = r.restLocalPos[b]; }
            return (local, body);
        }
        static SawPose SawFrom(SawRig r, AnimationClip clip, float t)
        {
            var s = SawSample(r, clip, t); return new SawPose { baseLocal = s.local, baseBodyPos = s.body };
        }
        static SawPose SawBlend(SawRig r, AnimationClip a, float ta, AnimationClip b, float tb, float w)
        {
            var sa = SawSample(r, a, ta); var sb = SawSample(r, b, tb);
            return new SawPose
            {
                baseLocal = r.bones.ToDictionary(x => x, x => Quaternion.SlerpUnclamped(sa.local[x], sb.local[x], w)),
                baseBodyPos = Vector3.LerpUnclamped(sa.body, sb.body, w)
            };
        }

        const float SawFps = 60;
        static AnimationClip SawWrite(SawRig r, string name, float seconds, bool loop, Func<float, SawPose> at)
        {
            EnsureFolder(SawRobotClipDir);
            var path = $"{SawRobotClipDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.frameRate = SawFps; // 60: the 0.25 s prepare clip lands on a frame
            int frames = Mathf.Max(2, Mathf.RoundToInt(seconds * SawFps)) + 1;
            var rot = r.bones.ToDictionary(t => t, _ => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
            var pos = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var prev = new Dictionary<Transform, Quaternion>(r.restLocal);
            for (int f = 0; f < frames; f++)
            {
                float u = (float)f / (frames - 1); if (loop && f == frames - 1) u = 0;
                var pose = at(u); var local = SawLocal(r, pose); float time = f / SawFps;
                foreach (var t in r.bones)
                {
                    var q = local[t]; if (Quaternion.Dot(prev[t], q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); prev[t] = q;
                    var c = rot[t]; c[0].AddKey(time, q.x); c[1].AddKey(time, q.y); c[2].AddKey(time, q.z); c[3].AddKey(time, q.w);
                }
                var lp = (pose.baseBodyPos ?? r.restLocalPos[r.body]) + r.body.parent.InverseTransformVector(pose.bodyOffset);
                for (int a = 0; a < 3; a++) pos[a].AddKey(time, lp[a]);
            }
            void Lin(AnimationCurve c) { for (int i = 0; i < c.length; i++) { AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear); } }
            foreach (var t in r.bones)
            {
                var bpath = AnimationUtility.CalculateTransformPath(t, r.root);
                for (int a = 0; a < 4; a++) { Lin(rot[t][a]); AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bpath, typeof(Transform), "m_LocalRotation." + "xyzw"[a]), rot[t][a]); }
            }
            var bodyPath = AnimationUtility.CalculateTransformPath(r.body, r.root);
            for (int a = 0; a < 3; a++) { Lin(pos[a]); AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bodyPath, typeof(Transform), "m_LocalPosition." + "xyz"[a]), pos[a]); }
            var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, s);
            clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip);
            return clip;
        }

        static GameObject SawProxy(UnityEngine.SceneManagement.Scene scene, out SawRig rig, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SawSource);
            if (!prefab) throw new InvalidOperationException("Missing " + SawSource);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anim = SawAnimator(inst);
            rig = new SawRig(anim.gameObject);
            log.AppendLine($"SawBot animator={Path(anim.transform, inst.transform)} ctrl={anim.runtimeAnimatorController?.name} rootRot={anim.transform.rotation.eulerAngles.ToString("F1")} scale={anim.transform.lossyScale.ToString("F2")}");
            foreach (var t in anim.GetComponentsInChildren<Transform>(true))
            {
                int depth = 0; for (var x = t; x && x != anim.transform; x = x.parent) depth++;
                if (depth > 6) continue;
                log.AppendLine($"  {new string(' ', depth * 2)}{t.name} pos={t.position.ToString("F3")} fwd={t.forward.ToString("F2")} up={t.up.ToString("F2")} right={t.right.ToString("F2")} lscale={t.localScale.ToString("F2")}");
            }
            return inst;
        }

        static void SawInspect()
        {
            RobotRequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var log = new StringBuilder("[SawBot robot anim inspect]\n");
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SawProxy(scene, out var r, log);
                Directory.CreateDirectory(SawRobotReports);
                foreach (var kv in SawOld) RSheet(kv.Value, kv.Key == "Hit" ? SawOld["Idle"] : null, $"{SawRobotReports}/before_{kv.Key.ToLowerInvariant()}.png", 12, SawSource, go => SawAnimator(go).gameObject, null, 1.1f);
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory(SawRobotReports);
                File.WriteAllText(SawRobotReports + "/inspect-log.txt", log.ToString());
            }
        }

        // ---------------------------------------------------------------- poses
        // Key poses come from the existing clips (so the blade contact geometry that SawBotCombat's damage uses
        // is unchanged at the moment of contact); the robot timing and the motion layered on top are new.
        // Axes: +pitch tips the body forward / swings a hanging arm back; +roll swings the right arm outward.
        static AnimationClip sawOldIdle, sawOldDrive, sawOldPrepare, sawOldHold;
        static float SawSide(SawRig r, Transform t) => t == r.uaR || t == r.faR ? 1 : -1;

        static SawPose SawIdle(SawRig r, float u) // 2 s loop
        {
            const float T = 2f; float t = u * T;
            var p = SawFrom(r, sawOldIdle, 0);
            float hum = RHum(t, T, 7, 0) * .3f + RHum(t, T, 13, .2f) * .15f;          // R3: servo hum, no breathing
            float scan = RTrack(t, .14f, 0, 0, .35f, -9, 1.0f, 0, 1.3f, 7, 1.75f, 0);   // R2: sensor dome scans in snaps
            float settle = RTrack(t, .12f, 0, 0, .65f, 1, 1.55f, 0);                  // discrete weight re-settle
            p.bodyOffset = r.up * (-.006f * settle);
            p.D(r, r.body, .8f * settle + hum, scan, 0);
            float twL = RTrack(t, .1f, 0, 0, .9f, -6, 1.2f, 0), twR = RTrack(t, .1f, 0, 0, 1.6f, -6, 1.9f, 0);
            p.D(r, r.uaL, twL, 0, 0); p.D(r, r.uaR, twR, 0, 0);     // arms twitch-check one at a time
            p.D(r, r.faL, hum * .6f, 0, 0); p.D(r, r.faR, -hum * .6f, 0, 0);
            return p;
        }

        static SawPose SawDrive(SawRig r, float u) // 1 s loop at DriveSpeed
        {
            const float T = 1f; float t = u * T;
            var p = SawFrom(r, sawOldDrive, t);
            float chatter = RHum(t, T, 17, 0) * .5f + RHum(t, T, 23, .3f) * .3f;       // drivetrain rattle
            float bump = RKick(Mathf.Repeat(t, .5f), .03f, 4f, 9f);                   // two floor seams per cycle (R4)
            float lag = RKick(Mathf.Repeat(t - .06f, .5f), .04f, 3f, 7f);             // arms answer the bump late
            p.bodyOffset = r.up * (-.008f * bump);
            p.D(r, r.body, 6 + 2.5f * bump + chatter, 0, 0);                         // leans into the drive
            foreach (var ua in new[] { r.uaL, r.uaR }) p.D(r, ua, -10 + 5 * lag, 0, SawSide(r, ua) * 3);
            foreach (var fa in new[] { r.faL, r.faR }) p.D(r, fa, -8 + 3 * lag + chatter * .5f, 0, 0);
            return p;
        }

        static SawPose SawPrepare(SawRig r, float t) // 0.25 s (played at AttackSpeed = 0.25 / preparation)
        {
            const float T = .25f; float u = t / T;
            // 0-35 %: cock the saws back and up (servo), 35-50 %: hold, 50-100 %: snap to contact with overshoot
            float wind = RServo(u / .35f) * (u < .5f ? 1 : 1 - RServo((u - .5f) / .2f));
            float strike = u < .5f ? 0 : RServo((u - .5f) / .5f);
            float over = strike > 1 ? strike - 1 : 0;
            var p = SawBlend(r, sawOldPrepare, 0, sawOldHold, 0, Mathf.Min(1, strike));
            p.D(r, r.body, -7 * wind + 60 * over, 0, 0);               // rocks back on the wind-up, tips in on the snap
            foreach (var ua in new[] { r.uaL, r.uaR }) p.D(r, ua, 22 * wind - 30 * over, 0, SawSide(r, ua) * 10 * wind);
            foreach (var fa in new[] { r.faL, r.faR }) p.D(r, fa, -25 * wind - 20 * over, 0, 0);
            return p;
        }

        static SawPose SawHold(SawRig r, float u) // 0.4 s loop while the saws bite
        {
            const float T = .4f; float t = u * T;
            var p = SawFrom(r, sawOldHold, t);
            float bite = RHum(t, T, 25, 0), bite2 = RHum(t, T, 25, .5f);             // chains biting (opposite phase)
            float push = RKick(Mathf.Repeat(t, .2f), .025f, 5f, 12f);                 // drive pushes in, twice per loop
            p.bodyOffset = r.fwd * (.006f * push);
            p.D(r, r.body, 2 * push + .4f * bite, 0, .5f * bite2);
            p.D(r, r.faL, 1.6f * bite, 0, 0); p.D(r, r.faR, 1.6f * bite2, 0, 0);
            p.D(r, r.uaL, -.8f * push, 0, 0); p.D(r, r.uaR, -.8f * push, 0, 0);
            return p;
        }

        static SawPose SawHit(SawRig r, float t) // 0.32 s, additive layer: frame 0 is the reference (zero delta)
        {
            float tq = t > .06f && t < .22f ? .06f + Mathf.Floor((t - .06f) / .05f) * .05f : t; // R6: servo stutter
            float k = t <= 0 ? 0 : RKick(tq, .035f, 2.4f, 6f) * (1 - Smooth(Mathf.Clamp01((t - .24f) / .08f)));
            float cal = RTrack(t, .03f, 0, 0, .22f, 6, .26f, -4, .3f, 0);            // sensor re-calibration twitch
            var p = new SawPose();
            p.bodyOffset = -r.fwd * (.02f * k);
            p.D(r, r.body, -13 * k, 8 * k + cal, 5 * k);                             // knocked back and twisted
            foreach (var ua in new[] { r.uaL, r.uaR }) p.D(r, ua, 10 * k, 0, SawSide(r, ua) * 14 * k);
            foreach (var fa in new[] { r.faL, r.faR }) p.D(r, fa, 12 * k, 0, 0);
            return p;
        }

        static void SawBuildClips()
        {
            RobotRequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var log = new StringBuilder("[SawBot robot anim]\n");
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SawProxy(scene, out var r, new StringBuilder());
                AnimationClip Old(string k) => AssetDatabase.LoadAssetAtPath<AnimationClip>(SawOld[k]) ?? throw new InvalidOperationException("Missing " + SawOld[k]);
                sawOldIdle = Old("Idle"); sawOldDrive = Old("Drive"); sawOldPrepare = Old("Prepare"); sawOldHold = Old("Hold");
                var clips = new Dictionary<string, AnimationClip>
                {
                    ["Idle"] = SawWrite(r, "R_SawIdle", 2f, true, u => SawIdle(r, u)),
                    ["Drive"] = SawWrite(r, "R_SawDrive", 1f, true, u => SawDrive(r, u)),
                    ["Prepare"] = SawWrite(r, "R_SawContactPrepare", .25f, false, u => SawPrepare(r, u * .25f)),
                    ["Hold"] = SawWrite(r, "R_SawHold", .4f, true, u => SawHold(r, u)),
                    ["Hit"] = SawWrite(r, "R_SawHit", .32f, false, u => SawHit(r, u * .32f)),
                };
                foreach (var kv in clips) log.AppendLine($"  {kv.Value.name} len={kv.Value.length:0.###} curves={AnimationUtility.GetCurveBindings(kv.Value).Length}");

                if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(SawRobotController))
                    if (!AssetDatabase.CopyAsset(SawOldController, SawRobotController)) throw new InvalidOperationException("Controller copy failed");
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(SawRobotController);
                var byName = new Dictionary<string, string>
                {
                    ["SawBot_Idle"] = "Idle", ["R_SawIdle"] = "Idle", ["SawBot_Drive"] = "Drive", ["R_SawDrive"] = "Drive",
                    ["SawBot_ContactPrepare"] = "Prepare", ["R_SawContactPrepare"] = "Prepare",
                    ["SawBot_SawHold"] = "Hold", ["R_SawHold"] = "Hold", ["SawBot_Hit"] = "Hit", ["R_SawHit"] = "Hit",
                };
                var swaps = new List<string>();
                Motion Map(Motion m, string owner)
                {
                    if (m is AnimationClip c) { if (byName.TryGetValue(c.name, out var k)) { swaps.Add($"{owner}: {c.name} -> {clips[k].name}"); return clips[k]; } return m; }
                    if (m is BlendTree bt) { var ch = bt.children; for (int i = 0; i < ch.Length; i++) ch[i].motion = Map(ch[i].motion, bt.name); bt.children = ch; EditorUtility.SetDirty(bt); }
                    return m;
                }
                var layers = ctrl.layers;
                foreach (var layer in layers)
                    foreach (var st in layer.stateMachine.states) { st.state.motion = Map(st.state.motion, st.state.name); EditorUtility.SetDirty(st.state); }
                ctrl.layers = layers; EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
                foreach (var s in swaps) log.AppendLine("  swap " + s);

                Directory.CreateDirectory(SawRobotReports);
                foreach (var kv in SawOld)
                {
                    string tag = kv.Key.ToLowerInvariant(); string nw = AssetDatabase.GetAssetPath(clips[kv.Key]);
                    RSheet(kv.Value, kv.Key == "Hit" ? SawOld["Idle"] : null, $"{SawRobotReports}/before_{tag}.png", 12, SawSource, go => SawAnimator(go).gameObject, null, 1.1f);
                    RSheet(nw, kv.Key == "Hit" ? AssetDatabase.GetAssetPath(clips["Idle"]) : null, $"{SawRobotReports}/after_{tag}.png", 12, SawSource, go => SawAnimator(go).gameObject, null, 1.1f);
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory(SawRobotReports);
                File.WriteAllText(SawRobotReports + "/build-log.txt", log.ToString());
            }
        }

        // ---------------------------------------------------------------- showcase
        static void SawBuildShowcase()
        {
            string N(string n) => $"{SawRobotClipDir}/{n}.anim";
            RobotBuildShowcaseFor(SawRobotShowcase, SawRobotReports, "DELANTE: NUEVO (robot)      DETRAS: ANTES (actual)", scene =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SawSource);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var anim = SawAnimator(inst);
                StripToVisual(inst, anim);
                return inst;
            }, new[]
            {
                ("REPOSO", N("R_SawIdle"), SawOld["Idle"]),
                ("MARCHA", N("R_SawDrive"), SawOld["Drive"]),
                ("PREPARAR\n(x0.55)", N("R_SawContactPrepare"), SawOld["Prepare"]),
                ("SERRAR", N("R_SawHold"), SawOld["Hold"]),
                ("GOLPE", N("R_SawHit"), SawOld["Hit"]),
            }, 1.6f, new[] { 1f, 1f, .55f, 1f, 1f }); // dome top ~1.5 m; skinned bounds are loose (2.36 m)
        }
    }
}
