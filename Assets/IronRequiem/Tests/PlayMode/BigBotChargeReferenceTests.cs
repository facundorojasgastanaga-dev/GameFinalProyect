using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
    public sealed class BigBotChargeReferenceTests
    {
        const string Reports="Tools/IronRequiem/Reports/BigBotChargeReference";
        PlayerCharacterController player;PlayerWeaponsManager manager;StolenWeaponCarrier carrier;StolenCannon module;WeaponController weapon;CannonChargeShot charge;CannonChargePresentation presentation;Keyboard keys;Mouse mouse;float capture;bool background;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;int shots,frames,peak;
        [UnitySetUp]public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime;background=Application.runInBackground;Time.captureDeltaTime=1f/60;Time.timeScale=1;Application.runInBackground=true;devices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.actions.devices=new InputDevice[]{keys,mouse};keys.MakeCurrent();mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/BigBotChargeReferenceLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;player=Object.FindAnyObjectByType<PlayerCharacterController>();manager=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();player.GetComponent<Health>().Invincible=true;Object.FindAnyObjectByType<GameFlowManager>().enabled=false;Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;
            foreach(var enemy in Object.FindObjectsByType<EnemyController>()){enemy.enabled=false;if(enemy.NavMeshAgent.isOnNavMesh)enemy.NavMeshAgent.isStopped=true;var m=enemy.GetComponent<EnemyMobile>();if(m)m.enabled=false;var b=enemy.GetComponent<BigBotCombat>();if(b)b.enabled=false;var t=enemy.GetComponent<EnemyTurret>();if(t)t.enabled=false;}
            var destruction=Object.FindAnyObjectByType<BigBotDestruction>();destruction.DetachCannon();module=destruction.FallenCannon.GetComponent<StolenCannon>();Cursor.lockState=CursorLockMode.Locked;yield return AimAtModule();yield return Tap(Key.E);yield return new WaitForSeconds(.8f);Bind();player.PlayerCamera.transform.localRotation=Quaternion.identity;yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;Time.captureDeltaTime=capture;var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("ChargeReferenceCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(scene);InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;Application.runInBackground=background;yield return null;}
        void Bind(){weapon=manager.GetActiveWeapon();Assert.That(carrier.Held,Is.SameAs(module));charge=weapon.GetComponent<CannonChargeShot>();presentation=weapon.GetComponent<CannonChargePresentation>();weapon.OnShoot+=()=>shots++;Assert.That(presentation.UsesReferenceVortex,Is.True);}
        IEnumerator Tap(Key key){InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;}
        void Fire(bool held,bool aim=false,Vector2 delta=default)=>InputSystem.QueueStateEvent(mouse,new MouseState{buttons=(ushort)((held?1:0)|(aim?2:0)),delta=delta});
        IEnumerator AimAtModule()
        {player.enabled=false;module.Body.isKinematic=true;module.transform.SetPositionAndRotation(new Vector3(-14,.3f,-9.6f),Quaternion.identity);Physics.SyncTransforms();module.transform.position+=Vector3.up*(.03f-module.SolidCollider.bounds.min.y);Physics.SyncTransforms();var center=module.SolidCollider.bounds.center;var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(new Vector3(center.x,.03f,center.z-1.7f),Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;player.PlayerCamera.transform.LookAt(center);Physics.SyncTransforms();yield return new WaitForSeconds(.4f);}
        void Aligned()
        {
            frames++;Assert.That(Vector3.Distance(presentation.EffectOrigin.position,weapon.WeaponMuzzle.position),Is.LessThan(.0001f));Assert.That(Vector3.Angle(presentation.EffectOrigin.forward,weapon.WeaponMuzzle.forward),Is.LessThan(.01f));Assert.That(presentation.EnergyParticles.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.Local));peak=Mathf.Max(peak,presentation.EnergyParticles.particleCount);Assert.That(presentation.EnergyParticles.particleCount,Is.LessThanOrEqualTo(24));
            Assert.That(weapon.transform.Find("GunRoot"),Is.Not.Null);Assert.That(presentation.EffectOrigin.GetComponentsInChildren<Light>(true),Is.Empty);
        }
        void Hidden(){Assert.That(charge.IsCharging,Is.False);Assert.That(presentation.IsVisible,Is.False);Assert.That(presentation.EnergyParticles.IsAlive(true),Is.False);Assert.That(presentation.EnergyParticles.isPlaying,Is.False);}
        void Capture(Camera camera,string name,int width=1280,int height=720)
        {var rt=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);var active=RenderTexture.active;var target=camera.targetTexture;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Reports+"/"+name+".png",texture.EncodeToPNG());}finally{camera.targetTexture=target;RenderTexture.active=active;Object.Destroy(rt);Object.Destroy(texture);}}
        void EquivalentCapture(string name)
        {
            var camera=new GameObject("ReferenceComparisonCamera").AddComponent<Camera>();camera.cullingMask=manager.WeaponCamera.cullingMask;var settings=camera.GetUniversalAdditionalCameraData();var sourceSettings=player.PlayerCamera.GetUniversalAdditionalCameraData();settings.renderPostProcessing=sourceSettings.renderPostProcessing;settings.volumeLayerMask=sourceSettings.volumeLayerMask;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.23f,.23f);camera.nearClipPlane=.01f;camera.fieldOfView=40;var muzzle=weapon.WeaponMuzzle;camera.transform.position=muzzle.position+muzzle.right*.65f+muzzle.up*.30f+muzzle.forward*.42f;camera.transform.LookAt(muzzle.position+muzzle.forward*.12f,muzzle.up);Capture(camera,name,728,472);Object.Destroy(camera.gameObject);
        }
        [UnityTest]public IEnumerator PartialFullConsecutiveAndVisualParity()
        {
            var before=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_Bazooka_v08.prefab").GetComponent<WeaponController>();var after=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_ReferenceCharge_v09.prefab").GetComponent<WeaponController>();
            foreach(var field in typeof(WeaponController).GetFields(BindingFlags.Public|BindingFlags.Instance)){if(new[]{"WeaponRoot","WeaponMuzzle","WeaponAnimator","EjectionPort","OnShoot"}.Contains(field.Name))continue;Assert.That(field.GetValue(after),Is.EqualTo(field.GetValue(before)),"Gameplay field "+field.Name);}
            Assert.That(charge.MaximumChargeDuration,Is.EqualTo(1.2f));Assert.That(charge.MinimumEnergyCost,Is.EqualTo(12));Assert.That(charge.MaximumEnergyCost,Is.EqualTo(30));Assert.That(weapon.ProjectilePrefab,Is.SameAs(before.ProjectilePrefab));Assert.That(presentation.EffectOrigin.Find("OuterFrame").gameObject.activeSelf,Is.False,"Legacy square hidden only in variant");Assert.That(presentation.EffectOrigin.Find("BlueFilaments").GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(300));
            float energy=module.Ammo.Remaining;Fire(true);yield return new WaitForSeconds(.35f);yield return new WaitForEndOfFrame();Aligned();Capture(player.PlayerCamera,"partial-fps");Fire(false);yield return new WaitForSeconds(.2f);Assert.That(shots,Is.EqualTo(1));Assert.That(energy-module.Ammo.Remaining,Is.EqualTo(charge.LastEnergyCost).Within(.001f));Hidden();
            Fire(true);var fractions=new List<string>();foreach(float fraction in new[]{.04f,.12f,.5f,1}){float timeout=Time.time+2;while(charge.ChargeFraction<fraction&&Time.time<timeout){yield return new WaitForEndOfFrame();Aligned();}Assert.That(charge.ChargeFraction,Is.GreaterThanOrEqualTo(fraction-.001f));Assert.That(presentation.IsVisible,Is.True);EquivalentCapture("stage-"+fraction.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));fractions.Add(fraction+" captured="+charge.ChargeFraction);}
            Capture(player.PlayerCamera,"full-fps");Assert.That(peak,Is.GreaterThan(0));Fire(false);yield return new WaitForEndOfFrame();Aligned();EquivalentCapture("release");yield return new WaitForSeconds(.2f);Assert.That(shots,Is.EqualTo(2));Hidden();
            for(int i=0;i<2;i++){Fire(true);yield return new WaitForSeconds(.32f);yield return new WaitForEndOfFrame();Aligned();Assert.That(presentation.EnergyParticles.isPlaying,Is.True);Fire(false);yield return new WaitForSeconds(.23f);Hidden();}Assert.That(shots,Is.EqualTo(4));Assert.That(module.Overdrive.IsArmed,Is.False);
            File.WriteAllText(Reports+"/stages.txt","Actual partial/full/two consecutive charged shots="+shots+"; aligned frames="+frames+"; peak particles="+peak+"/24. Original gameplay fields/projectile and 1.2 s charge unchanged.\n"+string.Join("\n",fractions));
        }
        [UnityTest]public IEnumerator CancelMoveSwapThrowAndDeathCleanEffects()
        {
            float energy=module.Ammo.Remaining;Fire(true);yield return new WaitForSeconds(.5f);charge.CancelPendingShot();Fire(false);yield return new WaitForSeconds(.1f);Hidden();Assert.That(shots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy));
            player.enabled=true;InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));Fire(true,true,new Vector2(15,-7));float until=Time.time+.45f;while(Time.time<until){yield return new WaitForEndOfFrame();Aligned();}InputSystem.QueueStateEvent(keys,new KeyboardState());Fire(true,false);yield return null;yield return Tap(Key.Digit1);Fire(false);yield return new WaitForSeconds(.5f);Hidden();Assert.That(shots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy));
            yield return Tap(Key.Digit2);yield return new WaitForSeconds(.5f);Fire(true);yield return new WaitForSeconds(.3f);Assert.That(presentation.IsVisible,Is.True);var oldEffect=presentation.EffectOrigin.gameObject;yield return Tap(Key.G);Fire(false);yield return new WaitForSeconds(.2f);Assert.That(carrier.Held,Is.Null);Assert.That(!oldEffect||!oldEffect.activeInHierarchy,Is.True);Assert.That(shots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy));
            yield return AimAtModule();yield return Tap(Key.E);yield return new WaitForSeconds(.6f);Bind();player.PlayerCamera.transform.localRotation=Quaternion.identity;Fire(true);yield return new WaitForSeconds(.3f);Aligned();var deathEffect=presentation.EffectOrigin.gameObject;var health=player.GetComponent<Health>();health.Invincible=false;health.TakeDamage(health.MaxHealth+1,module.gameObject);yield return null;yield return null;Assert.That(carrier.Held,Is.Null);Assert.That(!deathEffect||!deathEffect.activeInHierarchy,Is.True);Assert.That(Object.FindObjectsByType<ParticleSystem>().Where(p=>p.name=="ReferenceMotes").All(p=>!p.isPlaying&&!p.IsAlive(true)),Is.True);Assert.That(shots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy));
            File.WriteAllText(Reports+"/cleanup.txt","Cancel, movement/mouselook/ADS alignment, weapon switch, G throw and death during charge: no accepted shots, no energy consumed, no visible or live reference particles. All input virtual; no window activation. Alignment frames="+frames);
        }
    }
}
