using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace IronRequiem.Editor
{
    // Uses Unity's installed Test Runner API in the visible Editor. No network service.
    [InitializeOnLoad]
    public static class CharacterVerificationRunner
    {
        const string Key="IronRequiem.CharacterVerification";
        static TestRunnerApi api;
        static readonly Results callbacks=new Results();
        static readonly MethodInfo isRunActive=typeof(TestRunnerApi).GetMethod("IsRunActive",BindingFlags.NonPublic|BindingFlags.Static);
        static bool RunnerBusy => isRunActive!=null && (bool)isRunActive.Invoke(null,null);

        static CharacterVerificationRunner()
        {
            if(SessionState.GetString(Key,"")!="")EditorApplication.delayCall+=Register;
            if(SessionState.GetString(Key,"")=="editPending" || SessionState.GetString(Key,"")=="reviewPending")EditorApplication.update+=ContinueAfterPlay;
        }

        static void Register()
        {
            if(api)return;
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(callbacks);
        }

        [MenuItem("Tools/Iron Requiem/Characters/Run Visible Editor Verification")]
        public static void Run()
        {
            if(RunnerBusy)throw new InvalidOperationException("The Test Runner is still finishing a run.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(active.IsValid() && active.isDirty)throw new InvalidOperationException("The current scene has user changes.");
            EditorSceneManager.OpenScene(CharacterPrototypeBuilder.ScenePath);
            SessionState.SetString(Key,"play");Register();
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).Focus();
            api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,assemblyNames=new[]{"IronRequiem.PlayModeTests"}}));
        }

        static void ContinueAfterPlay()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || RunnerBusy)return;
            EditorApplication.update-=ContinueAfterPlay;
            if(SessionState.GetString(Key,"")=="editPending")CompleteEditVerification();
            else if(SessionState.GetString(Key,"")=="reviewPending")
            {
                SessionState.SetString(Key,"");
                EditorSceneManager.OpenScene(CharacterPrototypeBuilder.ScenePath);IronRequiemSetupTools.WriteAudit();
            }
        }

        [MenuItem("Tools/Iron Requiem/Characters/Complete Edit Verification %#F10")]
        public static void CompleteEditVerification()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            SessionState.SetString(Key,"edit");Register();
            api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,assemblyNames=new[]{"IronRequiem.EditModeTests"}}));
        }

        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                string phase=SessionState.GetString(Key,"");if(phase!="play" && phase!="edit")return;
                Directory.CreateDirectory("Tools/IronRequiem/Reports");
                TestRunnerApi.SaveResultToFile(result,"Tools/IronRequiem/Reports/character-"+phase+"-visible.xml");
                File.WriteAllText("Tools/IronRequiem/Reports/character-visible-status.txt",phase+": "+result.ResultState+", passed="+result.PassCount+", failed="+result.FailCount);
                SessionState.SetString(Key,phase=="play"?"editPending":"reviewPending");
                EditorApplication.update+=ContinueAfterPlay;
            }
        }
    }
}
