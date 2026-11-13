using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026): Release benchmark Players for the A/B (same CombatLoadBenchmark fixture).
    // Same protocol as the existing CombatLoad builds (SecondaryShotgunAuthoring.BenchBuild): frame timing stats,
    // D3D11 and Mono are switched for the build only, and ProjectSettings / render assets are restored byte for byte
    // from a snapshot afterwards (hashes are checked by the caller). Also counts the shader variants that survive
    // stripping per shader/pass, before and after the overhaul.
    public static class VisualOverhaulBench
    {
        public static bool Building { get; private set; }
        public static readonly Dictionary<string, int> Variants = new Dictionary<string, int>();

        public static string Build(string label)
        {
            string scene = label.StartsWith("vo-b") ? VisualOverhaulScenes.BenchB : label.StartsWith("vo-a") ? VisualOverhaulScenes.BenchA : throw new Exception("label vo-a-* or vo-b-*");
            string root = "Tools/IronRequiem/Builds/CombatLoad/" + label, snapshot = "Tools/IronRequiem/Backups/20261107-visual-overhaul/build-" + label;
            var protectedFiles = Directory.GetFiles("ProjectSettings", "*", SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/Rendering", "*", SearchOption.AllDirectories))
                .Concat(new[] { "Assets/DefaultVolumeProfile.asset", "Assets/UniversalRenderPipelineGlobalSettings.asset" }).Where(File.Exists).ToArray();
            foreach (string path in protectedFiles) { string d = snapshot + "/" + path; Directory.CreateDirectory(Path.GetDirectoryName(d)); File.Copy(path, d, true); }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool timing = PlayerSettings.enableFrameTimingStats, defaults = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64); var preloaded = PlayerSettings.GetPreloadedAssets();
            var backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone);
            var flag = typeof(SecondaryShotgunAuthoring).GetProperty("Building", BindingFlags.Public | BindingFlags.Static);
            try
            {
                PlayerSettings.enableFrameTimingStats = true; PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Building = true; Variants.Clear(); flag.SetValue(null, true); SecondaryShotgunAuthoring.StrippedVariants = 0;
                Directory.CreateDirectory(root); Directory.CreateDirectory("Tools/IronRequiem/Reports/CombatLoad");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, locationPathName = root + "/IronRequiemCombatLoad.exe", target = BuildTarget.StandaloneWindows64, options = label.Contains("development") ? BuildOptions.Development : BuildOptions.None });
                string summary = report.summary.result + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings + " bytes=" + report.summary.totalSize + " seconds=" + report.summary.totalTime.TotalSeconds.ToString("F0") + " scene=" + scene;
                File.WriteAllText("Tools/IronRequiem/Reports/CombatLoad/build-" + label + ".txt", summary);
                File.WriteAllText(VisualOverhaulDriver.Reports + "/variants-" + label + ".txt", string.Join("\n", Variants.OrderByDescending(kv => kv.Value).Select(kv => kv.Value + "\t" + kv.Key)));
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Build " + report.summary.result);
                return summary;
            }
            finally
            {
                Building = false; flag.SetValue(null, false); PlayerSettings.enableFrameTimingStats = timing; PlayerSettings.SetPreloadedAssets(preloaded);
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, backend);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, graphics); PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, defaults);
                foreach (string path in protectedFiles) File.Copy(snapshot + "/" + path, path, true);
                AssetDatabase.Refresh(); EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        // Reuses the existing CombatLoad profile analysis (Development Player, wave 3, 6-11 s).
        public static string Analyse(string label)
        {
            var m = typeof(SecondaryShotgunAuthoring).GetMethod("BenchAnalyse", BindingFlags.NonPublic | BindingFlags.Static); m.Invoke(null, new object[] { label });
            return string.Join(",", Directory.GetFiles("Tools/IronRequiem/Reports/CombatLoad", label + "-profile*").Select(Path.GetFileName));
        }

        // Full game build (Build Settings scenes, project settings untouched) to prove the quarantine left builds working.
        public static string BuildGame(string label)
        {
            string root = "Tools/IronRequiem/Builds/VisualOverhaul/" + label; Directory.CreateDirectory(root);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = root + "/IronRequiem.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            string s2 = report.summary.result + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings + " bytes=" + report.summary.totalSize + " seconds=" + report.summary.totalTime.TotalSeconds.ToString("F0") + " scenes=" + scenes.Length;
            File.WriteAllText(VisualOverhaulDriver.Reports + "/build-game-" + label + ".txt", s2);
            return s2;
        }
    }

    // Counts surviving variants of the shaders that matter for the A/B (runs after URP stripping).
    sealed class VisualOverhaulVariantCounter : IPreprocessShaders
    {
        public int callbackOrder => 10000;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!VisualOverhaulBench.Building) return;
            string n = shader.name; if (n != "IronRequiem/HardSurface" && n != "Universal Render Pipeline/Lit" && n != "Shader Graphs/Decal") return;
            string key = n + " | " + snippet.passName + " | " + snippet.shaderType;
            VisualOverhaulBench.Variants.TryGetValue(key, out int c); VisualOverhaulBench.Variants[key] = c + data.Count;
        }
    }
}
