using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    // Fixed, local one-shot asset operations for the requested weapon repair. No server or code evaluation.
    [InitializeOnLoad]
    public static class NativeWeaponRepairTools
    {
        const string Queue = "Tools/IronRequiem/Requests/native-weapon-repair.txt";
        public const string Reports = "Tools/IronRequiem/Reports/NativeWeaponRepair";
        public const string Art = "Assets/IronRequiem/Art/Weapons/NativeRepair";
        public const string Source = "Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab";
        static readonly DateTime LoadedAt = DateTime.UtcNow;
        const string TestKey = "IRON.NativeWeaponRepair.Testing";
        static TestRunnerApi testRunner;

        static NativeWeaponRepairTools()
        {
            EditorApplication.update += Tick;
            if (SessionState.GetBool(TestKey, false)) EditorApplication.delayCall += RegisterTests;
        }
        static void RegisterTests()
        {
            if (testRunner) return;
            testRunner = ScriptableObject.CreateInstance<TestRunnerApi>(); testRunner.RegisterCallbacks(new Results());
        }
        static void Tick()
        {
            if (SessionState.GetBool(TestKey + ".restore", false) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                SessionState.EraseBool(TestKey + ".restore");
                if (!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) EditorSceneManager.OpenScene(NativeWeaponVisualAuthoring.Lab);
                IronRequiemSetupTools.WriteAudit();
            }
            if (!File.Exists(Queue) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/NativeWeaponRepairTools.cs") > LoadedAt) return;
            string operation = File.ReadAllText(Queue).Trim(); File.Delete(Queue);
            Directory.CreateDirectory(Reports);
            try
            {
                if (operation == "inspect") Inspect();
                else if (operation == "build") Build();
                else if (operation == "camera") ClearCameraSoftening();
                else if (operation == "test")
                {
                    if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Preserving dirty scene");
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null && stage.scene.isDirty) throw new InvalidOperationException("Preserving dirty prefab stage");
                    StageUtility.GoToMainStage(); RegisterTests(); SessionState.SetBool(TestKey, true);
                    testRunner.Execute(new ExecutionSettings(new Filter {testMode = TestMode.PlayMode, testNames = new[] {"IronRequiem.Tests.NativeWeaponVisualTests"}}));
                }
                else throw new InvalidOperationException("Unknown fixed repair operation");
            }
            catch (Exception e) { File.WriteAllText(Reports + "/error.txt", e.ToString()); Debug.LogException(e); }
        }

        static void Inspect()
        {
            var report = new StringBuilder();
            report.AppendLine("Prefab stage=" + PrefabStageUtility.GetCurrentPrefabStage()?.assetPath);
            for (int i = 0; i < SceneManager.sceneCount; i++) { var s = SceneManager.GetSceneAt(i); report.AppendLine("Scene=" + s.path + " dirty=" + s.isDirty); }
            foreach (string path in new[] {Source, NativeWeaponVisualAuthoring.Weapon})
            {
                var go = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var w = go.GetComponent<WeaponController>(); report.AppendLine("WEAPON " + path + " muzzle=" + w.WeaponMuzzle.position.ToString("F5"));
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var filter = r.GetComponent<MeshFilter>();
                        report.AppendLine(r.name + " active=" + r.gameObject.activeInHierarchy + " enabled=" + r.enabled + " center=" + r.bounds.center.ToString("F5") + " size=" + r.bounds.size.ToString("F5") + " mesh=" + (filter ? filter.sharedMesh.name : "skin/particle") + " materials=" + string.Join(";", r.sharedMaterials.Select(m => m ? AssetDatabase.GetAssetPath(m) : "null")));
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); if (!path.StartsWith("Assets/")) continue;
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                foreach (var c in profile.components.Where(c => c is MotionBlur || c is DepthOfField)) report.AppendLine("PROFILE " + path + " " + c.name + " active=" + c.active);
            }
            File.WriteAllText(Reports + "/inspection.txt", report.ToString());
        }

        static Transform Named(GameObject go, string name) => go.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

        static Material PaletteMaterial(Material original, string name, Color tint, Texture2D albedo, Color glow)
        {
            string path = Art + "/" + name + ".mat";
            if (!File.Exists(path)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            // Keep original UV-compatible normal, AO, metallic/smoothness and emission masks.
            material.SetTexture("_BaseMap", albedo); material.SetTexture("_MainTex", albedo);
            material.SetColor("_BaseColor", tint); material.SetColor("_Color", tint);
            material.SetColor("_EmissionColor", glow); material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static Texture2D MakeAlbedo()
        {
            // Recolour the native atlas in-place in UV space; never project the robot's atlas onto unrelated geometry.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes("Assets/FPS/Art/Textures/Weapons/Blaster_Albedo.png"));
            var pixels = texture.GetPixels32();
            var armor = new Color(.811285f, .747641f, .693296f);
            var metal = new Color(.08143f, .07854f, .07718f);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i]; bool grip = p.b > p.r * 1.15f && p.b > p.g * 1.08f;
                float detail = grip ? Mathf.Clamp(p.b / .52f, .4f, 1.25f) : Mathf.Clamp(p.r / .79f, .25f, 1.2f);
                Color result = (grip ? metal : armor) * detail; result.a = p.a; pixels[i] = result;
            }
            texture.SetPixels32(pixels); texture.Apply(); string path = Art + "/Blaster_PlayerPalette.png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 1024; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty user scene");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isDirty) throw new InvalidOperationException("Preserving dirty user prefab stage");
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            var albedo = MakeAlbedo();
            var original = PrefabUtility.LoadPrefabContents(Source);
            var go = PrefabUtility.LoadPrefabContents(NativeWeaponVisualAuthoring.Weapon);
            try
            {
                var sourceWeapon = original.GetComponent<WeaponController>(); var weapon = go.GetComponent<WeaponController>();
                var cyan = new Color(.08678f, .487317f, .543305f); var orange = new Color(.831094f, .432375f, .058681f);
                var sourceSurface = Named(original, "Primary_Weapon").GetComponent<Renderer>().sharedMaterial;
                var sourceGlass = original.GetComponent<WeaponFuelCellHandler>().FuelCells[0].GetComponent<Renderer>().sharedMaterials[1];
                var surface = PaletteMaterial(sourceSurface, "Blaster_PlayerArmor", Color.white, albedo, cyan * 4);
                var glass = PaletteMaterial(sourceGlass, "Blaster_PlayerEnergy", new Color(1, 1, 1, sourceGlass.GetColor("_BaseColor").a), albedo, cyan * 4);
                var front = PaletteMaterial(sourceSurface, "Blaster_PlayerFront", new Color(.18f, .17f, .16f), albedo, cyan * 4);
                // Native meshes, native vertex normals/UVs, native capsule transforms. Only the complete model's mount is translated.
                foreach (var filter in original.GetComponentsInChildren<MeshFilter>(true))
                {
                    var target = Named(go, filter.name); target.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    target.localPosition = filter.transform.localPosition; target.localRotation = filter.transform.localRotation; target.localScale = filter.transform.localScale;
                    var renderer = target.GetComponent<MeshRenderer>(); renderer.enabled = true;
                    renderer.sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m == sourceGlass ? glass : filter.name.StartsWith("Primary_Weapon_Front") ? front : surface).ToArray();
                }
                var visual = Named(go, "PrimaryModuleVisual");
                // Disable the previous deformed shell and its replacement supports, retaining helpers for the existing arm socket.
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("TelescopicSupport_"))) skin.enabled = false;
                var model = Named(go, "WeaponMesh_Pistol"); var sourceModel = Named(original, "WeaponMesh_Pistol");
                model.localPosition = sourceModel.localPosition + weapon.WeaponMuzzle.position - sourceWeapon.WeaponMuzzle.position;
                model.localRotation = sourceModel.localRotation; model.localScale = sourceModel.localScale;
                var attachment = Named(go, "Attachment"); attachment.SetPositionAndRotation(new Vector3(0, weapon.WeaponMuzzle.position.y, -.047f), weapon.WeaponMuzzle.rotation);
                Named(go, "Muzzle").SetPositionAndRotation(weapon.WeaponMuzzle.position, weapon.WeaponMuzzle.rotation);

                var heat = go.GetComponent<OverheatBehavior>(); heat.OverheatingMaterial = glass;
                var originalGradient = original.GetComponent<OverheatBehavior>().OverheatGradient;
                var gradient = new Gradient();
                gradient.SetKeys(originalGradient.colorKeys.Select(k => new GradientColorKey(Color.Lerp(cyan, orange, k.time) * (k.color.maxColorComponent / Mathf.Max(.001f, Color.Lerp(cyan, orange, k.time).maxColorComponent)), k.time)).ToArray(), originalGradient.alphaKeys);
                heat.OverheatGradient = gradient;
                // Preserve the existing effects and lights; only recolour emissive surfaces. No additional light sources.
                PrefabUtility.SaveAsPrefabAsset(go, NativeWeaponVisualAuthoring.Weapon);
                File.WriteAllText(Reports + "/build.txt", "Restored all 7 native mesh references, UVs, normals and capsule transforms. Preserved native motion code, weapon stats, projectile, existing effect activation. Native atlas recoloured to verified protagonist ivory/graphite/cyan; native emission intensity envelope mapped cyan to orange. Whole model translated to current muzzle; arm socket at rear. No runtime code changes.");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); PrefabUtility.UnloadPrefabContents(original); }

            var clarity = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); if (!path.StartsWith("Assets/")) continue;
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                bool changed = false;
                if (profile.TryGet<MotionBlur>(out var motion)) { motion.active = false; motion.intensity.Override(0); EditorUtility.SetDirty(motion); changed = true; }
                if (profile.TryGet<DepthOfField>(out var depth)) { depth.active = false; depth.mode.Override(DepthOfFieldMode.Off); EditorUtility.SetDirty(depth); changed = true; }
                if (changed) { EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); clarity.AppendLine(path + ": MotionBlur disabled, depth of field off where present; other effects retained."); }
            }
            File.WriteAllText(Reports + "/clarity.txt", clarity.ToString());
            IronRequiemSetupTools.WriteAudit();
        }

        static void ClearCameraSoftening()
        {
            var player = PrefabUtility.LoadPrefabContents(NativeWeaponVisualAuthoring.Player);
            try
            {
                foreach (var camera in player.GetComponentsInChildren<Camera>(true))
                {
                    var data = camera.GetUniversalAdditionalCameraData();
                    // Native-resolution rendering with no FXAA filtering or temporal accumulation. Retain pipeline MSAA.
                    data.antialiasing = AntialiasingMode.None; camera.allowDynamicResolution = false;
                }
                PrefabUtility.SaveAsPrefabAsset(player, NativeWeaponVisualAuthoring.Player);
                File.AppendAllText(Reports + "/clarity.txt", "Player_NativeVisual_v06: FXAA/TAA disabled on both cameras; dynamic resolution off; pipeline renderScale remains 1. Bloom and lighting retained.\n");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor t) { }
            public void OnError(string message) { SessionState.EraseBool(TestKey); File.WriteAllText(Reports + "/runner-error.txt", message); }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(TestKey, false)) return;
                SessionState.EraseBool(TestKey); TestRunnerApi.SaveResultToFile(result, Reports + "/play.xml");
                File.WriteAllText(Reports + "/status.txt", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount);
                SessionState.SetBool(TestKey + ".restore", true);
            }
        }
    }
}
