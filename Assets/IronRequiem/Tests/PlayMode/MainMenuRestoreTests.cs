using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
    public sealed class MainMenuRestoreTests
    {
        const string Scene="Assets/FPS/Scenes/IntroMenu.unity",Report="Tools/IronRequiem/Reports/MainMenuRestore";
        bool background;float capture;
        UnityEngine.InputSystem.Mouse mouse;
        void Screenshot(string name,int width=1280,int height=720)
        {
            var camera=Camera.main;var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();int mask=camera.cullingMask;camera.cullingMask|=1<<5;
            var target=RenderTexture.GetTemporary(width,height,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            try{camera.targetTexture=target;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;}Canvas.ForceUpdateCanvases();foreach(var fit in Object.FindObjectsByType<ScreenFitPanel>())fit.Refit();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Report+"/"+name+".png",texture.EncodeToPNG());Object.Destroy(texture);}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}camera.cullingMask=mask;Canvas.ForceUpdateCanvases();}
        }
        static T Field<T>(Object owner,string name)where T:Object=>new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
        IEnumerator Load(){EditorSceneManager.LoadSceneInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;yield return new WaitForSecondsRealtime(2.2f);}
        [UnityTest]public IEnumerator CapsuleOptionsBackgroundAndSingleLaunch()
        {
            background=Application.runInBackground;Application.runInBackground=true;capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;Time.timeScale=1;AudioListener.pause=false;Directory.CreateDirectory(Report);
            yield return Load();var sequence=Object.FindAnyObjectByType<HangarLaunchSequence>();var robot=Object.FindAnyObjectByType<CapsuleRobotIdle>();var view=Object.FindAnyObjectByType<RequiemMenuView>();var patrol=Object.FindAnyObjectByType<BridgePatrol>();Assert.That(sequence&&robot&&view&&patrol,Is.True);Assert.That(robot.IsDark,Is.True);Assert.That(MenuLaunchRegistry.Active,Is.SameAs(sequence));
            Screenshot("main");
            int kills=patrol.Kills;var button=Field<Button>(view,"optionsButton");mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();var pointer=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=pointer});yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=pointer}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=pointer});yield return null;yield return null;Assert.That(view.IsOptionsOpen,Is.True);Assert.That(patrol.Kills,Is.EqualTo(kills));Assert.That(Object.FindObjectsByType<MenuCameraRig>().Length,Is.EqualTo(2),"Existing primary and CCTV rigs are preserved");
            Screenshot("options");Screenshot("options-1920",1920,1080);Screenshot("options-1024",1024,768);var slider=Field<Slider>(view,"sensitivity");float initial=slider.value;slider.value=Mathf.Min(slider.maxValue,initial+.1f);yield return null;view.ShowHome();yield return null;view.OpenOptions();Assert.That(slider.value,Is.EqualTo(Mathf.Min(slider.maxValue,initial+.1f)).Within(.001f));slider.value=initial;view.ShowHome();yield return null;Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(Field<Button>(view,"primary").gameObject));
            patrol.DebugDetonateFirst();yield return null;Assert.That(patrol.Kills,Is.EqualTo(kills+1),"Existing background interaction remains available");
            int callbacks=0;sequence.Play(()=>callbacks++);sequence.Play(()=>callbacks++);yield return new WaitForSecondsRealtime(2.5f);Assert.That(robot.IsAwake,Is.True);yield return new WaitForSecondsRealtime(2);Assert.That(callbacks,Is.EqualTo(1));
            yield return Load();Assert.That(Object.FindAnyObjectByType<CapsuleRobotIdle>().IsDark,Is.True);Assert.That(Object.FindAnyObjectByType<BridgePatrol>().Kills,Is.Zero);Assert.That(Object.FindObjectsByType<HangarLaunchSequence>().Length,Is.EqualTo(1));Assert.That(Object.FindObjectsByType<AudioListener>().Length,Is.EqualTo(1));
            view=Object.FindAnyObjectByType<RequiemMenuView>();int loads=0;void Loaded(UnityEngine.SceneManagement.Scene s,LoadSceneMode mode){if(s.name=="RequiemCampaign")loads++;}SceneManager.sceneLoaded+=Loaded;try{var play=Field<Button>(view,"primary");play.onClick.Invoke();play.onClick.Invoke();float end=Time.realtimeSinceStartup+35;while(loads==0&&Time.realtimeSinceStartup<end)yield return null;Assert.That(loads,Is.EqualTo(1));yield return null;}finally{SceneManager.sceneLoaded-=Loaded;}
            // Exercise the existing gameplay return interface, without editing its implementation.
            var session=Object.FindAnyObjectByType<CombatLabMenu>();Assert.That(session,Is.Not.Null);session.ReturnToMainMenu();float backEnd=Time.realtimeSinceStartup+30;while(!Object.FindAnyObjectByType<HangarLaunchSequence>()&&Time.realtimeSinceStartup<backEnd)yield return null;yield return new WaitForSecondsRealtime(2.2f);Assert.That(Object.FindObjectsByType<HangarLaunchSequence>().Length,Is.EqualTo(1));Assert.That(Object.FindAnyObjectByType<CapsuleRobotIdle>().IsDark,Is.True);
            File.WriteAllText(Report+"/play.txt","Capsule idle, animated intro, options buttons/settings/focus, no background kill from UI, power-up and guarded callback, actual JUGAR loads campaign once, menu reload has one sequence/listener and returns dark. No desktop or project settings changes.");
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(mouse!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);Time.timeScale=1;AudioListener.pause=false;var empty=SceneManager.CreateScene("MenuRestoreCleanup");SceneManager.SetActiveScene(empty);foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=empty&&s.isLoaded).OrderBy(s=>s.name=="RequiemCampaign"?1:0).ToArray())yield return SceneManager.UnloadSceneAsync(scene);Application.runInBackground=background;Time.captureDeltaTime=capture;Cursor.lockState=CursorLockMode.None;}
    }
}
