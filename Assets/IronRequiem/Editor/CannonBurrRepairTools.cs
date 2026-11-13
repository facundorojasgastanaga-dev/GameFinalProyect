using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace IronRequiem.Editor
{
    [InitializeOnLoad] public static class CannonBurrRepairTools
    {
        const string Request="Tools/IronRequiem/Requests/cannon-burr.txt", Output="Tools/IronRequiem/Reports/CannonBurr", Key="IRON.CannonBurr";
        const string Prefab="Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab";
        const string Model="Assets/IronRequiem/Art/Weapons/T07CannonClean/T07_CannonClean.fbx";
        static readonly DateTime LoadedAt=DateTime.UtcNow;
        static CannonBurrRepairTools(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(SessionState.GetString(Key,"")=="run"&&EditorApplication.isPlaying&&!EditorApplication.isCompiling)
            {
                if(!SessionState.GetBool(Key+".started",false))
                {
                    SessionState.SetBool(Key+".started",true);
                    var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="IronRequiem.PlayModeTests").GetType("IronRequiem.Tests.CannonBurrSmokeRelay",true);
                    new GameObject("CannonBurrTemporaryVerifier").AddComponent(type);
                }
                if(SessionState.GetBool(Key+".done",false)){SessionState.SetString(Key,"exit");EditorApplication.ExitPlaymode();}
                return;
            }
            if(SessionState.GetString(Key,"")=="exit"&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.EraseString(Key);IronRequiemSetupTools.WriteAudit();return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            if(LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/CannonBurrRepairTools.cs")||LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Tests/PlayMode/CannonBurrSmokeRelay.cs"))return;
            var op=File.ReadAllText(Request).Trim();File.Delete(Request);Directory.CreateDirectory(Output);
            try
            {
                if(op=="apply")Apply();
                else if(op=="test"){SessionState.SetString(Key,"run");SessionState.SetBool(Key+".started",false);SessionState.SetBool(Key+".done",false);EditorApplication.EnterPlaymode();}
                else throw new InvalidOperationException("Unknown fixed operation");
            }
            catch(Exception e){File.WriteAllText(Output+"/status.txt",e.ToString());Debug.LogException(e);}
        }
        static void Apply()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            if(!importer)throw new InvalidOperationException("Model not imported yet");
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;importer.isReadable=false;importer.SaveAndReimport();
            var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<MeshFilter>(true).Single(x=>x.sharedMesh.name=="CannonClean_LOD0").sharedMesh;
            if(mesh.subMeshCount!=2)throw new InvalidOperationException("Expected shell and closed interior submeshes");
            var cap=AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Enemies/T07Destructible/ExposedMechanism.mat");
            if(!cap)throw new InvalidOperationException("Shared interior material missing");
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var transforms=root.GetComponentsInChildren<Transform>(true);
                var before=transforms.Select(t=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale)).ToArray();
                var visual=transforms.Single(t=>t.name=="StolenCannonVisual");
                var filter=visual.GetComponent<MeshFilter>();var renderer=visual.GetComponent<MeshRenderer>();
                var oldPath=AssetDatabase.GetAssetPath(filter.sharedMesh);var shell=renderer.sharedMaterials[0];
                filter.sharedMesh=mesh;renderer.sharedMaterials=new[]{shell,cap};
                for(int i=0;i<transforms.Length;i++)if(before[i]!=Matrix4x4.TRS(transforms[i].localPosition,transforms[i].localRotation,transforms[i].localScale))throw new InvalidOperationException("Unexpected socket/transform change");
                if(!PrefabUtility.SaveAsPrefabAsset(root,Prefab))throw new InvalidOperationException("Prefab save failed");
                File.WriteAllText(Output+"/status.txt","Applied equipped mesh only: "+oldPath+" -> "+Model+"; 2 shared materials; all local transforms unchanged. No scene saved.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            IronRequiemSetupTools.WriteAudit();
        }
    }
}
