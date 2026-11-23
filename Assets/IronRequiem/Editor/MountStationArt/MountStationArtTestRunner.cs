using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// Runs this pass's EditMode tests and the station regression (PlayMode) in the open Editor from the file bridge.
    /// Results: Tools/IronRequiem/Reports/MountStationArt/tests/<group>.xml + log.txt + status.txt. Never saves scenes.
    [InitializeOnLoad]
    public static class MountStationArtTestRunner
    {
        const string Key = "IRON.MountStationArt.Tests", GroupKey = "IRON.MountStationArt.Tests.Group";
        const string Folder = MountStationArtTools.ReportDir + "/tests";
        const string Menu = MountStationArtTools.Menu + "Tests/";
        static TestRunnerApi api;
        static MountStationArtTestRunner() { EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }

        [MenuItem(Menu + "Run StationArmRigSegmentedTests (EditMode)")]
        static void RunEdit() => Run("editmode", TestMode.EditMode, "^IronRequiem\\.Tests\\.StationArmRigSegmentedTests\\.");
        [MenuItem(Menu + "Run station regression (PlayMode: campaign, hangar, arms-fix)")]
        static void RunRegression() => Run("regression", TestMode.PlayMode, "^IronRequiem\\.Tests\\.(CampaignTests|HangarIntroPresentationTests|ArmsFixPassTests)\\.");

        static void Run(string group, TestMode mode, string regex)
        {
            Directory.CreateDirectory(Folder);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            MountStationArtTools.RequireCleanScenes();
            Register();
            File.WriteAllText(Folder + "/status.txt", "running " + group + " " + DateTime.Now.ToString("HH:mm:ss"));
            SessionState.SetBool(Key, true); SessionState.SetString(GroupKey, group);
            api.Execute(new ExecutionSettings(new Filter { testMode = mode, groupNames = new[] { regex } }));
        }

        sealed class Results : ICallbacks
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
        }
    }
}
