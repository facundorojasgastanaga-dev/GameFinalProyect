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
    public sealed class PhaseFlowTests
    {
        const string Report = "Tools/IronRequiem/Reports/PhaseFlow/";
        Keyboard keys; Mouse mouse; bool background; float capture;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        PlayerCharacterController player; StolenWeaponCarrier carrier; CombatLabMenu menu;
        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; background = Application.runInBackground; Time.captureDeltaTime = 1f / 60; Application.runInBackground = true; Time.timeScale = 1; AudioListener.pause = false;
            devices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            yield return Load("ChaseCorridorLab"); player.enabled = false; yield return new WaitForSeconds(.6f);
        }
        IEnumerator Load(string name)
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); carrier = player.GetComponent<StolenWeaponCarrier>(); menu = Object.FindAnyObjectByType<CombatLabMenu>(); menu.BeginGame(); player.GetComponent<Health>().Invincible = true; yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            // Complete a held load before unloading the fixture, so it cannot activate in the next test.
            Time.timeScale = 1; var entrance = Object.FindAnyObjectByType<ChaseSceneEntrance>();
            if (entrance && entrance.IsPreparing) { SceneManager.LoadScene("ChaseCorridorLab"); yield return null; yield return null; }
            Time.timeScale = 1; AudioListener.pause = false; var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("PhaseCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = capture; InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None; Application.runInBackground = background;
        }
        IEnumerator TapE() { InputSystem.QueueStateEvent(keys, new KeyboardState(Key.E)); yield return null; yield return null; InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; }
        void Place(Vector3 point) { player.GetComponent<AdvancedPlayerMovement>().ResetForCheckpoint(); player.TeleportToCheckpoint(point); Physics.SyncTransforms(); }
        static void Stop(GameObject enemy)
        {
            foreach (var c in enemy.GetComponents<MonoBehaviour>()) if (c is EnemyController || c is EnemyMobile || c is EnemyTurret || c is BigBotCombat || c is SawBotCombat) c.enabled = false;
            var agent = enemy.GetComponent<NavMeshAgent>(); if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
        }
        void Aim(GameObject enemy, float gap = 1)
        {
            var box = enemy.GetComponent<Collider>(); Assert.That(box, Is.Not.Null); Place(new Vector3(box.bounds.center.x, .03f, box.bounds.min.z - gap)); player.PlayerCamera.transform.LookAt(box.bounds.center); Physics.SyncTransforms();
        }
        GameObject Spawn(string path) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), new Vector3(0, 0, -8), Quaternion.identity);
        [UnityTest] public IEnumerator BaselineLegacyPrefabsCannotExecute()
        {
            foreach (string path in new[] { "Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab", "Assets/IronRequiem/Prefabs/Enemies/BigBot_Burst_v10.prefab" })
            {
                var enemy = Spawn(path); yield return null; yield return null; Stop(enemy); var hp = enemy.GetComponent<Health>(); hp.TakeDamage(hp.MaxHealth * .3f, player.gameObject); Aim(enemy); yield return null;
                Assert.That(enemy.GetComponent<EnemyExecutionSource>(), Is.Null, path); yield return TapE(); Assert.That(carrier.Held, Is.Null); Assert.That(hp.CurrentHealth, Is.GreaterThan(0)); Object.Destroy(enemy); yield return null;
            }
            File.WriteAllText(Report + "baseline-execution.txt", "Play Mode reproduction: actual legacy T07 v05 and BigBot v10 at70%HP, real E input at1m and clear ray. Neither advertises execution or gives module/kills: EnemyExecutionSource absent. Input unchanged.");
        }
        [UnityTest] public IEnumerator T07ExecutionThresholdWallDistanceAndSingleDelivery() { yield return Execute("T07_LegRepair_Charged_v05_Execution"); }
        [UnityTest] public IEnumerator BigBotExecutionAndNormalDeathNoDrop() { yield return Execute("BigBot_Burst_v10_Execution"); }
        IEnumerator Execute(string name)
        {
            string path = "Assets/IronRequiem/Prefabs/PhaseFlow/" + name + ".prefab"; var enemy = Spawn(path); yield return null; yield return null; Stop(enemy);
            var source = enemy.GetComponent<EnemyExecutionSource>(); var hp = enemy.GetComponent<Health>(); int deaths = 0; hp.OnDie += () => deaths++;
            hp.TakeDamage(hp.MaxHealth * .25f, player.gameObject); Aim(enemy); yield return TapE(); Assert.That(carrier.Held, Is.Null, "75% is not below threshold");
            hp.TakeDamage(hp.MaxHealth * .05f, player.gameObject); Aim(enemy, 5); yield return TapE(); Assert.That(carrier.Held, Is.Null, "Distance"); Assert.That(hp.CurrentHealth, Is.GreaterThan(0));
            Aim(enemy); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = Vector3.Lerp(player.PlayerCamera.transform.position, enemy.GetComponent<Collider>().bounds.center, .45f); wall.transform.localScale = new Vector3(8, 8, .15f); Physics.SyncTransforms(); yield return TapE(); Assert.That(carrier.Held, Is.Null, "Wall"); Assert.That(hp.CurrentHealth, Is.GreaterThan(0)); Object.Destroy(wall); yield return null;
            Aim(enemy); yield return null; Assert.That(carrier.ExecutionCandidate, Is.SameAs(source)); Assert.That(carrier.ExecutionStatus, Is.EqualTo(CannonPickupStatus.Ready)); yield return TapE(); var module = carrier.Held; Assert.That(module, Is.Not.Null, "E delivers real modular weapon");
            yield return TapE(); yield return new WaitForSeconds(3.5f); Assert.That(deaths, Is.EqualTo(1)); Assert.That(carrier.Held, Is.SameAs(module)); Assert.That(Object.FindObjectsByType<StolenCannon>().Count(m => m.State == StolenCannonState.Available), Is.Zero, "No duplicate automatic death drop");
            Assert.That(module.Ammo.Remaining, Is.EqualTo(module.Ammo.Capacity).Within(.01f));
            var another = Spawn(path); yield return null; yield return null; Stop(another); var hp2 = another.GetComponent<Health>(); hp2.TakeDamage(hp2.MaxHealth * .3f, player.gameObject); Aim(another); yield return TapE(); Assert.That(hp2.CurrentHealth, Is.GreaterThan(0), "Already holding cannot kill target"); Assert.That(carrier.Held, Is.SameAs(module)); hp2.Kill(); yield return new WaitForSeconds(2); Assert.That(Object.FindObjectsByType<StolenCannon>().Count(m => m.State == StolenCannonState.Available), Is.Zero);
            File.WriteAllText(Report + name + ".txt", "Real E: strict75%, distance and wall block;70% ready; single module, single death under repeat E; full energy; held module blocks second kill; normal death creates no collectible. AI frozen to isolate interaction.");
        }
        IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            float end = Time.realtimeSinceStartup + 75, gameEnd = Time.time + seconds;
            while (!condition() && Time.time < gameEnd && Time.realtimeSinceStartup < end) { foreach (var enemy in Object.FindObjectsByType<EnemyController>()) Stop(enemy.gameObject); yield return null; }
            Assert.That(condition(), Is.True, label);
        }
        [UnityTest] public IEnumerator FlatRoomsCullAndConnectToCorridor()
        {
            yield return Load("CombatTwoRoomsLab"); player.enabled = false; var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); var visibility = waves.GetComponent<LabRoomVisibility>(); var entrance = Object.FindAnyObjectByType<ChaseSceneEntrance>();
            Place(new Vector3(0, .03f, -20)); yield return null; Assert.That(visibility.CurrentRoom, Is.EqualTo(1)); int suppressed1 = visibility.SuppressedRenderers; Assert.That(suppressed1, Is.GreaterThan(0));
            foreach (int count in new[] { 6, 4, 5 }) { yield return Until(() => waves.PendingSpawns == 0 && waves.LivingEnemies == count, 15, "Lower wave " + count); foreach (var h in waves.Living.ToArray()) h.Kill(); yield return null; yield return null; }
            float cleared = Time.time; Assert.That(waves.GateOpen, Is.False); yield return Until(() => waves.GateOpen, 5, "Prepared door"); Assert.That(Time.time - cleared, Is.GreaterThan(1.3f));
            Place(new Vector3(0, .03f, 40)); yield return null; Assert.That(visibility.CurrentRoom, Is.EqualTo(2)); int suppressed2 = visibility.SuppressedRenderers; Assert.That(suppressed2, Is.GreaterThan(0));
            yield return Until(() => waves.WaveIndex == 3 && waves.PendingSpawns == 0 && waves.LivingEnemies == 5, 15, "Upper wave");
            foreach (var h in waves.Living) foreach (var r in h.GetComponentsInChildren<Renderer>()) Assert.That(r.forceRenderingOff, Is.False, "Active enemies visible");
            foreach (var h in waves.Living.ToArray()) h.Kill(); yield return Until(() => waves.Completed, 3, "Completed"); yield return null; Assert.That(entrance.IsOpen, Is.False); float start = Time.time;
            yield return Until(() => entrance.IsOpen, 30, "Actual async preload ready"); Assert.That(Time.time - start, Is.GreaterThan(1.3f)); Assert.That(entrance.LoadingProgress, Is.EqualTo(1)); Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CombatTwoRoomsLab"), "Preload does not activate early");
            player.enabled = true; Place(new Vector3(0, .03f, 79)); player.transform.rotation = Quaternion.identity; player.PlayerCamera.transform.localRotation = Quaternion.identity; InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W));
            yield return Until(() => SceneManager.GetActiveScene().name == "ChaseCorridorLab", 15, "Cross corridor portal"); InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; yield return null;
            Assert.That(Object.FindAnyObjectByType<CombatLabMenu>().IsPlaying, Is.True); File.WriteAllText(Report + "flat-flow.txt", "3 lower waves ->1.5s/prewarm barrier -> second room -> final5 -> actual async corridor preload -> min1.5s closed door -> crossing loads corridor directly in play. Suppressed renderers room1=" + suppressed1 + ",room2=" + suppressed2 + ". Physics/LOD enable flags retained; AI frozen for flow. No FPS claim.");
        }
        [UnityTest] public IEnumerator StackedVisibilityAndMenuDuringPreload()
        {
            void Empty(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != "CombatTwoFloorsLab") return;
                var waves = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CombatTwoRoomsLab>()).Single();
                waves.ConfigureVariety(false, 17); var data = new SerializedObject(waves); var list = data.FindProperty("waves"); for (int i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).FindPropertyRelative("Groups").arraySize = 0; data.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int pass = 0; pass < 2; pass++)
            {
                SceneManager.sceneLoaded += Empty; try { yield return Load("CombatTwoFloorsLab"); } finally { SceneManager.sceneLoaded -= Empty; }
                player.enabled = false; var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); var visibility = waves.GetComponent<LabRoomVisibility>(); var entrance = Object.FindAnyObjectByType<ChaseSceneEntrance>();
                Place(new Vector3(0, .03f, -20)); yield return null; Assert.That(visibility.CurrentRoom, Is.EqualTo(1)); int lower = visibility.SuppressedRenderers; Assert.That(lower, Is.GreaterThan(0));
                Place(new Vector3(0, 5, -20)); yield return null; Assert.That(visibility.CurrentRoom, Is.EqualTo(1), "Execution leap is not upper-floor entry");
                yield return Until(() => waves.GateOpen, 5, "Empty fixture opens stair door after delay"); Place(new Vector3(0, 8.03f, 5)); yield return null;
                Assert.That(visibility.CurrentRoom, Is.EqualTo(2)); Assert.That(visibility.SuppressedRenderers, Is.GreaterThan(0));
                foreach (var r in Object.FindObjectsByType<Renderer>().Where(r => r.gameObject.scene == visibility.gameObject.scene && r.name.StartsWith("UpperFarBank"))) { Assert.That(r.forceRenderingOff, Is.False); Assert.That(r.GetComponent<Collider>().enabled, Is.True); }
                if (pass == 0)
                {
                    var visitor = Spawn("Assets/IronRequiem/Prefabs/PhaseFlow/T07_LegRepair_Charged_v05_Execution.prefab"); yield return null; yield return null; Stop(visitor); var nav = visitor.GetComponent<NavMeshAgent>(); nav.enabled = false; visibility.Track(visitor, 1);
                    var view = visitor.GetComponentsInChildren<SkinnedMeshRenderer>().First(); Assert.That(view.forceRenderingOff, Is.True, "Enemy on inactive lower floor hidden"); visitor.transform.position = new Vector3(0, 8, 12); yield return null;
                    Assert.That(view.forceRenderingOff, Is.False, "Pursuing enemy entering the current room becomes visible"); Object.Destroy(visitor); yield return null;
                }
                player.PlayerCamera.transform.localRotation = Quaternion.Euler(8, 180, 0);
                yield return Until(() => entrance.IsPreparing, 5, "Preload starts only after completion");
                if (pass == 0) menu.ReturnToMainMenu(); else menu.RestartFromFirstRoom();
                yield return null; yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CombatTwoFloorsLab"), "Reload must not activate corridor instead");
                var nextMenu = Object.FindAnyObjectByType<CombatLabMenu>(); Assert.That(nextMenu, Is.Not.Null); Assert.That(nextMenu.IsMainMenu, Is.EqualTo(pass == 0)); Assert.That(nextMenu.IsPlaying, Is.EqualTo(pass == 1));
                File.AppendAllText(Report + "preload-menu.txt", "pass=" + pass + " lower suppressed=" + lower + "; upper floor visible and solid; leap stays room1; pending preload -> " + (pass == 0 ? "menu" : "restart") + " returns to source lab correctly.\n");
            }
        }
    }
}
