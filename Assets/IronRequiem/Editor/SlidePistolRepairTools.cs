using System;
using System.IO;
using System.Linq;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad] public static class SlidePistolRepairTools
    {
        const string Request="Tools/IronRequiem/Requests/slide-pistol.txt", Output="Tools/IronRequiem/Reports/SlidePistol", Key="IRON.SlidePistol";
        public const string Lab="Assets/IronRequiem/Scenes/SlidePistolLab.unity";
        static TestRunnerApi api;static readonly DateTime LoadedAt=DateTime.UtcNow;
        [Serializable] class Snapshot {public string scene;public bool advancedMovement,slideParticles;public string[] weapons;public float groundSpeed,sprintMultiplier;}
        static SlidePistolRepairTools(){EditorApplication.update+=Tick;if(SessionState.GetString(Key,"")=="test")EditorApplication.delayCall+=Register;}
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        static void Tick()
        {
                        if(SessionState.GetString(Key,"")=="runtime-run"&&EditorApplication.isPlaying&&!EditorApplication.isCompiling)
            {
                if(!SessionState.GetBool(Key+".started",false))
                {
                    SessionState.SetBool(Key+".started",true);
                    var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="IronRequiem.PlayModeTests").GetType("IronRequiem.Tests.SlidePistolSmokeRelay",true);
                    new GameObject("SlidePistolTemporaryVerifier").AddComponent(type);
                }
                if(SessionState.GetBool(Key+".RuntimeDone",false)){SessionState.SetString(Key,"runtime-exit");EditorApplication.ExitPlaymode();}
                return;
            }
            if(SessionState.GetString(Key,"")=="runtime-exit"&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.EraseString(Key);IronRequiemSetupTools.WriteAudit();return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SessionState.GetString(Key,"")=="restore")
            {SessionState.EraseString(Key);EditorSceneManager.OpenScene(CombatV03Tools.Main);IronRequiemSetupTools.WriteAudit();return;}
            if(!File.Exists(Request))return;if(LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/SlidePistolRepairTools.cs")||LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Tests/PlayMode/SlidePistolMainTests.cs"))return;
            if(File.GetLastWriteTimeUtc(typeof(SlidePistolRepairTools).Assembly.Location)<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/SlidePistolRepairTools.cs"))return;
            if(File.GetLastWriteTimeUtc("Library/ScriptAssemblies/IronRequiem.PlayModeTests.dll")<File.GetLastWriteTimeUtc("Assets/IronRequiem/Tests/PlayMode/SlidePistolMainTests.cs"))return;
            string op=File.ReadAllText(Request).Trim();File.Delete(Request);Directory.CreateDirectory(Output);
            try
            {
                if(op=="runtime-test"){SessionState.SetString(Key,"runtime-run");SessionState.SetBool(Key+".started",false);SessionState.SetBool(Key+".RuntimeDone",false);EditorApplication.EnterPlaymode();return;}if(op=="copy-check"){CheckDirtiness();return;}T07AnimationBuilder.RequireClean();
                if(op=="apply")Apply();
                else if(op=="test")
                {
                    api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
                    SessionState.SetString(Key,"test");api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.SlidePistolMainTests"}}));
                }
                else throw new InvalidOperationException("Unknown slide/pistol operation.");
            }
            catch(Exception e){File.WriteAllText(Output+"/status.txt",op+" FAILED "+e);Debug.LogException(e);}
        }
        static void CheckDirtiness(){var scene=SceneManager.GetActiveScene();if(scene.path!=CombatV03Tools.Main)throw new InvalidOperationException("Expected MainScene.");if(!scene.isDirty){File.WriteAllText(Output+"/dirty-check.txt","Already clean.");return;}if(!EditorSceneManager.SaveScene(scene,Lab,true))throw new InvalidOperationException("Cannot preserve dirty scene as separate verification copy.");bool same=File.ReadAllBytes(CombatV03Tools.Main).SequenceEqual(File.ReadAllBytes(Lab));File.WriteAllText(Output+"/dirty-check.txt",same?"Dirty flag only: live scene serialized identically to saved MainScene; reloaded identical file without losing changes.":"Live scene differs from saved MainScene. Original remains dirty and untouched; separate verification copy preserved.");if(same)EditorSceneManager.OpenScene(CombatV03Tools.Main);IronRequiemSetupTools.WriteAudit();}
        static Snapshot Inspect(Scene scene,PlayerCharacterController player)
        {
            var fx=player.GetComponent<SlideContactEffects>();return new Snapshot{scene=scene.path,advancedMovement=player.GetComponent<AdvancedPlayerMovement>(),slideParticles=fx&&fx.ContactParticles,
                weapons=player.GetComponent<PlayerWeaponsManager>().StartingWeapons.Select(w=>AssetDatabase.GetAssetPath(w)).ToArray(),groundSpeed=player.MaxSpeedOnGround,sprintMultiplier=player.SprintSpeedModifier};
        }
        static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(CombatV03Tools.Main);
            var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerCharacterController>(true)).Single();
            File.WriteAllText(Output+"/before.json",JsonUtility.ToJson(Inspect(scene,player),true));
            var pistol=AssetDatabase.LoadAssetAtPath<GameObject>(DesignV02Upgrade.PistolPath);
            if(!pistol.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="PistolModule_1000_Equipped"))throw new InvalidOperationException("Original IRON REQUIEM pistol visual missing; will not substitute template gun.");
            var motion=player.GetComponent<AdvancedPlayerMovement>();if(!motion)motion=Undo.AddComponent<AdvancedPlayerMovement>(player.gameObject);
            motion.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(motion);
            var effects=player.GetComponent<SlideContactEffects>();if(!effects)effects=Undo.AddComponent<SlideContactEffects>(player.gameObject);
            if(!effects.ContactParticles)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.PlayerPath).GetComponent<SlideContactEffects>().ContactParticles;
                var copy=Object.Instantiate(source.gameObject,player.transform,false);copy.name="SlideContactParticles";Undo.RegisterCreatedObjectUndo(copy,"Restore slide contact particles");
                effects.Configure(copy.GetComponent<ParticleSystem>());
            }
            effects.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
            var inventory=player.GetComponent<PlayerWeaponsManager>();Undo.RecordObject(inventory,"Restore IRON REQUIEM starting pistol");
            inventory.StartingWeapons.Clear();inventory.StartingWeapons.Add(pistol.GetComponent<WeaponController>());PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
            foreach(var hud in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MechanicHud>(true)))
            {var serialized=new SerializedObject(hud);serialized.FindProperty("movement").objectReferenceValue=motion;serialized.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(hud);}
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("MainScene not saved.");
            File.WriteAllText(Output+"/after.json",JsonUtility.ToJson(Inspect(scene,player),true));
            if(File.Exists(Lab))throw new InvalidOperationException("The separate verification lab already exists; preserving it.");
            if(!AssetDatabase.CopyAsset(CombatV03Tools.Main,Lab))throw new InvalidOperationException("Cannot copy separate slide/pistol verification lab.");
            IronRequiemSetupTools.WriteAudit();File.WriteAllText(Output+"/status.txt","Applied MainScene scene overrides: existing movement + contact particles + Weapon_IR_Pistol. Source Player.prefab and weapon assets preserved.");
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
            public void RunFinished(ITestResultAdaptor result)
            {if(SessionState.GetString(Key,"")!="test")return;TestRunnerApi.SaveResultToFile(result,Output+"/tests.xml");File.WriteAllText(Output+"/status.txt",result.ResultState+" passed="+result.PassCount+" failed="+result.FailCount);SessionState.SetString(Key,"restore");}
        }
    }
}
