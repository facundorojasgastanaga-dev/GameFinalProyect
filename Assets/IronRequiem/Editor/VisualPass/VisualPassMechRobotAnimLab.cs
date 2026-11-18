using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem.EditorTools
{
    // Test-only wiring for the T-07 Mech robot animation pass: a prefab variant with the copied
    // controller, two identical copies of CharacterLab (Before = F8 clips, After = robot clips) and an
    // automated Play Mode capture (patrol -> chase/attack -> damage). Production prefabs, campaign
    // scenes and CharacterLab itself are never saved. Refuses to run with modified open scenes.
    public static partial class VisualPassMech
    {
        public const string RobotVariant = "Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_RobotAnim_Test.prefab";
        const string RobotLabSource = "Assets/IronRequiem/Scenes/CharacterLab.unity";
        public const string RobotPlayKey = "IRON.T07RobotAnim.Play";
        // tags "Before"/"After"/"Showcase*" belong to the T-07 Mech; tags starting with "T07" to the campaign T-07
        static string RobotLabPath(string tag) => tag.StartsWith("T07")
            ? $"Assets/IronRequiem/Scenes/T07RobotAnimLab_{tag.Substring(3)}.unity"
            : tag.StartsWith("Saw") ? $"Assets/IronRequiem/Scenes/SawBotRobotAnimLab_{tag.Substring(3)}.unity"
            : $"Assets/IronRequiem/Scenes/T07MechRobotAnimLab_{tag}.unity";
        static string RepDir(string tag) => tag.StartsWith("T07") ? T07RobotReports : tag.StartsWith("Saw") ? SawRobotReports : RobotReports;

        static void RobotRequireCleanScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Editor is in Play Mode");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty) throw new InvalidOperationException($"Open scene '{s.path}' has unsaved changes; not touching it.");
                if (string.IsNullOrEmpty(s.path)) throw new InvalidOperationException("An untitled scene is open; not touching it.");
            }
        }

        static string RobotSerializeSetup(SceneSetup[] setup) =>
            string.Join("\n", setup.Select(s => $"{s.path}\t{(s.isLoaded ? 1 : 0)}\t{(s.isActive ? 1 : 0)}"));

        static SceneSetup[] RobotParseSetup(string text) =>
            text.Split('\n').Where(l => l.Contains('\t')).Select(l => l.Split('\t'))
                .Select(p => new SceneSetup { path = p[0], isLoaded = p[1] == "1", isActive = p[2] == "1" }).ToArray();

        // ---------------------------------------------------------------- variant + labs
        static void RobotBuildLabs() => RobotBuildLabsFor(TestPrefab, RobotVariant, RobotController, "T07Mech_Locomotion", "", RobotReports);

        // t07Style: T07AnimationDriver robots (hit trigger added; lab attack range 10 m). Otherwise the robot keeps its
        // own damage reaction and attack range (melee) and only detection/timeout are shortened for the arena.
        static void RobotBuildLabsFor(string basePath, string variantPath, string controllerPath, string expectedController, string tagPrefix, string reports, bool t07Style = true)
        {
            RobotRequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var log = new StringBuilder($"[robot anim labs {tagPrefix}]\n");
            try
            {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (!basePrefab || !ctrl) throw new InvalidOperationException($"Missing {basePath} or {controllerPath}");

                var tmp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, tmp);
                var drv = inst.GetComponentInChildren<T07AnimationDriver>(true);
                var anim = drv ? drv.Animator : inst.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController && a.runtimeAnimatorController.name == expectedController);
                if (!anim) throw new InvalidOperationException("T07AnimationDriver/Animator not found on " + basePath);
                if (!anim.runtimeAnimatorController || anim.runtimeAnimatorController.name != expectedController)
                    throw new InvalidOperationException("Unexpected base controller: " + anim.runtimeAnimatorController);
                anim.runtimeAnimatorController = ctrl;
                // fires OnDamaged (absent in game for this mech) and hands the cannon arm to hit/recoil clips
                if (t07Style && !inst.GetComponent<MechHitReactionTrigger>()) inst.AddComponent<MechHitReactionTrigger>();
                EnsureFolder(System.IO.Path.GetDirectoryName(variantPath).Replace('\\', '/'));
                var variant = PrefabUtility.SaveAsPrefabAsset(inst, variantPath, out bool saved);
                if (!saved || !variant) throw new InvalidOperationException("Variant save failed");
                log.AppendLine($"variant={variantPath} isVariant={PrefabUtility.GetPrefabAssetType(variant) == PrefabAssetType.Variant} animator={Path(anim.transform, inst.transform)}");

                RobotBuildLab(tagPrefix + "Before", basePrefab, log, t07Style);
                RobotBuildLab(tagPrefix + "After", variant, log, t07Style);
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory(reports);
                File.WriteAllText(reports + "/labs-log.txt", log.ToString());
                Debug.Log(log.ToString());
            }
        }

        static void RobotBuildLab(string tag, GameObject prefab, StringBuilder log, bool t07Style = true)
        {
            var path = RobotLabPath(tag);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(RobotLabSource, path)) throw new InvalidOperationException("Lab copy failed: " + path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var patrol = UnityEngine.Object.FindObjectsByType<PatrolPath>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.name == "T07_PatrolPath");
            var oldEnemy = patrol ? patrol.EnemiesToAssign.FirstOrDefault(e => e) : null;
            if (!oldEnemy) throw new InvalidOperationException("T07_PatrolPath or its enemy not found in lab copy");
            var oldRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(oldEnemy.gameObject) ?? oldEnemy.gameObject;

            // other references to the replaced enemy (reported, not rewired)
            int refs = 0; var oldSet = new HashSet<UnityEngine.Object>(oldRoot.GetComponentsInChildren<Component>(true)) { oldRoot };
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (!c || c is Transform || oldSet.Contains(c) || c == patrol) continue;
                    var so = new SerializedObject(c); var it = so.GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue && oldSet.Contains(it.objectReferenceValue))
                        { refs++; log.AppendLine($"  [{tag}] reference kept to old enemy: {c.GetType().Name}.{it.propertyPath} on {c.name}"); }
                }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.transform.SetParent(oldRoot.transform.parent, false);
            inst.transform.SetPositionAndRotation(oldRoot.transform.position, oldRoot.transform.rotation);
            inst.name = prefab.name + "_" + tag;
            // Lab-only scene overrides (identical in Before/After): the campaign tuning (45 m detection,
            // 38 m attack) covers the whole 32 m arena, so the mech would never patrol or chase here.
            var det = inst.GetComponentInChildren<DetectionModule>(true);
            if (det)
            {
                det.DetectionRange = 20f; det.KnownTargetTimeout = 1f; if (t07Style) det.AttackRange = 10f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(det);
                log.AppendLine($"  [{tag}] lab override DetectionModule: detection 20 m, attack {(t07Style ? "10 m" : $"{det.AttackRange:0.##} m (unchanged)")}, timeout 1 s");
            }
            patrol.EnemiesToAssign.Remove(oldEnemy);
            patrol.EnemiesToAssign.Add(inst.GetComponent<EnemyController>());
            EditorUtility.SetDirty(patrol);
            log.AppendLine($"  [{tag}] {path}: replaced {oldRoot.name} by {prefab.name} at {inst.transform.position.ToString("F2")} refsElsewhere={refs}");
            UnityEngine.Object.DestroyImmediate(oldRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Lab save failed: " + path);
        }

        // ---------------------------------------------------------------- play capture
        static void RobotPlay(string tag)
        {
            RobotRequireCleanScenes();
            var lab = AssetDatabase.LoadAssetAtPath<SceneAsset>(RobotLabPath(tag));
            if (!lab) throw new InvalidOperationException("Lab not built: " + tag);
            SessionState.SetString(RobotPlayKey + ".restore", RobotSerializeSetup(EditorSceneManager.GetSceneManagerSetup()));
            var start = EditorSceneManager.playModeStartScene;
            SessionState.SetString(RobotPlayKey + ".start", start ? AssetDatabase.GetAssetPath(start) : "");
            SessionState.SetBool(RobotPlayKey + ".done", false);
            SessionState.SetString(RobotPlayKey + ".since", DateTime.UtcNow.ToString("O"));
            EditorSceneManager.OpenScene(RobotLabPath(tag), OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = lab;
            capInit = false; capDone = false;
            SessionState.SetString(RobotPlayKey, tag);
            EditorApplication.EnterPlaymode();
        }

        // ---------------------------------------------------------------- manual play (user test)
        [MenuItem("Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - Play lab AFTER (manual)")]
        static void PlayAfterManual() => RobotPlayManual("After");
        [MenuItem("Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - Play lab BEFORE (manual)")]
        static void PlayBeforeManual() => RobotPlayManual("Before");

        // Opens the lab and plays it even though IntroMenu is the Editor's Play entry; when Play stops, the
        // previous scenes and the Play entry scene are restored. Nothing from Play Mode is saved.
        static void RobotPlayManual(string tag) => RobotPlayScene(RobotLabPath(tag), tag);

        [MenuItem("Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - Play SHOWCASE (all clips)")]
        static void PlayShowcase() => RobotPlayScene(RobotShowcase, "Showcase");

        static void RobotPlayScene(string scenePath, string tag)
        {
            try
            {
                RobotRequireCleanScenes();
                var lab = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (!lab) throw new InvalidOperationException("Scene not built: " + scenePath);
                SessionState.SetString(RobotPlayKey + ".restore", RobotSerializeSetup(EditorSceneManager.GetSceneManagerSetup()));
                var start = EditorSceneManager.playModeStartScene;
                SessionState.SetString(RobotPlayKey + ".start", start ? AssetDatabase.GetAssetPath(start) : "");
                SessionState.SetBool(RobotPlayKey + ".manualEntered", false);
                SessionState.SetString(RobotPlayKey + ".since", DateTime.UtcNow.ToString("O"));
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EditorSceneManager.playModeStartScene = lab;
                SessionState.SetString(RobotPlayKey + ".manual", tag);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { EditorUtility.DisplayDialog("T07 Mech robot anim", e.Message, "OK"); }
        }

        // ---------------------------------------------------------------- showcase scene
        public const string RobotShowcase = "Assets/IronRequiem/Scenes/T07MechRobotAnimShowcase.unity";

        [MenuItem("Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - Build SHOWCASE scene")]
        static void RobotBuildShowcaseMenu()
        {
            try { RobotBuildShowcase(); } catch (Exception e) { EditorUtility.DisplayDialog("T07 Mech robot anim", e.Message, "OK"); }
        }

        // Two rows of the visual-only mech (no AI): front row = new robot clips, back row = the F8 clips they
        // replace, one column per situation, each playing its clip on a loop (AnimationClipShowcase).
        internal static void RobotBuildShowcase()
        {
            string N(string n) => $"{RobotClipDir}/{n}.anim"; string O(string n) => $"{ClipDir}/{n}.anim";
            RobotBuildShowcaseFor(RobotShowcase, RobotReports, "DELANTE: NUEVO (robot)      DETRAS: ANTES (F8)", scene =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab);
                if (!prefab) throw new InvalidOperationException("Missing " + VisualPrefab);
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            }, new[]
            {
                ("REPOSO\nPATRULLA", N("R_Idle"), O("Mech_Idle")),
                ("REPOSO\nCOMBATE", N("R_CombatIdle"), O("Mech_CombatIdle")),
                ("PATRULLA", N("R_PatrolWalk"), O("Mech_Walk")),
                ("PERSECUCION", N("R_CombatAdvance"), O("Mech_Walk")),
                ("DISPARO", N("R_CannonAttack"), O("Mech_CannonAttack")),
                ("GOLPE", N("R_HitReaction"), O("Mech_HitReaction")),
            });
        }

        // Two rows of a visual-only robot (no AI): front row = new robot clips, back row = the clips they
        // replace, one column per situation, each playing its clip on a loop (AnimationClipShowcase).
        static void RobotBuildShowcaseFor(string scenePath, string reports, string banner,
            Func<UnityEngine.SceneManagement.Scene, GameObject> make, (string label, string after, string before)[] slots, float heightOverride = 0, float[] speeds = null)
        {
            RobotRequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var log = new StringBuilder($"[robot anim showcase] {scenePath}\n");
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var probe = make(scene);
                // size from the body only: skip helper/FX renderers far bigger than the robot (logged)
                var all = probe.GetComponentsInChildren<Renderer>();
                foreach (var r in all.OrderByDescending(x => x.bounds.size.magnitude).Take(4)) log.AppendLine($"  probe renderer {Path(r.transform, probe.transform)} {r.GetType().Name} size={r.bounds.size.ToString("F2")}");
                var body = all.Where(x => x is SkinnedMeshRenderer || x is MeshRenderer).Where(x => x.bounds.size.magnitude < 6f).ToArray();
                var rs = body.Length > 0 ? body : all; var bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
                float h = bounds.size.y, sp = Mathf.Max(bounds.size.x, bounds.size.z, h * .6f) * 1.5f, rowGap = h * 2.4f;
                if (heightOverride > 0) { h = heightOverride; sp = h * 1.35f; rowGap = h * 2.4f; } // skinned bounds can be loose
                UnityEngine.Object.DestroyImmediate(probe);
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                GameObject Label(string text, Vector3 pos, float size, Color color)
                {
                    var go = new GameObject("Label_" + text.Replace("\n", " "));
                    go.transform.position = pos;
                    var tm = go.AddComponent<TextMesh>(); tm.text = text; tm.font = font; tm.fontSize = 64; tm.characterSize = size;
                    tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
                    go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    return go;
                }
                var rows = new (string title, float z, int clip, Color color)[] { ("NUEVO", 0f, 0, new Color(1f, .75f, .3f)), ("ANTES", rowGap, 1, new Color(.7f, .8f, .9f)) };
                var root = new GameObject("Showcase");
                for (int i = 0; i < slots.Length; i++)
                {
                    float x = (i - (slots.Length - 1) * .5f) * sp;
                    Label(slots[i].label, new Vector3(x, -h * .12f, -h * .35f), h * .018f, Color.white).transform.SetParent(root.transform, true);
                    foreach (var row in rows)
                    {
                        var path = row.clip == 0 ? slots[i].after : slots[i].before;
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (!clip) throw new InvalidOperationException("Missing clip " + path);
                        var inst = make(scene);
                        inst.name = $"{row.title}_{i + 1}_{clip.name}";
                        inst.transform.SetParent(root.transform, false);
                        inst.transform.SetPositionAndRotation(new Vector3(x, 0, row.z), Quaternion.Euler(0, 149, 0)); // 3/4 front, cannon side
                        var anim = inst.GetComponentInChildren<Animator>(true);
                        if (!anim) throw new InvalidOperationException("No Animator on showcase robot");
                        var show = inst.AddComponent<AnimationClipShowcase>(); show.Configure(anim, clip);
                        if (speeds != null && i < speeds.Length) show.Speed = speeds[i];
                        log.AppendLine($"  {inst.name} at x={x:0.00} z={row.z:0.00}");
                    }
                }
                Label(banner, new Vector3(0, h * 1.35f, rowGap + h * .3f), h * .024f, new Color(1f, .75f, .3f)).transform.SetParent(root.transform, true);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor"; floor.transform.position = new Vector3(0, 0, rowGap * .5f);
                floor.transform.localScale = new Vector3(slots.Length * sp * .18f, 1, rowGap * .45f);
                var cam = Camera.main;
                if (cam)
                {
                    cam.fieldOfView = 46;
                    cam.transform.position = new Vector3(0, h * 2.7f, -slots.Length * sp * .62f);
                    cam.transform.LookAt(new Vector3(0, h * .45f, rowGap * .45f));
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Showcase save failed");
                log.AppendLine($"scene={scenePath} height={h:0.00} spacing={sp:0.00}");
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory(reports);
                File.WriteAllText(reports + "/showcase-log.txt", log.ToString());
            }
        }

        // Edit-mode still of the showcase through its own camera (clips sampled at a readable time).
        internal static void RobotShowcaseShot() => RobotShowcaseShotFor(RobotShowcase, RobotReports);
        static void RobotShowcaseShotFor(string scenePath, string reports)
        {
            RobotRequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var show in UnityEngine.Object.FindObjectsByType<AnimationClipShowcase>(FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(show);
                    var clip = so.FindProperty("clip").objectReferenceValue as AnimationClip;
                    var anim = so.FindProperty("target").objectReferenceValue as Animator;
                    if (clip && anim) clip.SampleAnimation(anim.gameObject, clip.isLooping ? clip.length * .3f : Mathf.Min(.07f, clip.length));
                }
                var cam = Camera.main;
                var rt = new RenderTexture(1920, 1080, 24); cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
                RenderTexture.active = prev; rt.Release();
                File.WriteAllBytes(reports + "/showcase.png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                // the sampled poses dirty the showcase scene: discard them, never save
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        internal static void RobotShowcaseCheck() { checkSamples = null; RobotPlayScene(RobotShowcase, "ShowcaseCheck"); }
        internal static void T07ShowcaseCheck() { checkSamples = null; RobotPlayScene(T07RobotShowcase, "T07ShowcaseCheck"); }
        static Dictionary<AnimationClipShowcase, (Transform[] bones, Vector3[] first, float[] maxMove, string[] names)> checkSamples; static bool checkDone;
        // Plays the showcase from 0.6 s to 2.6 s, sampling every frame, and records each robot's largest bone
        // excursion from its first pose (bone positions relative to the robot root; catches cyclic clips too).
        static void ShowcaseCheckTick(string tag)
        {
            try { ShowcaseCheckStep(RepDir(tag)); }
            catch (Exception e) { checkDone = true; File.WriteAllText(RepDir(tag) + "/showcase-play-check.txt", e.ToString()); EditorApplication.ExitPlaymode(); }
        }

        static void ShowcaseCheckStep(string reports)
        {
            float t = Time.timeSinceLevelLoad;
            if (t < .6f) { checkDone = false; return; }
            if (checkDone) return;
            var shows = UnityEngine.Object.FindObjectsByType<AnimationClipShowcase>(FindObjectsSortMode.None);
            Vector3 Local(AnimationClipShowcase s, Transform b) => s.transform.InverseTransformPoint(b.position);
            if (checkSamples == null)
            {
                checkSamples = shows.ToDictionary(s => s, s =>
                {
                    var bones = s.GetComponentsInChildren<Transform>().Where(b => b != s.transform && !b.GetComponent<Renderer>()).ToArray();
                    return (bones, bones.Select(b => Local(s, b)).ToArray(), new float[bones.Length], bones.Select(b => b.name).ToArray());
                });
                return;
            }
            foreach (var kv in checkSamples)
                for (int i = 0; i < kv.Value.bones.Length; i++)
                    kv.Value.maxMove[i] = Mathf.Max(kv.Value.maxMove[i], Vector3.Distance(kv.Value.first[i], Local(kv.Key, kv.Value.bones[i])));
            if (t < 2.6f) return;
            checkDone = true;
            var sb = new StringBuilder($"[showcase play check] robots={shows.Length} window=0.6-2.6 s (largest bone excursion)\n");
            int moving = 0;
            foreach (var s in shows.OrderBy(x => x.name))
            {
                if (!checkSamples.TryGetValue(s, out var c)) { sb.AppendLine($"  {s.name}: not sampled"); continue; }
                int top = 0; for (int i = 1; i < c.maxMove.Length; i++) if (c.maxMove[i] > c.maxMove[top]) top = i;
                float d = c.maxMove.Length > 0 ? c.maxMove[top] : 0;
                if (d > .01f) moving++;
                sb.AppendLine($"  {s.name}: max {d * 100:0.0} cm ({(c.names.Length > 0 ? c.names[top] : "-")})");
            }
            sb.AppendLine($"moving={moving}/{shows.Length}");
            Directory.CreateDirectory(reports);
            File.WriteAllText(reports + "/showcase-play-check.txt", sb.ToString());
            EditorApplication.ExitPlaymode();
        }

        internal static void RobotManualTick()
        {
            if (SessionState.GetString(RobotPlayKey + ".manual", "") == "") return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(RobotPlayKey + ".manualEntered", true);
                var manualTag = SessionState.GetString(RobotPlayKey + ".manual", "");
                if (EditorApplication.isPlaying && manualTag.EndsWith("ShowcaseCheck")) ShowcaseCheckTick(manualTag);
                return;
            }
            // play mode is still tearing down (isPlaying stays true briefly after ExitingPlayMode): wait
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!SessionState.GetBool(RobotPlayKey + ".manualEntered", false) && !RobotPlayTimedOut()) return;
            var startPath = SessionState.GetString(RobotPlayKey + ".start", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(startPath) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(startPath);
            var setup = RobotParseSetup(SessionState.GetString(RobotPlayKey + ".restore", ""));
            try
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                // clear the request only once the restore went through, so a failure retries next tick
                SessionState.EraseString(RobotPlayKey + ".manual"); SessionState.EraseBool(RobotPlayKey + ".manualEntered");
            }
            catch (Exception e) { Debug.LogWarning("T07 robot anim: scene restore deferred: " + e.Message); }
        }

        // Reopens a project scene after a test run whose saved setup was lost (never saves; refuses dirty scenes).
        internal static void RobotOpenScene(string path)
        {
            if (!path.StartsWith("Assets/") || !path.EndsWith(".unity") || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) throw new InvalidOperationException("Not a project scene: " + path);
            RobotRequireCleanScenes();
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        internal static void RobotPlayFinish(string reason)
        {
            SessionState.EraseString(RobotPlayKey);
            var startPath = SessionState.GetString(RobotPlayKey + ".start", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(startPath) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(startPath);
            var setup = RobotParseSetup(SessionState.GetString(RobotPlayKey + ".restore", ""));
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            File.WriteAllText(RobotReports + "/status.txt", $"{reason} {DateTime.Now:O}");
        }

        internal static bool RobotPlayTimedOut() =>
            DateTime.TryParse(SessionState.GetString(RobotPlayKey + ".since", ""), null, System.Globalization.DateTimeStyles.RoundtripKind, out var since)
            && (DateTime.UtcNow - since).TotalSeconds > 120;

        const int CapCols = 10, CapRows = 4, CapW = 320, CapH = 400;
        static bool capInit, capDone;
        static Texture2D capSheet; static Camera capCam; static RenderTexture capRT;
        static List<(float time, int row, int col)> capShots;
        static StringBuilder capLog; static int capAttacks, capDamaged, capErrors;
        static GameObject capMech; static Animator capAnim; static DetectionModule capDetect;
        static PlayerCharacterController capPlayer; static bool capTeleported, capHit, capAttackBurst; static float capHitTime;

        static void CapLogMessage(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition.Contains("Licensing")) return;
            capErrors++; if (capErrors <= 5) capLog?.AppendLine("  error: " + condition);
        }

        internal static void RobotCaptureTick(string tag)
        {
            if (capDone) return;
            if (!capInit)
            {
                capInit = true; capDone = false; capTeleported = capHit = capAttackBurst = false; capAttacks = capDamaged = capErrors = 0;
                capLog = new StringBuilder($"[T07 robot anim play {tag}] scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}\n");
                Application.logMessageReceived += CapLogMessage;
                // the lab robot is named "<prefab>_<tag>" by RobotBuildLab
                var robot = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).FirstOrDefault(e => e.name.EndsWith("_" + tag));
                capPlayer = UnityEngine.Object.FindAnyObjectByType<PlayerCharacterController>();
                if (!robot || !capPlayer) { capLog.AppendLine("  missing robot or player"); CapFinish(tag); return; }
                capMech = robot.gameObject;
                var drv = capMech.GetComponent<T07AnimationDriver>();
                capAnim = drv ? drv.Animator : capMech.GetComponentsInChildren<Animator>(true).First(a => a.runtimeAnimatorController);
                var ec = capMech.GetComponent<EnemyController>();
                capDetect = ec ? ec.DetectionModule : null;
                if (ec) { ec.onAttack += CapOnAttack; ec.onDamaged += () => capDamaged++; }
                var ph = capPlayer.GetComponent<Health>(); if (ph) ph.Invincible = true;
                // patrol phase: park the player in the far corner, out of detection range
                CapPlacePlayer(GameObject.Find("Floor_32m"), 1f);
                capLog.AppendLine($"  mech={capMech.name} controller={capAnim.runtimeAnimatorController.name}");
                capRT = new RenderTexture(CapW, CapH, 24);
                var camGo = new GameObject("RobotAnimCaptureCam") { hideFlags = HideFlags.DontSave };
                capCam = camGo.AddComponent<Camera>(); capCam.enabled = false; capCam.fieldOfView = 35; capCam.targetTexture = capRT;
                capSheet = new Texture2D(CapW * CapCols, CapH * CapRows, TextureFormat.RGB24, false);
                capShots = new List<(float, int, int)>();
                for (int i = 0; i < CapCols; i++) capShots.Add((2.5f + i * .35f, 0, i));   // patrol
                for (int i = 0; i < CapCols; i++) capShots.Add((6.3f + i * .3f, 1, i));    // chase
                // row 2: burst locked to the first shot after the chase; row 3: burst locked to the hit
            }
            float t = Time.timeSinceLevelLoad;
            if (!capMech) { capLog.AppendLine("  mech destroyed"); CapFinish(tag); return; }
            if (!capTeleported && t >= 6f)
            {
                capTeleported = true;
                var center = GameObject.Find("Floor_32m"); var c = center ? center.transform.position : Vector3.zero;
                var dir = Vector3.ProjectOnPlane(c - capMech.transform.position, Vector3.up); if (dir.sqrMagnitude < .01f) dir = capMech.transform.forward;
                var target = capMech.transform.position + dir.normalized * 15f; // outside attack range: forces a chase first
                CapMovePlayer(target);
                capLog.AppendLine($"  t={t:0.00} player placed {Vector3.Distance(capPlayer.transform.position, capMech.transform.position):0.0} m from mech");
            }
            if (capTeleported && capDetect) capDetect.ForceKnownTarget(capPlayer.gameObject);
            if (!capHit && t >= 10.2f)
            {
                capHit = true; var h = capMech.GetComponent<Health>(); if (h) h.TakeDamage(1f, capPlayer.gameObject);
                // No forced trigger: Before shows what the game does today (no OnDamaged for this mech);
                // After relies on MechHitReactionTrigger on the variant.
                capLog.AppendLine($"  t={t:0.00} damage applied (trigger={(capMech.GetComponent<MechHitReactionTrigger>() ? "MechHitReactionTrigger" : "none")})");
                capHitTime = t; CapBurst(t, 3);
            }
            while (capShots.Count > 0 && t >= capShots[0].time)
            {
                var s = capShots[0]; capShots.RemoveAt(0);
                var m = capMech.transform; var focus = m.position + Vector3.up * 1.3f;
                var side = s.row == 1 ? (m.forward * .55f + m.right * .85f).normalized : m.right;
                float dist = s.row >= 2 ? 4.8f : 6.5f;
                // keep the camera out of walls: use the mirrored side when the first one is blocked
                if (Physics.Raycast(focus + side * 1.2f, side, dist, ~0, QueryTriggerInteraction.Ignore)) side = Vector3.Reflect(side, m.right);
                capCam.transform.position = focus + side * dist + Vector3.up * .4f; capCam.transform.LookAt(focus);
                capCam.Render();
                var prev = RenderTexture.active; RenderTexture.active = capRT;
                capSheet.ReadPixels(new Rect(0, 0, CapW, CapH), s.col * CapW, (CapRows - 1 - s.row) * CapH);
                RenderTexture.active = prev;
                string state = capAnim.GetCurrentAnimatorStateInfo(0) switch
                {
                    var i when i.IsName("ArmedLocomotion") => "ArmedLocomotion",
                    var i when i.IsName("AlertedLocomotion") => "AlertedLocomotion",
                    var i when i.IsName("HitReaction") => "HitReaction",
                    var i when i.IsName("DisarmedLocomotion") => "DisarmedLocomotion",
                    var i when i.IsName("Death") => "Death",
                    var i when i.IsName("Locomotion") => "Locomotion",
                    var i when i.IsName("Attack") => "Attack",
                    var i when i.IsName("SawHold") => "SawHold",
                    var i when i.IsName("Rest") => "Rest",
                    _ => "?"
                };
                string upper = "";
                if (capAnim.layerCount > 1)
                {
                    var u1 = capAnim.GetCurrentAnimatorStateInfo(1);
                    upper = u1.IsName("CannonAttack") ? " +CannonAttack" : u1.IsName("HitReaction") ? " +HitReaction" : u1.IsName("Hit") ? " +Hit" : "";
                }
                var ag = capMech.GetComponent<EnemyController>()?.NavMeshAgent;
                var mob = capMech.GetComponent<EnemyMobile>();
                if (s.col == 0 || s.col == 5)
                    capLog.AppendLine($"    diag onNavMesh={(ag ? ag.isOnNavMesh : false)} agentType={(ag ? NavMesh.GetSettingsNameFromID(ag.agentTypeID) : "-")} agentEnabled={(ag ? ag.enabled : false)} stopped={(ag && ag.isOnNavMesh ? ag.isStopped : true)} hasPath={(ag && ag.isOnNavMesh ? ag.hasPath : false)} vel={(ag ? ag.velocity.magnitude : 0):0.00} ai={(mob ? mob.AiState.ToString() : "-")} target={(capDetect && capDetect.KnownDetectedTarget ? capDetect.KnownDetectedTarget.name : "none")} dist={Vector3.Distance(capPlayer.transform.position, capMech.transform.position):0.0}");
                capLog.AppendLine($"  shot r{s.row}c{s.col} t={t:0.00} state={state}{upper} speed={capAnim.GetFloat("MoveSpeed"):0.00} alerted={capAnim.GetBool("Alerted")} attacks={capAttacks}");
            }
            if ((capShots.Count == 0 && capHit && t > capHitTime + .6f) || t > 14f) CapFinish(tag);
        }

        static void CapOnAttack()
        {
            capAttacks++;
            if (!capTeleported || capAttackBurst) return;
            capAttackBurst = true; float t = Time.timeSinceLevelLoad;
            capLog.AppendLine($"  t={t:0.00} first shot after chase: burst");
            CapBurst(t, 2);
        }

        static void CapBurst(float start, int row)
        {
            for (int i = 0; i < CapCols; i++) capShots.Add((start + i * .05f, row, i));
            capShots.Sort((a, b) => a.time.CompareTo(b.time));
        }

        static void CapMovePlayer(Vector3 target)
        {
            if (NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas)) target = hit.position;
            var cc = capPlayer.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            capPlayer.transform.position = target + Vector3.up * .1f; if (cc) cc.enabled = true;
        }

        // Far corner of the arena relative to the mech (out of the 20 m detection range).
        static void CapPlacePlayer(GameObject floor, float _)
        {
            var c = floor ? floor.transform.position : Vector3.zero;
            var away = Vector3.ProjectOnPlane(c - capMech.transform.position, Vector3.up);
            if (away.sqrMagnitude < .01f) away = -capMech.transform.forward;
            var side = Vector3.Cross(Vector3.up, away.normalized);
            CapMovePlayer(c + away.normalized * 13f + side * 13f);
            capLog.AppendLine($"  player parked {Vector3.Distance(capPlayer.transform.position, capMech.transform.position):0.0} m from mech");
        }

        static void CapFinish(string tag)
        {
            capDone = true;
            Application.logMessageReceived -= CapLogMessage;
            var rep = RepDir(tag); Directory.CreateDirectory(rep);
            if (capSheet) { capSheet.Apply(); File.WriteAllBytes($"{rep}/play_{tag.ToLowerInvariant()}.png", capSheet.EncodeToPNG()); }
            capLog.AppendLine($"  attacks={capAttacks} damagedEvents={capDamaged} consoleErrors={capErrors}");
            File.WriteAllText($"{rep}/play_{tag.ToLowerInvariant()}.txt", capLog.ToString());
            if (capCam) UnityEngine.Object.Destroy(capCam.gameObject);
            if (capRT) capRT.Release();
            SessionState.SetBool(RobotPlayKey + ".done", true);
            EditorApplication.ExitPlaymode();
        }
    }
}
