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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace IronRequiem.Tests
{
    public sealed class T07AnimationBehaviorTests
    {
        const string Reports="Tools/IronRequiem/Reports/T07Animation/";
        EnemyController enemy;
        T07AnimationDriver driver;
        PlayerCharacterController player;
        Keyboard keyboard;
        Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;Application.runInBackground=true;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/T07OptimizationLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
            previousDevices=InputSystem.actions.devices;InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};
            yield return null;yield return null;
            enemy=Object.FindAnyObjectByType<EnemyController>();driver=enemy.GetComponent<T07AnimationDriver>();player=Object.FindAnyObjectByType<PlayerCharacterController>();
            Assert.That(driver,Is.Not.Null);player.GetComponent<Health>().Invincible=true;
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(.5f);
            Directory.CreateDirectory(Reports);Assert.That(player.GetComponent<MechanicInput>().CanAct,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("T07AnimationCleanup"));
            yield return SceneManager.UnloadSceneAsync(old);InputSystem.actions.devices=previousDevices;
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;yield return null;
        }
        void FreezeEnemy(){enemy.GetComponent<EnemyMobile>().enabled=false;enemy.NavMeshAgent.ResetPath();enemy.NavMeshAgent.isStopped=true;}
        Transform Bone(string name)=>enemy.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name && t.IsChildOf(enemy.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.name=="Body_LOD0").rootBone));
        [UnityTest] public IEnumerator NativePatrolDrivesSkinnedWalkFeetLodsAndTurning()
        {
            var group=enemy.GetComponentsInChildren<LODGroup>().Single(g=>g.name=="Body");var position=enemy.transform.position;
            FreezeEnemy();yield return new WaitForSeconds(.3f);CaptureEnemy("idle.png");
            enemy.NavMeshAgent.isStopped=false;enemy.GetComponent<EnemyMobile>().enabled=true;position=enemy.transform.position;yield return new WaitForSeconds(2);
            Assert.That(Vector3.Distance(enemy.transform.position,position),Is.GreaterThan(.5f));Assert.That(driver.Speed,Is.GreaterThan(.1f));
            Assert.That(driver.Animator.GetFloat("MoveSpeed"),Is.EqualTo(enemy.NavMeshAgent.velocity.magnitude).Within(.1f));
            var feet=enemy.GetComponent<T07FootPlacement>();var previous=feet.Targets[0].position;int plantedFrames=0;float maximumTargetDrift=0,maximumFootError=0;
            for(int i=0;i<70;i++)
            {
                bool stance=feet.LeftPlanted;previous=feet.Targets[0].position;yield return null;
                if(stance && feet.LeftPlanted)
                {
                    plantedFrames++;maximumTargetDrift=Mathf.Max(maximumTargetDrift,Vector3.Distance(previous,feet.Targets[0].position));
                    maximumFootError=Mathf.Max(maximumFootError,Vector3.Distance(Bone("Foot_L").position,feet.Targets[0].position));
                }
            }
            Assert.That(plantedFrames,Is.GreaterThan(10));Assert.That(maximumTargetDrift,Is.LessThan(.015f));
            Assert.That(maximumFootError,Is.LessThan(.16f),"Actual constrained foot must follow its planted target");
            CaptureEnemy("walk.png");
            for(int lod=0;lod<3;lod++)
            {
                group.ForceLOD(lod);yield return null;yield return null;
                var skin=(SkinnedMeshRenderer)group.GetLODs()[lod].renderers[0];var mesh=new Mesh();skin.BakeMesh(mesh);
                Assert.That(mesh.vertexCount,Is.GreaterThan(0));Assert.That(mesh.bounds.size.magnitude,Is.GreaterThan(.01f));Object.Destroy(mesh);
                CaptureEnemy("lod-"+lod+".png");
            }
            group.ForceLOD(-1);float yaw=enemy.transform.eulerAngles.y;
            enemy.OrientTowards(enemy.transform.position+enemy.transform.right*8);yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw,enemy.transform.eulerAngles.y)),Is.GreaterThan(.1f));
            File.WriteAllText(Reports+"locomotion.json","{\"plantedFrames\":"+plantedFrames+",\"maximumTargetDrift\":"+maximumTargetDrift.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"maximumActualFootError\":"+maximumFootError.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"rootMotion\":false}");
        }
        [UnityTest] public IEnumerator AttackDamageReactionAndDetachKeepOneIndependentLodCannon()
        {
            FreezeEnemy();int attacks=0;enemy.onAttack+=()=>attacks++;
            driver.Animator.SetBool("Alerted",true);yield return new WaitForSeconds(.2f);
            Assert.That(driver.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.AlertedLocomotion"),Is.True);
            var joint=enemy.GetComponentInChildren<DetachableCannon>();var module=enemy.GetComponentInChildren<StolenCannon>();
            var original=module.transform.position;
            enemy.TryAtack(player.PlayerCamera.transform.position);yield return new WaitForSeconds(.09f);
            Assert.That(attacks,Is.GreaterThan(0));Assert.That(driver.Animator.GetCurrentAnimatorStateInfo(1).IsName("UpperBody.CannonAttack"),Is.True);
            CaptureEnemy("attack.png");Assert.That(Vector3.Distance(original,module.transform.position),Is.GreaterThan(.0001f));
            joint.Detach();joint.Detach();yield return null;
            Assert.That(Object.FindObjectsByType<StolenCannon>().Length,Is.EqualTo(1));Assert.That(module.transform.parent,Is.Null);
            Assert.That(module.GetComponent<LODGroup>().GetLODs(),Has.Length.EqualTo(3));Assert.That(module.GetComponentsInChildren<SkinnedMeshRenderer>(),Is.Empty);
            Assert.That(driver.Animator.GetBool("Disarmed"),Is.True);Assert.That(enemy.GetCurrentWeapon().GetCurrentAmmo(),Is.Zero);
            Assert.That(enemy.GetCurrentWeapon().AutomaticReload,Is.False);yield return new WaitForSeconds(.25f);
            Assert.That(driver.Animator.GetCurrentAnimatorStateInfo(1).IsName("UpperBody.CannonAttack"),Is.False);
            driver.Animator.SetTrigger("OnDamaged");yield return new WaitForSeconds(.08f);
            Assert.That(driver.Animator.GetCurrentAnimatorStateInfo(1).IsName("UpperBody.HitReaction"),Is.True);
            CaptureEnemy("disarmed-hit.png");
        }
        [UnityTest] public IEnumerator DeathInterruptsAttackStopsAiAndAgentAndFinishesBeforeDestruction()
        {
            var module=enemy.GetComponentInChildren<StolenCannon>();driver.Animator.SetTrigger("Attack");yield return new WaitForSeconds(.08f);
            enemy.GetComponent<Health>().Kill();yield return new WaitForSeconds(.25f);
            Assert.That(driver.IsDead,Is.True);Assert.That(enemy.enabled,Is.False);Assert.That(enemy.GetComponent<EnemyMobile>().enabled,Is.False);
            Assert.That(enemy.NavMeshAgent.enabled,Is.False);Assert.That(driver.Animator.GetBool("Dead"),Is.True);
            Assert.That(driver.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Death"),Is.True);
            Assert.That(module.transform.parent,Is.Null);Assert.That(module.GetComponent<LODGroup>(),Is.Not.Null);
            Vector3 stopped=enemy.transform.position;yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(stopped,enemy.transform.position),Is.LessThan(.001f));CaptureEnemy("death.png");
            yield return new WaitForSeconds(1.3f);Assert.That(enemy==null,Is.True);Assert.That(module,Is.Not.Null);
        }
        [UnityTest] public IEnumerator RealPickupChargeCriticalSmokeThrowAndSingleExplosionPreserveEnergyAndLods()
        {
            FreezeEnemy();var joint=enemy.GetComponentInChildren<DetachableCannon>();var module=enemy.GetComponentInChildren<StolenCannon>();joint.Detach();
            module.Body.isKinematic=true;module.transform.position=player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*1.5f;
            Physics.SyncTransforms();module.transform.position+=player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*1.5f-module.SolidCollider.bounds.center;Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);Cursor.lockState=CursorLockMode.Locked;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return new WaitForSecondsRealtime(.04f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(2.1f);
            var carrier=player.GetComponent<StolenWeaponCarrier>();Assert.That(carrier.Held,Is.SameAs(module));
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(1.35f);
            Assert.That(module.EquippedWeapon.GetComponent<CannonChargePresentation>().IsVisible,Is.True);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;
            Assert.That(module.Ammo.Remaining,Is.EqualTo(70).Within(.01f));module.Ammo.Drain(50);yield return new WaitForSeconds(.15f);
            Assert.That(module.Overdrive.Smoke.isEmitting,Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(module.State,Is.EqualTo(StolenCannonState.Thrown));Assert.That(module.GetComponent<LODGroup>().GetLODs().All(l=>l.renderers[0].enabled),Is.True);
            int blasts=0;module.Explosion.Detonated+=()=>blasts++;
            Assert.That(module.Explosion.TryDetonate(),Is.True);Assert.That(module.Explosion.TryDetonate(),Is.False);Assert.That(blasts,Is.EqualTo(1));
            yield return null;Assert.That(Object.FindObjectsByType<CannonExplosionPresentation>().Length,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator HeldEnergyZeroDamagesOnceAndPresentationCannotApplyAdditionalDamage()
        {
            FreezeEnemy();var module=enemy.GetComponentInChildren<StolenCannon>();enemy.GetComponentInChildren<DetachableCannon>().Detach();module.Body.isKinematic=true;
            yield return new WaitForSeconds(.4f);var manager=player.GetComponent<PlayerWeaponsManager>();manager.AddWeapon(module.WeaponPrefab);
            Assert.That(module.Equip(player.GetComponent<StolenWeaponCarrier>(),manager.HasWeapon(module.WeaponPrefab)),Is.True);
            var health=player.GetComponent<Health>();health.Invincible=false;int hits=0;health.OnDamaged+=(damage,source)=>hits++;
            module.Ammo.Drain(100);yield return null;Assert.That(hits,Is.EqualTo(1));float after=health.CurrentHealth;
            yield return new WaitForSeconds(.4f);Assert.That(hits,Is.EqualTo(1));Assert.That(health.CurrentHealth,Is.EqualTo(after));
        }
        [UnityTest] public IEnumerator ReusableExplosionShowsPhasesLimitsConcurrentLightsAndCleansUpOffCamera()
        {
            FreezeEnemy();var prefab=enemy.GetComponentInChildren<CannonExplosion>().PresentationPrefab;
            Vector3 center=player.transform.position+player.transform.forward*7+Vector3.up;
            var effect=Object.Instantiate(prefab,center,Quaternion.identity).GetComponent<CannonExplosionPresentation>();
            var camera=CameraAt(center+new Vector3(5,3,-8),center);
            yield return new WaitForSeconds(.05f);Capture(camera,"explosion-005.png");
            Assert.That(effect.Layers.Sum(p=>p.particleCount),Is.GreaterThan(0));
            Assert.That(player.PlayerCamera.GetComponent<DistanceCameraImpulse>().IsShaking,Is.True,"Nearby explosion must produce a live impulse");
            Assert.That(effect.GetComponent<AudioSource>().isPlaying,Is.True,"Sound starts with the detonation");
            yield return new WaitForSeconds(.18f);Capture(camera,"explosion-023.png");
            Assert.That(effect.Flash.enabled,Is.False);yield return new WaitForSeconds(.8f);Capture(camera,"explosion-103.png");
            Object.Destroy(camera.gameObject);yield return new WaitForSeconds(2.4f);Assert.That(effect==null,Is.True);
            var shake=player.PlayerCamera.GetComponent<DistanceCameraImpulse>();Assert.That(shake,Is.Not.Null);Assert.That(shake.IsShaking,Is.False);
            Vector3 basePosition=player.PlayerCamera.transform.localPosition;Quaternion baseRotation=player.PlayerCamera.transform.localRotation;
            for(int i=0;i<8;i++)Object.Instantiate(prefab,center+new Vector3((i%4-1.5f)*2,0,(i/4)*2),Quaternion.identity);
            Assert.That(CannonExplosionPresentation.ActiveFlashLights,Is.LessThanOrEqualTo(2));
            yield return new WaitForSeconds(.45f);Assert.That(CannonExplosionPresentation.ActiveFlashLights,Is.Zero);Assert.That(shake.IsShaking,Is.False);
            yield return null;Assert.That(Vector3.Distance(basePosition,player.PlayerCamera.transform.localPosition),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(baseRotation,player.PlayerCamera.transform.localRotation),Is.LessThan(.01f));
            yield return new WaitForSeconds(3);Assert.That(Object.FindObjectsByType<CannonExplosionPresentation>(),Is.Empty);
            Object.Instantiate(prefab,center+Vector3.forward*150,Quaternion.identity);yield return null;
            Assert.That(CannonExplosionPresentation.ActiveFlashLights,Is.Zero);Assert.That(shake.IsShaking,Is.False);
            yield return new WaitForSeconds(3.5f);Assert.That(Object.FindObjectsByType<CannonExplosionPresentation>(),Is.Empty);
        }
        void CaptureEnemy(string name)
        {
            var camera=CameraAt(enemy.transform.position+enemy.transform.forward*4.2f+enemy.transform.right*2+Vector3.up*1.8f,enemy.transform.position+Vector3.up);
            Capture(camera,name);Object.Destroy(camera.gameObject);
        }
        static Camera CameraAt(Vector3 position,Vector3 target)
        {
            var camera=new GameObject("T07EvidenceCamera").AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.085f,.105f);camera.fieldOfView=40;return camera;
        }
        static void Capture(Camera camera,string filename)
        {
            var target=new RenderTexture(960,640,24);camera.targetTexture=target;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(960,640,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,960,640),0,0);texture.Apply();File.WriteAllBytes(Reports+filename,texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.Destroy(texture);Object.Destroy(target);
        }
    }
}
