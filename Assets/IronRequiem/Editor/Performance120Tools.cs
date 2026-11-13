using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace IronRequiem.Editor
{
    [InitializeOnLoad] public static class Performance120Tools
    {
        const string Request="Tools/IronRequiem/Requests/performance120.txt";
        public const string Lab="Assets/IronRequiem/Scenes/Performance120Lab.unity";
        public const string Reports="Tools/IronRequiem/Reports/Performance120";
        public static bool Building {get;private set;}
        public static int StrippedVariants;
        static Performance120Tools(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            if(File.GetLastWriteTimeUtc(typeof(Performance120Tools).Assembly.Location)<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/Performance120Tools.cs"))return;
            if(File.GetLastWriteTimeUtc(typeof(SustainedCombatBenchmark).Assembly.Location)<File.GetLastWriteTimeUtc("Assets/IronRequiem/Scripts/Diagnostics/SustainedCombatBenchmark.cs"))return;
            string op=File.ReadAllText(Request).Trim();File.Delete(Request);Directory.CreateDirectory(Reports);
            try
            {
                T07AnimationBuilder.RequireClean();
                if(op=="prepare")Prepare();else if(op=="run-smoke")RunNative("baseline-development","smoke-native",1,false);else if(op=="run-development")RunNative("baseline-development","baseline-development",15,true);else if(op=="run-release-baseline")RunNative("baseline-release","baseline-release",15,false);else if(op=="run-before")RunNative("candidate-release","validation-before",15,false);else if(op=="run-after1")RunNative("candidate-release","validation-after1",15,false);else if(op=="run-after2")RunNative("candidate-release","validation-after2",15,false);else if(op=="analyse-smoke")PerformanceProfileAnalysis.Run("smoke-development");else if(op=="analyse-development")PerformanceProfileAnalysis.Run("baseline-development");else if(op.StartsWith("build-"))Build(op.Substring(6));
                else if(op=="audit")IronRequiemSetupTools.WriteAudit();else throw new InvalidOperationException("Unknown performance operation "+op);
                File.WriteAllText(Reports+"/status.txt",op+" SUCCESS");
            }
            catch(Exception e){File.WriteAllText(Reports+"/status.txt",op+" FAILED "+e);Debug.LogException(e);}
        }
        static void RunNative(string build,string run,int cycles,bool profile)
        {
            string exe=Path.GetFullPath("Tools/IronRequiem/Builds/Performance120/"+build+"/IronRequiemBenchmark.exe"),output=Path.GetFullPath(Reports+"/"+run);
            var args="-force-d3d11 -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 -window-mode exclusive --iron-output "+output+" --iron-cycles "+cycles+" --iron-profile "+(profile?"1":"0")+" -logFile "+output+"-player.log";
            var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe,args){UseShellExecute=false,WorkingDirectory=Directory.GetCurrentDirectory()});
            File.WriteAllText(Reports+"/"+run+"-pid.txt",p.Id.ToString());
        }
        static void Prepare()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if(!File.Exists(Lab)&&!AssetDatabase.CopyAsset(CombatV03Tools.Lab,Lab))throw new InvalidOperationException("Cannot copy performance fixture.");
                var scene=EditorSceneManager.OpenScene(Lab);
                foreach(var route in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Unity.FPS.AI.PatrolPath>(true)))route.EnemiesToAssign.Clear();
                foreach(var enemy in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Unity.FPS.AI.EnemyController>(true)))UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                foreach(var root in scene.GetRootGameObjects())foreach(var probe in root.GetComponentsInChildren<CombatLoadProbe>(true))UnityEngine.Object.DestroyImmediate(probe);
                var runner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SustainedCombatBenchmark>(true)).FirstOrDefault();
                if(!runner)runner=new GameObject("SustainedCombatBenchmark").AddComponent<SustainedCombatBenchmark>();
                runner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(T07DestructionBuilder.Enemy));
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save separate benchmark scene.");
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
            IronRequiemSetupTools.WriteAudit();
        }
        static void Build(string label)
        {
            bool development=label.Contains("development");
            string root="Tools/IronRequiem/Builds/Performance120/"+label;
            string snapshot="Tools/IronRequiem/Backups/20261030-performance120/build-"+label;
            var protectedFiles=Directory.GetFiles("ProjectSettings","*",SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Rendering","*",SearchOption.AllDirectories))
                .Concat(new[]{"Assets/DefaultVolumeProfile.asset","Assets/UniversalRenderPipelineGlobalSettings.asset"}).Where(File.Exists).ToArray();
            foreach(string path in protectedFiles){string destination=snapshot+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination,true);}
            var setup=EditorSceneManager.GetSceneManagerSetup();
            bool timing=PlayerSettings.enableFrameTimingStats, defaults=PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var graphics=PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);var preloaded=PlayerSettings.GetPreloadedAssets();
            var backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.enableFrameTimingStats=true;PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
                Building=true;StrippedVariants=0;Directory.CreateDirectory(root);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Lab},locationPathName=root+"/IronRequiemBenchmark.exe",target=BuildTarget.StandaloneWindows64,options=development?BuildOptions.Development:BuildOptions.None});
                File.WriteAllText(Reports+"/build-"+label+".txt",report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings+" bytes="+report.summary.totalSize+" inferenceComputeVariantsExcluded="+StrippedVariants);
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Benchmark build "+report.summary.result);
            }
            finally
            {
                Building=false;PlayerSettings.enableFrameTimingStats=timing;PlayerSettings.SetPreloadedAssets(preloaded);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,backend);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,graphics);PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,defaults);
                foreach(string path in protectedFiles)File.Copy(snapshot+"/"+path,path,true);
                AssetDatabase.Refresh();EditorSceneManager.RestoreSceneManagerSetup(setup);IronRequiemSetupTools.WriteAudit();
            }
        }
    }
}
