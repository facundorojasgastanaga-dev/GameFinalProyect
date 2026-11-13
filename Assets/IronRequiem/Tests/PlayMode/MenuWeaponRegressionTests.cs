using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class MenuWeaponRegressionTests
    {
        const string Report = "Tools/IronRequiem/Reports/MenuWeaponRegression";
        PlayerCharacterController player; CombatLabMenu menu; StolenWeaponCarrier carrier;
        Keyboard keys; Mouse mouse; float capture, oldSensitivity; bool background, hadSensitivity;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        [UnitySetUp] public IEnumerator Setup()
        {
            Directory.CreateDirectory(Report); hadSensitivity = PlayerPrefs.HasKey(RequiemSettings.SensitivityKey); oldSensitivity = RequiemSettings.Sensitivity(); capture = Time.captureDeltaTime; background = Application.runInBackground;
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false; devices = InputSystem.actions.devices;
            keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CombatVarietyLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            menu = Object.FindAnyObjectByType<CombatLabMenu>(); player = Object.FindAnyObjectByType<PlayerCharacterController>(); player.enabled = false; player.GetComponent<Health>().Invincible = true; carrier = player.GetComponent<StolenWeaponCarrier>();
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); waves.enabled = false; if (waves.SpawnQueue) { waves.SpawnQueue.CancelPending(); waves.SpawnQueue.enabled = false; }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (hadSensitivity) PlayerPrefs.SetFloat(RequiemSettings.SensitivityKey, oldSensitivity); else PlayerPrefs.DeleteKey(RequiemSettings.SensitivityKey); PlayerPrefs.Save();
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("MenuWeaponCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }
        static UnityEngine.UI.Slider Sensitivity(RequiemMenuView view) => (UnityEngine.UI.Slider)new SerializedObject(view).FindProperty("sensitivity").objectReferenceValue;
        InGameMenuManager Pause => Object.FindObjectsByType<InGameMenuManager>().Single(p => p.UseIndustrialMenu && p.gameObject.activeInHierarchy);
        IEnumerator Escape() { InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Escape)); yield return null; yield return null; InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; }
        IEnumerator Until(Func<bool> condition, float timeout, string label) { float end = Time.time + timeout, real = Time.realtimeSinceStartup + 60; while (!condition() && Time.time < end && Time.realtimeSinceStartup < real) yield return null; Assert.That(condition(), Is.True, label); }
        static Rect ScreenBounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(null, p)).ToArray(); var min = points.Aggregate(Vector2.positiveInfinity, Vector2.Min); var max = points.Aggregate(Vector2.negativeInfinity, Vector2.Max); return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        static void Capture(Camera camera, string name)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active; var rt = new RenderTexture(1000, 650, 24); var image = new Texture2D(1000, 650, TextureFormat.RGB24, false);
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1000, 650), 0, 0); image.Apply(); File.WriteAllBytes(Report + "/" + name + ".png", image.EncodeToPNG()); }
            finally { camera.targetTexture = previous; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(image); }
        }
        IEnumerator EquippedBigBot(Action<StolenCannon> received)
        {
            menu.BeginGame(); foreach (var collider in Object.FindObjectsByType<Collider>()) if (!collider.transform.IsChildOf(player.transform) && !(collider.bounds.size.y < 2 && collider.bounds.size.x > 20 && collider.bounds.size.z > 20)) collider.enabled = false;
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false; player.transform.SetPositionAndRotation(new Vector3(-12, .03f, -10), Quaternion.identity); controller.enabled = true; player.PlayerCamera.transform.localRotation = Quaternion.identity;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/SecondaryShotgun/BigBot_Burst_v10_NavThrottle.prefab");
            Assert.That(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(-12, 0, -6), out var hit, 3, UnityEngine.AI.NavMesh.AllAreas), Is.True);
            var enemy = Object.Instantiate(prefab, hit.position, Quaternion.Euler(0, 180, 0)); yield return null; yield return null; enemy.GetComponent<BigBotCombat>().enabled = false; enemy.GetComponent<EnemyMobile>().enabled = false;
            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent.isOnNavMesh) agent.isStopped = true; agent.enabled = false;
            var source = enemy.GetComponent<EnemyExecutionSource>(); var health = enemy.GetComponent<Health>(); health.TakeDamage(health.MaxHealth * .3f, player.gameObject); Assert.That(source.TryReserve(), Is.True); var module = source.CreateReservedModule(); Assert.That(module, Is.Not.Null); source.Complete();
            // Isolate the module's render/LOD checks: its donor corpse otherwise blocks the camera.
            Object.Destroy(enemy);
            module.Body.linearVelocity = Vector3.zero; module.Body.angularVelocity = Vector3.zero; module.Body.isKinematic = true;
            module.transform.position += player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 1.2f - module.SolidCollider.bounds.center;
            Physics.SyncTransforms(); player.PlayerCamera.transform.LookAt(module.SolidCollider.bounds.center); yield return new WaitForSeconds(.4f); Physics.SyncTransforms(); player.PlayerCamera.transform.LookAt(module.SolidCollider.bounds.center); yield return null;
            File.WriteAllText(Report + "/pickup-probe.txt", "camera=" + player.PlayerCamera.transform.position + " forward=" + player.PlayerCamera.transform.forward + " bounds=" + module.SolidCollider.bounds + " state=" + module.State + " scale=" + module.transform.lossyScale + " canPickup=" + module.CanPickup + " status=" + carrier.PickupStatus + " rays=" + string.Join(";", Physics.RaycastAll(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward, 4, -1, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Select(h => h.collider.name + "@" + h.distance)));
            yield return Until(() => { player.PlayerCamera.transform.LookAt(module.SolidCollider.bounds.center); return carrier.Candidate == module; }, 3, "Real Big Bot module detected");
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.E)); yield return null; yield return null; InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return new WaitForSeconds(.8f); Assert.That(carrier.Held, Is.SameAs(module)); received(module);
        }
        [UnityTest] public IEnumerator BaselineCapture()
        {
            var report = new StringBuilder(); report.AppendLine("Menu music count=" + Object.FindObjectsByType<MenuMusic>().Length);
            Sensitivity(menu.GetComponent<RequiemMenuView>()).value = 3.2f; menu.BeginGame(); yield return null; yield return Escape(); var pause = Pause;
            report.AppendLine("Saved sensitivity=" + RequiemSettings.Sensitivity() + " actual=" + player.GetComponent<PlayerInputHandler>().LookSensitivity + " pause display=" + Sensitivity(pause.GetComponent<RequiemMenuView>()).value);
            report.AppendLine("Pause frame=" + ScreenBounds(pause.MenuRoot.transform.Find("IndustrialShell/ConsoleFrame") as RectTransform) + " screen=" + Screen.width + "x" + Screen.height);
            File.WriteAllText(Report + "/baseline.txt", report.ToString()); ScreenCapture.CaptureScreenshot(Path.GetFullPath(Report + "/baseline-pause.png")); yield return null; yield return Escape();
            StolenCannon module = null; yield return EquippedBigBot(value => module = value); Assert.That(carrier.ThrowHeld(), Is.True); yield return null;
            module.Body.linearVelocity = Vector3.zero; module.Body.angularVelocity = Vector3.zero; module.Body.isKinematic = true;
            module.transform.position += player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 3 - module.SolidCollider.bounds.center; Physics.SyncTransforms(); yield return null;
            var views = module.GetComponentsInChildren<MeshRenderer>(true); foreach (var renderer in views) report.AppendLine(renderer.name + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled + " forceOff=" + renderer.forceRenderingOff + " layer=" + renderer.gameObject.layer + " bounds=" + renderer.bounds + " visible=" + renderer.isVisible);
            foreach (var lod in module.GetComponentsInChildren<LODGroup>(true)) report.AppendLine("LOD size=" + lod.size + " scale=" + lod.transform.lossyScale + " reference=" + lod.localReferencePoint);
            Capture(player.PlayerCamera, "baseline-throw-auto"); foreach (var lod in module.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0); Capture(player.PlayerCamera, "baseline-throw-force0");
            File.WriteAllText(Report + "/baseline.txt", report.ToString());
        }
        [UnityTest] public IEnumerator MenuMusicSensitivityAndPauseFit()
        {
            var music = Object.FindAnyObjectByType<MenuMusic>(); Assert.That(music, Is.Not.Null); yield return new WaitForSecondsRealtime(.35f); var audio = music.GetComponent<AudioSource>(); Assert.That(audio.isPlaying && audio.ignoreListenerPause && audio.volume > .01f, Is.True);
            Sensitivity(menu.GetComponent<RequiemMenuView>()).value = 3.2f; menu.BeginGame(); yield return new WaitForSecondsRealtime(.6f); Assert.That(audio.isPlaying, Is.False, "Menu theme retires before combat");
            yield return Escape(); var pause = Pause; var view = pause.GetComponent<RequiemMenuView>(); Assert.That(Sensitivity(view).value, Is.EqualTo(3.2f).Within(.001f)); Assert.That(player.GetComponent<PlayerInputHandler>().LookSensitivity, Is.EqualTo(3.2f).Within(.001f));
            var bounds = ScreenBounds(pause.MenuRoot.transform.Find("IndustrialShell/ConsoleFrame") as RectTransform); Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Screen.width)); Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Screen.height));
            view.OpenOptions(); yield return null; Sensitivity(view).value = 1.8f; Assert.That(player.GetComponent<PlayerInputHandler>().LookSensitivity, Is.EqualTo(1.8f).Within(.001f)); ScreenCapture.CaptureScreenshot(Path.GetFullPath(Report + "/fixed-pause-options.png")); yield return null; yield return Escape(); yield return Escape(); Assert.That(Sensitivity(view).value, Is.EqualTo(1.8f).Within(.001f)); yield return Escape();
            menu.RestartFromFirstRoom(); yield return null; yield return null; yield return null; player = Object.FindAnyObjectByType<PlayerCharacterController>(); menu = Object.FindAnyObjectByType<CombatLabMenu>(); Assert.That(player.GetComponent<PlayerInputHandler>().LookSensitivity, Is.EqualTo(1.8f).Within(.001f));
            menu.ReturnToMainMenu(); yield return null; yield return null; yield return null; music = Object.FindAnyObjectByType<MenuMusic>(); yield return new WaitForSecondsRealtime(.35f); Assert.That(music.GetComponent<AudioSource>().isPlaying, Is.True); File.WriteAllText(Report + "/menu-fixed.txt", "Theme plays while menu pauses listener, fades/stops on Play, returns after reload. Sensitivity3.2 reaches pause UI/input,1.8 survives reopen/restart. Console frame fits screen " + Screen.width + "x" + Screen.height + ". Preferences restored after tests.");
        }
        [UnityTest] public IEnumerator BigBotThrowVisibleLodsAndRecollect()
        {
            StolenCannon module = null; yield return EquippedBigBot(value => module = value); module.Ammo.Drain(13); float energy = module.Ammo.Remaining; Assert.That(carrier.ThrowHeld(), Is.True); yield return null;
            module.Body.linearVelocity = Vector3.zero; module.Body.angularVelocity = Vector3.zero; module.Body.isKinematic = true; var views = module.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(views.Max(r => r.bounds.size.magnitude), Is.GreaterThan(.5f), "World module retains physical cannon proportions");
            foreach (float distance in new[] { 2f, 8f, 18f })
            {
                module.transform.position += player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * distance - module.SolidCollider.bounds.center; Physics.SyncTransforms(); yield return new WaitForSeconds(.1f); Capture(player.PlayerCamera, "fixed-throw-" + distance);
                Assert.That(views.Any(r => r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && (player.PlayerCamera.cullingMask & (1 << r.gameObject.layer)) != 0 && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(player.PlayerCamera), r.bounds)), Is.True);
                Assert.That(views.Any(r => r.isVisible), Is.True, "At least one LOD must render");
            }
            module.transform.position += player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 1.2f - module.SolidCollider.bounds.center; Physics.SyncTransforms(); yield return new WaitForSeconds(.4f); yield return Until(() => carrier.Candidate == module, 3, "Thrown module remains collectible"); InputSystem.QueueStateEvent(keys, new KeyboardState(Key.E)); yield return null; yield return null; InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return new WaitForSeconds(.8f);
            Assert.That(carrier.Held, Is.SameAs(module)); Assert.That(module.Ammo.Remaining, Is.EqualTo(energy).Within(.001f)); File.WriteAllText(Report + "/throw-fixed.txt", "Real source module, E pickup, throw, auto LOD visibility at2/8/18m, recollect same instance and energy. No desktop control.");
        }
    }
}
