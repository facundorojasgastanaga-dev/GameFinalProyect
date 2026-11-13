using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
    public sealed class T07EyeRegressionTests
    {
        bool background;float capture;
        static void Suppress(Scene scene,LoadSceneMode mode){foreach(var root in scene.GetRootGameObjects())foreach(var waves in root.GetComponentsInChildren<CombatTwoRoomsLab>())waves.enabled=false;}
        [UnityTest]public IEnumerator SpawnWithActiveControllerHasSmallEyeAndNoBodyGlow()
        {
            background=Application.runInBackground;capture=Time.captureDeltaTime;Application.runInBackground=true;Time.captureDeltaTime=1f/60;Time.timeScale=1;AudioListener.pause=false;SceneManager.sceneLoaded+=Suppress;
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/RequiemCampaign.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            var flow=Object.FindAnyObjectByType<CampaignFlow>();float end=Time.realtimeSinceStartup+60;while(flow.Busy&&Time.realtimeSinceStartup<end)yield return null;Assert.That(flow.Busy,Is.False);flow.Menu.BeginGame();var player=flow.Player;player.GetComponent<Health>().Invincible=true;player.TeleportToCheckpoint(new Vector3(0,.05f,-16));player.enabled=false;
            var robot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab"),new Vector3(0,0,-10),Quaternion.Euler(0,180,0));yield return new WaitForSeconds(1);
            var eye=robot.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Sphere");Assert.That(eye.GetComponent<Renderer>().bounds.size.magnitude,Is.LessThan(.1f));
            var enemy=robot.GetComponent<EnemyController>();Assert.That(enemy.isActiveAndEnabled,Is.True);Assert.That(enemy.OnHitBodyGradient.Evaluate(1).maxColorComponent,Is.LessThan(.02f));
            player.PlayerCamera.transform.LookAt(robot.transform.position+Vector3.up*1.15f);FrameGrab.Save(player.PlayerCamera,"Tools/IronRequiem/Reports/VisualRepair/Shots/t07-spawn-fixed.png",1280,720);
            robot.GetComponent<Health>().TakeDamage(1,player.gameObject);yield return new WaitForSeconds(.5f);
            var block=new MaterialPropertyBlock();foreach(var r in robot.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled))for(int slot=0;slot<r.sharedMaterials.Length;slot++){r.GetPropertyBlock(block,slot);Assert.That(block.GetColor("_EmissionColor").maxColorComponent,Is.LessThan(.05f),"Body flash must finish");}
            var weapon=enemy.GetCurrentWeapon();int shots=0;void Shot(){shots++;}weapon.OnShootProcessed+=Shot;
            Assert.That(AssetDatabase.GetAssetPath(weapon.MuzzleFlashPrefab),Does.Contain("T07_MuzzleFlash_Bounded"));Assert.That(weapon.MuzzleFlashPrefab.transform.localScale.x,Is.EqualTo(.22f).Within(.001f));
            var eyeColor=eye.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");Assert.That(eyeColor.r,Is.GreaterThan(eyeColor.g*5));
            for(int frame=0;frame<360;frame++)
            {
                if(frame==120)robot.GetComponent<Health>().TakeDamage(robot.GetComponent<Health>().MaxHealth*.55f,player.gameObject);
                if(frame==240)robot.GetComponent<Health>().TakeDamage(robot.GetComponent<Health>().MaxHealth*.22f,player.gameObject);
                if(frame%40==0)FrameGrab.Save(player.PlayerCamera,"Tools/IronRequiem/Reports/VisualRepair/Shots/t07-combat-"+frame+".png",1280,720);
                foreach(var ps in robot.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.name=="DamageSparks"||p.name=="BodyDamageSmoke"||p.name=="CriticalSmoke"))Assert.That(ps.transform.lossyScale.x,Is.EqualTo(1).Within(.01f),"Imported rig must not magnify VFX");
                yield return null;
            }
            weapon.OnShootProcessed-=Shot;Assert.That(shots,Is.GreaterThan(0),"Actual AI fires through existing charge system");
            var saw=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab"),new Vector3(3,0,-10),Quaternion.Euler(0,180,0));yield return null;
            var sawEye=saw.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="CoreIndicator");var red=sawEye.sharedMaterial.GetColor("_BaseColor");Assert.That(red.r,Is.GreaterThan(red.g*5));
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=Suppress;Time.timeScale=1;AudioListener.pause=false;foreach(var bar in Object.FindObjectsByType<Unity.FPS.UI.WorldspaceHealthBar>())bar.enabled=false;foreach(var q in Object.FindObjectsByType<EnemySpawnQueue>()){q.CancelPending();q.enabled=false;}
            var clean=SceneManager.CreateScene("T07EyeCleanup");SceneManager.SetActiveScene(clean);foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=clean&&s.isLoaded).OrderBy(s=>s.name=="RequiemCampaign"?1:0).ToArray())yield return SceneManager.UnloadSceneAsync(scene);
            Application.runInBackground=background;Time.captureDeltaTime=capture;Cursor.lockState=CursorLockMode.None;
        }
    }
}
