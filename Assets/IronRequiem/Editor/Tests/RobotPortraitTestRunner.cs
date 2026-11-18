using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    [InitializeOnLoad]
    public static class RobotPortraitTestRunner
    {
        const string Key = "IronRequiem.RobotPortraitV03Tests";
        static TestRunnerApi api;
        static RobotPortraitTestRunner()
        {
            EditorApplication.update += Pump;
            if (SessionState.GetBool(Key, false)) Register();
        }
        static void Pump()
        {
            if (SessionState.GetBool(Key + ".RestoreEntry", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                string path = SessionState.GetString(Key + ".Entry", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                SessionState.SetBool(Key + ".RestoreEntry", false);
            }
            if (!SessionState.GetBool(Key, false)) return;
            Application.runInBackground = true;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        }
        static void Register()
        {
            if (api) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results());
        }
        [MenuItem("Tools/Iron Requiem/Robot Portrait/Restore editor after cancelled tests")]
        public static void RestoreEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit portrait test Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !scene.path.StartsWith("Assets/InitTestScene"))
                    throw new InvalidOperationException("Preserving user scene changes.");
            }
            // The pre-task audit recorded this single clean scene. Cancelled Test Runner attempts left its own init scene loaded.
            if (SessionState.GetBool(Key, false))
            {
                string path = SessionState.GetString(Key + ".Entry", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            }
            SessionState.SetBool(Key, false);
            EditorSceneManager.OpenScene("Assets/FPS/Scenes/IntroMenu.unity");
        }
        [MenuItem("Tools/Iron Requiem/Robot Portrait/Run portrait tests")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving user scene changes.");
            SessionState.SetBool(Key + ".Background", Application.runInBackground);
            // The configured IntroMenu override otherwise replaces Unity Test Runner's initialization scene.
            SessionState.SetString(Key + ".Entry", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = null;
            Application.runInBackground = true;
            SessionState.SetBool(Key, true); Register();
            string guid = api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                assemblyNames = new[] { "IronRequiem.PlayModeTests" },
                groupNames = new[] { "^IronRequiem\\.Tests\\.RobotPortraitTests\\." } }));
            SessionState.SetString(Key + ".Guid", guid);
        }
        sealed class Results : IErrorCallbacks
        {
            static void Progress(string line)
            {
                System.IO.Directory.CreateDirectory("Tools/IronRequiem/Reports/RobotPortraitV03");
                System.IO.File.AppendAllText("Tools/IronRequiem/Reports/RobotPortraitV03/progress.txt", DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
            }
            public void OnError(string message)
            {
                if (!SessionState.GetBool(Key, false)) return;
                Progress("Runner error: " + message);
                SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".RestoreEntry", true);
                Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            }
            public void RunStarted(ITestAdaptor tests) { if (SessionState.GetBool(Key, false)) Progress("Run started: " + tests.TestCaseCount); }
            public void TestStarted(ITestAdaptor test) { if (SessionState.GetBool(Key, false) && !test.HasChildren) Progress("Start " + test.FullName); }
            public void TestFinished(ITestResultAdaptor result)
            { if (SessionState.GetBool(Key, false) && !result.HasChildren) Progress(result.TestStatus + " " + result.FullName + " " + result.Message); }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Key, false)) return;
                SessionState.SetBool(Key, false);
                SessionState.SetBool(Key + ".RestoreEntry", true);
                Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
                System.IO.Directory.CreateDirectory("Tools/IronRequiem/Reports/RobotPortraitV03");
                TestRunnerApi.SaveResultToFile(result, "Tools/IronRequiem/Reports/RobotPortraitV03/playmode.xml");
                Debug.Log($"[RobotPortraitV03] {result.ResultState}: {result.PassCount} passed, {result.FailCount} failed.");
            }
        }
    }
}
