using System.Collections;
using System.Linq;
using NUnit.Framework;
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
    public sealed class SlidePistolMainTests
    {
        PlayerCharacterController player;PlayerWeaponsManager weapons;AdvancedPlayerMovement motion;Keyboard keys;Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        [UnitySetUp]public IEnumerator Setup()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/SlidePistolLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            Application.runInBackground=true;Time.timeScale=1;Time.captureDeltaTime=0;
            yield return null;yield return null;
            Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;Object.FindAnyObjectByType<GameFlowManager>().enabled=false;
            player=Object.FindAnyObjectByType<PlayerCharacterController>();weapons=player.GetComponent<PlayerWeaponsManager>();motion=player.GetComponent<AdvancedPlayerMovement>();
            player.GetComponent<Health>().Invincible=true;
            Assert.That(motion,Is.Not.Null,"MainScene player must have its existing motion extension.");
            previousDevices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,mouse};
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="SlideVerificationFloor";floor.transform.position=new Vector3(0,9.5f,0);floor.transform.localScale=new Vector3(100,1,100);
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(new Vector3(0,10.05f,-15),Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(.4f);Assert.That(player.IsGrounded,Is.True);
        }
        void Keys(params Key[] pressed){Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(keys,new KeyboardState(pressed));}
        IEnumerator Tap(params Key[] pressed){Keys(pressed);yield return new WaitForSeconds(.05f);Keys();yield return null;}
        [UnityTearDown]public IEnumerator Cleanup()
        {
            InputSystem.actions.devices=previousDevices;if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            Cursor.lockState=CursorLockMode.None;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("SlidePistolCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(old);yield return null;
        }
        [UnityTest]public IEnumerator OriginalPistolModelShootsThroughNativeInput()
        {
            yield return new WaitForSeconds(1.2f);var pistol=weapons.GetActiveWeapon();Assert.That(pistol,Is.Not.Null);
#if UNITY_EDITOR
            Assert.That(AssetDatabase.GetAssetPath(weapons.StartingWeapons.Single()),Is.EqualTo("Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol.prefab"));
#endif
            var visual=pistol.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="PistolModule_1000_Equipped");
            Assert.That(visual.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),Is.True);Assert.That(pistol.WeaponMuzzle.IsChildOf(visual),Is.True);
            int shots=0;pistol.OnShootProcessed+=()=>shots++;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;Assert.That(shots,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator PistolHasNoTemplateEnergyLoopsBeforeAndAfterFire()
        {
            yield return new WaitForSeconds(1.2f);var pistol=weapons.GetActiveWeapon();
            var particles=pistol.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(particles.Length,Is.GreaterThan(0),"Preserved inactive template sources");
            foreach(var ps in particles){Assert.That(ps.gameObject.activeInHierarchy,Is.False);Assert.That(ps.particleCount,Is.Zero);Assert.That(ps.isEmitting,Is.False);}
            int shots=0;pistol.OnShootProcessed+=()=>shots++;
            for(int i=0;i<3;i++)
            {
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.45f);
            }
            Assert.That(shots,Is.EqualTo(3));yield return new WaitForSeconds(1.2f);
            foreach(var ps in particles){Assert.That(ps.particleCount,Is.Zero);Assert.That(ps.isEmitting,Is.False);}
            Assert.That(pistol.WeaponMuzzle.IsChildOf(pistol.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="PistolModule_1000_Equipped")),Is.True);
        }
        [UnityTest]public IEnumerator SlideKeepsWeaponSteadyAllowsAimAndFireThenRestoresWalkingBob()
        {
            yield return new WaitForSeconds(1.2f);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);
            Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return new WaitForEndOfFrame();
            Assert.That(motion.IsSliding,Is.True);var socket=weapons.WeaponParentSocket;var stable=socket.localPosition;var start=player.transform.position;
            int samples=0;
            Keys(Key.W,Key.LeftShift);
            for(int i=0;i<5;i++)
            {
                yield return new WaitForEndOfFrame();if(!motion.IsSliding)break;
                Assert.That(Vector3.Distance(socket.localPosition,stable),Is.LessThan(.0001f),"No walking bob while sliding");samples++;
            }
            Assert.That(samples,Is.GreaterThanOrEqualTo(2));Assert.That(Vector3.Distance(player.transform.position,start),Is.GreaterThan(.1f));
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return null;yield return new WaitForEndOfFrame();
            Assert.That(motion.IsSliding,Is.True);Assert.That(weapons.IsAiming,Is.True);
            var cameraRotation=player.PlayerCamera.transform.rotation;
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(8,0)}.WithButton(MouseButton.Right));yield return null;yield return new WaitForEndOfFrame();
            Assert.That(Quaternion.Angle(cameraRotation,player.PlayerCamera.transform.rotation),Is.GreaterThan(.01f));
            int shots=0;weapons.GetActiveWeapon().OnShootProcessed+=()=>shots++;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));yield return null;yield return null;
            Assert.That(shots,Is.EqualTo(1));InputSystem.QueueStateEvent(mouse,new MouseState());Keys();yield return new WaitForSeconds(1.2f);
            Assert.That(motion.IsSliding,Is.False);
            Keys(Key.W);yield return new WaitForSeconds(.3f);var previous=socket.localPosition;float maxChange=0;
            for(int i=0;i<12;i++){yield return new WaitForEndOfFrame();maxChange=Mathf.Max(maxChange,Vector3.Distance(previous,socket.localPosition));}
            Assert.That(maxChange,Is.GreaterThan(.0002f),"Walking bob resumes after slide");Keys();
        }
        [UnityTest]public IEnumerator ShiftAndCStartsSlideMovesAndReturnsToStanding()
        {
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);Assert.That(player.CharacterVelocity.magnitude,Is.GreaterThan(11));
            Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;
            Assert.That(motion.IsSliding,Is.True);Assert.That(player.IsCrouching,Is.True);var start=player.transform.position;
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.15f);
            Assert.That(player.transform.position.z-start.z,Is.GreaterThan(1));Assert.That(player.GetComponent<SlideContactEffects>().ContactParticles.isEmitting,Is.True);
            yield return new WaitForSeconds(.8f);Assert.That(motion.IsSliding,Is.False);Assert.That(player.IsCrouching,Is.False);Keys();
        }
        [UnityTest]public IEnumerator StationaryCrouchAndJumpDuringSlideKeepNativeControls()
        {
            yield return Tap(Key.C);Assert.That(player.IsCrouching,Is.True);Assert.That(motion.IsSliding,Is.False);
            yield return Tap(Key.C);Assert.That(player.IsCrouching,Is.False);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;Assert.That(motion.IsSliding,Is.True);
            Keys(Key.W,Key.Space);yield return null;yield return null;Assert.That(motion.IsSliding,Is.False);Assert.That(player.IsGrounded,Is.False);Keys();
        }
    }
}
