using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// 12/11/2026 — runs the PlayMode CampaignTests from a menu item (so the file bridge can start them) and writes
    /// every leaf result to Tools/IronRequiem/Reports/Campaign/testrun.txt. The callbacks are registered again after
    /// each domain reload (entering Play Mode reloads it), so the results of the run are not lost.
    [InitializeOnLoad]
    public static class CampaignTestRunner
    {
        const string Output = "Tools/IronRequiem/Reports/Campaign/testrun.txt";

        static CampaignTestRunner() => ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());

        [MenuItem("Tools/Iron Requiem/Tests/Run CampaignTests (PlayMode)")]
        static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, "CampaignTests requested " + DateTime.Now + "\n");
            var filter = new Filter { testMode = TestMode.PlayMode, groupNames = new[] { "^IronRequiem\\.Tests\\.CampaignTests" } };
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) => Append($"run started: {tests.TestCaseCount} test(s)");
            public void RunFinished(ITestResultAdaptor result) =>
                Append($"RUN FINISHED {result.ResultState} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount} duration={result.Duration:0.0}s");
            public void TestStarted(ITestAdaptor test) { if (!test.HasChildren) Append("start " + test.FullName); }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                Append($"{result.TestStatus.ToString().ToUpperInvariant()} {result.FullName} ({result.Duration:0.0}s)" + (string.IsNullOrEmpty(result.Message) ? "" : "\n    " + result.Message.Trim().Replace("\n", "\n    ")));
                Debug.Log($"[CampaignTests] {result.TestStatus} {result.Name}");
            }
            static void Append(string line)
            {
                try { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.AppendAllText(Output, DateTime.Now.ToString("HH:mm:ss ") + line + "\n"); } catch (IOException) { }
            }
        }
    }
}
