using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace IronRequiem.Editor
{
 [InitializeOnLoad] public static class SawBotWeaponDeathVerification
 {
  const string Queue="Tools/IronRequiem/Requests/sawbot-arsenal-test.txt",Key="Iron.SawArsenal.Test";static readonly DateTime Loaded=DateTime.UtcNow;static TestRunnerApi api;
  static SawBotWeaponDeathVerification(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
  static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
  static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;
   if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))EditorSceneManager.OpenScene(SessionState.GetString(Key+".scene","Assets/IronRequiem/Scenes/SawBotLab.unity"));IronRequiemSetupTools.WriteAudit();}
   if(!File.Exists(Queue)||new[]{"Assets/IronRequiem/Tests/PlayMode/SawBotWeaponDeathTests.cs","Assets/IronRequiem/Editor/SawBotWeaponDeathVerification.cs","Assets/IronRequiem/Scripts/AI/SawBotCombat.cs"}.Any(p=>File.GetLastWriteTimeUtc(p)>Loaded)||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))return;
   var op=File.ReadAllText(Queue).Trim();File.Delete(Queue);SessionState.SetString(Key+".scene",SceneManager.GetActiveScene().path);SessionState.SetString(Key+".op",op);SessionState.SetBool(Key,true);Register();api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.SawBotWeaponDeathTests"}}));}
  sealed class Results:IErrorCallbacks{public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(SawBotWeaponDeathAuthoring.Report+"/runner-error.txt",m);}public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);string op=SessionState.GetString(Key+".op","test");TestRunnerApi.SaveResultToFile(r,SawBotWeaponDeathAuthoring.Report+"/"+op+".xml");File.WriteAllText(SawBotWeaponDeathAuthoring.Report+"/"+op+"-status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}}
 }
}
