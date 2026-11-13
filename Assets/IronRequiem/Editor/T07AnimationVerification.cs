using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class T07AnimationVerification
    {
        const string Key="IronRequiem.T07AnimationVerification";
        static TestRunnerApi api;
        static readonly Results callbacks=new Results();
        static readonly MethodInfo active=typeof(TestRunnerApi).GetMethod("IsRunActive",BindingFlags.Static|BindingFlags.NonPublic);
        static bool Busy=>active!=null && (bool)active.Invoke(null,null);
        [Serializable] class Saved {public string[] paths;public bool[] loaded,current;}
        static T07AnimationVerification(){EditorApplication.update+=Tick;if(SessionState.GetString(Key,"")!="")EditorApplication.delayCall+=Register;}
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(callbacks);}}
        static void Tick()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Busy)return;
            string phase=SessionState.GetString(Key,"");
            if(phase=="editPending")
            {
                SessionState.SetString(Key,"edit");Register();
                bool full=SessionState.GetBool(Key+".full",false);
                var filter=full?new Filter{testMode=TestMode.EditMode,assemblyNames=new[]{"IronRequiem.EditModeTests"}}:
                    new Filter{testMode=TestMode.EditMode,testNames=new[]{"IronRequiem.Tests.T07AnimationAssetTests","IronRequiem.Tests.T07OptimizationAssetTests"}};
                api.Execute(new ExecutionSettings(filter));
            }
            else if(phase=="restore")
            {
                SessionState.SetString(Key,"");
                var saved=JsonUtility.FromJson<Saved>(SessionState.GetString(Key+".scenes",""));
                EditorSceneManager.RestoreSceneManagerSetup(Enumerable.Range(0,saved.paths.Length).Select(i=>new SceneSetup{path=saved.paths[i],isLoaded=saved.loaded[i],isActive=saved.current[i]}).ToArray());
                IronRequiemSetupTools.WriteAudit();
            }
            else if(File.Exists("Tools/IronRequiem/Requests/t07-animation-tests.json"))
            {
                string request=File.ReadAllText("Tools/IronRequiem/Requests/t07-animation-tests.json");File.Delete("Tools/IronRequiem/Requests/t07-animation-tests.json");Run(request.Contains("regression"));
            }
        }
        public static void Run(bool full)
        {
            if(Busy || EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor is busy.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("User scene has changes.");
            var setup=EditorSceneManager.GetSceneManagerSetup();SessionState.SetString(Key+".scenes",JsonUtility.ToJson(new Saved{paths=setup.Select(s=>s.path).ToArray(),loaded=setup.Select(s=>s.isLoaded).ToArray(),current=setup.Select(s=>s.isActive).ToArray()}));
            SessionState.SetBool(Key+".full",full);SessionState.SetString(Key,"play");Register();
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            var filter=full?new Filter{testMode=TestMode.PlayMode,assemblyNames=new[]{"IronRequiem.PlayModeTests"}}:
                new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.T07AnimationBehaviorTests"}};
            api.Execute(new ExecutionSettings(filter));
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor r){}
            public void RunFinished(ITestResultAdaptor result)
            {
                string phase=SessionState.GetString(Key,"");if(phase!="play" && phase!="edit")return;
                string prefix=SessionState.GetBool(Key+".full",false)?"regression-":"";
                TestRunnerApi.SaveResultToFile(result,T07AnimationBuilder.Reports+"/"+prefix+phase+".xml");
                File.WriteAllText(T07AnimationBuilder.Reports+"/test-status.txt",prefix+phase+": "+result.ResultState+", passed="+result.PassCount+", failed="+result.FailCount);
                SessionState.SetString(Key,phase=="play"?"editPending":"restore");
            }
        }
    }
}
