using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Visual overhaul (07/11/2026). Runs on Assets/IronRequiem/Scenes/VisualOverhaul/MainScene_VisualLab.unity
    // (arms "before" captures on BunkerLab). Virtual Input System devices only; no window focus, no desktop input.
    // Fixed 60 Hz simulated time where timing is measured (deterministic frame counts, not FPS measurements).
    public sealed class VisualOverhaulTests
    {
        const string Lab = "Assets/IronRequiem/Scenes/VisualOverhaul/MainScene_VisualLab.unity";
        const string Bunker = "Assets/IronRequiem/Scenes/BunkerLab.unity";
        const string Reports = "Tools/IronRequiem/Reports/VisualOverhaul";
        const string Shots = Reports + "/Shots";
        PlayerCharacterController player; PlayerWeaponsManager weapons; StolenWeaponCarrier carrier; ProtagonistHandsPresentation hands; AdvancedPlayerMovement motion;
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        bool background; float capture; readonly List<string> log = new List<string>();

        IEnumerator Load(string scene, bool platform)
        {
            log.Clear();
            Directory.CreateDirectory(Shots);
            capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false;
            EditorSceneManager.LoadSceneInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (menu) { menu.BeginGame(); menu.enabled = false; }
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (waves) waves.enabled = false;
            Time.timeScale = 1;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); weapons = player.GetComponent<PlayerWeaponsManager>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            hands = player.GetComponent<ProtagonistHandsPresentation>(); motion = player.GetComponent<AdvancedPlayerMovement>();
            player.GetComponent<Health>().Invincible = true;
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            if (platform)
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "VOTestFloor"; floor.transform.position = new Vector3(0, 29.5f, 0); floor.transform.localScale = new Vector3(80, 1, 80);
                Move(new Vector3(0, 30.05f, -20));
            }
            Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.6f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1; Time.captureDeltaTime = capture; AudioListener.pause = false; Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("VOCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }

        void Move(Vector3 p) { var c = player.GetComponent<CharacterController>(); c.enabled = false; player.transform.SetPositionAndRotation(p, Quaternion.identity); c.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        void Keys(params Key[] pressed) { Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keys, new KeyboardState(pressed)); }
        void Buttons(bool fire, bool aim) { var s = new MouseState(); if (fire) s = s.WithButton(MouseButton.Left); if (aim) s = s.WithButton(MouseButton.Right); InputSystem.QueueStateEvent(mouse, s); }
        IEnumerator Tap(params Key[] pressed) { Keys(pressed); yield return null; yield return null; Keys(); yield return null; }
        void Shot(string name) => FrameGrab.Save(player.PlayerCamera, Shots + "/" + name + ".png", 960, 540);
        void Write(string name) { File.WriteAllText(Reports + "/test-" + name + ".txt", string.Join("\n", log)); log.Clear(); }
        IEnumerator WaitUp(float seconds = 3) { float t = 0; while (weapons.SwitchState != PlayerWeaponsManager.WeaponSwitchState.Up && t < seconds) { t += Time.deltaTime; yield return null; } Assert.That(weapons.SwitchState, Is.EqualTo(PlayerWeaponsManager.WeaponSwitchState.Up)); }

        [UnityTest] public IEnumerator LabLoadsCleanAndIsDressed()
        {
            yield return Load(Lab, false); yield return new WaitForSeconds(1.5f);
            var decalRoot = GameObject.Find("VO_Decals"); // mesh decals (static quads), no DecalProjector left
            var decals = decalRoot ? decalRoot.GetComponentsInChildren<MeshRenderer>().Length : 0;
            Assert.That(Object.FindObjectsByType<DecalProjector>(FindObjectsSortMode.None).Length, Is.Zero, "URP decal projectors replaced by mesh decals");
            var reflection = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length;
            int probes = LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0;
            var modules = Enumerable.Range(0, 9).Select(i => weapons.GetWeaponAtSlotIndex(i)).Where(w => w).Select(w => w.GetComponent<ArmModuleVisual>()).Count(m => m);
            var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None); var lit = enemies.Count(e => e.GetComponent<EnemyStateLights>());
            var hs = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.sharedMaterials.Any(m => m && m.shader && m.shader.name == "IronRequiem/HardSurface"));
            var flicker = Object.FindObjectsByType<LightFlicker>(FindObjectsSortMode.None).Length;
            log.Add($"decals {decals}, reflection probes {reflection}, baked light probes {probes}, arm modules {modules}, enemies {enemies.Length} (state lights {lit}), HardSurface renderers {hs}, flickering fixtures {flicker}, renderer index {player.PlayerCamera.GetUniversalAdditionalCameraData().scriptableRenderer?.GetType().Name}");
            Shot("lab-play-start"); Write("lab-load");
            Assert.That(decals, Is.GreaterThanOrEqualTo(20)); Assert.That(reflection, Is.GreaterThanOrEqualTo(10)); Assert.That(probes, Is.GreaterThan(100));
            Assert.That(modules, Is.EqualTo(2)); Assert.That(lit, Is.EqualTo(enemies.Length)); Assert.That(enemies.Length, Is.EqualTo(3), "no enemy removed");
        }

        // Switch cannon -> shotgun -> cannon: duration, fire blocked, ADS held through the switch, switch during a slide.
        [UnityTest] public IEnumerator ModuleSwitchAnimatesBlocksFireAndWorksWithAdsAndSlide()
        {
            yield return Load(Lab, false);
            weapons.SwitchToWeaponIndex(0); yield return WaitUp(); yield return new WaitForSeconds(.3f);
            var w0 = weapons.GetWeaponAtSlotIndex(0); var w1 = weapons.GetWeaponAtSlotIndex(1); var m0 = w0.GetComponent<ArmModuleVisual>(); var m1 = w1.GetComponent<ArmModuleVisual>();
            int shots = 0, blocked = 0; void Count() { shots++; if (weapons.SwitchState != PlayerWeaponsManager.WeaponSwitchState.Up) blocked++; } w0.OnShootProcessed += Count; w1.OnShootProcessed += Count;
            string[] modes = { "plain", "ads", "slide" };
            for (int run = 0; run < 3; run++)
            {
                string mode = modes[run]; int target = run % 2 == 0 ? 1 : 0; var from = target == 1 ? m0 : m1; var to = target == 1 ? m1 : m0;
                int unlock = from.UnlockCount, dock = to.DockCount; shots = 0; blocked = 0;
                if (mode == "slide") { Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(.15f); Keys(Key.W, Key.LeftShift, Key.C); yield return null; Assert.That(motion.IsSliding, Is.True, "slide started"); }
                weapons.SwitchToWeaponIndex(target); float start = Time.time; int frame = 0; bool slidDuring = false;
                while (weapons.SwitchState != PlayerWeaponsManager.WeaponSwitchState.Up && Time.time - start < 2)
                {
                    Buttons(true, mode == "ads"); yield return null; frame++; slidDuring |= motion.IsSliding;
                    if (run == 0 && frame % 4 == 0) Shot("swap-" + frame.ToString("00"));
                    log.Add($"{mode} f{frame} t={Time.time - start:F3} state={weapons.SwitchState} p={weapons.SwitchPhaseProgress:F2} lights={to.LightsOn:F2} offset={(weapons.GetActiveWeapon() == from.GetComponent<WeaponController>() ? from.ModuleRoot.localPosition : to.ModuleRoot.localPosition).ToString("F3")} shots={shots}");
                }
                float duration = Time.time - start; int shotsDuring = blocked;
                Buttons(false, mode == "ads"); yield return null; yield return null;
                if (mode == "ads") { for (int i = 0; i < 20; i++) { Buttons(false, true); yield return null; } Assert.That(weapons.IsAiming, Is.True, "ADS after the switch"); if (run == 1) Shot("swap-ads-after"); }
                Buttons(false, false); Keys(); yield return new WaitForSeconds(.3f);
                log.Add($"{mode}: duration {duration:F3} s, shots while switching {shotsDuring}, unlocks +{from.UnlockCount - unlock}, docks +{to.DockCount - dock}, slide during switch {slidDuring}");
                Assert.That(duration, Is.InRange(.5f, .7f), mode + " duration"); Assert.That(shotsDuring, Is.Zero, mode + " no fire while switching");
                Assert.That(from.UnlockCount - unlock, Is.EqualTo(1)); Assert.That(to.DockCount - dock, Is.EqualTo(1));
                if (mode == "slide") Assert.That(slidDuring, Is.True);
                // Fire works again once up.
                shots = 0; Buttons(true, false); for (int i = 0; i < 30; i++) yield return null; Buttons(false, false); yield return null;
                Assert.That(shots, Is.GreaterThan(0), mode + " fires after the switch"); yield return new WaitForSeconds(target == 1 ? 1.4f : .4f);
            }
            w0.OnShootProcessed -= Count; w1.OnShootProcessed -= Count; Write("module-switch");
        }

        [UnityTest] public IEnumerator ShotgunKeepsFourChargesAndBankReload()
        {
            yield return Load(Lab, false);
            weapons.SwitchToWeaponIndex(1); yield return WaitUp(); yield return new WaitForSeconds(.3f);
            var w = weapons.GetActiveWeapon(); var reload = w.GetComponent<CylinderBankReload>(); var cells = w.GetComponent<WeaponFuelCellHandler>();
            Assert.That(w.MaxAmmo, Is.EqualTo(4)); Assert.That(reload, Is.Not.Null); Assert.That(cells.FuelCells.Length, Is.EqualTo(4));
            Assert.That(cells.FuelCells.All(c => c.GetComponentsInChildren<Renderer>().Any(r => r.enabled)), Is.True, "4 cells visible");
            int shots = 0; void Count() => shots++; w.OnShootProcessed += Count;
            for (int i = 0; i < 4; i++) { Buttons(true, false); yield return null; yield return null; Buttons(false, false); log.Add("after shot " + (i + 1) + ": ammo " + w.GetCurrentAmmo()); if (i < 3) yield return new WaitForSeconds(.95f); else yield return new WaitForSeconds(.1f); }
            Assert.That(shots, Is.EqualTo(4)); Assert.That(reload.IsReloading, Is.True, "bank reload after the 4th charge");
            float t = 0; int f = 0; while (reload.IsReloading && t < 3) { t += Time.deltaTime; if (++f % 12 == 0) Shot("shotgun-reload-" + f.ToString("00")); yield return null; }
            log.Add($"reload {t:F2} s, vented {reload.Vented}, ammo {w.GetCurrentAmmo()}");
            Assert.That(reload.Vented, Is.True); Assert.That(w.GetCurrentAmmo(), Is.EqualTo(4));
            w.OnShootProcessed -= Count; Write("shotgun");
        }

        [UnityTest] public IEnumerator EnemyLightsFollowStatesWarnAndGoOutOnDeath()
        {
            yield return Load(Lab, false);
            var t07 = Object.FindObjectsByType<EnemyStateLights>(FindObjectsSortMode.None).First(e => e.GetComponent<EnemyMobile>());
            var turret = Object.FindObjectsByType<EnemyStateLights>(FindObjectsSortMode.None).FirstOrDefault(e => e.GetComponent<EnemyTurret>());
            // Isolate the observed T-07: the other robots could block its line of fire or shoot it.
            foreach (var other in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) if (other.gameObject != t07.gameObject) other.gameObject.SetActive(false); // turret included (it can hit the T-07 from the pit)
            yield return new WaitForSeconds(.3f);
            var seen = new HashSet<EnemyStateLights.LightState> { t07.State }; var colors = new Dictionary<EnemyStateLights.LightState, Color> { [t07.State] = t07.CurrentColor };
            log.Add("T-07 initial " + t07.State + " " + t07.CurrentColor + (turret ? "; turret " + turret.State : ""));
            // Walk into the T-07's view: it detects the player, follows and attacks (charge = warning before the shot).
            // 9 m in front of the T-07, on the NavMesh (the level has a pit; a chase towards it would kill the robot).
            // 9 m from the T-07 towards the player start (solid floor; the level pit is on the other side).
            var toStart = player.transform.position - t07.transform.position; toStart.y = 0; var p = t07.transform.position + toStart.normalized * 9; Move(p + Vector3.up * .1f); player.transform.LookAt(new Vector3(t07.transform.position.x, player.transform.position.y, t07.transform.position.z));
            float t = 0; bool shot = false; t07.GetComponent<EnemyController>().onAttack += () => shot = true;
            var lastField = typeof(EnemyStateLights).GetField("lastAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); int frames = 0; var ec = t07.GetComponent<EnemyController>();
            while (t < 20 && !(seen.Contains(EnemyStateLights.LightState.Warning) && shot)) { yield return null; t += Time.deltaTime; if (!ec) { log.Add("T-07 destroyed at " + t.ToString("F2") + " s (killed by another robot)"); break; } if (++frames % 30 == 0) { var cw = ec.GetCurrentWeapon(); log.Add($"{t:F2}s {t07.State} sinceShot={Time.time - (float)lastField.GetValue(t07):F2} weapon={(cw ? cw.name + " delay " + cw.DelayBetweenShots : "null")} inRange={ec.IsTargetInAttackRange} seeing={ec.IsSeeingTarget}"); } if (seen.Add(t07.State)) { colors[t07.State] = t07.CurrentColor; log.Add($"{t:F2}s state {t07.State} colour {t07.CurrentColor}"); if (t07.State == EnemyStateLights.LightState.Warning) Shot("enemy-t07-warning"); if (t07.State == EnemyStateLights.LightState.Attack) Shot("enemy-t07-attack"); } }
            log.Add("states seen: " + string.Join(",", seen) + "; shot " + shot + " at " + t.ToString("F2") + " s"); Write("enemy-lights-states");
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(2), "state changes"); Assert.That(seen.Contains(EnemyStateLights.LightState.Attack) || seen.Contains(EnemyStateLights.LightState.Warning), Is.True);
            log.Add("shot observed " + shot + " after " + t.ToString("F2") + " s"); Write("enemy-lights-states");
            Assert.That(seen.Contains(EnemyStateLights.LightState.Warning), Is.True, "warning before firing");
            Assert.That(colors.Values.Distinct().Count(), Is.GreaterThanOrEqualTo(2), "different colour per state");
            var indicators = t07.GetComponent<EnemyPowerIndicators>();
            t07.GetComponent<Health>().Kill(); yield return null; yield return null;
            Assert.That(t07.State, Is.EqualTo(EnemyStateLights.LightState.Dead)); if (indicators) Assert.That(indicators.Indicators.All(r => !r || !r.enabled), Is.True, "lights off on death");
            log.Add("dead: state " + t07.State + ", indicators off " + (indicators ? indicators.Indicators.All(r => !r || !r.enabled).ToString() : "n/a"));
            Write("enemy-lights");
        }

        // Patrol safety: robots left alone (player far away) for 15 s, original MainScene vs the variant with the rebaked NavMesh.
        [UnityTest] public IEnumerator PatrolSurvivalMain() => Patrol("Assets/FPS/Scenes/MainScene.unity", "main");
        [UnityTest] public IEnumerator PatrolSurvivalLab() => Patrol(Lab, "lab");
        IEnumerator Patrol(string scene, string tag)
        {
            yield return Load(scene, false); Move(player.transform.position + Vector3.down * 0); player.GetComponent<Health>().Invincible = true;
            var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).ToList(); int start = enemies.Count;
            for (float t = 0; t < 15; t += Time.deltaTime) { yield return null; }
            var alive = enemies.Where(e => e && e.GetComponent<Health>() && e.GetComponent<Health>().CurrentHealth > 0).ToList();
            log.Add(tag + ": start " + start + ", alive after 15 s " + alive.Count + " :: " + string.Join("; ", alive.Select(e => e.name + " y=" + e.transform.position.y.ToString("F1"))));
            Write("patrol-" + tag);
        }

        // Arm continuity with the new modules: the cut elbow end of the short FPS forearm must stay covered by the
        // sleeves during the punch and both executions. Same probe as BunkerPolishTests.
        int cutFrames, armFrames;
        void ArmProbe()
        {
            var cam = player.PlayerCamera;
            void One(Transform forearm, Renderer view, Renderer sleeve)
            {
                if (!forearm || !view || !view.enabled || !view.gameObject.activeInHierarchy) return; armFrames++;
                var v = cam.WorldToViewportPoint(forearm.position); bool inside = v.z > cam.nearClipPlane && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1;
                if (inside && !(sleeve && sleeve.enabled)) cutFrames++;
            }
            One(hands.LeftForearm, hands.LeftForearmView, hands.LeftSleeve); One(hands.RightForearm, hands.RightForearmView, hands.RightSleeve);
        }
        [UnityTest] public IEnumerator ArmsBefore() => Arms(Bunker, "before", "Assets/IronRequiem/Prefabs/SecondaryShotgun/");
        [UnityTest] public IEnumerator ArmsAfter() => Arms(Lab, "after", "Assets/IronRequiem/Prefabs/VisualOverhaul/", "_VO");
        IEnumerator Arms(string scene, string tag, string folder, string suffix = "")
        {
            yield return Load(scene, true); player.PlayerCamera.transform.localRotation = Quaternion.identity;
            foreach (int slot in new[] { 0, 1 })
            {
                weapons.SwitchToWeaponIndex(slot); yield return WaitUp(); yield return new WaitForSeconds(.4f); cutFrames = armFrames = 0;
                yield return Tap(Key.F);
                for (int i = 0; i < 40; i++) { if (i % 8 == 0) Shot("arms-" + tag + "-punch-slot" + slot + "-" + i.ToString("00")); ArmProbe(); yield return null; }
                log.Add($"{tag} punch slot {slot}: forearm frames {armFrames}, uncovered cut frames {cutFrames}"); Assert.That(cutFrames, Is.Zero, "punch cut visible slot " + slot);
                yield return new WaitForSeconds(.4f);
            }
            foreach (var name in new[] { "T07_HitFeedback_v04_NavThrottle", "BigBot_Burst_v10_NavThrottle" })
            {
                cutFrames = armFrames = 0; player.enabled = name.StartsWith("BigBot"); Move(new Vector3(0, 30.05f, -12));
                var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + suffix + ".prefab"), new Vector3(0, 30.05f, -6), Quaternion.Euler(0, 180, 0));
                foreach (var b in new Behaviour[] { enemy.GetComponent<EnemyMobile>(), enemy.GetComponent<EnemyController>(), enemy.GetComponent<BigBotCombat>(), enemy.GetComponent<UnityEngine.AI.NavMeshAgent>() }) if (b) b.enabled = false;
                yield return null; yield return null;
                var h = enemy.GetComponent<Health>(); h.TakeDamage(h.MaxHealth * .3f, player.gameObject);
                var box = enemy.GetComponent<Collider>(); Move(new Vector3(box.bounds.center.x, box.bounds.min.y + .03f, box.bounds.min.z - 1.2f)); player.PlayerCamera.transform.LookAt(box.bounds.center); Physics.SyncTransforms();
                yield return null; yield return null;
                Assert.That(carrier.TryExecute(enemy.GetComponent<EnemyExecutionSource>()), Is.True, name);
                for (int i = 0; i < 240; i++)
                {
                    if (i % 12 == 0) Shot("arms-" + tag + "-exec-" + name.Substring(0, 6) + "-" + i.ToString("000")); ArmProbe(); yield return null;
                    if (!carrier.ExecutionInProgress && !hands.IsDisarming && i > 30) break;
                }
                log.Add($"{tag} execution {name}: forearm frames {armFrames}, uncovered cut frames {cutFrames}"); Assert.That(cutFrames, Is.Zero, name + " cut visible");
                player.enabled = true; yield return Tap(Key.G); yield return new WaitForSeconds(.6f); if (enemy) Object.Destroy(enemy); yield return null;
            }
            Write("arms-" + tag);
        }
    }
}
