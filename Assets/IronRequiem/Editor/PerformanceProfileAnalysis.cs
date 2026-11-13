using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
namespace IronRequiem.Editor
{
    public static class PerformanceProfileAnalysis
    {
        [Serializable] public sealed class Marker {public string name;public double selfMs,totalMs,maxSelfMs;public int calls;}
        [Serializable] public sealed class Report {public int frames;public Marker[] markers;public string note="Development binary CPU profiler during warmup 11.8–16.8s; inclusive marker totals overlap. Rankings use self time, not inclusive sums.";}
        public static void Run(string label)
        {
            string path=Path.GetFullPath(Performance120Tools.Reports+"/"+label+"-warmup.raw");
            if(!File.Exists(path))throw new InvalidOperationException("Profiler capture missing: "+label);
            Profiler.enabled=false;ProfilerDriver.LoadProfile(path,false);
            var report=new Report();var totals=new Dictionary<string,Marker>();
            for(int frame=ProfilerDriver.firstFrameIndex;frame<=ProfilerDriver.lastFrameIndex;frame++)
            using(var raw=ProfilerDriver.GetRawFrameDataView(frame,0))
            {
                if(!raw.valid)continue;report.frames++;
                for(int i=0;i<raw.sampleCount;i++)
                {
                    double total=raw.GetSampleTimeMs(i),self=total;int child=i+1;
                    for(int c=0;c<raw.GetSampleChildrenCount(i);c++){self-=raw.GetSampleTimeMs(child);child+=1+raw.GetSampleChildrenCountRecursive(child);}
                    self=Math.Max(0,self);string name=raw.GetSampleName(i);
                    if(!totals.TryGetValue(name,out var m))totals[name]=m=new Marker{name=name};
                    m.selfMs+=self;m.totalMs+=total;m.maxSelfMs=Math.Max(m.maxSelfMs,self);m.calls++;
                }
            }
            report.markers=totals.Values.OrderByDescending(m=>m.selfMs).Take(100).ToArray();
            File.WriteAllText(Performance120Tools.Reports+"/"+label+"-profile.json",JsonUtility.ToJson(report,true));
        }
    }
}
