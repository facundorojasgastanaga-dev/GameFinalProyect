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
    public sealed class ProtagonistIntegrationTests
    {
        const string Lab="Assets/IronRequiem/Scenes/ProtagonistIntegrationLab.unity",Reports="Tools/IronRequiem/Reports/Protagonist";
        PlayerCharacterController player;PlayerWeaponsManager weapons;StolenWeaponCarrier carrier;ProtagonistHandsPresentation hands;Health health;
        Keyboard keys;Mouse mouse;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        [UnitySetUp]public IEnumerator Setup()
        {
            previousDevices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,mouse};
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(Lab,new LoadSceneParameters(LoadSceneMode.Single));
#endif
            Time.timeScale=1;Application.runInBackground=true;yield return null;yield return null;
            foreach(var flow in Object.FindObjectsByType<GameFlowManager>(FindObjectsSortMode.None))flow.enabled=false;
            foreach(var objective in Object.FindObjectsByType<ObjectiveManager>(FindObjectsSortMode.None))objective.enabled=false;
            foreach(var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)){var mobile=enemy.GetComponent<EnemyMobile>();if(mobile)mobile.enabled=false;enemy.enabled=false;if(enemy.NavMeshAgent&&enemy.NavMeshAgent.isOnNavMesh)enemy.NavMeshAgent.ResetPath();}
            player=Object.FindAnyObjectByType<PlayerCharacterController>();weapons=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();hands=player.GetComponent<ProtagonistHandsPresentation>();health=player.GetComponent<Health>();health.Invincible=true;

            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="ProtagonistVerificationFloor";floor.transform.position=new Vector3(0,9.5f,0);floor.transform.localScale=new Vector3(100,1,100);
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(new Vector3(0,10.05f,-15),Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();
#if UNITY_EDITOR
            if(!Application.isBatchMode)EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(1.5f);Assert.That(player.IsGrounded,Is.True);Assert.That(hands,Is.Not.Null);
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("ProtagonistCleanup_"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(old);
            InputSystem.actions.devices=previousDevices;if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);yield return null;
        }
        void Keys(params Key[] values){if(Time.timeScale>0)Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(keys,new KeyboardState(values));}
        IEnumerator Tap(params Key[] values){Keys(values);yield return new WaitForSecondsRealtime(.06f);Keys();yield return null;yield return null;}
        void Mouse(bool fire=false,bool aim=false,Vector2 delta=default){if(Time.timeScale>0)Cursor.lockState=CursorLockMode.Locked;var state=new MouseState{delta=delta};if(fire)state=state.WithButton(MouseButton.Left);if(aim)state=state.WithButton(MouseButton.Right);InputSystem.QueueStateEvent(mouse,state);}
        void CheckPistol()
        {
            Assert.That(hands.CurrentRepresentation,Is.EqualTo(ProtagonistHandsPresentation.Representation.Pistol));Assert.That(hands.RightHandVisible,Is.False);
            Assert.That(Vector3.Distance(hands.RightModuleSocket.position,weapons.GetActiveWeapon().WeaponMuzzle.position+weapons.GetActiveWeapon().WeaponMuzzle.rotation*new Vector3(0,-.015f,-.23f)),Is.LessThan(.0001f));
            Assert.That(Vector3.Dot(hands.RightModuleSocket.forward,weapons.GetActiveWeapon().WeaponMuzzle.forward),Is.GreaterThan(.9999f));
        }
        void CheckMeshes()
        {
            foreach(var renderer in hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!renderer.enabled)continue;var mesh=new Mesh();renderer.BakeMesh(mesh);
                Assert.That(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),Is.True);
                float closest=mesh.vertices.Min(v=>weapons.WeaponCamera.transform.InverseTransformPoint(renderer.transform.TransformPoint(v)).z);
                Assert.That(closest,Is.GreaterThan(weapons.WeaponCamera.nearClipPlane+.005f),renderer.name+" clips camera");Object.Destroy(mesh);
            }
        }
        void Capture(string name)
        {
            Directory.CreateDirectory(Reports);var camera=player.PlayerCamera;var rt=new RenderTexture(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Reports+"/"+name+".png",image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(image);
        }
        StolenCannon Detached()
        {
            var attachment=Object.FindObjectsByType<DetachableCannon>(FindObjectsSortMode.None).First();attachment.Detach();var module=Object.FindObjectsByType<StolenCannon>(FindObjectsSortMode.None).Single(x=>x.State==StolenCannonState.Available);Place(module);return module;
        }
        void Place(StolenCannon module)
        {
            module.Body.isKinematic=true;Physics.SyncTransforms();var target=player.PlayerCamera.transform.position+player.PlayerCamera.transform.forward*1.3f;module.transform.position+=target-module.SolidCollider.bounds.center;Physics.SyncTransforms();
        }
        IEnumerator Pickup(StolenCannon module)
        {
            yield return new WaitForSeconds(.5f);Assert.That(carrier.Candidate,Is.SameAs(module));yield return Tap(Key.E);yield return new WaitForSeconds(2.1f);yield return new WaitForEndOfFrame();Assert.That(carrier.Held,Is.SameAs(module));Assert.That(hands.CurrentRepresentation,Is.EqualTo(ProtagonistHandsPresentation.Representation.StolenCannon));Assert.That(hands.RightHandVisible,Is.False);
        }
        [UnityTest]public IEnumerator SharedSkeletonLodsAndMaterialRemainValid()
        {
            yield return new WaitForEndOfFrame();CheckPistol();CheckMeshes();var body=player.transform.Find("Protagonist_Body");Assert.That(body,Is.Not.Null);var group=body.GetComponent<LODGroup>();Assert.That(group.lodCount,Is.EqualTo(3));
            var lods=group.GetLODs();var material=lods[0].renderers[0].sharedMaterial;Assert.That(material.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
            for(int i=0;i<3;i++)
            {
                Assert.That(lods[i].renderers.Length,Is.EqualTo(5));group.ForceLOD(i);yield return null;
                foreach(SkinnedMeshRenderer renderer in lods[i].renderers){Assert.That(renderer.sharedMaterial,Is.SameAs(material));Assert.That(renderer.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));Assert.That(renderer.bones.All(b=>b&&b.IsChildOf(body)),Is.True);}
            }
            group.ForceLOD(-1);Assert.That(hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,Is.EqualTo(4));
            var rigs=body.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Hand_R").ToArray();Assert.That(rigs.Length,Is.EqualTo(1),"All LODs must share one skeleton");Capture("fps-pistol");
#if UNITY_EDITOR
            var texture=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")));Assert.That(texture.sRGBTexture,Is.False);
            var normal=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")));Assert.That(normal.textureType,Is.EqualTo(TextureImporterType.NormalMap));
#endif
        }
        [UnityTest]public IEnumerator NativeMovementSlideDashAimAndPistolFire()
        {
            yield return new WaitForEndOfFrame();CheckPistol();var start=player.transform.position;Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.3f);Assert.That(Vector3.Distance(start,player.transform.position),Is.GreaterThan(1));
            Keys(Key.W,Key.LeftShift,Key.C);yield return null;yield return null;Assert.That(player.GetComponent<AdvancedPlayerMovement>().IsSliding,Is.True);
            Keys(Key.W,Key.LeftShift);Mouse(false,true,new Vector2(6,0));yield return new WaitForSeconds(.1f);Assert.That(weapons.IsAiming,Is.True);yield return new WaitForEndOfFrame();CheckPistol();CheckMeshes();Capture("fps-slide-aim");
            Mouse();Keys();yield return new WaitForSeconds(1);
            yield return Tap(Key.Space);yield return new WaitForSeconds(.08f);yield return Tap(Key.Space);Assert.That(player.GetComponent<AdvancedPlayerMovement>().DashAvailable,Is.False);
            Mouse(false,true);yield return new WaitForSeconds(.3f);var weapon=weapons.GetActiveWeapon();int shots=0;weapon.OnShootProcessed+=()=>shots++;Mouse(true,true);yield return null;yield return null;Mouse();Assert.That(shots,Is.EqualTo(1));yield return new WaitForEndOfFrame();CheckPistol();
        }
        [UnityTest]public IEnumerator EmptyInventoryRestoresNormalHandsWithoutAddingMelee()
        {
            var source=weapons.StartingWeapons.Single();Assert.That(weapons.RemoveWeapon(weapons.GetActiveWeapon()),Is.True);yield return new WaitForSeconds(1.2f);
            Assert.That(hands.CurrentRepresentation,Is.EqualTo(ProtagonistHandsPresentation.Representation.Normal));Assert.That(hands.RightHandVisible,Is.True);CheckMeshes();Capture("fps-normal-fist");
            Assert.That(weapons.AddWeapon(source),Is.True);weapons.SwitchToWeaponIndex(0,true);yield return new WaitForSeconds(1.3f);yield return new WaitForEndOfFrame();CheckPistol();
        }
        [UnityTest]public IEnumerator NativeCannonPickupChargeSwitchThrowRepickupAndExplosion()
        {
            var module=Detached();yield return Pickup(module);CheckMeshes();Capture("fps-cannon");float before=module.Ammo.Remaining;int shots=0;module.EquippedWeapon.OnShootProcessed+=()=>shots++;
            Mouse(true);yield return new WaitForSeconds(1.35f);var charge=module.EquippedWeapon.GetComponent<CannonChargeShot>();Assert.That(charge.ChargeFraction,Is.EqualTo(1));var fx=module.EquippedWeapon.GetComponent<CannonChargePresentation>();Assert.That(Vector3.Distance(fx.EffectOrigin.position,module.EquippedWeapon.WeaponMuzzle.position),Is.LessThan(.0001f));Capture("fps-cannon-charge");Mouse();yield return null;yield return null;Assert.That(shots,Is.EqualTo(1));Assert.That(module.Ammo.Remaining,Is.LessThan(before));float remaining=module.Ammo.Remaining;
            yield return Tap(Key.Digit1);yield return new WaitForSeconds(2.1f);yield return new WaitForEndOfFrame();CheckPistol();Assert.That(module.Ammo.Remaining,Is.EqualTo(remaining));
            yield return Tap(Key.Digit2);yield return new WaitForSeconds(2.1f);Assert.That(hands.CurrentRepresentation,Is.EqualTo(ProtagonistHandsPresentation.Representation.StolenCannon));
            yield return Tap(Key.G);yield return new WaitForSeconds(.15f);Assert.That(carrier.Held,Is.Null);Assert.That(module.State,Is.EqualTo(StolenCannonState.Available));yield return new WaitForSeconds(1.3f);yield return new WaitForEndOfFrame();CheckPistol();Place(module);yield return Pickup(module);Assert.That(module.Ammo.Remaining,Is.EqualTo(remaining));
            int explosions=0;module.Explosion.Detonated+=()=>explosions++;module.Ammo.TryConsume(module.Ammo.Remaining-5);yield return null;yield return null;Assert.That(module.Overdrive.IsArmed,Is.True);Assert.That(module.Overdrive.Smoke.isEmitting,Is.True);
            yield return Tap(Key.G);yield return new WaitForSeconds(7.1f);Assert.That(explosions,Is.EqualTo(1));yield return new WaitForEndOfFrame();CheckPistol();CheckMeshes();
        }
        [UnityTest]public IEnumerator EquippedExhaustionRestoresPistolAndClearsChargePresentation()
        {
            var module=Detached();yield return Pickup(module);int detonations=0;module.Explosion.Detonated+=()=>detonations++;
            Mouse(true);yield return new WaitForSeconds(.35f);var feedback=module.EquippedWeapon.GetComponent<CannonChargePresentation>();Assert.That(feedback.IsVisible,Is.True);
            module.Ammo.TryConsume(module.Ammo.Remaining);Mouse();yield return new WaitForSeconds(2.2f);
            Assert.That(detonations,Is.EqualTo(1));Assert.That(carrier.Held,Is.Null);yield return new WaitForEndOfFrame();CheckPistol();
            Assert.That(hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,Is.EqualTo(4));
        }
        [UnityTest]public IEnumerator PauseAndDeathDoNotDuplicateHandsOrConsumeHeldModule()
        {
            var module=Detached();yield return Pickup(module);yield return Tap(Key.Escape);Assert.That(Time.timeScale,Is.Zero);var energy=module.Ammo.Remaining;yield return new WaitForSecondsRealtime(.25f);Assert.That(hands.CurrentRepresentation,Is.EqualTo(ProtagonistHandsPresentation.Representation.StolenCannon));Assert.That(hands.RightHandVisible,Is.False);Assert.That(module.Ammo.Remaining,Is.EqualTo(energy));
            yield return Tap(Key.Escape);Assert.That(Time.timeScale,Is.EqualTo(1));Cursor.lockState=CursorLockMode.Locked;
            health.Invincible=false;health.TakeDamage(health.MaxHealth+1,null);yield return null;yield return null;Assert.That(hands.FpsRoot.gameObject.activeInHierarchy,Is.False);Assert.That(carrier.Held,Is.Null);Assert.That(module,Is.Not.Null);Assert.That(module.State,Is.EqualTo(StolenCannonState.Available));
        }
    }
}
