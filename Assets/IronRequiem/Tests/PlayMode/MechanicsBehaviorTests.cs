using System.Collections;
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
    public sealed class MechanicsBehaviorTests
    {
        PlayerCharacterController player;
        PlayerWeaponsManager weapons;
        AdvancedPlayerMovement motion;
        StolenWeaponCarrier carrier;
        EnemyController enemy;
        Keyboard keyboard;
        Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;Application.runInBackground=true;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CharacterLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
            previousDevices=InputSystem.actions.devices;
            InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};
            yield return null;yield return null;
            player=Object.FindAnyObjectByType<PlayerCharacterController>();weapons=player.GetComponent<PlayerWeaponsManager>();
            motion=player.GetComponent<AdvancedPlayerMovement>();carrier=player.GetComponent<StolenWeaponCarrier>();enemy=Object.FindAnyObjectByType<EnemyController>();
            player.GetComponent<Health>().Invincible=true;
            enemy.GetComponent<EnemyMobile>().enabled=false;enemy.NavMeshAgent.ResetPath();
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState=CursorLockMode.Locked;
            int frame=Time.frameCount;yield return new WaitForSeconds(.5f);
            Assert.That(Time.frameCount,Is.GreaterThan(frame));Assert.That(player.GetComponent<MechanicInput>().CanAct,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            var old=SceneManager.GetActiveScene();var empty=SceneManager.CreateScene("MechanicsCleanup_"+Time.frameCount);SceneManager.SetActiveScene(empty);
            if(old.IsValid() && old.isLoaded)yield return SceneManager.UnloadSceneAsync(old);
            InputSystem.actions.devices=previousDevices;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            Cursor.lockState=CursorLockMode.None;yield return null;
        }
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        void Teleport(Vector3 position)
        {var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(position,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
        IEnumerator Tap(params Key[] keys){Keys(keys);yield return new WaitForSecondsRealtime(.04f);Keys();yield return null;}
        StolenCannon DetachAndPlace()
        {
            enemy.GetComponentInChildren<DetachableCannon>().Detach();
            var module=Object.FindAnyObjectByType<StolenCannon>();Assert.That(module,Is.Not.Null);
            PlaceForPickup(module);return module;
        }
        void PlaceForPickup(StolenCannon module)
        {
            module.Body.isKinematic=true;
            module.transform.position=player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*1.5f;
            Physics.SyncTransforms();
            module.transform.position+=player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*1.5f-module.SolidCollider.bounds.center;
            Physics.SyncTransforms();
        }
        IEnumerator Pickup(StolenCannon module)
        {
            yield return new WaitForSeconds(.4f);
            Assert.That(player.GetComponent<MechanicInput>().CanAct,Is.True,"Pickup input gate; cursor="+Cursor.lockState);
            Assert.That(carrier.Candidate,Is.EqualTo(module),"Visible in-range module must offer interaction; camera="+player.PlayerCamera.transform.eulerAngles+", module="+module.SolidCollider.bounds.center);
            Cursor.lockState=CursorLockMode.Locked;
            yield return Tap(Key.E);Assert.That(carrier.Held,Is.EqualTo(module));
            yield return new WaitForSeconds(2.1f);Assert.That(carrier.CanUseHeldWeapon,Is.True);
        }
        IEnumerator ChargeAndRelease(float seconds)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;
        }
        Health Target(Vector3 position)
        {
            var go=new GameObject("RadialDamageTarget");go.transform.position=position;var health=go.AddComponent<Health>();health.MaxHealth=1000;
            for(int i=0;i<3;i++)
            {
                var part=new GameObject("Hitbox_"+i);part.transform.SetParent(go.transform,false);part.transform.localPosition=Vector3.right*(i-1)*.15f;
                part.AddComponent<BoxCollider>().size=new Vector3(1.6f,2.5f,1);part.AddComponent<Damageable>();
            }
            Physics.SyncTransforms();return health;
        }

        [UnityTest] public IEnumerator AirDashConsumesOneChargeFallsAndRechargesOnlyOnLanding()
        {
            Teleport(new Vector3(0,3,-10));yield return null;yield return null;
            Assert.That(player.IsGrounded,Is.False);var start=player.transform.position;
            yield return Tap(Key.Space);
            Assert.That(motion.IsDashing,Is.True);Assert.That(motion.DashAvailable,Is.False);
            yield return new WaitForSeconds(.22f);
            Assert.That(motion.IsDashing,Is.False);Assert.That(player.transform.position.z-start.z,Is.GreaterThan(2));
            Assert.That(player.CharacterVelocity.y,Is.LessThan(0));
            yield return Tap(Key.Space);Assert.That(motion.IsDashing,Is.False);Assert.That(motion.DashAvailable,Is.False);
            yield return new WaitForSeconds(1);Assert.That(player.IsGrounded,Is.True);Assert.That(motion.DashAvailable,Is.True);
        }

        [UnityTest] public IEnumerator AirDashUsesMovementDirectionAndCannotCrossWall()
        {
            Teleport(new Vector3(0,3,-10));yield return null;yield return null;
            Keys(Key.D,Key.Space);yield return new WaitForSeconds(.09f);Keys();
            Assert.That(player.transform.position.x,Is.GreaterThan(1));Assert.That(Mathf.Abs(player.transform.position.z+10),Is.LessThan(.5f));
            yield return new WaitForSeconds(1);Assert.That(motion.DashAvailable,Is.True);
            var wall=new GameObject("DashWall");wall.transform.position=new Vector3(0,3,-8);wall.AddComponent<BoxCollider>().size=new Vector3(10,8,.4f);
            Teleport(new Vector3(0,3,-10));yield return null;yield return null;yield return Tap(Key.Space);
            yield return new WaitForSeconds(.25f);
            Assert.That(player.transform.position.z,Is.LessThan(-8.4f));Assert.That(motion.IsDashing,Is.False);Assert.That(motion.DashAvailable,Is.False);
        }

        [UnityTest] public IEnumerator SlideRequiresSprintRespectsCeilingAndJumpCanCancelIntoAirDash()
        {
            Teleport(new Vector3(0,.05f,-12));yield return new WaitForSeconds(.2f);
            yield return Tap(Key.C);Assert.That(player.IsCrouching,Is.True);Assert.That(motion.IsSliding,Is.False);
            yield return Tap(Key.C);Assert.That(player.IsCrouching,Is.False);
            var roof=new GameObject("SlideLowCeiling");roof.transform.position=new Vector3(0,1.65f,-1.5f);roof.AddComponent<BoxCollider>().size=new Vector3(4,.3f,10);
            JsonUtility.FromJsonOverwrite("{\"slideDuration\":0.3}",motion);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.15f);
            Keys(Key.W,Key.LeftShift,Key.C);yield return null;Assert.That(motion.IsSliding,Is.True);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.33f);Keys();
            Assert.That(motion.IsSliding,Is.False);Assert.That(player.IsCrouching,Is.True);Assert.That(player.TrySetCrouching(false),Is.False);
            Teleport(new Vector3(0,.05f,-12));yield return new WaitForSeconds(.2f);yield return Tap(Key.C);yield return new WaitForSeconds(.25f);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.15f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;
            Assert.That(motion.IsSliding,Is.True);Keys(Key.W,Key.Space);yield return null;Keys();yield return null;
            Assert.That(motion.IsSliding,Is.False);Assert.That(player.IsGrounded,Is.False);
            yield return Tap(Key.Space);Assert.That(motion.IsDashing,Is.True);
        }

        [UnityTest] public IEnumerator SlideFollowsFifteenDegreeRampWithoutTunneling()
        {
            Teleport(new Vector3(7,.05f,-3));yield return new WaitForSeconds(.2f);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.1f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;
            Assert.That(motion.IsSliding,Is.True);Keys();float peak=player.transform.position.y;bool slidingOnRamp=false;
            float deadline=Time.time+.45f;
            while(Time.time<deadline){peak=Mathf.Max(peak,player.transform.position.y);if(player.transform.position.y>.3f && motion.IsSliding)slidingOnRamp=true;yield return null;}
            Assert.That(peak,Is.GreaterThan(.45f));Assert.That(player.transform.position.y,Is.GreaterThan(-.2f));
            Assert.That(slidingOnRamp,Is.True,"The slide must remain active on the ramp itself");
        }

        [UnityTest] public IEnumerator PickupIsUniqueAmmoSurvivesSwitchDropAndRepickAndEnemyStaysDisarmed()
        {
            var previous=weapons.GetActiveWeapon();
            var module=DetachAndPlace();yield return Pickup(module);
            Assert.That(module.State,Is.EqualTo(StolenCannonState.Equipped));Assert.That(module.Body.isKinematic,Is.True);Assert.That(module.SolidCollider.enabled,Is.False);
            Assert.That(Object.FindObjectsByType<Unity.FPS.UI.AmmoCounter>().Length,Is.EqualTo(2),"The pistol and stolen cannon need ammo counters");
            Assert.That(carrier.TryPickup(module),Is.False);enemy.GetComponentInChildren<DetachableCannon>().Detach();
            Assert.That(Object.FindObjectsByType<StolenCannon>().Length,Is.EqualTo(1));
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.4f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            float remaining=module.Ammo.Remaining;Assert.That(remaining,Is.InRange(70,90));
            yield return Tap(Key.Digit1);yield return new WaitForSeconds(1.2f);yield return Tap(Key.Digit2);yield return new WaitForSeconds(1.2f);
            Assert.That(module.Ammo.Remaining,Is.EqualTo(remaining));Assert.That(weapons.GetActiveWeapon().GetCurrentAmmo(),Is.EqualTo(Mathf.FloorToInt(remaining)));
            yield return Tap(Key.G);Assert.That(carrier.Held,Is.Null);Assert.That(module.State,Is.EqualTo(StolenCannonState.Available));
            Assert.That(module.Body.isKinematic,Is.False);Assert.That(module.SolidCollider.enabled,Is.True);Assert.That(module.Explosion.TryDetonate(),Is.False);
            Assert.That(Physics.GetIgnoreCollision(module.SolidCollider,player.GetComponent<CharacterController>()),Is.True);
            yield return new WaitForSeconds(1.2f);Assert.That(weapons.GetActiveWeapon(),Is.EqualTo(previous));
            Assert.That(Object.FindObjectsByType<Unity.FPS.UI.AmmoCounter>().Length,Is.EqualTo(1));
            PlaceForPickup(module);yield return Pickup(module);Assert.That(module.Ammo.Remaining,Is.EqualTo(remaining));
            Assert.That(enemy.GetComponent<EnemyMobile>().enabled,Is.False);
            Assert.That(enemy.GetCurrentWeapon().GetCurrentAmmo(),Is.Zero);
            Assert.That(enemy.TryAtack(player.transform.position),Is.False,"Cached enemy weapon must also remain unusable");
        }

        [UnityTest] public IEnumerator FullInventoryLeavesModuleAvailableUntilASlotIsReleased()
        {
            var module=DetachAndPlace();WeaponController firstExtra=null;
            for(int i=0;i<8;i++)
            {
                var definition=Object.Instantiate(weapons.StartingWeapons[0],new Vector3(100+i,100,100),Quaternion.identity);
                Assert.That(weapons.AddWeapon(definition),Is.True);
                if(i==0)firstExtra=weapons.HasWeapon(definition);
            }
            yield return new WaitForSeconds(.4f);yield return Tap(Key.E);
            Assert.That(carrier.Held,Is.Null);Assert.That(module.CanPickup,Is.True);Assert.That(module.Ammo.Remaining,Is.EqualTo(100));
            Assert.That(weapons.RemoveWeapon(firstExtra),Is.True);yield return null;
            yield return Pickup(module);Assert.That(module.State,Is.EqualTo(StolenCannonState.Equipped));
        }

        [UnityTest] public IEnumerator ChargedEnergyNoReloadThenAutomaticOverdriveDetonatesOnceOnImpactPerHealth()
        {
            var module=DetachAndPlace();yield return Pickup(module);int shots=0;
            var equipped=weapons.GetActiveWeapon();equipped.OnShootProcessed+=()=>shots++;
            yield return ChargeAndRelease(1.3f);yield return ChargeAndRelease(1.3f);yield return ChargeAndRelease(1.3f);
            Assert.That(shots,Is.EqualTo(3));Assert.That(module.Ammo.Remaining,Is.InRange(0,10));Assert.That(module.Overdrive.IsArmed,Is.True);
            float remaining=module.Ammo.Remaining;
            yield return Tap(Key.R);yield return new WaitForSeconds(.2f);Assert.That(module.Ammo.Remaining,Is.LessThan(remaining));
            yield return Tap(Key.Digit1);yield return new WaitForSeconds(1.2f);yield return Tap(Key.Digit2);yield return new WaitForSeconds(1.2f);
            Assert.That(module.Ammo.Remaining,Is.LessThan(remaining));
            var target=Target(player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*4);int hits=0;target.OnDamaged+=(damage,source)=>hits++;
            yield return null;Assert.That(module.Overdrive.IsArmed,Is.True);
            Assert.That(module.Overdrive.Smoke.particleCount,Is.GreaterThan(0));
            var smoke=module.Overdrive.Smoke;var smokeRenderer=smoke.GetComponent<ParticleSystemRenderer>();
            Assert.That(smokeRenderer.bounds.size.x,Is.LessThan(1.5f),"Smoke must stay concentrated at the casing despite FBX scale 100");
            var particle=new ParticleSystem.Particle[1];smoke.GetParticles(particle);
            System.IO.File.WriteAllLines("Tools/IronRequiem/Reports/design-v02-smoke.txt",new[]{
                "emitter="+smoke.transform.position,"viewport="+player.PlayerCamera.WorldToViewportPoint(smoke.transform.position),
                "bounds="+smokeRenderer.bounds,"enabled="+smokeRenderer.enabled,"shader="+smokeRenderer.sharedMaterial.shader.name,
                "particle="+particle[0].position,"size="+particle[0].GetCurrentSize(smoke),"color="+particle[0].GetCurrentColor(smoke),
                "scale="+smoke.transform.lossyScale,"layer="+smoke.gameObject.layer,"mask="+player.PlayerCamera.cullingMask});
            ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/design-v02-critical-hud.png");yield return null;yield return null;yield return null;
            int blasts=0;module.Explosion.Detonated+=()=>blasts++;
            Vector3 blastPosition=Vector3.zero;module.Explosion.Detonated+=()=>blastPosition=module.Explosion.DetonationPosition;
            yield return Tap(Key.G);Assert.That(carrier.Held,Is.Null);
            yield return new WaitForSeconds(1.3f);Assert.That(blasts,Is.EqualTo(1));Assert.That(hits,Is.EqualTo(1));Assert.That(target.CurrentHealth,Is.LessThan(1000));
            Assert.That(Vector3.Distance(blastPosition,target.transform.position),Is.LessThan(2));
            yield return new WaitForSeconds(2);Assert.That(blasts,Is.EqualTo(1));Assert.That(hits,Is.EqualTo(1));
            Assert.That(weapons.GetActiveWeapon(),Is.Not.Null);
        }

        [UnityTest] public IEnumerator OverdriveBackupFusePausesWithGameAndModuleDestructionRestoresInventory()
        {
            var previous=weapons.GetActiveWeapon();var module=DetachAndPlace();yield return Pickup(module);
            JsonUtility.FromJsonOverwrite("{\"criticalGraceDuration\":100}",module.Overdrive);
            JsonUtility.FromJsonOverwrite("{\"backupFuse\":0.6}",module.Explosion);
            module.Ammo.TryConsume(80);int blasts=0;module.Explosion.Detonated+=()=>blasts++;
            yield return Tap(Key.G);module.Body.isKinematic=true;module.transform.position=new Vector3(0,8,0);Physics.SyncTransforms();
            float pausedEnergy=module.Ammo.Remaining;
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.35f);Assert.That(blasts,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(pausedEnergy));Time.timeScale=1;
            yield return new WaitForSeconds(module.Explosion.BackupFuse+.2f);Assert.That(blasts,Is.EqualTo(1));Assert.That(weapons.GetActiveWeapon(),Is.EqualTo(previous));
            // A second independent module verifies removal/destruction without a throw.
#if UNITY_EDITOR
            var prefab=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/T07_IR_v01.prefab"),new Vector3(8,0,8),Quaternion.identity);
#else
            Assert.Ignore("Requires the saved Editor lab assets.");yield break;
#endif
            yield return null;prefab.GetComponentInChildren<DetachableCannon>().Detach();
            var second=Object.FindAnyObjectByType<StolenCannon>();PlaceForPickup(second);yield return Pickup(second);
            Object.Destroy(second.gameObject);yield return null;yield return new WaitForSeconds(1.2f);
            Assert.That(carrier.Held==null,Is.True);Assert.That(weapons.GetActiveWeapon(),Is.EqualTo(previous));
        }

        [UnityTest] public IEnumerator PistolModuleStartsAloneAndRequiresFreshPressWithFasterCadence()
        {
            Assert.That(weapons.StartingWeapons.Count,Is.EqualTo(1));Assert.That(weapons.GetWeaponAtSlotIndex(1),Is.Null);
            var pistol=weapons.GetActiveWeapon();Assert.That(pistol.ShootType,Is.EqualTo(WeaponShootType.Manual));
            Assert.That(pistol.WeaponMuzzle.name,Is.EqualTo("Muzzle"));
            ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/weapon-feedback-pistol.png");yield return null;yield return null;
            Assert.That(pistol.BulletsPerShot,Is.EqualTo(1));Assert.That(pistol.BulletSpreadAngle,Is.Zero);
            yield return Tap(Key.Digit2);yield return new WaitForSeconds(1.2f);Assert.That(weapons.GetActiveWeapon(),Is.EqualTo(pistol));
            int shots=0,firstShotProjectiles=0;float firstShot=0,lastShot=0;pistol.OnShootProcessed+=()=>
            {
                shots++;if(shots==1)
                {
                    firstShot=Time.time;
                    firstShotProjectiles=System.Array.FindAll(Object.FindObjectsByType<ProjectileStandard>(FindObjectsSortMode.None),p=>p.Owner==player.gameObject).Length;
                }
                lastShot=Time.time;
            };
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.1f);
            Assert.That(shots,Is.EqualTo(1));
            Assert.That(firstShotProjectiles,Is.EqualTo(1),"Count at release, before the fast projectile hits the arena");
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.1f);
            Assert.That(shots,Is.EqualTo(1),"A fresh press inside the cooldown must not fire");
            yield return new WaitForSeconds(.6f);Assert.That(shots,Is.EqualTo(1),"Holding cannot queue another pistol shot");
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            yield return ChargeAndRelease(.05f);Assert.That(shots,Is.EqualTo(2));Assert.That(lastShot-firstShot,Is.GreaterThanOrEqualTo(.4f));
            float previousShot=lastShot;
            while(Time.time<previousShot+.42f)yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;yield return null;
            Assert.That(shots,Is.EqualTo(3),"A fresh press after 0.4s must fire before the old 0.5s cooldown");
            Assert.That(lastShot-previousShot,Is.InRange(.4f,.5f));
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            pistol.SetAmmo(0);yield return new WaitForSeconds(.15f);
            var inheritedSteam=pistol.GetComponent<OverheatBehavior>();
            Assert.That(inheritedSteam.enabled,Is.False);Assert.That(inheritedSteam.SteamVfx.isEmitting,Is.False);
            Assert.That(inheritedSteam.SteamVfx.particleCount,Is.Zero);Assert.That(pistol.GetComponent<CannonExplosion>(),Is.Null);
        }

        [UnityTest] public IEnumerator CannonChargeCapsPowerAndConsumesOnlyTheReleasedShot()
        {
            var module=DetachAndPlace();yield return Pickup(module);var weapon=module.EquippedWeapon;
            var charge=weapon.GetComponent<CannonChargeShot>();var feedback=weapon.GetComponent<CannonChargePresentation>();
            int shots=0;float shotDamage=0,shotRadius=0,shotDiameter=0;
            weapon.OnShootProcessed+=()=>
            {
                shots++;shotDamage=0;
                foreach(var projectile in Object.FindObjectsByType<ProjectileStandard>(FindObjectsSortMode.None))
                    if(projectile.Owner==player.gameObject)
                    {
                        shotDamage=Mathf.Max(shotDamage,projectile.Damage);shotRadius=projectile.Radius;
                        shotDiameter=projectile.GetComponent<ChargedProjectileDamage>().EnergyVisual.lossyScale.x;
                    }
            };
            yield return ChargeAndRelease(.07f);Assert.That(shots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(100));
            yield return ChargeAndRelease(.35f);Assert.That(shots,Is.EqualTo(1));
            float partialCost=100-module.Ammo.Remaining;float partialDamage=shotDamage,partialRadius=shotRadius,partialDiameter=shotDiameter;
            yield return new WaitForSeconds(.12f); // Accepted-shot contraction lasts at most 85 ms.
            Assert.That(feedback.IsVisible,Is.False);Assert.That(feedback.EnergyParticles.particleCount,Is.Zero);
            Assert.That(partialCost,Is.InRange(12,20));Assert.That(partialDamage,Is.InRange(25,50));
            Assert.That(module.Overdrive.Smoke.isEmitting,Is.False,"Only a critical explosive module may smoke");
            Assert.That(module.Overdrive.Smoke.particleCount,Is.Zero);
            Assert.That(weapon.GetComponent<OverheatBehavior>().SteamVfx.gameObject.activeSelf,Is.False);
            Assert.That(partialCost,Is.EqualTo(charge.LastEnergyCost).Within(.01f));
            Assert.That(partialDamage,Is.EqualTo(Mathf.Lerp(25,65,charge.LastShotPower)).Within(.01f));
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(1.45f);
            Assert.That(shots,Is.EqualTo(1));Assert.That(charge.ChargeFraction,Is.EqualTo(1));
            Assert.That(module.Ammo.Remaining,Is.EqualTo(100-partialCost).Within(.01f));
            Assert.That(feedback.IsVisible,Is.True);Assert.That(feedback.EnergyParticles.particleCount,Is.GreaterThan(0));
            Assert.That(feedback.EnergyParticles.particleCount,Is.LessThanOrEqualTo(32));Assert.That(feedback.EnergyCore.lossyScale.x,Is.GreaterThan(.15f));
            ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/weapon-feedback-charge.png");yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;
            Assert.That(shots,Is.EqualTo(2));Assert.That(charge.LastEnergyCost,Is.EqualTo(30).Within(.01f));
            Assert.That(shotDamage,Is.EqualTo(65).Within(.01f));Assert.That(module.Ammo.Remaining,Is.EqualTo(70-partialCost).Within(.01f));
            Assert.That(shotRadius,Is.EqualTo(.14f).Within(.001f));Assert.That(shotDiameter,Is.EqualTo(.28f).Within(.001f));
            Assert.That(shotRadius,Is.GreaterThan(partialRadius));Assert.That(shotDiameter,Is.GreaterThan(partialDiameter));
            yield return new WaitForSeconds(.12f);
            Assert.That(feedback.IsVisible,Is.False);Assert.That(feedback.EnergyParticles.particleCount,Is.Zero);
        }

        [UnityTest] public IEnumerator FullChargeUsesItsLargerRadiusForAnActualOffsetHit()
        {
            var module=DetachAndPlace();yield return Pickup(module);
            var owner=new GameObject("ProjectileRadiusTestOwner");
            var testWeapon=Object.Instantiate(module.WeaponPrefab,owner.transform);testWeapon.Owner=owner;
            testWeapon.DelayBetweenShots=0;testWeapon.WeaponMuzzle.SetPositionAndRotation(new Vector3(0,50,0),Quaternion.identity);
            var target=new GameObject("OffsetProjectileTarget");target.transform.position=new Vector3(.115f,50,4);
            var health=target.AddComponent<Health>();health.MaxHealth=1000;
            target.AddComponent<BoxCollider>().size=new Vector3(.02f,.2f,.2f);target.AddComponent<Damageable>();
            yield return null;yield return null;Physics.SyncTransforms();
            float initial=health.CurrentHealth;
            Assert.That(testWeapon.TryShootSingleWithCharge(0),Is.True);yield return new WaitForSeconds(.15f);
            Assert.That(health.CurrentHealth,Is.EqualTo(initial),"Small 4cm radius must miss the target offset by 11.5cm");
            Assert.That(testWeapon.TryShootSingleWithCharge(1),Is.True);yield return new WaitForSeconds(.15f);
            Assert.That(health.CurrentHealth,Is.EqualTo(initial-65).Within(.01f),"Full 14cm radius must hit with its charged damage");

            // Compare the same prefab's endpoint visuals with a fixed camera; no gameplay speed changes.
            for(int i=0;i<2;i++)
            {
                var preview=Object.Instantiate(module.WeaponPrefab.ProjectilePrefab,new Vector3(i==0?-.3f:.3f,55,0),Quaternion.identity);
                preview.GetComponent<ProjectileStandard>().enabled=false;
                preview.GetComponent<ChargedProjectileDamage>().ApplyCharge(i);
            }
            var evidenceCamera=new GameObject("ProjectileComparisonCamera").AddComponent<Camera>();
            evidenceCamera.transform.position=new Vector3(0,55,-1.4f);evidenceCamera.transform.LookAt(new Vector3(0,55,0));
            evidenceCamera.clearFlags=CameraClearFlags.SolidColor;evidenceCamera.backgroundColor=new Color(.025f,.04f,.06f);evidenceCamera.fieldOfView=35;
            CaptureCamera(evidenceCamera,"weapon-feedback-projectile-size.png");Object.Destroy(evidenceCamera.gameObject);
        }

        [UnityTest] public IEnumerator SwitchingThrowingAndPauseCancelChargeWithoutAccidentalRelease()
        {
            var module=DetachAndPlace();yield return Pickup(module);var weapon=module.EquippedWeapon;var charge=weapon.GetComponent<CannonChargeShot>();int shots=0;
            weapon.OnShootProcessed+=()=>shots++;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.4f);
            Assert.That(charge.IsCharging,Is.True);yield return Tap(Key.Digit1);Assert.That(charge.IsCharging,Is.False);
            Assert.That(weapon.GetComponent<CannonChargePresentation>().IsVisible,Is.False);
            yield return new WaitForSeconds(2.1f);yield return Tap(Key.Digit2);yield return new WaitForSeconds(2.1f);
            Assert.That(charge.IsCharging,Is.False);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            Assert.That(shots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(100));
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.4f);
            Time.timeScale=0;yield return null;Assert.That(charge.IsCharging,Is.False);
            Assert.That(weapon.GetComponent<CannonChargePresentation>().IsVisible,Is.False);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;Time.timeScale=1;yield return null;
            Assert.That(shots,Is.Zero);Assert.That(module.Ammo.Remaining,Is.EqualTo(100));
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.4f);
            yield return Tap(Key.G);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            Assert.That(shots,Is.Zero);Assert.That(module.State,Is.EqualTo(StolenCannonState.Available));Assert.That(module.Ammo.Remaining,Is.EqualTo(100));
        }

        [UnityTest] public IEnumerator CriticalHeldZeroExplodesAtWeaponAndDamagesPlayerOnce()
        {
            var previous=weapons.GetActiveWeapon();var module=DetachAndPlace();yield return Pickup(module);
            var health=player.GetComponent<Health>();health.Invincible=false;int hits=0,blasts=0;health.OnDamaged+=(damage,source)=>hits++;
            Vector3 position=Vector3.zero;module.Explosion.Detonated+=()=>{blasts++;position=module.Explosion.DetonationPosition;};
            JsonUtility.FromJsonOverwrite("{\"criticalGraceDuration\":0.8}",module.Overdrive);
            Assert.That(module.Ammo.TryConsume(80),Is.True);yield return new WaitForSeconds(.2f);
            Assert.That(module.Overdrive.IsArmed,Is.True);Assert.That(module.Overdrive.Smoke.isEmitting,Is.True);Assert.That(module.Overdrive.Smoke.particleCount,Is.GreaterThan(0));
            Vector3 expected=carrier.HeldWeaponPosition;Assert.That(Vector3.Distance(module.transform.position,expected),Is.LessThan(.1f));
            float before=health.CurrentHealth;yield return new WaitForSeconds(.8f);
            Assert.That(blasts,Is.EqualTo(1));Assert.That(hits,Is.EqualTo(1));Assert.That(health.CurrentHealth,Is.LessThan(before));
            Assert.That(Vector3.Distance(position,player.PlayerCamera.transform.position),Is.LessThan(2));Assert.That(carrier.Held,Is.Null);
            yield return new WaitForSeconds(1.2f);Assert.That(weapons.GetActiveWeapon(),Is.EqualTo(previous));Assert.That(blasts,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CriticalEnergyExpiresInFlightAndCannotBeHiddenInAnotherSlot()
        {
            var module=DetachAndPlace();yield return Pickup(module);int blasts=0;Vector3 position=Vector3.zero;
            module.Explosion.Detonated+=()=>{blasts++;position=module.Explosion.DetonationPosition;};
            JsonUtility.FromJsonOverwrite("{\"criticalGraceDuration\":0.9}",module.Overdrive);module.Ammo.TryConsume(80);
            yield return Tap(Key.G);module.Body.isKinematic=true;module.transform.position=new Vector3(0,8,0);Physics.SyncTransforms();
            yield return new WaitForSeconds(1);Assert.That(blasts,Is.EqualTo(1));Assert.That(Vector3.Distance(position,new Vector3(0,8,0)),Is.LessThan(.05f));
        }

        [UnityTest] public IEnumerator SmokingPickupPreservesEnergyAndDeadlineAndStoredWeaponStillExplodes()
        {
            var previous=weapons.GetActiveWeapon();var module=DetachAndPlace();
            JsonUtility.FromJsonOverwrite("{\"criticalGraceDuration\":6}",module.Overdrive);module.Ammo.TryConsume(80);
            yield return new WaitForSeconds(.4f);float before=module.Ammo.Remaining;float dangerBefore=module.Overdrive.RemainingDangerSeconds;
            yield return Pickup(module);Assert.That(module.Ammo.Remaining,Is.LessThan(before));Assert.That(module.Overdrive.RemainingDangerSeconds,Is.LessThan(dangerBefore));
            int blasts=0;module.Explosion.Detonated+=()=>blasts++;
            yield return Tap(Key.Digit1);yield return new WaitForSeconds(1.2f);Assert.That(weapons.GetActiveWeapon(),Is.EqualTo(previous));
            float danger=module.Overdrive.RemainingDangerSeconds;
            yield return new WaitForSeconds(danger+.2f);Assert.That(blasts,Is.EqualTo(1));Assert.That(carrier.Held,Is.Null);
            Assert.That(weapons.GetWeaponAtSlotIndex(1),Is.Null);
        }

        [UnityTest] public IEnumerator SlideContactParticlesFollowAndStopWithSlideAndCAloneCrouches()
        {
            Teleport(new Vector3(0,.05f,-12));yield return new WaitForSeconds(.2f);
            var effects=player.GetComponent<SlideContactEffects>().ContactParticles;
            Keys(Key.W);yield return new WaitForSeconds(.2f);Keys(Key.W,Key.C);yield return null;Keys();yield return null;
            Assert.That(player.IsCrouching,Is.True);Assert.That(motion.IsSliding,Is.False);Assert.That(effects.isEmitting,Is.False);
            yield return Tap(Key.C);yield return new WaitForSeconds(.25f);Teleport(new Vector3(0,.05f,-12));yield return new WaitForSeconds(.1f);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.15f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;
            Assert.That(motion.IsSliding,Is.True);Assert.That(effects.isEmitting,Is.True);Keys();yield return new WaitForSeconds(.1f);
            Assert.That(effects.particleCount,Is.GreaterThan(0));Assert.That(Vector3.Distance(effects.transform.position,player.transform.position),Is.LessThan(.2f));
            CaptureParticles(effects,"design-v02-slide-particles.png");
            Teleport(new Vector3(0,4,-12));yield return null;yield return null;
            Assert.That(motion.IsSliding,Is.False);Assert.That(effects.isEmitting,Is.False);
            yield return new WaitForSeconds(.4f);Assert.That(effects.particleCount,Is.Zero);
        }

        [UnityTest] public IEnumerator DeathCancelsChargeAndDropsCriticalWeaponWithItsRemainingDanger()
        {
            var module=DetachAndPlace();yield return Pickup(module);var weapon=module.EquippedWeapon;int shots=0,blasts=0;
            weapon.OnShootProcessed+=()=>shots++;module.Explosion.Detonated+=()=>blasts++;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.35f);
            Assert.That(weapon.GetComponent<CannonChargeShot>().IsCharging,Is.True);
            module.Ammo.TryConsume(80);yield return null;Assert.That(weapon.GetComponent<CannonChargeShot>().IsCharging,Is.False);
            float remaining=module.Ammo.Remaining;Time.timeScale=0;yield return new WaitForSecondsRealtime(.3f);
            Assert.That(module.Ammo.Remaining,Is.EqualTo(remaining));Assert.That(module.Overdrive.Smoke.isEmitting,Is.False);Time.timeScale=1;
            JsonUtility.FromJsonOverwrite("{\"backupFuse\":0.5}",module.Explosion);
            player.GetComponent<Health>().Invincible=false;player.GetComponent<Health>().Kill();yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            Assert.That(shots,Is.Zero);Assert.That(carrier.Held,Is.Null);Assert.That(module.State,Is.EqualTo(StolenCannonState.Thrown));
            Assert.That(module.Ammo.Remaining,Is.LessThanOrEqualTo(remaining));yield return new WaitForSeconds(.8f);Assert.That(blasts,Is.EqualTo(1));
        }

        void CaptureParticles(ParticleSystem effect,string filename)
        {
            var camera=new GameObject("VfxEvidenceCamera").AddComponent<Camera>();camera.cullingMask=player.PlayerCamera.cullingMask;
            camera.transform.position=effect.transform.position+new Vector3(1.5f,.65f,-1.5f);camera.transform.LookAt(effect.transform.position+Vector3.up*.1f);camera.fieldOfView=40;
            CaptureCamera(camera,filename);Object.Destroy(camera.gameObject);
        }
        void CaptureCamera(Camera camera,string filename)
        {
            var target=new RenderTexture(960,540,24);camera.targetTexture=target;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();System.IO.File.WriteAllBytes("Tools/IronRequiem/Reports/"+filename,texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.Destroy(texture);Object.Destroy(target);
        }

        [UnityTest] public IEnumerator PauseDeathAndGameEndingBlockAllNewActionsAndDeathDropsWeapon()
        {
            var module=DetachAndPlace();yield return Pickup(module);
            Teleport(new Vector3(0,3,-10));yield return null;yield return null;Assert.That(player.IsGrounded,Is.False);Assert.That(motion.DashAvailable,Is.True);
            Time.timeScale=0;Keys(Key.X,Key.G,Key.Space,Key.E);yield return null;Keys();yield return null;
            Assert.That(carrier.Held,Is.EqualTo(module));Assert.That(module.Overdrive.IsArmed,Is.False);Assert.That(motion.IsDashing,Is.False);Time.timeScale=1;
            EventManager.Broadcast(Events.AllObjectivesCompletedEvent);yield return null;
            Keys(Key.X,Key.G,Key.Space);yield return null;Keys();Assert.That(carrier.Held,Is.EqualTo(module));Assert.That(module.Overdrive.IsArmed,Is.False);Assert.That(motion.IsDashing,Is.False);
            player.GetComponent<Health>().Invincible=false;player.GetComponent<Health>().Kill();yield return null;
            Assert.That(player.IsDead,Is.True);Assert.That(carrier.Held,Is.Null);Assert.That(module.State,Is.EqualTo(StolenCannonState.Available));
            Keys(Key.E,Key.Space,Key.X,Key.G);yield return null;Keys();Assert.That(carrier.Held,Is.Null);Assert.That(motion.IsDashing,Is.False);
        }
    }
}

