using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    /// <summary>Exercises the menu through virtual devices without operating the user's desktop.</summary>
    public sealed class MenuPolishTests
    {
        const string Reports = "Tools/IronRequiem/Reports/MenuPolish";
        RequiemMenuView view;
        Camera camera;
        Keyboard keys;
        Mouse mouse;
        Gamepad pad;
        bool background;
        float capture;
        string output;
        readonly List<InputActionAsset> actionAssets = new List<InputActionAsset>();
        readonly List<UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>?> previousDevices = new List<UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>?>();

        static T Field<T>(Object owner, string name) where T : Object => new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
        static IEnumerator Frames(int count = 3) { for (int i = 0; i < count; i++) yield return null; }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            background = Application.runInBackground; capture = Time.captureDeltaTime;
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false;
            keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); pad = InputSystem.AddDevice<Gamepad>();
            string scene = File.ReadAllText(Reports + "/scene.txt").Trim();
            string label = File.ReadAllText(Reports + "/label.txt").Trim();
            output = Reports + "/" + label; Directory.CreateDirectory(output);
            EditorSceneManager.LoadSceneInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames();
            actionAssets.Clear(); previousDevices.Clear();
            if (InputSystem.actions) actionAssets.Add(InputSystem.actions);
            foreach (var module in Object.FindObjectsByType<InputSystemUIInputModule>())
                if (module.actionsAsset && !actionAssets.Contains(module.actionsAsset)) actionAssets.Add(module.actionsAsset);
            foreach (var asset in actionAssets)
            {
                previousDevices.Add(asset.devices); asset.devices = new InputDevice[] { keys, mouse, pad };
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width - 2, 2) });
            yield return new WaitForSecondsRealtime(2.4f);
            view = Object.FindAnyObjectByType<RequiemMenuView>(); camera = Camera.main;
            Assert.That(view && camera, Is.True, "Existing animated main menu must be present.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            for (int i = 0; i < actionAssets.Count; i++) if (actionAssets[i]) actionAssets[i].devices = previousDevices[i];
            actionAssets.Clear(); previousDevices.Clear();
            if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse); if (pad != null) InputSystem.RemoveDevice(pad);
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; Application.runInBackground = background;
            var empty = SceneManager.CreateScene("MenuPolishCleanup"); SceneManager.SetActiveScene(empty);
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != empty && s.isLoaded).ToArray();
            foreach (var scene in loaded) yield return SceneManager.UnloadSceneAsync(scene);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator A_BaselineCapturesExistingMainAndOptions()
        {
            yield return CaptureAll("main", false); view.OpenOptions(); yield return new WaitForSecondsRealtime(.3f); yield return CaptureAll("options", false);
            WriteInventory();
        }

        [UnityTest]
        public IEnumerator B_LayoutAndExistingAnimatedBackground()
        {
            var sequence = Object.FindAnyObjectByType<HangarLaunchSequence>();
            var robot = Object.FindAnyObjectByType<CapsuleRobotIdle>();
            var walker = Object.FindObjectsByType<BridgeWalker>().FirstOrDefault(w => w.Alive);
            Assert.That(sequence && robot && walker, Is.True); Assert.That(robot.IsDark, Is.True);
            Vector3 robotPosition = walker.transform.position, cameraPosition = camera.transform.position;
            yield return Frames(50);
            Assert.That(Vector3.Distance(robotPosition, walker.transform.position), Is.GreaterThan(.01f), "Background robot must actually move.");
            Assert.That(Vector3.Distance(cameraPosition, camera.transform.position), Is.GreaterThan(.00001f), "Existing camera motion must remain active.");
            yield return CaptureAll("main", true);
            view.OpenOptions(); yield return new WaitForSecondsRealtime(.3f); yield return CaptureAll("options", true);
            view.ShowHome(); yield return Frames();
            WriteInventory();
        }

        [UnityTest]
        public IEnumerator C_KeyboardAndGamepadNavigateSubmitCancelWithoutLostFocus()
        {
            var primary = Field<Button>(view, "primary"); var options = Field<Button>(view, "optionsButton");
            view.ShowHome(); yield return Frames();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(primary.gameObject));
            yield return PressKey(Key.DownArrow); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.gameObject));
            yield return PressKey(Key.Enter); Assert.That(view.IsOptionsOpen, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Field<Slider>(view, "volume").gameObject));
            yield return PressKey(Key.Escape); Assert.That(view.IsOptionsOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.gameObject), "Return restores the originating options button.");
            view.ShowHome(); primary.Select(); yield return Frames();
            yield return PressPad(new GamepadState().WithButton(GamepadButton.DpadDown));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.gameObject));
            yield return PressPad(new GamepadState().WithButton(GamepadButton.South)); Assert.That(view.IsOptionsOpen, Is.True);
            yield return PressPad(new GamepadState().WithButton(GamepadButton.East)); Assert.That(view.IsOptionsOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.gameObject));
            File.WriteAllText(output + "/navigation.txt", "Virtual keyboard Down/Enter/Escape and virtual gamepad dpad/A/B traversed existing home/options flow. Initial and restored focus checked. No physical-device or desktop automation.\n");
        }

        [UnityTest]
        public IEnumerator D_PointerOptionsBackQuitAndSettings()
        {
            var patrol = Object.FindAnyObjectByType<BridgePatrol>(); int kills = patrol.Kills;
            yield return Click(Field<Button>(view, "optionsButton")); Assert.That(view.IsOptionsOpen, Is.True);
            Assert.That(patrol.Kills, Is.EqualTo(kills), "UI clicks must not explode background robots.");
            var volume = Field<Slider>(view, "volume"); float initial = volume.value;
            try
            {
                volume.value = Mathf.Clamp01(initial > .5f ? initial - .1f : initial + .1f); yield return Frames();
                float changed = volume.value;
                yield return Click(Field<Button>(view, "optionsBack")); Assert.That(view.IsOptionsOpen, Is.False);
                yield return Click(Field<Button>(view, "optionsButton")); Assert.That(volume.value, Is.EqualTo(changed).Within(.001f));
            }
            finally { volume.value = initial; }
            yield return Click(Field<Button>(view, "optionsBack")); Assert.That(view.IsOptionsOpen, Is.False);
            LogAssert.Expect(LogType.Log, "SALIR: cierre de aplicación omitido en el Editor.");
            yield return Click(Field<Button>(view, "quitButton"));
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(AudioListener.pause, Is.False);
            File.WriteAllText(output + "/pointer-settings-quit.txt", "Virtual mouse opens options and returns. Volume change persists across reopen and is restored to its prior value. SALIR invokes existing Editor log branch; a standalone executable was not closed. UI clicks did not kill background robots.\n");
        }

        [UnityTest]
        public IEnumerator E_PlayLoadsConfiguredSceneExactlyOnce()
        {
            var loader = Field<LoadSceneButton>(view, "sceneLoader"); string target = loader.SceneName;
            var sequence = Object.FindAnyObjectByType<HangarLaunchSequence>();
            int loads = 0; void Loaded(Scene scene, LoadSceneMode mode) { if (scene.name == target) loads++; }
            SceneManager.sceneLoaded += Loaded;
            try
            {
                var play = Field<Button>(view, "primary");
                yield return Click(play);
                Assert.That(sequence.IsPlaying, Is.True); Assert.That(play.interactable, Is.False);
                yield return Click(play);
                float deadline = Time.realtimeSinceStartup + 75;
                while (loads == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(loads, Is.EqualTo(1), "The existing configured destination must load exactly once.");
                yield return Frames(3); Assert.That(loads, Is.EqualTo(1));
                var campaign = Object.FindAnyObjectByType<CampaignFlow>();
                Assert.That(campaign, Is.Not.Null);
                Assert.That(campaign.gameObject.scene.name, Is.EqualTo(target));
                string firstRoom = campaign.StageScenes[0];
                while (SceneManager.GetActiveScene().name != firstRoom && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(firstRoom));
                Assert.That(loads, Is.EqualTo(1));
                File.WriteAllText(output + "/launch.txt", "Two virtual mouse clicks on JUGAR. One load of " + target + ". Active first room: " + firstRoom + ".\n");
            }
            finally { SceneManager.sceneLoaded -= Loaded; }
        }

        [UnityTest]
        public IEnumerator F_ButtonStatesKeepHitRectangleAndSettledPresentationAllocatesNothing()
        {
            var primary = Field<Button>(view, "primary"); var options = Field<Button>(view, "optionsButton");
            var fx = options.GetComponent<MenuPolishButtonFx>();
            Assert.That(fx, Is.Not.Null, "Polished presentation must be installed only on the menu copy.");
            var rect = (RectTransform)options.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 size = rect.rect.size; Vector3 position = rect.position, scale = rect.localScale;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width - 2, 2) });
            primary.Select(); yield return Frames(16); Assert.That(fx.State, Is.EqualTo(MenuPolishButtonFx.VisualState.Rest));
            options.Select(); yield return Frames(16); Assert.That(fx.State, Is.EqualTo(MenuPolishButtonFx.VisualState.Selected));
            var pointer = RectTransformUtility.WorldToScreenPoint(null, options.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }); yield return Frames(16);
            Assert.That(fx.State, Is.EqualTo(MenuPolishButtonFx.VisualState.Hover));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left)); yield return Frames();
            Assert.That(fx.State, Is.EqualTo(MenuPolishButtonFx.VisualState.Pressed));
            // Disable before release so this presentation check does not activate Options.
            options.interactable = false; yield return Frames(); Assert.That(fx.State, Is.EqualTo(MenuPolishButtonFx.VisualState.Disabled));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }); yield return Frames();
            Assert.That(rect.rect.size, Is.EqualTo(size)); Assert.That(rect.position, Is.EqualTo(position)); Assert.That(rect.localScale, Is.EqualTo(scale));
            options.interactable = true;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width - 2, 2) }); primary.Select(); yield return Frames(20);
            var controller = Object.FindAnyObjectByType<MainMenuPolish>(); Assert.That(controller, Is.Not.Null);
            Action tickFx = (Action)Delegate.CreateDelegate(typeof(Action), fx, typeof(MenuPolishButtonFx).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic));
            Action tickMenu = (Action)Delegate.CreateDelegate(typeof(Action), controller, typeof(MainMenuPolish).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic));
            for (int i = 0; i < 20; i++) { tickFx(); tickMenu(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { tickFx(); tickMenu(); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero, "Steady menu presentation callbacks should allocate no managed memory.");
            File.WriteAllText(output + "/presentation-performance.txt", "Rest, selected, hover, held press and disabled tested through real EventSystem selection/virtual pointer. Button position, size and scale remain unchanged. 1000 warmed Update calls for the menu focus/entry component and one settled button: " + bytes + " managed bytes. Excludes Canvas rebuild/rendering, other menu components and full-frame allocations. Editor only; no Player FPS benchmark.\n");
        }

        IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(keys, new KeyboardState(key)); yield return Frames(3);
            InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return Frames(3);
        }
        IEnumerator PressPad(GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state); yield return Frames(3);
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return Frames(3);
        }
        IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases(); var position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return Frames(3);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return Frames(2);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return Frames(3);
        }

        IEnumerator CaptureAll(string name, bool validate)
        {
            // Capture the actual GameView first, before touching its resolution. Overlay UI remains native.
            yield return Capture(name + "-native", 0, 0, false);
            using (var sizes = new GameViewCaptureSizes(output))
            {
                int[] widths = { 1280, 1920, 2560, 3440 }, heights = { 720, 1080, 1440, 1440 };
                string[] labels = { "720p", "1080p", "1440p", "ultrawide" };
                for (int i = 0; i < widths.Length; i++)
                {
                    sizes.Set(widths[i], heights[i]);
                    for (int frame = 0; frame < 8; frame++)
                    {
                        sizes.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                    }
                    yield return new WaitForSecondsRealtime(.15f);
                    Canvas.ForceUpdateCanvases(); foreach (var fit in Object.FindObjectsByType<ScreenFitPanel>()) fit.Refit(); Canvas.ForceUpdateCanvases();
                    sizes.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                    yield return Capture(name + "-" + labels[i], widths[i], heights[i], validate);
                }
            }
        }

        IEnumerator Capture(string name, int width, int height, bool validate)
        {
            string path = Path.GetFullPath(output + "/" + name + ".png");
            if (File.Exists(path)) File.Delete(path);
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
            Assert.That(gameView, Is.Not.Null, "Native capture requires an existing GameView.");
            ScreenCapture.CaptureScreenshot(path);
            byte[] bytes = null;
            float deadline = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < deadline)
            {
                gameView.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
                if (File.Exists(path))
                {
                    try
                    {
                        var candidate = File.ReadAllBytes(path);
                        // A complete PNG ends with the zero-length IEND chunk and its CRC.
                        if (candidate.Length > 32 && candidate[0] == 137 && candidate[1] == 80 && candidate[2] == 78 && candidate[3] == 71 &&
                            candidate[candidate.Length - 8] == 73 && candidate[candidate.Length - 7] == 69 &&
                            candidate[candidate.Length - 6] == 78 && candidate[candidate.Length - 5] == 68 &&
                            candidate[candidate.Length - 4] == 174 && candidate[candidate.Length - 3] == 66 &&
                            candidate[candidate.Length - 2] == 96 && candidate[candidate.Length - 1] == 130)
                        { bytes = candidate; break; }
                    }
                    catch (IOException) { /* The asynchronous screenshot writer still owns the file. */ }
                }
                yield return null;
            }
            Assert.That(bytes, Is.Not.Null, "Native GameView screenshot did not finish within 10 seconds: " + path);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                Assert.That(texture.LoadImage(bytes), Is.True, "Native screenshot PNG must decode without altering the file.");
                File.AppendAllText(output + "/capture-diagnostics.txt", name + ": Screen=" + Screen.width + "x" + Screen.height +
                    ", texture=" + texture.width + "x" + texture.height + ", frame=" + Time.frameCount + ", overlayCanvases=" +
                    Object.FindObjectsByType<Canvas>().Count(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) + "\n");
                if (width > 0)
                {
                    Assert.That(texture.width, Is.EqualTo(width), "Native GameView screenshot must match requested resolution.");
                    Assert.That(texture.height, Is.EqualTo(height), "Native GameView screenshot must match requested resolution.");
                    Assert.That(Screen.width, Is.EqualTo(width)); Assert.That(Screen.height, Is.EqualTo(height));
                    if (validate) ValidateLayout(width, height);
                }
            }
            finally { Object.Destroy(texture); }
        }

        /// <summary>Changes only the existing Editor GameView's resolution backend; never opens or focuses a window.</summary>
        sealed class GameViewCaptureSizes : IDisposable
        {
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            readonly EditorWindow window;
            readonly PropertyInfo selected;
            readonly object group;
            readonly ConstructorInfo constructor;
            readonly object fixedResolution;
            readonly MethodInfo add, remove, total;
            readonly int originalIndex;
            readonly int originalCount;
            readonly string diagnostics;
            readonly List<int> added = new List<int>();

            public GameViewCaptureSizes(string reportDirectory)
            {
                diagnostics = reportDirectory + "/gameview-restoration.txt";
                var gameView = FindType("UnityEditor.GameView");
                window = Resources.FindObjectsOfTypeAll(gameView).OfType<EditorWindow>().FirstOrDefault();
                Assert.That(window, Is.Not.Null, "An existing GameView is required; tests never open or focus one.");
                selected = gameView.GetProperty("selectedSizeIndex", Instance);
                if (selected == null || !selected.CanWrite) throw new InvalidOperationException("Installed GameView has no writable selectedSizeIndex.");
                originalIndex = (int)selected.GetValue(window);
                var sizesType = FindType("UnityEditor.GameViewSizes");
                var singleton = sizesType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                if (singleton == null) throw new InvalidOperationException("Installed GameViewSizes singleton is unavailable.");
                var sizes = singleton.GetValue(null);
                var groupType = FindType("UnityEditor.GameViewSizeGroupType");
                var currentGroup = sizesType.GetProperty("currentGroupType", Instance);
                var groupValue = currentGroup != null ? currentGroup.GetValue(sizes) : Enum.Parse(groupType, "Standalone");
                group = sizesType.GetMethod("GetGroup", Instance, null, new[] { groupType }, null).Invoke(sizes, new[] { groupValue });
                var sizeType = FindType("UnityEditor.GameViewSizeType");
                fixedResolution = Enum.Parse(sizeType, "FixedResolution");
                var size = FindType("UnityEditor.GameViewSize");
                constructor = size.GetConstructor(Instance, null, new[] { sizeType, typeof(int), typeof(int), typeof(string) }, null);
                add = group.GetType().GetMethod("AddCustomSize", Instance); remove = group.GetType().GetMethod("RemoveCustomSize", Instance);
                total = group.GetType().GetMethod("GetTotalCount", Instance);
                var builtins = group.GetType().GetMethod("GetBuiltinCount", Instance);
                var getSize = group.GetType().GetMethod("GetGameViewSize", Instance, null, new[] { typeof(int) }, null);
                if (constructor == null || add == null || remove == null || total == null || builtins == null || getSize == null)
                    throw new InvalidOperationException("Installed GameView resolution API differs; no resolution was changed.");
                File.AppendAllText(diagnostics, "Before cleanup: count=" + total.Invoke(group, null) + ", selected=" + originalIndex + "\n");
                // Older interrupted attempts left only our named temporary sizes. Preserve every user/built-in size.
                for (int index = (int)total.Invoke(group, null) - 1, first = (int)builtins.Invoke(group, null); index >= first; index--)
                {
                    var entry = getSize.Invoke(group, new object[] { index });
                    string caption = SizeCaption(entry);
                    if (!caption.StartsWith("IR_MenuCapture_", StringComparison.Ordinal)) continue;
                    remove.Invoke(group, new object[] { index });
                    if (index < originalIndex) originalIndex--;
                    else if (index == originalIndex) originalIndex = Math.Max(0, index - 1);
                    File.AppendAllText(diagnostics, "Removed owned orphan global=" + index + ": " + caption + "\n");
                }
                originalCount = (int)total.Invoke(group, null);
                originalIndex = Mathf.Clamp(originalIndex, 0, Math.Max(0, originalCount - 1));
                selected.SetValue(window, originalIndex);
                File.AppendAllText(diagnostics, "After cleanup: count=" + originalCount + ", selected=" + originalIndex + "\n");
            }

            public void Set(int width, int height)
            {
                var size = constructor.Invoke(new[] { fixedResolution, (object)width, height, "IR_MenuCapture_" + width + "x" + height });
                add.Invoke(group, new[] { size });
                int globalIndex = (int)total.Invoke(group, null) - 1; added.Add(globalIndex);
                selected.SetValue(window, globalIndex); Repaint();
            }
            public void Repaint() => window.Repaint();
            public void Dispose()
            {
                try { if (window) selected.SetValue(window, originalIndex); }
                finally { for (int i = added.Count - 1; i >= 0; i--) remove.Invoke(group, new object[] { added[i] }); }
                if (window) window.Repaint();
                int count = (int)total.Invoke(group, null), index = window ? (int)selected.GetValue(window) : -1;
                File.AppendAllText(diagnostics, "After capture: count=" + count + ", selected=" + index + "; expected count=" + originalCount + ", selected=" + originalIndex + "\n");
                Assert.That(count, Is.EqualTo(originalCount), "Temporary GameView sizes must all be removed.");
                Assert.That(index, Is.EqualTo(originalIndex), "The user's original GameView selection must be restored.");
            }
            static string SizeCaption(object size)
            {
                var type = size.GetType();
                var property = type.GetProperty("baseText", Instance) ?? type.GetProperty("displayText", Instance);
                if (property != null) return property.GetValue(size) as string ?? "";
                var field = type.GetField("m_BaseText", Instance);
                if (field != null) return field.GetValue(size) as string ?? "";
                throw new InvalidOperationException("Cannot identify owned GameView sizes safely; no unnamed sizes will be removed.");
            }
            static Type FindType(string name)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { var type = assembly.GetType(name, false); if (type != null) return type; }
                throw new InvalidOperationException("Installed Editor does not expose " + name);
            }
        }

        void ValidateLayout(int width, int height)
        {
            foreach (var selectable in view.GetComponentsInChildren<Selectable>())
            {
                var corners = new Vector3[4]; ((RectTransform)selectable.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var pixel = RectTransformUtility.WorldToScreenPoint(null, corner);
                    Assert.That(pixel.x, Is.InRange(-2, width + 2), selectable.name + " horizontal bounds");
                    Assert.That(pixel.y, Is.InRange(-2, height + 2), selectable.name + " vertical bounds");
                }
            }
            foreach (var label in view.GetComponentsInChildren<TMP_Text>())
                if (label.enabled && label.color.a > .01f && !string.IsNullOrWhiteSpace(label.text))
                    Assert.That(label.isTextOverflowing, Is.False, label.name + " text overflow at " + width + "x" + height);
        }

        void WriteInventory()
        {
            var lines = new List<string>();
            foreach (var text in view.GetComponentsInChildren<TMP_Text>(true)) lines.Add(text.name + ": " + text.text.Replace('\n', '|'));
            lines.Add("destination=" + Field<LoadSceneButton>(view, "sceneLoader").SceneName);
            lines.Add("camera rigs=" + Object.FindObjectsByType<MenuCameraRig>().Length);
            lines.Add("audio listeners=" + Object.FindObjectsByType<AudioListener>().Length);
            File.WriteAllLines(output + "/inventory.txt", lines);
        }
    }
}
