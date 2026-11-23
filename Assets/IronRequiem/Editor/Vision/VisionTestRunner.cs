using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools.Vision
{
    /// Chat 11 · runs the vision tests in the open Editor (no second Unity) and writes XML + status to Reports/Vision/F2.
    [InitializeOnLoad]
    public static class VisionTestRunner
    {
        const string Report = "Tools/IronRequiem/Reports/Vision/F2";
        const string Key = "IR.VisionTests";
        static TestRunnerApi api;
        static VisionTestRunner() { if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }

        [MenuItem("Tools/Iron Requiem/Vision/Run EditMode tests")]
        public static void EditMode() => Run("editmode", TestMode.EditMode, "IronRequiem.Tests.VisionReadabilityEditTests");
        [MenuItem("Tools/Iron Requiem/Vision/Run PlayMode tests")]
        public static void PlayMode() => Run("playmode", TestMode.PlayMode, "IronRequiem.Tests.VisionReadabilityTests");

        // MapFxAudit horde sample in Room03 (worst SetPass room) with the vision feature off and on; horde.txt is copied per run.
        [MenuItem("Tools/Iron Requiem/Vision/Perf horde Room03 - vision OFF")]
        public static void HordeOff() => Horde("horde-off", 0);
        [MenuItem("Tools/Iron Requiem/Vision/Perf horde Room03 - vision ON")]
        public static void HordeOn() => Horde("horde-on", 1);
        static void Horde(string phase, float intensity)
        {
            SessionState.SetFloat(Key + ".userIntensity", RequiemSettings.VisionIntensity); RequiemSettings.SetVisionIntensity(intensity);
            RunNames(phase, TestMode.PlayMode, new[] { "IronRequiem.Tests.MapFxAudit.C_HordeProfilerRoom03" });
        }
        static void RunNames(string phase, TestMode mode, string[] names)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene.");
            Directory.CreateDirectory(Report); SessionState.SetString(Key + ".phase", phase); SessionState.SetBool(Key, true); Register();
            SessionState.SetString(Key + ".start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)); SessionState.SetBool(Key + ".restore", true); EditorSceneManager.playModeStartScene = null;
            api.Execute(new ExecutionSettings(new Filter { testMode = mode, testNames = names }));
        }

        static void Run(string phase, TestMode mode, string fixture)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene.");
            Directory.CreateDirectory(Report);
            SessionState.SetString(Key + ".phase", phase); SessionState.SetBool(Key, true); Register();
            if (mode == TestMode.PlayMode)
            {
                SessionState.SetString(Key + ".start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Key + ".restore", true); EditorSceneManager.playModeStartScene = null;
            }
            api.Execute(new ExecutionSettings(new Filter { testMode = mode, groupNames = new[] { "^" + fixture.Replace(".", "\\.") } }));
        }
        static void Restore()
        {
            if (!SessionState.GetBool(Key + ".restore", false)) return;
            SessionState.EraseBool(Key + ".restore"); string p = SessionState.GetString(Key + ".start", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(p);
        }
        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t) { } public void TestStarted(ITestAdaptor t) { } public void TestFinished(ITestResultAdaptor r) { }
            public void OnError(string message) { SessionState.EraseBool(Key); Restore(); File.WriteAllText(Report + "/test-runner-error.txt", message); }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Key, false)) return; SessionState.EraseBool(Key);
                string phase = SessionState.GetString(Key + ".phase", "");
                TestRunnerApi.SaveResultToFile(result, Report + "/" + phase + "-tests.xml");
                File.WriteAllText(Report + "/" + phase + "-tests-status.txt", $"{DateTime.Now:s} {result.ResultState}; passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}");
                if (phase.StartsWith("horde-"))
                {
                    const string horde = "Tools/IronRequiem/Reports/MapFxAudit/horde.txt";
                    if (File.Exists(horde)) File.Copy(horde, "Tools/IronRequiem/Reports/Vision/F3/" + phase + ".txt", true);
                    RequiemSettings.SetVisionIntensity(SessionState.GetFloat(Key + ".userIntensity", 1));
                }
                Restore();
            }
        }
    }
}
