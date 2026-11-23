using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// <summary>Runs the menu suite in the open Editor and restores its clean scene setup afterward.</summary>
    [InitializeOnLoad]
    public static class MenuPolishTestRunner
    {
        const string Key = "IronRequiem.MenuPolishTests";
        const string Reports = "Tools/IronRequiem/Reports/MenuPolish";
        static TestRunnerApi api;

        [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }
        [Serializable] sealed class SceneRecords { public SceneRecord[] scenes; }

        static MenuPolishTestRunner()
        {
            EditorApplication.update += Pump;
            if (SessionState.GetBool(Key, false)) Register();
        }

        static void Pump()
        {
            if (SessionState.GetBool(Key + ".Restore", false) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                SessionState.SetBool(Key + ".Restore", false);
                try { Restore(); }
                catch (Exception error) { Progress("Restore deferred: " + error.Message); }
            }
            if (SessionState.GetBool(Key, false))
            {
                Application.runInBackground = true;
                if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        [MenuItem("Tools/Iron Requiem/Menu Polish/Capture baseline")]
        public static void Baseline() => Start("Assets/FPS/Scenes/IntroMenu.unity", "before", "^IronRequiem\\.Tests\\.MenuPolishTests\\.A_");

        [MenuItem("Tools/Iron Requiem/Menu Polish/Run tests")]
        public static void RunLab() => Start("Assets/IronRequiem/Scenes/IntroMenuPolishLab.unity", "after-lab", "^IronRequiem\\.Tests\\.MenuPolishTests\\.[BCDEF]_");

        [MenuItem("Tools/Iron Requiem/Menu Polish/Run integrated tests")]
        public static void RunIntegrated() => Start("Assets/FPS/Scenes/IntroMenu.unity", "after", "^IronRequiem\\.Tests\\.MenuPolishTests\\.[BCDEF]_");

        [MenuItem("Tools/Iron Requiem/Menu Polish/Test play button")]
        public static void RunPlayButton() => Start(MenuPolishAuthoring.Source, "play-button", "^IronRequiem\\.Tests\\.MenuPolishTests\\.E_");

        static void Start(string scene, string label, string filter)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || SessionState.GetBool(Key, false))
                throw new InvalidOperationException("Wait for the Editor and previous test run to finish.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty user scenes.");
            if (!File.Exists(scene)) throw new FileNotFoundException("Menu test scene is missing.", scene);
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports + "/scene.txt", scene); File.WriteAllText(Reports + "/label.txt", label);
            var records = new SceneRecords { scenes = new SceneRecord[SceneManager.sceneCount] };
            for (int i = 0; i < records.scenes.Length; i++)
            {
                var current = SceneManager.GetSceneAt(i);
                records.scenes[i] = new SceneRecord { path = current.path, loaded = current.isLoaded, active = current == SceneManager.GetActiveScene() };
            }
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(records));
            SessionState.SetString(Key + ".Entry", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Key + ".Label", label);
            SessionState.SetBool(Key + ".Background", Application.runInBackground);
            SessionState.SetBool(Key + ".Restore", false);
            SessionState.SetBool(Key + ".HasRestore", true);
            EditorSceneManager.playModeStartScene = null;
            Application.runInBackground = true;
            SessionState.SetBool(Key, true); Register();
            string guid = api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                assemblyNames = new[] { "IronRequiem.PlayModeTests" }, groupNames = new[] { filter } }));
            SessionState.SetString(Key + ".Guid", guid);
        }

        [MenuItem("Tools/Iron Requiem/Menu Polish/Restore editor")]
        public static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit test Play Mode first.");
            if (!SessionState.GetBool(Key + ".HasRestore", false)) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !scene.path.StartsWith("Assets/InitTestScene", StringComparison.Ordinal))
                    throw new InvalidOperationException("Preserving user scene changes after tests.");
            }
            string entry = SessionState.GetString(Key + ".Entry", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(entry) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(entry);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            var records = JsonUtility.FromJson<SceneRecords>(SessionState.GetString(Key + ".Scenes", ""));
            if (records != null && records.scenes != null && records.scenes.Length > 0)
            {
                var setup = new SceneSetup[records.scenes.Length];
                for (int i = 0; i < setup.Length; i++) setup[i] = new SceneSetup { path = records.scenes[i].path, isLoaded = records.scenes[i].loaded, isActive = records.scenes[i].active };
                bool onlyEmpty = setup.Length == 1 && string.IsNullOrEmpty(setup[0].path);
                if (onlyEmpty) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".HasRestore", false);
            using (var report = new StreamWriter(Reports + "/editor-restoration.txt"))
            {
                report.WriteLine("playing=" + EditorApplication.isPlaying + " compiling=" + EditorApplication.isCompiling);
                report.WriteLine("playModeStartScene=" + AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                for (int i = 0; i < SceneManager.sceneCount; i++) report.WriteLine("scene=" + SceneManager.GetSceneAt(i).path + " dirty=" + SceneManager.GetSceneAt(i).isDirty);
            }
        }

        [MenuItem("Tools/Iron Requiem/Menu Polish/Cancel own test run")]
        public static void CancelOwnRun()
        {
            SessionState.SetBool(Key, false);
            // A failed compilation can leave an earlier job waiting for Play Mode. Inspect only
            // this installed framework's job metadata and cancel jobs filtered to this suite.
            var type = typeof(TestRunnerApi).Assembly.GetType("UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
            if (type != null)
                foreach (var holder in Resources.FindObjectsOfTypeAll(type))
                {
                    var jobs = type.GetField("TestRuns").GetValue(holder) as System.Collections.IEnumerable;
                    if (jobs == null) continue;
                    var own = new System.Collections.Generic.List<string>();
                    foreach (var job in jobs)
                    {
                        var data = job.GetType();
                        var settings = data.GetField("executionSettings").GetValue(job) as ExecutionSettings;
                        bool matches = false;
                        if (settings != null && settings.filters != null)
                            foreach (var filter in settings.filters)
                                if (filter.groupNames != null)
                                    foreach (var group in filter.groupNames)
                                        if (group.Contains("MenuPolishTests")) matches = true;
                        if (matches) own.Add((string)data.GetField("guid").GetValue(job));
                    }
                    foreach (var guid in own) Progress("Cancel own job " + guid + " accepted=" + TestRunnerApi.CancelTestRun(guid));
                }
            if (SessionState.GetBool(Key + ".HasRestore", false)) SessionState.SetBool(Key + ".Restore", true);
        }

        static void Register()
        {
            if (api) return; api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
        }

        static void Progress(string message)
        {
            Directory.CreateDirectory(Reports);
            File.AppendAllText(Reports + "/progress.txt", DateTime.Now.ToString("HH:mm:ss ") + message + "\n");
        }

        sealed class Results : IErrorCallbacks
        {
            public void OnError(string message)
            {
                if (!SessionState.GetBool(Key, false)) return;
                Progress("Runner error: " + message); SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Restore", true);
            }
            public void RunStarted(ITestAdaptor tests) { if (SessionState.GetBool(Key, false)) Progress("Run started " + SessionState.GetString(Key + ".Label", "")); }
            public void TestStarted(ITestAdaptor test) { if (SessionState.GetBool(Key, false) && !test.HasChildren) Progress("Start " + test.FullName); }
            public void TestFinished(ITestResultAdaptor result) { if (SessionState.GetBool(Key, false) && !result.HasChildren) Progress(result.TestStatus + " " + result.FullName + " " + result.Message); }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Key, false)) return;
                string label = SessionState.GetString(Key + ".Label", "run");
                TestRunnerApi.SaveResultToFile(result, Reports + "/" + label + ".xml");
                Progress("Finished " + label + ": " + result.ResultState + ", passed=" + result.PassCount + ", failed=" + result.FailCount);
                SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Restore", true);
                Debug.Log("[MenuPolish] " + label + ": " + result.ResultState + ", passed=" + result.PassCount + ", failed=" + result.FailCount);
            }
        }
    }
}
