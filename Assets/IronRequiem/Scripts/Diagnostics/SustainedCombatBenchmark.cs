using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace IronRequiem
{
    // Fixture only: feeds the existing Input System and calls existing damage/detachment rules.
    // Never placed in MainScene. No actor pooling or gameplay replacements.
    public sealed class SustainedCombatBenchmark : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        EnemyController[] bots=new EnemyController[16]; StolenCannon[] modules=new StolenCannon[16];
        PlayerCharacterController player; PlayerWeaponsManager inventory; StolenWeaponCarrier carrier; EnemyManager enemies; PatrolPath path;
        Keyboard keys; Mouse mouse; CombatFrameCapture capture; string output; bool picked, thrown; int cycles=15; bool profiling; bool profileRequested; int action;
        readonly List<string> errors=new List<string>();
        static readonly KeyboardState Idle=new KeyboardState(), Forward=new KeyboardState(Key.W), Back=new KeyboardState(Key.S), Left=new KeyboardState(Key.A), Right=new KeyboardState(Key.D), Jump=new KeyboardState(Key.Space), Pickup=new KeyboardState(Key.F), Throw=new KeyboardState(Key.G);
        public void Configure(GameObject prefab)=>enemyPrefab=prefab;
        string Arg(string name,string fallback){var a=Environment.GetCommandLineArgs();for(int i=0;i<a.Length-1;i++)if(a[i]==name)return a[i+1];return fallback;}
        void Awake(){if(!Application.isEditor){var f=Object.FindAnyObjectByType<GameFlowManager>();if(f)f.enabled=false;var o=Object.FindAnyObjectByType<ObjectiveManager>();if(o)o.enabled=false;}}
        IEnumerator Start()
        {
            if(Application.isEditor)yield break;
            output=Arg("--iron-output",Application.persistentDataPath+"/Performance120/run");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); Application.logMessageReceived+=OnLog; int.TryParse(Arg("--iron-cycles","15"),out cycles);cycles=Mathf.Clamp(cycles,1,15);profileRequested=Debug.isDebugBuild&&Arg("--iron-profile","0")=="1";UnityEngine.Random.InitState(9707);InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;Time.captureDeltaTime=0;Time.timeScale=1;
            yield return null;yield return null;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);Display.main.Activate(0,0,0);Display.main.SetParams(1920,1080,0,0);yield return new WaitForSecondsRealtime(1);File.WriteAllText(output+"-display.txt","screen="+Screen.width+"x"+Screen.height+" display="+Display.main.systemWidth+"x"+Display.main.systemHeight+" mode="+Screen.fullScreenMode+" dpi="+Screen.dpi+" resolutions="+string.Join(";",Array.ConvertAll(Screen.resolutions,r=>r.ToString())));if(Screen.width!=1920||Screen.height!=1080){File.WriteAllText(output+".failed","Requested 1920x1080 but got "+Screen.width+"x"+Screen.height);Application.Quit(3);yield break;}
            try{Initialize();}catch(Exception e){File.WriteAllText(output+".failed",e.ToString());Application.Quit(2);yield break;}
            yield return null;yield return null;
            capture=new CombatFrameCapture(Time.realtimeSinceStartupAsDouble);
            yield return Wave(-1);
            capture.Save(output+"-warmup",errors.ToArray());capture.Dispose();capture=null;
            yield return new WaitForSecondsRealtime(2);
            errors.Clear();capture=new CombatFrameCapture(Time.realtimeSinceStartupAsDouble);
            for(int wave=0;wave<cycles;wave++)yield return Wave(wave);
            capture.Save(output,errors.ToArray());capture.Dispose();capture=null;
            InputSystem.QueueStateEvent(keys,Idle);InputSystem.QueueStateEvent(mouse,new MouseState());
            File.WriteAllText(output+".done","Completed warmup and requested measured cycles; see JSON for duration, event counts and missing counters.");
            yield return null;Application.Quit(errors.Count==0?0:2);
        }
        void Initialize()
        {
            var flow=Object.FindAnyObjectByType<GameFlowManager>();if(flow)flow.enabled=false;
            var objectives=Object.FindAnyObjectByType<ObjectiveManager>();if(objectives)objectives.enabled=false;
            player=Object.FindAnyObjectByType<PlayerCharacterController>();inventory=player.GetComponent<PlayerWeaponsManager>();carrier=player.GetComponent<StolenWeaponCarrier>();
            if(!carrier)throw new InvalidOperationException("Existing stolen weapon components missing from benchmark player.");
            player.GetComponent<Health>().Invincible=true;enemies=Object.FindAnyObjectByType<EnemyManager>();path=Object.FindAnyObjectByType<PatrolPath>();
            foreach(var old in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)){enemies.UnregisterEnemy(old);Destroy(old.gameObject);}
            keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();
            InputSystem.actions.devices=new InputDevice[]{keys,mouse};Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            inventory.OnAddedWeapon+=OnAdded;
            for(int i=0;i<9;i++){var w=inventory.GetWeaponAtSlotIndex(i);if(w)OnAdded(w,i);}
        }
        void OnAdded(WeaponController weapon,int slot){weapon.OnShootProcessed+=OnPlayerShot;}
        void OnPlayerShot(){if(capture==null)return;capture.Result.playerShots++;if(inventory.GetActiveWeapon().GetComponent<CannonChargeShot>())capture.Result.chargeShots++;}
        void OnEnemyShot(){if(capture!=null)capture.Result.enemyShots++;}
        void OnEnemyDeath(){if(capture!=null)capture.Result.deaths++;}
        void OnExplosion(){if(capture!=null)capture.Result.cannonExplosions++;}
        void OnLog(string message,string stack,LogType type)
        {if((type==LogType.Exception||type==LogType.Error)&&errors.Count<24 && !errors.Contains(message))errors.Add(message+"\n"+stack);}
        IEnumerator Wave(int cycle)
        {
            double start=Time.realtimeSinceStartupAsDouble;action=1;picked=thrown=false;
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(new Vector3(0,.05f,-8),Quaternion.identity);cc.enabled=true;
            player.PlayerCamera.transform.localRotation=Quaternion.identity;
            for(int i=0;i<16;i++)
            {
                bots[i]=Instantiate(enemyPrefab,new Vector3((i%4-1.5f)*3,.05f,2+(i/4)*3),Quaternion.Euler(0,180,0)).GetComponent<EnemyController>();
                bots[i].PatrolPath=path;bots[i].onAttack+=OnEnemyShot;bots[i].GetComponent<Health>().OnDie+=OnEnemyDeath;
                modules[i]=bots[i].GetComponentInChildren<StolenCannon>(true);modules[i].Explosion.Detonated+=OnExplosion;
            }
            capture.Result.waves++;
            bool photographed=false;bool initialized=false,detached=false,placed=false,critical=false,exploded=false,damaged75=false,damaged50=false,damaged25=false,dead1=false,dead2=false,clean=false;
            while(Time.realtimeSinceStartupAsDouble-start<20)
            {
                yield return null;
                double t=Time.realtimeSinceStartupAsDouble-start;action=0;if(cycle<0&&profileRequested){if(t>=11.8&&t<16.8&&!profiling){UnityEngine.Profiling.Profiler.logFile=output+"-warmup.raw";UnityEngine.Profiling.Profiler.enableBinaryLog=true;UnityEngine.Profiling.Profiler.enabled=true;profiling=true;}if(t>=16.8&&profiling){UnityEngine.Profiling.Profiler.enabled=false;UnityEngine.Profiling.Profiler.enableBinaryLog=false;profiling=false;profileRequested=false;}}
                if(!initialized){int onNav=0;foreach(var b in bots)if(b){b.GetComponent<Health>().Invincible=true;if(b.NavMeshAgent&&b.NavMeshAgent.isOnNavMesh)onNav++;}if(onNav!=16)errors.Add("Expected 16 active NavMeshAgents, got "+onNav);initialized=true;action=1;}
                if(t>=2&&!detached){for(int i=0;i<4;i++)bots[i].GetComponentInChildren<DetachableCannon>().Detach();detached=true;action=2;}
                if(t>=3.1&&!placed)
                {
                    // Position the physical fixture within interaction range, without bypassing pickup.
                    var m=modules[0];m.Body.isKinematic=true;Vector3 center=player.PlayerCamera.transform.position+Vector3.forward*1.2f;
                    m.transform.position+=center-m.SolidCollider.bounds.center;Physics.SyncTransforms();placed=true;action=3;
                }
                if(t>=3.1&&t<4.2 && modules[0])player.PlayerCamera.transform.LookAt(modules[0].SolidCollider.bounds.center);
                else player.PlayerCamera.transform.rotation=Quaternion.Euler(0,Mathf.Sin((float)t*.3f)*18,0);
                if(!picked&&carrier.Held){picked=true;capture.Result.pickups++;action=4;}
                if(t>=6.3&&!critical){if(modules[0])modules[0].Ammo.Drain(85);critical=true;action=5;}
                if(picked&&!thrown&&t>6.9&&!carrier.Held){thrown=true;capture.Result.throws++;}
                if(t>=7.2&&!exploded){for(int i=1;i<4;i++)if(modules[i]){modules[i].Ammo.Drain(85);modules[i].Explosion.TryDetonate();}exploded=true;action=6;}
                if(t>=8&&!damaged75){DamageStage(.73f);damaged75=true;action=7;}
                if(t>=10&&!damaged50){DamageStage(.48f);damaged50=true;action=8;}
                if(t>=12&&!damaged25){DamageStage(.23f);damaged25=true;action=9;}
                if(t>=14&&!dead1){for(int i=4;i<8;i++)if(bots[i])bots[i].GetComponent<Health>().Kill();dead1=true;action=10;}
                if(t>=16&&!dead2){for(int i=8;i<12;i++)if(bots[i])bots[i].GetComponent<Health>().Kill();dead2=true;action=11;}
                if(t>=18&&!clean){Cleanup();clean=true;action=12;}
                Cursor.lockState=CursorLockMode.Locked;if(cycle<0&&t>=9&&!photographed){ScreenCapture.CaptureScreenshot(output+"-view.png");photographed=true;}KeyboardState state=Idle;
                if(t<.3)state=Forward;else if(t<.6)state=Back;else if(t<.9)state=Left;else if(t<1.2)state=Right;else if(t<1.3)state=Jump;
                if(t>=3.6&&t<3.8)state=Pickup;if(t>=6.8&&t<7)state=Throw;
                bool fire=t>=4.6&&t<6.05 || t>=.5&&t<2 && t% .24 < .1;
                InputSystem.QueueStateEvent(keys,state);InputSystem.QueueStateEvent(mouse,fire?new MouseState().WithButton(MouseButton.Left):new MouseState());
                capture.Tick((cycle<0?0:cycle*20)+Math.Min(20,t),cycle,action);
            }
            Cleanup();
        }
        void DamageStage(float targetRatio)
        {
            for(int i=4;i<16;i++)if(bots[i]){var h=bots[i].GetComponent<Health>();h.Invincible=false;h.TakeDamage(Mathf.Max(0,h.CurrentHealth-h.MaxHealth*targetRatio),null);h.Invincible=true;}
        }
        void Cleanup()
        {
            for(int i=0;i<16;i++)
            {
                if(modules[i])Destroy(modules[i].gameObject);
                if(bots[i]){if(bots[i].GetComponent<Health>().CurrentHealth>0)enemies.UnregisterEnemy(bots[i]);Destroy(bots[i].gameObject);}
                bots[i]=null;modules[i]=null;
            }
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;if(capture!=null)capture.Dispose();}
    }
}
