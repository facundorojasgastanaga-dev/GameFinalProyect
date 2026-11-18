using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class HangarIntroPresentationTests
    {
        Keyboard keyboard;bool background;float capture;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        CampaignFlow flow;HangarIntroCinematic intro;
        const string Main="Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        [UnitySetUp]public IEnumerator Setup()
        {
            background=Application.runInBackground;capture=Time.captureDeltaTime;Application.runInBackground=true;Time.captureDeltaTime=1f/60;Time.timeScale=1;
            devices=InputSystem.actions.devices;keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keyboard};
            yield return BeginCampaign();
        }
        IEnumerator BeginCampaign()
        {
            EditorSceneManager.LoadSceneInPlayMode(Main,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            flow=Object.FindAnyObjectByType<CampaignFlow>();Assert.That(flow,Is.Not.Null);
            yield return Until(()=>!flow.Busy&&flow.CurrentRoom&&flow.CurrentRoom.Ready,90,"Hangar ready");flow.Menu.BeginGame();
            intro=Object.FindAnyObjectByType<HangarIntroCinematic>();Assert.That(intro,Is.Not.Null);
            yield return Until(()=>intro.IsPlaying,10,"Fresh cinematic starts");
        }
        static IEnumerator Until(Func<bool> condition,float seconds,string label)
        {
            float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(condition(),Is.True,label);
        }
        static void Shot(string tag)
        {
            SessionState.SetString("IR.HandsHud.CaptureTag",tag);var began=DateTime.UtcNow;
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/Iron Requiem/Hands HUD Campaign/Capture gameplay state"),Is.True);
            string path="Tools/IronRequiem/Reports/HandsHudCampaign/"+tag+".png";
            Assert.That(File.Exists(path)&&File.GetLastWriteTimeUtc(path)>=began.AddSeconds(-1),Is.True,"Fresh cinematic evidence: "+tag);
        }
        [UnityTest]public IEnumerator FullCinematicAndKeyboardSkipsConverge()
        {
            foreach(var sample in new[]{(16f,"cinematic-inspect"),(22.3f,"cinematic-shoulder-start"),(23.2f,"cinematic-shoulder-turn"),(24.4f,"cinematic-shoulder-end")})
            {
                yield return Until(()=>intro.Time01*intro.Duration>=sample.Item1,45,"Cinematic sample "+sample.Item1);
                yield return new WaitForEndOfFrame();Shot(sample.Item2);
            }
            yield return Until(()=>intro.Finished&&!PlayerInputLock.Locked,15,"Seen cinematic hands off");
            var seenPosition=flow.Player.transform.position;var seenRotation=flow.Player.transform.rotation;
            var pose=Object.FindAnyObjectByType<WakeUpRobotPose>();
            var seenFingers=pose.Model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Hand")&&t.name.EndsWith("1")).ToDictionary(t=>t.name,t=>t.localRotation);
            foreach(var key in new[]{Key.E,Key.Space})
            {
                yield return Drain();yield return BeginCampaign();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Until(()=>intro.Finished,5,"Hold "+key+" skips");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.That(intro.WasSkipped,Is.True);Assert.That(PlayerInputLock.Locked,Is.False);
                Assert.That(Vector3.Distance(flow.Player.transform.position,seenPosition),Is.LessThan(.06f));
                Assert.That(Quaternion.Angle(flow.Player.transform.rotation,seenRotation),Is.LessThan(.1f));
                var skipped=Object.FindAnyObjectByType<WakeUpRobotPose>();
                foreach(var bone in skipped.Model.GetComponentsInChildren<Transform>(true))
                    if(seenFingers.TryGetValue(bone.name,out var rotation))Assert.That(Quaternion.Angle(bone.localRotation,rotation),Is.LessThan(.1f),"Final finger "+bone.name);
                Assert.That(flow.CurrentRoom.Completed,Is.True);
            }
        }
        IEnumerator Drain()
        {
            Time.timeScale=1;var session=Object.FindAnyObjectByType<CampaignFlow>();if(session)yield return session.DrainPendingLoadsForShutdown();
            var clean=SceneManager.CreateScene("CinematicCleanup"+Time.frameCount);SceneManager.SetActiveScene(clean);
            var loaded=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=clean&&s.isLoaded).OrderBy(s=>s.name=="RequiemCampaign"?1:0).ToArray();
            foreach(var scene in loaded)if(scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            yield return Drain();Time.captureDeltaTime=capture;InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keyboard);Application.runInBackground=background;
        }
    }
}
