using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IronRequiem.EditorTools
{
    /// v35: campaign tour benchmark launchers. The tour itself is CampaignTourBenchmark (runtime).
    /// Editor: arms the tour and enters Play (it loads RequiemCampaign by itself).
    /// Player: builds a Development player with Autoconnect Profiler into Builds/IR_Tour and starts it with -irTour at
    /// 1920x1080 windowed; the player writes Builds/IR_Tour/ir_tour.txt and quits.
    public static class PerfTourTools
    {
        const string Menu = "Tools/Iron Requiem/Perf/";
        const string BuildDir = "Builds/IR_Tour";
        const string Exe = BuildDir + "/IronRequiem.exe";
        const string Report = "Tools/IronRequiem/Reports/Perf/";

        [MenuItem(Menu + "Tag = before")] static void TagBefore() { PlayerPrefs.SetString("IR_TourTag", "before"); PlayerPrefs.Save(); }
        [MenuItem(Menu + "Tag = after")] static void TagAfter() { PlayerPrefs.SetString("IR_TourTag", "after"); PlayerPrefs.Save(); }

        [MenuItem(Menu + "Tour in Editor (play)")]
        static void EditorTour()
        {
            if (EditorApplication.isPlaying) return;
            PlayerPrefs.SetInt("IR_Tour", 1); PlayerPrefs.Save(); EditorApplication.isPlaying = true;
        }

        [MenuItem(Menu + "Build tour player (development) + run")]
        static void BuildAndRun()
        {
            Directory.CreateDirectory(Report); var log = Report + "build_" + PlayerPrefs.GetString("IR_TourTag", "run") + ".txt";
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = Exe, target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler,
            };
            var t0 = System.DateTime.Now; var result = BuildPipeline.BuildPlayer(options); var summary = result.summary;
            File.WriteAllText(log, $"build {t0:HH:mm:ss} result={summary.result} time={summary.totalTime} size={summary.totalSize / 1048576f:0.0} MB errors={summary.totalErrors} warnings={summary.totalWarnings}\nscenes: {string.Join(", ", scenes)}\n");
            Debug.Log("[PerfTour] build " + summary.result + " → " + log);
            if (summary.result == BuildResult.Succeeded) Run();
        }

        [MenuItem(Menu + "Run tour player (last build)")]
        static void Run() => RunWith("");
        [MenuItem(Menu + "Run tour player (last build, no warm)")]
        static void RunNoWarm() => RunWith(" -irNoWarm");
        [MenuItem(Menu + "Run tour player (last build, no warm, no teardown)")]
        static void RunBaseline() => RunWith(" -irNoWarm -irNoTeardown");
        [MenuItem(Menu + "Run tour player (last build, markers)")]
        static void RunMarkers() => RunWith(" -irMarkers");
        static void RunWith(string extra)
        {
            var exe = Path.GetFullPath(Exe); if (!File.Exists(exe)) { Debug.LogError("[PerfTour] no build at " + exe); return; }
            var old = Path.Combine(Path.GetDirectoryName(exe), "ir_tour.txt"); if (File.Exists(old)) File.Delete(old);
            var info = new ProcessStartInfo(exe, "-irTour -irFullHD" + extra + " -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 -window-mode borderless -logFile \"" + Path.Combine(Path.GetDirectoryName(exe), "player.log") + "\"") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) };
            Process.Start(info); Debug.Log("[PerfTour] started " + exe);
        }

        [MenuItem(Menu + "Copy player tour report")]
        static void CopyReport()
        {
            var src = Path.Combine(BuildDir, "ir_tour.txt"); if (!File.Exists(src)) { Debug.LogWarning("[PerfTour] no player report yet"); return; }
            Directory.CreateDirectory(Report); File.Copy(src, Report + "tour_player_" + PlayerPrefs.GetString("IR_TourTag", "run") + ".txt", true); Debug.Log("[PerfTour] report copied");
        }
    }
}
