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
 [InitializeOnLoad] public static class SawBotVerification
 {
  const string Queue="Tools/IronRequiem/Requests/sawbot-gameplay.txt",Key="IRON.SawBotGameplay";static TestRunnerApi api;static readonly DateTime Loaded=DateTime.UtcNow;
  static SawBotVerification(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
  static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
  static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
  static void Tick()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;
   if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(SawBotAuthoring.Lab);IronRequiemSetupTools.WriteAudit();}
   if(!File.Exists(Queue)||new[]{"Assets/IronRequiem/Editor/SawBotVerification.cs","Assets/IronRequiem/Editor/SawBotAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/SawBotTests.cs","Assets/IronRequiem/Scripts/AI/SawBotCombat.cs"}.Any(p=>File.Exists(p)&&File.GetLastWriteTimeUtc(p)>Loaded))return;
   Directory.CreateDirectory(SawBotAuthoring.Reports);if(Dirty()){File.WriteAllText(SawBotAuthoring.Reports+"/waiting.txt","Preserving dirty user scene");return;}
   string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);
   try{if(op=="build"){SawBotAuthoring.Build();File.WriteAllText(Queue,"test");}else{Register();SessionState.SetBool(Key,true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=op=="target"?new[]{"IronRequiem.Tests.SawBotTests.PlayerBulletsMeleeFeedbackAndCorpse","IronRequiem.Tests.SawBotTests.TelegraphSingleImpactAndDeathDuringAttack"}:new[]{op=="regression"?"IronRequiem.Tests.FastCombatTests":"IronRequiem.Tests.SawBotTests"}}));}}
   catch(Exception e){File.WriteAllText(SawBotAuthoring.Reports+"/runner-error.txt",e.ToString());Debug.LogException(e);}
  }
  sealed class Results:IErrorCallbacks
  {public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(SawBotAuthoring.Reports+"/runner-error.txt",m);}public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);TestRunnerApi.SaveResultToFile(r,SawBotAuthoring.Reports+"/play.xml");File.WriteAllText(SawBotAuthoring.Reports+"/status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}}
 }
}