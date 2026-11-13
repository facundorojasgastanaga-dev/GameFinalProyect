using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
 [InitializeOnLoad]public static class CombatFollowupAuthoring
 {
  public const string Folder="Assets/IronRequiem/Prefabs/CombatFollowup",Lab="Assets/IronRequiem/Scenes/CombatFollowupLab.unity",Reports="Tools/IronRequiem/Reports/CombatFollowup";
  const string Queue="Tools/IronRequiem/Requests/combat-followup.txt",Key="IRON.CombatFollowup";static readonly DateTime Loaded=DateTime.UtcNow;static TestRunnerApi api;
  static CombatFollowupAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
  static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
  static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
  static void Tick()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(File.Exists(Lab)?Lab:EnemyRangeAuthoring.Lab);IronRequiemSetupTools.WriteAudit();}
   if(!File.Exists(Queue)||SessionState.GetBool(Key,false)||new[]{"Assets/IronRequiem/Editor/CombatFollowupAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/CombatFollowupTests.cs"}.Any(p=>File.Exists(p)&&File.GetLastWriteTimeUtc(p)>Loaded))return;
   string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Reports);
   try{if(Dirty())throw new Exception("Preserving dirty user scene");if(op=="build"){Build();File.WriteAllText(Queue,"final");return;}File.WriteAllText(Reports+"/phase.txt",op);Register();SessionState.SetBool(Key,true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{op=="baseline"||op=="after"||op.StartsWith("pool-")?"IronRequiem.Tests.CombatFollowupTests.CapturePerformance":"IronRequiem.Tests.CombatFollowupTests"}}));}
   catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
  }
  static void Variant(string source,string dest)
  {
   if(File.Exists(dest))throw new Exception("Preserve existing "+dest);var preview=EditorSceneManager.NewPreviewScene();try{var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),preview);PrefabUtility.SaveAsPrefabAsset(go,dest);}finally{EditorSceneManager.ClosePreviewScene(preview);}
  }
  static void Number(Object target,string property,float value){var s=new SerializedObject(target);var p=s.FindProperty(property);p.floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
  static void Build()
  {
   if(File.Exists(Lab))throw new Exception("Preserve existing lab");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
   string projectile=Folder+"/BigBot_PlayerRocket.prefab",weapon=Folder+"/BigBot_PlayerWeapon.prefab",module=Folder+"/BigBot_Module.prefab",t07=Folder+"/T07_ChargedProjectile.prefab";
   var bigSource=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRangeAuthoring.Folder+"/BigBot_Burst_v10_Fast_Range.prefab");var enemyProjectile=bigSource.GetComponent<BigBotCombat>().Cannons[0].ProjectilePrefab;
   Variant(AssetDatabase.GetAssetPath(enemyProjectile),projectile);var go=PrefabUtility.LoadPrefabContents(projectile);try{var old=AssetDatabase.LoadAssetAtPath<GameObject>(FastCombatAuthoring.Folder+"/StolenBigBotProjectile.prefab").GetComponent<ProjectileStandard>();var p=go.GetComponent<ProjectileStandard>();p.Damage=old.Damage;p.Speed=bigSource.GetComponent<BigBotCombat>().ProjectileSpeed;p.MaxLifeTime=bigSource.GetComponent<BigBotCombat>().ProjectileRange/p.Speed;p.AreaOfDamage.AreaOfEffectDistance=old.AreaOfDamage.AreaOfEffectDistance;PrefabUtility.SaveAsPrefabAsset(go,projectile);}finally{PrefabUtility.UnloadPrefabContents(go);}
   Variant(FastCombatAuthoring.Folder+"/Weapon_BigBot.prefab",weapon);go=PrefabUtility.LoadPrefabContents(weapon);try{go.GetComponent<WeaponController>().ProjectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(projectile).GetComponent<ProjectileBase>();PrefabUtility.SaveAsPrefabAsset(go,weapon);}finally{PrefabUtility.UnloadPrefabContents(go);}
   Variant(FastCombatAuthoring.Folder+"/BigBot_Module.prefab",module);go=PrefabUtility.LoadPrefabContents(module);try{go.GetComponent<StolenCannon>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(weapon).GetComponent<WeaponController>());PrefabUtility.SaveAsPrefabAsset(go,module);}finally{PrefabUtility.UnloadPrefabContents(go);}
   Variant(CannonChargeUpgrade.ProjectilePath,t07);go=PrefabUtility.LoadPrefabContents(t07);try{var charged=go.GetComponent<ChargedProjectileDamage>();Number(charged,"minimumDamage",8);Number(charged,"maximumDamage",8);var p=go.GetComponent<ProjectileStandard>();p.Damage=8;p.Speed=40;p.MaxLifeTime=Mathf.Max(p.MaxLifeTime,1);PrefabUtility.SaveAsPrefabAsset(go,t07);}finally{PrefabUtility.UnloadPrefabContents(go);}
   var replacements=new System.Collections.Generic.Dictionary<GameObject,GameObject>();
   foreach(string name in new[]{"T07_HitFeedback_v04","BigBot_Burst_v10","HoverBot_CombatPolish","Turret_CombatPolish"})
   {
    string source=EnemyRangeAuthoring.Folder+"/"+name+"_Fast_Range.prefab",dest=Folder+"/"+name+".prefab";Variant(source,dest);go=PrefabUtility.LoadPrefabContents(dest);
    try
    {
     if(name.StartsWith("T07")){var controller=go.GetComponent<EnemyController>();controller.EnemyChargeDuration=1.2f;var gun=go.GetComponentsInChildren<WeaponController>(true).First();gun.ProjectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(t07).GetComponent<ProjectileBase>();var effect=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CannonChargeUpgrade.ChargePrefab),gun.WeaponMuzzle);effect.transform.localPosition=Vector3.zero;effect.transform.localRotation=Quaternion.identity;effect.transform.localScale=Vector3.one;go.AddComponent<EnemyCannonChargePresentation>().Configure(controller,effect);}
     if(name.StartsWith("BigBot"))go.GetComponent<EnemyExecutionSource>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(module).GetComponent<StolenCannon>());
     PrefabUtility.SaveAsPrefabAsset(go,dest);
    }finally{PrefabUtility.UnloadPrefabContents(go);}replacements.Add(AssetDatabase.LoadAssetAtPath<GameObject>(source),AssetDatabase.LoadAssetAtPath<GameObject>(dest));
   }
   AssetDatabase.CopyAsset(EnemyRangeAuthoring.Lab,Lab);var scene=EditorSceneManager.OpenScene(Lab);var player=Object.FindAnyObjectByType<PlayerCharacterController>();if(!player.GetComponent<ExecutionProtection>())player.gameObject.AddComponent<ExecutionProtection>();
   var data=new SerializedObject(Object.FindAnyObjectByType<CombatTwoRoomsLab>());var waves=data.FindProperty("waves");for(int i=0;i<waves.arraySize;i++){var groups=waves.GetArrayElementAtIndex(i).FindPropertyRelative("Groups");for(int j=0;j<groups.arraySize;j++){var p=groups.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab");p.objectReferenceValue=replacements[(GameObject)p.objectReferenceValue];}}data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==Lab)){scenes.Add(new EditorBuildSettingsScene(Lab,true));EditorBuildSettings.scenes=scenes.ToArray();}AssetDatabase.SaveAssets();IronRequiemSetupTools.WriteAudit();
   File.WriteAllText(Reports+"/settings.txt","Execution: all animation +2s after. T07:1.2s charge, damage8,40m/s, magenta charge geometry/projectile; same range12.5. Big stolen: enemy rocket visual+trajectory22m/s,48m; player damage30/radius2.6,energy120/12,burst3/.18/1.5 preserved. EnemyBig6/.15/1 and emergency6/.12/.75 unchanged.");
  }  sealed class Results:IErrorCallbacks
  {public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(Reports+"/runner-error.txt",m);}public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);var phase=File.ReadAllText(Reports+"/phase.txt");TestRunnerApi.SaveResultToFile(r,Reports+"/"+phase+".xml");File.WriteAllText(Reports+"/status.txt",phase+": "+r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);if(phase=="pool-off")File.WriteAllText(Queue,"pool-on");}}
 }
}