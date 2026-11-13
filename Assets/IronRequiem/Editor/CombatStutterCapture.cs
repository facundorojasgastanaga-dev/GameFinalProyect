using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
namespace IronRequiem.Editor
{
    // Editor-only capture; never saves the scene or changes game settings.
    [InitializeOnLoad] public static class CombatStutterCapture
    {
        const string Request="Tools/IronRequiem/Requests/stutter.txt", Output="Tools/IronRequiem/Reports/CombatV03/stutter-main.json", Key="IRON.StutterCapture";
        static int warmup, first; static bool oldEnabled, oldEditor;
        [Serializable] public class Marker { public string name; public double selfMs,totalMs,maxSelfMs; public int calls; }
        [Serializable] public class Frame { public int index; public double ms; public Marker[] expensive; }
        [Serializable] public class Result
        {
            public string environment="Editor CPU Profiler, stationary MainScene camera, capture overhead included; no Player measurement";
            public string resolution,quality,gpu; public int frames,over33ms,over50ms,over100ms;
            public double meanMs,fps,p95Ms,p99Ms,maxMs; public Marker[] markers; public Frame[] slowest;
        }
        static CombatStutterCapture(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                string state=SessionState.GetString(Key,"");
                if(state=="warmup"&&EditorApplication.isPlaying)
                {
                    if(++warmup==1){var player=UnityEngine.Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();if(player)player.GetComponent<Unity.FPS.Game.Health>().Invincible=true;var objectives=UnityEngine.Object.FindAnyObjectByType<Unity.FPS.Game.ObjectiveManager>();if(objectives)objectives.enabled=false;}if(warmup<150)return;
                    oldEnabled=Profiler.enabled;oldEditor=ProfilerDriver.profileEditor;
                    ProfilerDriver.profileEditor=false;Profiler.enabled=true;first=ProfilerDriver.lastFrameIndex+1;
                    SessionState.SetString(Key,"capture");return;
                }
                if(state=="capture"&&EditorApplication.isPlaying)
                {
                    int last=ProfilerDriver.lastFrameIndex;if(last-first<239)return;
                    Profiler.enabled=false;Analyse(Math.Max(first,last-239),last);
                    ProfilerDriver.profileEditor=oldEditor;Profiler.enabled=oldEnabled;
                    SessionState.SetString(Key,"finish");EditorApplication.ExitPlaymode();return;
                }
                if(state=="finish"&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {SessionState.EraseString(Key);IronRequiemSetupTools.WriteAudit();return;}
                if(state!=""||!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(Request);T07AnimationBuilder.RequireClean();
                if(Profiler.enabled)throw new InvalidOperationException("Profiler already recording; preserving user's capture.");
                EditorSceneManager.OpenScene(CombatV03Tools.Main);warmup=0;SessionState.SetString(Key,"warmup");EditorApplication.EnterPlaymode();
            }
            catch(Exception e)
            {
                if(SessionState.GetString(Key,"")=="capture"){ProfilerDriver.profileEditor=oldEditor;Profiler.enabled=oldEnabled;}
                SessionState.EraseString(Key);File.WriteAllText(Output+".error",e.ToString());
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
            }
        }
        static void Analyse(int start,int end)
        {
            var r=new Result{resolution=Screen.width+"x"+Screen.height,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],gpu=SystemInfo.graphicsDeviceName};
            var totals=new Dictionary<string,Marker>();var frames=new List<Frame>();
            for(int f=start;f<=end;f++)using(var raw=ProfilerDriver.GetRawFrameDataView(f,0))
            {
                if(!raw.valid)continue;var local=new Dictionary<string,Marker>();
                for(int i=0;i<raw.sampleCount;i++)
                {
                    double ms=raw.GetSampleTimeMs(i),self=ms;int child=i+1;
                    for(int c=0;c<raw.GetSampleChildrenCount(i);c++){self-=raw.GetSampleTimeMs(child);child+=1+raw.GetSampleChildrenCountRecursive(child);}
                    self=Math.Max(0,self);string name=raw.GetSampleName(i);Add(totals,name,ms,self);Add(local,name,ms,self);
                }
                frames.Add(new Frame{index=f,ms=raw.frameTimeMs,expensive=local.Values.OrderByDescending(m=>m.selfMs).Take(10).ToArray()});
            }
            if(frames.Count==0)throw new InvalidOperationException("No valid CPU profiler frames.");
            var times=frames.Select(f=>f.ms).OrderBy(t=>t).ToArray();r.frames=times.Length;r.meanMs=times.Average();r.fps=1000/r.meanMs;
            r.p95Ms=times[(int)((times.Length-1)*.95)];r.p99Ms=times[(int)((times.Length-1)*.99)];r.maxMs=times.Last();
            r.over33ms=times.Count(t=>t>33.333);r.over50ms=times.Count(t=>t>50);r.over100ms=times.Count(t=>t>100);
            r.markers=totals.Values.OrderByDescending(m=>m.selfMs).Take(40).ToArray();r.slowest=frames.OrderByDescending(f=>f.ms).Take(12).ToArray();
            File.WriteAllText(Output,JsonUtility.ToJson(r,true));
        }
        static void Add(Dictionary<string,Marker> d,string name,double ms,double self)
        {if(!d.TryGetValue(name,out var m))d[name]=m=new Marker{name=name};m.totalMs+=ms;m.selfMs+=self;m.maxSelfMs=Math.Max(m.maxSelfMs,self);m.calls++;}
    }
}
