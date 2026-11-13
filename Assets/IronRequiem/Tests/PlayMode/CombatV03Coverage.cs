using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using Object=UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class CombatV03Coverage
    {
        const string Output="Tools/IronRequiem/Reports/CombatV03";
        PlayerCharacterController player;
        StolenWeaponCarrier carrier;
        PlayerWeaponsManager inventory;
        EnemyController bot;
        StolenCannon module;
        Keyboard keys;
        Mouse pointer;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        bool devicesChanged;
        IEnumerator SetupLab()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CombatPerformanceLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            Application.runInBackground=true;Time.timeScale=1;yield return null;yield return null;
            Object.FindAnyObjectByType<GameFlowManager>().enabled=false;
            Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;
            player=Object.FindAnyObjectByType<PlayerCharacterController>();inventory=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();
            player.GetComponent<Health>().Invincible=true;player.enabled=false;
            bot=Object.FindAnyObjectByType<RobotDamageProgression>().GetComponent<EnemyController>();bot.GetComponent<EnemyMobile>().enabled=false;
            if(bot.NavMeshAgent.isOnNavMesh){bot.NavMeshAgent.ResetPath();bot.NavMeshAgent.isStopped=true;}
            module=bot.GetComponentInChildren<StolenCannon>(true);
            previousDevices=InputSystem.actions.devices;devicesChanged=true;keys=InputSystem.AddDevice<Keyboard>();pointer=InputSystem.AddDevice<Mouse>();
            keys.MakeCurrent();pointer.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,pointer};Cursor.lockState=CursorLockMode.Locked;
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            yield return new WaitForSeconds(.1f);
        }
        void Aim(StolenCannon target,Vector3 offset)
        {
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;
            player.transform.position=new Vector3(target.transform.position.x, .05f,target.transform.position.z)+offset;cc.enabled=true;
            player.PlayerCamera.transform.LookAt(target.SolidCollider.bounds.center);Physics.SyncTransforms();Cursor.lockState=CursorLockMode.Locked;
        }
        IEnumerator Tap(Key key)
        {
            Directory.CreateDirectory(Output);File.AppendAllText(Output+"/pickup-diagnostic.txt",Diagnostic()+"\n");
            InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
        }
        string Diagnostic()
        {
            var camera=player.PlayerCamera.transform;
            var hits=Physics.RaycastAll(camera.position,camera.forward,6,-1,QueryTriggerInteraction.Ignore);
            return "canAct="+player.GetComponent<MechanicInput>().CanAct+" cursor="+Cursor.lockState+" ending="+Object.FindAnyObjectByType<GameFlowManager>().GameIsEnding+
                " candidate="+(carrier.Candidate?carrier.Candidate.name:"NULL")+" status="+carrier.PickupStatus+" direct="+carrier.GetPickupStatus(module)+
                " camera="+camera.position+" forward="+camera.forward+" solid="+module.SolidCollider.bounds+" hits="+string.Join(";",hits.Select(h=>h.collider.name+":"+h.distance));
        }
        IEnumerator DropAndGround()
        {
            bot.GetComponentInChildren<DetachableCannon>().Detach();yield return new WaitForSeconds(1);
            Assert.That(module.SolidCollider.bounds.size.magnitude,Is.LessThan(2),"The imported scale must not inflate the pickup collider.");
            module.Body.isKinematic=true;module.transform.position=new Vector3(0,.3f,-2);Physics.SyncTransforms();yield return null;
        }
        [UnityTearDown] public IEnumerator ResetCoverage()
        {
            if(devicesChanged)InputSystem.actions.devices=previousDevices;
            if(keys!=null)InputSystem.RemoveDevice(keys);if(pointer!=null)InputSystem.RemoveDevice(pointer);
            keys=null;pointer=null;devicesChanged=false;Cursor.lockState=CursorLockMode.None;Time.timeScale=1;Time.captureDeltaTime=0;
            var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("CombatCoverageCleanup"+Time.frameCount));
            if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);yield return null;
        }
        [UnityTest] public IEnumerator GroundAnglesRepeatedInputSwitchAndThrowKeepEnergy()
        {
            yield return SetupLab();yield return DropAndGround();float energy=module.Ammo.Remaining;
            for(int i=0;i<4;i++)
            {
                var angle=Quaternion.Euler(0,i*90,0)*Vector3.back*1.2f;Aim(module,angle);yield return null;yield return Tap(Key.E);
                Assert.That(carrier.Held,Is.SameAs(module),"Ground angle "+i);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy).Within(.001));
                Assert.That(carrier.TryPickup(module),Is.False);yield return Tap(Key.E);Assert.That(carrier.Held,Is.SameAs(module));
                yield return new WaitForSeconds(.4f);
                int slot=inventory.ActiveWeaponIndex;inventory.SwitchToWeaponIndex(0,true);yield return new WaitForSeconds(.4f);
                Assert.That(module.State,Is.EqualTo(StolenCannonState.Equipped));Assert.That(module.Ammo.Remaining,Is.EqualTo(energy).Within(.001));
                inventory.SwitchToWeaponIndex(slot,true);yield return new WaitForSeconds(.4f);
                module.Ammo.Drain(3);energy-=3;yield return Tap(Key.G);
                Assert.That(module.State,Is.EqualTo(StolenCannonState.Available));module.Body.isKinematic=true;module.transform.position=new Vector3(0,.3f,-2);
                yield return new WaitForSeconds(.4f);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy).Within(.001));
            }
        }
        [UnityTest] public IEnumerator RangeCooldownAndSolidObstaclesGiveCorrectPickupState()
        {
            yield return SetupLab();bot.GetComponentInChildren<DetachableCannon>().Detach();module.Body.isKinematic=true;module.transform.position=new Vector3(0,.3f,-2);
            Aim(module,Vector3.back*1.2f);yield return null;Assert.That(carrier.GetPickupStatus(module),Is.EqualTo(CannonPickupStatus.CoolingDown));
            yield return new WaitForSeconds(.4f);Aim(module,Vector3.back*3);yield return null;
            Assert.That(carrier.PickupStatus,Is.EqualTo(CannonPickupStatus.TooFar));yield return Tap(Key.E);Assert.That(carrier.Held,Is.Null);
            Aim(module,Vector3.back*1.2f);yield return null;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Vector3.Lerp(player.PlayerCamera.transform.position,module.SolidCollider.bounds.center,.5f);wall.transform.localScale=Vector3.one*.35f;
            Physics.SyncTransforms();yield return null;Assert.That(carrier.Candidate,Is.Null);yield return Tap(Key.E);Assert.That(carrier.Held,Is.Null);
            Object.Destroy(wall);yield return null;yield return Tap(Key.E);Assert.That(carrier.Held,Is.SameAs(module));
        }
        [UnityTest] public IEnumerator FullInventoryExplainsFailureAndFreeSlotEnablesPickup()
        {
            yield return SetupLab();yield return DropAndGround();Aim(module,Vector3.back*1.2f);
            var source=inventory.GetActiveWeapon();
            for(int i=0;i<9;i++)
            {
                if(inventory.GetWeaponAtSlotIndex(i))continue;
                var clone=Object.Instantiate(source);clone.SourcePrefab=null;clone.ShowWeapon(false);clone.name="UniqueTestWeapon"+i;
                Assert.That(inventory.AddWeapon(clone),Is.True);
            }
            yield return null;Assert.That(carrier.PickupStatus,Is.EqualTo(CannonPickupStatus.InventoryFull));yield return Tap(Key.E);Assert.That(carrier.Held,Is.Null);
            var hud=Object.FindAnyObjectByType<MechanicHud>();Assert.That(hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("LLENO")),Is.True);
            inventory.RemoveWeapon(inventory.GetWeaponAtSlotIndex(8));yield return null;yield return Tap(Key.E);Assert.That(carrier.Held,Is.SameAs(module));
        }
        [UnityTest] public IEnumerator NearestOfTwoCannonsIsTheOnlyOneEquipped()
        {
            yield return SetupLab();yield return DropAndGround();
#if UNITY_EDITOR
            var second=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/T07_Common_Destructible.prefab"),new Vector3(5,.05f,5),Quaternion.identity);
#endif
            yield return null;yield return null;second.GetComponent<EnemyMobile>().enabled=false;
            second.GetComponentInChildren<DetachableCannon>().Detach();var other=second.GetComponentInChildren<StolenCannon>();
            // The detached module is now independent of the owner hierarchy.
            other=Object.FindObjectsByType<StolenCannon>(FindObjectsSortMode.None).Single(m=>m!=module);
            other.Body.isKinematic=true;other.transform.position=module.transform.position+Vector3.forward*.7f;yield return new WaitForSeconds(.4f);
            Aim(module,Vector3.back*1.2f);yield return null;yield return Tap(Key.E);
            Assert.That(carrier.Held,Is.SameAs(module));Assert.That(other.State,Is.EqualTo(StolenCannonState.Available));Assert.That(other.CanPickup,Is.True);
        }
        [UnityTest] public IEnumerator ChargedShotCriticalSmokeThrowAndExplosionStillWork()
        {
            yield return SetupLab();yield return DropAndGround();Aim(module,Vector3.back*1.2f);yield return null;yield return Tap(Key.E);yield return new WaitForSeconds(.4f);
            Assert.That(carrier.Held,Is.SameAs(module));var shot=module.EquippedWeapon.GetComponent<CannonChargeShot>();float initial=module.Ammo.Remaining;
            InputSystem.QueueStateEvent(pointer,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(shot.MaximumChargeDuration+.15f);
            InputSystem.QueueStateEvent(pointer,new MouseState());yield return null;yield return null;
            Assert.That(shot.LastShotPower,Is.GreaterThan(.99f));Assert.That(module.Ammo.Remaining,Is.EqualTo(initial-shot.MaximumEnergyCost).Within(.01));
            InputSystem.QueueStateEvent(pointer,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.1f);
            inventory.SwitchToWeaponIndex(0,true);yield return new WaitForSeconds(.4f);Assert.That(shot.IsCharging,Is.False);
            InputSystem.QueueStateEvent(pointer,new MouseState());inventory.SwitchToWeaponIndex(1,true);yield return new WaitForSeconds(.4f);
            module.Ammo.Drain(module.Ammo.Remaining-15);yield return null;
            Assert.That(module.Overdrive.IsArmed,Is.True);Assert.That(module.Overdrive.Smoke.isPlaying,Is.True);
            yield return Tap(Key.G);Assert.That(module.State,Is.EqualTo(StolenCannonState.Thrown));
            int detonations=0;module.Explosion.Detonated+=()=>detonations++;Assert.That(module.Explosion.TryDetonate(),Is.True);Assert.That(module.Explosion.TryDetonate(),Is.False);Assert.That(detonations,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator EquippedExhaustionExplodesOnceAndRemovesInventoryEntry()
        {
            yield return SetupLab();yield return DropAndGround();Aim(module,Vector3.back*1.2f);yield return null;yield return Tap(Key.E);yield return new WaitForSeconds(.4f);
            Assert.That(carrier.Held,Is.SameAs(module));var weapon=module.EquippedWeapon;int detonations=0;module.Explosion.Detonated+=()=>detonations++;
            module.Ammo.Drain(module.Ammo.Remaining);Assert.That(detonations,Is.EqualTo(1));Assert.That(carrier.Held,Is.Null);
            yield return null;Assert.That(weapon==null,Is.True);
        }
        [UnityTest] public IEnumerator CriticalRightArmFallsOnceAcrossEveryLodWithoutLosingCannon()
        {
            yield return SetupLab();var damage=bot.GetComponent<RobotDamageProgression>();Assert.That(damage.CriticalArm,Is.Not.Null);
            var arm=damage.CriticalArm;var originalBounds=arm.Source.bounds;var hp=bot.GetComponent<Health>();hp.TakeDamage(hp.MaxHealth*.76f,null);
            Assert.That(damage.Stage,Is.EqualTo(3));Assert.That(arm.IsDetached,Is.True);Assert.That(arm.gameObject.activeSelf,Is.False);
            Assert.That(RobotDestructionPool.ArmsInUse,Is.EqualTo(1));Assert.That(module.State,Is.EqualTo(StolenCannonState.Attached));
            var fragment=Object.FindObjectsByType<RobotArmorDebris>(FindObjectsSortMode.None).Single(d=>d.InUse && d.name.StartsWith("RightArm"));
            Assert.That(Vector3.Distance(fragment.GetComponent<Renderer>().bounds.center,originalBounds.center),Is.LessThan(.3f));
            var group=bot.GetComponentsInChildren<LODGroup>().Single(g=>g.name=="Body");
            for(int lod=0;lod<3;lod++){group.ForceLOD(lod);yield return null;Assert.That(arm.gameObject.activeInHierarchy,Is.False);}
            hp.TakeDamage(1,null);Assert.That(RobotDestructionPool.ArmsInUse,Is.EqualTo(1));Assert.That(bot.TryAtack(player.transform.position),Is.True);
            hp.Kill();yield return new WaitForSeconds(3.5f);Assert.That(RobotDestructionPool.ArmsInUse,Is.Zero);Assert.That(module && module.CanPickup,Is.True);
        }
        [UnityTest] public IEnumerator DetectionNonAllocMatchesOriginalWhenBufferIsFull()
        {
            yield return SetupLab();var detector=bot.DetectionModule;var actor=bot.GetComponent<Actor>();var colliders=bot.GetComponentsInChildren<Collider>();
            var target=player.GetComponent<Actor>().AimPoint.position;var origin=detector.DetectionSourcePoint.position;
            for(int i=0;i<40;i++)
            {var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=Vector3.Lerp(origin,target,.1f+i*.015f);blocker.transform.localScale=Vector3.one*.025f;}
            Physics.SyncTransforms();yield return null;
            Assert.That(Physics.RaycastAll(origin,(target-origin).normalized,detector.DetectionRange,-1,QueryTriggerInteraction.Ignore).Length,Is.GreaterThan(16));
            detector.UseNonAllocQueries=false;detector.HandleTargetDetection(actor,colliders);bool old=detector.IsSeeingTarget;
            detector.UseNonAllocQueries=true;detector.HandleTargetDetection(actor,colliders);Assert.That(detector.IsSeeingTarget,Is.EqualTo(old));Assert.That(old,Is.False);
            detector.HandleTargetDetection(actor,colliders);long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<32;i++)detector.HandleTargetDetection(actor,colliders);
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-start,Is.Zero,"Warmed detection should allocate no managed query arrays.");
        }
        [UnityTest] public IEnumerator ZZ_CaptureClarityAtIdenticalCameraAndTurn()
        {
            yield return SetupLab();yield return new WaitForSeconds(.7f);inventory.enabled=false;bot.enabled=false;bot.GetComponent<T07AnimationDriver>().Animator.enabled=false;
            var camera=player.PlayerCamera;var data=camera.GetUniversalAdditionalCameraData();
            player.transform.position=new Vector3(0,.05f,-5);camera.transform.localRotation=Quaternion.identity;
            var volume=Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).First(v=>v.sharedProfile && v.sharedProfile.Has<MotionBlur>());
#if UNITY_EDITOR
            var profiles=new[]{AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Rendering/PostProcessingProfile.asset"),AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/IronRequiem/Art/Rendering/CombatClarity.asset")};
#endif
            Time.captureDeltaTime=1f/60;
            for(int pass=0;pass<2;pass++)
            {
                volume.sharedProfile=profiles[pass];data.antialiasing=pass==0?AntialiasingMode.FastApproximateAntialiasing:AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality=AntialiasingQuality.Medium;
                for(int frame=0;frame<45;frame++){camera.transform.rotation=Quaternion.Euler(0,Mathf.Clamp(frame-15,0,30)*.55f,0);yield return new WaitForEndOfFrame();}
                var texture=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(texture.width,Is.GreaterThan(500));
                File.WriteAllBytes(Output+(pass==0?"/clarity-before.png":"/clarity-after.png"),texture.EncodeToPNG());Object.Destroy(texture);
            }
            Time.captureDeltaTime=0;
            File.WriteAllText(Output+"/clarity.txt",Screen.width+"x"+Screen.height+"; identical camera, lighting, 60 Hz simulated turn; old MotionBlur .35/clamp .05 + FXAA; new .12/clamp .03 + SMAA Medium; renderScale unchanged at 1; no sharpening added.");
        }
    }
}
