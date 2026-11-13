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
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
 public sealed class SawBotWeaponDeathTests
 {
  const string Enemy="Assets/IronRequiem/Prefabs/Enemies/SawBot_Arsenal_v03.prefab",Lab="Assets/IronRequiem/Scenes/SawBotWeaponDeathLab.unity",Report="Tools/IronRequiem/Reports/SawBotWeaponDeath";
  PlayerCharacterController player;PlayerWeaponsManager weapons;StolenWeaponCarrier carrier;SawBotCombat bot;Keyboard keys;Mouse mouse;float capture;bool background;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
  [UnitySetUp]public IEnumerator Setup()
  {
   capture=Time.captureDeltaTime;background=Application.runInBackground;Application.runInBackground=true;Time.captureDeltaTime=1f/60;Time.timeScale=1;AudioListener.pause=false;devices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,mouse};
   EditorSceneManager.LoadSceneInPlayMode(Lab,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;var menu=Object.FindAnyObjectByType<CombatLabMenu>();menu.BeginGame();menu.enabled=false;var waves=Object.FindAnyObjectByType<CombatTwoRoomsLab>();waves.enabled=false;if(waves.SpawnQueue)waves.SpawnQueue.enabled=false;Object.FindAnyObjectByType<GameFlowManager>().enabled=false;Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;
   player=Object.FindAnyObjectByType<PlayerCharacterController>();player.enabled=false;player.GetComponent<Health>().Invincible=true;weapons=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();foreach(var c in Object.FindObjectsByType<Collider>())if(!c.transform.IsChildOf(player.transform)&&!(c.bounds.size.y<2&&c.bounds.size.x>20&&c.bounds.size.z>20))c.enabled=false;
   MovePlayer(new Vector3(-12,.03f,-10));Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(.7f);
  }
  [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;Time.captureDeltaTime=capture;InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("SawArsenalCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(scene);Application.runInBackground=background;}
  void MovePlayer(Vector3 p){var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);c.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
  IEnumerator Spawn(Vector3 p,bool moving=false){Assert.That(NavMesh.SamplePosition(p,out var hit,3,NavMesh.AllAreas),Is.True);var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy),hit.position,Quaternion.Euler(0,180,0));bot=go.GetComponent<SawBotCombat>();var path=new GameObject("ArsenalPatrol").AddComponent<PatrolPath>();path.PathNodes.Add(player.transform);go.GetComponent<EnemyController>().PatrolPath=path;yield return null;yield return null;if(!moving){bot.GetComponent<EnemyMobile>().enabled=false;bot.GetComponent<EnemyController>().enabled=false;if(bot.GetComponent<NavMeshAgent>().isOnNavMesh)bot.GetComponent<NavMeshAgent>().isStopped=true;}}
  void Aim(GameObject go){player.PlayerCamera.transform.LookAt(go.GetComponent<Actor>().AimPoint.position);Cursor.lockState=CursorLockMode.Locked;Physics.SyncTransforms();}
  IEnumerator Tap(Key key){InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;}
  IEnumerator Until(Func<bool> condition,float seconds,string label){float end=Time.time+seconds,real=Time.realtimeSinceStartup+60;while(!condition()&&Time.time<end&&Time.realtimeSinceStartup<real)yield return null;Assert.That(condition(),Is.True,label);}
  IEnumerator ExecuteSaw()
  {
   yield return Spawn(new Vector3(-12,0,-8.2f));bot.GetComponent<Health>().TakeDamage(bot.GetComponent<Health>().MaxHealth*.3f,player.gameObject);Aim(bot.gameObject);yield return null;yield return null;Assert.That(carrier.ExecutionCandidate,Is.SameAs(bot.GetComponent<EnemyExecutionSource>()));yield return Tap(Key.E);Assert.That(carrier.Held,Is.Not.Null);yield return Until(()=>bot.IsDead,2,"Execution ends at existing punch");yield return new WaitForSeconds(.6f);Assert.That(carrier.Held.EquippedWeapon.GetComponent<StolenSawAttack>(),Is.Not.Null);
  }
  Camera View(Vector3 relative)
  {
   var camera=new GameObject("SawArsenalView").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=bot.transform.position+relative;camera.transform.LookAt(bot.transform.position+Vector3.up*.65f);camera.fieldOfView=42;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.14f,.17f);var light=new GameObject("ArsenalKey").AddComponent<Light>();light.type=LightType.Directional;light.intensity=3;light.transform.rotation=Quaternion.Euler(35,140,0);return camera;
  }
  static void Capture(Camera camera,string name)
  {var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Report+"/"+name+".png",tex.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;Object.Destroy(rt);Object.Destroy(tex);}}
  float Bottom(SawBotCombat target)
  {
   float min=float.PositiveInfinity;foreach(var skin in target.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(skin.forceRenderingOff)continue;var mesh=skin.sharedMesh;var vs=mesh.vertices;var ws=mesh.boneWeights;var bind=mesh.bindposes;var matrices=skin.bones.Select(b=>target.transform.worldToLocalMatrix*b.localToWorldMatrix).ToArray();for(int i=0;i<vs.Length;i++)min=Mathf.Min(min,matrices[ws[i].boneIndex0].MultiplyPoint3x4(bind[ws[i].boneIndex0].MultiplyPoint3x4(vs[i])).y);}return min;
  }
  [UnityTest]public IEnumerator HeavyDeathAndFloorAcrossLods()
  {
   yield return Spawn(new Vector3(-12,0,-6));var position=bot.transform.position;var rig=bot.Animator.transform.Find("Rig");var camera=View(new Vector3(3,1.5f,-3.8f));bot.GetComponent<Health>().Kill();Assert.That(bot.IsDead,Is.True);Assert.That(bot.GetComponent<EnemyMobile>().enabled,Is.False);Assert.That(bot.GetComponent<NavMeshAgent>().enabled,Is.False);float worst=float.PositiveInfinity;
   for(int i=0;i<126;i++){yield return null;worst=Mathf.Min(worst,Bottom(bot));if(i==12||i==45||i==82||i==123)Capture(camera,"death-"+i);}
   Assert.That(worst,Is.GreaterThan(-.035f),"Heavy collapse must not penetrate flat floor");Assert.That(Quaternion.Angle(Quaternion.identity,rig.localRotation),Is.GreaterThan(65));Assert.That(bot.transform.position,Is.EqualTo(position));Assert.That(bot.DeathCount,Is.EqualTo(1));File.WriteAllText(Report+"/death.txt","Three LOD skins sampled126 frames; minimum root-space floor="+worst+"m. Rig tips>65deg; gameplay root fixed, agent/attacks stop immediately. Original wheels not driven by death clip.");yield return new WaitForSeconds(2.1f);Assert.That(!bot,Is.True,"Existing corpse cleanup remains active");
  }
  [UnityTest]public IEnumerator DeathFromContactAndNormalKillDoesNotDropWeapon()
  {
   yield return Spawn(new Vector3(-12,0,-7),true);yield return Until(()=>bot.IsAttacking,6,"Saw prepares contact");yield return new WaitForSeconds(.4f);Assert.That(bot.IsAttacking,Is.True);int modules=Object.FindObjectsByType<StolenCannon>().Length;bot.GetComponent<Health>().Kill();yield return new WaitForSeconds(.3f);Assert.That(bot.Animator.GetCurrentAnimatorStateInfo(0).IsName("DeathContact"),Is.True);Assert.That(Object.FindObjectsByType<StolenCannon>().Length,Is.EqualTo(modules));Assert.That(bot.GetComponent<SawBotWeaponSource>().WeaponRemoved,Is.False);Assert.That(bot.IsAttacking,Is.False);
  }
  [UnityTest]public IEnumerator ExecutionSingleDeliveryAndHiddenArmAcrossLods()
  {
   yield return ExecuteSaw();var source=bot.GetComponent<EnemyExecutionSource>();var module=carrier.Held;Assert.That(source.Delivered,Is.True);Assert.That(bot.GetComponent<SawBotWeaponSource>().WeaponRemoved,Is.True);Assert.That(bot.Animator.GetCurrentAnimatorStateInfo(0).IsName("DeathDisarmed"),Is.True);
   foreach(var view in bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("RightSaw")))Assert.That(view.forceRenderingOff,Is.True);for(int lod=0;lod<3;lod++){bot.GetComponent<LODGroup>().ForceLOD(lod);yield return null;Assert.That(bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("RightSaw")).All(r=>r.forceRenderingOff),Is.True);}
   Assert.That(module.Ammo.Remaining,Is.EqualTo(100));Assert.That(carrier.TryExecute(source),Is.False);Assert.That(Object.FindObjectsByType<StolenCannon>().Length,Is.EqualTo(1));var hands=player.GetComponent<ProtagonistHandsPresentation>();Assert.That(hands.CurrentRepresentation,Is.EqualTo(ProtagonistHandsPresentation.Representation.StolenCannon));Capture(player.PlayerCamera,"equipped-saw");File.WriteAllText(Report+"/execution.txt","Actual E interaction, existing rip/punch execution, one delivered module, hand replaced through real inventory, right saw hidden in every enemy LOD, DeathDisarmed and100 energy. Repeated execution rejected.");
  }
  [UnityTest]public IEnumerator HeldSawDamageEnergyWallAndCancellation()
  {
   yield return ExecuteSaw();var module=carrier.Held;var attack=module.EquippedWeapon.GetComponent<StolenSawAttack>();player.PlayerCamera.transform.localRotation=Quaternion.identity;yield return Spawn(new Vector3(-12,0,-8.7f));bot.enabled=false;bot.GetComponent<NavMeshAgent>().enabled=false;bot.transform.position=new Vector3(player.transform.position.x,.03f,player.transform.position.z+1.5f);Physics.SyncTransforms();var hp=bot.GetComponent<Health>();hp.MaxHealth=hp.CurrentHealth=5000;Aim(bot.gameObject);yield return null;float before=hp.CurrentHealth,energy=module.Ammo.Remaining;
   int projectiles=Object.FindObjectsByType<ProjectileBase>().Length;InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(1);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
   float damage=before-hp.CurrentHealth,cost=energy-module.Ammo.Remaining;Assert.That(damage,Is.EqualTo(45).Within(4.6));Assert.That(cost,Is.EqualTo(12).Within(1.21));Assert.That(Object.FindObjectsByType<ProjectileBase>().Length,Is.EqualTo(projectiles));Assert.That(attack.IsRunning,Is.False);before=hp.CurrentHealth;yield return new WaitForSeconds(.3f);Assert.That(hp.CurrentHealth,Is.EqualTo(before));
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="SawPlayerWall";wall.transform.position=(player.PlayerCamera.transform.position+bot.GetComponent<Actor>().AimPoint.position)*.5f;wall.transform.rotation=Quaternion.LookRotation(bot.GetComponent<Actor>().AimPoint.position-player.PlayerCamera.transform.position);File.WriteAllText(Report+"/wall-setup.txt","Camera="+player.PlayerCamera.transform.position+" actual NavMesh target="+bot.transform.position+" wall="+wall.transform.position);wall.transform.localScale=new Vector3(5,4,.12f);Physics.SyncTransforms();yield return null;before=hp.CurrentHealth;InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.5f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;Assert.That(hp.CurrentHealth,Is.EqualTo(before));
   File.WriteAllText(Report+"/player-dps.txt","M1 hold1s: damage="+damage+", energy="+cost+". No projectile, no duplicate damage among hitboxes, wall blocks contact; release stops damage. Config45DPS,12energy/s.");
  }
  [UnityTest]public IEnumerator SwitchThrowRecollectCriticalAndPause()
  {
   yield return ExecuteSaw();var module=carrier.Held;var weapon=module.EquippedWeapon;var attack=weapon.GetComponent<StolenSawAttack>();InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.3f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;float energy=module.Ammo.Remaining;weapons.SwitchWeapon(true);yield return new WaitForSeconds(.6f);Assert.That(attack.IsRunning,Is.False);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy).Within(.001));int index=Enumerable.Range(0,9).Single(i=>weapons.GetWeaponAtSlotIndex(i)==weapon);weapons.SwitchToWeaponIndex(index,true);yield return new WaitForSeconds(.7f);
   Assert.That(carrier.ThrowHeld(),Is.True);Assert.That(carrier.Held,Is.Null);module.Body.isKinematic=true;module.transform.position=player.PlayerCamera.transform.position+Vector3.forward*1.1f;module.transform.rotation=Quaternion.identity;Physics.SyncTransforms();player.PlayerCamera.transform.LookAt(module.SolidCollider.bounds.center);yield return new WaitForSeconds(.4f);yield return null;Assert.That(carrier.Candidate,Is.SameAs(module));yield return Tap(Key.E);yield return new WaitForSeconds(.7f);Assert.That(carrier.Held,Is.SameAs(module));Assert.That(module.Ammo.Remaining,Is.EqualTo(energy).Within(.001));
   module.Ammo.Drain(module.Ammo.Remaining-19);Assert.That(module.Overdrive.IsArmed,Is.True);Assert.That(module.Overdrive.Smoke.isPlaying,Is.True);float pausedEnergy=module.Ammo.Remaining;Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);Assert.That(module.Ammo.Remaining,Is.EqualTo(pausedEnergy));Time.timeScale=1;int explosions=0;module.Explosion.Detonated+=()=>explosions++;Assert.That(carrier.ThrowHeld(),Is.True);yield return new WaitForSeconds(5.5f);Assert.That(explosions,Is.EqualTo(1));Assert.That(carrier.Held,Is.Null);File.WriteAllText(Report+"/resource-cycle.txt","Switch preserves resource; G/native throw then E recollect preserves same instance/energy. Critical smoke and OVERDRIVE inherited; pause freezes drain; thrown critical module detonates once.");
  }
  [UnityTest]public IEnumerator LegacyChargeAndBurstStillBind()
  {
   foreach(string path in new[]{"Assets/IronRequiem/Prefabs/FastCombat/T07_Module.prefab","Assets/IronRequiem/Prefabs/FastCombat/BigBot_Module.prefab"})
   {
    var module=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)).GetComponent<StolenCannon>();var body=module.GetComponent<Rigidbody>();if(!body)body=module.gameObject.AddComponent<Rigidbody>();var box=module.GetComponent<BoxCollider>();if(!box)box=module.gameObject.AddComponent<BoxCollider>();body.isKinematic=true;module.FitPickupCollider(box);module.MakeAvailable(body,box,true);module.transform.position=player.PlayerCamera.transform.position+Vector3.forward;module.transform.rotation=Quaternion.identity;Physics.SyncTransforms();player.PlayerCamera.transform.LookAt(box.bounds.center);yield return null;yield return null;Assert.That(carrier.Candidate,Is.SameAs(module));yield return Tap(Key.E);yield return new WaitForSeconds(.7f);Assert.That(carrier.Held,Is.SameAs(module));var weapon=module.EquippedWeapon;Assert.That(weapon.GetComponent<IStolenWeaponBinding>(),Is.Not.Null);InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.35f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.6f);Assert.That(module.Ammo.Remaining,Is.LessThan(module.Ammo.Capacity),"Existing T07 charged and Big Bot burst inputs still consume on actual release/shot");Assert.That(carrier.ThrowHeld(),Is.True);Object.Destroy(module.gameObject);yield return null;
   }
  }
 }
}
