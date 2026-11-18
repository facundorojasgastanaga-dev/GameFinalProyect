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
    // Robot motion pass for the campaign T-07 (18/11/2026), same standard (R1-R6) as the T-07 Mech pass.
    // The campaign T-07 uses the T07_SharedRig skeleton (17 bones); its bones are renamed on a throw-away
    // copy so the mech pose solver can drive it, and the clips are written back with the real bone paths.
    // Never edits the T07Animated clips/controller, the campaign prefabs or any existing scene.
    //
    // What the game shows of these clips: the feet are placed in world space by T07FootPlacement (two-bone
    // IK), and in combat T07CannonSocket re-aims the cannon arm, so the visible change is the pelvis
    // (height, sway, jolt), torso, sensor head and the free arm, plus shot/hit while the arm is yielded.
    public static partial class VisualPassMech
    {
        public const string T07Source = "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";
        public const string T07OldDir = "Assets/IronRequiem/Art/Enemies/T07Animated";
        public const string T07OldController = T07OldDir + "/T07_Locomotion.controller";
        public const string T07RobotDir = "Assets/IronRequiem/Art/Enemies/T07RobotAnim";
        public const string T07RobotClipDir = T07RobotDir + "/Clips";
        public const string T07RobotController = T07RobotDir + "/T07Robot_Locomotion.controller";
        public const string T07RobotVariant = "Assets/IronRequiem/Prefabs/VisualPass/T07Robot/T07_RobotAnim_Test.prefab";
        public const string T07RobotReports = "Tools/IronRequiem/Reports/T07RobotAnim";
        public const string T07RobotShowcase = "Assets/IronRequiem/Scenes/T07RobotAnimShowcase.unity";

        static readonly Dictionary<string, string> T07ToRig = new()
        {
            // T07_SharedRig names its sides from the viewer: the "_L" bones sit on the robot's own right (+X
            // while it faces +Z) and carry the cannon (T07CannonSocket socket = Forearm_L), so _L -> Right_*.
            ["Thigh_L"] = "Right_UpperLeg", ["Shin_L"] = "Right_LowerLeg", ["Foot_L"] = "Right_Foot",
            ["Thigh_R"] = "Left_UpperLeg", ["Shin_R"] = "Left_LowerLeg", ["Foot_R"] = "Left_Foot",
            ["UpperArm_L"] = "Right_UpperArm", ["Forearm_L"] = "Right_LowerArm", ["Hand_L"] = "Right_Hand",
            ["UpperArm_R"] = "Left_UpperArm", ["Forearm_R"] = "Left_LowerArm", ["Hand_R"] = "Left_Hand",
        };

        internal static void T07Run(string op)
        {
            if (op == "clips") T07BuildClips();
            else if (op == "labs") RobotBuildLabsFor(T07Source, T07RobotVariant, T07RobotController, "T07_Locomotion", "T07", T07RobotReports);
            else if (op == "play-before") RobotPlay("T07Before");
            else if (op == "play-after") RobotPlay("T07After");
            else if (op == "inspect") T07Inspect();
            else if (op == "showcase") T07BuildShowcase();
            else if (op == "showcase-shot") RobotShowcaseShotFor(T07RobotShowcase, T07RobotReports);
            else if (op == "showcase-check") T07ShowcaseCheck();
            else throw new InvalidOperationException("Unknown fixed T-07 operation: " + op);
        }

        // ---------------------------------------------------------------- menus
        const string T07Menu = "Tools/Iron Requiem/Visual Pass/T07 robot anim (test) - ";
        [MenuItem(T07Menu + "build clips + sheets")] static void T07ClipsMenu() => T07Menu_(T07BuildClips);
        [MenuItem(T07Menu + "build variant + labs")] static void T07LabsMenu() => T07Menu_(() => T07Run("labs"));
        [MenuItem(T07Menu + "Play lab AFTER (manual)")] static void T07PlayAfter() => RobotPlayScene(RobotLabPath("T07After"), "T07After");
        [MenuItem(T07Menu + "Play lab BEFORE (manual)")] static void T07PlayBefore() => RobotPlayScene(RobotLabPath("T07Before"), "T07Before");
        [MenuItem(T07Menu + "Build SHOWCASE scene")] static void T07ShowcaseMenu() => T07Menu_(T07BuildShowcase);
        [MenuItem(T07Menu + "Play SHOWCASE (all clips)")] static void T07PlayShowcase() => RobotPlayScene(T07RobotShowcase, "T07Showcase");
        static void T07Menu_(Action a) { try { a(); } catch (Exception e) { EditorUtility.DisplayDialog("T07 robot anim", e.Message, "OK"); } }

        // ---------------------------------------------------------------- proxy rig
        // Unpacked copy of the campaign T-07 in a temporary scene; its animated hierarchy gets the mech's
        // bone names, plus leaf stand-ins for bones the T-07 lacks (toes, neck, upper chest, shoulder).
        sealed class T07Proxy
        {
            public GameObject animGo; public Rig rig; public float stride, height; public Vector3 barrelRest = Vector3.forward;
            public readonly Dictionary<Transform, string> path = new();
            public readonly HashSet<Transform> dummies = new();

            public T07Proxy(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(T07Source);
                if (!prefab) throw new InvalidOperationException("Missing " + T07Source);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var drv = root.GetComponent<T07AnimationDriver>();
                if (!drv || !drv.Animator) throw new InvalidOperationException("T07AnimationDriver/Animator missing on " + T07Source);
                animGo = drv.Animator.gameObject;
                float scale = animGo.transform.lossyScale.y;
                stride = drv.StrideDistance / scale;
                foreach (var t in animGo.GetComponentsInChildren<Transform>(true)) path[t] = AnimationUtility.CalculateTransformPath(t, animGo.transform);
                if (animGo != root) animGo.transform.SetParent(null, false);
                animGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); animGo.transform.localScale = Vector3.one;

                // the skeleton is the "Root" bone under T07Rig; other objects reuse bone names (e.g. the
                // ArticulationGuides_NoRig/Head helper), so they are renamed on this copy and never keyed
                var skelRoot = animGo.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Root" && t.Find("Hips") && t.parent && t.parent.name == "T07Rig");
                if (!skelRoot) throw new InvalidOperationException("T07Rig/Root not found under the T-07 animator");
                for (var t = skelRoot.Find("Hips/Spine/Chest/UpperArm_L/Forearm_L"); t; t = t.parent)
                    log.AppendLine($"  chain {t.name} localScale={t.localScale.ToString("F3")} lossy={t.lossyScale.ToString("F3")} localRot={t.localEulerAngles.ToString("F1")} rot={t.eulerAngles.ToString("F1")} pos={t.position.ToString("F3")} det={Mathf.Sign(t.localToWorldMatrix.determinant)}");
                var boneNames = new HashSet<string>(T07ToRig.Keys.Concat(T07ToRig.Values).Concat(new[] { "Root", "Hips", "Spine", "Chest", "Head", "UpperChest", "Neck", "Left_Shoulder", "Left_Toes", "Right_Toes" }));
                var skel = new HashSet<Transform>(skelRoot.GetComponentsInChildren<Transform>(true).Where(t => boneNames.Contains(t.name)));
                foreach (var t in animGo.GetComponentsInChildren<Transform>(true))
                {
                    if (!skel.Contains(t) && boneNames.Contains(t.name)) { log.AppendLine($"  renamed non-bone duplicate {path[t]}"); t.name += "__notbone"; }
                    if (!skel.Contains(t)) dummies.Add(t); // never written to clips
                }
                var bones = skel.GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                foreach (var kv in T07ToRig)
                {
                    if (!bones.TryGetValue(kv.Key, out var b)) throw new InvalidOperationException("T-07 bone missing: " + kv.Key);
                    b.name = kv.Value;
                }
                foreach (var n in new[] { "Hips", "Spine", "Chest", "Head" })
                    if (!bones.ContainsKey(n)) throw new InvalidOperationException("T-07 bone missing: " + n);
                Transform B(string n) => bones.TryGetValue(n, out var t) ? t : bones[T07ToRig.First(kv => kv.Value == n).Key];
                Vector3 up = Vector3.up;
                Vector3 right = Vector3.ProjectOnPlane(B("Right_UpperLeg").position - B("Left_UpperLeg").position + B("Right_UpperArm").position - B("Left_UpperArm").position, up).normalized;
                Vector3 fwd = Vector3.Cross(right, up);
                float shin = (B("Left_Foot").position - B("Left_LowerLeg").position).magnitude;
                Transform Dummy(string name, Transform parent, Vector3 world)
                {
                    var d = new GameObject(name).transform; d.SetParent(parent, false); d.position = world; dummies.Add(d); return d;
                }
                Dummy("Left_Toes", B("Left_Foot"), B("Left_Foot").position + fwd * shin * .4f);
                Dummy("Right_Toes", B("Right_Foot"), B("Right_Foot").position + fwd * shin * .4f);
                Dummy("UpperChest", bones["Chest"], bones["Chest"].position);
                Dummy("Neck", bones["Chest"], bones["Head"].position);
                Dummy("Left_Shoulder", bones["Chest"], B("Left_UpperArm").position);
                rig = new Rig(animGo);
                var wcRest = animGo.GetComponentInChildren<Unity.FPS.Game.WeaponController>(true);
                if (wcRest && wcRest.WeaponMuzzle) { T07FollowCannon(animGo); barrelRest = wcRest.WeaponMuzzle.forward; }
                log.AppendLine($"  cannon barrel at rest={barrelRest.ToString("F2")} (mounted across the forearm)");
                var rs = animGo.GetComponentsInChildren<SkinnedMeshRenderer>(false).Where(x => x.enabled).ToArray();
                if (rs.Length == 0) throw new InvalidOperationException("No skinned renderer under the T-07 animator");
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                height = rig.P("Head").y * 1.15f; // the bounds include tall FX/antenna pieces (bounds top logged)
                foreach (var n in new[] { "Left_UpperLeg", "Right_UpperLeg", "Left_UpperArm", "Right_UpperArm", "Right_Hand", "Head" })
                    log.AppendLine($"  bone {n} ({path[rig[n]]}) pos={rig.P(n).ToString("F3")}");
                var sock = animGo.GetComponentInChildren<T07CannonSocket>(true) ?? UnityEngine.Object.FindObjectsByType<T07CannonSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                if (sock)
                {
                    var so = new SerializedObject(sock);
                    var s = so.FindProperty("socket").objectReferenceValue as Transform; var a = so.FindProperty("aimBone").objectReferenceValue as Transform;
                    log.AppendLine($"  cannon socket={(s ? s.name : "-")} at {(s ? s.position.ToString("F3") : "-")} aimBone={(a ? a.name : "-")}");
                }
                log.AppendLine($"T-07 proxy animator={animGo.name} scale={scale:0.###} stride={drv.StrideDistance} (model {stride:0.###}) height={height:0.###} legLen={rig.legLen:0.###} hip={rig.hipHeight:0.###} ankle={rig.ankleHeight:0.###} fwd={rig.fwd} smr={rs.Length} boundsTop={bb.max.y:0.###}");
            }
        }

        // ---------------------------------------------------------------- clips + controller + sheets
        static void T07BuildClips()
        {
            RobotRequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var log = new StringBuilder("[T07 robot anim]\n");
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var px = new T07Proxy(scene, log);
                var r = px.rig;
                rStride = px.stride;
                rCannonPerp = true; rBarrelRest = px.barrelRest;
                AnimationClip W(string name, float sec, bool loop, Func<float, Pose> at) =>
                    RWrite(r, name, sec, loop, at, T07RobotClipDir, t => px.path[t], px.dummies);
                var clips = new Dictionary<string, AnimationClip>
                {
                    ["Idle"] = W("R_Idle", 4f, true, u => RIdle(r, u)),
                    ["CombatIdle"] = W("R_CombatIdle", 2.4f, true, u => RCombatIdle(r, u)),
                    ["PatrolWalk"] = W("R_PatrolWalk", 1.4f, true, u => RWalk(r, u, false)),
                    ["CombatAdvance"] = W("R_CombatAdvance", 1.4f, true, u => RWalk(r, u, true)),
                    ["CannonAttack"] = W("R_CannonAttack", .7f, false, u => RAttack(r, u * .7f)),
                    ["HitReaction"] = W("R_HitReaction", .6f, false, u => RHit(r, u * .6f)),
                };
                foreach (var kv in clips) log.AppendLine($"  {kv.Value.name} len={kv.Value.length:0.###} curves={AnimationUtility.GetCurveBindings(kv.Value).Length}");
                {
                    // measured check: solver directions vs. the clip played back on a clean copy
                    var pose = RCombatIdle(r, 0); var fk = Solve(r, pose);
                    Vector3 D(Dictionary<Transform, Vector3> pos, Transform a, Transform b) => (pos[b] - pos[a]).normalized;
                    log.AppendLine($"  solver upper={D(fk.pos, r["Right_UpperArm"], r["Right_LowerArm"]).ToString("F2")} fore={D(fk.pos, r["Right_LowerArm"], r["Right_Hand"]).ToString("F2")} hips={fk.pos[r["Hips"]].ToString("F3")}");
                    var fresh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(T07Source), scene);
                    var fm = T07Model(fresh); clips["CombatIdle"].SampleAnimation(fm, 0);
                    Transform F(string n) => fm.GetComponentsInChildren<Transform>(true).First(t => t.name == n && t.parent && (t.parent.name.Contains("Chest") || t.parent.name.Contains("Arm") || t.parent.name.Contains("Forearm") || t.parent.name == "Root" || t.parent.name == "Hips"));
                    log.AppendLine($"  played upper={(F("Forearm_L").position - F("UpperArm_L").position).normalized.ToString("F2")} fore={(F("Hand_L").position - F("Forearm_L").position).normalized.ToString("F2")} hips={F("Hips").position.ToString("F3")}");
                    var muzzle = fresh.GetComponentInChildren<EnemyMuzzleAnchor>(true);
                    var wc = fresh.GetComponentInChildren<Unity.FPS.Game.WeaponController>(true);
                    var mouthT = wc && wc.WeaponMuzzle ? wc.WeaponMuzzle : muzzle ? muzzle.transform : null;
                    void Barrel(string tag)
                    {
                        T07FollowCannon(fresh);
                        var fore = (F("Hand_L").position - F("Forearm_L").position).normalized;
                        log.AppendLine($"  {tag}: forearm={fore.ToString("F2")} mouth={(mouthT ? mouthT.name : "-")} fwd={(mouthT ? mouthT.forward.ToString("F2") : "-")} mouthFromElbow={(mouthT ? (mouthT.position - F("Forearm_L").position).ToString("F2") : "-")}");
                    }
                    var restClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{T07OldDir}/CombatIdle.anim");
                    Barrel("new R_CombatIdle");
                    UnityEngine.Object.DestroyImmediate(fresh);
                    fresh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(T07Source), scene);
                    fm = T07Model(fresh);
                    muzzle = fresh.GetComponentInChildren<EnemyMuzzleAnchor>(true); wc = fresh.GetComponentInChildren<Unity.FPS.Game.WeaponController>(true);
                    mouthT = wc && wc.WeaponMuzzle ? wc.WeaponMuzzle : muzzle ? muzzle.transform : null;
                    Barrel("prefab rest");
                    restClip.SampleAnimation(fm, 0); Barrel("old CombatIdle");
                    UnityEngine.Object.DestroyImmediate(fresh);
                }

                // controller: copy of T07_Locomotion (same graph, parameters, mask); armed/alerted motions remapped
                if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(T07RobotController))
                    if (!AssetDatabase.CopyAsset(T07OldController, T07RobotController)) throw new InvalidOperationException("Controller copy failed");
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(T07RobotController);
                var swaps = new List<string>();
                Motion Map(Motion m, string owner)
                {
                    if (m is AnimationClip c)
                    {
                        if (owner == "DisarmedTree" || owner == "DisarmedLocomotion") return m; // no cannon: keep the disarmed set
                        string key = c.name switch
                        {
                            "Idle" or "R_Idle" => "Idle",
                            "CombatIdle" or "R_CombatIdle" => "CombatIdle",
                            "Walk" or "R_PatrolWalk" or "R_CombatAdvance" => owner == "AlertedTree" ? "CombatAdvance" : "PatrolWalk",
                            "CannonAttack" or "R_CannonAttack" => "CannonAttack",
                            "HitReaction" or "R_HitReaction" => "HitReaction",
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

                Directory.CreateDirectory(T07RobotReports);
                string Old(string n) => $"{T07OldDir}/{n}.anim"; string New(string n) => $"{T07RobotClipDir}/{n}.anim";
                var pairs = new (string tag, string before, string after, string baseBefore, string baseAfter)[]
                {
                    ("idle", Old("Idle"), New("R_Idle"), null, null),
                    ("combat_idle", Old("CombatIdle"), New("R_CombatIdle"), null, null),
                    ("patrol_walk", Old("Walk"), New("R_PatrolWalk"), null, null),
                    ("chase_walk", Old("Walk"), New("R_CombatAdvance"), null, null),
                    ("attack", Old("CannonAttack"), New("R_CannonAttack"), Old("CombatIdle"), New("R_CombatIdle")),
                    ("hit", Old("HitReaction"), New("R_HitReaction"), Old("CombatIdle"), null),
                };
                float h = px.height;
                foreach (var pr in pairs)
                {
                    RSheet(pr.before, pr.baseBefore, $"{T07RobotReports}/before_{pr.tag}.png", 12, T07Source, T07Model, T07FollowCannon, h);
                    RSheet(pr.after, pr.baseAfter, $"{T07RobotReports}/after_{pr.tag}.png", 12, T07Source, T07Model, T07FollowCannon, h);
                }
                log.AppendLine($"controller={T07RobotController} sheets={T07RobotReports}");
            }
            finally
            {
                rStride = 0; rCannonPerp = false; rBarrelRest = Vector3.forward;
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory(T07RobotReports);
                File.WriteAllText(T07RobotReports + "/build-log.txt", log.ToString());
                Debug.Log(log.ToString());
            }
        }

        // Close views (front, top, right side, left side) of the new clips' key frames, and of the old ones.
        static void T07Inspect()
        {
            Directory.CreateDirectory(T07RobotReports);
            var views = new[] { new Vector3(0, 0, 1), new Vector3(0, 1, .2f), new Vector3(1, 0, 0), new Vector3(-1, 0, 0) };
            const float h = 1.87f;
            string N(string n) => $"{T07RobotClipDir}/{n}.anim"; string O(string n) => $"{T07OldDir}/{n}.anim";
            void V(string clip, string bas, float[] times, string png) => RViews(clip, bas, times, views, $"{T07RobotReports}/{png}", T07Source, T07Model, T07FollowCannon, h);
            V(N("R_CombatIdle"), null, new[] { 0f, .6f, 1.2f, 1.8f }, "inspect_combat_idle.png");
            V(N("R_CannonAttack"), N("R_CombatIdle"), new[] { 0f, .033f, .1f, .3f }, "inspect_attack.png");
            V(N("R_HitReaction"), null, new[] { 0f, .1f, .25f, .5f }, "inspect_hit.png");
            V(N("R_Idle"), null, new[] { 0f, .8f, 2.2f, 3.2f }, "inspect_idle.png");
            V(O("CombatIdle"), null, new[] { 0f, .5f, 1f, 1.5f }, "inspect_old_combat_idle.png");
        }

        static GameObject T07Model(GameObject inst)
        {
            var drv = inst.GetComponentInChildren<T07AnimationDriver>(true);
            if (!drv || !drv.Animator) throw new InvalidOperationException("T07AnimationDriver/Animator missing");
            return drv.Animator.gameObject;
        }

        // What T07CannonSocket does each frame at runtime: the cannon mount follows its arm socket.
        static void T07FollowCannon(GameObject inst)
        {
            var sock = inst.GetComponentInChildren<T07CannonSocket>(true); if (!sock) return;
            var so = new SerializedObject(sock);
            var socket = so.FindProperty("socket").objectReferenceValue as Transform;
            var pivot = so.FindProperty("pivot").objectReferenceValue as Transform;
            if (!socket || !pivot) return;
            pivot.SetPositionAndRotation(socket.TransformPoint(so.FindProperty("positionOffset").vector3Value), socket.rotation * so.FindProperty("rotationOffset").quaternionValue);
        }

        // ---------------------------------------------------------------- showcase
        static void T07BuildShowcase()
        {
            string N(string n) => $"{T07RobotClipDir}/{n}.anim"; string O(string n) => $"{T07OldDir}/{n}.anim";
            RobotBuildShowcaseFor(T07RobotShowcase, T07RobotReports, "DELANTE: NUEVO (robot)      DETRAS: ANTES (actual)", T07Visual, new[]
            {
                ("REPOSO\nPATRULLA", N("R_Idle"), O("Idle")),
                ("REPOSO\nCOMBATE", N("R_CombatIdle"), O("CombatIdle")),
                ("PATRULLA", N("R_PatrolWalk"), O("Walk")),
                ("PERSECUCION", N("R_CombatAdvance"), O("Walk")),
                ("DISPARO", N("R_CannonAttack"), O("CannonAttack")),
                ("GOLPE", N("R_HitReaction"), O("HitReaction")),
            }, 1.87f); // measured head height x 1.15 (build-log); the skinned bounds are about twice that
        }

        // Visual-only campaign T-07: unpacked copy with the cannon mount parented to its arm socket and
        // every gameplay component removed (no AI, physics, IK or audio), so only the clip moves it.
        static GameObject T07Visual(UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(T07Source);
            if (!prefab) throw new InvalidOperationException("Missing " + T07Source);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var anim = T07Model(inst).GetComponent<Animator>();
            var sock = inst.GetComponentInChildren<T07CannonSocket>(true);
            if (sock)
            {
                var so = new SerializedObject(sock);
                var socket = so.FindProperty("socket").objectReferenceValue as Transform;
                var pivot = so.FindProperty("pivot").objectReferenceValue as Transform;
                if (socket && pivot)
                {
                    // world pose as T07CannonSocket sets it; the mount already lives under the forearm, so it then
                    // follows the clip (reparent only if it does not, keeping world scale: the rig is scaled x100)
                    pivot.SetPositionAndRotation(socket.TransformPoint(so.FindProperty("positionOffset").vector3Value), socket.rotation * so.FindProperty("rotationOffset").quaternionValue);
                    if (!pivot.IsChildOf(socket)) pivot.SetParent(socket, true);
                }
            }
            StripToVisual(inst, anim);
            return inst;
        }

        // Removes every component that is not rendering or the given Animator (dependency-aware), hides particles.
        static void StripToVisual(GameObject inst, Animator anim)
        {
            foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            for (int pass = 0; pass < 10; pass++)
            {
                bool removed = false;
                foreach (var c in inst.GetComponentsInChildren<Component>(true))
                {
                    if (!c || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup
                        || c is ParticleSystem || c is ParticleSystemRenderer || c == anim) continue;
                    var type = c.GetType();
                    bool needed = c.GetComponents<Component>().Any(o => o && o != c && o.GetType()
                        .GetCustomAttributes(typeof(RequireComponent), true).Cast<RequireComponent>()
                        .Any(rc => new[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 }.Any(rt => rt != null && rt.IsAssignableFrom(type))));
                    if (needed) continue;
                    UnityEngine.Object.DestroyImmediate(c); removed = true;
                }
                if (!removed) break;
            }
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            anim.runtimeAnimatorController = null; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}
