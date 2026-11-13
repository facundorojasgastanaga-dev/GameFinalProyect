using System;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class T07AnimationBuilder
    {
        public const string Art="Assets/IronRequiem/Art/Enemies/T07Animated";
        public const string Reports="Tools/IronRequiem/Reports/T07Animation";
        public const string Draft=T07AnimationAuthoring.Draft;
        public const string ExplosionPath=T07ExplosionAuthoring.ExplosionPath;
        const string Request="Tools/IronRequiem/Requests/t07-animation.json";
        [Serializable] sealed class Job { public string operation; }
        [Serializable] sealed class Status { public string utc,operation,error;public bool success; }
        [Serializable] sealed class Node { public string path;public Vector3 position,rotation,scale; }
        [Serializable] sealed class Skin { public string name; public int vertices,bones,bindposes;public Bounds bounds;public string[] boneNames; }
        [Serializable] sealed class Inspection { public string utc;public Skin[] skins;public Node[] nodes; public string[] explosionComponents;public string effectPath; }
        static T07AnimationBuilder(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request))return;
            var job=JsonUtility.FromJson<Job>(File.ReadAllText(Request));File.Delete(Request);
            var status=new Status{utc=DateTime.UtcNow.ToString("O"),operation=job.operation};
            try
            {
                if(job.operation=="inspect")Inspect();
                else RunStage(job.operation);
                status.success=true;
            }
            catch(Exception e){status.error=e.ToString();Debug.LogException(e);}
            Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/"+job.operation+"-status.json",JsonUtility.ToJson(status,true));
        }
        internal static void RequireClean()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("User scene is dirty: "+SceneManager.GetSceneAt(i).path);
        }
        static void Inspect()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/T07_SharedRig.fbx");
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.optimizeGameObjects=false;
            importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.isReadable=true;
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/T07_SharedRig.fbx");
            var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(T07OptimizationBuilder.Enemy);
            var explosion=enemy.GetComponentInChildren<CannonExplosion>(true);
            var field=new SerializedObject(explosion).FindProperty("impactEffect").objectReferenceValue as GameObject;
            var report=new Inspection{utc=DateTime.UtcNow.ToString("O"),
                skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>new Skin{name=s.name,vertices=s.sharedMesh.vertexCount,bones=s.bones.Length,bindposes=s.sharedMesh.bindposeCount,bounds=s.bounds,boneNames=s.bones.Select(b=>b.name).ToArray()}).ToArray(),
                nodes=model.GetComponentsInChildren<Transform>(true).Select(t=>new Node{path=AnimationUtility.CalculateTransformPath(t,model.transform),position=t.localPosition,rotation=t.localEulerAngles,scale=t.localScale}).ToArray(),
                effectPath=AssetDatabase.GetAssetPath(field),explosionComponents=field.GetComponentsInChildren<Component>(true).Where(c=>c).Select(c=>c.GetType().FullName+" "+c.name).ToArray()};
            Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/unity-rig-inspection.json",JsonUtility.ToJson(report,true));
            IronRequiemSetupTools.WriteAudit();
        }
        static void RunStage(string operation)
        {
            if(operation=="build-lab")T07AnimationAuthoring.BuildLab();
            else if(operation=="revise-working")T07AnimationAuthoring.ReviseWorking();
            else if(operation=="finalize-lab")
            {
                T07ExplosionAuthoring.ReviseWorking();T07ScenePromotion.RestoreTransientDirtyFlags();
                T07AnimationVerification.Run(false);
            }
            else if(operation=="finish-lab")
            {
                T07ScenePromotion.RestoreTransientDirtyFlags();
                T07ScenePromotion.ReplaceScene(T07OptimizationBuilder.Lab,T07OptimizationBuilder.Enemy,Draft);
                IronRequiemSetupTools.WriteAudit();
            }
            else if(operation=="promote")T07ScenePromotion.Promote();
            else throw new InvalidOperationException("Unknown task stage: "+operation);
        }
    }
}
