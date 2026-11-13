using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class MenuWeaponRegressionVerification
    {
        const string Queue = "Tools/IronRequiem/Requests/menu-weapon-regression-test.txt", Key = "Iron.MenuWeapon.Test";
        static readonly DateTime Loaded = DateTime.UtcNow; static TestRunnerApi api;
        static MenuWeaponRegressionVerification() { EditorApplication.update += Tick; if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + ".restore", false)) { SessionState.EraseBool(Key + ".restore"); if (!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) EditorSceneManager.OpenScene(SessionState.GetString(Key + ".scene", "Assets/IronRequiem/Scenes/SawBotLab.unity")); IronRequiemSetupTools.WriteAudit(); }
            if (!File.Exists(Queue) || new[] { "Assets/IronRequiem/Tests/PlayMode/MenuWeaponRegressionTests.cs", "Assets/IronRequiem/Editor/MenuWeaponRegressionVerification.cs" }.Any(p => File.GetLastWriteTimeUtc(p) > Loaded) || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string phase = File.ReadAllText(Queue).Trim(); File.Delete(Queue); SessionState.SetString(Key + ".phase", phase); SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path); SessionState.SetBool(Key, true); Register();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = phase == "baseline" ? new[] { "IronRequiem.Tests.MenuWeaponRegressionTests.BaselineCapture" } : phase == "throw" ? new[] { "IronRequiem.Tests.MenuWeaponRegressionTests.BigBotThrowVisibleLodsAndRecollect" } : new[] { "IronRequiem.Tests.MenuWeaponRegressionTests.MenuMusicSensitivityAndPauseFit", "IronRequiem.Tests.MenuWeaponRegressionTests.BigBotThrowVisibleLodsAndRecollect" } }));
        }
        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor test) { } public void TestStarted(ITestAdaptor test) { } public void TestFinished(ITestResultAdaptor test) { }
            public void OnError(string message) { SessionState.EraseBool(Key); File.WriteAllText(MenuWeaponRegressionAuthoring.Report + "/runner-error.txt", message); }
            public void RunFinished(ITestResultAdaptor result) { if (!SessionState.GetBool(Key, false)) return; SessionState.EraseBool(Key); string phase = SessionState.GetString(Key + ".phase", "test"); TestRunnerApi.SaveResultToFile(result, MenuWeaponRegressionAuthoring.Report + "/" + phase + ".xml"); File.WriteAllText(MenuWeaponRegressionAuthoring.Report + "/" + phase + "-status.txt", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount); SessionState.SetBool(Key + ".restore", true); }
        }
    }
}
