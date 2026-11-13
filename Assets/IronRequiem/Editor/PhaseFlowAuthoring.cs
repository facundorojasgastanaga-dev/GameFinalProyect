using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class PhaseFlowAuthoring
    {
        public const string Report = "Tools/IronRequiem/Reports/PhaseFlow", Folder = "Assets/IronRequiem/Prefabs/PhaseFlow";
        const string Queue = "Tools/IronRequiem/Requests/phase-flow.txt";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static readonly string[] Scenes = { "CombatTwoFloorsLab", "CombatTwoRoomsLab", "ChaseCorridorLab" };
        static PhaseFlowAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue) || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/PhaseFlowAuthoring.cs") > Loaded || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { if (op == "apply") Apply(); else if (op == "open") EditorSceneManager.OpenScene("Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity"); else Inspect(); IronRequiemSetupTools.WriteAudit(); }
            catch (Exception e) { File.WriteAllText(Report + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        static void Inspect()
        {
            var log = new StringBuilder();
            foreach (string name in Scenes)
            {
                string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    log.AppendLine(name + " corridor entrances=" + All<ChaseSceneEntrance>(scene).Length);
                    foreach (var c in All<MonoBehaviour>(scene).Where(c => c is CombatTwoRoomsLab || c is ChaseCorridorEncounter))
                    {
                        var p = new SerializedObject(c).GetIterator(); while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is GameObject go && EditorUtility.IsPersistent(go) && go.GetComponent<EnemyController>())
                        {
                            var source = go.GetComponent<EnemyExecutionSource>();
                            log.AppendLine(p.propertyPath + " = " + AssetDatabase.GetAssetPath(go) + " Health=" + go.GetComponent<Health>().MaxHealth + " execution=" + (source ? AssetDatabase.GetAssetPath(source.ModulePrefab) : "MISSING"));
                        }
                    }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Report + "/before.txt", log.ToString());
        }
        static GameObject Repair(string sourcePath, string canonicalPath)
        {
            var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalPath).GetComponent<EnemyExecutionSource>();
            if (!canonical || !canonical.ModulePrefab || !canonical.ModulePrefab.WeaponPrefab) throw new Exception("Missing canonical module: " + canonicalPath);
            string path = Folder + "/" + Path.GetFileNameWithoutExtension(sourcePath) + "_Execution.prefab";
            if (!File.Exists(path))
            {
                var preview = EditorSceneManager.NewPreviewScene();
                try { var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath), preview); PrefabUtility.SaveAsPrefabAsset(instance, path); }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var execution = root.GetComponent<EnemyExecutionSource>(); if (!execution) execution = root.AddComponent<EnemyExecutionSource>(); execution.Configure(canonical.ModulePrefab);
                foreach (var joint in root.GetComponentsInChildren<DetachableCannon>(true))
                {
                    var data = new SerializedObject(joint); data.FindProperty("executionOnly").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var hurt in joint.GetComponentsInChildren<Damageable>(true)) { data = new SerializedObject(hurt); data.FindProperty("healthOverride").objectReferenceValue = root.GetComponent<Health>(); data.ApplyModifiedPropertiesWithoutUndo(); }
                }
                var destruction = root.GetComponent<BigBotDestruction>(); if (destruction) { var data = new SerializedObject(destruction); data.FindProperty("executionOnly").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo(); }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        static void Apply()
        {
            Inspect(); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            string t07 = ShredderT07RepairAuthoring.FixedT07, big = "Assets/IronRequiem/Prefabs/Enemies/BigBot_Burst_v10.prefab";
            var before = new[] { AssetDatabase.LoadAssetAtPath<GameObject>(t07), AssetDatabase.LoadAssetAtPath<GameObject>(big) };
            var after = new[] { Repair(t07, "Assets/IronRequiem/Prefabs/CombatFollowup/T07_HitFeedback_v04.prefab"), Repair(big, "Assets/IronRequiem/Prefabs/SecondaryShotgun/BigBot_Burst_v10_NavThrottle.prefab") };
            var log = new StringBuilder();
            foreach (string name in Scenes)
            {
                string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); var previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
                try
                {
                    int changed = 0;
                    foreach (var c in All<MonoBehaviour>(scene).Where(c => c is CombatTwoRoomsLab || c is ChaseCorridorEncounter))
                    {
                        var data = new SerializedObject(c); var p = data.GetIterator(); while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference) for (int i = 0; i < before.Length; i++) if (p.objectReferenceValue == before[i]) { p.objectReferenceValue = after[i]; changed++; break; }
                        if (c is CombatTwoRoomsLab) data.FindProperty("gatePreparationSeconds").floatValue = 1.5f;
                        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(c); if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    }
                    var waves = All<CombatTwoRoomsLab>(scene).SingleOrDefault();
                    if (waves)
                    {
                        var visibility = waves.GetComponent<LabRoomVisibility>(); if (!visibility) visibility = waves.gameObject.AddComponent<LabRoomVisibility>(); visibility.Configure(All<PlayerCharacterController>(scene).Single(), name == "CombatTwoFloorsLab");
                        if (All<ChaseSceneEntrance>(scene).Length == 0) AddFlatEntrance(scene, waves);
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); log.AppendLine(name + " repaired enemy references=" + changed + "; renderer gating=" + (waves != null));
                }
                finally { SceneManager.SetActiveScene(previous); if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets(); File.WriteAllText(Report + "/applied.txt", log.ToString());
        }
        static void AddFlatEntrance(Scene scene, CombatTwoRoomsLab waves)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Environment/Bunker/Bunker_Hazard.mat");
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube); door.name = "Sector03Door"; door.transform.position = new Vector3(0, 3.5f, 82); door.transform.localScale = new Vector3(8, 7, .5f); door.GetComponent<Renderer>().sharedMaterial = material;
            var trigger = new GameObject("Sector03Entrance", typeof(BoxCollider), typeof(Rigidbody), typeof(ChaseSceneEntrance)); trigger.transform.position = new Vector3(0, 2, 81); trigger.GetComponent<BoxCollider>().size = new Vector3(8, 4, 1); trigger.GetComponent<BoxCollider>().isTrigger = true; trigger.GetComponent<Rigidbody>().isKinematic = true; trigger.GetComponent<Rigidbody>().useGravity = false; trigger.GetComponent<ChaseSceneEntrance>().Configure(waves, door);
            var label = new GameObject("Sector03Sign").AddComponent<TMPro.TextMeshPro>(); label.font = All<TMPro.TMP_Text>(scene).First(t => t.font).font; label.text = "SECTOR 03\nPERSECUCIÓN"; label.fontSize = 4; label.alignment = TMPro.TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(8, 3); label.transform.position = new Vector3(0, 5.3f, 81.6f);
        }
    }
}
