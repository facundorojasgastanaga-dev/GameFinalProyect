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
    [InitializeOnLoad]
    public static class T07OptimizationVerification
    {
        const string Key = "IronRequiem.T07OptimizationTests";
        const string Reports = "Tools/IronRequiem/Reports/T07Optimization";
        static TestRunnerApi api;
        static readonly Callback callbacks = new Callback();
        static readonly MethodInfo active = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
        static bool Busy => active != null && (bool)active.Invoke(null, null);
        [Serializable] sealed class SavedScenes { public string[] paths; public bool[] loaded, current; }
        static T07OptimizationVerification()
        {
            if (SessionState.GetString(Key, "") != "") EditorApplication.delayCall += Register;
            EditorApplication.update += Tick;
        }
        static void Register()
        { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(callbacks); } }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Busy) return;
            string phase = SessionState.GetString(Key, "");
            if (phase == "editPending")
            {
                SessionState.SetString(Key, "edit"); Register();
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { "IronRequiem.Tests.T07OptimizationAssetTests" } }));
            }
            else if (phase == "restore")
            {
                SessionState.SetString(Key, "");
                var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + ".scenes", ""));
                if (saved != null && saved.paths.Any(p => !string.IsNullOrEmpty(p)))
                    EditorSceneManager.RestoreSceneManagerSetup(Enumerable.Range(0, saved.paths.Length).Select(i => new SceneSetup { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.current[i] }).ToArray());
            }
            else if (File.Exists("Tools/IronRequiem/Requests/t07-verify.json"))
            { File.Delete("Tools/IronRequiem/Requests/t07-verify.json"); Run(); }
        }
        [MenuItem("Tools/Iron Requiem/Optimization/Verify Common T07 LODs")]
        public static void Run()
        {
            if (Busy || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("The Editor is busy.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("User scene has changes; do not overwrite it.");
            SessionState.SetString(Key + ".scenes", JsonUtility.ToJson(new SavedScenes { paths = setup.Select(s => s.path).ToArray(), loaded = setup.Select(s => s.isLoaded).ToArray(), current = setup.Select(s => s.isActive).ToArray() }));
            Directory.CreateDirectory(Reports); SessionState.SetString(Key, "play"); Register();
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "IronRequiem.Tests.T07OptimizationBehaviorTests" } }));
        }
        sealed class Callback : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string phase = SessionState.GetString(Key, "");
                if (phase != "play" && phase != "edit") return;
                TestRunnerApi.SaveResultToFile(result, Reports + "/" + phase + ".xml");
                File.WriteAllText(Reports + "/" + phase + "-status.txt", result.ResultState + ", passed=" + result.PassCount + ", failed=" + result.FailCount);
                SessionState.SetString(Key, phase == "play" ? "editPending" : "restore");
            }
        }
    }
}
