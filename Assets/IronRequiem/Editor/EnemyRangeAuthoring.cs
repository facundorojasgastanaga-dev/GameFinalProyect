using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Unity.FPS.AI;
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
 [InitializeOnLoad] public static class EnemyRangeAuthoring
 {
  public const string Folder="Assets/IronRequiem/Prefabs/EnemyRange",Lab="Assets/IronRequiem/Scenes/EnemyRangeBurstLab.unity",Reports="Tools/IronRequiem/Reports/EnemyRange";
  const string Queue="Tools/IronRequiem/Requests/enemy-range.txt",Key="IRON.EnemyRange";static readonly DateTime Loaded=DateTime.UtcNow;static TestRunnerApi api;
  static EnemyRangeAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
  static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
  static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
  static void Tick()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(File.Exists(Queue)&&File.ReadAllText(Queue).Trim()=="retry")SessionState.EraseBool(Key);
   if(SessionState.GetBool(Key,false))return;
   if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);IronRequiemSetupTools.WriteAudit();}
   if(!File.Exists(Queue)||new[]{"Assets/IronRequiem/Editor/EnemyRangeAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/EnemyRangeTests.cs","Assets/IronRequiem/Scripts/AI/BigBotCombat.cs","Assets/FPS/Scripts/AI/EnemyController.cs"}.Any(p=>File.Exists(p)&&File.GetLastWriteTimeUtc(p)>Loaded))return;
   string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Reports);
   try{if(Dirty())throw new Exception("Preserving dirty user scene");if(op=="register"){var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==Lab)){scenes.Add(new EditorBuildSettingsScene(Lab,true));EditorBuildSettings.scenes=scenes.ToArray();}IronRequiemSetupTools.WriteAudit();File.WriteAllText(Reports+"/registered.txt",Lab);}else if(op=="build"){Build();File.WriteAllText(Queue,"test");}else{Register();SessionState.SetBool(Key,true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.EnemyRangeTests"}}));}}
   catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
  }
  static void Set(Object target,string property,float value){var s=new SerializedObject(target);var p=s.FindProperty(property);if(p==null)throw new Exception(property);if(p.propertyType==SerializedPropertyType.Boolean)p.boolValue=value!=0;else if(p.propertyType==SerializedPropertyType.Integer)p.intValue=(int)value;else p.floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
  static void Variant(string source,string dest){if(File.Exists(dest))throw new Exception("Preserve existing "+dest);var preview=EditorSceneManager.NewPreviewScene();try{var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),preview);PrefabUtility.SaveAsPrefabAsset(go,dest);}finally{EditorSceneManager.ClosePreviewScene(preview);}}
  static void Build()
  {
   if(File.Exists(Lab))throw new Exception("Preserve existing lab");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var report=new StringBuilder();var replacements=new Dictionary<GameObject,GameObject>();
   foreach(var name in new[]{"T07_HitFeedback_v04_Fast","BigBot_Burst_v10_Fast","HoverBot_CombatPolish_Fast","Turret_CombatPolish_Fast"})
   {
    string source=FastCombatAuthoring.Folder+"/"+name+".prefab",dest=Folder+"/"+name+"_Range.prefab";Variant(source,dest);var original=AssetDatabase.LoadAssetAtPath<GameObject>(source);var go=PrefabUtility.LoadPrefabContents(dest);
    try
    {
     var detector=go.GetComponentInChildren<DetectionModule>(true);var big=go.GetComponent<BigBotCombat>();float before=big?big.AttackDistance:detector.AttackRange;float after=Mathf.Round(before*(big?1.3f:1.25f)*1000)/1000;
     report.AppendLine(name+": attack "+before+" -> "+after+"; detection "+detector.DetectionRange+" -> "+Mathf.Max(detector.DetectionRange,after+2));detector.AttackRange=after;detector.DetectionRange=Mathf.Max(detector.DetectionRange,after+2);go.GetComponent<EnemyController>().RequireVisibleTargetToShoot=true;
     if(big){Set(big,"attackDistance",after);Set(big,"detectionDistance",Mathf.Max(new SerializedObject(big).FindProperty("detectionDistance").floatValue,after+2));Set(big,"normalBurstCount",6);Set(big,"emergencyBurstCount",6);Set(big,"normalBurstInterval",.15f);Set(big,"normalBurstPause",1);Set(big,"emergencyBurstInterval",.12f);Set(big,"emergencyBurstPause",.75f);Set(big,"pauseIncludesPreparation",1);report.AppendLine("Big burst 3 ->6 total; .18 ->.15 normal / .12 emergency; last-shot-to-next-shot pause (warning included) 1 / .75; unchanged damage "+big.Damage+", speed "+big.ProjectileSpeed+", travel "+big.ProjectileRange);}
     var weapons=go.GetComponentsInChildren<WeaponController>(true);
     foreach(var weapon in weapons)
     {
      var p=weapon.ProjectilePrefab.GetComponent<ProjectileStandard>();if(!p)throw new Exception("Unknown projectile "+name);float range=big?big.ProjectileRange:p.Speed*p.MaxLifeTime;report.AppendLine("  "+weapon.name+" projectile "+p.Speed+" m/s, "+p.MaxLifeTime+" s, nominal range "+range+", gravity "+p.GravityDownAcceleration+", damage "+p.Damage);
      if(range<after+3)throw new Exception("Insufficient projectile range; inspect before changing "+name);
     }
     PrefabUtility.SaveAsPrefabAsset(go,dest);
    }finally{PrefabUtility.UnloadPrefabContents(go);}replacements.Add(original,AssetDatabase.LoadAssetAtPath<GameObject>(dest));
   }
   AssetDatabase.CopyAsset(FastCombatAuthoring.Lab,Lab);var scene=EditorSceneManager.OpenScene(Lab);var lab=Object.FindAnyObjectByType<CombatTwoRoomsLab>();var data=new SerializedObject(lab);var waves=data.FindProperty("waves");for(int i=0;i<waves.arraySize;i++){var groups=waves.GetArrayElementAtIndex(i).FindPropertyRelative("Groups");for(int j=0;j<groups.arraySize;j++){var p=groups.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab");p.objectReferenceValue=replacements[(GameObject)p.objectReferenceValue];}}data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Reports+"/values.txt",report.ToString());IronRequiemSetupTools.WriteAudit();
  }
  sealed class Results:IErrorCallbacks
  {public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(Reports+"/runner-error.txt",m);}public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);TestRunnerApi.SaveResultToFile(r,Reports+"/play.xml");File.WriteAllText(Reports+"/status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}}
 }
}