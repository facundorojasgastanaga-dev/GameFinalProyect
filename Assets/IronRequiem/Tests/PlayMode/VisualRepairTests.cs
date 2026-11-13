using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Matched captures use the actual campaign camera and its existing URP weapon overlay.
    // Enemy AI is frozen for the comparison; this is presentation validation, not a performance benchmark.
    public sealed class VisualRepairTests
    {
        const string Report="Tools/IronRequiem/Reports/VisualRepair/";
        PlayerCharacterController player; PlayerWeaponsManager weapons; CampaignFlow flow;
        Keyboard keyboard;Mouse mouse;bool background;float capture;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        static void SuppressWaves(Scene scene,LoadSceneMode mode)
        {foreach(var root in scene.GetRootGameObjects())foreach(var waves in root.GetComponentsInChildren<CombatTwoRoomsLab>())waves.enabled=false;}
        static void Freeze(GameObject root)
        {
            foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true))if(c is EnemyController||c is EnemyMobile||c is EnemyTurret||c is BigBotCombat||c is SawBotCombat)c.enabled=false;
            var agent=root.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}
        }
        [UnityTest]public IEnumerator CampaignVisualsAndCurrentWeapons()
        {
            string phase=File.ReadAllText(Report+"phase.txt").Trim();Directory.CreateDirectory(Report+"Shots");
            background=Application.runInBackground;Application.runInBackground=true;capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;Time.timeScale=1;AudioListener.pause=false;
            devices=InputSystem.actions.devices;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};SceneManager.sceneLoaded+=SuppressWaves;
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/RequiemCampaign.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            flow=Object.FindAnyObjectByType<CampaignFlow>();Assert.That(flow,Is.Not.Null);float end=Time.realtimeSinceStartup+70;while(flow.Busy&&Time.realtimeSinceStartup<end)yield return null;Assert.That(flow.Busy,Is.False);
            flow.Menu.BeginGame();player=flow.Player;weapons=player.GetComponent<PlayerWeaponsManager>();player.GetComponent<Health>().Invincible=true;
            player.TeleportToCheckpoint(new Vector3(0,.05f,-20));player.transform.rotation=Quaternion.identity;player.PlayerCamera.transform.localRotation=Quaternion.identity;Physics.SyncTransforms();
            string[] paths={"Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab","Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab"};
            var starting=string.Join("\n",weapons.StartingWeapons.Select(w=>AssetDatabase.GetAssetPath(w)));File.WriteAllText(Report+phase+"-loadout.txt",starting);
            if(phase=="after")foreach(string path in paths)Assert.That(weapons.StartingWeapons.Any(w=>AssetDatabase.GetAssetPath(w)==path),Is.True,"Current weapon restored in saved campaign");
            foreach(var enemy in Object.FindObjectsByType<EnemyController>())Freeze(enemy.gameObject);
            var names=new[]{"T07_Current_Repaired","BigBot_Current","SawBot_Current","HoverBot_Current","Turret_Current"};
            var robots=new GameObject[names.Length];
            for(int i=0;i<names.Length;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/"+names[i]+".prefab");robots[i]=Object.Instantiate(prefab,new Vector3((i-2)*5,0,-7+(i%2)*3),Quaternion.Euler(0,180,0));Freeze(robots[i]);
            }
            yield return new WaitForSeconds(.7f);FrameGrab.Save(player.PlayerCamera,Report+"Shots/"+phase+"-campaign.png",1280,720);
            // Before captures deliberately add the two current modules in memory: saved loadout remains untouched.
            foreach(string path in paths){var prefab=AssetDatabase.LoadAssetAtPath<WeaponController>(path);if(!weapons.HasWeapon(prefab))Assert.That(weapons.AddWeapon(prefab),Is.True);}
            foreach(string path in paths)
            {
                var source=AssetDatabase.LoadAssetAtPath<WeaponController>(path);var w=weapons.HasWeapon(source);Assert.That(w,Is.Not.Null);int slot=Enumerable.Range(0,9).First(i=>weapons.GetWeaponAtSlotIndex(i)==w);weapons.SwitchToWeaponIndex(slot);
                float until=Time.realtimeSinceStartup+6;while(weapons.SwitchState!=PlayerWeaponsManager.WeaponSwitchState.Up&&Time.realtimeSinceStartup<until)yield return null;Assert.That(weapons.SwitchState,Is.EqualTo(PlayerWeaponsManager.WeaponSwitchState.Up));yield return new WaitForSeconds(.4f);
                string label=path.Contains("Shotgun")?"shotgun":"cannon";FrameGrab.Save(player.PlayerCamera,Report+"Shots/"+phase+"-fps-"+label+".png",1280,720);
                if(phase=="after")Assert.That(player.GetComponent<ProtagonistHandsPresentation>().RightHandVisible,Is.False,"Module replaces the hand");
                int shots=0;void Count(){shots++;}w.OnShootProcessed+=Count;InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.35f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;Assert.That(shots,Is.GreaterThan(0),label+" real M1 input fires");w.OnShootProcessed-=Count;
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return new WaitForSeconds(.6f);Assert.That(weapons.IsAiming,Is.True);FrameGrab.Save(player.PlayerCamera,Report+"Shots/"+phase+"-ads-"+label+".png",1280,720);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.25f);
            }
            if(phase=="after")
            {
                player.TeleportToCheckpoint(new Vector3(-10,.05f,-8.2f));player.PlayerCamera.transform.LookAt(new Vector3(-10,1.62f,-7.16f));Physics.SyncTransforms();FrameGrab.Save(player.PlayerCamera,Report+"Shots/after-eye-close.png",1280,720);
                player.TeleportToCheckpoint(new Vector3(0,.05f,-20));player.PlayerCamera.transform.localRotation=Quaternion.identity;
            }
            foreach(var robot in robots)
            {
                var animator=robot.GetComponentInChildren<Animator>();if(animator&&animator.runtimeAnimatorController)
                {
                    animator.enabled=false;foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())for(int i=0;i<5;i++){clip.SampleAnimation(animator.gameObject,clip.length*i/4);yield return null;foreach(var skin in robot.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.enabled))Assert.That(float.IsNaN(skin.bounds.size.x),Is.False);}
                }
                if(phase=="after"){var lights=robot.GetComponent<EnemyPowerIndicators>();Assert.That(lights&&lights.Indicators.Length>0,Is.True);Assert.That(robot.GetComponentsInChildren<Light>(true).Length,Is.Zero);robot.GetComponent<Health>().Kill();yield return null;Assert.That(lights.IsPoweredOff,Is.True);}
            }
            File.WriteAllText(Report+phase+"-play.txt","Campaign loaded; actual camera captures1280x720; frozen AI for comparison; both current modules switched, real M1 fires and ADS input tested; all existing enemy clips sampled at5 poses in Play. No FPS claim. "+(phase=="after"?"Saved campaign loadout and death power-off checked.":"Before: modules added only in memory for matched visual comparison."));
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=SuppressWaves;Time.timeScale=1;AudioListener.pause=false;var clean=SceneManager.CreateScene("VisualRepairCleanup");SceneManager.SetActiveScene(clean);
            foreach(var bar in Object.FindObjectsByType<Unity.FPS.UI.WorldspaceHealthBar>())bar.enabled=false;
            foreach(var q in Object.FindObjectsByType<EnemySpawnQueue>()){q.CancelPending();q.enabled=false;}
            foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=clean&&s.isLoaded).OrderBy(s=>s.name=="RequiemCampaign"?1:0).ToArray())yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.actions.devices=devices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);Time.captureDeltaTime=capture;Application.runInBackground=background;Cursor.lockState=CursorLockMode.None;
        }
    }
}
