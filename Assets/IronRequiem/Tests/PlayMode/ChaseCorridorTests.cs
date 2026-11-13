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
    public sealed class ChaseCorridorTests
    {
        const string Lab = "Assets/IronRequiem/Scenes/ChaseCorridorLab.unity", Report = "Tools/IronRequiem/Reports/ChaseCorridor";
        Keyboard keys; Mouse mouse; bool background; float capture;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        PlayerCharacterController player; Health health; CombatLabMenu menu; ChaseCorridorEncounter chase;
        [UnitySetUp] public IEnumerator Setup()
        {
            background = Application.runInBackground; capture = Time.captureDeltaTime; Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false;
            devices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null; Bind(); menu.BeginGame(); yield return new WaitForSeconds(.3f);
        }
        void Bind() { player = Object.FindAnyObjectByType<PlayerCharacterController>(); health = player.GetComponent<Health>(); menu = Object.FindAnyObjectByType<CombatLabMenu>(); chase = Object.FindAnyObjectByType<ChaseCorridorEncounter>(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("ChaseCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }
        void Input(params Key[] held) => InputSystem.QueueStateEvent(keys, new KeyboardState(held));
        void Place(Vector3 p) { player.GetComponent<AdvancedPlayerMovement>().ResetForCheckpoint(); player.TeleportToCheckpoint(p); player.transform.rotation = Quaternion.identity; Physics.SyncTransforms(); }
        IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            float end = Time.time + seconds, real = Time.realtimeSinceStartup + 60;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < real) { Freeze(); yield return null; }
            Assert.That(condition(), Is.True, label + ": " + (player ? player.transform.position.ToString() : "no player"));
        }
        void Freeze()
        {
            foreach (var controller in Object.FindObjectsByType<EnemyController>())
            {
                var mobile = controller.GetComponent<EnemyMobile>(); if (mobile) mobile.enabled = false; var turret = controller.GetComponent<EnemyTurret>(); if (turret) turret.enabled = false; var big = controller.GetComponent<BigBotCombat>(); if (big) big.enabled = false; var saw = controller.GetComponent<SawBotCombat>(); if (saw) saw.enabled = false;
                controller.enabled = false; var agent = controller.NavMeshAgent; if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            }
        }
        IEnumerator Enter()
        {
            Assert.That(chase.Running, Is.False); Assert.That(chase.EntranceDoor.activeSelf, Is.False); Input(Key.W);
            yield return Until(() => chase.Running, 4, "Walk through entrance trigger"); Input(); yield return null; Assert.That(chase.EntranceDoor.activeSelf, Is.True);
        }
        [UnityTest] public IEnumerator RunCorridorJumpThreeGapsAndReachExit()
        {
            health.Invincible = true; yield return Enter();
            float before = chase.SawCarriage.position.z; Time.timeScale = 0; yield return new WaitForSecondsRealtime(.12f); Assert.That(chase.SawCarriage.position.z, Is.EqualTo(before)); Time.timeScale = 1;
            yield return new WaitForSeconds(.5f); Assert.That(chase.SawCarriage.position.z, Is.EqualTo(before), "Two-second warning before translation");
            Input(Key.W);
            foreach (float gap in new[] { 48f, 98f, 148f })
            {
                yield return Until(() => player.transform.position.z >= gap - 2, 8, "Approach gap " + gap); Input(Key.W, Key.Space); yield return null; yield return null; Input(Key.W);
                yield return Until(() => player.transform.position.z > gap + 9, 3, "Real Space+W jump " + gap);
            }
            yield return Until(() => chase.Completed, 5, "Run into exit safe zone"); Input(); yield return null;
            Assert.That(chase.SpawnedCount, Is.EqualTo(5)); Assert.That(chase.Spawned.Count(g => g && g.GetComponent<SawBotCombat>()), Is.EqualTo(1)); Assert.That(Object.FindObjectsByType<GapFallRecovery>().All(g => g.RecoveryCount == 0), Is.True);
            Assert.That(chase.ExitDoor.activeSelf, Is.False); Assert.That(chase.Running, Is.False); Assert.That(chase.Motor.isPlaying, Is.False); Assert.That(chase.SawCarriage.position.z, Is.GreaterThan(before + 30));
            foreach (var zone in chase.Ambushes) foreach (var point in zone.Points) Assert.That(NavMesh.SamplePosition(point.position, out _, .75f, NavMesh.AllAreas), Is.True);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Report + "/corridor-completed.png")); yield return null;
            File.WriteAllText(Report + "/run.txt", "Real W traversal and Space jumps through3 gaps; door locks;2s warning; saws advance at8m/s; pause freezes;5 existing enemies spawned once; exit opens and motor stops. Enemy AI frozen after spawn to isolate course geometry, not a manual combat playthrough.");
        }
        [UnityTest] public IEnumerator SawsCatchPlayerAndRestartResetsEncounter()
        {
            yield return Enter(); yield return Until(() => health.CurrentHealth <= 0, 5, "Saws catch stationary player"); yield return null; yield return null;
            Assert.That(menu.IsDeathMenu, Is.True); Assert.That(chase.Running, Is.False); Assert.That(chase.Motor.isPlaying, Is.False);
            menu.RestartFromFirstRoom(); yield return null; yield return null; Bind(); yield return null;
            Assert.That(menu.IsPlaying, Is.True); Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth)); Assert.That(chase.Running, Is.False); Assert.That(chase.SpawnedCount, Is.Zero); Assert.That(chase.EntranceDoor.activeSelf, Is.False); Assert.That(chase.SawCarriage.gameObject.activeSelf, Is.False);
            menu.ReturnToMainMenu(); yield return null; yield return null; Bind(); Assert.That(menu.IsMainMenu, Is.True); Assert.That(Time.timeScale, Is.Zero);
        }
        [UnityTest] public IEnumerator FallCostsHealthAndPreservesInventoryWhileSawsContinue()
        {
            yield return Enter(); var inventory = player.GetComponent<PlayerWeaponsManager>(); var weapon = inventory.GetActiveWeapon(); Place(new Vector3(0, .05f, 46)); yield return new WaitForSeconds(.3f);
            var gap = Object.FindObjectsByType<GapFallRecovery>().OrderBy(g => g.transform.position.z).First(); float sawZ = chase.SawCarriage.position.z; Input(Key.W);
            yield return Until(() => gap.RecoveryCount == 1, 3, "Fall trigger recovers player"); Input(); yield return new WaitForSeconds(.2f);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 20)); Assert.That(player.transform.position.z, Is.LessThan(48)); Assert.That(player.transform.position.y, Is.EqualTo(0).Within(.2f)); Assert.That(inventory.GetActiveWeapon(), Is.SameAs(weapon));
            yield return new WaitForSeconds(2); Assert.That(chase.SawCarriage.position.z, Is.GreaterThan(sawZ)); Assert.That(chase.Running, Is.True);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(0, 180, 0); ScreenCapture.CaptureScreenshot(Path.GetFullPath(Report + "/saws-pursuing.png")); yield return null;
        }
        [UnityTest] public IEnumerator PreviousUpperRoomUnlocksDoorAndLoadsChaseIntoPlay()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null; Bind(); menu.BeginGame(); health.Invincible = true;
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); var entrance = Object.FindAnyObjectByType<ChaseSceneEntrance>(); Assert.That(entrance.IsOpen, Is.False); Place(new Vector3(0, .05f, -20));
            foreach (int count in new[] { 6, 4, 5 }) { yield return Until(() => waves.PendingSpawns == 0 && waves.LivingEnemies == count, 10, "Source wave " + count); foreach (var h in waves.Living.ToArray()) h.Kill(); yield return null; yield return null; }
            yield return Until(() => waves.GateOpen, 3, "Source stair access"); Place(new Vector3(0, 8.05f, 5));
            try { yield return Until(() => waves.PendingSpawns == 0 && waves.WaveIndex == 3 && waves.LivingEnemies == 5, 12, "Upper wave"); }
            finally
            {
                var text = "seed=" + waves.RunSeed + " composition=" + waves.CurrentComposition + " wave=" + waves.WaveIndex + " alive=" + waves.LivingEnemies + " pending=" + waves.PendingSpawns + " status=" + waves.WaitingForSpawnSpace + "\n";
                foreach (var h in waves.Living) text += h.name + " at " + h.transform.position + " hp=" + h.CurrentHealth + "\n";
                foreach (var c in Physics.OverlapSphere(new Vector3(0, 9, 14), 2, -1, QueryTriggerInteraction.Ignore)) text += "turretSlot=" + c.name + " bounds=" + c.bounds + "\n";
                File.WriteAllText(Report + "/upper-spawn-probe.txt", text);
            }
            foreach (var h in waves.Living.ToArray()) h.Kill(); yield return Until(() => waves.Completed, 3, "Source completed");
            yield return Until(() => entrance.IsOpen, 30, "Async corridor ready after door delay"); Place(new Vector3(0, 8.05f, 22)); Input(Key.W);
            yield return Until(() => SceneManager.GetActiveScene().name == "ChaseCorridorLab", 4, "Third-scene door transition"); Input(); yield return null; yield return null; Bind(); yield return null;
            Assert.That(menu.IsPlaying, Is.True); Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(chase, Is.Not.Null); Assert.That(chase.Running, Is.False); Assert.That(player.transform.position.z, Is.LessThan(0));
        }
        [UnityTest] public IEnumerator UpperTurretRemainsAvailableAcrossThreeSpawnSeeds()
        {
            foreach (int seed in new[] { 11, 27, 43 })
            {
                CombatTwoRoomsLab waves = null;
                void Configure(Scene scene, LoadSceneMode mode)
                {
                    if (scene.name != "CombatTwoFloorsLab") return;
                    waves = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CombatTwoRoomsLab>(true)).Single(); var setupData = new SerializedObject(waves); var sequence = setupData.FindProperty("waves");
                    // Isolate the unchanged upper encounter; the full route is covered by the door transition test.
                    for (int i = 0; i < 3; i++) { var wave = sequence.GetArrayElementAtIndex(i); wave.FindPropertyRelative("Groups").arraySize = 0; wave.FindPropertyRelative("Variations").arraySize = 0; }
                    setupData.ApplyModifiedPropertiesWithoutUndo(); waves.ConfigureVariety(true, seed);
                }
                SceneManager.sceneLoaded += Configure;
                try { EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null; }
                finally { SceneManager.sceneLoaded -= Configure; }
                Bind(); menu.BeginGame(); health.Invincible = true; Place(new Vector3(0, .05f, -20)); yield return Until(() => waves.GateOpen, 3, "Upper fixture access");
                var data = new SerializedObject(waves); var slots = data.FindProperty("secondRoomSpawns"); for (int i = 0; i < slots.arraySize; i++) Assert.That(Vector3.Distance(((Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue).position, new Vector3(0, 8, 14)), Is.GreaterThan(3), "General spawn cannot occupy the turret slot");
                Place(new Vector3(0, 8.05f, 5)); yield return Until(() => waves.WaveIndex == 3 && waves.PendingSpawns == 0 && waves.LivingEnemies == 5, 10, "Upper seed " + seed);
                Assert.That(waves.Living.Any(h => h.name.Contains("Turret")), Is.True);
            }
        }
    }
}
