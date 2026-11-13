using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace IronRequiem.Editor
{
    [InitializeOnLoad] public static class SlideAimRepairTools
    {
        const string Request="Tools/IronRequiem/Requests/slide-aim.txt", Output="Tools/IronRequiem/Reports/SlideAim", Key="IRON.SlideAim";
        const string Prefab="Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol.prefab";
        static readonly DateTime LoadedAt=DateTime.UtcNow;
        static SlideAimRepairTools(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(SessionState.GetString(Key,"")=="run"&&EditorApplication.isPlaying&&!EditorApplication.isCompiling)
            {
                if(!SessionState.GetBool(Key+".started",false))
                {
                    SessionState.SetBool(Key+".started",true);
                    var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="IronRequiem.PlayModeTests").GetType("IronRequiem.Tests.SlideAimSmokeRelay",true);
                    new GameObject("SlideAimTemporaryVerifier").AddComponent(type);
                }
                if(SessionState.GetBool(Key+".done",false)){SessionState.SetString(Key,"exit");EditorApplication.ExitPlaymode();}
                return;
            }
            if(SessionState.GetString(Key,"")=="exit"&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.EraseString(Key);IronRequiemSetupTools.WriteAudit();return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            if(LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/SlideAimRepairTools.cs")||LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Tests/PlayMode/SlidePistolMainTests.cs"))return;
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
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var particles=root.GetComponentsInChildren<ParticleSystem>(true);
                File.WriteAllLines(Output+"/inherited-effects.txt",particles.Select(p=>p.name+" active="+p.gameObject.activeSelf+" autoplay="+p.main.playOnAwake+" emission="+p.emission.enabled));
                foreach(var ps in particles)
                {
                    ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=ps.main;main.playOnAwake=false;var emission=ps.emission;emission.enabled=false;
                    ps.gameObject.SetActive(false);
                }
                if(!PrefabUtility.SaveAsPrefabAsset(root,Prefab))throw new InvalidOperationException("Prefab save failed");
                File.WriteAllText(Output+"/status.txt","Disabled "+particles.Length+" inherited local template particle systems. Muzzle flash prefab preserved; no scene saved.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            IronRequiemSetupTools.WriteAudit();
        }
    }
}
