using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor.Map01
{
    /// R0: runs the Map01 F2 PlayMode tests (NUnit category "Map01F2") from the file bridge and writes the results to
    /// Tools/IronRequiem/Reports/Map01/F2/tests/. Refuses to start with a dirty scene; never saves scenes.
    [InitializeOnLoad]
    public static class Map01TestRunner
    {
        const string Key = "IRON.Map01.Tests";
        static readonly string Folder = Map01Paths.ReportRoot + "/tests";
        static TestRunnerApi api;

        static Map01TestRunner() { EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }

        [MenuItem(Map01Paths.MenuRoot + "Run F2 PlayMode tests")]
        public static void RunAll() => Run(null);
        [MenuItem(Map01Paths.MenuRoot + "Test Jugar to hangar")]
        public static void RunLaunch() => Run(new[] { "IronRequiem.Tests.Map01F2Tests.JugarStartsHangarAndReturnsControl" });
        [MenuItem(Map01Paths.MenuRoot + "Run F2 test S2 only")]
        public static void RunS2() => Run(new[] { "IronRequiem.Tests.Map01F2Tests.SessionTraversal(1)" });

        public static void Run(string[] testNames)
        {
            Directory.CreateDirectory(Folder);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Map01 tests: stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Map01 tests: dirty scene " + SceneManager.GetSceneAt(i).path + " (not saved, not discarded).");
            Register();
            File.WriteAllText(Folder + "/status.txt", "running " + DateTime.Now.ToString("HH:mm:ss"));
            SessionState.SetBool(Key, true);
            var filter = new Filter { testMode = TestMode.PlayMode, categoryNames = new[] { "Map01F2" } };
            if (testNames != null) filter.testNames = testNames;
            api.Execute(new ExecutionSettings(filter));
        }

        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor t)
            {
                if (!SessionState.GetBool(Key, false) || t.HasChildren) return;
                File.AppendAllText(Folder + "/log.txt", DateTime.Now.ToString("HH:mm:ss") + " " + t.ResultState + " " + t.FullName + (string.IsNullOrEmpty(t.Message) ? "" : " :: " + t.Message.Replace('\n', ' ')) + "\n");
            }
            public void RunFinished(ITestResultAdaptor r)
            {
                if (!SessionState.GetBool(Key, false)) return;
                SessionState.EraseBool(Key);
                TestRunnerApi.SaveResultToFile(r, Folder + "/play.xml");
                File.WriteAllText(Folder + "/status.txt", r.ResultState + " passed=" + r.PassCount + " failed=" + r.FailCount + " skipped=" + r.SkipCount + " " + DateTime.Now.ToString("HH:mm:ss"));
            }
            public void OnError(string message) { SessionState.EraseBool(Key); File.WriteAllText(Folder + "/runner-error.txt", message); }
        }
    }
}
