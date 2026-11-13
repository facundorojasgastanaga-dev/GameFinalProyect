using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace IronRequiem.Editor
{
    [InitializeOnLoad]public static class ProtagonistPlayVerification
    {
        const string Key="IRON.ProtagonistVerification";static TestRunnerApi api;
        static ProtagonistPlayVerification(){EditorApplication.update+=Tick;if(SessionState.GetString(Key,"")=="run")EditorApplication.delayCall+=Register;}
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        public static void Run()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving modified scene; verification was not started");
            Register();SessionState.SetString(Key,"run");api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.ProtagonistIntegrationTests"}}));
        }
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            const string request="Tools/IronRequiem/Requests/protagonist-verify.txt";
            if(File.Exists(request))
            {
                string op=File.ReadAllText(request).Trim();File.Delete(request);
                try{if(op=="finalize-test"){ProtagonistLabFinalizer.PreserveCameraOverrides();Run();}else if(op=="test")Run();else throw new InvalidOperationException("Unknown fixed request");}
                catch(Exception e){File.WriteAllText(ProtagonistIntegrationTools.Output+"/verification-error.txt",e.ToString());Debug.LogException(e);}return;
            }
            if(SessionState.GetString(Key,"")!="restore"||EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".when",0)<6)return;
            SessionState.EraseString(Key);EditorSceneManager.OpenScene(ProtagonistLabBuilder.Lab);IronRequiemSetupTools.WriteAudit();
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
            public void RunFinished(ITestResultAdaptor result)
            {
                if(SessionState.GetString(Key,"")!="run")return;
                TestRunnerApi.SaveResultToFile(result,ProtagonistIntegrationTools.Output+"/playmode-gui.xml");File.WriteAllText(ProtagonistIntegrationTools.Output+"/play-status.txt",result.ResultState+" passed="+result.PassCount+" failed="+result.FailCount);SessionState.SetFloat(Key+".when",(float)EditorApplication.timeSinceStartup);SessionState.SetString(Key,"restore");
            }
        }
    }
}
