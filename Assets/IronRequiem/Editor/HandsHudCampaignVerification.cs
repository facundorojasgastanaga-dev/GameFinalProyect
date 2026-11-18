using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    [InitializeOnLoad]
    public static class HandsHudCampaignVerification
    {
        const string Report="Tools/IronRequiem/Reports/HandsHudCampaign";
        const string Key="IR.HandsHud.Tests";
        static TestRunnerApi api;
        static HandsHudCampaignVerification()
        {
            Application.logMessageReceived+=RecordTestError;
            if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;
        }
        static void RecordTestError(string message,string stack,LogType type)
        {
            if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
                File.AppendAllText(Report+"/test-errors.txt",$"{type}: {message}\n{stack}\n");
        }
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Run pose tests")]
        public static void PoseTests()=>Run("poses",TestMode.EditMode,"IronRequiem.Tests.HandPoseControllerTests");
        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Run campaign tests")]
        public static void CampaignTests()=>Run("campaign",TestMode.PlayMode,"IronRequiem.Tests.CampaignTests","IronRequiem.Tests.HangarIntroPresentationTests");
        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Run presentation tests")]
        public static void PresentationTests()=>Run("presentation",TestMode.PlayMode,"IronRequiem.Tests.CampaignTests.PresentationHandsMountedArmsAndOverdriveHud");
        static void Run(string name,TestMode mode,params string[] tests)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving dirty scene.");
            Directory.CreateDirectory(Report);File.WriteAllText(Report+"/test-errors.txt","");
            // Historical errors remain in Editor.log and our diagnostic reports; start a fresh console observation.
            var entries=typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            entries?.GetMethod("Clear",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public)?.Invoke(null,null);
            SessionState.SetString(Key+".phase",name);SessionState.SetBool(Key,true);Register();
            if(mode==TestMode.PlayMode)
            {
                SessionState.SetString(Key+".startScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Key+".restoreStartScene",true);EditorSceneManager.playModeStartScene=null;
            }
            api.Execute(new ExecutionSettings(new Filter{testMode=mode,testNames=tests}));
        }
        static void RestoreStartScene()
        {
            if(!SessionState.GetBool(Key+".restoreStartScene",false))return;
            SessionState.EraseBool(Key+".restoreStartScene");string path=SessionState.GetString(Key+".startScene","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
        sealed class Results:IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor test){}public void TestStarted(ITestAdaptor test){}public void TestFinished(ITestResultAdaptor result){}
            public void OnError(string message){SessionState.EraseBool(Key);RestoreStartScene();File.WriteAllText(Report+"/test-runner-error.txt",message);}
            public void RunFinished(ITestResultAdaptor result)
            {
                if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);string phase=SessionState.GetString(Key+".phase","");
                TestRunnerApi.SaveResultToFile(result,Report+"/"+phase+"-tests.xml");
                File.WriteAllText(Report+"/"+phase+"-tests-status.txt",$"{result.ResultState}; passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}");
                RestoreStartScene();
            }
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Capture HUD resolutions")]
        public static void CaptureHud()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required.");
            var player=UnityEngine.Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();
            var carrier=player.GetComponent<StolenWeaponCarrier>();
            string tag=carrier&&carrier.Held&&carrier.Held.Overdrive.IsArmed?"overdrive":player.GetComponent<Unity.FPS.Game.Health>().GetRatio()<.5f?"damaged":"healthy";
            var sizes=new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440)};
            // Reuse the established UI capture path: base camera owns the temporary screen-space canvases.
            // This intentionally excludes the viewmodel overlay; gameplay pose captures verify arms separately.
            var data=player.PlayerCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post=data&&data.renderPostProcessing;
            try
            {
                // Actual overlay UI bypasses post-processing. Match that behavior in the offscreen capture.
                if(data)data.renderPostProcessing=false;
                HangarIntroDebugV5Ui.Capture("classic-hud-"+tag,sizes);
            }
            finally{if(data)data.renderPostProcessing=post;}
            string source="Tools/IronRequiem/Bridge/v5ui/"+EditorPrefs.GetString("IR_V5UiTag","after");
            Directory.CreateDirectory(Report);
            foreach(var size in sizes)
                File.Copy(source+"/classic-hud-"+tag+"_"+size.x+"x"+size.y+".png",Report+"/hud-"+tag+"-"+size.x+"x"+size.y+".png",true);
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Inspect runtime visuals")]
        public static void InspectRuntimeVisuals()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required.");
            var report=new System.Text.StringBuilder();
            foreach(var graphic in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None))
            {
                if(!(graphic is RetroDigitsGraphic)&&!(graphic is RobotPortraitGraphic))continue;
                var mesh=graphic.canvasRenderer.GetMesh();
                if(!mesh){report.AppendLine(graphic.GetType().Name+"/"+graphic.transform.parent.name+": no mesh");continue;}
                report.AppendLine($"{graphic.GetType().Name}/{graphic.transform.parent.name}: rect={graphic.rectTransform.rect}; color={graphic.color}; inheritedAlpha={graphic.canvasRenderer.GetInheritedAlpha()}; culled={graphic.canvasRenderer.cull}; depth={graphic.depth}; vertices={mesh.vertexCount}; bounds={mesh.bounds}; shader={graphic.materialForRendering.shader.name}");
                if(mesh.colors32.Length>0)report.AppendLine("  vertexColor="+mesh.colors32[0]);
            }
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas))
                report.AppendLine($"Canvas {canvas.name}: size={((RectTransform)canvas.transform).rect.size}; factor={canvas.scaleFactor}; render={canvas.renderMode}; active={canvas.gameObject.activeInHierarchy}");
            Directory.CreateDirectory(Report);File.WriteAllText(Report+"/runtime-visuals.txt",report.ToString());
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Damage portrait preview")]
        public static void DamagePortrait()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required.");
            var player=UnityEngine.Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();var health=player.GetComponent<Unity.FPS.Game.Health>();
            var source=new GameObject("HUDDamageDirectionProbe");source.transform.position=player.transform.position-player.transform.right*3;
            bool previous=health.Invincible;health.Invincible=false;health.TakeDamage(Mathf.Max(0,health.CurrentHealth-health.MaxHealth*.2f),source);health.Invincible=previous;
            UnityEngine.Object.Destroy(source,2);
        }
    }
}
