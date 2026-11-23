using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
namespace IronRequiem.Tests
{
    public sealed class ProtagonistActionsTests
    {
        const string Reports="Tools/IronRequiem/Reports/ProtagonistActions/SlideFacing";
        PlayerCharacterController player;PlayerWeaponsManager weapons;StolenWeaponCarrier carrier;ProtagonistHandsPresentation hands;ProtagonistMelee melee;ProtagonistSlidePresentation legs;SlideContactEffects sparks;
        Keyboard keys;Mouse mouse;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        [UnitySetUp]public IEnumerator Setup()
        {
            Time.timeScale=1;Application.runInBackground=true;previousDevices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,mouse};
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/ProtagonistActionsLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;yield return null;
            Object.FindAnyObjectByType<GameFlowManager>().enabled=false;Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;
            foreach(var e in Object.FindObjectsByType<EnemyController>()){if(e.GetComponent<EnemyMobile>())e.GetComponent<EnemyMobile>().enabled=false;if(e.GetComponent<EnemyTurret>())e.GetComponent<EnemyTurret>().enabled=false;e.enabled=false;if(e.NavMeshAgent&&e.NavMeshAgent.isOnNavMesh)e.NavMeshAgent.ResetPath();}
            player=Object.FindAnyObjectByType<PlayerCharacterController>();weapons=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();hands=player.GetComponent<ProtagonistHandsPresentation>();melee=player.GetComponent<ProtagonistMelee>();legs=player.GetComponent<ProtagonistSlidePresentation>();sparks=player.GetComponent<SlideContactEffects>();player.GetComponent<Health>().Invincible=true;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="ActionVerificationFloor";floor.transform.position=new Vector3(0,9.5f,0);floor.transform.localScale=new Vector3(120,1,120);Teleport(new Vector3(0,10.05f,-20));
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(1.4f);yield return new WaitForEndOfFrame();Assert.That(melee,Is.Not.Null);Assert.That(hands.LeftHandVisible,Is.False);
        }
        void Teleport(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
        [UnityTearDown]public IEnumerator Cleanup()
        {
            Time.timeScale=1;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("ActionCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(old);InputSystem.actions.devices=previousDevices;if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;yield return null;
        }
        void Keys(params Key[] value){if(Time.timeScale>0)Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(keys,new KeyboardState(value));}
        IEnumerator Tap(params Key[] value){Keys(value);yield return new WaitForSecondsRealtime(.045f);Keys();yield return null;yield return null;}
        void Mouse(bool fire=false,bool aim=false,Vector2 delta=default){if(Time.timeScale>0)Cursor.lockState=CursorLockMode.Locked;var value=new MouseState{delta=delta};if(fire)value=value.WithButton(MouseButton.Left);if(aim)value=value.WithButton(MouseButton.Right);InputSystem.QueueStateEvent(mouse,value);}
        void Capture(string name,Camera source=null)
        {
            Directory.CreateDirectory(Reports);var camera=source?source:player.PlayerCamera;var rt=new RenderTexture(1280,720,24);var prior=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Reports+"/"+name+".png",image.EncodeToPNG());camera.targetTexture=prior;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(image);
        }
        Transform Bone(string name)=>hands.FpsRoot.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        void Mounted()
        {
            var w=weapons.GetActiveWeapon();var attachment=w.WeaponRoot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Attachment");
            Assert.That(Vector3.Distance(hands.RightModuleSocket.position,attachment.position+w.WeaponMuzzle.forward*.003f),Is.LessThan(.0001f));
            Assert.That(Vector3.Dot((Bone("Hand_R").position-Bone("Forearm_R").position).normalized,w.WeaponMuzzle.forward),Is.GreaterThan(.9999f));Assert.That(hands.RightHandVisible,Is.False);Assert.That(hands.LeftHandVisible,Is.False);
        }
        Health Target(Vector3 position)
        {
            var root=new GameObject("MeleeTarget");root.transform.position=position;var health=root.AddComponent<Health>();health.MaxHealth=100;
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="TrainingTargetVisual";visual.transform.SetParent(root.transform,false);visual.transform.localScale=new Vector3(.4f,.45f,.25f);Object.Destroy(visual.GetComponent<Collider>());var renderer=visual.GetComponent<Renderer>();health.OnDamaged+=(value,owner)=>{var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(.8f,.18f,.06f));renderer.SetPropertyBlock(block);};
            for(int i=0;i<3;i++){var child=new GameObject("Hitbox"+i);child.transform.SetParent(root.transform,false);child.transform.localPosition=new Vector3((i-1)*.05f,0,0);child.AddComponent<BoxCollider>().size=new Vector3(.3f,.45f,.25f);child.AddComponent<Damageable>();}Physics.SyncTransforms();return health;
        }
        StolenCannon Module()
        {
            Object.FindObjectsByType<DetachableCannon>().First().Detach();var module=Object.FindObjectsByType<StolenCannon>().Single(x=>x.State==StolenCannonState.Available);Place(module);return module;
        }
        void Place(StolenCannon module){module.Body.isKinematic=true;Physics.SyncTransforms();var target=player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*1.3f;module.transform.position+=target-module.SolidCollider.bounds.center;Physics.SyncTransforms();}
        IEnumerator Pickup(StolenCannon module){yield return new WaitForSeconds(.4f);Assert.That(carrier.Candidate,Is.SameAs(module));int serial=melee.AttackSerial;yield return Tap(Key.E);yield return new WaitForSeconds(2.1f);yield return new WaitForEndOfFrame();Assert.That(carrier.Held,Is.SameAs(module));Assert.That(melee.AttackSerial,Is.EqualTo(serial));Assert.That(hands.LeftHandVisible,Is.False);}
        [UnityTest]public IEnumerator MountStaysCoaxialDuringMovementAdsRecoilAndFire()
        {
            Mounted();Capture("01-mounted-idle");Keys(Key.W);yield return new WaitForSeconds(.2f);Mouse(false,true,new Vector2(5,0));yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();Mounted();Capture("02-mounted-ads");int shots=0;var w=weapons.GetActiveWeapon();w.OnShootProcessed+=()=>shots++;Mouse(true,true);yield return null;yield return null;Mouse(false,true);yield return new WaitForEndOfFrame();Mounted();Assert.That(shots,Is.EqualTo(1));Capture("03-mounted-recoil");Mouse();Keys();
        }
        [UnityTest]public IEnumerator FArticulatesFistHitsOneHealthOnceAndRecovers()
        {
            player.GetComponent<Health>().Invincible=false;var target=Target(player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*.78f);yield return null;int damageEvents=0;target.OnDamaged+=(v,o)=>damageEvents++;var before=Bone("Index2_L").localRotation;float self=player.GetComponent<Health>().CurrentHealth;
            Keys(Key.F);yield return new WaitForSeconds(.045f);yield return new WaitForEndOfFrame();Assert.That(hands.LeftHandVisible,Is.True);Capture("04-melee-preparation");Keys();
            yield return new WaitForSeconds(.09f);yield return new WaitForEndOfFrame();Assert.That(Quaternion.Angle(before,Bone("Index2_L").localRotation),Is.GreaterThan(12));Capture("05-melee-extension");
            yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();Capture("06-melee-impact");yield return new WaitForSeconds(.10f);yield return new WaitForEndOfFrame();Capture("07-melee-recovery");yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();
            Assert.That(target.CurrentHealth,Is.EqualTo(100-melee.Damage).Within(.001));Assert.That(damageEvents,Is.EqualTo(1));Assert.That(melee.LastHitCount,Is.EqualTo(1));Assert.That(hands.LeftHandVisible,Is.False);Assert.That(player.GetComponent<Health>().CurrentHealth,Is.EqualTo(self));Capture("08-melee-hidden");
        }
        [UnityTest]public IEnumerator MeleeRespectsWallRangeHoldAndRecovery()
        {
            var camera=player.PlayerCamera.transform;var target=Target(camera.position+camera.forward*1.05f);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=camera.position+camera.forward*.5f;wall.transform.localScale=new Vector3(2,2,.12f);Physics.SyncTransforms();yield return null;
            yield return Tap(Key.F);yield return new WaitForSeconds(.65f);Assert.That(target.CurrentHealth,Is.EqualTo(100));Object.Destroy(wall);target.transform.position=camera.position+camera.forward*3;Physics.SyncTransforms();yield return Tap(Key.F);yield return new WaitForSeconds(.65f);Assert.That(target.CurrentHealth,Is.EqualTo(100));
            target.transform.position=camera.position+camera.forward;Physics.SyncTransforms();Keys(Key.F);yield return new WaitForSeconds(1.2f);Keys();yield return null;Assert.That(target.CurrentHealth,Is.EqualTo(100-melee.Damage));Assert.That(melee.IsAttacking,Is.False);
        }
        [UnityTest]public IEnumerator ECollectsFOnlyPunchesAndSimultaneousKeysPreferPickup()
        {
            Assert.That(InputSystem.actions.FindAction("Player/CollectWeapon").bindings.Any(b=>b.effectivePath=="<Keyboard>/e"),Is.True);Assert.That(InputSystem.actions.FindAction("Player/WeaponPrevious").bindings.Any(b=>b.effectivePath=="<Keyboard>/e"),Is.False);
            var module=Module();yield return new WaitForSeconds(.4f);yield return Tap(Key.F);yield return new WaitForSeconds(.65f);Assert.That(carrier.Held,Is.Null);Assert.That(melee.AttackSerial,Is.EqualTo(1));
            yield return Pickup(module);yield return Tap(Key.G);yield return new WaitForSeconds(1.4f);Place(module);yield return new WaitForSeconds(.4f);int count=melee.AttackSerial;yield return Tap(Key.E,Key.F);yield return new WaitForSeconds(2.1f);Assert.That(carrier.Held,Is.SameAs(module));Assert.That(melee.AttackSerial,Is.EqualTo(count));
        }
        [UnityTest]public IEnumerator SlideShowsOnlyLegsAndContactSparksThenClearsOnJump()
        {
            Assert.That(legs.IsVisible,Is.False);Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.13f);yield return new WaitForEndOfFrame();
            Assert.That(player.GetComponent<AdvancedPlayerMovement>().IsSliding,Is.True);Assert.That(legs.IsVisible,Is.True);
            var leftThigh=legs.Visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Thigh_L");var leftShin=legs.Visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Shin_L");var leftFoot=legs.Visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Foot_L");
            Assert.That(Vector3.Angle(leftShin.position-leftThigh.position,leftFoot.position-leftShin.position),Is.GreaterThan(40),"Asymmetric bent left knee");
            Assert.That(sparks.ContactParticles.isEmitting,Is.True);Assert.That(sparks.SecondContact.isEmitting,Is.True);Assert.That(hands.LeftHandVisible,Is.False);Capture("09-slide-entry");
            Mouse(false,false,new Vector2(0,-140));yield return new WaitForSeconds(.05f);yield return new WaitForEndOfFrame();Capture("10-slide-legs-sparks");
            var contactCamera=new GameObject("TemporaryContactCapture").AddComponent<Camera>();contactCamera.cullingMask=player.PlayerCamera.cullingMask;contactCamera.nearClipPlane=.02f;contactCamera.farClipPlane=30;contactCamera.fieldOfView=60;
            var focus=sparks.SecondContact.transform.position-player.transform.forward*.35f;contactCamera.transform.position=focus+player.transform.right*.9f+Vector3.up*.4f-player.transform.forward*.5f;contactCamera.transform.LookAt(focus);Capture("14-ground-contact-sparks",contactCamera);Object.Destroy(contactCamera.gameObject);
            Assert.That(legs.Visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single().name,Is.EqualTo("SlideLegs_LOD0"));Assert.That(legs.Visual.GetComponentsInChildren<Light>(true).Length,Is.Zero);
            Keys(Key.W,Key.Space);yield return null;yield return null;yield return new WaitForEndOfFrame();Assert.That(player.IsGrounded,Is.False);Assert.That(legs.IsVisible,Is.False);Assert.That(sparks.ContactParticles.particleCount,Is.Zero);Assert.That(sparks.SecondContact.isEmitting,Is.False);Capture("11-slide-jump-clean");Keys();
        }
        Transform LegBone(string name)=>legs.Visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        [UnityTest]public IEnumerator DownwardLookKeepsLegPoseAnchoredAndVisible()
        {
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.11f);yield return new WaitForEndOfFrame();Assert.That(legs.IsVisible,Is.True);
            var foot=LegBone("Foot_R");var pelvis=LegBone("Pelvis");var footOffset=foot.position-player.transform.position;var pelvisOffset=pelvis.position-player.transform.position;
            Mouse(false,false,new Vector2(0,-200));yield return null;yield return null;yield return new WaitForEndOfFrame();
            Assert.That(Vector3.Distance(footOffset,foot.position-player.transform.position),Is.LessThan(.005f),"Pitch must not pull the feet towards the camera");Capture("15-slide-look-down-moderate");
            Mouse(false,false,new Vector2(0,-1000));yield return null;yield return null;yield return new WaitForEndOfFrame();
            // Design change 2026-11-04: the slide limits downward pitch (AdvancedPlayerMovement.slideMaxPitch)
            // instead of allowing a vertical 89-degree view. Previously this asserted a straight-down camera.
            var motion=player.GetComponent<AdvancedPlayerMovement>();
            Assert.That(motion.IsSliding,Is.True);Assert.That(legs.IsVisible,Is.True,"Legs remain active while pitch is limited");
            float pitch=Vector3.Angle(Vector3.ProjectOnPlane(player.PlayerCamera.transform.forward,Vector3.up),player.PlayerCamera.transform.forward);
            Assert.That(pitch,Is.LessThanOrEqualTo(motion.SlideMaxPitch+.5f),"Slide must not allow looking fully down");
            Assert.That(Vector3.Dot(player.PlayerCamera.transform.forward,Vector3.down),Is.LessThan(.9f));
            Assert.That(Vector3.Distance(footOffset,foot.position-player.transform.position),Is.LessThan(.005f));
            Assert.That(Vector3.Distance(pelvisOffset,pelvis.position-player.transform.position),Is.LessThan(.005f));
            Capture("16-slide-look-down-extreme");Keys();
            yield return new WaitForSeconds(2f);Assert.That(motion.IsSliding,Is.False);
            Assert.That(motion.CurrentPitchLimit,Is.EqualTo(89f),"Limit is released after the slide");
        }
        [UnityTest]public IEnumerator SlideDirectionStaysFixedAndTorsoCannotTurnBehind()
        {
            Mouse(false,false,new Vector2(-50,0));yield return null;yield return null;
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.11f);yield return new WaitForEndOfFrame();
            var move=player.GetComponent<AdvancedPlayerMovement>();var direction=move.SlideDirection;var rightLeg=(LegBone("Shin_R").position-LegBone("Thigh_R").position).normalized;var forward=Vector3.ProjectOnPlane(rightLeg,Vector3.up).normalized;
            Assert.That(Vector3.Dot(forward,direction),Is.GreaterThan(.999f));
            Mouse(false,false,new Vector2(1000,0));yield return null;yield return null;yield return new WaitForEndOfFrame();
            Assert.That(move.IsSliding,Is.True);Assert.That(Vector3.SignedAngle(direction,player.transform.forward,Vector3.up),Is.EqualTo(move.SlideLookAngle).Within(.1f));
            Assert.That(Vector3.Dot(legs.FacingDirection,direction),Is.GreaterThan(.9999f));
            Assert.That(Vector3.Angle(rightLeg,(LegBone("Shin_R").position-LegBone("Thigh_R").position).normalized),Is.LessThan(.01f),"Camera yaw must not rotate the legs");Capture("17-slide-look-right-limit");
            Mouse(false,false,new Vector2(-2000,0));yield return null;yield return null;yield return new WaitForEndOfFrame();
            Assert.That(Vector3.SignedAngle(direction,player.transform.forward,Vector3.up),Is.EqualTo(-move.SlideLookAngle).Within(.1f));
            Assert.That(Vector3.Dot(move.SlideDirection,direction),Is.GreaterThan(.9999f));Assert.That(Vector3.Dot(Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up).normalized,direction),Is.GreaterThan(.999f),"Look constraint does not steer slide movement");Capture("18-slide-look-left-limit");
            Mouse();yield return Tap(Key.C);Assert.That(move.IsSliding,Is.False);Mouse(false,false,new Vector2(-200,0));yield return null;yield return null;
            Assert.That(Vector3.SignedAngle(direction,player.transform.forward,Vector3.up),Is.LessThan(-move.SlideLookAngle-20),"Free yaw returns as soon as slide ends");Keys();
        }
        [UnityTest]public IEnumerator SlopeContactsFollowSurfaceNormalAndSlideEndsCleanly()
        {
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.name="MetalContactSlope";ramp.transform.SetPositionAndRotation(new Vector3(0,13,-15),Quaternion.Euler(-10,0,0));ramp.transform.localScale=new Vector3(20,.4f,25);Physics.SyncTransforms();Physics.Raycast(new Vector3(0,19,-20),Vector3.down,out var hit,15);Teleport(hit.point+Vector3.up*.06f);yield return new WaitForSeconds(.3f);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();Assert.That(legs.IsVisible,Is.True);Assert.That(sparks.ContactParticles.isEmitting,Is.True);
            Assert.That(Vector3.Dot(sparks.ContactParticles.transform.up,ramp.transform.up),Is.GreaterThan(.94f));Capture("12-slide-slope");yield return new WaitForSeconds(.9f);yield return new WaitForEndOfFrame();Assert.That(legs.IsVisible,Is.False);Assert.That(sparks.ContactParticles.isEmitting,Is.False);Assert.That(sparks.SecondContact.particleCount,Is.Zero);Keys();
        }
        [UnityTest]public IEnumerator PauseDeathAndWeaponChangesLeaveNoPresentationResidue()
        {
            yield return Tap(Key.F);yield return Tap(Key.Escape);Assert.That(Time.timeScale,Is.Zero);yield return null;yield return new WaitForEndOfFrame();Assert.That(melee.IsAttacking,Is.False,"Melee cancelled on pause");Assert.That(hands.LeftHandVisible,Is.False,"Left arm hidden on pause");Assert.That(legs.IsVisible,Is.False,"Legs hidden on pause");Assert.That(sparks.ContactParticles.particleCount,Is.Zero);yield return Tap(Key.Escape);
            var module=Module();yield return Pickup(module);yield return Tap(Key.F);player.GetComponent<Health>().Invincible=false;player.GetComponent<Health>().TakeDamage(player.GetComponent<Health>().MaxHealth+1,null);yield return null;yield return new WaitForEndOfFrame();Assert.That(hands.FpsRoot.gameObject.activeInHierarchy,Is.False,"FPS root hidden on death");Assert.That(melee.IsAttacking,Is.False,"Melee cancelled on death");Assert.That(legs.IsVisible,Is.False,"Legs hidden on death");Assert.That(carrier.Held,Is.Null);Assert.That(module,Is.Not.Null);
        }
        [UnityTest]public IEnumerator StolenCannonChargeSwitchThrowAndExhaustionRemainFunctional()
        {
            var module=Module();yield return Pickup(module);var weapon=module.EquippedWeapon;var fx=weapon.GetComponent<CannonChargePresentation>();float before=module.Ammo.Remaining;Mouse(true);yield return new WaitForSeconds(1.35f);yield return new WaitForEndOfFrame();Assert.That(Vector3.Distance(fx.EffectOrigin.position,weapon.WeaponMuzzle.position),Is.LessThan(.0001f));Assert.That(hands.LeftHandVisible,Is.False);Capture("13-cannon-charge-hidden-left");Mouse();yield return null;yield return null;Assert.That(module.Ammo.Remaining,Is.LessThan(before));
            yield return Tap(Key.Digit1);yield return new WaitForSeconds(2.1f);yield return new WaitForEndOfFrame();Mounted();yield return Tap(Key.Digit2);yield return new WaitForSeconds(2.1f);yield return Tap(Key.G);yield return new WaitForSeconds(1.4f);Place(module);yield return Pickup(module);int blasts=0;module.Explosion.Detonated+=()=>blasts++;module.Ammo.TryConsume(module.Ammo.Remaining);yield return new WaitForSeconds(2.2f);yield return new WaitForEndOfFrame();Assert.That(blasts,Is.EqualTo(1));Mounted();Assert.That(sparks.ContactParticles.isEmitting,Is.False);
        }
    }
}
