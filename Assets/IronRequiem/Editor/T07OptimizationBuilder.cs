using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // New variants and a dedicated lab only; original prefabs/scenes are never saved.
    [InitializeOnLoad]
    public static class T07OptimizationBuilder
    {
        public const string Art = "Assets/IronRequiem/Art/Enemies/T07Optimized";
        public const string Enemy = "Assets/IronRequiem/Prefabs/Enemies/T07_Common_Optimized.prefab";
        public const string Weapon = "Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab";
        public const string Lab = "Assets/IronRequiem/Scenes/T07OptimizationLab.unity";
        const string Reports = "Tools/IronRequiem/Reports/T07Optimization";
        static readonly Mesh[,] Meshes = new Mesh[3, 2];
        static T07OptimizationBuilder() { EditorApplication.update += CheckRequest; }
        static void CheckRequest()
        {
            const string request = "Tools/IronRequiem/Requests/t07-optimization.json";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string finalRequest = "Tools/IronRequiem/Requests/t07-finalize.json";
            if (File.Exists(finalRequest)) { File.Delete(finalRequest); FinalizeDelivery(); return; }
            if (!File.Exists(request)) return;
            File.Delete(request);
            Build();
        }

        [Serializable] sealed class MeshRecord { public int level, vertices, submeshes; public long triangles; public string part; public Bounds bounds; }
        [Serializable] sealed class Result
        {
            public bool success; public string unity, utc, error, enemy, weapon, lab;
            public MeshRecord[] meshes; public int missingScripts, bodyLodGroups, cannonLodGroups;
            public bool variant, independentCannon, sharedMaterial, preservedTransforms;
            public long[] trianglesByLod; public long originalTriangles;
            public bool[] sceneDirtyBefore, sceneDirtyAfter;
        }

        [MenuItem("Tools/Iron Requiem/Optimization/Build Common T07 LOD Variants")]
        public static void Build()
        {
            var r = new Result { unity = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), enemy = Enemy, weapon = Weapon, lab = Lab };
            var setup = EditorSceneManager.GetSceneManagerSetup();
            r.sceneDirtyBefore = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).isDirty).ToArray();
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
                Require(r.sceneDirtyBefore.All(d => !d), "User scenes are dirty; do not save or replace them.");
                Require(!File.Exists(Enemy) && !File.Exists(Weapon) && !File.Exists(Lab), "Never overwrite delivered variants.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var records = new List<MeshRecord>();
                for (int level = 0; level < 3; level++)
                {
                    string path = Art + "/T07_LOD" + level + ".fbx";
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    Require(importer != null, "Missing LOD importer: " + path);
                    // Configure these new model copies only.
                    importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
                    importer.meshCompression = ModelImporterMeshCompression.Off; importer.isReadable = false;
                    importer.SaveAndReimport();
                    var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                    Require(meshes.Length == 2, "Each LOD needs independent body and cannon.");
                    for (int part = 0; part < 2; part++)
                    {
                        string name = (part == 0 ? "Body" : "CannonModule") + "_LOD" + level;
                        var mesh = meshes.Single(m => m.name == name); Meshes[level, part] = mesh;
                        Require(mesh.bindposeCount == 0 && mesh.subMeshCount == 1, "Unexpected skin or submesh layout.");
                        records.Add(new MeshRecord { level = level, part = name, vertices = mesh.vertexCount, triangles = Triangles(mesh), submeshes = mesh.subMeshCount, bounds = mesh.bounds });
                    }
                }
                r.meshes = records.ToArray();
                r.trianglesByLod = Enumerable.Range(0, 3).Select(l => Triangles(Meshes[l, 0]) + Triangles(Meshes[l, 1])).ToArray();
                r.originalTriangles = AssetDatabase.LoadAllAssetsAtPath("Assets/IronRequiem/Art/Enemies/T07New/T07_Prepared.fbx").OfType<Mesh>().Sum(Triangles);
                Require(r.trianglesByLod[0] < 13000 && r.trianglesByLod[1] < 6500 && r.trianglesByLod[2] < 2800, "LOD triangle budgets failed.");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Enemies/T07New/Materials/T07_URP.mat");
                Require(material && material.shader.name == "Universal Render Pipeline/Lit", "Existing URP material missing.");
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MechanicsLabUpgrade.CannonPrefab), preview);
                    weapon.name = "Weapon_T07_Temporary_Optimized";
                    var held = weapon.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "StolenCannonVisual");
                    VerifyCompatible(held.sharedMesh, Meshes[0, 1]); held.sharedMesh = Meshes[0, 1];
                    PrefabUtility.SaveAsPrefabAsset(weapon, Weapon); Object.DestroyImmediate(weapon);

                    var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.EnemyPath), preview);
                    enemy.name = "T07_Common_Optimized";
                    var body = enemy.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Body");
                    var cannon = enemy.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "CannonModule_Visual");
                    var bodyTrs = body.transform.localToWorldMatrix; var cannonTrs = cannon.transform.localToWorldMatrix;
                    VerifyCompatible(body.sharedMesh, Meshes[0, 0]); VerifyCompatible(cannon.sharedMesh, Meshes[0, 1]);
                    AddLods(body, 0, material, new[] { .35f, .12f, .006f });
                    AddLods(cannon, 1, material, new[] { .18f, .06f, .003f });
                    // This inactive source object exists in the inherited model; use the reduced copy too.
                    foreach (var inactive in enemy.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name == "CannonModule")) inactive.sharedMesh = Meshes[0, 1];
                    var module = cannon.GetComponent<StolenCannon>();
                    Require(module, "Existing modular gameplay must remain attached to the cannon root.");
                    module.Configure(AssetDatabase.LoadAssetAtPath<WeaponController>(Weapon));
                    r.preservedTransforms = body.transform.localToWorldMatrix == bodyTrs && cannon.transform.localToWorldMatrix == cannonTrs;
                    r.independentCannon = !cannon.transform.IsChildOf(body.transform) && module.GetComponent<LODGroup>();
                    r.bodyLodGroups = body.GetComponents<LODGroup>().Length; r.cannonLodGroups = cannon.GetComponents<LODGroup>().Length;
                    r.sharedMaterial = body.GetComponent<Renderer>().sharedMaterial == cannon.GetComponent<Renderer>().sharedMaterial;
                    r.missingScripts = enemy.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    Require(r.preservedTransforms && r.independentCannon && r.sharedMaterial && r.missingScripts == 0, "Variant safety checks failed.");
                    PrefabUtility.SaveAsPrefabAsset(enemy, Enemy); Object.DestroyImmediate(enemy);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
                r.variant = PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy)) == PrefabAssetType.Variant;
                Require(r.variant, "Enemy must remain a variant.");
                CreateLab();
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Tools/IronRequiem/Exports");
                AssetDatabase.ExportPackage(new[] { Art, Enemy, Weapon }, "Tools/IronRequiem/Exports/T07_Common_Optimized.unitypackage", ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
                r.success = true;
            }
            catch (Exception e) { r.error = e.ToString(); Debug.LogException(e); }
            finally
            {
                if (setup.Any(s => s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                r.sceneDirtyAfter = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).isDirty).ToArray();
                Directory.CreateDirectory(Reports); File.WriteAllText(Reports + "/unity.json", JsonUtility.ToJson(r, true));
            }
            if (!r.success) throw new InvalidOperationException(r.error);
        }

        static void AddLods(MeshFilter filter, int part, Material material, float[] heights)
        {
            var renderers = new Renderer[3];
            filter.sharedMesh = Meshes[0, part]; renderers[0] = filter.GetComponent<MeshRenderer>();
            renderers[0].sharedMaterial = material;
            for (int i = 1; i < 3; i++)
            {
                var child = new GameObject("LOD" + i); child.transform.SetParent(filter.transform, false);
                child.layer = filter.gameObject.layer;
                child.AddComponent<MeshFilter>().sharedMesh = Meshes[i, part];
                var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = renderers[0].shadowCastingMode; renderer.receiveShadows = renderers[0].receiveShadows;
                renderers[i] = renderer;
            }
            var group = filter.gameObject.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None;
            group.SetLODs(Enumerable.Range(0, 3).Select(i => new LOD(heights[i], new[] { renderers[i] })).ToArray());
            group.RecalculateBounds();
        }

        static void VerifyCompatible(Mesh original, Mesh reduced)
        {
            // The same transforms may be reused only if both meshes use the same local units and origin.
            float size = original.bounds.size.magnitude;
            Require(Vector3.Distance(original.bounds.center, reduced.bounds.center) < size * .05f, "Mesh pivot changed.");
            Require(Vector3.Distance(original.bounds.size, reduced.bounds.size) < size * .08f, "Mesh units or dimensions changed.");
        }

        static void CreateLab()
        {
            Require(AssetDatabase.CopyAsset(CharacterPrototypeBuilder.ScenePath, Lab), "Cannot create separate test scene.");
            var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Single);
            var old = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyController>(true)).Single();
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy), scene);
            replacement.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation); replacement.transform.localScale = old.transform.localScale;
            foreach (var path in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PatrolPath>(true)))
                for (int i = 0; i < path.EnemiesToAssign.Count; i++)
                    if (path.EnemiesToAssign[i] == old) path.EnemiesToAssign[i] = replacement.GetComponent<EnemyController>();
            Object.DestroyImmediate(old.gameObject);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the new lab.");
        }

        public static long Triangles(Mesh mesh) => Enumerable.Range(0, mesh.subMeshCount).Sum(i => (long)mesh.GetIndexCount(i) / 3);

        [MenuItem("Tools/Iron Requiem/Optimization/Validate and Export T07 Delivery")]
        public static void FinalizeDelivery()
        {
            var r = JsonUtility.FromJson<Result>(File.ReadAllText(Reports + "/unity.json"));
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                r.utc = DateTime.UtcNow.ToString("O");
                foreach (var record in r.meshes)
                {
                    var mesh = AssetDatabase.LoadAllAssetsAtPath(Art + "/T07_LOD" + record.level + ".fbx").OfType<Mesh>().Single(m => m.name == record.part);
                    record.triangles = Triangles(mesh); record.vertices = mesh.vertexCount; record.bounds = mesh.bounds;
                    record.submeshes = mesh.subMeshCount;
                }
                r.trianglesByLod = Enumerable.Range(0, 3).Select(l => r.meshes.Where(m => m.level == l).Sum(m => m.triangles)).ToArray();
                var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy);
                var groups = enemy.GetComponentsInChildren<LODGroup>(true);
                Require(groups.Length == 2 && groups.All(g => g.GetLODs().Length == 3), "LOD groups are missing.");
                foreach (var group in groups)
                    for (int i = 0; i < 3; i++)
                        Require(AssetDatabase.GetAssetPath(group.GetLODs()[i].renderers.Single().GetComponent<MeshFilter>().sharedMesh) == Art + "/T07_LOD" + i + ".fbx", "LOD mesh reference broke after reimport.");
                AssetDatabase.ExportPackage(new[] { Art, Enemy, Weapon, Lab }, "Tools/IronRequiem/Exports/T07_Common_Optimized.unitypackage", ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
                r.success = true; r.error = "";
            }
            catch (Exception e) { r.success = false; r.error = e.ToString(); Debug.LogException(e); }
            File.WriteAllText(Reports + "/unity-final.json", JsonUtility.ToJson(r, true));
        }
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    }
}
