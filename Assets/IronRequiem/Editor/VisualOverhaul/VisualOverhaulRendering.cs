using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026): a second Forward renderer (copy of the project renderer + screen-space URP decals)
    // appended to the pipeline asset's renderer list. Only cameras of the visual-overhaul scenes select it, so every
    // other scene keeps renderer 0 unchanged. Backup of the pipeline asset before the change.
    public static class VisualOverhaulRendering
    {
        public const string Pipeline = "Assets/Rendering/Default_PipelineAsset.asset";
        public const string SourceRenderer = "Assets/Rendering/Default_PipelineAsset_ForwardRenderer.asset";
        public static string RendererPath => VisualOverhaulAssets.Root + "/Rendering/VO_ForwardRenderer.asset";

        public static int EnsureRenderer()
        {
            string backup = "Tools/IronRequiem/Backups/20261107-visual-overhaul/02-rendering";
            if (!File.Exists(backup + "/Default_PipelineAsset.asset")) { Directory.CreateDirectory(backup); File.Copy(Pipeline, backup + "/Default_PipelineAsset.asset"); File.Copy(Pipeline + ".meta", backup + "/Default_PipelineAsset.asset.meta"); }
            Directory.CreateDirectory(Path.GetDirectoryName(RendererPath));
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (!data)
            {
                if (!AssetDatabase.CopyAsset(SourceRenderer, RendererPath)) throw new Exception("Cannot copy renderer");
                data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            }
            if (!data.rendererFeatures.OfType<DecalRendererFeature>().Any())
            {
                var feature = ScriptableObject.CreateInstance<DecalRendererFeature>(); feature.name = "VO_Decals";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures"); list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                var map = so.FindProperty("m_RendererFeatureMap"); map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                var fs = new SerializedObject(feature);
                fs.FindProperty("m_Settings.technique").enumValueIndex = 2;          // ScreenSpace: no depth-normals prepass, no DBuffer keywords
                fs.FindProperty("m_Settings.maxDrawDistance").floatValue = 60;
                fs.FindProperty("m_Settings.screenSpaceSettings.normalBlend").enumValueIndex = 0; // Low (1 sample)
                fs.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
            var pso = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline));
            var renderers = pso.FindProperty("m_RendererDataList"); int index = -1;
            for (int i = 0; i < renderers.arraySize; i++) if (renderers.GetArrayElementAtIndex(i).objectReferenceValue == data) index = i;
            if (index < 0) { renderers.arraySize++; index = renderers.arraySize - 1; renderers.GetArrayElementAtIndex(index).objectReferenceValue = data; pso.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssets();
            return index;
        }

        // 08/11/2026 mesh decals: the variant scenes go back to the pipeline's default renderer (-1), the pipeline asset is
        // restored byte for byte from its backup and the unused VO renderer is quarantined (outside Assets).
        public static string UseDefaultOnCameras()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(VisualOverhaulScenes.Folder)) throw new Exception("Only variant scenes");
            int n = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { var data = cam.GetUniversalAdditionalCameraData(); if (!data || data.renderType != CameraRenderType.Base) continue; data.SetRenderer(-1); EditorUtility.SetDirty(data); n++; }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            return "cameras on default renderer: " + n;
        }

        public static string RestorePipeline()
        {
            string backup = "Tools/IronRequiem/Backups/20261107-visual-overhaul/02-rendering/Default_PipelineAsset.asset";
            File.Copy(backup, Pipeline, true); AssetDatabase.ImportAsset(Pipeline, ImportAssetOptions.ForceUpdate);
            string q = "Tools/IronRequiem/Quarantine/20261108-mesh-decals";
            if (File.Exists(RendererPath))
            {
                Directory.CreateDirectory(q); AssetDatabase.ReleaseCachedFileHandles();
                File.Move(RendererPath, q + "/VO_ForwardRenderer.asset"); File.Move(RendererPath + ".meta", q + "/VO_ForwardRenderer.asset.meta");
                AssetDatabase.Refresh();
            }
            bool same = File.ReadAllBytes(backup).SequenceEqual(File.ReadAllBytes(Pipeline));
            return "pipeline restored (identical to backup: " + same + "); VO renderer quarantined: " + File.Exists(q + "/VO_ForwardRenderer.asset");
        }

        // Select the decal renderer on every camera of the open scene (base and overlay cameras).
        public static void UseOnCameras(int index)
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData(); if (!data) continue;
                if (data.renderType == CameraRenderType.Base) { data.SetRenderer(index); EditorUtility.SetDirty(data); }
            }
        }
    }
}
