using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026) request queue. Same local, file-based protocol as the other Iron Requiem tools:
    // one request file, one-shot, no socket. Only static methods of IronRequiem.Editor types whose name starts with
    // "VisualOverhaul" can be invoked, plus Test Runner executions. Never runs while compiling, importing, playing
    // or while a loaded scene has unsaved user changes.
    [InitializeOnLoad]
    public static class VisualOverhaulDriver
    {
        public const string Reports = "Tools/IronRequiem/Reports/VisualOverhaul";
        const string Queue = "Tools/IronRequiem/Requests/visual-overhaul.txt";
        const string Key = "Iron.VisualOverhaul.Test";
        static double next;
        static TestRunnerApi api;

        static VisualOverhaulDriver()
        {
            EditorApplication.update += Tick;
            if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register;
        }

        static void Register()
        {
            if (api) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results());
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            Directory.CreateDirectory(Reports);
            bool testing = SessionState.GetBool(Key, false);
            File.WriteAllText(Reports + "/heartbeat.txt", DateTime.Now.ToString("O") + " compiling=" + EditorApplication.isCompiling + " updating=" + EditorApplication.isUpdating + " playing=" + EditorApplication.isPlayingOrWillChangePlaymode + " testing=" + testing + " paused=" + EditorApplication.isPaused + " timeScale=" + Time.timeScale + " scene=" + SceneManager.GetActiveScene().path);
            if (testing) Watchdog();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || testing) return;
            if (SessionState.GetBool(Key + ".restore", false))
            {
                SessionState.EraseBool(Key + ".restore");
                if (SessionState.GetBool(Key + ".startSceneSaved", false))
                {
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".startScene", ""));
                    SessionState.EraseBool(Key + ".startSceneSaved");
                }
                string scene = SessionState.GetString(Key + ".scene", "");
                if (!string.IsNullOrEmpty(scene) && File.Exists(scene) && !AnyDirty()) EditorSceneManager.OpenScene(scene);
            }
            if (!File.Exists(Queue)) return;
            string line = File.ReadAllText(Queue).Trim(); File.Delete(Queue);
            var parts = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return;
            string id = parts[0], command = parts[1], args = parts.Length > 2 ? parts[2] : "";
            if (AnyDirty() && !SceneManager.GetActiveScene().path.StartsWith("Assets/InitTestScene") && !(command.StartsWith("VisualOverhaulScenes.") && SceneManager.GetActiveScene().path.StartsWith(VisualOverhaulScenes.Folder))) { Write(id, "refused", "A loaded scene has unsaved changes."); return; }
            try
            {
                if (command == "tests") { StartTests(id, args); return; }
                if (command == "open-lab") { EditorSceneManager.OpenScene(VisualOverhaulScenes.Lab, OpenSceneMode.Single); Write(id, "ok", "opened"); return; }
                if (command == "refresh") { AssetDatabase.Refresh(); Write(id, "ok", "refresh requested"); return; }
                var dot = command.LastIndexOf('.');
                string typeName = command.Substring(0, dot), method = command.Substring(dot + 1);
                if (!typeName.StartsWith("VisualOverhaul", StringComparison.Ordinal) && !typeName.StartsWith("IronCampaign", StringComparison.Ordinal)) throw new InvalidOperationException("Type outside the visual overhaul tools.");
                var type = typeof(VisualOverhaulDriver).Assembly.GetType("IronRequiem.Editor." + typeName, true);
                var mi = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
                if (mi == null) throw new MissingMethodException(typeName, method);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                object result = mi.GetParameters().Length == 1 ? mi.Invoke(null, new object[] { args }) : mi.Invoke(null, null);
                Write(id, "ok", (result?.ToString() ?? "done") + "\nseconds=" + watch.Elapsed.TotalSeconds.ToString("F1"));
            }
            catch (Exception e) { Write(id, "failed", (e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e).ToString()); }
        }

        // A test that never finishes (for example waiting on scaled time while a menu keeps timeScale at 0) would block
        // the queue forever: after WatchdogMinutes without a finished test, record it and leave Play Mode.
        public const double WatchdogMinutes = 6;
        static void Watchdog()
        {
            string stamp = SessionState.GetString(Key + ".last", "");
            if (stamp.Length == 0) { Touch(); return; }
            double last = double.Parse(stamp, System.Globalization.CultureInfo.InvariantCulture);
            if ((DateTime.Now - DateTime.FromOADate(last)).TotalMinutes < WatchdogMinutes) return;
            string id = SessionState.GetString(Key + ".id", "x");
            SessionState.EraseBool(Key);
            Write(id, "hang", "No test finished for " + WatchdogMinutes + " min; left Play Mode. Last finished test in tests-" + id + "-progress.txt");
            File.AppendAllText(Reports + "/tests-" + id + "-progress.txt", "Hang (watchdog) after the line above\n");
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            SessionState.SetBool(Key + ".restore", true);
        }
        static void Touch() => SessionState.SetString(Key + ".last", DateTime.Now.ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture));

        static bool AnyDirty() => Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);

        public static void Write(string id, string status, string detail)
        {
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports + "/req-" + id + ".txt", status + "\n" + DateTime.Now.ToString("O") + "\n" + detail);
        }

        // args: "<EditMode|PlayMode> [all | name1,name2 | ns:Namespace.Prefix,...]"
        static void StartTests(string id, string args)
        {
            var a = args.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var mode = a[0] == "EditMode" ? TestMode.EditMode : TestMode.PlayMode;
            var filter = new Filter { testMode = mode };
            if (a.Length > 1 && a[1] != "all")
            {
                var items = a[1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                var names = items.Where(s => !s.StartsWith("ns:")).ToArray();
                var groups = items.Where(s => s.StartsWith("ns:")).Select(s => "^" + System.Text.RegularExpressions.Regex.Escape(s.Substring(3))).ToArray();
                if (names.Length > 0) filter.testNames = names;
                if (groups.Length > 0) filter.groupNames = groups;
            }
            SessionState.SetString(Key + ".id", id);
            SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path);
            // A configured campaign entry would replace the Test Runner's temporary bootstrap scene.
            // Clear it only for this run and restore it after success, runner error or watchdog exit.
            SessionState.SetString(Key + ".startScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key + ".startSceneSaved", true);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(Key, true); Touch();
            Register();
            Write(id, "running", args);
            api.Execute(new ExecutionSettings(filter));
        }

        sealed class Results : IErrorCallbacks
        {
            int done;
            public void RunStarted(ITestAdaptor test) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor test)
            {
                if (test.HasChildren) return;
                done++; Touch();
                File.AppendAllText(Reports + "/tests-" + SessionState.GetString(Key + ".id", "x") + "-progress.txt", test.ResultState + " " + test.FullName + " " + test.Duration.ToString("F1") + "s\n");
            }
            public void OnError(string message)
            {
                SessionState.EraseBool(Key);
                Write(SessionState.GetString(Key + ".id", "x"), "runner-error", message);
                SessionState.SetBool(Key + ".restore", true);
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Key, false)) return;
                SessionState.EraseBool(Key);
                string id = SessionState.GetString(Key + ".id", "x");
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(Reports + "/tests-" + id + ".xml"));
                Write(id, result.FailCount == 0 ? "ok" : "failed", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount + " skipped=" + result.SkipCount + " inconclusive=" + result.InconclusiveCount + " seconds=" + result.Duration.ToString("F0"));
                SessionState.SetBool(Key + ".restore", true);
            }
        }
    }
}
