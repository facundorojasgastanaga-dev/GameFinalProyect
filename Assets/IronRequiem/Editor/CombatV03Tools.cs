using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class CombatV03Tools
    {
        public const string Main = "Assets/FPS/Scenes/MainScene.unity";
        public const string Lab = "Assets/IronRequiem/Scenes/CombatPerformanceLab.unity";
        public const string Output = "Tools/IronRequiem/Reports/CombatV03";
        const string Key = "IronRequiem.CombatV03";
        const string Request = "Tools/IronRequiem/Requests/combat-v03.json";
        static TestRunnerApi api;
        static readonly Results results = new Results();
        static readonly MethodInfo active = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic);
        static CombatV03Tools() { EditorApplication.update += Tick; if (SessionState.GetString(Key, "") == "test") EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(results); } }
        static void Tick()
        {
            if(SessionState.GetString(Key,"")=="benchmark" && EditorApplication.isPlaying)
            {
                var probe=UnityEngine.Object.FindAnyObjectByType<CombatLoadProbe>();
                if(probe && !SessionState.GetBool(Key+".started",false))
                {SessionState.SetBool(Key+".started",true);probe.StartCoroutine(probe.Run(Output+"/editor-performance.json"));}
                if(probe && probe.Complete){SessionState.SetString(Key,"restore");EditorApplication.ExitPlaymode();}
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                (active != null && (bool)active.Invoke(null, null))) return;
            if (SessionState.GetString(Key, "") == "restore")
            {
                SessionState.EraseString(Key); EditorSceneManager.OpenScene(Main); IronRequiemSetupTools.WriteAudit(); return;
            }
            if (!File.Exists(Request)) return;
            if(File.GetLastWriteTimeUtc(typeof(CombatV03Tools).Assembly.Location)<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/CombatV03Authoring.cs"))return;
            var source=Directory.GetFiles("Assets/IronRequiem/Tests/PlayMode","CombatV03*.cs").Max(File.GetLastWriteTimeUtc);
            if(File.GetLastWriteTimeUtc("Library/ScriptAssemblies/IronRequiem.PlayModeTests.dll")<source)return;
            string operation = File.ReadAllText(Request).Trim(); File.Delete(Request); Directory.CreateDirectory(Output);
            try
            {
                T07AnimationBuilder.RequireClean();
                if (operation == "reproduce" || operation == "test")
                {
                    SessionState.SetString(Key, "test"); SessionState.SetString(Key + ".run", operation); Register();
                    var filter = operation == "reproduce" ? new Filter { testMode = TestMode.PlayMode,
                        testNames = new[] { "IronRequiem.Tests.CombatV03Tests.MainScenePickupIntegration" } } :
                        new Filter { testMode = TestMode.PlayMode, testNames = new[] { "IronRequiem.Tests.CombatV03Tests", "IronRequiem.Tests.CombatV03Coverage" } };
                    api.Execute(new ExecutionSettings(filter));
                }
                else if(operation=="benchmark")
                {EditorSceneManager.OpenScene(Lab);SessionState.SetBool(Key+".started",false);SessionState.SetString(Key,"benchmark");EditorApplication.EnterPlaymode();}
                else ExecuteAuthoring(operation);
            }
            catch (Exception e) { File.WriteAllText(Output + "/status.txt", operation + " FAILED " + e); Debug.LogException(e); }
        }
        static void ExecuteAuthoring(string operation)
        {
            // Resolved by the focused authoring class after its compilation.
            var method = typeof(CombatV03Tools).Assembly.GetType("IronRequiem.Editor.CombatV03Authoring")?
                .GetMethod("Run", BindingFlags.Static | BindingFlags.Public);
            if (method == null) throw new InvalidOperationException("Authoring step not ready: " + operation);
            method.Invoke(null, new object[] { operation });
        }
        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor t) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (SessionState.GetString(Key, "") != "test") return;
                string run = SessionState.GetString(Key + ".run", "test");
                TestRunnerApi.SaveResultToFile(result, Output + "/" + run + ".xml");
                File.WriteAllText(Output + "/status.txt", run + ": " + result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount);
                SessionState.SetString(Key, "restore");
            }
        }
    }
}
