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
    // Diagnostic (23/11/2026, user: "normal, then suddenly very heavy, after the charged punch or Shift"). Real campaign session,
    // real time, the USER'S OWN bindings and settings (Melee = right mouse, Aim = F, keyboard sprint toggle on) are kept.
    // After each action sequence it probes: time scale, look response (yaw per mouse delta), time to 90 % walk speed, FPS and
    // every movement / combat state that could stay stuck. Writes Tools/IronRequiem/Reports/Controls/heaviness-repro.txt.
    public sealed class HeavinessRepro
    {
        const string Session = "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity";
        const string Report = "Tools/IronRequiem/Reports/Controls/heaviness-repro.txt";
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices; bool background;
        readonly List<string> log = new List<string>();
        PlayerCharacterController player; AdvancedPlayerMovement movement; ProtagonistMelee melee; PlayerWeaponsManager weapons; MechanicInput mechanic;
        readonly HashSet<Key> held = new HashSet<Key>(); bool rmb; Vector3 home; float homeYaw;

        [UnitySetUp] public IEnumerator Setup()
        {
            log.Clear(); background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = 0; Time.timeScale = 1;
            EditorSceneManager.LoadSceneInPlayMode(Session, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<CampaignFlow>();
            float end = Time.realtimeSinceStartup + 90; while ((flow.Busy || flow.CurrentStage != 0) && Time.realtimeSinceStartup < end) yield return null;
            flow.Menu.BeginGame(); flow.Player.GetComponent<Health>().Invincible = true; yield return null;
            var intro = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro) { end = Time.realtimeSinceStartup + 10; while (!intro.IsPlaying && !intro.Finished && Time.realtimeSinceStartup < end) yield return null; intro.RequestSkip(); }
            end = Time.realtimeSinceStartup + 15; while (PlayerInputLock.Locked && Time.realtimeSinceStartup < end) yield return null;
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            Cursor.lockState = CursorLockMode.Locked;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); movement = player.GetComponent<AdvancedPlayerMovement>(); melee = player.GetComponent<ProtagonistMelee>(); weapons = player.GetComponent<PlayerWeaponsManager>(); mechanic = player.GetComponent<MechanicInput>();
            yield return new WaitForSecondsRealtime(1.5f);
            home = player.transform.position; homeYaw = player.transform.eulerAngles.y;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None; Application.runInBackground = background; Time.timeScale = 1;
            var session = Object.FindAnyObjectByType<CampaignFlow>(); if (session) { session.enabled = true; yield return session.DrainPendingLoadsForShutdown(); }
            SceneManager.SetActiveScene(SceneManager.CreateScene("HeavyCleanup" + Time.frameCount));
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--) { var s = SceneManager.GetSceneAt(i); if (s.isLoaded && s != SceneManager.GetActiveScene()) yield return SceneManager.UnloadSceneAsync(s); }
        }

        void SendKeys() { InputSystem.QueueStateEvent(keys, new KeyboardState(held.ToArray())); }
        void Down(Key k) { held.Add(k); SendKeys(); }
        void Up(Key k) { held.Remove(k); SendKeys(); }
        void Mouse(bool right, Vector2 delta = default) { rmb = right; var s = new MouseState { delta = delta }; if (rmb) s = s.WithButton(MouseButton.Right); InputSystem.QueueStateEvent(mouse, s); }

        string State()
        {
            var adv = movement;
            return $"timeScale {Time.timeScale:F2}, fixedDelta {Time.fixedDeltaTime:F4}, aiming {weapons.IsAiming}, rotMul {player.RotationMultiplier:F2}, crouch {player.IsCrouching}, grounded {player.IsGrounded}, " +
                   $"scripted {adv.IsScripted}, lunging {adv.IsLunging}, dashing {adv.IsDashing}, sliding {adv.IsSliding}, charging {melee.IsCharging}, attacking {melee.IsAttacking}, " +
                   $"canAct {mechanic.CanAct}, inputLock {PlayerInputLock.Locked}, sharpness {player.MovementSharpnessOnGround}, maxSpeed {player.MaxSpeedOnGround}, execBudget {GameFeel.ExecutionBudgetActive}";
        }

        // Look response, walk response and FPS, with nothing held but the probe's own input.
        IEnumerator Probe(string label)
        {
            // same free spot and heading for every probe (the charged punch lunges forward and could leave the player against a wall)
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = home; cc.enabled = true; player.CharacterVelocity = Vector3.zero; player.SetViewAngles(homeYaw, 0); Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            float yaw0 = player.transform.eulerAngles.y;
            for (int i = 0; i < 6; i++) { Mouse(rmb, new Vector2(20, 0)); yield return null; }
            float turned = Mathf.DeltaAngle(yaw0, player.transform.eulerAngles.y);
            Mouse(rmb); yield return null;
            float t0 = Time.realtimeSinceStartup, v90 = -1; Down(Key.S); var trace = new List<string>(); var p0 = player.transform.position;
            for (int i = 0; i < 90; i++) { yield return null; float v = Vector3.ProjectOnPlane(player.CharacterVelocity, Vector3.up).magnitude; if (i < 10) trace.Add(v.ToString("F1")); if (v > player.MaxSpeedOnGround * .9f) { v90 = (Time.realtimeSinceStartup - t0) * 1000; break; } }
            log.Add($"[{label}] speed per frame [{string.Join(" ", trace)}], moved {(player.transform.position - p0).magnitude:F2} m, grounded {player.IsGrounded}");
            float vmax = Vector3.ProjectOnPlane(player.CharacterVelocity, Vector3.up).magnitude; Up(Key.S);
            var dts = new List<float>(); for (int i = 0; i < 40; i++) { yield return null; dts.Add(Time.unscaledDeltaTime * 1000); }
            log.Add($"[{label}] look: {turned:F1} deg for 6x20 px; walk 90 % in {v90:F0} ms (speed {vmax:F1}); frame {dts.Average():F1} ms avg, max {dts.Max():F1}");
            log.Add($"[{label}] state: {State()}");
        }

        IEnumerator Charge(float seconds)
        {
            Mouse(true); float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; Mouse(false);
            yield return new WaitForSecondsRealtime(.9f);
        }

        [UnityTest] public IEnumerator ChargedPunchAndSprintToggleLeaveNoHeaviness()
        {
            LogAssert.ignoreFailingMessages = true;
            log.Add($"user settings: sprint toggle keyboard {RequiemSettings.SprintToggleKeyboard}, overrides {InputSystem.actions.SaveBindingOverridesAsJson().Length > 2}, melee bindings [{string.Join(", ", InputSystem.actions.FindAction("Player/Melee").bindings.Select(b => b.effectivePath))}], aim [{string.Join(", ", InputSystem.actions.FindAction("Player/Aim").bindings.Select(b => b.effectivePath))}]");
            yield return Probe("start");
            Down(Key.W); yield return new WaitForSecondsRealtime(.6f); float walk = Vector3.ProjectOnPlane(player.CharacterVelocity, Vector3.up).magnitude;
            Down(Key.LeftShift); yield return new WaitForSecondsRealtime(.6f); float shifted = Vector3.ProjectOnPlane(player.CharacterVelocity, Vector3.up).magnitude; Up(Key.LeftShift); Up(Key.W);
            log.Add($"speed: W {walk:F1} m/s, W + Shift held {shifted:F1} m/s (sprint removed: must be equal)"); yield return new WaitForSecondsRealtime(.5f);
            Assert.That(shifted, Is.EqualTo(walk).Within(.3f), "Shift no longer changes the speed");

            yield return Charge(1.2f); yield return Probe("after full charged punch (RMB 1.2 s)");
            yield return Charge(.3f); yield return Probe("after short charged punch (RMB 0.3 s)");

            Down(Key.W); yield return null; Down(Key.LeftShift); yield return null; Up(Key.LeftShift);
            yield return new WaitForSecondsRealtime(.8f); Up(Key.W); yield return new WaitForSecondsRealtime(.4f);
            yield return Probe("after W + Shift tap (toggle sprint), W released");

            Down(Key.W); yield return null; Down(Key.LeftShift); yield return null; Up(Key.LeftShift); yield return new WaitForSecondsRealtime(.4f);
            yield return Charge(1.2f); Up(Key.W); yield return new WaitForSecondsRealtime(.4f);
            yield return Probe("after charged punch while sprinting");

            Down(Key.W); yield return null; Down(Key.LeftShift); yield return null; Up(Key.LeftShift); yield return new WaitForSecondsRealtime(.3f);
            Down(Key.C); yield return null; Up(Key.C); yield return new WaitForSecondsRealtime(.8f); Up(Key.W); yield return new WaitForSecondsRealtime(.4f);
            yield return Probe("after sprint + C (slide)");

            Mouse(true); yield return new WaitForSecondsRealtime(.5f); Down(Key.LeftShift); yield return null; Up(Key.LeftShift); yield return new WaitForSecondsRealtime(.6f); Mouse(false); yield return new WaitForSecondsRealtime(.9f);
            yield return Probe("after Shift while charging");

            Down(Key.F); yield return new WaitForSecondsRealtime(.5f); Up(Key.F); yield return new WaitForSecondsRealtime(.3f);
            yield return Probe("after F (aim) tap");

            for (int k = 0; k < 4; k++) { Down(Key.W); Down(Key.LeftShift); yield return null; Up(Key.LeftShift); yield return Charge(1.2f); Up(Key.W); }
            yield return Probe("after 4x sprint + full charge");

            Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, string.Join("\n", log));
        }
    }
}
