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
    public static class ShredderT07RepairVerification
    {
        const string Queue = "Tools/IronRequiem/Requests/shredder-t07-repair-test.txt", Key = "Iron.ShredderT07Repair.Test";
        static readonly DateTime Loaded = DateTime.UtcNow; static TestRunnerApi api;
        static ShredderT07RepairVerification() { EditorApplication.update += Tick; if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + ".restore", false)) { SessionState.EraseBool(Key + ".restore"); if (!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) EditorSceneManager.OpenScene(SessionState.GetString(Key + ".scene", "Assets/IronRequiem/Scenes/SawBotLab.unity")); IronRequiemSetupTools.WriteAudit(); }
            if (!File.Exists(Queue) || new[] { "Assets/IronRequiem/Tests/PlayMode/ShredderT07RepairTests.cs", "Assets/IronRequiem/Editor/ShredderT07RepairVerification.cs" }.Any(p => File.GetLastWriteTimeUtc(p) > Loaded) || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string phase = File.ReadAllText(Queue).Trim(); File.Delete(Queue); SessionState.SetString(Key + ".phase", phase); SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path); SessionState.SetBool(Key, true); Register();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = phase == "variant" ? new[] { "IronRequiem.Tests.ShredderT07RepairTests.RepairedLegInteriorsPreserveAllLodsAndDamageStages", "IronRequiem.Tests.ShredderT07RepairTests.T07ReallyChargesThenCancelsAtWallsAndDeath", "IronRequiem.Tests.ShredderT07RepairTests.CrusherRollersAreHorizontalCounterRotatingAndPauseCleanly" } : phase == "integrated" ? new[] { "IronRequiem.Tests.ChaseCorridorTests.RunCorridorJumpThreeGapsAndReachExit", "IronRequiem.Tests.ChaseCorridorTests.FallCostsHealthAndPreservesInventoryWhileSawsContinue", "IronRequiem.Tests.ChaseCorridorTests.SawsCatchPlayerAndRestartResetsEncounter" } : new[] { "IronRequiem.Tests.ShredderT07RepairTests.BaselineRobotLegAndCharge" } }));
        }
        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor test) { } public void TestStarted(ITestAdaptor test) { } public void TestFinished(ITestResultAdaptor test) { }
            public void OnError(string message) { SessionState.EraseBool(Key); File.WriteAllText(ShredderT07RepairAuthoring.Report + "/runner-error.txt", message); }
            public void RunFinished(ITestResultAdaptor result) { if (!SessionState.GetBool(Key, false)) return; SessionState.EraseBool(Key); string phase = SessionState.GetString(Key + ".phase", "test"); TestRunnerApi.SaveResultToFile(result, ShredderT07RepairAuthoring.Report + "/" + phase + ".xml"); File.WriteAllText(ShredderT07RepairAuthoring.Report + "/" + phase + "-status.txt", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount); SessionState.SetBool(Key + ".restore", true); }
        }
    }
}





