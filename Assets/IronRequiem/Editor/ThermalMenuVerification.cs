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
 [InitializeOnLoad] public static class ThermalMenuVerification
 {
  const string Queue="Tools/IronRequiem/Requests/thermal-menu-test.txt",Key="Iron.ThermalMenu.Test";
  static TestRunnerApi api;
  static readonly DateTime Loaded=DateTime.UtcNow;
  static ThermalMenuVerification(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
  static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
  static void Tick()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;
   if(SessionState.GetBool(Key+".restore",false))
   {
    SessionState.EraseBool(Key+".restore");if(!Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))EditorSceneManager.OpenScene(SessionState.GetString(Key+".scene","Assets/IronRequiem/Scenes/SawBotLab.unity"));IronRequiemSetupTools.WriteAudit();
   }
   if(!File.Exists(Queue)||!File.Exists(ThermalMenuAuthoring.Lab)||new[]{"Assets/IronRequiem/Tests/PlayMode/ThermalMenuTests.cs","Assets/IronRequiem/Editor/ThermalMenuVerification.cs"}.Any(p=>File.GetLastWriteTimeUtc(p)>Loaded))return;
   if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))return;
   File.Delete(Queue);SessionState.SetString(Key+".scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);Register();api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.ThermalMenuTests"}}));
  }
  sealed class Results:IErrorCallbacks
  {
   public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
   public void OnError(string message){SessionState.EraseBool(Key);File.WriteAllText(ThermalMenuAuthoring.Report+"/runner-error.txt",message);}
   public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);TestRunnerApi.SaveResultToFile(r,ThermalMenuAuthoring.Report+"/play.xml");File.WriteAllText(ThermalMenuAuthoring.Report+"/status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}
  }
 }
}
