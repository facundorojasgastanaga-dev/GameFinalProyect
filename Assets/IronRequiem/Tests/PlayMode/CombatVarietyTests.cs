using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class CombatVarietyTests
    {
        const string Report = "Tools/IronRequiem/Reports/CombatVariety";
        PlayerCharacterController player; CombatTwoRoomsLab lab; CombatLabMenu menu;
        Keyboard keys; Mouse mouse; float capture; bool background; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        [UnitySetUp] public IEnumerator Setup()
        {
            Directory.CreateDirectory(Report); capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false;
            devices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse }; yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("VarietyCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }
        IEnumerator Load(string name, int seed)
        {
            Time.timeScale = 1; AudioListener.pause = false; string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; bool prepared = false;
            void Prepare(Scene scene, LoadSceneMode mode) { if (scene.path != path) return; lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CombatTwoRoomsLab>(true)).Single(); lab.ConfigureVariety(true, seed); prepared = true; }
            SceneManager.sceneLoaded += Prepare;
            try { EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null; }
            finally { SceneManager.sceneLoaded -= Prepare; }
            Assert.That(prepared, Is.True, "Seed configured in sceneLoaded before component Start");
            menu = Object.FindAnyObjectByType<CombatLabMenu>(); player = Object.FindAnyObjectByType<PlayerCharacterController>(); player.enabled = false; player.GetComponent<Health>().Invincible = true;
        }
        void Move(Vector3 position) { var controller = player.GetComponent<CharacterController>(); controller.enabled = false; player.transform.position = position; controller.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        IEnumerator Until(Func<bool> predicate, float timeout, string message)
        {
            float end = Time.time + timeout, real = Time.realtimeSinceStartup + 70;
            while (!predicate() && Time.time < end && Time.realtimeSinceStartup < real) { FreezeEnemies(); yield return null; }
            Assert.That(predicate(), Is.True, message);
        }
        void FreezeEnemies()
        {
            if (!lab) return;
            foreach (var health in lab.Living) { if (!health) continue; var saw = health.GetComponent<SawBotCombat>(); if (saw) saw.enabled = false; var big = health.GetComponent<BigBotCombat>(); if (big) big.enabled = false; var mobile = health.GetComponent<EnemyMobile>(); if (mobile) mobile.enabled = false; var enemy = health.GetComponent<EnemyController>(); if (enemy) { enemy.enabled = false; var agent = enemy.NavMeshAgent; if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; } } }
        }
        IEnumerator Key(Key key) { InputSystem.QueueStateEvent(keys, new KeyboardState(key)); yield return null; yield return null; InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; }
        static void Click(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases(); Assert.That(button && button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True);
            var point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position); var es = EventSystem.current; var pointer = new PointerEventData(es) { position = point, button = PointerEventData.InputButton.Left }; var hits = new List<RaycastResult>(); es.RaycastAll(pointer, hits); Assert.That(hits.Count, Is.GreaterThan(0));
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject); Assert.That(target, Is.SameAs(button.gameObject), "Frontmost UI must be intended button"); ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }
        UnityEngine.UI.Button Label(string text) => menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMPro.TMP_Text>().text == text);
        [UnityTest] public IEnumerator IndustrialMenuOptionsPauseDeathAndRestart()
        {
            yield return Load("SawBotLab", 5); Assert.That(menu.IsMainMenu, Is.True); Assert.That(Time.timeScale, Is.Zero); Assert.That(menu.transform.Find("IndustrialShell").gameObject.activeInHierarchy, Is.True); var view = menu.GetComponent<RequiemMenuView>(); Assert.That(view, Is.Not.Null);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Report + "/industrial-menu.png")); yield return null;
            Click(Label("OPCIONES")); Assert.That(view.IsOptionsOpen, Is.True); yield return null;
            Click(Label("VOLVER")); Assert.That(view.IsOptionsOpen, Is.False); yield return null;
            Click(Label("JUGAR")); yield return null; yield return null;
            Assert.That(menu.IsPlaying, Is.True); Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(menu.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g => !g.gameObject.activeInHierarchy), Is.True, "No persistent menu overlay");
            var pause = Object.FindObjectsByType<InGameMenuManager>().Single(p => p.enabled && p.UseIndustrialMenu); yield return Key(UnityEngine.InputSystem.Key.Escape); Assert.That(pause.IsPaused, Is.True); Assert.That(pause.MenuRoot.transform.Find("IndustrialShell"), Is.Not.Null); yield return Key(UnityEngine.InputSystem.Key.Escape); Assert.That(pause.IsPaused, Is.False);
            player.GetComponent<Health>().Invincible = false; player.GetComponent<Health>().Kill(); yield return null; yield return null; Assert.That(menu.IsDeathMenu, Is.True); Click(menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "RestartButton")); yield return null; yield return null; yield return null;
            menu = Object.FindAnyObjectByType<CombatLabMenu>(); Assert.That(menu.IsPlaying, Is.True); lab = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); Assert.That(lab.WaveIndex, Is.LessThanOrEqualTo(0)); menu.ReturnToMainMenu(); yield return null; yield return null; yield return null;
            menu = Object.FindAnyObjectByType<CombatLabMenu>(); Assert.That(menu.IsMainMenu, Is.True); Assert.That(menu.transform.Find("IndustrialShell").gameObject.activeInHierarchy, Is.True); File.WriteAllText(Report + "/menu-playmode.txt", "Real EventSystem raycasts: Play, options/back, Escape pause/resume, death/restart, return to industrial menu. No persistent basic overlay.");
        }
        static string Kind(GameObject go) => go.GetComponent<SawBotCombat>() ? "Saw" : go.GetComponent<BigBotCombat>() ? "Big" : go.GetComponent<EnemyTurret>() ? "Turret" : go.GetComponent<RobotDamageProgression>() ? "T07" : "Drone";
        void CheckSpawns(int expected)
        {
            Assert.That(lab.LivingEnemies, Is.EqualTo(expected)); var roots = lab.Living.Select(h => h.gameObject).ToArray();
            foreach (var root in roots) { Assert.That(root.GetComponentsInChildren<Component>(true).All(c => c), Is.True); Assert.That(root.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, Is.True); Assert.That(Vector3.Distance(root.transform.position, player.transform.position), Is.GreaterThan(6)); }
            for (int i = 0; i < roots.Length; i++) for (int j = i + 1; j < roots.Length; j++) Assert.That(Vector3.Distance(roots[i].transform.position, roots[j].transform.position), Is.GreaterThan(2), "Distinct unoccupied spawn slots");
        }
        [UnityTest] public IEnumerator SeedsChangeMixedSpawnsAndPlayerPositionIsRechecked()
        {
            var signatures = new List<string>();
            foreach (int seed in new[] { 11, 27, 43 })
            {
                yield return Load("CombatVarietyLab", seed); menu.BeginGame(); Move(new Vector3(0, .03f, -21)); yield return Until(() => lab.WaveIndex == 0, 3, "First arena entry");
                if (seed == 27) Move(new Vector3(-18, .03f, 6)); // Move after requests were queued, before actual activation.
                yield return Until(() => lab.PendingSpawns == 0 && lab.LivingEnemies == 2, 12, "Safe varied first wave"); FreezeEnemies(); CheckSpawns(2); Assert.That(lab.Living.Select(h => Kind(h.gameObject)).Distinct().Count(), Is.GreaterThanOrEqualTo(2)); Assert.That(lab.RunSeed, Is.EqualTo(seed));
                signatures.Add(seed + ": " + lab.CurrentComposition + " " + string.Join(";", lab.Living.Select(h => Kind(h.gameObject) + "@" + h.transform.position.ToString("F1")).OrderBy(s => s)));
            }
            Assert.That(signatures.Select(s => s.Substring(s.IndexOf(':') + 1)).Distinct().Count(), Is.GreaterThan(1)); File.WriteAllLines(Report + "/seeds.txt", signatures);
        }
        [UnityTest] public IEnumerator BlockedSlotsRecoverAndOnlyLivingEnemiesAdvanceWaves()
        {
            yield return Load("BunkerLab", 37); var blockers = new List<GameObject>();
            var points = Object.FindObjectsByType<Transform>().Where(t => t.name.StartsWith("Spawn_0_")).ToArray(); Assert.That(points.Length, Is.GreaterThan(6));
            foreach (var point in points) { var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = "SpawnTestBlocker"; block.transform.position = point.position + Vector3.up * 1.5f; block.transform.localScale = new Vector3(4, 3, 4); blockers.Add(block); }
            Physics.SyncTransforms(); menu.BeginGame(); Move(new Vector3(0, .03f, -21)); yield return new WaitForSeconds(1.5f); Assert.That(lab.WaveIndex, Is.Zero); Assert.That(lab.PendingSpawns, Is.EqualTo(7)); Assert.That(lab.LivingEnemies, Is.Zero); Assert.That(lab.WaitingForSpawnSpace, Is.True); Assert.That(lab.GateOpen, Is.False);
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f); Assert.That(lab.LivingEnemies, Is.Zero); Time.timeScale = 1;
            foreach (var block in blockers) { block.GetComponent<Collider>().enabled = false; Object.Destroy(block); } Physics.SyncTransforms(); yield return Until(() => lab.PendingSpawns == 0 && lab.LivingEnemies == 7, 15, "Blocked queue recovers"); CheckSpawns(7);
            var survivor = lab.Living.First(); foreach (var hp in lab.Living.ToArray()) if (hp != survivor) hp.Kill(); yield return new WaitForSeconds(.3f); Assert.That(lab.WaveIndex, Is.Zero); Assert.That(lab.LivingEnemies, Is.EqualTo(1));
            var debris = new GameObject("DetachedModule_Debris_NotEnemy"); debris.transform.position = player.transform.position;
            survivor.Kill(); yield return Until(() => lab.WaveIndex == 1 && lab.PendingSpawns == 0 && lab.LivingEnemies == 4, 15, "Second composition"); CheckSpawns(4);
            foreach (var hp in lab.Living.ToArray()) hp.Kill(); yield return Until(() => lab.WaveIndex == 2 && lab.PendingSpawns == 0 && lab.LivingEnemies == 5, 15, "Third composition"); CheckSpawns(5); Assert.That(lab.GateOpen, Is.False);
            foreach (var hp in lab.Living.ToArray()) hp.Kill(); yield return Until(() => lab.GateOpen, 3, "Only third wave opens gate"); Assert.That(lab.Completed, Is.False); yield return new WaitForSeconds(.6f); Move(new Vector3(0, .03f, 37));
            yield return Until(() => lab.WaveIndex == 3 && lab.PendingSpawns == 0 && lab.LivingEnemies == 5, 15, "Final room"); CheckSpawns(5); Assert.That(lab.Living.Count(h => h.GetComponent<EnemyTurret>()), Is.EqualTo(1), "Fixed central turret retained");
            foreach (var hp in lab.Living.ToArray()) hp.Kill(); yield return Until(() => lab.Completed, 3, "Completes once"); Assert.That(lab.LivingEnemies, Is.Zero); Assert.That(debris, Is.Not.Null); File.WriteAllText(Report + "/waves-playmode.txt", "Blocked physical slots wait/recover; no spawn while paused; Bunker counts7/4/5/5 unchanged; survivor holds wave, bodies/debris ignored, gate after third wave, fixed turret and final completion preserved.");
        }
    }
}
