using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace IronRequiem.Editor
{
    /// Runs the controls tests from the command bridge and writes Tools/IronRequiem/Reports/Controls/editmode.txt or
    /// playmode.txt. The callbacks are registered on every domain reload because a PlayMode run reloads the domain.
    [InitializeOnLoad]
    public static class ControlsTestRunner
    {
        const string Dir = "Tools/IronRequiem/Reports/Controls";
        const string EditFilter = @"^IronRequiem\.Tests\.(ControlsSchemeTests|ControlsRebinderTests|MechanicsAssetTests)\.";
        const string PlayFilter = @"^IronRequiem\.Tests\.ControlsRebindPlayTests\.";
        static readonly TestRunnerApi Api = UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();

        sealed class Collector : ICallbacks
        {
            string File_ => SessionState.GetString("IR.ControlsRun", "editmode");
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite || !SessionState.GetBool("IR.ControlsActive", false)) return;
                System.IO.File.AppendAllText(Dir + "/" + File_ + ".log", result.TestStatus + "  " + result.Test.FullName + (result.TestStatus == TestStatus.Failed ? "\n    " + result.Message : "") + "\n");
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool("IR.ControlsActive", false)) return;
                SessionState.SetBool("IR.ControlsActive", false);
                var body = System.IO.File.Exists(Dir + "/" + File_ + ".log") ? System.IO.File.ReadAllText(Dir + "/" + File_ + ".log") : "";
                System.IO.File.WriteAllText(Dir + "/" + File_ + ".txt", body + "SUMMARY pass=" + result.PassCount + " fail=" + result.FailCount + " skip=" + result.SkipCount + "\n");
            }
        }

        static ControlsTestRunner() { Api.RegisterCallbacks(new Collector()); }

        static void Run(string name, TestMode mode, string filter)
        {
            Directory.CreateDirectory(Dir);
            File.Delete(Dir + "/" + name + ".log"); File.Delete(Dir + "/" + name + ".txt");
            SessionState.SetString("IR.ControlsRun", name); SessionState.SetBool("IR.ControlsActive", true);
            Api.Execute(new ExecutionSettings(new Filter { testMode = mode, groupNames = new[] { filter } }));
        }

        [MenuItem("Tools/Iron Requiem/Controls/Run EditMode controls tests")] static void RunEdit() => Run("editmode", TestMode.EditMode, EditFilter);
        [MenuItem("Tools/Iron Requiem/Controls/Run PlayMode controls tests")] static void RunPlay() => Run("playmode", TestMode.PlayMode, PlayFilter);
    }
}
