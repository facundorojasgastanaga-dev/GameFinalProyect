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
    public sealed class BigBotBazookaTests
    {
        const string Reports="Tools/IronRequiem/Reports/BigBotBazooka", Lab="Assets/IronRequiem/Scenes/BigBotBazookaLab.unity";
        BigBotCombat bot;EnemyController enemy;EnemyMobile mobile;PlayerCharacterController player;PlayerWeaponsManager manager;StolenWeaponCarrier carrier;Keyboard keys;Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;float capture;bool background;
        [UnitySetUp]public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime;background=Application.runInBackground;Time.captureDeltaTime=1f/60;Time.timeScale=1;Application.runInBackground=true;devices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.actions.devices=new InputDevice[]{keys,mouse};keys.MakeCurrent();mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode(Lab,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            player=Object.FindAnyObjectByType<PlayerCharacterController>();manager=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();bot=Object.FindAnyObjectByType<BigBotCombat>();enemy=bot.GetComponent<EnemyController>();mobile=bot.GetComponent<EnemyMobile>();
            Object.FindAnyObjectByType<GameFlowManager>().enabled=false;Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;player.GetComponent<Health>().Invincible=true;
            foreach(var e in Object.FindObjectsByType<EnemyController>()){e.enabled=false;if(e.NavMeshAgent.isOnNavMesh)e.NavMeshAgent.isStopped=true;var m=e.GetComponent<EnemyMobile>();if(m)m.enabled=false;var b=e.GetComponent<BigBotCombat>();if(b)b.enabled=false;var t=e.GetComponent<EnemyTurret>();if(t)t.enabled=false;}
            MovePlayer(new Vector3(-10,.03f,-7));enemy.PatrolPath=null;Assert.That(enemy.NavMeshAgent.Warp(new Vector3(-10,0,12)),Is.True);bot.transform.rotation=Quaternion.Euler(0,180,0);Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(.7f);
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;Time.captureDeltaTime=capture;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("BazookaCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(old);InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;Application.runInBackground=background;yield return null;}
        void MovePlayer(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
        void EnableBot(){bot.enabled=true;enemy.enabled=true;mobile.enabled=true;}
        IEnumerator Until(Func<bool> p,float timeout,string reason){float end=Time.time+timeout;while(!p()&&Time.time<end)yield return null;Assert.That(p(),Is.True,reason+"; visible="+enemy.IsSeeingTarget+" canAttack="+bot.CanAttack+" shots="+bot.ShotsFired+" pos="+bot.transform.position);}
        IEnumerator Tap(Key key){InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;}
        void Fire(bool held,bool aim=false)=>InputSystem.QueueStateEvent(mouse,new MouseState{buttons=(ushort)((held?1:0)|(aim?2:0))});
        GameObject Cover(Vector3 center,Vector3 scale){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="BazookaTestCover";go.transform.position=center;go.transform.localScale=scale;Physics.SyncTransforms();return go;}
        void Capture(string name,Camera camera=null){if(!camera)camera=player.PlayerCamera;var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;var target=camera.targetTexture;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Reports+"/"+name+".png",tex.EncodeToPNG());}finally{camera.targetTexture=target;RenderTexture.active=old;Object.Destroy(rt);Object.Destroy(tex);}}
        [UnityTest]public IEnumerator DistantNormalAttackAndCover()
        {
            Assert.That(bot.GetComponentsInChildren<Component>(true).All(c=>c),Is.True);Assert.That(bot.ExplosiveShots,Is.True);Assert.That(bot.AttackDistance,Is.EqualTo(22));Assert.That(bot.GetComponent<Health>().MaxHealth,Is.EqualTo(1200));
            var times=new List<float>();var origins=new List<int>();bot.ProjectileSpawned+=p=>{times.Add(Time.time);int closest=Enumerable.Range(0,2).OrderBy(i=>Vector3.Distance(p.InitialPosition,bot.Cannons[i].WeaponMuzzle.position)).First();origins.Add(closest);Assert.That(Vector3.Distance(p.InitialPosition,bot.Cannons[closest].WeaponMuzzle.position),Is.LessThan(.001f));var s=p.GetComponent<ProjectileStandard>();Assert.That(s.AreaOfDamage,Is.Not.Null);Assert.That(s.Speed,Is.EqualTo(22));Assert.That(s.Damage,Is.EqualTo(10));Assert.That(s.AreaOfDamage.AreaOfEffectDistance,Is.EqualTo(1.8f));};
            EnableBot();yield return Until(()=>times.Count>=4,9,"Two long-range salvos");Assert.That(origins.Distinct().Count(),Is.EqualTo(2));Assert.That(Vector3.Distance(bot.transform.position,player.transform.position),Is.GreaterThan(12));float period=times[2]-times[0];Assert.That(period,Is.InRange(2.45f,3.5f));Capture("bigbot-long-range");
            var wall=Cover(new Vector3(-10,2,2),new Vector3(10,5,.4f));yield return new WaitForSeconds(.3f);int before=times.Count;yield return new WaitForSeconds(2.5f);Assert.That(times.Count,Is.EqualTo(before));Assert.That(bot.IsPreparing,Is.False);Object.Destroy(wall);
            File.WriteAllText(Reports+"/normal.txt","Real EnemyMobile: attack at >12 m, both muzzles, explosive projectile settings, measured salvo period="+period+"; opaque cover stops preparation/shots. No desktop focus.");
        }
        Health Dummy(string name,Vector3 position)
        {var go=new GameObject(name);go.transform.position=position;var hp=go.AddComponent<Health>();hp.MaxHealth=100;go.AddComponent<Damageable>();var box=go.AddComponent<BoxCollider>();box.size=Vector3.one*.4f;return hp;}
        ProjectileStandard Launch(Vector3 from,Vector3 direction)
        {var go=Object.Instantiate(bot.Cannons[0].ProjectilePrefab.gameObject,from,Quaternion.LookRotation(direction));var p=go.GetComponent<ProjectileStandard>();p.Shoot(bot.Cannons[0]);return p;}
        [UnityTest]public IEnumerator FloorWallAndPlayerImpactsApplyAreaOnlyOnce()
        {
            var floor=Cover(new Vector3(60,-.1f,0),new Vector3(16,.2f,12));var wall=Cover(new Vector3(60,1.5f,0),new Vector3(.2f,3,5));var visible=Dummy("VisibleSplashTarget",new Vector3(59,.45f,0));var hidden=Dummy("CoveredSplashTarget",new Vector3(61,.45f,0));for(int i=0;i<3;i++)visible.gameObject.AddComponent<BoxCollider>().size=Vector3.one*.4f;
            yield return null;int damageEvents=0;visible.OnDamaged+=(d,o)=>damageEvents++;Physics.SyncTransforms();Launch(new Vector3(57,1.2f,0),Vector3.right);yield return new WaitForSeconds(.3f);Assert.That(damageEvents,Is.EqualTo(1));Assert.That(visible.CurrentHealth,Is.InRange(90,99.99f));Assert.That(hidden.CurrentHealth,Is.EqualTo(100));float wallDamage=100-visible.CurrentHealth;var review=new GameObject("ImpactReviewCamera").AddComponent<Camera>();review.CopyFrom(player.PlayerCamera);review.transform.position=new Vector3(56.6f,2,-4);review.transform.LookAt(new Vector3(60,1.2f,0));Capture("impact-wall",review);Object.Destroy(review.gameObject);
            Object.Destroy(wall);Object.Destroy(visible.gameObject);Object.Destroy(hidden.gameObject);yield return null;
            var near=Dummy("FloorSplashTarget",new Vector3(65.7f,.4f,0));yield return null;int floorEvents=0;near.OnDamaged+=(d,o)=>floorEvents++;Launch(new Vector3(65,3,0),Vector3.down);yield return new WaitForSeconds(.35f);Assert.That(floorEvents,Is.EqualTo(1));Assert.That(near.CurrentHealth,Is.InRange(90,99.99f));
            MovePlayer(new Vector3(60,.03f,3));player.enabled=false;var hp=player.GetComponent<Health>();hp.Invincible=false;hp.MaxHealth=1000;hp.CurrentHealth=1000;int playerEvents=0;hp.OnDamaged+=(d,o)=>playerEvents++;var aim=player.GetComponent<Actor>().AimPoint.position;Launch(aim+Vector3.back*3,Vector3.forward);yield return new WaitForSeconds(.3f);Assert.That(playerEvents,Is.EqualTo(1));Assert.That(1000-hp.CurrentHealth,Is.InRange(.01f,10.001f));
            int selfEvents=0;hp.OnDamaged+=(d,o)=>selfEvents++;var area=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Projectiles/Projectile_BigBot_StolenExplosive_v08.prefab").GetComponent<DamageArea>();area.InflictDamageInArea(65,aim,-1,QueryTriggerInteraction.Collide,player.gameObject);Assert.That(selfEvents,Is.Zero,"Projectile blast excludes own carrier");
            File.WriteAllText(Reports+"/area.txt","Actual ProjectileStandard impacts on wall, floor and player. Duplicate target colliders: one damage event. No direct+area sum. Covered target receives 0. Wall splash="+wallDamage+"; player direct-contact blast="+(1000-hp.CurrentHealth)+"; owner excluded.");
        }
        [UnityTest]public IEnumerator CannonLossBurstCoverAndDeathCancelPendingShots()
        {
            var times=new List<float>();bot.ProjectileSpawned+=p=>{times.Add(Time.time);if(bot.IsEmergency)Assert.That(Vector3.Distance(p.InitialPosition,bot.Cannons[1].WeaponMuzzle.position),Is.LessThan(.001f),"Only surviving cannon may spawn");};EnableBot();yield return Until(()=>bot.IsPreparing,3,"Initial warning");bot.GetComponent<BigBotDestruction>().DetachCannon();Assert.That(bot.IsPreparing,Is.False);Assert.That(bot.ActiveCannonCount,Is.EqualTo(1));Assert.That(bot.Cannons[0].enabled,Is.False);Assert.That(bot.LoseCannon(0),Is.False);Assert.That(times.Count,Is.Zero);
            yield return Until(()=>times.Count>=3,6,"Complete emergency burst");Assert.That(times.Count,Is.EqualTo(3));Assert.That(times[1]-times[0],Is.InRange(.25f,.31f));Assert.That(times[2]-times[1],Is.InRange(.25f,.31f));yield return new WaitForSeconds(1);Assert.That(times.Count,Is.EqualTo(3),"Readable pause after burst");
            yield return Until(()=>bot.IsPreparing,5,"Next burst preparation");var wall=Cover(new Vector3(-10,2,2),new Vector3(10,5,.4f));yield return new WaitForSeconds(1);Assert.That(times.Count,Is.EqualTo(3));Assert.That(bot.IsBursting,Is.False);Assert.That(bot.IsPreparing,Is.False);Object.Destroy(wall);yield return null;
            yield return Until(()=>times.Count>=4,6,"Burst resumes after recovering LOS");bot.GetComponent<Health>().TakeDamage(10000,player.gameObject);int atDeath=times.Count;yield return new WaitForSeconds(1);Assert.That(times.Count,Is.EqualTo(atDeath));Assert.That(bot.IsBursting,Is.False);Assert.That(bot.IsPreparing,Is.False);Assert.That(bot.GetComponent<BigBotDestruction>().CannonDropCount,Is.EqualTo(1));
            File.WriteAllText(Reports+"/emergency.txt","Cannon lost during preparation: cancelled, one drop, repeated loss ignored. Three surviving-cannon shots at "+(times[1]-times[0])+" / "+(times[2]-times[1])+" s; >1 s observed pause. LOS loss cancels pending burst. Death after first resumed shot cancels remaining shots.");
        }
        IEnumerator AimAtModule(StolenCannon module)
        {player.enabled=false;module.Body.isKinematic=true;module.transform.SetPositionAndRotation(new Vector3(-14,.3f,-9.6f),Quaternion.identity);Physics.SyncTransforms();module.transform.position+=Vector3.up*(.03f-module.SolidCollider.bounds.min.y);Physics.SyncTransforms();var center=module.SolidCollider.bounds.center;MovePlayer(new Vector3(center.x,.03f,center.z-1.7f));player.PlayerCamera.transform.LookAt(center);Physics.SyncTransforms();yield return new WaitForSeconds(.4f);}
        [UnityTest]public IEnumerator StolenBazookaFramingChargeThrowAndOverdrive()
        {
            bot.GetComponent<BigBotDestruction>().DetachCannon();var module=bot.GetComponent<BigBotDestruction>().FallenCannon.GetComponent<StolenCannon>();yield return AimAtModule(module);Assert.That(carrier.Candidate,Is.SameAs(module));yield return Tap(Key.E);yield return new WaitForSeconds(.8f);Assert.That(carrier.Held,Is.SameAs(module));player.PlayerCamera.transform.localRotation=Quaternion.identity;yield return null;
            var w=manager.GetActiveWeapon();Assert.That(w.SourcePrefab.name,Is.EqualTo("Weapon_BigBot_Bazooka_v08"));var visual=w.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BigBot_EquippedCannon");Assert.That(visual.localScale.x,Is.EqualTo(1.65f).Within(.001f));var guide=visual.Find("BazookaMuzzleReference");var attachment=w.WeaponRoot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Attachment");var hands=player.GetComponent<ProtagonistHandsPresentation>();Assert.That(hands.RightHandVisible,Is.False);
            float energy=module.Ammo.Remaining;var charge=w.GetComponent<CannonChargeShot>();var chargeFx=w.GetComponent<CannonChargePresentation>();int released=0;charge.ShotReleased+=_=>released++;int frames=0,nearCuts=0;float nearest=float.PositiveInfinity;
            for(int phase=0;phase<3;phase++)
            {
                Fire(phase>0,phase==1);float until=Time.time+.8f;while(Time.time<until){yield return new WaitForEndOfFrame();frames++;Assert.That(Vector3.Distance(guide.position,w.WeaponMuzzle.position),Is.LessThan(.001f));Assert.That(Vector3.Angle(guide.forward,w.WeaponMuzzle.forward),Is.LessThan(.1f));Assert.That(Vector3.Distance(hands.RightModuleSocket.position,attachment.position+w.WeaponMuzzle.forward*.003f),Is.LessThan(.001f));if(chargeFx.IsVisible)Assert.That(Vector3.Distance(chargeFx.EffectOrigin.position,w.WeaponMuzzle.position),Is.LessThan(.001f));foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())foreach(var v in filter.sharedMesh.vertices){float z=manager.WeaponCamera.WorldToViewportPoint(filter.transform.TransformPoint(v)).z;nearest=Mathf.Min(nearest,z);if(z<manager.WeaponCamera.nearClipPlane)nearCuts++;}}Capture("bazooka-phase-"+phase);
                Health direct=null,splash=null;int directHits=0,splashHits=0;
                if(phase==1){direct=Dummy("StolenDirectTarget",w.WeaponMuzzle.position+w.WeaponMuzzle.forward*5);splash=Dummy("StolenSplashTarget",direct.transform.position+w.WeaponMuzzle.right*.9f);direct.OnDamaged+=(d,o)=>directHits++;splash.OnDamaged+=(d,o)=>splashHits++;yield return null;Physics.SyncTransforms();}
                Fire(false,phase==1);yield return new WaitForSeconds(.25f);if(phase>0)Capture("bazooka-recoil-"+phase);
                if(phase==1){float expected=Mathf.Lerp(25,65,charge.LastShotPower);Assert.That(directHits,Is.EqualTo(1));Assert.That(splashHits,Is.EqualTo(1));Assert.That(100-direct.CurrentHealth,Is.EqualTo(expected).Within(.1f),"No direct plus area damage");Assert.That(100-splash.CurrentHealth,Is.InRange(.01f,expected-.01f));Capture("stolen-impact-area");File.WriteAllText(Reports+"/stolen-area.txt","Real charged projectile: charge power="+charge.LastShotPower+"; direct blast="+(100-direct.CurrentHealth)+"; adjacent splash="+(100-splash.CurrentHealth)+"; one event per target, no sum of direct and area damage.");Object.Destroy(direct.gameObject);Object.Destroy(splash.gameObject);}
            }
            Assert.That(nearCuts,Is.Zero,"Equipped model never crosses near plane in tested phases");Assert.That(released,Is.EqualTo(2));Assert.That(module.Ammo.Remaining,Is.LessThan(energy));Assert.That(chargeFx.IsVisible,Is.False);energy=module.Ammo.Remaining;yield return Tap(Key.G);yield return new WaitForSeconds(.4f);Assert.That(carrier.Held,Is.Null);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy).Within(.001f));yield return AimAtModule(module);yield return Tap(Key.E);yield return new WaitForSeconds(.8f);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy).Within(.001f));
            module.Ammo.Drain(module.Ammo.Remaining-10);yield return null;Assert.That(module.Overdrive.IsArmed,Is.True);Assert.That(module.Overdrive.Smoke.isPlaying,Is.True);int detonations=0;module.Explosion.Detonated+=()=>detonations++;yield return Tap(Key.G);yield return new WaitForSeconds(6);Assert.That(detonations,Is.EqualTo(1));Assert.That(carrier.Held,Is.Null);
            File.WriteAllText(Reports+"/stolen.txt","Actual E pickup, two charged shots (idle/ADS), energy, G throw, same-energy re-pickup, existing automatic critical OVERDRIVE, smoke and one thrown detonation. "+frames+" muzzle/effect/rear-socket aligned frames, near-plane cut vertices="+nearCuts+", nearest model vertex="+nearest+" m. Camera unchanged, no desktop focus. World pickup scale unchanged.");
        }
    }
}
