using System.Linq;
using System.Text;
using IronRequiem;
using Unity.FPS.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // F8 test: a standalone copy of the campaign T-07 driven by the new mech visual, and a copy of the
    // campaign scene that spawns it instead of the original. Originals are left untouched.
    public static partial class VisualPassMech
    {
        const string OldPrefab = "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";
        public const string TestPrefab = "Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_Test.prefab";
        const float TargetHeight = 2.6f, TestAgentSpeed = 3f;

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f) return f; }
            return null;
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 5 test prefab")]
        static void BuildTestPrefab()
        {
            var log = new StringBuilder("[F8 test prefab]\n");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefab));
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "T07_Mech_Test"; inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var drv = inst.GetComponent<T07AnimationDriver>(); var oldAnim = inst.GetComponent<Animator>();
                var mobile = inst.GetComponent<EnemyMobile>(); var socket = inst.GetComponent<T07CannonSocket>();
                var mount = inst.transform.Find("WeaponRoot/Weapon_EyeLazers/CannonMount");
                var module = mount.Find("CannonModule_Visual"); var joint = mount.GetComponentInChildren<DetachableCannon>(true);

                // hide the old body and old cannon art (gameplay objects stay)
                int hidden = 0;
                foreach (var r in inst.transform.Find("Body").GetComponentsInChildren<Renderer>(true)) if (r is not ParticleSystemRenderer) { r.enabled = false; hidden++; }
                foreach (var r in module.GetComponentsInChildren<Renderer>(true)) if (r is not ParticleSystemRenderer) { r.enabled = false; hidden++; }

                // new visual, scaled to the T-07 height
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab), inst.transform);
                PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                vis.transform.localPosition = Vector3.zero; vis.transform.localRotation = Quaternion.identity;
                var smr = vis.GetComponentInChildren<SkinnedMeshRenderer>(true);
                float h = smr.sharedMesh.bounds.size.y; // model space, before scale
                float scale = TargetHeight / Mathf.Max(.01f, h); vis.transform.localScale = Vector3.one * scale;
                var model = vis.transform.Find("Model"); var anim = model.GetComponent<Animator>();
                anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // the old animation stack goes quiet
                oldAnim.enabled = false;
                var rb = inst.GetComponent("RigBuilder") as Behaviour; if (rb) rb.enabled = false;
                var feet = inst.GetComponent<T07FootPlacement>(); if (feet) feet.enabled = false;
                var footRig = inst.transform.Find("FootPlacementRig"); if (footRig) footRig.gameObject.SetActive(false);

                // every reference to the old Animator now points at the mech
                int refs = 0;
                foreach (var c in inst.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!c) continue; var so = new SerializedObject(c); var it = so.GetIterator(); bool dirty = false;
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == oldAnim) { it.objectReferenceValue = anim; dirty = true; refs++; }
                    if (dirty) so.ApplyModifiedPropertiesWithoutUndo();
                }
                drv.Configure(anim, joint, null);
                if (mobile) mobile.Animator = anim;

                // cannon: the mech forearm cannon becomes the detachable module art
                var forearm = FindDeep(model, CannonBone); var mechCannon = forearm.Find("T07Mech_Cannon");
                mount.SetPositionAndRotation(forearm.position, forearm.rotation);
                mechCannon.SetParent(module, true);
                var so2 = new SerializedObject(socket);
                var weapon = so2.FindProperty("weapon").objectReferenceValue as Transform;
                socket.Configure(forearm, mount, weapon, joint, mobile);

                // hit flash + torso reactions on the new body
                var chest = FindDeep(model, "Chest");
                var hit = inst.GetComponent<RobotHitFeedback>();
                if (hit)
                {
                    var so3 = new SerializedObject(hit);
                    var body = so3.FindProperty("body"); body.arraySize = 2;
                    body.GetArrayElementAtIndex(0).objectReferenceValue = smr; body.GetArrayElementAtIndex(1).objectReferenceValue = mechCannon.GetComponent<Renderer>();
                    so3.FindProperty("torso").objectReferenceValue = chest; so3.ApplyModifiedPropertiesWithoutUndo();
                }

                // death: the shared overload burst at the new chest, then the mech blows apart (no old armor/arm debris)
                var prog = inst.GetComponent<RobotDamageProgression>();
                if (prog)
                {
                    var so5 = new SerializedObject(prog);
                    so5.FindProperty("bodyRenderers").arraySize = 0; so5.FindProperty("armor").arraySize = 0;
                    so5.FindProperty("criticalArm").objectReferenceValue = null; so5.FindProperty("chest").objectReferenceValue = chest;
                    so5.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var old in inst.GetComponentsInChildren<Transform>(true).Where(t => t && t.name.StartsWith("VPD_")).ToArray()) if (old) Object.DestroyImmediate(old.gameObject);
                var boom = inst.GetComponent<MechDeathExplosion>() ?? inst.AddComponent<MechDeathExplosion>();
                var chunks = model.GetComponentsInChildren<MechDebrisPiece>(true);
                boom.Configure(smr, mechCannon.GetComponent<Renderer>(), joint, chest, chunks);
                if (!inst.GetComponent<HeavyDeathFeel>()) inst.AddComponent<HeavyDeathFeel>();
                log.AppendLine($"death chunks={chunks.Length}");

                // gait: stride matches the clips; a 2.6 m mech at the old 4.2 m/s would be sprinting
                var probe = new Rig(InstantiateRigProbe());
                float stride = GaitStride(probe) * scale; Object.DestroyImmediate(probe.root.gameObject);
                var so4 = new SerializedObject(drv); so4.FindProperty("strideDistance").floatValue = stride; so4.ApplyModifiedPropertiesWithoutUndo();
                var agent = inst.GetComponent<UnityEngine.AI.NavMeshAgent>(); float oldSpeed = agent.speed; agent.speed = TestAgentSpeed;

                PrefabUtility.SaveAsPrefabAsset(inst, TestPrefab);
                EditorApplication.delayCall += VisualPassDeathFx.MechAim; // cannon arm aims + muzzle at the mouth
                log.AppendLine($"hidden renderers={hidden} animatorRefs={refs} scale={scale:0.###} height={h * scale:0.##} stride={stride:0.##} speed {oldSpeed}->{TestAgentSpeed} saved {TestPrefab}");
            }
            finally { Object.DestroyImmediate(inst); Debug.Log(log.ToString()); }
        }

        static GameObject InstantiateRigProbe()
        {
            var wrap = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab));
            var go = wrap.transform.Find("Model").gameObject; go.transform.SetParent(null, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); Object.DestroyImmediate(wrap); return go;
        }

        // Swaps every T07_Current_Repaired instance/reference in an open scene; logs other T-07 prefabs it meets.
        static int SwapInScene(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            var oldAsset = AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefab); var newAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefab);
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (!t) continue; var go = t.gameObject;
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(go) && PrefabUtility.GetCorrespondingObjectFromOriginalSource(go) == oldAsset)
                    {
                        var ni = (GameObject)PrefabUtility.InstantiatePrefab(newAsset, go.transform.parent);
                        ni.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation); ni.name = go.name;
                        ni.SetActive(go.activeSelf); ni.transform.SetSiblingIndex(go.transform.GetSiblingIndex());
                        Object.DestroyImmediate(go); n++;
                    }
                }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!c) continue; var so = new SerializedObject(c); var it = so.GetIterator(); bool dirty = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || !it.objectReferenceValue) continue;
                        var v = it.objectReferenceValue; var vgo = v as GameObject ?? (v as Component)?.gameObject;
                        if (!vgo || !EditorUtility.IsPersistent(vgo)) continue;
                        if (vgo == oldAsset) { it.objectReferenceValue = v is GameObject ? newAsset : newAsset.GetComponent(v.GetType()); dirty = true; n++; }
                        else if (vgo.GetComponent<T07AnimationDriver>()) log.AppendLine($"  other T-07 prefab kept: {vgo.name} on {c.gameObject.name}:{c.GetType().Name}");
                    }
                    if (dirty) so.ApplyModifiedPropertiesWithoutUndo();
                }
            return n;
        }

        static string CopyScene(string name)
        {
            var src = AssetDatabase.FindAssets(name + " t:Scene").Select(AssetDatabase.GUIDToAssetPath).First(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
            var dst = System.IO.Path.GetDirectoryName(src).Replace('\\', '/') + "/" + name + "_MechTest.unity";
            if (!System.IO.File.Exists(dst)) AssetDatabase.CopyAsset(src, dst);
            return dst;
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 6 test scene")]
        static void BuildTestScene()
        {
            var log = new StringBuilder("[F8 test scene]\n");
            var main = CopyScene("RequiemCampaign");
            var build = EditorBuildSettings.scenes.ToList();
            var mainScene = EditorSceneManager.OpenScene(main, OpenSceneMode.Additive); // the open scene and its edits stay untouched
            var flow = mainScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignFlow>(true)).First();
            var fso = new SerializedObject(flow); var stages = fso.FindProperty("stageScenes");
            for (int i = 0; i < stages.arraySize; i++)
            {
                var name = stages.GetArrayElementAtIndex(i).stringValue; if (name.EndsWith("_MechTest")) name = name.Substring(0, name.Length - 9);
                var path = CopyScene(name);
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                int swapped = SwapInScene(sc, log);
                EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); EditorSceneManager.CloseScene(sc, true);
                stages.GetArrayElementAtIndex(i).stringValue = name + "_MechTest";
                if (!build.Any(b => b.path == path)) build.Add(new EditorBuildSettingsScene(path, true));
                log.AppendLine($"  {name} -> {path} swapped={swapped}");
            }
            fso.ApplyModifiedPropertiesWithoutUndo();
            int mainSwapped = SwapInScene(mainScene, log);
            if (!build.Any(b => b.path == main)) build.Add(new EditorBuildSettingsScene(main, true));
            EditorBuildSettings.scenes = build.ToArray();
            EditorSceneManager.MarkSceneDirty(mainScene); EditorSceneManager.SaveScene(mainScene); EditorSceneManager.CloseScene(mainScene, true);
            log.AppendLine($"main={main} swapped={mainSwapped}");
            Debug.Log(log.ToString());
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 7 open test scene")]
        static void OpenTestScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.isDirty) { Debug.LogWarning($"[F8 open] {active.name} has unsaved changes; not switching scenes"); return; }
            EditorSceneManager.OpenScene("Assets/IronRequiem/Scenes/RequiemCampaign_MechTest.unity", OpenSceneMode.Single);
            Debug.Log("[F8 open] RequiemCampaign_MechTest open");
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 8 frame nearest T-07 (play)")]
        static void FrameMech()
        {
            var mechs = Object.FindObjectsByType<T07AnimationDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var cam = Camera.main; if (!cam || mechs.Length == 0) { Debug.Log($"[F8 frame] mechs={mechs.Length}"); return; }
            var m = mechs.OrderBy(x => (x.transform.position - cam.transform.position).sqrMagnitude).First();
            var player = cam.transform.root;
            var dir = Vector3.ProjectOnPlane(m.transform.position - player.position, Vector3.up).normalized;
            var cc = player.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            player.position = m.transform.position - dir * 7f + Vector3.up * .2f; player.rotation = Quaternion.LookRotation(dir);
            if (cc) cc.enabled = true;
            Debug.Log($"[F8 frame] {m.name} speed={m.Speed:0.00} anim={m.Animator?.runtimeAnimatorController?.name}");
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 9 spawn test T-07 (play)")]
        static void SpawnMech()
        {
            if (!Application.isPlaying) return;
            var cam = Camera.main; var player = cam.transform.root;
            var fwd = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            var pos = player.position + fwd * 12f;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefab), pos, Quaternion.LookRotation(-fwd));
            Debug.Log($"[F8 spawn] {go.name} at {pos}");
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 10 kill test T-07s (play)")]
        static void KillMechs()
        {
            if (!Application.isPlaying) return;
            foreach (var m in Object.FindObjectsByType<MechDeathExplosion>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            { var h = m.GetComponent<Unity.FPS.Game.Health>(); if (h && h.CurrentHealth > 0) h.Kill(); }
        }
    }
}
