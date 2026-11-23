using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// Runs the arms-fix PlayMode tests (new ArmsFixPassTests + the regression suites around melee, fist charge,
    /// station, switching, executions, OVERDRIVE and throw) in the open Editor from the file bridge.
    /// Results: Tools/IronRequiem/Reports/ArmsFixPass/tests/<group>.xml + log.txt + status.txt. Never saves scenes.
    [InitializeOnLoad]
    public static class ArmsFixPassTestRunner
    {
        const string Key = "IRON.ArmsFix.Tests", GroupKey = "IRON.ArmsFix.Tests.Group";
        const string Folder = "Tools/IronRequiem/Reports/ArmsFixPass/tests";
        const string Menu = "Tools/Iron Requiem/Arms Fix Pass/Tests/";
        static TestRunnerApi api;
        static ArmsFixPassTestRunner() { EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }

        [MenuItem(Menu + "Run new ArmsFixPassTests")] static void RunNew() => Run("new", "^IronRequiem\\.Tests\\.ArmsFixPassTests\\.");
        [MenuItem(Menu + "Run regression A (station, campaign, hangar)")] static void RunA() => Run("regA", "^IronRequiem\\.Tests\\.(CampaignTests|HangarIntroPresentationTests)\\.");
        [MenuItem(Menu + "Run regression B (executions, melee, switch, throw)")]
        static void RunB() => Run("regB", "^IronRequiem\\.Tests\\.(BunkerPolishTests|ProtagonistActionsTests|VisualOverhaulTests|SawBotWeaponDeathTests|FastCombatTests|CombatFollowupTests|ModularArmTests)\\.");
        [MenuItem(Menu + "Run regression C (mechanics, OVERDRIVE, combat)")] static void RunC() => Run("regC", "^IronRequiem\\.Tests\\.(MechanicsBehaviorTests|CombatV03Coverage)\\.");

        // the ten tests that failed after the change, to run against the pre-change code (baseline) and again after fixes
        static readonly string[] Failing =
        {
            "IronRequiem.Tests.BunkerPolishTests.ShotgunReloadsBankOfFourWithVent",
            "IronRequiem.Tests.CombatFollowupTests.ExecutionProtectsT07AndExpiresWithoutChangingGodMode",
            "IronRequiem.Tests.FastCombatTests.T07ThresholdWallsRangeInventoryAndUniqueDelivery",
            "IronRequiem.Tests.ProtagonistActionsTests.FArticulatesFistHitsOneHealthOnceAndRecovers",
            "IronRequiem.Tests.ProtagonistActionsTests.MeleeRespectsWallRangeHoldAndRecovery",
            "IronRequiem.Tests.ProtagonistActionsTests.SlideDirectionStaysFixedAndTorsoCannotTurnBehind",
            "IronRequiem.Tests.CampaignTests.IntroMenuStartsCampaignAndRestartReturnsToRoomOne",
            "IronRequiem.Tests.CombatV03Coverage.CriticalRightArmFallsOnceAcrossEveryLodWithoutLosingCannon",
            "IronRequiem.Tests.MechanicsBehaviorTests.FullChargeUsesItsLargerRadiusForAnActualOffsetHit",
            "IronRequiem.Tests.MechanicsBehaviorTests.SlideContactParticlesFollowAndStopWithSlideAndCAloneCrouches",
        };
        [MenuItem(Menu + "Run the 10 failing (baseline tag)")] static void RunBaseline() => RunNames("baseline", Failing);
        [MenuItem(Menu + "Run the 10 failing (recheck tag)")] static void RunRecheck() => RunNames("recheck", Failing);
        [MenuItem(Menu + "Run second-pass newcomers (flaky check)")]
        static void RunFlaky() => RunNames("flaky", new[] { "IronRequiem.Tests.ModularArmTests.SustainedFireTurnsCapsulesRedAndCoolsBack", "IronRequiem.Tests.ProtagonistActionsTests.DownwardLookKeepsLegPoseAnchoredAndVisible" });
        static void RunNames(string group, string[] names)
        {
            Directory.CreateDirectory(Folder);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Register();
            File.WriteAllText(Folder + "/status.txt", "running " + group + " " + DateTime.Now.ToString("HH:mm:ss"));
            SessionState.SetBool(Key, true); SessionState.SetString(GroupKey, group);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = names }));
        }

        static void Run(string group, string regex)
        {
            Directory.CreateDirectory(Folder);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Dirty scene " + SceneManager.GetSceneAt(i).path);
            Register();
            File.WriteAllText(Folder + "/status.txt", "running " + group + " " + DateTime.Now.ToString("HH:mm:ss"));
            SessionState.SetBool(Key, true); SessionState.SetString(GroupKey, group);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, groupNames = new[] { regex } }));
        }

        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor t)
            {
                if (!SessionState.GetBool(Key, false) || t.HasChildren) return;
                File.AppendAllText(Folder + "/log.txt", DateTime.Now.ToString("HH:mm:ss") + " [" + SessionState.GetString(GroupKey, "") + "] " + t.ResultState + " " + t.FullName
                    + " (" + t.Duration.ToString("0.0") + " s)" + (string.IsNullOrEmpty(t.Message) ? "" : " :: " + t.Message.Replace('\n', ' ')) + "\n");
            }
            public void RunFinished(ITestResultAdaptor r)
            {
                if (!SessionState.GetBool(Key, false)) return;
                var group = SessionState.GetString(GroupKey, "run"); SessionState.EraseBool(Key);
                TestRunnerApi.SaveResultToFile(r, Folder + "/" + group + ".xml");
                File.WriteAllText(Folder + "/status.txt", group + " " + r.ResultState + " passed=" + r.PassCount + " failed=" + r.FailCount + " skipped=" + r.SkipCount + " " + DateTime.Now.ToString("HH:mm:ss"));
            }
            public void OnError(string message) { SessionState.EraseBool(Key); File.WriteAllText(Folder + "/runner-error.txt", message); }
        }
    }
}
