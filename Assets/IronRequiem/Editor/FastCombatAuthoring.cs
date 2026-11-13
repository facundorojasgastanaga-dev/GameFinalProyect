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
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
 [InitializeOnLoad]public static class FastCombatAuthoring
 {
  public const string Folder="Assets/IronRequiem/Prefabs/FastCombat",Lab="Assets/IronRequiem/Scenes/FastCombatExecutionLab.unity",Profile="Assets/IronRequiem/Art/Levels/TwoRooms/FastCombatTuning.asset",Reports="Tools/IronRequiem/Reports/FastCombat";
  const string Queue="Tools/IronRequiem/Requests/fast-combat.txt",Key="IRON.FastCombat";static readonly DateTime Loaded=DateTime.UtcNow;static TestRunnerApi api;static readonly Dictionary<string,string> projectiles=new Dictionary<string,string>();static StringBuilder report;
  static FastCombatAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
  static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
  static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
  static void Tick()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;
   if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);IronRequiemSetupTools.WriteAudit();}
   if(!File.Exists(Queue)||new[]{"Assets/IronRequiem/Editor/FastCombatAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/FastCombatTests.cs","Assets/IronRequiem/Scripts/Enemies/EnemyExecutionSource.cs","Assets/IronRequiem/Scripts/Player/StolenWeaponCarrier.cs"}.Any(p=>File.Exists(p)&&File.GetLastWriteTimeUtc(p)>Loaded))return;
   var operation=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Reports);
   try{if(Dirty())throw new InvalidOperationException("Preserving dirty user scene");if(operation=="build"){Build();File.WriteAllText(Queue,"test");}else if(operation=="joint-routing"){var path=Folder+"/T07_HitFeedback_v04_Fast.prefab";var contents=PrefabUtility.LoadPrefabContents(path);try{RouteJointDamage(contents);PrefabUtility.SaveAsPrefabAsset(contents,path);}finally{PrefabUtility.UnloadPrefabContents(contents);}File.WriteAllText(Queue,"test");}else if(operation=="hud"){var scene=EditorSceneManager.OpenScene(Lab);AddHud(scene,Object.FindAnyObjectByType<PlayerCharacterController>());EditorSceneManager.SaveScene(scene);File.WriteAllText(Queue,"test");}else if(operation=="patch"){var scene=EditorSceneManager.OpenScene(Lab);report=new StringBuilder();Player(Object.FindAnyObjectByType<PlayerCharacterController>().gameObject,AssetDatabase.LoadAssetAtPath<CombatTuningProfile>(Profile));EditorSceneManager.SaveScene(scene);var pickup=PrefabUtility.LoadPrefabContents(Folder+"/BigBot_Module.prefab");try{Float(pickup.GetComponent<CannonAmmo>(),"capacity",120);PrefabUtility.SaveAsPrefabAsset(pickup,Folder+"/BigBot_Module.prefab");}finally{PrefabUtility.UnloadPrefabContents(pickup);}var projectile=PrefabUtility.LoadPrefabContents(Folder+"/StolenBigBotProjectile.prefab");try{projectile.GetComponent<ProjectileStandard>().Damage=30;PrefabUtility.SaveAsPrefabAsset(projectile,Folder+"/StolenBigBotProjectile.prefab");}finally{PrefabUtility.UnloadPrefabContents(projectile);}File.WriteAllText(Reports+"/player-save-fix.txt",report.ToString());File.WriteAllText(Queue,"test");}else{Register();SessionState.SetBool(Key,true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.FastCombatTests"}}));}}
   catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
  }
  static void Variant(string source,string dest){if(File.Exists(dest))return;var preview=EditorSceneManager.NewPreviewScene();try{var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),preview);PrefabUtility.SaveAsPrefabAsset(instance,dest);}finally{EditorSceneManager.ClosePreviewScene(preview);}}
  static void Float(Object target,string property,float value){var so=new SerializedObject(target);var p=so.FindProperty(property);if(p==null)throw new Exception(target.name+" missing "+property);p.floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
  static void Flag(Object target,string property,bool value){var so=new SerializedObject(target);so.FindProperty(property).boolValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
  static ProjectileBase Projectile(ProjectileBase source,float multiplier)
  {
   string path=AssetDatabase.GetAssetPath(source);if(string.IsNullOrEmpty(path))throw new Exception("Embedded projectile requires explicit inspection");
   if(!projectiles.TryGetValue(path,out string dest)){dest=Folder+"/EnemyProjectile_"+projectiles.Count+".prefab";projectiles.Add(path,dest);Variant(path,dest);var go=PrefabUtility.LoadPrefabContents(dest);try{var p=go.GetComponent<ProjectileStandard>();var original=source.GetComponent<ProjectileStandard>();if(!p||!original)throw new Exception("Unsupported projectile "+path);p.Damage=original.Damage*multiplier;report.AppendLine("Projectile "+path+": "+original.Damage+" -> "+p.Damage);PrefabUtility.SaveAsPrefabAsset(go,dest);}finally{PrefabUtility.UnloadPrefabContents(go);}}
   return AssetDatabase.LoadAssetAtPath<GameObject>(dest).GetComponent<ProjectileBase>();
  }
  static void Player(GameObject go,CombatTuningProfile profile)
  {
   var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Player/Player_ModularArm_v07.prefab");if(!source)throw new Exception("Missing source player");var p=go.GetComponent<PlayerCharacterController>();var old=source.GetComponent<PlayerCharacterController>();p.MaxSpeedOnGround=old.MaxSpeedOnGround*profile.PlayerSpeed;p.MaxSpeedInAir=old.MaxSpeedInAir*profile.PlayerSpeed;
   var motion=go.GetComponent<AdvancedPlayerMovement>();var original=new SerializedObject(source.GetComponent<AdvancedPlayerMovement>());foreach(var property in new[]{"dashSpeed","slideMinimumSpeed","slideImpulse","slideMaximumSpeed","slideDeceleration"})Float(motion,property,original.FindProperty(property).floatValue*profile.PlayerSpeed);
   PrefabUtility.RecordPrefabInstancePropertyModifications(p);PrefabUtility.RecordPrefabInstancePropertyModifications(motion);
   report.AppendLine("Player ground "+old.MaxSpeedOnGround+" -> "+p.MaxSpeedOnGround+"; sprint "+old.MaxSpeedOnGround*old.SprintSpeedModifier+" -> "+p.MaxSpeedOnGround*p.SprintSpeedModifier+"; air "+old.MaxSpeedInAir+" -> "+p.MaxSpeedInAir+"; dash "+original.FindProperty("dashSpeed").floatValue+" -> "+motion.DashSpeed+". Durations, jump, health, damage unchanged.");
  }
  static void RouteJointDamage(GameObject enemy)
  {
   foreach(var joint in enemy.GetComponentsInChildren<DetachableCannon>(true))foreach(var hurt in joint.GetComponentsInChildren<Damageable>(true)){var data=new SerializedObject(hurt);data.FindProperty("healthOverride").objectReferenceValue=enemy.GetComponent<Health>();data.ApplyModifiedPropertiesWithoutUndo();}
  }
  static void AddHud(Scene scene,PlayerCharacterController player)
  {
   var hud=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MechanicHud>(true)).FirstOrDefault();
   if(!hud)
   {
    var canvas=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Unity.FPS.UI.WeaponHUDManager>(true)).First().AmmoPanel.GetComponentInParent<Canvas>();
    var sourceScene=EditorSceneManager.OpenScene(T07DestructionBuilder.Lab,OpenSceneMode.Additive);
    try{var source=sourceScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MechanicHud>(true)).Single();hud=Object.Instantiate(source.gameObject,canvas.transform,false).GetComponent<MechanicHud>();hud.name="MechanicsHUD";}
    finally{EditorSceneManager.CloseScene(sourceScene,true);SceneManager.SetActiveScene(scene);}
   }
   var texts=hud.GetComponentsInChildren<TMPro.TMP_Text>(true);hud.Configure(player.GetComponent<AdvancedPlayerMovement>(),player.GetComponent<StolenWeaponCarrier>(),texts.Single(t=>t.name=="MechanicStatus"),texts.Single(t=>t.name=="PickupPrompt"));PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
  }
  static void Build()
  {
   if(File.Exists(Lab))throw new Exception("Preserve existing test scene; use test operation");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();report=new StringBuilder();projectiles.Clear();var tuning=AssetDatabase.LoadAssetAtPath<CombatTuningProfile>(Profile);if(!tuning){tuning=ScriptableObject.CreateInstance<CombatTuningProfile>();AssetDatabase.CreateAsset(tuning,Profile);}
   string bigWeapon=Folder+"/Weapon_BigBot.prefab",bigPickup=Folder+"/BigBot_Module.prefab",t07Pickup=Folder+"/T07_Module.prefab",bigProjectile=Folder+"/StolenBigBotProjectile.prefab";
   Variant(BigBotBurstAuthoring.Projectile,bigProjectile);var go=PrefabUtility.LoadPrefabContents(bigProjectile);try{var p=go.GetComponent<ProjectileStandard>();p.Damage=AssetDatabase.LoadAssetAtPath<GameObject>(BigBotBurstAuthoring.Projectile).GetComponent<ProjectileStandard>().Damage*tuning.StolenBigBotDamage;p.Damage=Mathf.Round(p.Damage*1000)/1000;report.AppendLine("Stolen BigBot damage 25 -> "+p.Damage+"; radius 2.6, burst 3/.18/1.5 unchanged");PrefabUtility.SaveAsPrefabAsset(go,bigProjectile);}finally{PrefabUtility.UnloadPrefabContents(go);}
   Variant(BigBotBurstAuthoring.Weapon,bigWeapon);go=PrefabUtility.LoadPrefabContents(bigWeapon);try{go.GetComponent<WeaponController>().ProjectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(bigProjectile).GetComponent<ProjectileBase>();PrefabUtility.SaveAsPrefabAsset(go,bigWeapon);}finally{PrefabUtility.UnloadPrefabContents(go);}
   Variant(BigBotBurstAuthoring.Pickup,bigPickup);go=PrefabUtility.LoadPrefabContents(bigPickup);try{go.GetComponent<StolenCannon>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(bigWeapon).GetComponent<WeaponController>());var original=new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(BigBotBurstAuthoring.Pickup).GetComponent<CannonAmmo>());float cap=original.FindProperty("capacity").floatValue;Float(go.GetComponent<CannonAmmo>(),"capacity",Mathf.Round(cap*tuning.StolenBigBotEnergy*1000)/1000);report.AppendLine("Stolen BigBot capacity "+cap+" -> "+cap*tuning.StolenBigBotEnergy+"; 12/shot; critical ratio .2 (24 energy); grace 5s unchanged");PrefabUtility.SaveAsPrefabAsset(go,bigPickup);}finally{PrefabUtility.UnloadPrefabContents(go);}
   var t07Source=AssetDatabase.LoadAssetAtPath<GameObject>(HitWeaponAuthoring.T07);var attached=t07Source.GetComponentInChildren<StolenCannon>(true);if(!attached)throw new Exception("T07 lacks attached compatible module");if(!File.Exists(t07Pickup)){go=PrefabUtility.LoadPrefabContents(HitWeaponAuthoring.T07);GameObject module=null;try{module=go.GetComponentInChildren<StolenCannon>(true).gameObject;module.transform.SetParent(null,true);module.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);PrefabUtility.SaveAsPrefabAsset(module,t07Pickup);}finally{if(module)Object.DestroyImmediate(module);PrefabUtility.UnloadPrefabContents(go);}}
   string[] sources={HitWeaponAuthoring.T07,BigBotBurstAuthoring.Enemy,CombatPolishAuthoring.Types[2],CombatPolishAuthoring.Types[3]};var variants=new Dictionary<GameObject,GameObject>();
   foreach(var path in sources)
   {
    string dest=Folder+"/"+Path.GetFileNameWithoutExtension(path)+"_Fast.prefab";Variant(path,dest);go=PrefabUtility.LoadPrefabContents(dest);var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);try
    {
     var health=go.GetComponent<Health>();health.MaxHealth=original.GetComponent<Health>().MaxHealth*tuning.EnemyHealth;report.AppendLine(original.name+" health "+original.GetComponent<Health>().MaxHealth+" -> "+health.MaxHealth);
     var navs=go.GetComponentsInChildren<NavigationModule>(true);var oldNavs=original.GetComponentsInChildren<NavigationModule>(true);for(int i=0;i<navs.Length;i++){navs[i].MoveSpeed=oldNavs[i].MoveSpeed*tuning.EnemySpeed;report.AppendLine("  navigation "+oldNavs[i].MoveSpeed+" -> "+navs[i].MoveSpeed);}var agent=go.GetComponent<NavMeshAgent>();if(agent)agent.speed=original.GetComponent<NavMeshAgent>().speed*tuning.EnemySpeed;
     var weapons=go.GetComponentsInChildren<WeaponController>(true);var originals=original.GetComponentsInChildren<WeaponController>(true);for(int i=0;i<weapons.Length;i++)if(originals[i].ProjectilePrefab)weapons[i].ProjectilePrefab=Projectile(originals[i].ProjectilePrefab,tuning.EnemyDamage);
     foreach(var joint in go.GetComponentsInChildren<DetachableCannon>(true))Flag(joint,"executionOnly",true);RouteJointDamage(go);
     if(go.GetComponent<BigBotCombat>()){Float(go.GetComponent<BigBotCombat>(),"damage",original.GetComponent<BigBotCombat>().Damage*tuning.EnemyDamage);Flag(go.GetComponent<BigBotDestruction>(),"executionOnly",true);var target=go.GetComponent<EnemyExecutionSource>();if(!target)target=go.AddComponent<EnemyExecutionSource>();target.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(bigPickup).GetComponent<StolenCannon>());report.AppendLine("  BigBot runtime damage "+original.GetComponent<BigBotCombat>().Damage+" -> "+go.GetComponent<BigBotCombat>().Damage);}
     else if(go.GetComponentInChildren<DetachableCannon>(true)){var target=go.GetComponent<EnemyExecutionSource>();if(!target)target=go.AddComponent<EnemyExecutionSource>();target.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(t07Pickup).GetComponent<StolenCannon>());}else report.AppendLine("  No compatible module: no execution interaction (drone/turret).");
     PrefabUtility.SaveAsPrefabAsset(go,dest);
    }finally{PrefabUtility.UnloadPrefabContents(go);}variants.Add(original,AssetDatabase.LoadAssetAtPath<GameObject>(dest));
   }
   AssetDatabase.CopyAsset("Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity",Lab);var scene=EditorSceneManager.OpenScene(Lab);var player=Object.FindAnyObjectByType<PlayerCharacterController>();Player(player.gameObject,tuning);var lab=Object.FindAnyObjectByType<CombatTwoRoomsLab>();var data=new SerializedObject(lab);var waves=data.FindProperty("waves");for(int i=0;i<waves.arraySize;i++){var groups=waves.GetArrayElementAtIndex(i).FindPropertyRelative("Groups");for(int j=0;j<groups.arraySize;j++){var p=groups.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab");p.objectReferenceValue=variants[(GameObject)p.objectReferenceValue];}}data.ApplyModifiedPropertiesWithoutUndo();
   AddHud(scene,player);EditorSceneManager.SaveScene(scene);var build=EditorBuildSettings.scenes.ToList();if(!build.Any(s=>s.path==Lab)){build.Add(new EditorBuildSettingsScene(Lab,true));EditorBuildSettings.scenes=build.ToArray();}AssetDatabase.SaveAssets();File.WriteAllText(Reports+"/values.txt",report.ToString());IronRequiemSetupTools.WriteAudit();
  }
  sealed class Results:IErrorCallbacks
  {public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(Reports+"/runner-error.txt",m);}public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);TestRunnerApi.SaveResultToFile(r,Reports+"/play.xml");File.WriteAllText(Reports+"/status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}}
 }
}