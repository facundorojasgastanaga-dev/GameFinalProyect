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
    public static class WeaponHeatBalanceVerification
    {
        const string Queue = "Tools/IronRequiem/Requests/weapon-heat-balance-test.txt", Key = "Iron.WeaponHeatBalance.Test";
        static readonly DateTime Loaded = DateTime.UtcNow; static TestRunnerApi api;
        static WeaponHeatBalanceVerification() { EditorApplication.update += Tick; if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + ".restore", false)) { SessionState.EraseBool(Key + ".restore"); if (!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) EditorSceneManager.OpenScene(SessionState.GetString(Key + ".scene", "Assets/IronRequiem/Scenes/SawBotLab.unity")); IronRequiemSetupTools.WriteAudit(); }
            if (!File.Exists(Queue) || new[] { "Assets/IronRequiem/Tests/PlayMode/ThermalHudRepairTests.cs", "Assets/IronRequiem/Editor/WeaponHeatBalanceVerification.cs" }.Any(p => File.GetLastWriteTimeUtc(p) > Loaded) || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string phase = File.ReadAllText(Queue).Trim(); File.Delete(Queue); SessionState.SetString(Key + ".phase", phase); SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path); SessionState.SetBool(Key, true); Register();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = phase == "lab" ? new[] { "IronRequiem.Tests.ThermalHudRepairTests.ExtendedHeatAndShotgunWithoutAmmo" } : new[] { "IronRequiem.Tests.ThermalHudRepairTests" } }));
        }
        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor test) { } public void TestStarted(ITestAdaptor test) { } public void TestFinished(ITestResultAdaptor test) { }
            public void OnError(string message) { SessionState.EraseBool(Key); File.WriteAllText(WeaponHeatBalanceAuthoring.Report + "/runner-error.txt", message); }
            public void RunFinished(ITestResultAdaptor result) { if (!SessionState.GetBool(Key, false)) return; SessionState.EraseBool(Key); string phase = SessionState.GetString(Key + ".phase", "test"); TestRunnerApi.SaveResultToFile(result, WeaponHeatBalanceAuthoring.Report + "/" + phase + ".xml"); File.WriteAllText(WeaponHeatBalanceAuthoring.Report + "/" + phase + "-status.txt", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount); SessionState.SetBool(Key + ".restore", true); }
        }
    }
}


