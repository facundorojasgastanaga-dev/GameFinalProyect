using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class ShredderT07RepairAuthoring
    {
        public const string Report = "Tools/IronRequiem/Reports/ShredderT07Repair";
        public const string FixedT07 = "Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab";
        const string Queue = "Tools/IronRequiem/Requests/shredder-t07-repair.txt";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static ShredderT07RepairAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue) || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/ShredderT07RepairAuthoring.cs") > Loaded) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { if (op == "shredder") Shredder(); else if (op == "prepare") PrepareRobot(); else if (op == "promote") PromoteRobot(); else if (op == "open") EditorSceneManager.OpenScene("Assets/IronRequiem/Scenes/ChaseCorridorLab.unity"); else Inspect(); IronRequiemSetupTools.WriteAudit(); }
            catch (Exception e) { File.WriteAllText(Report + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static void Inspect()
        {
            var text = new StringBuilder();
            foreach (string path in new[] { "Assets/IronRequiem/Prefabs/Enemies/T07_HitFeedback_v04.prefab", "Assets/IronRequiem/Prefabs/CombatFollowup/T07_HitFeedback_v04.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var enemy = root.GetComponent<EnemyController>(); text.AppendLine(path + " health=" + root.GetComponent<Health>().MaxHealth + " charge=" + enemy.EnemyChargeDuration + " visual=" + root.GetComponent<EnemyCannonChargePresentation>());
                    foreach (var weapon in root.GetComponentsInChildren<WeaponController>(true)) text.AppendLine("gun=" + weapon.name + " muzzle=" + weapon.WeaponMuzzle.position + " scale=" + weapon.WeaponMuzzle.lossyScale + " projectile=" + AssetDatabase.GetAssetPath(weapon.ProjectilePrefab));
                    foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => new[] { "thigh", "shin", "foot", "leg", "calf" }.Any(s => t.name.ToLowerInvariant().Contains(s)))) text.AppendLine("joint " + t.name + " p=" + root.transform.InverseTransformPoint(t.position) + " local=" + t.localPosition + " scale=" + t.localScale + " active=" + t.gameObject.activeInHierarchy);
                    foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        text.AppendLine("skin=" + skin.name + " active=" + skin.gameObject.activeInHierarchy + " enabled=" + skin.enabled + " mesh=" + AssetDatabase.GetAssetPath(skin.sharedMesh) + " count=" + skin.sharedMesh.vertexCount + " bounds=" + skin.localBounds + " bones=" + string.Join(",", skin.bones.Select(b => b ? b.name : "NULL")));
                        text.AppendLine(" rendererTransform=" + skin.transform.position + ":" + skin.transform.eulerAngles + ":" + skin.transform.lossyScale + " worldBounds=" + skin.bounds);
                        if (skin.sharedMesh.isReadable && skin.name.StartsWith("Body_LOD"))
                        {
                            var raw = skin.sharedMesh.vertices.Select(v => root.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray(); var bounds = new Bounds(raw[0], Vector3.zero); foreach (var v in raw) bounds.Encapsulate(v); text.AppendLine(" raw rest world=" + bounds);
                            var bind = skin.sharedMesh.bindposes; for (int i = 0; i < skin.bones.Length; i++) { var expected = skin.bones[i].worldToLocalMatrix * skin.transform.localToWorldMatrix; float delta = 0; for (int j = 0; j < 16; j++) delta = Mathf.Max(delta, Mathf.Abs(expected[j] - bind[i][j])); text.AppendLine(" bindDelta " + skin.bones[i].name + "=" + delta); }
                        }
                        var baked = new Mesh(); skin.BakeMesh(baked); var verts = baked.vertices.Select(v => root.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                        int left = verts.Count(v => v.y < .9f && v.y > -.5f && v.x < -.03f), right = verts.Count(v => v.y < .9f && v.y > -.5f && v.x > .03f);
                        text.AppendLine(" baked legs L=" + left + " R=" + right + " bounds=" + baked.bounds); Object.DestroyImmediate(baked);
                        if (skin.sharedMesh.isReadable)
                        {
                            var weights = skin.sharedMesh.boneWeights;
                            for (int i = 0; i < skin.bones.Length; i++) { int index = i; int count = weights.Count(w => w.boneIndex0 == index && w.weight0 > .1f || w.boneIndex1 == index && w.weight1 > .1f || w.boneIndex2 == index && w.weight2 > .1f || w.boneIndex3 == index && w.weight3 > .1f); text.AppendLine("  " + (skin.bones[i] ? skin.bones[i].name : "NULL") + " weighted=" + count); }
                        }
                    }
                    foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) text.AppendLine("LOD=" + lod.name + " levels=" + string.Join(" / ", lod.GetLODs().Select(l => string.Join(",", l.renderers.Select(r => r ? r.name : "NULL")))));
                    var animator = root.GetComponentInChildren<Animator>(); foreach (var clip in animator.runtimeAnimatorController.animationClips.Distinct()) foreach (var b in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.Contains("Scale"))) text.AppendLine("scale curve=" + clip.name + ":" + b.path + ":" + b.propertyName);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            File.WriteAllText(Report + "/inspection.txt", text.ToString());
        }
        static void Shredder()
        {
            const string path = "Assets/IronRequiem/Scenes/ChaseCorridorLab.unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); var previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
            try
            {
                var chase = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ChaseCorridorEncounter>(true)).Single(); var data = new SerializedObject(chase); var carriage = (Transform)data.FindProperty("carriage").objectReferenceValue;
                if (carriage.Find("ShredderAssembly")) return;
                var old = data.FindProperty("blades"); var reference = ((Transform)old.GetArrayElementAtIndex(0).objectReferenceValue).GetComponent<MeshRenderer>(); var material = reference.sharedMaterial; var mesh = reference.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < old.arraySize; i++) ((Transform)old.GetArrayElementAtIndex(i).objectReferenceValue).gameObject.SetActive(false);
                var assembly = new GameObject("ShredderAssembly").transform; assembly.SetParent(carriage, false); var rotors = new Transform[2];
                for (int row = 0; row < 2; row++)
                {
                    var rotor = new GameObject(row == 0 ? "LowerRoller" : "UpperRoller").transform; rotor.SetParent(assembly, false); rotor.localPosition = new Vector3(0, row == 0 ? 1.85f : 5.05f, 0); rotor.localRotation = Quaternion.Euler(0, 90, 0); rotors[row] = rotor;
                    var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder); drum.name = "SteelDrum"; drum.transform.SetParent(rotor, false); drum.transform.localRotation = Quaternion.Euler(90, 0, 0); drum.transform.localScale = new Vector3(2.6f, 7.7f, 2.6f); Object.DestroyImmediate(drum.GetComponent<Collider>()); drum.GetComponent<Renderer>().sharedMaterial = material;
                    int count = row == 0 ? 10 : 9; float start = row == 0 ? -7.2f : -6.4f;
                    for (int i = 0; i < count; i++)
                    {
                        var ring = new GameObject("ToothedRing_" + i, typeof(MeshFilter), typeof(MeshRenderer)); ring.transform.SetParent(rotor, false); ring.transform.localPosition = new Vector3(0, 0, start + 1.6f * i); ring.transform.localScale = new Vector3(.58f, .58f, 2.2f); ring.GetComponent<MeshFilter>().sharedMesh = mesh; ring.GetComponent<MeshRenderer>().sharedMaterial = material;
                    }
                }
                var beam = carriage.Find("MachineFrame"); if (beam) beam.localPosition = new Vector3(0, 7.2f, 0);
                for (int i = 0; i < 2; i++) { var frame = GameObject.CreatePrimitive(PrimitiveType.Cube); frame.name = "ShredderSideFrame"; frame.transform.SetParent(assembly, false); frame.transform.localPosition = new Vector3(i == 0 ? -7.85f : 7.85f, 3.65f, 0); frame.transform.localScale = new Vector3(.25f, 7.2f, .65f); frame.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(frame.GetComponent<Collider>()); }
                old.arraySize = 2; for (int i = 0; i < 2; i++) old.GetArrayElementAtIndex(i).objectReferenceValue = rotors[i]; data.FindProperty("counterRotateBlades").boolValue = true; data.FindProperty("sawContactOffset").floatValue = 2.1f; data.ApplyModifiedPropertiesWithoutUndo();
                Directory.CreateDirectory("Assets/IronRequiem/Prefabs/Levels"); AssetDatabase.Refresh(); PrefabUtility.SaveAsPrefabAsset(carriage.gameObject, "Assets/IronRequiem/Prefabs/Levels/GiantShredder.prefab"); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Report + "/shredder.txt", "Two horizontal X-axis drums,19 interleaved toothed rings, counterrotation into the nip. Shared original mesh/material, old three blades inactive/preserved. Contact front2.1m matches1.8m tooth radius plus player capsule. Speed8m/s, warning2s and damage unchanged; no extra colliders or lights.");
            }
            finally { SceneManager.SetActiveScene(previous); if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        static void PrepareRobot()
        {
            const string modelPath = "Assets/IronRequiem/Art/Enemies/T07LegRepair/T07_LegInteriors.fbx";
            if (File.Exists(FixedT07)) throw new Exception("Preserving delivered robot variant");
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport); var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath); var originalImporter = (ModelImporter)AssetImporter.GetAtPath("Assets/IronRequiem/Art/Enemies/T07Destructible/T07_SeverableArm.fbx");
            importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = false; importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.optimizeGameObjects = false; importer.isReadable = true; importer.globalScale = originalImporter.globalScale; importer.bakeAxisConversion = originalImporter.bakeAxisConversion; importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath); var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/T07_HitFeedback_v04.prefab"), preview); root.name = "T07_LegRepair_Charged_v05";
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("Body_LOD")))
                {
                    var imported = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == skin.name); var bones = skin.bones.ToDictionary(b => b.name); skin.sharedMesh = imported.sharedMesh; skin.bones = imported.bones.Select(b => bones[b.name]).ToArray();
                    if (skin.sharedMaterials.Length != skin.sharedMesh.subMeshCount) throw new Exception("Preserve body material slots: " + skin.name);
                }
                var enemy = root.GetComponent<EnemyController>(); enemy.EnemyChargeDuration = 1.2f;
                var gun = root.GetComponentsInChildren<WeaponController>(true).First();
                var effect = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CannonChargeUpgrade.ChargePrefab), gun.WeaponMuzzle); effect.name = "T07_EnemyCharge"; effect.transform.localPosition = Vector3.zero; effect.transform.localRotation = Quaternion.identity; effect.transform.localScale = Vector3.one;
                root.AddComponent<EnemyCannonChargePresentation>().Configure(enemy, effect);
                PrefabUtility.SaveAsPrefabAsset(root, FixedT07);
                File.WriteAllText(Report + "/robot-prepared.txt", "New variant of the actual160HP T07; existing damage/projectile/speed/range,17 bones, UVs, armor and detachable arm retained. Three copied body skins with measured leg-port interior caps (+69/+44/+23 triangles). Native charge1.2s and existing magenta presentation at actual muzzle; no alternate AI or animation approximations.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            // The first diagnostic export contained no new faces; retain its bytes outside Assets only.
            const string diagnostic = "Assets/IronRequiem/Art/Enemies/T07LegRepair/T07_LegsClosed.fbx";
            if (File.Exists(diagnostic)) { File.Copy(diagnostic, "Tools/IronRequiem/Backups/20261107-shredder-t07/T07_LegsClosed-diagnostic.fbx", true); AssetDatabase.DeleteAsset(diagnostic); }
        }
        static void PromoteRobot()
        {
            var before = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/T07_HitFeedback_v04.prefab"); var after = AssetDatabase.LoadAssetAtPath<GameObject>(FixedT07); if (!after) throw new Exception("Validate the robot variant first"); var log = new StringBuilder();
            foreach (string name in new[] { "ChaseCorridorLab", "CombatTwoFloorsLab", "CombatTwoRoomsLab", "SawBotLab", "CombatVarietyLab", "BunkerLab", "SawBotSustainLab", "SawBotWeaponDeathLab", "SawBotSawPolishLab", "WeaponHeatBalanceLab" })
            {
                string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); int changes = 0;
                try
                {
                    foreach (var component in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c is CombatTwoRoomsLab || c is ChaseCorridorEncounter))
                    {
                        var data = new SerializedObject(component); var p = data.GetIterator(); int count = 0; while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == before) { p.objectReferenceValue = after; count++; }
                        if (count > 0) { data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(component); if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component); changes += count; }
                    }
                    if (changes > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); } log.AppendLine(name + ": replaced=" + changes);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Report + "/promotion.txt", log.ToString());
        }
    }
}
