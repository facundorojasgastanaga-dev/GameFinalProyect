using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem
{
    // Reproducible lab workload, never added to a game scene. Original vs non-alloc AI query in one build.
    public sealed class CombatLoadProbe : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        public bool Complete { get; private set; }
        public string ResultPath { get; private set; }
        [Serializable] public sealed class Sample
        {
            public string mode;
            public int trial, frames, robots=16, initialLooseCannons=4, shots, collections, gpuSamples, rigidbodiesAfter, armPeak, burstPeak;
            public double frameMs, p95Ms, mainMs, renderMs, gpuMs, gcBytes, batches, drawCalls, setPass, triangles;
            public double detectionUs, detectionBytes;
        }
        [Serializable] public sealed class Result
        {
            public string environment, gpu, resolution, quality, unity;
            public int vsync, simulationHz=60;
            public string conditions="Same scene/camera/lighting, seed 9707, 16 animated robots with active AI/navigation, 4 initial loose cannons, scheduled shots and 4 cannon explosions + 4 deaths per trial. ABBA order. Only detection query implementation changes.";
            public Sample[] samples;
        }
        public void Configure(GameObject prefab)=>enemyPrefab=prefab;
        IEnumerator Start()
        {
            if(Application.isEditor)yield break;
            string path=Application.persistentDataPath+"/combat-performance.json";
            var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="--iron-output")path=args[i+1];
            yield return Run(path); Application.Quit();
        }
        public IEnumerator Run(string path)
        {
            Complete=false; ResultPath=path;
            int oldVsync=QualitySettings.vSyncCount, oldTarget=Application.targetFrameRate;
            float oldCapture=Time.captureDeltaTime;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;Time.timeScale=1;
            Time.captureDeltaTime=1f/60;
            yield return null;yield return null;
            var flow=Object.FindAnyObjectByType<GameFlowManager>();if(flow)flow.enabled=false;
            var objectives=Object.FindAnyObjectByType<ObjectiveManager>();if(objectives)objectives.enabled=false;
            var player=Object.FindAnyObjectByType<PlayerCharacterController>();player.GetComponent<Health>().Invincible=true;
            player.enabled=false;player.transform.position=new Vector3(0,.05f,-8);player.GetComponent<PlayerWeaponsManager>().enabled=false;
            player.PlayerCamera.transform.SetPositionAndRotation(new Vector3(12,7,-15),Quaternion.LookRotation(new Vector3(-12,-6,21)));
            var manager=Object.FindAnyObjectByType<EnemyManager>();
            foreach(var old in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {manager.UnregisterEnemy(old);Object.Destroy(old.gameObject);}
            yield return null;yield return null;
            var result=new Result{environment=Application.isEditor?"Editor":"Windows Player (development Mono)",gpu=SystemInfo.graphicsDeviceName,
                resolution=Screen.width+"x"+Screen.height,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],unity=Application.unityVersion,vsync=0};
            var samples=new List<Sample>();
            for(int trial=0;trial<4;trial++)
            {
                bool optimized=trial==1 || trial==2;UnityEngine.Random.InitState(9707);
                var bots=new EnemyController[16];var modules=new StolenCannon[16];
                for(int i=0;i<16;i++)
                {
                    bots[i]=Instantiate(enemyPrefab,new Vector3((i%4-1.5f)*3,.05f,2+i/4*3),Quaternion.Euler(0,180,0)).GetComponent<EnemyController>();
                    modules[i]=bots[i].GetComponentInChildren<StolenCannon>(true);
                }
                yield return null;yield return null;
                var detectors=bots.Select(b=>b.DetectionModule).ToArray();
                foreach(var detector in detectors)detector.UseNonAllocQueries=optimized;
                var actors=bots.Select(b=>b.GetComponent<Actor>()).ToArray();var self=bots.Select(b=>b.GetComponentsInChildren<Collider>()).ToArray();
                for(int i=0;i<16;i++)
                {
                    var hp=bots[i].GetComponent<Health>();hp.TakeDamage(hp.MaxHealth*(i%3==0?.26f:i%3==1?.51f:.76f),null);hp.Invincible=true;
                    if(i<4)bots[i].GetComponentInChildren<DetachableCannon>().Detach();
                }
                for(int i=0;i<75;i++)yield return null;
                var sample=new Sample{mode=optimized?"nonalloc":"original_RaycastAll",trial=trial,frames=240};
                var watch=new Stopwatch();
                for(int j=0;j<16;j++)detectors[j].HandleTargetDetection(actors[j],self[j]);
                long bytes=GC.GetAllocatedBytesForCurrentThread();watch.Start();
                for(int j=0;j<128;j++)for(int i=0;i<16;i++)detectors[i].HandleTargetDetection(actors[i],self[i]);
                watch.Stop();sample.detectionBytes=(GC.GetAllocatedBytesForCurrentThread()-bytes)/2048d;sample.detectionUs=watch.Elapsed.TotalMilliseconds*1000/2048;
                int collectionBefore=GC.CollectionCount(0);var elapsed=new double[240];var timing=new FrameTiming[1];double previous=Time.realtimeSinceStartupAsDouble;
                using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
                using(var render=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Render Thread",1))
                using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
                using(var batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
                using(var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
                using(var passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",1))
                using(var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
                {
                    for(int frame=0;frame<240;frame++)
                    {
                        if(frame%20==0)for(int i=4;i<16;i++)if(bots[i] && bots[i].TryAtack(player.transform.position+Vector3.up))sample.shots++;
                        if(frame==10)for(int i=0;i<4;i++)modules[i].Ammo.Drain(85);
                        if(frame==40)for(int i=0;i<4;i++)if(modules[i])modules[i].Explosion.TryDetonate();
                        if(frame==90)for(int i=4;i<8;i++)bots[i].GetComponent<Health>().Kill();
                        FrameTimingManager.CaptureFrameTimings();yield return null;
                        double now=Time.realtimeSinceStartupAsDouble;elapsed[frame]=(now-previous)*1000;previous=now;
                        sample.mainMs+=cpu.Valid?cpu.LastValue/1000000d:0;sample.renderMs+=render.Valid?render.LastValue/1000000d:0;
                        sample.gcBytes+=gc.Valid?gc.LastValue:0;sample.batches+=batches.Valid?batches.LastValue:0;
                        sample.drawCalls+=draws.Valid?draws.LastValue:0;sample.setPass+=passes.Valid?passes.LastValue:0;sample.triangles+=tris.Valid?tris.LastValue:0;
                        if(FrameTimingManager.GetLatestTimings(1,timing)>0 && timing[0].gpuFrameTime>0){sample.gpuMs+=timing[0].gpuFrameTime;sample.gpuSamples++;}
                        sample.armPeak=Mathf.Max(sample.armPeak,RobotDestructionPool.ArmsInUse);sample.burstPeak=Mathf.Max(sample.burstPeak,RobotDestructionPool.BurstsInUse);
                    }
                }
                sample.frameMs=elapsed.Average();Array.Sort(elapsed);sample.p95Ms=elapsed[228];
                sample.mainMs/=240;sample.renderMs/=240;sample.gcBytes/=240;sample.batches/=240;sample.drawCalls/=240;sample.setPass/=240;sample.triangles/=240;
                sample.gpuMs=sample.gpuSamples>0?sample.gpuMs/sample.gpuSamples:-1;sample.collections=GC.CollectionCount(0)-collectionBefore;
                sample.rigidbodiesAfter=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;samples.Add(sample);
                for(int i=0;i<16;i++)
                {
                    if(modules[i])Object.Destroy(modules[i].gameObject);
                    if(bots[i]){if(bots[i].GetComponent<Health>().CurrentHealth>0)manager.UnregisterEnemy(bots[i]);Object.Destroy(bots[i].gameObject);}
                }
                for(int j=0;j<210;j++)yield return null;
            }
            result.samples=samples.ToArray();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(result,true));
            QualitySettings.vSyncCount=oldVsync;Application.targetFrameRate=oldTarget;Time.captureDeltaTime=oldCapture;Complete=true;
        }
    }
}
