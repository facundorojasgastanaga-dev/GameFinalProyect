using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class CombatV03Authoring
    {
        public const string Clarity="Assets/IronRequiem/Art/Rendering/CombatClarity.asset";
        public const string OldClarity="Assets/Rendering/PostProcessingProfile.asset";
        const string Model=T07DestructionBuilder.Art+"/T07_SeverableArm.fbx";
        public static void Run(string operation)
        {
            if(operation=="apply")Apply();
            else if(operation=="build")Build();
            else if(operation=="restore")RestoreBuildSettings();
            else throw new InvalidOperationException("Unknown V03 operation: "+operation);
        }
        static void RestoreBuildSettings()
        {
            // Build preprocessors serialize these fields even when a build is cancelled.
            PlayerSettings.enableFrameTimingStats=false;PlayerSettings.SetPreloadedAssets(Array.Empty<Object>());
            const string backup="Tools/IronRequiem/Backups/20261030-gameplay-performance-v03/";
            foreach(string path in new[]{"Assets/Rendering/Default_PipelineAsset.asset","ProjectSettings/ProjectSettings.asset","ProjectSettings/GraphicsSettings.asset"})
                File.Copy(backup+path,path,true);
            AssetDatabase.ImportAsset("Assets/Rendering/Default_PipelineAsset.asset",ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            if(SceneManager.GetActiveScene().path!=CombatV03Tools.Main)EditorSceneManager.OpenScene(CombatV03Tools.Main);
            IronRequiemSetupTools.WriteAudit();File.WriteAllText(CombatV03Tools.Output+"/restore.txt","Original URP/Graphics/Player configuration restored; MainScene open.");
        }
        static void Apply()
        {
            T07AnimationBuilder.RequireClean();Directory.CreateDirectory("Assets/IronRequiem/Art/Rendering");AssetDatabase.Refresh();
            if(!File.Exists(Clarity) && !AssetDatabase.CopyAsset(OldClarity,Clarity))throw new InvalidOperationException("Cannot copy original volume profile.");
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Clarity);
            if(profile.TryGet<MotionBlur>(out var blur))
            {blur.intensity.Override(.12f);blur.clamp.Override(.03f);EditorUtility.SetDirty(blur);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
            BuildArm();
            var scene=EditorSceneManager.OpenScene(CombatV03Tools.Main,OpenSceneMode.Single);
            var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerCharacterController>(true)).Single();
            if(!player.GetComponent<MechanicInput>())Undo.AddComponent<MechanicInput>(player.gameObject);
            var carrier=player.GetComponent<StolenWeaponCarrier>();if(!carrier)carrier=Undo.AddComponent<StolenWeaponCarrier>(player.gameObject);
            AddHud(scene,player,carrier);ApplyClarity(scene,profile);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save MainScene.");
            if(!File.Exists(CombatV03Tools.Lab) && !AssetDatabase.CopyAsset(T07DestructionBuilder.Lab,CombatV03Tools.Lab))throw new InvalidOperationException("Cannot copy performance lab.");
            scene=EditorSceneManager.OpenScene(CombatV03Tools.Lab,OpenSceneMode.Single);ApplyClarity(scene,profile);
            var probe=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CombatLoadProbe>(true)).FirstOrDefault();
            if(!probe)probe=new GameObject("CombatLoadProbe").AddComponent<CombatLoadProbe>();
            probe.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(T07DestructionBuilder.Enemy));
            EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(CombatV03Tools.Main);
            IronRequiemSetupTools.WriteAudit();File.WriteAllText(CombatV03Tools.Output+"/status.txt","apply SUCCESS: MainScene pickup + existing HUD, right arm at critical stage, cloned clarity profile and separate performance lab.");
        }
        static void ApplyClarity(Scene scene,VolumeProfile profile)
        {
            foreach(var volume in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Volume>(true)))
            {
                if(AssetDatabase.GetAssetPath(volume.sharedProfile)==OldClarity || volume.sharedProfile==profile)
                {Undo.RecordObject(volume,"Reduce combat blur");volume.sharedProfile=profile;PrefabUtility.RecordPrefabInstancePropertyModifications(volume);}
            }
            foreach(var data in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UniversalAdditionalCameraData>(true)))
                if(data.renderPostProcessing)
                {Undo.RecordObject(data,"Sharper combat antialiasing");data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.Medium;PrefabUtility.RecordPrefabInstancePropertyModifications(data);}
        }
        static void AddHud(Scene scene,PlayerCharacterController player,StolenWeaponCarrier carrier)
        {
            var hud=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MechanicHud>(true)).FirstOrDefault();
            if(!hud)
            {
                var canvas=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Unity.FPS.UI.WeaponHUDManager>(true)).First().AmmoPanel.GetComponentInParent<Canvas>();
                var sourceScene=EditorSceneManager.OpenScene(T07DestructionBuilder.Lab,OpenSceneMode.Additive);
                try
                {
                    var source=sourceScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MechanicHud>(true)).Single();
                    hud=Object.Instantiate(source.gameObject,canvas.transform,false).GetComponent<MechanicHud>();hud.name="MechanicsHUD";
                    Undo.RegisterCreatedObjectUndo(hud.gameObject,"Connect existing cannon HUD");
                }
                finally{EditorSceneManager.CloseScene(sourceScene,true);SceneManager.SetActiveScene(scene);}
            }
            var texts=hud.GetComponentsInChildren<TMP_Text>(true);
            hud.Configure(player.GetComponent<AdvancedPlayerMovement>(),carrier,texts.Single(t=>t.name=="MechanicStatus"),texts.Single(t=>t.name=="PickupPrompt"));
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        }
        static void BuildArm()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);if(!importer)throw new InvalidOperationException("The capped right arm export is missing.");
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.optimizeGameObjects=false;importer.isReadable=true;importer.SaveAndReimport();
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var root=PrefabUtility.LoadPrefabContents(T07DestructionBuilder.Enemy);
            try
            {
                var progression=root.GetComponent<RobotDamageProgression>();
                if(progression.CriticalArm)return; // Re-running clarity/integration must not duplicate the separated arm.
                var bodies=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name.StartsWith("Body_LOD")).OrderBy(s=>s.name).ToArray();
                var bones=bodies[0].bones.ToDictionary(b=>b.name);
                var inside=AssetDatabase.LoadAssetAtPath<Material>(T07DestructionBuilder.Art+"/ExposedMechanism.mat");
                var outside=bodies[0].sharedMaterial;
                var piece=new GameObject("SeverableRightArm");piece.transform.SetParent(bodies[0].transform.parent,false);
                var detached=piece.AddComponent<RobotDetachedArm>();var arms=new SkinnedMeshRenderer[3];
                for(int lod=0;lod<3;lod++)
                {
                    var source=imported.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="Body_LOD"+lod);
                    CopySkin(source,bodies[lod],bones,inside,outside);
                    var importedArm=imported.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="Arm_R_LOD"+lod);
                    var go=new GameObject(importedArm.name);go.transform.SetParent(piece.transform,false);var arm=go.AddComponent<SkinnedMeshRenderer>();arms[lod]=arm;
                    CopySkin(importedArm,arm,bones,inside,outside);
                    // Static topology is collapsed once, so pooled pose baking allocates no triangle arrays.
                    string path=T07DestructionBuilder.Art+"/T07_Arm_R_LOD"+lod+".asset";
                    var mesh=Object.Instantiate(arm.sharedMesh);var shell=mesh.GetTriangles(0);
                    var caps=Enumerable.Range(1,mesh.subMeshCount-1).SelectMany(i=>mesh.GetTriangles(i)).ToArray();
                    mesh.subMeshCount=2;mesh.SetTriangles(shell,0);mesh.SetTriangles(caps,1);AssetDatabase.CreateAsset(mesh,path);
                    arm.sharedMesh=mesh;arm.sharedMaterials=new[]{outside,inside};
                }
                detached.Configure(arms[0],inside);progression.ConfigureArm(detached);
                var serialized=new SerializedObject(progression);var renderers=serialized.FindProperty("bodyRenderers");int count=renderers.arraySize;
                renderers.arraySize=count+3;for(int i=0;i<3;i++)renderers.GetArrayElementAtIndex(count+i).objectReferenceValue=arms[i];serialized.ApplyModifiedPropertiesWithoutUndo();
                var group=root.GetComponentsInChildren<LODGroup>(true).Single(g=>g.name=="Body");var levels=group.GetLODs();
                for(int i=0;i<3;i++)levels[i].renderers=levels[i].renderers.Concat(new Renderer[]{arms[i]}).ToArray();group.SetLODs(levels);
                PrefabUtility.SaveAsPrefabAsset(root,T07DestructionBuilder.Enemy);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void CopySkin(SkinnedMeshRenderer source,SkinnedMeshRenderer target,System.Collections.Generic.Dictionary<string,Transform> bones,Material inside,Material outside)
        {
            target.sharedMesh=source.sharedMesh;target.bones=source.bones.Select(b=>bones[b.name]).ToArray();target.rootBone=bones["Root"];
            target.sharedMaterials=Enumerable.Range(0,source.sharedMesh.subMeshCount).Select(i=>i==0?outside:inside).ToArray();
            var bounds=source.localBounds;bounds.Expand(bounds.size.magnitude*.7f);target.localBounds=bounds;target.updateWhenOffscreen=false;
        }
        static void Build()
        {
            bool timing=PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.enableFrameTimingStats=true;
                string output=Path.GetFullPath("Tools/IronRequiem/Builds/CombatProbe/CombatProbe.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{CombatV03Tools.Lab},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText(CombatV03Tools.Output+"/build.txt",report.summary.result+" errors="+report.summary.totalErrors+" bytes="+report.summary.totalSize);
                if(report.summary.result==BuildResult.Cancelled)return;
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Benchmark Player build failed.");
            }
            finally{PlayerSettings.enableFrameTimingStats=timing;}
        }
    }
}
