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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace IronRequiem.Tests
{
    public sealed class BigBotDestructionTests
    {
        BigBotCombat bot;BigBotDestruction damage;Health health,playerHealth;EnemyController enemy;PlayerCharacterController player;Keyboard keyboard;Mouse mouse;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? oldDevices;
        const string Lab="Assets/IronRequiem/Scenes/BigBotDestructionLab.unity";
        [UnitySetUp]public IEnumerator Setup()
        {
            Time.timeScale=1;Application.runInBackground=true;oldDevices=InputSystem.actions.devices;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};keyboard.MakeCurrent();mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode(Lab,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            bot=Object.FindAnyObjectByType<BigBotCombat>();damage=bot.GetComponent<BigBotDestruction>();health=bot.GetComponent<Health>();enemy=bot.GetComponent<EnemyController>();enemy.PatrolPath=null;player=Object.FindAnyObjectByType<PlayerCharacterController>();playerHealth=player.GetComponent<Health>();playerHealth.MaxHealth=1000;playerHealth.CurrentHealth=1000;
            enemy.NavMeshAgent.Warp(new Vector3(0,0,10));bot.transform.rotation=Quaternion.Euler(0,180,0);MovePlayer(new Vector3(0,.03f,-12));enemy.DetectionModule.KnownTargetTimeout=0;yield return new WaitForSeconds(.15f);enemy.DetectionModule.KnownTargetTimeout=8;yield return new WaitForSeconds(.15f);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();Cursor.lockState=CursorLockMode.Locked;
        }
        void MovePlayer(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
        void Reveal(){enemy.NavMeshAgent.Warp(new Vector3(-10,0,12));bot.transform.rotation=Quaternion.Euler(0,180,0);MovePlayer(new Vector3(-10,.03f,4));}
        IEnumerator Until(System.Func<bool> f,float timeout,string why){float end=Time.time+timeout;while(!f()&&Time.time<end)yield return null;Assert.That(f(),Is.True,why);}
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("CleanupDestruction"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(old);InputSystem.actions.devices=oldDevices;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;yield return null;}
        Bounds CannonBounds(bool detached)
        {
            Bounds result=default;bool first=true;var baked=new Mesh();
            if(!detached)foreach(var view in damage.CannonViews.OfType<SkinnedMeshRenderer>().Where(r=>r.name.EndsWith("LOD0")))
            {view.BakeMesh(baked,false);var scale=view.transform.lossyScale;foreach(var v in baked.vertices){var p=view.transform.TransformPoint(new Vector3(v.x/scale.x,v.y/scale.y,v.z/scale.z));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}}
            else foreach(var view in damage.FallenCannon.GetComponentsInChildren<MeshFilter>(true).Where(r=>r.name.EndsWith("LOD0")))foreach(var v in view.sharedMesh.vertices){var p=view.transform.TransformPoint(v);if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}
            Object.Destroy(baked);return result;
        }
        [UnityTest]public IEnumerator MountsStayAttachedAndCriticalSparksAreVisible()
        {
            Reveal();enemy.enabled=false;bot.GetComponent<EnemyMobile>().enabled=false;enemy.NavMeshAgent.isStopped=true;health.TakeDamage(600,null);yield return null;
            foreach(var side in new[]{"L","R"})foreach(var skin in bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Shoulder_"+side+"_LOD")||r.name.StartsWith("Forearm_"+side+"_LOD")))Assert.That(skin.gameObject.activeSelf,Is.True,"Do not remove the support under a surviving cannon");
            Assert.That(damage.DetachedArmorCount,Is.EqualTo(1));health.TakeDamage(310,null);var electric=bot.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="DamageElectrical");var broken=bot.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="CannonBreak");int peak=0;
            for(int i=0;i<22;i++){yield return new WaitForSeconds(.05f);peak=Mathf.Max(peak,electric.particleCount+broken.particleCount);}
            Assert.That(peak,Is.GreaterThanOrEqualTo(8),"Readable electrical discharge at critical health");Assert.That(electric.GetComponent<ParticleSystemRenderer>().renderMode,Is.EqualTo(ParticleSystemRenderMode.Stretch));Assert.That(bot.GetComponentsInChildren<Light>(true).Length,Is.Zero);
            foreach(var skin in bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Shoulder_")||r.name.StartsWith("Forearm_L")))Assert.That(skin.gameObject.activeSelf,Is.True);
            yield return Until(()=>electric.particleCount>=8,1.2f,"Capture a critical discharge without projectiles");ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/BigBotDestruction/critical-sparks-and-mount.png");yield return new WaitForEndOfFrame();yield return null;health.Kill();yield return new WaitForSeconds(.6f);Assert.That(electric.particleCount,Is.Zero);
        }
        [UnityTest]public IEnumerator CloseCameraRendersEveryApproachAngle()
        {
            Reveal();enemy.enabled=false;bot.GetComponent<EnemyMobile>().enabled=false;enemy.NavMeshAgent.isStopped=true;
            var views=bot.GetComponentsInChildren<SkinnedMeshRenderer>(true);foreach(var view in views)view.gameObject.layer=2;
            var go=new GameObject("BigBotVisibilityCamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(player.PlayerCamera);camera.enabled=false;camera.cullingMask=1<<2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.magenta;camera.fieldOfView=80;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.allowHDR=false;camera.allowMSAA=false;camera.aspect=1;var additional=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();additional.renderPostProcessing=false;
            var target=new RenderTexture(192,192,24);var texture=new Texture2D(192,192,TextureFormat.RGB24,false);camera.targetTexture=target;var log=new List<string>();
            for(int stage=0;stage<3;stage++)
            {
                if(stage==1)health.TakeDamage(600,null);if(stage==2)health.TakeDamage(310,null);
                foreach(float distance in new[]{18f,7f,3f,2.2f,1.45f})for(int side=0;side<(distance<2?1:8);side++)
                {
                    float angle=(180+side*45)*Mathf.Deg2Rad;var center=bot.transform.position+Vector3.up*1.6f;camera.transform.position=center+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*distance;camera.transform.LookAt(center);yield return null;camera.Render();var active=RenderTexture.active;RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,192,192),0,0);texture.Apply();RenderTexture.active=active;
                    int pixels=texture.GetPixels32().Count(p=>!(p.r>230&&p.g<30&&p.b>230));log.Add(stage+","+distance+","+side+","+pixels);Assert.That(pixels,Is.GreaterThan(80),"Robot vanished: stage="+stage+" distance="+distance+" angle="+side);
                    if(stage==2&&distance==2.2f&&side==0)System.IO.File.WriteAllBytes("Tools/IronRequiem/Reports/BigBotDestruction/close-critical-render.png",texture.EncodeToPNG());
                }
            }
            System.IO.File.WriteAllLines("Tools/IronRequiem/Reports/BigBotDestruction/visibility-pixels.csv",log);camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(texture);Object.Destroy(go);
        }
        [UnityTest]public IEnumerator TurnStepsFollowAngularDistance()
        {
            Reveal();enemy.NavMeshAgent.isStopped=true;var foot=bot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Foot_L");float start=Time.time;Vector3 previous=foot.position;float previousPhase=0,previousYaw=bot.transform.eulerAngles.y;float stanceDistance=0,stanceTime=0,minY=100,maxY=-100;bool stepped=false;
            while(Time.time-start<7)
            {
                float elapsed=Time.time-start;float angle=(180+elapsed*30)*Mathf.Deg2Rad;MovePlayer(new Vector3(-10+Mathf.Sin(angle)*7,.03f,12+Mathf.Cos(angle)*7));player.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(bot.transform.position-player.transform.position,Vector3.up));yield return new WaitForEndOfFrame();
                var animator=bot.Presentation.Animator;var clips=animator.GetCurrentAnimatorClipInfo(0);bool turn=clips.Any(c=>c.clip.name.StartsWith("BigBot_StepTurn"));stepped|=turn;float phase=bot.TurnPhase;float yaw=bot.transform.eulerAngles.y;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(previousYaw,yaw))/Mathf.Max(Time.deltaTime,.001f),Is.LessThan(70));previousYaw=yaw;
                if(turn&&!animator.IsInTransition(0)&&elapsed>1&&phase>.12f&&phase<.48f&&previousPhase>.12f&&previousPhase<.48f){stanceDistance+=Vector3.ProjectOnPlane(foot.position-previous,Vector3.up).magnitude;stanceTime+=Time.deltaTime;}
                minY=Mathf.Min(minY,foot.position.y);maxY=Mathf.Max(maxY,foot.position.y);previous=foot.position;previousPhase=phase;
                if(elapsed>3&&elapsed<3.1f)ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/BigBotDestruction/step-turn.png");
            }
            Assert.That(stepped,Is.True);Assert.That(stanceTime,Is.GreaterThan(.3f));Assert.That(stanceDistance/stanceTime,Is.LessThan(.18f),"Foot should plant while chassis turns");Assert.That(maxY-minY,Is.GreaterThan(.035f));System.IO.File.WriteAllText("Tools/IronRequiem/Reports/BigBotDestruction/turn-foot-slip.txt","Mean stance slip="+stanceDistance/stanceTime+" m/s; observed stance="+stanceTime+" s");
        }
        [UnityTest]public IEnumerator GradualThresholdsAreOnceOnlyAndPersistAcrossAllLods()
        {
            Assert.That(health.MaxHealth,Is.EqualTo(1200));Assert.That(bot.ActiveCannonCount,Is.EqualTo(2));health.TakeDamage(300,null);Assert.That(damage.Stage,Is.EqualTo(1));Assert.That(damage.DetachedArmorCount,Is.Zero);
            health.TakeDamage(300,null);Assert.That(damage.Stage,Is.EqualTo(2));Assert.That(damage.DetachedArmorCount,Is.EqualTo(1));var attachedBounds=CannonBounds(false);health.TakeDamage(300,null);health.TakeDamage(1,null);health.TakeDamage(1,null);
            Assert.That(damage.TransitionCount,Is.EqualTo(3));Assert.That(damage.TransitionMask,Is.EqualTo(7));Assert.That(damage.CannonDropCount,Is.EqualTo(1));Assert.That(damage.DetachedArmorCount,Is.EqualTo(1));Assert.That(bot.IsEmergency,Is.True);
            var droppedBounds=CannonBounds(true);Assert.That(Vector3.Distance(attachedBounds.min,droppedBounds.min)+Vector3.Distance(attachedBounds.max,droppedBounds.max),Is.LessThan(.025f),"Detached pieces preserve actual world pose and scale");
            var lod=bot.GetComponent<LODGroup>();for(int i=0;i<3;i++){lod.ForceLOD(i);yield return null;Assert.That(damage.CannonViews.All(v=>!v.gameObject.activeSelf),Is.True);Assert.That(damage.Armor.All(p=>p.Views.All(v=>!v.gameObject.activeSelf)),Is.True);}lod.ForceLOD(-1);
            Assert.That(damage.FallenCannon.GetComponent<Collider>().bounds.size.magnitude,Is.LessThan(4f),"Real 3 m robot module must not inherit FBX scale twice");Assert.That(damage.FallenCannon.GetComponent<LODGroup>().GetLODs().Length,Is.EqualTo(3));Assert.That(damage.FallenCannon.GetComponentsInChildren<SkinnedMeshRenderer>().Length,Is.Zero);Assert.That(damage.FallenCannon.GetComponentsInChildren<WeaponController>().Length,Is.Zero);
        }
        [UnityTest]public IEnumerator MassiveDamageCrossesThreeStagesWithoutDuplicateDrop()
        {
            health.TakeDamage(1000,null);Assert.That(damage.Stage,Is.EqualTo(3));Assert.That(damage.TransitionCount,Is.EqualTo(3));Assert.That(damage.CannonDropCount,Is.EqualTo(1));var remnant=damage.FallenCannon;Assert.That(damage.DetachCannon(),Is.False);Assert.That(bot.LoseCannon(0),Is.False);health.TakeDamage(10,null);Assert.That(damage.FallenCannon,Is.SameAs(remnant));Assert.That(damage.DetachedArmorCount,Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator LethalFromFullLifeProcessesDeathAndDropOnceWithoutDamageExplosion()
        {
            float before=playerHealth.CurrentHealth;int deaths=0;health.OnDie+=()=>deaths++;health.TakeDamage(5000,null);health.Kill();Assert.That(deaths,Is.EqualTo(1));Assert.That(damage.DeathCount,Is.EqualTo(1));Assert.That(damage.TransitionMask,Is.EqualTo(7));Assert.That(damage.CannonDropCount,Is.EqualTo(1));Assert.That(enemy.enabled,Is.False);Assert.That(enemy.NavMeshAgent.enabled,Is.False);Assert.That(bot.IsPreparing,Is.False);
            yield return new WaitForSeconds(.4f);Assert.That(bot.Presentation.Animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),Is.True);Assert.That(playerHealth.CurrentHealth,Is.EqualTo(before));Assert.That(bot.GetComponentsInChildren<DamageArea>(true).Length,Is.Zero);Assert.That(damage.FallenCannon.GetComponentsInChildren<DamageArea>(true).Length,Is.Zero);
        }
        [UnityTest]public IEnumerator EarlyCannonLossKeepsRemainingWeaponAndIncreasesMeasuredCadence()
        {
            Reveal();var normalTimes=new List<float>();var emergencyTimes=new List<float>();var sides=new List<int>();
            bot.ProjectileSpawned+=p=>{int side=Vector3.Distance(p.InitialPosition,bot.Cannons[0].WeaponMuzzle.position)<.01f?0:1;sides.Add(side);var times=bot.IsEmergency?emergencyTimes:normalTimes;if(times.Count==0||Time.time-times[times.Count-1]>.1f)times.Add(Time.time);if(bot.IsEmergency)Assert.That(side,Is.EqualTo(1),"No projectile may come from the lost right mouth");};
            yield return Until(()=>normalTimes.Count>=2,8,"Two normal paired volleys");Assert.That(sides.Contains(0)&&sides.Contains(1),Is.True);float normal=normalTimes[1]-normalTimes[0];
            Assert.That(bot.LoseCannon(0),Is.True);Assert.That(damage.CannonDetached,Is.True);Assert.That(damage.CannonDropCount,Is.EqualTo(1));Assert.That(health.CurrentHealth,Is.EqualTo(1200));Assert.That(bot.IsEmergency,Is.True);Assert.That(bot.EmergencyCadenceMultiplier,Is.EqualTo(1.5f));Assert.That(bot.LoseCannon(0),Is.False);
            yield return Until(()=>emergencyTimes.Count>=2,7,"Two emergency shots from remaining module");float faster=emergencyTimes[1]-emergencyTimes[0];Assert.That(faster,Is.LessThan(normal*.84f));health.TakeDamage(910,null);Assert.That(damage.CannonDropCount,Is.EqualTo(1));Assert.That(bot.ActiveCannonCount,Is.EqualTo(1));
            Assert.That(bot.Cannons[0].GetComponentsInChildren<ParticleSystem>(true).All(p=>!p.isEmitting&&p.particleCount==0),Is.True);System.IO.File.WriteAllText("Tools/IronRequiem/Reports/BigBotDestruction/cadence.txt","normal volley interval="+normal+" s; emergency interval="+faster+" s; preparation="+bot.PreparationTime+" s");
        }
        [UnityTest]public IEnumerator EmergencyPreservesLosAndDeathInterruptsTheRemainingAttack()
        {
            damage.DetachCannon();MovePlayer(new Vector3(-8,.03f,3));yield return Until(()=>bot.IsPreparing,5,"Remaining cannon still prepares");int shots=bot.ShotsFired;MovePlayer(new Vector3(0,.03f,-9));yield return new WaitForSeconds(.3f);Assert.That(bot.IsPreparing,Is.False);yield return new WaitForSeconds(.6f);Assert.That(bot.ShotsFired,Is.EqualTo(shots));
            MovePlayer(new Vector3(-8,.03f,3));yield return Until(()=>bot.IsPreparing,5,"Prepare after reacquisition");health.Kill();Assert.That(bot.IsDead,Is.True);Assert.That(enemy.NavMeshAgent.enabled,Is.False);Assert.That(bot.GetComponent<EnemyMobile>().enabled,Is.False);int drops=damage.CannonDropCount;yield return new WaitForSeconds(1);Assert.That(bot.ShotsFired,Is.EqualTo(shots));Assert.That(damage.CannonDropCount,Is.EqualTo(drops));Assert.That(damage.FallenCannon,Is.Not.Null);
        }
        [UnityTest]public IEnumerator HeavyDeathArticulatesStaysAboveFloorAndUsesExistingCleanup()
        {
            Reveal();yield return Until(()=>bot.IsPreparing,4,"Kill during actual preparation");var position=bot.transform.position;var torso=bot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Torso");var start=torso.position;health.Kill();var root=bot.transform.position;var baked=new Mesh();var groundLog=new List<string>();float min=float.PositiveInfinity;
            for(int frame=0;frame<19;frame++)
            {
                yield return new WaitForSeconds(.1f);Assert.That(Vector3.Distance(bot.transform.position,root),Is.LessThan(.01f),"Nav root cannot jump during collapse");
                foreach(var skin in bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.gameObject.activeInHierarchy)){skin.BakeMesh(baked,false);foreach(var v in baked.vertices){var s=skin.transform.lossyScale;min=Mathf.Min(min,skin.transform.TransformPoint(new Vector3(v.x/s.x,v.y/s.y,v.z/s.z)).y);}}
                groundLog.Add(frame+" min="+min+" visual="+bot.Presentation.transform.position+" root="+bot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Root").position);
                if(frame==4||frame==14)ScreenCapture.CaptureScreenshot("Tools/IronRequiem/Reports/BigBotDestruction/death-"+frame+".png");
            }
            System.IO.File.WriteAllLines("Tools/IronRequiem/Reports/BigBotDestruction/death-ground-runtime.txt",groundLog);Object.Destroy(baked);Assert.That(start.y-torso.position.y,Is.GreaterThan(.45f),"Large torso must actually collapse");Assert.That(min,Is.GreaterThan(-.025f),"All surviving LOD geometry must stay above floor");var fallen=damage.FallenCannon;yield return new WaitForSeconds(.8f);Assert.That(bot==null,Is.True,"Existing 2.5 second EnemyController cleanup");Assert.That(fallen,Is.Not.Null,"Body cleanup cannot delete detached cannon");
        }
        [UnityTest]public IEnumerator SimultaneousDeathsRespectBudgetsAndCannonPoolResetsOnReuse()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/BigBot_Destructible_v01.prefab");var clones=new List<GameObject>();for(int i=0;i<6;i++)clones.Add(Object.Instantiate(prefab,new Vector3(-12+(i%3)*6,.04f,11+(i/3)*6),Quaternion.identity));yield return new WaitForSeconds(.2f);
            foreach(var go in clones)go.GetComponent<Health>().Kill();health.Kill();yield return new WaitForSeconds(.5f);Assert.That(BigBotCannonDebris.InUseCount,Is.LessThanOrEqualTo(8));Assert.That(RobotDestructionPool.ArmorInUse,Is.LessThanOrEqualTo(16));
            foreach(var go in clones)Assert.That(go.GetComponent<BigBotDestruction>().DeathCount,Is.EqualTo(1));foreach(var debris in Object.FindObjectsByType<BigBotCannonDebris>(FindObjectsInactive.Include))debris.Return();Assert.That(BigBotCannonDebris.InUseCount,Is.Zero);
            var anchor=new GameObject("DebrisReuseAnchor");anchor.transform.position=new Vector3(-10,2,12);anchor.transform.localScale=Vector3.one*348;var reused=BigBotCannonDebris.Spawn(anchor.transform,.2f,new Collider[0]);Assert.That(reused.InUse,Is.True);yield return new WaitForSeconds(.4f);Assert.That(reused.InUse,Is.False);Assert.That(BigBotCannonDebris.InUseCount,Is.Zero);
        }
    }
}
