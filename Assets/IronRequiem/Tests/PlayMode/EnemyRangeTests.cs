using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
 public sealed class EnemyRangeTests
 {
  const string Folder="Assets/IronRequiem/Prefabs/EnemyRange",Lab="Assets/IronRequiem/Scenes/EnemyRangeBurstLab.unity",Reports="Tools/IronRequiem/Reports/EnemyRange";
  PlayerCharacterController player;BigBotCombat bot;EnemyController enemy;float capture;bool background;readonly List<float> times=new List<float>();readonly List<int> origins=new List<int>();
  [UnitySetUp]public IEnumerator Setup()
  {
   capture=Time.captureDeltaTime;background=Application.runInBackground;Time.captureDeltaTime=1f/60;Time.timeScale=1;Application.runInBackground=true;
   EditorSceneManager.LoadSceneInPlayMode(Lab,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;Object.FindAnyObjectByType<CombatLabMenu>().BeginGame();Object.FindAnyObjectByType<CombatLabMenu>().enabled=false;Object.FindAnyObjectByType<CombatTwoRoomsLab>().enabled=false;
   player=Object.FindAnyObjectByType<PlayerCharacterController>();player.enabled=false;player.GetComponent<Health>().Invincible=true;
   foreach(var collider in Object.FindObjectsByType<Collider>())if(!collider.transform.IsChildOf(player.transform)&&!(collider.bounds.size.y<2&&collider.bounds.size.x>20&&collider.bounds.size.z>20))collider.enabled=false;
   Move(new Vector3(-10,.03f,-16));times.Clear();origins.Clear();yield return null;
  }
  [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;Time.captureDeltaTime=capture;AudioListener.pause=false;var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("RangeCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(scene);Application.runInBackground=background;}
  void Move(Vector3 p){var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);c.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
  IEnumerator Spawn(string name)
  {
   var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+"_Fast_Range.prefab"),new Vector3(-10,0,12),Quaternion.Euler(0,180,0));yield return null;yield return null;enemy=go.GetComponent<EnemyController>();var mobile=go.GetComponent<EnemyMobile>();if(mobile)mobile.enabled=false;var turret=go.GetComponent<EnemyTurret>();if(turret)turret.enabled=false;var agent=go.GetComponent<NavMeshAgent>();if(agent&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}bot=go.GetComponent<BigBotCombat>();
   if(bot)bot.ProjectileSpawned+=p=>{times.Add(Time.time);origins.Add(Enumerable.Range(0,2).OrderBy(i=>Vector3.Distance(bot.Cannons[i].WeaponMuzzle.position,p.InitialPosition)).First());Assert.That(p.GetComponent<ProjectileStandard>().Damage,Is.EqualTo(8));};yield return null;
  }
  IEnumerator Until(Func<bool> condition,float timeout,string message,bool request=true){float end=Time.time+timeout;while(!condition()&&Time.time<end){if(request&&bot)bot.RequestAttack();yield return null;}Assert.That(condition(),Is.True,message+" shots="+times.Count+" seeing="+(enemy&&enemy.IsSeeingTarget));}
  GameObject Wall(){var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(-10,3,(12+player.transform.position.z)*.5f);wall.transform.localScale=new Vector3(20,8,.5f);Physics.SyncTransforms();return wall;}
  [UnityTest]public IEnumerator SerializedRangesAndUnchangedBalance()
  {
   foreach(string n in new[]{"T07_HitFeedback_v04","BigBot_Burst_v10","HoverBot_CombatPolish","Turret_CombatPolish"})
   {var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/FastCombat/"+n+"_Fast.prefab");var tuned=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+n+"_Fast_Range.prefab");var a=source.GetComponentInChildren<DetectionModule>(true);var b=tuned.GetComponentInChildren<DetectionModule>(true);float before=source.GetComponent<BigBotCombat>()?source.GetComponent<BigBotCombat>().AttackDistance:a.AttackRange;Assert.That(b.AttackRange,Is.EqualTo(before*(source.GetComponent<BigBotCombat>()?1.3f:1.25f)).Within(.001));Assert.That(b.DetectionRange,Is.GreaterThan(b.AttackRange));Assert.That(tuned.GetComponent<Health>().MaxHealth,Is.EqualTo(source.GetComponent<Health>().MaxHealth));Assert.That(tuned.GetComponent<NavMeshAgent>().speed,Is.EqualTo(source.GetComponent<NavMeshAgent>().speed));var oldWeapons=source.GetComponentsInChildren<WeaponController>(true);var weapons=tuned.GetComponentsInChildren<WeaponController>(true);for(int i=0;i<weapons.Length;i++)Assert.That(weapons[i].ProjectilePrefab,Is.SameAs(oldWeapons[i].ProjectilePrefab));var execution=tuned.GetComponent<EnemyExecutionSource>();if(execution)Assert.That(new SerializedObject(execution).FindProperty("modulePrefab").objectReferenceValue,Is.SameAs(new SerializedObject(source.GetComponent<EnemyExecutionSource>()).FindProperty("modulePrefab").objectReferenceValue));}
   yield return null;File.WriteAllText(Reports+"/configuration.txt","Four ranges scaled once from current FastCombat baselines. HP, speed, projectile/damage and stolen module references unchanged.");
  }
  [UnityTest]public IEnumerator TwoCannonsSixAndExactPauseAtNewRange()
  {
   yield return Spawn("BigBot_Burst_v10");Assert.That(bot.AttackDistance,Is.EqualTo(28.6f).Within(.001));yield return Until(()=>times.Count>=12,7,"Two six-shot bursts");Assert.That(origins.Take(12),Is.EqualTo(new[]{0,1,0,1,0,1,0,1,0,1,0,1}));foreach(int i in new[]{1,2,3,4,5,7,8,9,10,11})Assert.That(times[i]-times[i-1],Is.InRange(.145f,.18f));Assert.That(times[6]-times[5],Is.InRange(1,1.09f));File.WriteAllText(Reports+"/normal.txt","Two real bursts, 12 projectiles alternating 3 per cannon per burst. New distance28m (>old22). Interval="+(times[1]-times[0])+" pause="+(times[6]-times[5])+" warning included. Damage8 unchanged.");
  }
  [UnityTest]public IEnumerator SingleCannonSixAndLossDuringBurstDeath()
  {
   yield return Spawn("BigBot_Burst_v10");yield return Until(()=>times.Count==1,4,"First shot");bot.GetComponent<BigBotDestruction>().DetachCannon();yield return Until(()=>times.Count>=12,6,"Retained budget and six-shot emergency");Assert.That(origins.Skip(1).All(i=>i==1),Is.True);foreach(int i in new[]{2,3,4,5,7,8,9,10,11})Assert.That(times[i]-times[i-1],Is.InRange(.115f,.15f));Assert.That(times[6]-times[5],Is.InRange(.75f,.84f));yield return Until(()=>times.Count==13,3,"Next burst");bot.GetComponent<Health>().Kill();yield return new WaitForSeconds(.6f);Assert.That(times.Count,Is.EqualTo(13));Assert.That(bot.IsBursting,Is.False);File.WriteAllText(Reports+"/emergency.txt","Loss after shot1 retained exactly5; next burst6 from surviving cannon only. Interval="+(times[7]-times[6])+" pause="+(times[6]-times[5])+". Death cancels remaining.");
  }
  [UnityTest]public IEnumerator WallAndTargetLossCancelPending()
  {
   yield return Spawn("BigBot_Burst_v10");yield return Until(()=>times.Count==1,4,"First shot");var wall=Wall();yield return new WaitForSeconds(.6f);Assert.That(times.Count,Is.EqualTo(1));Assert.That(bot.IsBursting,Is.False);Object.Destroy(wall);yield return null;yield return Until(()=>times.Count==2,4,"Reacquire");var hp=player.GetComponent<Health>();hp.Invincible=false;hp.Kill();yield return new WaitForSeconds(.6f);Assert.That(times.Count,Is.EqualTo(2));Assert.That(bot.IsBursting,Is.False);File.WriteAllText(Reports+"/cancellation.txt","Actual detector/LOS wall between projectiles cancels remainder. Reacquisition then target death cancels remainder. No late shots.");
  }
  [UnityTest]public IEnumerator EveryEnemyHitsBeyondOldRangeAndWallBlocks()
  {
   var result=new System.Text.StringBuilder();
   foreach(string n in new[]{"T07_HitFeedback_v04","HoverBot_CombatPolish","Turret_CombatPolish","BigBot_Burst_v10"})
   {
    yield return Spawn(n);float range=bot?bot.AttackDistance:enemy.DetectionModule.AttackRange;Move(new Vector3(-10,.03f,12-range*.96f));yield return null;yield return null;Assert.That(enemy.IsSeeingTarget,Is.True,n);Assert.That(enemy.IsTargetInAttackRange,Is.True,n);var hp=player.GetComponent<Health>();hp.MaxHealth=hp.CurrentHealth=10000;hp.Invincible=false;int accepted=0;float deadline=Time.time+6;
    while(hp.CurrentHealth>=10000&&Time.time<deadline){if(bot)bot.RequestAttack();else if(enemy.TryAtack(enemy.KnownDetectedTarget.transform.position))accepted++;yield return null;}
    Assert.That(hp.CurrentHealth,Is.LessThan(10000),n+" real projectile must reach target at "+range*.96f);hp.Invincible=true;result.AppendLine(n+" real projectile damaged player at "+range*.96f+"m; damage="+(10000-hp.CurrentHealth));var wall=Wall();yield return new WaitForSeconds(enemy.GetCurrentWeapon().DelayBetweenShots+.2f);yield return null;Assert.That(enemy.TryAtack(player.GetComponent<Actor>().AimPoint.position),Is.False,n+" wall blocks attack API");Object.Destroy(wall);Object.Destroy(enemy.gameObject);bot=null;yield return null;yield return null;foreach(var p in Object.FindObjectsByType<ProjectileBase>())Object.Destroy(p.gameObject);times.Clear();origins.Clear();
   }
   File.WriteAllText(Reports+"/range-hits.txt",result.ToString());
  }
 }
}