using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class TwoFloorGapTests
    {
        const string Report = "Tools/IronRequiem/Reports/TwoFloorGap";
        PlayerCharacterController player; Health health; GapFallRecovery gap; CombatTwoRoomsLab waves; CombatLabMenu menu;
        Keyboard keys; Mouse mouse; bool background; float capture;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        [UnitySetUp] public IEnumerator Setup()
        {
            background = Application.runInBackground; capture = Time.captureDeltaTime; Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false;
            devices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); health = player.GetComponent<Health>(); gap = Object.FindAnyObjectByType<GapFallRecovery>(); waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); menu = Object.FindAnyObjectByType<CombatLabMenu>(); menu.BeginGame();
            waves.enabled = false; if (waves.SpawnQueue) { waves.SpawnQueue.CancelPending(); waves.SpawnQueue.enabled = false; }
            yield return new WaitForSeconds(.4f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("TwoFloorCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }
        void Place(Vector3 position, float yaw = 0) { player.GetComponent<AdvancedPlayerMovement>().ResetForCheckpoint(); player.TeleportToCheckpoint(position); player.transform.rotation = Quaternion.Euler(0, yaw, 0); Physics.SyncTransforms(); }
        void Input(params Key[] held) => InputSystem.QueueStateEvent(keys, new KeyboardState(held));
        IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            float until = Time.time + seconds, real = Time.realtimeSinceStartup + 45;
            while (!condition() && Time.time < until && Time.realtimeSinceStartup < real) yield return null;
            Assert.That(condition(), Is.True, label + ": " + player.transform.position);
        }
        static NavMeshPath Path(Vector3 a, Vector3 b)
        {
            Assert.That(NavMesh.SamplePosition(a, out var start, 1, NavMesh.AllAreas), Is.True, "Nav start " + a); Assert.That(NavMesh.SamplePosition(b, out var end, 1, NavMesh.AllAreas), Is.True, "Nav end " + b);
            var path = new NavMeshPath(); NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path); return path;
        }
        [UnityTest] public IEnumerator StairsNavigateAndPlayerActuallyWalksUp()
        {
            var gate = (GameObject)new SerializedObject(waves).FindProperty("gate").objectReferenceValue; gate.SetActive(false); yield return new WaitForSeconds(.3f);
            var samples = new[] { new Vector3(0, 0, 20), new Vector3(31, 0, 26), new Vector3(31, 1.6f, 18), new Vector3(31, 6.4f, -1.6f), new Vector3(31, 8, -11), new Vector3(0, 8, -10) };
            var report = new System.Text.StringBuilder();
            for (int i = 0; i < samples.Length - 1; i++) { var segment = Path(samples[i], samples[i + 1]); report.AppendLine(samples[i] + " to " + samples[i + 1] + ": " + segment.status + "; " + string.Join(";", segment.corners.Select(p => p.ToString()))); }
            File.WriteAllText(Report + "/stair-navigation.txt", report.ToString());
            Assert.That(Path(new Vector3(0, 8, -10), new Vector3(0, 8, 10)).status, Is.Not.EqualTo(NavMeshPathStatus.PathComplete), "The gap remains a jump challenge");
            Place(new Vector3(20, .05f, 25), 90); yield return new WaitForSeconds(.25f); Input(Key.W);
            yield return Until(() => player.transform.position.x > 30.5f, 3, "Walk through the lower stair entrance"); Input(); yield return null; player.CharacterVelocity = Vector3.zero; player.transform.rotation = Quaternion.Euler(0, 180, 0); Input(Key.W);
            yield return Until(() => player.transform.position.z < -9, 9, "Walk up all40 physical steps"); Input(); yield return new WaitForSeconds(.2f); Assert.That(player.transform.position.y, Is.EqualTo(8).Within(.2f));
            player.transform.rotation = Quaternion.Euler(0, 270, 0); Input(Key.W); yield return Until(() => player.transform.position.x < 22, 3, "Exit stair onto upper floor"); Input(); yield return new WaitForSeconds(.2f);
            Assert.That(player.transform.position.y, Is.EqualTo(8).Within(.2f)); Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            Assert.That(Path(new Vector3(0, 0, 20), new Vector3(0, 8, -10)).status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(Report + "/upper-stairs.png")); yield return null;
        }
        [UnityTest] public IEnumerator SprintWithoutJumpFallsIntoGap()
        {
            Place(new Vector3(0, 8.05f, -10)); yield return new WaitForSeconds(.3f); Input(Key.W, Key.LeftShift);
            yield return Until(() => gap.RecoveryCount == 1, 3, "Sprinting without jumping cannot bypass the gap"); Input();
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - gap.Damage));
        }
        [UnityTest] public IEnumerator JumpSuccessAndFallsRecoverOnTheirTakeoffBank()
        {
            var inventory = player.GetComponent<PlayerWeaponsManager>(); var weapon = inventory.GetActiveWeapon();
            Place(new Vector3(0, 8.05f, -7)); yield return new WaitForSeconds(.3f); Input(Key.W); yield return new WaitForSeconds(.15f); Input(Key.W, Key.Space); yield return null; yield return null; Input(Key.W);
            yield return Until(() => player.transform.position.z > 6, 3, "Jump across the gap"); Input(); yield return new WaitForSeconds(.7f);
            Assert.That(gap.RecoveryCount, Is.Zero); Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth)); Assert.That(player.transform.position.y, Is.EqualTo(8).Within(.2f));
            Place(new Vector3(0, 8.05f, -5)); yield return new WaitForSeconds(.3f); Input(Key.W);
            yield return Until(() => gap.RecoveryCount == 1, 3, "Real trigger fall from near bank"); Input(); yield return new WaitForSeconds(.3f);
            Assert.That(player.transform.position.y, Is.EqualTo(8).Within(.2f)); Assert.That(player.transform.position.z, Is.LessThan(-2)); Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - gap.Damage)); Assert.That(player.CharacterVelocity.y, Is.GreaterThan(-1)); Assert.That(inventory.GetActiveWeapon(), Is.SameAs(weapon));
            yield return new WaitForSeconds(.8f); Place(new Vector3(0, 8.05f, 6), 180); yield return new WaitForSeconds(.4f); Input(Key.W);
            yield return Until(() => gap.RecoveryCount == 2, 3, "Real trigger fall from far bank"); Input(); yield return new WaitForSeconds(.4f);
            Assert.That(player.transform.position.z, Is.GreaterThan(2)); Assert.That(player.transform.position.y, Is.EqualTo(8).Within(.2f)); Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 2 * gap.Damage));
            Time.timeScale = 0; float before = health.CurrentHealth; Assert.That(gap.TryRecover(), Is.False); yield return new WaitForSecondsRealtime(.1f); Assert.That(health.CurrentHealth, Is.EqualTo(before)); Time.timeScale = 1;
            File.WriteAllText(Report + "/recovery.txt", "Successful Space+W jump: no damage. Two failed jumps from opposite banks: exactly20HP each, original bank, height8m, same inventory, no extra landing damage. Pause blocks recovery.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(Report + "/gap-recovery.png")); yield return null;
        }
        void Freeze()
        {
            foreach (var h in waves.Living) if (h) { var mobile = h.GetComponent<EnemyMobile>(); if (mobile) mobile.enabled = false; var controller = h.GetComponent<EnemyController>(); if (controller) { controller.enabled = false; var agent = controller.NavMeshAgent; if (agent && agent.enabled && agent.isOnNavMesh) agent.isStopped = true; } var big = h.GetComponent<BigBotCombat>(); if (big) big.enabled = false; }
        }
        [UnityTest] public IEnumerator ExistingWavesOpenStairGateAndUpperEncounterStartsAfterJump()
        {
            health.Invincible = true; waves.enabled = true; if (waves.SpawnQueue) waves.SpawnQueue.enabled = true; Place(new Vector3(0, .05f, -20));
            foreach (int count in new[] { 6, 4, 5 })
            {
                yield return Until(() => { Freeze(); return waves.PendingSpawns == 0 && waves.LivingEnemies == count; }, 10, "Downstairs wave count " + count);
                foreach (var enemy in waves.Living.ToArray()) enemy.Kill(); yield return null; yield return null;
            }
            yield return Until(() => waves.GateOpen, 3, "Stair gate opens");
            Place(new Vector3(0, 8.05f, -8)); yield return new WaitForSeconds(.4f); Assert.That(waves.WaveIndex, Is.EqualTo(2), "Upper fight waits until jump completed");
            Place(new Vector3(0, 8.05f, 5)); yield return Until(() => { Freeze(); return waves.WaveIndex == 3 && waves.PendingSpawns == 0 && waves.LivingEnemies == 5; }, 12, "Five enemies spawn on upper far bank");
            Assert.That(waves.Living.All(h => h.transform.position.y > 7.5f), Is.True); foreach (var enemy in waves.Living.ToArray()) enemy.Kill(); yield return Until(() => waves.Completed, 3, "Existing completion flow");
        }
    }
}
