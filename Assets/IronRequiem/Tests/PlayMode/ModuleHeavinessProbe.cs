using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Diagnostic (23/11/2026, user: "it usually happens right after you put on the first module and during the middle rooms").
    // Real campaign (RequiemCampaign), real time, the user's own bindings. Measures before the first module, right after the mount
    // station, 5 s later, then in each middle room: frame times, walk / look response, movement state and live counts (particles,
    // lights, audio, objects, managed memory) to catch a leak or a stuck state. Report: Tools/IronRequiem/Reports/Controls/module-heaviness.txt
    public sealed class ModuleHeavinessProbe
    {
        const string Main = "Assets/IronRequiem/Scenes/RequiemCampaign.unity", Report = "Tools/IronRequiem/Reports/Controls/module-heaviness.txt";
        Keyboard keys; Mouse mouse; bool background; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        CampaignFlow flow; PlayerCharacterController player; Health health; readonly StringBuilder log = new StringBuilder();

        [UnitySetUp] public IEnumerator Setup()
        {
            log.Clear(); background = Application.runInBackground; Application.runInBackground = true; Time.timeScale = 1; Time.captureDeltaTime = 0; AudioListener.pause = false;
            devices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            EditorSceneManager.LoadSceneInPlayMode(Main, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            flow = Object.FindAnyObjectByType<CampaignFlow>(); player = flow.Player; health = player.GetComponent<Health>();
            yield return Until(() => !flow.Busy && flow.CurrentStage == 0, 60, "Initial streamed room"); flow.Menu.BeginGame(); health.Invincible = true; yield return null;
            Cursor.lockState = CursorLockMode.Locked;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Input(); InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse); Time.timeScale = 1; Cursor.lockState = CursorLockMode.None;
            var clean = SceneManager.CreateScene("ModuleProbeCleanup" + Time.frameCount); SceneManager.SetActiveScene(clean);
            var session = Object.FindAnyObjectByType<CampaignFlow>(); if (session) yield return session.DrainPendingLoadsForShutdown();
            foreach (var queue in Object.FindObjectsByType<EnemySpawnQueue>()) { queue.CancelPending(); queue.enabled = false; }
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != clean && s.isLoaded).OrderBy(s => s.name == "RequiemCampaign" ? 1 : 0).ToArray();
            foreach (var scene in loaded) if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Application.runInBackground = background;
        }

        void Input(params Key[] held) => InputSystem.QueueStateEvent(keys, new KeyboardState(held));
        void Place(Vector3 point, float yaw = 0) { player.GetComponent<AdvancedPlayerMovement>().ResetForCheckpoint(); player.TeleportToCheckpoint(point); player.SetViewAngles(yaw, 0); Physics.SyncTransforms(); }
        IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            float end = Time.realtimeSinceStartup + seconds; while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            if (!condition()) { Write(); Assert.Fail(label + " stage=" + (flow ? flow.CurrentStage : -99) + " player=" + (player ? player.transform.position.ToString() : "none")); }
        }
        void Write() { Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, log.ToString()); }

        IEnumerator Probe(string label)
        {
            var dts = new List<float>(); for (int i = 0; i < 120; i++) { yield return null; dts.Add(Time.unscaledDeltaTime * 1000); }
            var s = dts.OrderBy(x => x).ToList();
            float yaw0 = player.transform.eulerAngles.y; for (int i = 0; i < 6; i++) { InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(20, 0) }); yield return null; }
            InputSystem.QueueStateEvent(mouse, new MouseState()); float turned = Mathf.DeltaAngle(yaw0, player.transform.eulerAngles.y);
            float t0 = Time.realtimeSinceStartup, v90 = -1, vmax = 0; Input(Key.S);
            for (int i = 0; i < 60; i++) { yield return null; float v = Vector3.ProjectOnPlane(player.CharacterVelocity, Vector3.up).magnitude; vmax = Mathf.Max(vmax, v); if (v90 < 0 && v > player.MaxSpeedOnGround * .9f) { v90 = (Time.realtimeSinceStartup - t0) * 1000; break; } }
            Input(); yield return new WaitForSecondsRealtime(.3f);
            var adv = player.GetComponent<AdvancedPlayerMovement>(); var weapons = player.GetComponent<PlayerWeaponsManager>(); var w = weapons.GetActiveWeapon();
            int particles = Object.FindObjectsByType<ParticleSystem>().Count(p => p.isPlaying), particleCount = Object.FindObjectsByType<ParticleSystem>().Sum(p => p.particleCount);
            int lights = Object.FindObjectsByType<Light>().Count(l => l.enabled), audio = Object.FindObjectsByType<AudioSource>().Count(a => a.isPlaying), objects = Object.FindObjectsByType<Transform>().Length;
            log.AppendLine($"[{label}] t {Time.realtimeSinceStartup:F0}s stage {flow.CurrentStage} room {(flow.CurrentRoom ? flow.CurrentRoom.name : "-")}");
            log.AppendLine($"   frame ms avg {dts.Average():F1} ({1000 / dts.Average():F0} FPS) p95 {s[(int)(s.Count * .95f)]:F1} max {s.Last():F1}; >33 ms {dts.Count(x => x > 33)}");
            log.AppendLine($"   look {turned:F1} deg / 6x20 px; walk 90 % in {v90:F0} ms (max {vmax:F1} m/s)");
            log.AppendLine($"   timeScale {Time.timeScale:F2} lock {PlayerInputLock.Locked} scripted {adv.IsScripted} lunging {adv.IsLunging} sliding {adv.IsSliding} crouch {player.IsCrouching} aiming {weapons.IsAiming} rotMul {player.RotationMultiplier:F2} sharp {player.MovementSharpnessOnGround} maxSpeed {player.MaxSpeedOnGround} weapon {(w ? w.name : "none")} switch {weapons.SwitchState}");
            log.AppendLine($"   live: particle systems playing {particles} ({particleCount} particles), lights {lights}, audio playing {audio}, transforms {objects}, managed MB {GC.GetTotalMemory(false) / 1048576f:F0}, enemies {Object.FindObjectsByType<EnemyController>().Length}");
            Write();
        }

        IEnumerator ToStation()
        {
            var intro = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro) { yield return Until(() => intro.IsPlaying || intro.Finished, 10, "Intro"); intro.RequestSkip(); }
            yield return Until(() => !PlayerInputLock.Locked, 15, "Unlock");
            yield return Until(() => !flow.Airlocks[0].SourceDoor.activeSelf, 15, "Hangar door"); Place(new Vector3(0, .03f, -80f)); yield return Until(() => !flow.Busy && flow.CurrentStage == 1, 90, "Corridor");
            var rounds = flow.CurrentRoom.Rounds; Place(new Vector3(0, .03f, -62f));
            float end = Time.realtimeSinceStartup + 90; while (rounds && !rounds.Completed && Time.realtimeSinceStartup < end) { foreach (var h in rounds.Living.ToArray()) if (h && h.CurrentHealth > 0) h.Kill(); yield return null; }
            yield return Until(() => !flow.Airlocks[1].SourceDoor.activeSelf, 8, "Lobby door");
            var station = Object.FindAnyObjectByType<WeaponMountStation>();
            Place(station.transform.position - station.transform.forward * .5f + Vector3.up * .03f); yield return new WaitForSecondsRealtime(.5f);
        }

        string Aim() { var w = player.GetComponent<PlayerWeaponsManager>(); return $"aiming {w.IsAiming}, rotMul {player.RotationMultiplier:F2}, switch {w.SwitchState}, weapon {(w.GetActiveWeapon() ? w.GetActiveWeapon().name : "none")}"; }
        void MouseButtons(bool right) { var m = new MouseState(); if (right) m = m.WithButton(MouseButton.Right); InputSystem.QueueStateEvent(mouse, m); }

        // With the user's bindings (Aim = F, Melee = right mouse) after the first module: is aim (0.4x look) ever left on?
        [UnityTest, Timeout(340000)] public IEnumerator AimAfterTheFirstModuleNeverSticks()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return ToStation(); var station = Object.FindAnyObjectByType<WeaponMountStation>();
            log.AppendLine($"aim bindings [{string.Join(", ", InputSystem.actions.FindAction("Player/Aim").bindings.Select(b => b.effectivePath))}], melee [{string.Join(", ", InputSystem.actions.FindAction("Player/Melee").bindings.Select(b => b.effectivePath))}]");
            Input(Key.F); yield return new WaitForSecondsRealtime(.3f); log.AppendLine("F held, no weapon yet: " + Aim()); Input(); yield return new WaitForSecondsRealtime(.3f);
            Input(Key.E); yield return null; yield return null; Input(); yield return Until(() => station.Busy, 3, "Station starts");
            yield return new WaitForSecondsRealtime(.5f); Input(Key.F); yield return new WaitForSecondsRealtime(.4f); log.AppendLine("F held during the mount: " + Aim()); Input();
            yield return Until(() => !station.Busy, 15, "Station ends"); yield return new WaitForSecondsRealtime(.6f); log.AppendLine("after the mount, F released: " + Aim());
            Input(Key.F); yield return new WaitForSecondsRealtime(.4f); log.AppendLine("F held with the module: " + Aim()); Input(); yield return new WaitForSecondsRealtime(.4f); log.AppendLine("F released: " + Aim());
            Input(Key.F); yield return new WaitForSecondsRealtime(.2f); MouseButtons(true); yield return new WaitForSecondsRealtime(.3f); Input(); yield return new WaitForSecondsRealtime(.9f); MouseButtons(false); yield return new WaitForSecondsRealtime(1f);
            log.AppendLine("F + charged punch, F released during the charge: " + Aim());
            MouseButtons(true); yield return new WaitForSecondsRealtime(.4f); Input(Key.F); yield return new WaitForSecondsRealtime(.4f); MouseButtons(false); yield return new WaitForSecondsRealtime(.3f); Input(); yield return new WaitForSecondsRealtime(1f);
            log.AppendLine("charge, F during the punch, all released: " + Aim());
            Input(Key.F); yield return new WaitForSecondsRealtime(.2f); Input(Key.F, Key.Q); yield return null; Input(Key.F); yield return new WaitForSecondsRealtime(.6f); Input(); yield return new WaitForSecondsRealtime(.8f);
            log.AppendLine("F + weapon switch (Q), all released: " + Aim());
            Write();
        }

        [UnityTest, Timeout(340000)] public IEnumerator FirstModuleAndMiddleRooms()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return ToStation();
            var station = Object.FindAnyObjectByType<WeaponMountStation>();
            yield return Probe("before the first module (lobby)");
            Input(Key.E); yield return null; yield return null; Input(); yield return Until(() => station.Busy, 3, "Station starts");
            yield return Until(() => !station.Busy, 15, "Station ends (not skipped)");
            yield return Probe("right after the first module");
            yield return new WaitForSecondsRealtime(5); yield return Probe("5 s after the module");
            Place(new Vector3(0, .03f, -30.5f)); yield return Until(() => !flow.Busy && flow.CurrentStage == flow.PrologueStages, 90, "Room01");
            yield return new WaitForSecondsRealtime(1); yield return Probe("Room01 arrival");
            int o = flow.PrologueStages; CombatTwoRoomsLab rounds; float end;
            for (int stage = 0; stage < 2 && stage + o < flow.Airlocks.Length; stage++) // two middle rooms: the queue watchdog aborts a single test after 6 min
            {
                rounds = flow.CurrentRoom.Rounds; end = Time.realtimeSinceStartup + 120;
                while (rounds && !rounds.Completed && Time.realtimeSinceStartup < end) { foreach (var h in rounds.Living.ToArray()) if (h && h.CurrentHealth > 0) h.Kill(); yield return null; }
                yield return Probe($"room {flow.CurrentStage} cleared");
                int a = stage + o; yield return Until(() => !flow.Airlocks[a].SourceDoor.activeSelf, 10, "Exit door");
                Vector3 transit = stage == 0 ? new Vector3(31, .03f, 25) : new Vector3(0, 8.03f, new[] { 0f, 27f, 91f, 155f }[stage] + 6); Place(transit, stage == 0 ? 180 : 0);
                yield return Until(() => flow.Busy, 8, "Enter airlock"); yield return Until(() => !flow.Busy && flow.CurrentStage == stage + o + 1, 90, "Next room");
                yield return new WaitForSecondsRealtime(1); yield return Probe($"room {flow.CurrentStage} arrival");
            }
            Write();
        }
    }
}
