using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IronRequiem.Tests
{
    // Diagnostic (23/11/2026, user report "enorme sensación de input lag"): in the real campaign session, real time (no capture
    // delta), measures the frame times and how many frames / ms pass between an input event (mouse delta, W, M1) and its first
    // visible effect (yaw, velocity, shot). Writes Tools/IronRequiem/Reports/Controls/input-latency.txt. Always passes: it only
    // reports. The user's binding overrides are set aside and restored (PlayerPrefs untouched).
    public sealed class InputLatencyProbe
    {
        const string Session = "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity";
        const string Report = "Tools/IronRequiem/Reports/Controls/input-latency.txt";
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices; string savedOverrides; bool background;
        readonly List<string> log = new List<string>();

        [UnitySetUp] public IEnumerator Setup()
        {
            log.Clear(); background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = 0; Time.timeScale = 1;
            EditorSceneManager.LoadSceneInPlayMode(Session, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<CampaignFlow>(); Assert.That(flow, Is.Not.Null);
            float end = Time.realtimeSinceStartup + 90; while ((flow.Busy || flow.CurrentStage != 0) && Time.realtimeSinceStartup < end) yield return null;
            flow.Menu.BeginGame(); flow.Player.GetComponent<Health>().Invincible = true; yield return null;
            var intro = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro) { end = Time.realtimeSinceStartup + 10; while (!intro.IsPlaying && !intro.Finished && Time.realtimeSinceStartup < end) yield return null; intro.RequestSkip(); }
            end = Time.realtimeSinceStartup + 15; while (PlayerInputLock.Locked && Time.realtimeSinceStartup < end) yield return null;
            savedOverrides = InputSystem.actions.SaveBindingOverridesAsJson(); InputSystem.actions.RemoveAllBindingOverrides();
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSecondsRealtime(2f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (savedOverrides != null) { InputSystem.actions.RemoveAllBindingOverrides(); if (savedOverrides.Length > 0) InputSystem.actions.LoadBindingOverridesFromJson(savedOverrides); savedOverrides = null; }
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None; Application.runInBackground = background; Time.timeScale = 1;
            var session = Object.FindAnyObjectByType<CampaignFlow>(); if (session) { session.enabled = true; yield return session.DrainPendingLoadsForShutdown(); }
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("LatencyCleanup" + Time.frameCount));
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--) { var s = SceneManager.GetSceneAt(i); if (s.isLoaded && s != SceneManager.GetActiveScene()) yield return SceneManager.UnloadSceneAsync(s); }
        }

        [UnityTest] public IEnumerator MeasureFrameTimesAndInputLatency()
        {
            LogAssert.ignoreFailingMessages = true;
            var player = Object.FindAnyObjectByType<PlayerCharacterController>(); Assert.That(player, Is.Not.Null);
            var weapons = player.GetComponent<PlayerWeaponsManager>();
            log.Add($"settings: input update {InputSystem.settings.updateMode}, vSync {QualitySettings.vSyncCount}, maxQueuedFrames {QualitySettings.maxQueuedFrames}, targetFrameRate {Application.targetFrameRate}, fixedDelta {Time.fixedDeltaTime:F4}, maximumDelta {Time.maximumDeltaTime:F3}, timeScale {Time.timeScale}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, screen {Screen.width}x{Screen.height}, scene {SceneManager.GetActiveScene().name}");
            log.Add($"player: RotationSpeed {player.RotationSpeed}, MovementSharpnessOnGround {player.MovementSharpnessOnGround}, MaxSpeedOnGround {player.MaxSpeedOnGround}");

            // 1) frame times, standing still
            var dts = new List<float>();
            for (int i = 0; i < 300; i++) { yield return null; dts.Add(Time.unscaledDeltaTime * 1000); }
            var sorted = dts.OrderBy(x => x).ToList();
            float P(float q) => sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * q), 0, sorted.Count - 1)];
            log.Add($"frame time ms (300 frames): avg {dts.Average():F1} ({1000 / dts.Average():F0} FPS), p50 {P(.5f):F1}, p95 {P(.95f):F1}, p99 {P(.99f):F1}, max {sorted.Last():F1}; frames > 33 ms {dts.Count(x => x > 33)}, > 50 ms {dts.Count(x => x > 50)}");

            // 2) mouse delta -> yaw
            for (int k = 0; k < 5; k++)
            {
                float yaw0 = player.transform.eulerAngles.y; float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40, 0) });
                int frames = -1; for (int i = 0; i < 30; i++) { yield return null; if (Mathf.Abs(Mathf.DeltaAngle(yaw0, player.transform.eulerAngles.y)) > .01f) { frames = Time.frameCount - f0; break; } }
                log.Add($"mouse delta -> yaw changed after {frames} frame(s), {(Time.realtimeSinceStartup - t0) * 1000:F0} ms");
                yield return new WaitForSecondsRealtime(.2f);
            }

            // 3) W -> horizontal velocity
            for (int k = 0; k < 3; k++)
            {
                float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount; float v90 = -1, vAny = -1;
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W));
                for (int i = 0; i < 60; i++)
                {
                    yield return null; var v = Vector3.ProjectOnPlane(player.CharacterVelocity, Vector3.up).magnitude;
                    if (vAny < 0 && v > .1f) vAny = (Time.realtimeSinceStartup - t0) * 1000;
                    if (v90 < 0 && v > player.MaxSpeedOnGround * .9f) { v90 = (Time.realtimeSinceStartup - t0) * 1000; break; }
                }
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                log.Add($"W -> moving after {vAny:F0} ms, 90 % speed after {v90:F0} ms");
                yield return new WaitForSecondsRealtime(.6f);
            }

            // 4) M1 -> shot processed
            for (int k = 0; k < 3; k++)
            {
                var w = weapons.GetActiveWeapon(); if (!w) { log.Add("M1: no active weapon"); break; }
                int shots = 0; void Shot() => shots++; w.OnShootProcessed += Shot;
                float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount; int frames = -1;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                for (int i = 0; i < 60; i++) { yield return null; if (shots > 0) { frames = Time.frameCount - f0; break; } }
                InputSystem.QueueStateEvent(mouse, new MouseState()); w.OnShootProcessed -= Shot;
                log.Add($"M1 -> {w.name} shot after {frames} frame(s), {(Time.realtimeSinceStartup - t0) * 1000:F0} ms");
                yield return new WaitForSecondsRealtime(.8f);
            }

            // 5) frame times while moving and turning
            dts.Clear();
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W));
            for (int i = 0; i < 180; i++) { InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(i % 60 < 30 ? 6 : -6, 0) }); yield return null; dts.Add(Time.unscaledDeltaTime * 1000); }
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            sorted = dts.OrderBy(x => x).ToList();
            log.Add($"frame time ms moving+turning (180 frames): avg {dts.Average():F1} ({1000 / dts.Average():F0} FPS), p95 {P(.95f):F1}, max {sorted.Last():F1}; frames > 33 ms {dts.Count(x => x > 33)}");
            Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, string.Join("\n", log));
        }
    }
}
