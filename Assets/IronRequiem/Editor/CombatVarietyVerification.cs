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
    public static class CombatVarietyVerification
    {
        const string Queue = "Tools/IronRequiem/Requests/combat-variety-test.txt", Key = "Iron.CombatVariety.Test";
        static readonly DateTime Loaded = DateTime.UtcNow; static TestRunnerApi api;
        static CombatVarietyVerification() { EditorApplication.update += Tick; if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + ".restore", false)) { SessionState.EraseBool(Key + ".restore"); if (!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) EditorSceneManager.OpenScene(SessionState.GetString(Key + ".scene", "Assets/IronRequiem/Scenes/SawBotLab.unity")); IronRequiemSetupTools.WriteAudit(); }
            if (!File.Exists(Queue) || !File.Exists(CombatVarietyAuthoring.Lab) || new[] { "Assets/IronRequiem/Tests/PlayMode/CombatVarietyTests.cs", "Assets/IronRequiem/Editor/CombatVarietyVerification.cs", "Assets/IronRequiem/Scripts/Levels/CombatTwoRoomsLab.cs", "Assets/IronRequiem/Scripts/Levels/EnemySpawnQueue.cs" }.Any(p => File.GetLastWriteTimeUtc(p) > Loaded) || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            File.Delete(Queue); SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path); SessionState.SetBool(Key, true); Register(); api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "IronRequiem.Tests.CombatVarietyTests", "IronRequiem.Tests.CombatTwoRoomsTests.ExactWavesGateAndCompletion" } }));
        }
        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor test) { } public void TestStarted(ITestAdaptor test) { } public void TestFinished(ITestResultAdaptor test) { }
            public void OnError(string message) { SessionState.EraseBool(Key); File.WriteAllText(CombatVarietyAuthoring.Report + "/runner-error.txt", message); }
            public void RunFinished(ITestResultAdaptor result) { if (!SessionState.GetBool(Key, false)) return; SessionState.EraseBool(Key); TestRunnerApi.SaveResultToFile(result, CombatVarietyAuthoring.Report + "/test.xml"); File.WriteAllText(CombatVarietyAuthoring.Report + "/test-status.txt", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount); SessionState.SetBool(Key + ".restore", true); }
        }
    }
}
