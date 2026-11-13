using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.Editor
{
    // One-shot creation of new visual assets only; never edits a gameplay prefab or user scene.
    [InitializeOnLoad]
    public static class PistolModuleAssetBuilder
    {
        const string Folder = "Assets/IronRequiem/Art/Weapons/BasicPistolModule";
        const string Prefab = "Assets/IronRequiem/Prefabs/Weapons/Visuals/PistolModule_1000_Visual.prefab";
        const string Request = "Tools/IronRequiem/Requests/pistol-module.json";
        const string Report = "Tools/IronRequiem/Reports/pistol-module-unity.json";
        [Serializable] class Result
        {
            public bool success; public string utc; public string prefab; public string material;
            public int vertices; public long triangles; public int submeshes;
            public Vector3 dimensions; public Vector3 muzzleForward;
            public string error; public bool[] sceneDirtyBefore; public bool[] sceneDirtyAfter;
        }
        static PistolModuleAssetBuilder() { EditorApplication.update += Check; }
        static void Check()
        {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string exportRequest = "Tools/IronRequiem/Requests/pistol-module-export.json";
            if (File.Exists(exportRequest))
            {
                File.Delete(exportRequest);
                Directory.CreateDirectory("Tools/IronRequiem/Exports");
                AssetDatabase.ExportPackage(new[] { Folder, Prefab }, "Tools/IronRequiem/Exports/PistolModule_1000.unitypackage", ExportPackageOptions.Recurse);
            }
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            Build();
        }
        [MenuItem("Tools/Iron Requiem/Weapons/Build 1000 Vertex Pistol Visual")]
        public static void Build()
        {
            var r = new Result { utc = DateTime.UtcNow.ToString("O"), prefab = Prefab, material = Folder + "/PistolModule_URP.mat" };
            var preview = default(UnityEngine.SceneManagement.Scene);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
                r.sceneDirtyBefore = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty).ToArray();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) || File.Exists(r.material)) throw new InvalidOperationException("Do not overwrite existing delivered assets.");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PistolModule_1000.fbx");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/PistolModule_BaseColor.png");
                var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/PistolModule_Emission.png");
                if (!model || !texture || !emission) throw new InvalidOperationException("Import FBX and both atlas textures first.");
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("Existing URP shader unavailable.");
                var mat = new Material(shader) { name = "PistolModule_URP" };
                mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", .65f); mat.SetFloat("_Smoothness", .4f);
                mat.SetTexture("_EmissionMap", emission); mat.SetColor("_EmissionColor", Color.white * .7f); mat.EnableKeyword("_EMISSION");
                AssetDatabase.CreateAsset(mat, r.material);
                preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("PistolModule_1000_Visual");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                visual.transform.SetParent(root.transform, false);
                var muzzle = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Muzzle");
                var attachment = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Attachment");
                var correction = Quaternion.FromToRotation((muzzle.position - attachment.position).normalized, Vector3.forward);
                visual.transform.rotation = correction * visual.transform.rotation;
                visual.transform.position -= attachment.position;
                muzzle.rotation = Quaternion.identity; attachment.rotation = Quaternion.identity;
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = mat;
                var meshes = visual.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).Where(m => m).ToArray();
                r.vertices = meshes.Sum(m => m.vertexCount);
                r.triangles = meshes.Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i) / 3));
                r.submeshes = meshes.Sum(m => m.subMeshCount);
                var renderers = visual.GetComponentsInChildren<Renderer>(true); var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                r.dimensions = bounds.size; r.muzzleForward = (muzzle.position - attachment.position).normalized;
                if (r.vertices > 1000 || r.vertices == 0 || r.triangles == 0) throw new InvalidOperationException("Imported vertex budget failed: " + r.vertices);
                Directory.CreateDirectory(Path.GetDirectoryName(Prefab)); AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                Directory.CreateDirectory("Tools/IronRequiem/Exports");
                AssetDatabase.ExportPackage(new[] { Folder, Prefab }, "Tools/IronRequiem/Exports/PistolModule_1000.unitypackage", ExportPackageOptions.Recurse);
                r.sceneDirtyAfter = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty).ToArray();
                if (!r.sceneDirtyBefore.SequenceEqual(r.sceneDirtyAfter)) throw new InvalidOperationException("Unexpected scene dirty state change.");
                r.success = true;
            }
            catch (Exception e) { r.error = e.ToString(); }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, JsonUtility.ToJson(r, true));
            }
        }
    }
}
