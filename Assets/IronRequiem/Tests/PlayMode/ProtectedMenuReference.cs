using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    /// Reference captures of the protected main-menu zone (conveyor moving, click explosion, JUGAR sequence).
    /// Read-only: loads IntroMenu in Play Mode, never saves. Output folder label comes from
    /// Tools/IronRequiem/Reports/ProtectedZone/menu-label.txt (default "run").
    public sealed class ProtectedMenuReference
    {
        const string Scene = "Assets/FPS/Scenes/IntroMenu.unity", Root = "Tools/IronRequiem/Reports/ProtectedZone";
        string dir; bool background; float capture; Mouse mouse;

        void Shot(string name)
        {
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            int mask = camera.cullingMask; camera.cullingMask |= 1 << 5;
            var target = RenderTexture.GetTemporary(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .1f; }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(dir + "/" + name + ".png", tex.EncodeToPNG()); Object.Destroy(tex);
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                camera.cullingMask = mask; Canvas.ForceUpdateCanvases();
            }
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator MenuConveyorExplosionAndPlay()
        {
            string labelFile = Root + "/menu-label.txt";
            string label = File.Exists(labelFile) ? File.ReadAllText(labelFile).Trim() : "run";
            dir = Root + "/menu-" + label; Directory.CreateDirectory(dir);
            background = Application.runInBackground; Application.runInBackground = true;
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1;
            Random.InitState(1234);
            EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames(2); Random.InitState(1234);
            yield return Frames(120);
            var patrol = Object.FindAnyObjectByType<BridgePatrol>(); var sequence = Object.FindAnyObjectByType<HangarLaunchSequence>();
            Assert.That(patrol && sequence, Is.True);
            Shot("01-main"); yield return Frames(30); Shot("02-belt-+0.5s"); yield return Frames(30); Shot("03-belt-+1.0s");
            var walkers = Object.FindObjectsByType<BridgeWalker>().Where(w => w.Alive).ToArray();
            var log = new System.Text.StringBuilder("walkers alive=" + walkers.Length + "\n");
            int kills = patrol.Kills;
            // real click on the nearest visible walker, through the same mouse path the menu uses
            var cam = Camera.main; BridgeWalker pick = null; Vector3 sp = default;
            foreach (var w in walkers.OrderBy(w => Vector3.Distance(cam.transform.position, w.transform.position)))
            {
                var p = cam.WorldToScreenPoint(w.transform.position + Vector3.up * 1.2f);
                if (p.z > 0 && p.x > 0 && p.y > 0 && p.x < Screen.width && p.y < Screen.height) { pick = w; sp = p; break; }
            }
            mouse = InputSystem.AddDevice<Mouse>();
            if (pick)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = sp }); yield return Frames(3);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = sp }.WithButton(MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = sp }); yield return null;
            }
            if (patrol.Kills == kills) { log.AppendLine("click did not hit a walker; used DebugDetonateFirst"); patrol.DebugDetonateFirst(); yield return null; }
            else log.AppendLine("explosion by real click at " + sp);
            yield return Frames(4); Shot("04-explosion-+4f"); yield return Frames(12); Shot("05-explosion-+16f"); yield return Frames(30); Shot("06-explosion-+46f");
            log.AppendLine("kills " + kills + " -> " + patrol.Kills);
            int done = 0; sequence.Play(() => done++);
            float[] marks = { .5f, 1.5f, 2.5f, 4f }; float t = 0; int k = 0;
            while (k < marks.Length) { yield return null; t += 1f / 60; if (t >= marks[k]) { Shot("07-play-" + marks[k].ToString("0.0") + "s"); k++; } }
            yield return Frames(60);
            log.AppendLine("play callback=" + done + " (the campaign itself is not loaded here)");
            File.WriteAllText(dir + "/log.txt", log.ToString());
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1; Time.captureDeltaTime = capture; Application.runInBackground = background; AudioListener.pause = false;
            var empty = SceneManager.CreateScene("ProtectedMenuCleanup"); SceneManager.SetActiveScene(empty);
            foreach (var s in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != empty && s.isLoaded).ToArray())
                yield return SceneManager.UnloadSceneAsync(s);
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
