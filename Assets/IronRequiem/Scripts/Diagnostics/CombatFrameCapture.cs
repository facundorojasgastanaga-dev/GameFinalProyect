using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace IronRequiem
{
    // Bounded storage; CSV formatting and statistics happen after measurement.
    public sealed class CombatFrameCapture : IDisposable
    {
        public struct Row
        {
            public double time, frame, cpu, gpu, main, render, present, gc, gcPause;
            public long ram, managed; public int cycle, action, collections;
        }
        [Serializable] public sealed class Distribution
        {
            public int samples, overBudget, over33, over50, over100;
            public double mean, median, p95, p99, maximum, overBudgetPercent, fps;
        }
        [Serializable] public sealed class Summary
        {
            public string environment, resolution, quality, graphicsApi, gpu, cpu, unity;
            public int systemRamMB, gpuMemoryMB, vsync, targetFps, sampleCount, invalidResolutionFrames;
            public double measuredSeconds, startupSeconds;
            public bool gcAllocationCounter, gcPauseCounter;
            public string caveats="GC collection count sums generation counters, not distinct pauses. FrameTiming CPU/GPU samples are asynchronous; absent/duplicate timings omitted. RAM is Unity allocated memory, process RSS and global GPU memory are sampled externally. No per-process VRAM counter is available under WDDM. Release has no development/profiler build flags, only this benchmark's lightweight telemetry.";
            public Distribution frame, cpuFrame, gpuFrame, mainThread, renderThread;
            public long unityMemoryStart, unityMemoryEnd, unityMemoryPeak, managedStart, managedEnd;
            public int gcCollections, maxArms, maxBursts, waves, pickups, throws, chargeShots, playerShots, enemyShots, deaths, cannonExplosions;
            public string[] errors;
        }
        readonly Row[] rows=new Row[300000];
        readonly FrameTiming[] timing=new FrameTiming[1];
        readonly ProfilerRecorder gc, pause;
        int count; double previous, stamp=-1; int collections;
        long memory, managed; double nextMemory;
        public readonly Summary Result=new Summary();
        public CombatFrameCapture(double startup)
        {
            gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            pause=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Collect",1);
            Result.environment=Debug.isDebugBuild?"Windows Development Mono":"Windows Release Mono";
            Result.resolution=Screen.width+"x"+Screen.height;Result.quality=QualitySettings.names[QualitySettings.GetQualityLevel()];
            Result.graphicsApi=SystemInfo.graphicsDeviceType.ToString();Result.gpu=SystemInfo.graphicsDeviceName;Result.cpu=SystemInfo.processorType;
            Result.systemRamMB=SystemInfo.systemMemorySize;Result.gpuMemoryMB=SystemInfo.graphicsMemorySize;Result.unity=Application.unityVersion;
            Result.vsync=QualitySettings.vSyncCount;Result.targetFps=Application.targetFrameRate;Result.startupSeconds=startup;
            previous=Time.realtimeSinceStartupAsDouble;collections=CollectionCount();
            memory=Profiler.GetTotalAllocatedMemoryLong();managed=GC.GetTotalMemory(false);
            Result.unityMemoryStart=memory;Result.managedStart=managed;
        }
        static int CollectionCount()=>GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2);
        public void Tick(double time,int cycle,int action)
        {
            double now=Time.realtimeSinceStartupAsDouble;if(Screen.width!=1920||Screen.height!=1080)Result.invalidResolutionFrames++;
            if(count==rows.Length)throw new InvalidOperationException("Benchmark capture capacity exceeded; no samples silently discarded.");
            if(time>=nextMemory){memory=Profiler.GetTotalAllocatedMemoryLong();managed=GC.GetTotalMemory(false);nextMemory=time+1;}
            var row=new Row{time=time,frame=(now-previous)*1000,cpu=-1,gpu=-1,main=-1,render=-1,present=-1,cycle=cycle,action=action,ram=memory,managed=managed};previous=now;
            int current=CollectionCount();row.collections=current-collections;collections=current;
            Result.gcCollections+=row.collections;Result.unityMemoryPeak=Math.Max(Result.unityMemoryPeak,memory);
            if(gc.Valid){row.gc=gc.LastValue;Result.gcAllocationCounter=true;}else row.gc=-1;
            if(pause.Valid){row.gcPause=pause.LastValue/1000000d;Result.gcPauseCounter=true;}else row.gcPause=-1;
            if(FrameTimingManager.GetLatestTimings(1,timing)>0 && timing[0].frameStartTimestamp!=stamp)
            {
                stamp=timing[0].frameStartTimestamp;var t=timing[0];
                row.cpu=t.cpuFrameTime>0?t.cpuFrameTime:-1;row.gpu=t.gpuFrameTime>0?t.gpuFrameTime:-1;
                row.main=t.cpuMainThreadFrameTime>0?t.cpuMainThreadFrameTime:-1;row.render=t.cpuRenderThreadFrameTime>0?t.cpuRenderThreadFrameTime:-1;
                row.present=t.cpuMainThreadPresentWaitTime;
            }
            rows[count++]=row;Result.maxArms=Math.Max(Result.maxArms,RobotDestructionPool.ArmsInUse);Result.maxBursts=Math.Max(Result.maxBursts,RobotDestructionPool.BurstsInUse);
            FrameTimingManager.CaptureFrameTimings();
        }
        public void Save(string prefix,string[] errors)
        {
            Result.sampleCount=count;Result.measuredSeconds=rows.Take(count).Sum(r=>r.frame)/1000;
            Result.unityMemoryEnd=Profiler.GetTotalAllocatedMemoryLong();Result.managedEnd=GC.GetTotalMemory(false);Result.errors=errors;
            var used=rows.Take(count).ToArray();
            Result.frame=Stats(used.Select(r=>r.frame));Result.cpuFrame=Stats(used.Select(r=>r.cpu));Result.gpuFrame=Stats(used.Select(r=>r.gpu));
            Result.mainThread=Stats(used.Select(r=>r.main));Result.renderThread=Stats(used.Select(r=>r.render));
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            using(var file=new StreamWriter(prefix+".csv"))
            {
                file.WriteLine("seconds,frame_ms,cpu_ms,gpu_ms,main_ms,render_ms,present_wait_ms,gc_bytes,gc_pause_ms,unity_ram_bytes,managed_bytes,cycle,action,gc_collection_count_delta");
                foreach(var r in used)file.WriteLine(string.Join(",",new[]{r.time,r.frame,r.cpu,r.gpu,r.main,r.render,r.present,r.gc,r.gcPause,r.ram,r.managed,r.cycle,r.action,r.collections}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            }
            File.WriteAllText(prefix+".json",JsonUtility.ToJson(Result,true));
        }
        static Distribution Stats(IEnumerable<double> values)
        {
            var a=values.Where(x=>x>=0 && !double.IsNaN(x)).OrderBy(x=>x).ToArray();var s=new Distribution{samples=a.Length};if(a.Length==0)return s;
            s.mean=a.Average();s.median=a[(a.Length-1)/2];s.p95=a[(int)((a.Length-1)*.95)];s.p99=a[(int)((a.Length-1)*.99)];s.maximum=a[a.Length-1];
            s.overBudget=a.Count(x=>x>1000d/120);s.overBudgetPercent=100d*s.overBudget/a.Length;s.over33=a.Count(x=>x>33.333);s.over50=a.Count(x=>x>50);s.over100=a.Count(x=>x>100);s.fps=s.mean>0?1000/s.mean:0;return s;
        }
        public void Dispose(){gc.Dispose();pause.Dispose();}
    }
}
