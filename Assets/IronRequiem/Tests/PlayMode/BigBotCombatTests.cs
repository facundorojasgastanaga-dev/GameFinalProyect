using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
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
    public sealed class BigBotCombatTests
    {
        BigBotCombat bot;EnemyController enemy;EnemyMobile mobile;NavMeshAgent agent;Health health,playerHealth;PlayerCharacterController player;Keyboard keys;Mouse mouse;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        [UnitySetUp]public IEnumerator Setup()
        {
            Time.timeScale=1;Application.runInBackground=true;previousDevices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,mouse};
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/BigBotCombatLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;yield return null;
            bot=Object.FindAnyObjectByType<BigBotCombat>();enemy=bot.GetComponent<EnemyController>();mobile=bot.GetComponent<EnemyMobile>();
            player=Object.FindAnyObjectByType<PlayerCharacterController>();health=bot.GetComponent<Health>();playerHealth=player.GetComponent<Health>();playerHealth.MaxHealth=1000;playerHealth.CurrentHealth=1000;enemy.PatrolPath=null;agent=bot.GetComponent<NavMeshAgent>();MoveBot(new Vector3(0,0,10));MovePlayer(new Vector3(0,.03f,-12));
            // Reset any target observed during scene load through the detector's real loss-of-sight path.
            enemy.DetectionModule.KnownTargetTimeout=0;yield return new WaitForSeconds(.12f);enemy.DetectionModule.KnownTargetTimeout=8;MoveBot(new Vector3(0,0,10));
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState=CursorLockMode.Locked;yield return new WaitForSeconds(.35f);
        }
        void MoveBot(Vector3 p){Assert.That(agent.Warp(p),Is.True);bot.transform.rotation=Quaternion.Euler(0,180,0);Physics.SyncTransforms();}
        void MovePlayer(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
        IEnumerator Until(System.Func<bool> predicate,float timeout,string message){float limit=Time.time+timeout;while(!predicate()&&Time.time<limit)yield return null;Assert.That(predicate(),Is.True,message+Diagnostics());}
        string Diagnostics()
        {
            var a=player.GetComponent<Actor>();var eye=enemy.DetectionModule.DetectionSourcePoint;var delta=a.AimPoint.position-eye.position;
            return "\nBot="+bot.transform.position+" player="+player.transform.position+" aim="+a.AimPoint.position+" eye="+eye.position+" health="+health.CurrentHealth+" enabled="+enemy.enabled+" seen="+enemy.IsSeeingTarget+" known="+enemy.KnownDetectedTarget+" canSee="+bot.CanSeeSource(player.gameObject)+" canAttack="+bot.CanAttack+" state="+mobile.AiState+" actors="+string.Join(",",Object.FindAnyObjectByType<ActorsManager>().Actors.Select(x=>x.name+":"+x.Affiliation))+" hits="+string.Join(",",Physics.RaycastAll(eye.position,delta.normalized,26,-1,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).Select(h=>h.collider.name+":"+h.distance));
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            Time.timeScale=1;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("BigBotCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(old);InputSystem.actions.devices=previousDevices;if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;yield return null;
        }
        [UnityTest]public IEnumerator PlayerContactRegressionProbe()
        {
            MoveBot(new Vector3(-10,0,12));MovePlayer(new Vector3(-10,.03f,6));enemy.enabled=false;mobile.enabled=false;agent.isStopped=true;
            var body=bot.GetComponent<BoxCollider>();var cc=player.GetComponent<CharacterController>();var start=player.transform.position;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));yield return new WaitForSeconds(1.8f);InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
            System.IO.File.WriteAllText("Tools/IronRequiem/Reports/BigBotCombat/contact-probe.txt","start="+start+" end="+player.transform.position+" bot="+bot.transform.position+" bounds="+body.bounds+" collider="+body.enabled+" trigger="+body.isTrigger+" cc="+cc.enabled+" detect="+cc.detectCollisions+" layers="+body.gameObject.layer+","+cc.gameObject.layer+" ignored="+Physics.GetIgnoreCollision(body,cc)+" matrix="+Physics.GetIgnoreLayerCollision(body.gameObject.layer,cc.gameObject.layer));
            Assert.That(player.transform.position.z,Is.LessThan(body.bounds.min.z-.2f),"Player must stop in front of solid body");
        }
        [UnityTest]public IEnumerator VariantReferencesLargeNavigationAndBlockedDetection()
        {
            Assert.That(health.CurrentHealth,Is.EqualTo(1200));Assert.That(bot.GetComponent<Damageable>().Health,Is.SameAs(health));Assert.That(agent.isOnNavMesh,Is.True);Assert.That(agent.radius,Is.EqualTo(2.25f));Assert.That(agent.height,Is.EqualTo(3.05f));Assert.That(bot.transform.localScale.y*.861631f,Is.EqualTo(3).Within(.02));Assert.That(bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,Is.EqualTo(45));Assert.That(bot.GetComponentsInChildren<Component>(true).All(c=>c),Is.True);Assert.That(bot.Cannons.Length,Is.EqualTo(2));Assert.That(bot.Presentation.Animator.applyRootMotion,Is.False);
            Assert.That(enemy.KnownDetectedTarget,Is.Null,"Solid central cover must prevent acquiring player");Assert.That(mobile.AiState,Is.EqualTo(EnemyMobile.AIState.Patrol));Assert.That(bot.ShotsFired,Is.Zero);yield return new WaitForSeconds(.5f);Assert.That(enemy.KnownDetectedTarget,Is.Null);Assert.That(bot.ShotsFired,Is.Zero);
        }
        [UnityTest]public IEnumerator HealthBarTracksDamageAndPlayerBlocksMovingBody()
        {
            var bar=bot.GetComponent<Unity.FPS.UI.WorldspaceHealthBar>();Assert.That(bar,Is.Not.Null);Assert.That(bar.HideFullHealthBar,Is.False);yield return null;
            Assert.That(bar.HealthBarPivot.gameObject.activeInHierarchy,Is.True);Assert.That(bar.HealthBarImage.fillAmount,Is.EqualTo(1).Within(.001f));
            health.TakeDamage(300,null);yield return null;Assert.That(bar.HealthBarImage.fillAmount,Is.EqualTo(.75f).Within(.001f));
            MoveBot(new Vector3(-10,0,12));MovePlayer(new Vector3(-10,.03f,7.5f));enemy.enabled=false;mobile.enabled=false;agent.isStopped=false;
            yield return new WaitForSeconds(.15f);ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/BigBotCombat/health-and-cannons.png");yield return new WaitForSeconds(.15f);agent.SetDestination(player.transform.position);
            var body=bot.GetComponent<BoxCollider>();var cc=player.GetComponent<CharacterController>();float end=Time.time+2.5f;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));
            while(Time.time<end)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(Physics.ComputePenetration(body,body.transform.position,body.transform.rotation,cc,cc.transform.position,cc.transform.rotation,out _,out float distance)&&distance>.04f,Is.False,"Moving robot and walking player must remain solid");
                Assert.That(player.transform.position.z,Is.LessThan(bot.transform.position.z),"Player must not cross the robot");
            }
            InputSystem.QueueStateEvent(keys,new KeyboardState());
        }
        [UnityTest]public IEnumerator OrbitUsesBoundedAnimatedTurnAndDoesNotAccumulateArmTwist()
        {
            MoveBot(new Vector3(-10,0,12));MovePlayer(new Vector3(-10,.03f,5));agent.isStopped=true;
            var arm=bot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Forearm_R");var foot=bot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Foot_R");
            var localRest=arm.localRotation;bool sawTurn=false,capturedOrbit=false;float previous=bot.transform.eulerAngles.y;float start=Time.time;float maxTwist=0;float minFoot=float.PositiveInfinity,maxFoot=float.NegativeInfinity;
            while(Time.time-start<7)
            {
                float elapsed=Time.time-start;float angle=Mathf.Lerp(180,410,elapsed/7)*Mathf.Deg2Rad;
                MovePlayer(new Vector3(-10+Mathf.Sin(angle)*6,.03f,12+Mathf.Cos(angle)*6));yield return null;
                if(!capturedOrbit&&elapsed>3){ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/BigBotCombat/orbit-turn.png");capturedOrbit=true;}
                float current=bot.transform.eulerAngles.y;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(previous,current))/Mathf.Max(Time.deltaTime,.001f),Is.LessThan(70),"Body rotation must be bounded, including when target crosses behind");previous=current;
                var state=bot.Presentation.Animator.GetCurrentAnimatorStateInfo(0);sawTurn|=state.IsName("TurnLeft")||state.IsName("TurnRight");
                maxTwist=Mathf.Max(maxTwist,Quaternion.Angle(localRest,arm.localRotation));minFoot=Mathf.Min(minFoot,foot.position.y);maxFoot=Mathf.Max(maxFoot,foot.position.y);
            }
            Assert.That(sawTurn,Is.True,"Existing articulated turn clips must play while tracking");Assert.That(maxTwist,Is.LessThan(85),"No cumulative cannon rotation");Assert.That(maxFoot-minFoot,Is.GreaterThan(.00001f),"Feet must articulate during turn");
            health.Kill();yield return null;Assert.That(bot.GetComponent<Unity.FPS.UI.WorldspaceHealthBar>().HealthBarPivot.gameObject.activeSelf,Is.False);
        }
        [UnityTest]public IEnumerator DetectsPursuesAndRespectsLargeBodyAroundCover()
        {
            MovePlayer(new Vector3(-10,.03f,-8));var start=bot.transform.position;yield return Until(()=>enemy.IsSeeingTarget,2,"Player should be acquired on visible side of cover");yield return new WaitForSeconds(1.2f);Assert.That(Vector3.Distance(start,bot.transform.position),Is.GreaterThan(.6f));Assert.That(mobile.AiState,Is.EqualTo(EnemyMobile.AIState.Follow));var cover=GameObject.Find("LineOfSightCover").GetComponent<BoxCollider>();var body=bot.GetComponent<BoxCollider>();
            for(int i=0;i<28;i++){yield return new WaitForSeconds(.2f);Assert.That(Physics.ComputePenetration(body,body.transform.position,body.transform.rotation,cover,cover.transform.position,cover.transform.rotation,out _,out _),Is.False,"Full-size body must not enter wall");}
            yield return Until(()=>mobile.AiState==EnemyMobile.AIState.Attack,8,"Bot should stop at a valid firing distance");yield return Until(()=>agent.velocity.magnitude<.3f,5,"Bot must finish approaching its stop distance");Assert.That(enemy.IsSeeingTarget,Is.True);
        }
        [UnityTest]public IEnumerator LosLossCancelsWarningAndPursuesLastSeenInsteadOfFiringBlind()
        {
            MovePlayer(new Vector3(-8,.03f,3));yield return Until(()=>bot.IsPreparing,4,"Telegraph should start while target visible");int shots=bot.ShotsFired;var seen=bot.LastSeenPosition;Assert.That(bot.GetComponentsInChildren<ParticleSystem>(true).Any(p=>p.isEmitting),Is.True);
            MovePlayer(new Vector3(0,.03f,-9));yield return new WaitForSeconds(.2f);Assert.That(enemy.IsSeeingTarget,Is.False);Assert.That(bot.IsPreparing,Is.False);Assert.That(mobile.AiState,Is.EqualTo(EnemyMobile.AIState.Follow));Assert.That(Vector3.Distance(seen,bot.LastSeenPosition),Is.LessThan(.1f));yield return new WaitForSeconds(.8f);Assert.That(bot.ShotsFired,Is.EqualTo(shots),"No new shot through wall after vision is lost");Assert.That(bot.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.name=="CannonPreparation").All(p=>p.particleCount==0),Is.True);
        }
        [UnityTest]public IEnumerator PairedProjectilesLeaveBothMouthsDamagePlayerAndIgnoreOwner()
        {
            MoveBot(new Vector3(-10,0,12));MovePlayer(new Vector3(-10,.03f,2));float ownHealth=health.CurrentHealth;var guns=new List<string>();var shotTimes=new List<float>();float largestOriginError=0;float largestAngle=0;float firstShotAt=-1;float preparationAt=-1;
            bot.ProjectileSpawned+=p=>{var weapon=enemy.GetCurrentWeapon();guns.Add(weapon.name);shotTimes.Add(Time.time);largestOriginError=Mathf.Max(largestOriginError,Vector3.Distance(p.InitialPosition,weapon.WeaponMuzzle.position));largestAngle=Mathf.Max(largestAngle,Vector3.Angle(p.InitialDirection,weapon.WeaponMuzzle.forward));if(firstShotAt<0)firstShotAt=Time.time;Assert.That(p.GetComponent<ProjectileStandard>().Speed,Is.EqualTo(bot.ProjectileSpeed));Assert.That(p.GetComponent<ProjectileStandard>().Damage,Is.EqualTo(bot.Damage));};
            yield return Until(()=>bot.IsPreparing,3,"Prepare before damage");preparationAt=Time.time;float before=playerHealth.CurrentHealth;yield return Until(()=>playerHealth.CurrentHealth<before,4,"Existing projectile must damage real player Health");yield return Until(()=>guns.Count>=2,5,"Both actual cannon mouths must fire together");
            Assert.That(guns.Distinct().Count(),Is.EqualTo(2));Assert.That(shotTimes[1]-shotTimes[0],Is.LessThan(.002f),"Two projectiles in the same firing frame");Assert.That(largestOriginError,Is.LessThan(.001f));Assert.That(largestAngle,Is.LessThan(.1f));Assert.That(firstShotAt-preparationAt,Is.GreaterThan(bot.PreparationTime-.08f));Assert.That(health.CurrentHealth,Is.EqualTo(ownHealth),"Projectiles ignore every owner collider");
        }
        [UnityTest]public IEnumerator ProjectileCanBeDodgedAndMuzzleObstructionPreventsDischarge()
        {
            MoveBot(new Vector3(-10,0,12));MovePlayer(new Vector3(-10,.03f,2));ProjectileBase shot=null;bot.ProjectileSpawned+=p=>{if(!shot)shot=p;};yield return Until(()=>shot,4,"Expected a real projectile");var initial=shot.InitialDirection;float before=playerHealth.CurrentHealth;MovePlayer(new Vector3(-6.8f,.03f,2));yield return new WaitForSeconds(.7f);Assert.That(playerHealth.CurrentHealth,Is.EqualTo(before),"Side-step after launch should dodge snapshot aim");if(shot)Assert.That(Vector3.Angle(initial,shot.transform.forward),Is.LessThan(.1f),"Projectile is not homing");
            // Block the next selected mouth, leaving the central detector ray clear.
            agent.isStopped=true;MovePlayer(new Vector3(-10,.03f,2));yield return new WaitForSeconds(1);var weapon=enemy.GetCurrentWeapon();var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="MuzzleExitBlocker";blocker.transform.position=weapon.WeaponMuzzle.position+weapon.WeaponMuzzle.forward*.35f;blocker.transform.localScale=Vector3.one*.9f;Physics.SyncTransforms();int count=bot.ShotsFired;Assert.That(enemy.IsSeeingTarget,Is.True,"Only gun exit, not eye, is obstructed");yield return new WaitForSeconds(3);Assert.That(bot.ShotsFired,Is.EqualTo(count),"No shot when muzzle exit is blocked");Object.Destroy(blocker);
        }
        [UnityTest]public IEnumerator DeathDuringPreparationStopsAiEffectsAndProcessesKillOnce()
        {
            MovePlayer(new Vector3(-8,.03f,3));yield return Until(()=>bot.IsPreparing,4,"Expected windup to interrupt");int shots=bot.ShotsFired;int deaths=0;health.OnDie+=()=>deaths++;health.Kill();health.Kill();yield return null;
            Assert.That(bot.IsDead,Is.True);Assert.That(enemy.enabled,Is.False);Assert.That(mobile.enabled,Is.False);Assert.That(agent.enabled,Is.False);Assert.That(bot.IsPreparing,Is.False);Assert.That(bot.Presentation.Animator.enabled,Is.False);Assert.That(bot.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.particleCount==0),Is.True);Assert.That(Object.FindAnyObjectByType<EnemyManager>().NumberOfEnemiesRemaining,Is.Zero);Assert.That(deaths,Is.EqualTo(1));yield return new WaitForSeconds(.8f);Assert.That(bot.ShotsFired,Is.EqualTo(shots));
        }
        [UnityTest]public IEnumerator PlayerProjectileCanDamageBigBotThroughExistingDamageable()
        {
            MoveBot(new Vector3(-10,0,12));MovePlayer(new Vector3(-10,.03f,2));float before=health.CurrentHealth;Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState());yield return Until(()=>health.CurrentHealth<before,2,"Player's existing projectile must hit Big Bot Damageable");Assert.That(health.CurrentHealth,Is.GreaterThan(0));
        }
    }
}
