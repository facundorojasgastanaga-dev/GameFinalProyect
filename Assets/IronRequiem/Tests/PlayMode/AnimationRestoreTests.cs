using System.Collections;
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
using UnityEditor;
using UnityEditor.SceneManagement;
namespace IronRequiem.Tests
{
    public sealed class AnimationRestoreTests
    {
        PlayerCharacterController player;BigBotCombat big;RobotDamageProgression t07;Keyboard keyboard;Mouse mouse;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previous;float oldCapture;
        [UnitySetUp]public IEnumerator Setup(){Time.timeScale=1;oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;previous=InputSystem.actions.devices;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};keyboard.MakeCurrent();mouse.MakeCurrent();EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/HitWeaponLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;player=Object.FindAnyObjectByType<PlayerCharacterController>();player.GetComponent<Health>().Invincible=true;big=Object.FindAnyObjectByType<BigBotCombat>();t07=Object.FindAnyObjectByType<RobotDamageProgression>();foreach(var enemy in Object.FindObjectsByType<EnemyController>()){enemy.enabled=false;var mobile=enemy.GetComponent<EnemyMobile>();if(mobile)mobile.enabled=false;var bot=enemy.GetComponent<BigBotCombat>();if(bot)bot.enabled=false;var turret=enemy.GetComponent<EnemyTurret>();if(turret)turret.enabled=false;enemy.NavMeshAgent.isStopped=true;}EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(.6f);}
        [UnityTearDown]public IEnumerator Cleanup(){Time.captureDeltaTime=oldCapture;var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("RestoreCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(scene);InputSystem.actions.devices=previous;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;yield return null;}
        void Move(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
        Damageable Body(GameObject go)=>go.GetComponentsInChildren<Damageable>(true).First(d=>d.Health==go.GetComponent<Health>());
        [UnityTest]public IEnumerator FuelCellsAndDamage()
        {
            var weapon=player.GetComponent<PlayerWeaponsManager>().GetActiveWeapon();var cells=weapon.GetComponent<WeaponFuelCellHandler>();Assert.That(cells.FuelCells.Length,Is.EqualTo(4));Assert.That(cells.FuelCells.All(c=>c.activeInHierarchy&&c.name.StartsWith("AnimatedCapsule_")),Is.True);Assert.That(cells.FuelCells.All(c=>c.transform.localPosition==Vector3.zero),Is.True);Assert.That(weapon.DelayBetweenShots,Is.EqualTo(.1f));Assert.That(weapon.ShootType,Is.EqualTo(WeaponShootType.Automatic));Assert.That(weapon.ProjectilePrefab.GetComponent<ProjectileStandard>().Damage,Is.EqualTo(6.5f));var box=big.GetComponent<BoxCollider>();Move(new Vector3(box.bounds.center.x,.03f,box.bounds.min.z-5));float health=big.GetComponent<Health>().CurrentHealth;InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.12f);Assert.That(health-big.GetComponent<Health>().CurrentHealth,Is.EqualTo(6.5f));
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(1.4f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForEndOfFrame();Assert.That(cells.FuelCells.Max(c=>c.transform.localPosition.y),Is.GreaterThan(.05f),"Template controller must animate visible cells, not legacy hidden cells");ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/AnimationRestore/capsules-used.png");yield return new WaitForSeconds(1);Assert.That(cells.FuelCells.All(c=>c.transform.localPosition.sqrMagnitude<.00001f),Is.True,"Recharge returns every visible capsule to rest");Assert.That(player.GetComponent<ProtagonistHandsPresentation>().RightHandVisible,Is.False);ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/AnimationRestore/capsules-restored.png");
        }
        [UnityTest]public IEnumerator EveryHitReactsWithoutStoppingCombat()
        {
            var enemy=big.GetComponent<EnemyController>();enemy.PatrolPath=null;enemy.NavMeshAgent.Warp(new Vector3(-10,0,12));big.transform.rotation=Quaternion.Euler(0,180,0);Move(new Vector3(-10,.03f,5));enemy.enabled=true;big.enabled=true;big.GetComponent<EnemyMobile>().enabled=true;enemy.NavMeshAgent.isStopped=true;var fb=big.GetComponent<RobotHitFeedback>();var body=Body(big.gameObject);var start=big.transform.position;int shots=big.ShotsFired,hits=0;float until=Time.time+5,next=Time.time,maxAngle=0;while(Time.time<until){if(Time.time>=next){body.InflictDamageAtPoint(1,false,player.gameObject,big.transform.position+Vector3.up*1.6f,Vector3.back);hits++;next=Time.time+.1f;}yield return new WaitForEndOfFrame();maxAngle=Mathf.Max(maxAngle,fb.CurrentReactionAngle);}Assert.That(fb.ReactionCount,Is.EqualTo(hits),"One motion contribution per hit, no three-hit cooldown");Assert.That(fb.ImpactCount,Is.EqualTo(hits));Assert.That(maxAngle,Is.GreaterThan(2.5f));Assert.That(big.ShotsFired,Is.GreaterThan(shots));Assert.That(Vector3.Distance(big.transform.position,start),Is.LessThan(.06f));yield return new WaitForSeconds(.4f);Assert.That(fb.CurrentReactionAngle,Is.EqualTo(0));Assert.That(fb.IsFlashing,Is.False);System.IO.File.WriteAllText("Tools/IronRequiem/Reports/AnimationRestore/reaction.txt","hits="+hits+" contributions="+fb.ReactionCount+" peak torso degrees="+maxAngle+" shots="+(big.ShotsFired-shots));
        }
        [UnityTest]public IEnumerator CommonRobotReactsAndClears()
        {
            var fb=t07.GetComponent<RobotHitFeedback>();var body=Body(t07.gameObject);var start=t07.transform.position;float max=0;for(int i=0;i<8;i++){body.InflictDamageAtPoint(1,false,player.gameObject,t07.transform.position+Vector3.up*1.4f,Vector3.back);float end=Time.time+.1f;while(Time.time<end){yield return new WaitForEndOfFrame();max=Mathf.Max(max,fb.CurrentReactionAngle);}}Assert.That(fb.ReactionCount,Is.EqualTo(8));Assert.That(max,Is.GreaterThan(2.5f));Assert.That(Vector3.Distance(start,t07.transform.position),Is.LessThan(.001f));t07.GetComponent<Health>().TakeDamage(10000,player.gameObject);yield return null;Assert.That(fb.IsFlashing,Is.False);Assert.That(fb.Sparks.isPlaying,Is.False);
        }
    }
}