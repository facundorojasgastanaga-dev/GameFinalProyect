using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IronRequiem.Tests
{
    /// Rebinds Melee F -> V with real virtual devices: V strikes, F no longer, the HUD help text shows V, the change
    /// persists through PlayerPrefs, and the input lock still blocks the new key.
    public sealed class ControlsRebindPlayTests
    {
        PlayerCharacterController player; ProtagonistMelee melee;
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        string savedPrefs; bool hadPrefs;

        [UnitySetUp] public IEnumerator Setup()
        {
            hadPrefs = PlayerPrefs.HasKey(ControlsRebinder.PrefsKey); savedPrefs = PlayerPrefs.GetString(ControlsRebinder.PrefsKey, "");
            ControlsRebinder.ResetAll(InputSystem.actions);
            Time.timeScale = 1; Application.runInBackground = true; previousDevices = InputSystem.actions.devices;
            keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent();
            InputSystem.actions.devices = new InputDevice[] { keys, mouse };
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/ProtagonistActionsLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null; yield return null;
            Object.FindAnyObjectByType<GameFlowManager>().enabled = false; Object.FindAnyObjectByType<ObjectiveManager>().enabled = false;
            foreach (var e in Object.FindObjectsByType<EnemyController>()) { var m = e.GetComponent<EnemyMobile>(); if (m) m.enabled = false; var t = e.GetComponent<EnemyTurret>(); if (t) t.enabled = false; e.enabled = false; }
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); melee = player.GetComponent<ProtagonistMelee>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "RebindFloor"; floor.transform.position = new Vector3(0, 9.5f, 0); floor.transform.localScale = new Vector3(120, 1, 120);
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.SetPositionAndRotation(new Vector3(0, 10.05f, -20), Quaternion.identity); cc.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms();
            Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(1.4f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; PlayerInputLock.Set(false);
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("RebindCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(old);
            ControlsRebinder.ResetAll(InputSystem.actions);
            if (hadPrefs) PlayerPrefs.SetString(ControlsRebinder.PrefsKey, savedPrefs);
            InputSystem.actions.devices = previousDevices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse);
        }

        void Keys(params Key[] value) { if (Time.timeScale > 0) Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keys, new KeyboardState(value)); }
        IEnumerator Tap(params Key[] value) { Keys(value); yield return new WaitForSecondsRealtime(.045f); Keys(); yield return null; yield return null; }

        [UnityTest] public IEnumerator RebindMeleeToVMakesVStrikeNotFAndHelpTextAndPersistence()
        {
            var action = InputSystem.actions.FindAction("Player/Melee", true);
            int index = ControlsRebinder.SlotIndex(action, "Keyboard&Mouse", null, 0);
            Assert.That(ControlHints.Key("Melee", false), Is.EqualTo("F"));
            string captured = null; bool cancelled = false;
            ControlsRebinder.Begin(action, index, "Keyboard&Mouse", p => captured = p, () => cancelled = true);
            yield return null; yield return Tap(Key.V); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(cancelled, Is.False); Assert.That(captured, Is.EqualTo("<Keyboard>/v"));
            Assert.That(ControlsRebinder.FindConflict(InputSystem.actions, "Keyboard&Mouse", action, index, captured, out _, out _), Is.False);
            ControlsRebinder.Assign(InputSystem.actions, action, index, captured);
            yield return null;
            Assert.That(action.enabled, Is.True, "the action is re-enabled after the capture");

            int serial = melee.AttackSerial;
            yield return Tap(Key.F); yield return new WaitForSeconds(.65f);
            Assert.That(melee.AttackSerial, Is.EqualTo(serial), "F no longer strikes");
            yield return Tap(Key.V); yield return new WaitForSeconds(.65f);
            Assert.That(melee.AttackSerial, Is.EqualTo(serial + 1), "V strikes");

            Assert.That(ControlHints.Key("Melee", false), Is.EqualTo("V"));
            yield return null; yield return null;
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).Select(t => t.text).ToArray();
            Assert.That(texts.Any(t => t != null && t.Contains("V  CARGAR")) || texts.Any(t => t != null && t.Contains("MANTENER [V] CARGA")), Is.True,
                "HUD help shows V: " + string.Join(" | ", texts.Where(t => !string.IsNullOrEmpty(t) && (t.Contains("CARGAR") || t.Contains("MANTENER")))));

            Directory.CreateDirectory("Docs/IronRequiem/Concepts/UI/Controls");
            ScreenCapture.CaptureScreenshot("Docs/IronRequiem/Concepts/UI/Controls/hud_melee_reasignado_V.png"); yield return null; yield return null;

            // persistence: wipe the in-memory overrides and load them back from PlayerPrefs as a new session would
            InputSystem.actions.RemoveAllBindingOverrides();
            Assert.That(ControlHints.Key("Melee", false), Is.EqualTo("F"));
            ControlsRebinder.Load(InputSystem.actions);
            Assert.That(ControlHints.Key("Melee", false), Is.EqualTo("V"));
        }

        [UnityTest] public IEnumerator EscapeCancelsTheCaptureWithoutChangingTheBinding()
        {
            var action = InputSystem.actions.FindAction("Player/Melee", true);
            int index = ControlsRebinder.SlotIndex(action, "Keyboard&Mouse", null, 0);
            string captured = null; bool cancelled = false;
            ControlsRebinder.Begin(action, index, "Keyboard&Mouse", p => captured = p, () => cancelled = true);
            yield return null; yield return Tap(Key.Escape); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(cancelled, Is.True); Assert.That(captured, Is.Null);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/f"));
            Assert.That(action.enabled, Is.True);
        }

        [UnityTest] public IEnumerator InputLockStillBlocksTheReassignedKey()
        {
            var action = InputSystem.actions.FindAction("Player/Melee", true);
            ControlsRebinder.Assign(InputSystem.actions, action, ControlsRebinder.SlotIndex(action, "Keyboard&Mouse", null, 0), "<Keyboard>/v");
            yield return null;
            int serial = melee.AttackSerial;
            PlayerInputLock.Set(true);
            yield return Tap(Key.V); yield return new WaitForSeconds(.4f);
            Assert.That(melee.AttackSerial, Is.EqualTo(serial), "the lock blocks V like it blocked F");
            PlayerInputLock.Set(false);
        }

        [UnityTest] public IEnumerator SpaceStillJumpsOnTheGroundWhileSharedWithDash()
        {
            var jump = InputSystem.actions.FindAction("Player/Jump", true);
            Assert.That(jump.bindings.Any(b => b.effectivePath == "<Keyboard>/space"), Is.True);
            float y = player.transform.position.y;
            yield return Tap(Key.Space); yield return new WaitForSeconds(.15f);
            Assert.That(player.transform.position.y, Is.GreaterThan(y + .05f), "Space jumps on the ground");
        }
    }
}
