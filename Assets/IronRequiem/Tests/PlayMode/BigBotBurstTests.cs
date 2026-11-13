using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
    public sealed class BigBotBurstTests
    {
        const string Reports="Tools/IronRequiem/Reports/BigBotBurst";PlayerCharacterController player;PlayerWeaponsManager manager;StolenWeaponCarrier carrier;BigBotCombat bot;EnemyController enemy;EnemyMobile mobile;StolenCannon module;WeaponController weapon;CannonBurstShot burst;Keyboard keys;Mouse mouse;float capture;bool background;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;readonly List<float> shots=new List<float>();
        [UnitySetUp]public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime;background=Application.runInBackground;Time.captureDeltaTime=1f/60;Time.timeScale=1;Application.runInBackground=true;devices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.actions.devices=new InputDevice[]{keys,mouse};keys.MakeCurrent();mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/BigBotBurstLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;player=Object.FindAnyObjectByType<PlayerCharacterController>();manager=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();player.GetComponent<Health>().Invincible=true;Object.FindAnyObjectByType<GameFlowManager>().enabled=false;Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;bot=Object.FindAnyObjectByType<BigBotCombat>();enemy=bot.GetComponent<EnemyController>();mobile=bot.GetComponent<EnemyMobile>();
            foreach(var e in Object.FindObjectsByType<EnemyController>()){e.enabled=false;if(e.NavMeshAgent.isOnNavMesh)e.NavMeshAgent.isStopped=true;var m=e.GetComponent<EnemyMobile>();if(m)m.enabled=false;var b=e.GetComponent<BigBotCombat>();if(b)b.enabled=false;var t=e.GetComponent<EnemyTurret>();if(t)t.enabled=false;}
            enemy.PatrolPath=null;Assert.That(enemy.NavMeshAgent.Warp(new Vector3(-10,0,12)),Is.True);bot.transform.rotation=Quaternion.Euler(0,180,0);Move(new Vector3(-10,.03f,-7));Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(.7f);shots.Clear();
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;Time.captureDeltaTime=capture;var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("BurstCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(scene);InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;Application.runInBackground=background;yield return null;}
        void Move(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
        void EnableBot(){bot.enabled=true;enemy.enabled=true;mobile.enabled=true;}
        IEnumerator Until(Func<bool> p,float timeout,string message){float end=Time.time+timeout;while(!p()&&Time.time<end)yield return null;Assert.That(p(),Is.True,message+"; shots="+shots.Count+" AI="+bot.ShotsFired+" visible="+enemy.IsSeeingTarget);}
        IEnumerator Tap(Key key){InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;}
        void Fire(bool held)=>InputSystem.QueueStateEvent(mouse,new MouseState{buttons=(ushort)(held?1:0)});
        GameObject Wall(){var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="BurstOcclusion";wall.transform.position=new Vector3(-10,2,2);wall.transform.localScale=new Vector3(12,5,.4f);Physics.SyncTransforms();return wall;}
        List<int> TrackBot()
        {var origins=new List<int>();bot.ProjectileSpawned+=p=>{shots.Add(Time.time);int i=Enumerable.Range(0,2).OrderBy(j=>Vector3.Distance(p.InitialPosition,bot.Cannons[j].WeaponMuzzle.position)).First();origins.Add(i);Assert.That(bot.IsCannonActive(i),Is.True);Assert.That(Vector3.Distance(p.InitialPosition,bot.Cannons[i].WeaponMuzzle.position),Is.LessThan(.001f));var standard=p.GetComponent<ProjectileStandard>();Assert.That(standard.Damage,Is.EqualTo(10));Assert.That(standard.AreaOfDamage.AreaOfEffectDistance,Is.EqualTo(1.8f));};return origins;}
        [UnityTest]public IEnumerator NormalAlternatesThreeAndRespectsPause()
        {
            Assert.That(bot.AlternatingBursts,Is.True);var origins=TrackBot();EnableBot();yield return Until(()=>shots.Count>=6,9,"Two normal bursts");Assert.That(origins.Take(6),Is.EqualTo(new[]{0,1,0,1,0,1}));foreach(int i in new[]{1,2,4,5})Assert.That(shots[i]-shots[i-1],Is.InRange(.175f,.22f));Assert.That(shots[3]-shots[2],Is.GreaterThanOrEqualTo(2.18f));Assert.That(bot.Damage,Is.EqualTo(10));Assert.That(bot.ProjectileSpeed,Is.EqualTo(22));Assert.That(bot.AttackDistance,Is.EqualTo(22));File.WriteAllText(Reports+"/normal.txt","Six real AI projectiles: alternating "+string.Join(",",origins.Take(6))+"; intervals="+(shots[1]-shots[0])+","+(shots[2]-shots[1])+"; gap between bursts including .7 s warning="+(shots[3]-shots[2])+". Damage 10 each, 30 total per burst; explosive radius/range/speed retained.");
        }
        [UnityTest]public IEnumerator ObstacleAndTargetLossCancelBetweenShots()
        {
            TrackBot();EnableBot();yield return Until(()=>shots.Count==1,4,"First projectile");var wall=Wall();yield return new WaitForSeconds(.6f);Assert.That(shots.Count,Is.EqualTo(1));Assert.That(bot.IsBursting,Is.False);Object.Destroy(wall);yield return null;yield return Until(()=>shots.Count==2,5,"Reacquired target");var hp=player.GetComponent<Health>();hp.Invincible=false;hp.TakeDamage(hp.MaxHealth+100,bot.gameObject);yield return new WaitForSeconds(.6f);Assert.That(shots.Count,Is.EqualTo(2));Assert.That(bot.IsBursting,Is.False);File.WriteAllText(Reports+"/occlusion.txt","Opaque wall inserted after first projectile cancels remainder. After LOS recovery, target death after next first projectile cancels remaining shots. Real detector and EnemyMobile; no through-wall shots.");
        }
        [UnityTest]public IEnumerator CannonLossContinuesOnlyRemainingBudgetAndEmergencyDeath()
        {
            var origins=TrackBot();EnableBot();yield return Until(()=>shots.Count==1,4,"First normal shot");bot.GetComponent<BigBotDestruction>().DetachCannon();Assert.That(bot.ActiveCannonCount,Is.EqualTo(1));yield return Until(()=>shots.Count>=6,7,"Remaining burst plus emergency burst");Assert.That(origins.Skip(1).All(i=>i==1),Is.True);Assert.That(shots.Count,Is.EqualTo(6));Assert.That(shots[2]-shots[1],Is.InRange(.115f,.155f));Assert.That(shots[4]-shots[3],Is.InRange(.115f,.155f));Assert.That(shots[3]-shots[2],Is.InRange(1.58f,1.75f));Assert.That(bot.GetComponent<BigBotDestruction>().CannonDropCount,Is.EqualTo(1));yield return Until(()=>shots.Count==7,5,"Next emergency starts");bot.GetComponent<Health>().TakeDamage(10000,player.gameObject);yield return new WaitForSeconds(.6f);Assert.That(shots.Count,Is.EqualTo(7));Assert.That(bot.IsBursting,Is.False);File.WriteAllText(Reports+"/emergency.txt","Loss during burst retained exactly two pending projectiles, no fresh extra burst. Surviving cannon only. Emergency interval="+(shots[4]-shots[3])+"; gap including warning="+(shots[3]-shots[2])+". Death after first shot cancels pending shots. Single drop.");
        }
        IEnumerator AimAtModule()
        {player.enabled=false;module.Body.isKinematic=true;module.transform.SetPositionAndRotation(new Vector3(-14,.3f,-9.6f),Quaternion.identity);Physics.SyncTransforms();module.transform.position+=Vector3.up*(.03f-module.SolidCollider.bounds.min.y);Physics.SyncTransforms();var center=module.SolidCollider.bounds.center;Move(new Vector3(center.x,.03f,center.z-1.7f));player.PlayerCamera.transform.LookAt(center);Physics.SyncTransforms();yield return new WaitForSeconds(.4f);}
        void BindPlayer(){weapon=manager.GetActiveWeapon();burst=weapon.GetComponent<CannonBurstShot>();Assert.That(burst,Is.Not.Null);weapon.OnShoot+=()=>shots.Add(Time.time);Assert.That(weapon.GetComponent<CannonChargeShot>(),Is.Null);Assert.That(weapon.GetComponent<CannonChargePresentation>(),Is.Null);Assert.That(weapon.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("ChargeEnergy")),Is.False);Assert.That(weapon.ProjectilePrefab.GetComponent<ChargedProjectileDamage>(),Is.Null);Assert.That(weapon.ProjectilePrefab.GetComponent<ProjectileStandard>().Damage,Is.EqualTo(25));}
        IEnumerator Equip()
        {bot.GetComponent<BigBotDestruction>().DetachCannon();module=bot.GetComponent<BigBotDestruction>().FallenCannon.GetComponent<StolenCannon>();yield return AimAtModule();yield return Tap(Key.E);yield return new WaitForSeconds(.8f);Assert.That(carrier.Held,Is.SameAs(module));BindPlayer();player.PlayerCamera.transform.localRotation=Quaternion.identity;}
        [UnityTest]public IEnumerator ClickCompletesAfterReleaseAndInsufficientEnergy()
        {
            yield return Equip();var t07=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab");Assert.That(t07.GetComponent<CannonChargeShot>().MaximumChargeDuration,Is.EqualTo(1.2f));Fire(true);yield return null;yield return null;Fire(false);yield return Until(()=>shots.Count==3,1,"Released click completes burst");yield return new WaitForSeconds(.3f);Assert.That(shots.Count,Is.EqualTo(3));Assert.That(module.Ammo.Remaining,Is.EqualTo(64).Within(.001f));Assert.That(burst.TotalEnergySpent,Is.EqualTo(36));foreach(int i in new[]{1,2})Assert.That(shots[i]-shots[i-1],Is.InRange(.175f,.22f));
            yield return new WaitForSeconds(1.5f);module.Ammo.Drain(module.Ammo.Remaining-35);Fire(true);yield return null;yield return null;Fire(false);yield return Until(()=>shots.Count==5,1,"Only two affordable shots");yield return new WaitForSeconds(.4f);Assert.That(shots.Count,Is.EqualTo(5));Assert.That(burst.TotalEnergySpent,Is.EqualTo(60));Assert.That(burst.PendingShots,Is.Zero);Assert.That(module.Overdrive.IsArmed,Is.True);Assert.That(module.Overdrive.Smoke.isPlaying,Is.True);File.WriteAllText(Reports+"/energy.txt","Actual E pickup; tap/release completes three shots at "+(shots[1]-shots[0])+" s; energy 100 to 64, exactly 36. With 35 remaining: two more projectiles, exactly 24 additional energy spent, no third charge, existing critical OVERDRIVE stops queue. T07 charge remains 1.2 s. No charge components/VFX or charged projectile adapter on new weapon.");
        }
        [UnityTest]public IEnumerator HeldRepeatsAndReleaseFinishesCurrentBurst()
        {
            yield return Equip();Fire(true);yield return Until(()=>shots.Count==4,4,"Held M1 repeats after pause");Fire(false);yield return Until(()=>shots.Count==6,1,"Release during burst still finishes");yield return new WaitForSeconds(1.7f);Assert.That(shots.Count,Is.EqualTo(6));Assert.That(module.Ammo.Remaining,Is.EqualTo(28).Within(.001f));Assert.That(burst.TotalEnergySpent,Is.EqualTo(72));Assert.That(shots[3]-shots[2],Is.InRange(1.49f,1.6f));Assert.That(burst.PendingShots,Is.Zero);Assert.That(weapon.WeaponMuzzle.GetComponentsInChildren<ParticleSystem>().Length,Is.Zero,"Brief muzzle particles retire");File.WriteAllText(Reports+"/held.txt","Holding M1 produced two three-shot bursts; released after shot four, completed six without another burst. Pause measured="+(shots[3]-shots[2])+"; energy spent 72, remaining 28. Native shot path produces recoil/muzzle/impact on each projectile; no lingering muzzle objects.");
        }
        [UnityTest]public IEnumerator SwitchThrowAndDeathInterruptBurstWithoutDelayedShots()
        {
            yield return Equip();Fire(true);yield return Until(()=>shots.Count==1,.5f,"First player projectile");yield return Tap(Key.Digit1);yield return new WaitForSeconds(.5f);Assert.That(shots.Count,Is.EqualTo(1));Assert.That(burst.PendingShots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(88));yield return Tap(Key.Digit2);yield return new WaitForSeconds(1.7f);Assert.That(shots.Count,Is.EqualTo(1),"Held M1 cannot restart after weapon switch");Fire(false);yield return null;Fire(true);yield return Until(()=>shots.Count==2,.5f,"Fresh press");yield return Tap(Key.G);Fire(false);yield return new WaitForSeconds(.4f);Assert.That(carrier.Held,Is.Null);Assert.That(shots.Count,Is.EqualTo(2));Assert.That(module.Ammo.Remaining,Is.EqualTo(76));yield return AimAtModule();yield return Tap(Key.E);yield return new WaitForSeconds(.8f);BindPlayer();player.PlayerCamera.transform.localRotation=Quaternion.identity;Fire(true);yield return Until(()=>shots.Count==3,.5f,"Shot before death");var hp=player.GetComponent<Health>();hp.Invincible=false;hp.TakeDamage(hp.MaxHealth+100,bot.gameObject);yield return new WaitForSeconds(.4f);Assert.That(carrier.Held,Is.Null);Assert.That(shots.Count,Is.EqualTo(3));Assert.That(module.Ammo.Remaining,Is.EqualTo(64));File.WriteAllText(Reports+"/interruptions.txt","Switch, G throw and player death each after first projectile: remainder cancelled, no delayed shots. Returning with M1 held needs fresh press. Three actual projectiles total, 36 energy, same-energy re-pickup. No desktop focus/control.");
        }
    }
}
