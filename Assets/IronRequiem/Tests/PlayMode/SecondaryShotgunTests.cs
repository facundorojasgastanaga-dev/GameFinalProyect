using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Lab tests for the secondary shotgun, slide cooldown and execution threshold.
    // Virtual Input System devices only: no window focus or desktop control.
    public sealed class SecondaryShotgunTests
    {
        const string Reports = "Tools/IronRequiem/Reports/SecondaryShotgun", Lab = "Assets/IronRequiem/Scenes/SecondaryShotgunLab.unity";
        const string Prefabs = "Assets/IronRequiem/Prefabs/CombatFollowup/";
        PlayerCharacterController player; PlayerWeaponsManager weapons; StolenWeaponCarrier carrier; AdvancedPlayerMovement motion;
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        bool background; float capture; GameObject current; readonly List<string> log = new List<string>();

        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = 0; Time.timeScale = 1;
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); menu.BeginGame(); menu.enabled = false; Object.FindAnyObjectByType<CombatTwoRoomsLab>().enabled = false;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); weapons = player.GetComponent<PlayerWeaponsManager>(); carrier = player.GetComponent<StolenWeaponCarrier>(); motion = player.GetComponent<AdvancedPlayerMovement>();
            player.GetComponent<Health>().Invincible = true;
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "ShotgunLabFloor"; floor.transform.position = new Vector3(0, 29.5f, 0); floor.transform.localScale = new Vector3(160, 1, 160);
            Move(new Vector3(0, 30.05f, -40)); Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.5f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1; Time.captureDeltaTime = capture; AudioListener.pause = false; Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("ShotgunCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }

        void Move(Vector3 p) { var c = player.GetComponent<CharacterController>(); c.enabled = false; player.transform.SetPositionAndRotation(p, Quaternion.identity); c.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        void Keys(params Key[] pressed) { Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keys, new KeyboardState(pressed)); }
        IEnumerator Tap(params Key[] pressed) { Keys(pressed); yield return null; yield return null; Keys(); yield return null; }
        void Write(string name) { Directory.CreateDirectory(Reports); File.WriteAllText(Reports + "/" + name + ".txt", string.Join("\n", log)); log.Clear(); }
        void Capture(string name)
        {
            var camera = player.PlayerCamera; var rt = new RenderTexture(1280, 720, 24); var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); var old = RenderTexture.active; var target = camera.targetTexture;
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { camera.targetTexture = target; RenderTexture.active = old; Object.Destroy(rt); Object.Destroy(tex); }
        }
        float TopViewport(WeaponController w)
        {
            float top = 0; var cam = weapons.WeaponCamera ? weapons.WeaponCamera : player.PlayerCamera;
            foreach (var r in w.WeaponRoot.GetComponentsInChildren<Renderer>()) { var b = r.bounds; for (int i = 0; i < 8; i++) { var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)); var v = cam.WorldToViewportPoint(c); if (v.z > 0 && Mathf.Abs(v.x - .5f) < .06f) top = Mathf.Max(top, v.y); } }
            return top;
        }
        // Largest per-frame viewport displacement of the visible weapon root and forearm socket while switching (60 Hz).
        readonly List<string> trace = new List<string>(); Vector3 lastRoot, lastSocket; WeaponController lastWeapon; float maxRoot, maxSocket; int swaps; string jumpReport;
        void Track(bool reset)
        {
            var cam = player.PlayerCamera; var w = weapons.GetActiveWeapon(); var hands = player.GetComponent<ProtagonistHandsPresentation>();
            var root = cam.WorldToViewportPoint(w.WeaponRoot.transform.position); var socket = cam.WorldToViewportPoint(hands.RightModuleSocket.position);
            bool visible = root.z > 0 && root.y > -.1f && root.y < 1.1f && root.x > -.1f && root.x < 1.1f;
            if (reset) { maxRoot = maxSocket = 0; swaps = 0; }
            else if (w != lastWeapon) swaps++;
            else if (visible) { maxRoot = Mathf.Max(maxRoot, Vector2.Distance(root, lastRoot)); maxSocket = Mathf.Max(maxSocket, Vector2.Distance(socket, lastSocket)); }
            trace.Add(Time.frameCount + " " + w.SourcePrefab.name.Substring(0, 12) + " root(" + root.x.ToString("F2") + "," + root.y.ToString("F2") + ") socket(" + socket.x.ToString("F2") + "," + socket.y.ToString("F2") + ") vis=" + visible + " repr=" + hands.CurrentRepresentation);
            lastRoot = root; lastSocket = socket; lastWeapon = w;
            jumpReport = "max visible per-frame weapon " + maxRoot.ToString("F3") + " / forearm socket " + maxSocket.ToString("F3") + " viewport units; weapon swaps " + swaps;
        }
        WeaponController Slot(int i) => weapons.GetWeaponAtSlotIndex(i);
        static readonly System.Reflection.FieldInfo SwitchState = typeof(PlayerWeaponsManager).GetField("m_WeaponSwitchState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bool Switching() => SwitchState.GetValue(weapons).ToString() != "Up";
        IEnumerator WaitEquipped(WeaponController w, float timeout = 3)
        {
            float end = Time.time + timeout; while (Time.time < end && !(weapons.GetActiveWeapon() == w && w.IsWeaponActive && Switching() == false)) yield return null;
            Assert.That(weapons.GetActiveWeapon(), Is.SameAs(w));
        }
        IEnumerator Spawn(string name, Vector3 position)
        {
            // The test floor is above the lab NavMesh: navigation and AI decisions are disabled before the first Update,
            // so only Health, Damageable, colliders and execution components are exercised.
            current = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab"), position, Quaternion.Euler(0, 180, 0));
            var m = current.GetComponent<EnemyMobile>(); if (m) m.enabled = false; var e = current.GetComponent<EnemyController>(); e.enabled = false;
            var big = current.GetComponent<BigBotCombat>(); if (big) big.enabled = false; var agent = current.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent) agent.enabled = false;
            yield return null; yield return null;
        }
        void Aim(float gap)
        {
            var box = current.GetComponent<Collider>(); Move(new Vector3(box.bounds.center.x, box.bounds.min.y + .03f, box.bounds.min.z - gap));
            player.PlayerCamera.transform.LookAt(box.bounds.center); Physics.SyncTransforms(); Cursor.lockState = CursorLockMode.Locked;
        }

        [UnityTest] public IEnumerator ShotgunIsSecondaryAndCoexistsWithStolenWeapon()
        {
            var primary = Slot(0); var shotgun = Slot(1);
            Assert.That(primary, Is.Not.Null); Assert.That(shotgun, Is.Not.Null);
            Assert.That(primary.SourcePrefab.name, Is.EqualTo("Weapon_IR_ArmModule_v07")); Assert.That(shotgun.SourcePrefab.name, Is.EqualTo("Weapon_Shotgun_Secondary"));
            Assert.That(Slot(2), Is.Null); Assert.That(weapons.GetActiveWeapon(), Is.SameAs(primary));
            yield return Tap(Key.Digit2); yield return WaitEquipped(shotgun); log.Add("Digit2 -> shotgun slot index 1");
            yield return Tap(Key.Digit1); yield return WaitEquipped(primary); log.Add("Digit1 -> primary slot index 0");
            yield return Tap(Key.Digit2); yield return WaitEquipped(shotgun);
            // Real E execution on a damaged T-07 while the shotgun is equipped.
            player.enabled = false; Move(new Vector3(-10, 30.05f, -12)); yield return Spawn("T07_HitFeedback_v04", new Vector3(-10, 30.05f, -6));
            var h = current.GetComponent<Health>(); h.TakeDamage(h.MaxHealth * .3f, player.gameObject); Aim(1.2f); yield return null; yield return null;
            Assert.That(carrier.ExecutionCandidate, Is.Not.Null); yield return Tap(Key.E);
            yield return new WaitForSeconds(.3f); var stolen = Slot(2);
            Assert.That(stolen, Is.Not.Null, "Stolen weapon takes the first free slot (3)"); Assert.That(carrier.Held, Is.Not.Null); Assert.That(carrier.Held.EquippedWeapon, Is.SameAs(stolen));
            Assert.That(Slot(0), Is.SameAs(primary)); Assert.That(Slot(1), Is.SameAs(shotgun)); Assert.That(Slot(3), Is.Null);
            float end = Time.time + 4; while (Time.time < end && (player.GetComponent<ProtagonistHandsPresentation>().IsDisarming || weapons.GetActiveWeapon() != stolen)) yield return null;
            Assert.That(weapons.GetActiveWeapon(), Is.SameAs(stolen)); log.Add("E execution with shotgun equipped -> stolen weapon slot index 2, active; primary and shotgun kept");
            float energy = carrier.Held.Ammo.Remaining;
            weapons.SwitchToWeaponIndex(1); yield return WaitEquipped(shotgun); weapons.SwitchToWeaponIndex(2); yield return WaitEquipped(stolen);
            Assert.That(carrier.Held.Ammo.Remaining, Is.EqualTo(energy)); log.Add("shotgun <-> stolen switch keeps energy " + energy);
            player.enabled = true; yield return Tap(Key.G); yield return new WaitForSeconds(.6f);
            Assert.That(carrier.Held, Is.Null); Assert.That(Slot(2), Is.Null); Assert.That(Slot(0), Is.SameAs(primary)); Assert.That(Slot(1), Is.SameAs(shotgun));
            Assert.That(weapons.GetActiveWeapon(), Is.Not.Null); log.Add("G throw frees slot 3 only; active after throw " + weapons.GetActiveWeapon().SourcePrefab.name);
            Write("inventory");
        }

        [UnityTest] public IEnumerator ShotgunDamageFallsOffWithDistanceAndRange()
        {
            var shotgun = Slot(1); weapons.SwitchToWeaponIndex(1); yield return WaitEquipped(shotgun); player.enabled = false;
            var falloff = shotgun.ProjectilePrefab.GetComponent<ProjectileDistanceFalloff>(); var p = shotgun.ProjectilePrefab.GetComponent<ProjectileStandard>();
            Assert.That(falloff, Is.Not.Null); Assert.That(falloff.Multiplier(3), Is.EqualTo(1)); Assert.That(falloff.Multiplier(12), Is.EqualTo(.65f).Within(1e-4)); Assert.That(falloff.Multiplier(25), Is.EqualTo(.3f)); Assert.That(falloff.Multiplier(33), Is.EqualTo(0)); Assert.That(falloff.MaximumDistance, Is.EqualTo(32));
            Assert.That(shotgun.BulletsPerShot * p.Damage, Is.EqualTo(90)); Assert.That(p.Speed * p.MaxLifeTime, Is.EqualTo(32).Within(.01f));
            var results = new Dictionary<float, float>();
            foreach (float gap in new[] { 3f, 12f, 22f, 40f })
            {
                yield return Spawn("T07_HitFeedback_v04", new Vector3(0, 30.05f, 10)); var h = current.GetComponent<Health>();
                Aim(gap); yield return new WaitForSeconds(shotgun.DelayBetweenShots + .05f); float total = 0;
                for (int shot = 0; shot < 3; shot++)
                {
                    float wait = Time.time + 6; while (shotgun.GetCurrentAmmo() < 1 && Time.time < wait) yield return null;
                    Random.InitState(100 + shot); h.CurrentHealth = h.MaxHealth; float before = h.CurrentHealth;
                    Assert.That(shotgun.HandleShootInputs(true, true, false), Is.True, "shot at " + gap);
                    yield return new WaitForSeconds(.4f); total += before - h.CurrentHealth;
                    yield return new WaitForSeconds(shotgun.DelayBetweenShots);
                }
                results[gap] = total / 3; log.Add("gap " + gap + " m: mean damage per shot " + results[gap].ToString("F1") + " (T-07 " + h.MaxHealth + " HP)");
                Object.Destroy(current); yield return null;
            }
            Assert.That(results[3], Is.GreaterThan(60)); Assert.That(results[3], Is.LessThanOrEqualTo(90.01f));
            Assert.That(results[12], Is.LessThan(results[3] * .6f)); Assert.That(results[22], Is.LessThan(results[12])); Assert.That(results[22], Is.LessThanOrEqualTo(27.01f)); Assert.That(results[40], Is.EqualTo(0));
            Write("falloff");
        }

        [UnityTest] public IEnumerator SlideCooldownStartsAtEndAndNeedsNewPress()
        {
            Assert.That(motion.SlideCooldown, Is.EqualTo(1.5f)); Assert.That(motion.SlideRequiresRelease, Is.True);
            Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(.4f); Assert.That(player.CharacterVelocity.magnitude, Is.GreaterThan(11));
            Keys(Key.W, Key.LeftShift, Key.C); yield return null; yield return null; Assert.That(motion.IsSliding, Is.True); float start = Time.time;
            // Hold C for the whole slide and beyond: never a second slide while held.
            float end = Time.time + 2; while (motion.IsSliding && Time.time < end) yield return null; Assert.That(motion.IsSliding, Is.False); float ended = Time.time;
            log.Add("slide duration " + (ended - start).ToString("F3") + " s; cooldown remaining at end " + motion.SlideCooldownRemaining.ToString("F3"));
            Assert.That(motion.SlideCooldownRemaining, Is.EqualTo(1.5f).Within(.05f));
            float hold = Time.time + 1.8f; while (Time.time < hold) { Assert.That(motion.IsSliding, Is.False); yield return null; }
            log.Add("C held 1.8 s after slide end: no slide (cooldown elapsed but no new press)");
            Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(.1f); Assert.That(motion.SlideReady, Is.True);
            Keys(Key.W, Key.LeftShift, Key.C); yield return null; yield return null; Assert.That(motion.IsSliding, Is.True); log.Add("release + new press after cooldown: slide");
            Keys(Key.W, Key.LeftShift); end = Time.time + 2; while (motion.IsSliding && Time.time < end) yield return null; ended = Time.time;
            // Repeated taps during the cooldown: consumed, no slide, no crouch toggle mid sprint.
            int taps = 0; while (Time.time - ended < 1.3f) { Keys(Key.W, Key.LeftShift, Key.C); yield return null; yield return null; Assert.That(motion.IsSliding, Is.False); Assert.That(player.IsCrouching, Is.False); Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(.08f); taps++; }
            log.Add(taps + " taps within 1.3 s of slide end: no slide and no crouch");
            while (Time.time - ended < 1.55f) yield return null;
            Keys(Key.W, Key.LeftShift, Key.C); yield return null; yield return null; Assert.That(motion.IsSliding, Is.True); log.Add("first tap after " + (Time.time - ended).ToString("F2") + " s: slide");
            Keys(Key.W, Key.LeftShift); end = Time.time + 2; while (motion.IsSliding && Time.time < end) yield return null; yield return new WaitForSeconds(1.6f);
            // Unchanged requirements: no sprint -> native crouch; air -> no slide.
            Keys(Key.W); yield return new WaitForSeconds(.3f); yield return Tap(Key.W, Key.C); Assert.That(motion.IsSliding, Is.False); Assert.That(player.IsCrouching, Is.True); yield return Tap(Key.C); Assert.That(player.IsCrouching, Is.False);
            Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(.4f); Keys(Key.W, Key.LeftShift, Key.Space); yield return new WaitForSeconds(.1f); Keys(Key.W, Key.LeftShift, Key.C); yield return null; yield return null;
            Assert.That(player.IsGrounded, Is.False); Assert.That(motion.IsSliding, Is.False); Keys();
            log.Add("walking C -> native crouch; airborne Shift+C -> no slide");
            Write("slide-cooldown");
        }

        IEnumerator Threshold(string name, Vector3 at)
        {
            player.enabled = false; Move(new Vector3(at.x, 30.05f, at.z - 6)); yield return Spawn(name, at);
            var h = current.GetComponent<Health>(); var source = current.GetComponent<EnemyExecutionSource>(); Assert.That(source, Is.Not.Null);
            h.TakeDamage(h.MaxHealth * .25f, player.gameObject); Assert.That(h.CurrentHealth, Is.EqualTo(h.MaxHealth * .75f)); Aim(1.2f); yield return null; yield return null;
            Assert.That(source.Eligible, Is.False); Assert.That(carrier.ExecutionCandidate, Is.Null);
            yield return Tap(Key.E); yield return new WaitForSeconds(.2f); Assert.That(source.Delivered, Is.False); Assert.That(Slot(2), Is.Null);
            log.Add(name + ": exactly 75% (" + h.CurrentHealth + "/" + h.MaxHealth + ") -> no prompt, E ignored");
            h.TakeDamage(.01f, player.gameObject); yield return null; yield return null; Assert.That(source.Eligible, Is.True); Assert.That(carrier.ExecutionCandidate, Is.SameAs(source));
            // Revalidation at E: heal back to 75% within the same frame as the press.
            h.Heal(1); h.CurrentHealth = h.MaxHealth * .75f; yield return Tap(Key.E); yield return new WaitForSeconds(.2f);
            Assert.That(source.Delivered, Is.False); Assert.That(Slot(2), Is.Null); log.Add(name + ": prompt shown below 75%, healed to 75% before E -> rejected");
            h.TakeDamage(1, player.gameObject); yield return null; yield return null; Assert.That(carrier.ExecutionCandidate, Is.SameAs(source));
            player.enabled = name.StartsWith("BigBot"); Keys(Key.E); yield return null; Keys(Key.E); yield return null; Keys(); yield return null; Keys(Key.E); yield return null; Keys();
            float end = Time.time + 6; while (Time.time < end && (carrier.ExecutionInProgress || player.GetComponent<ProtagonistHandsPresentation>().IsDisarming || !source.Delivered)) yield return null;
            Assert.That(source.Delivered, Is.True); Assert.That(Slot(2), Is.Not.Null); Assert.That(Slot(3), Is.Null); Assert.That(h.CurrentHealth, Is.LessThanOrEqualTo(0));
            int modules = Object.FindObjectsByType<StolenCannon>(FindObjectsSortMode.None).Count(c => c.EquippedWeapon); Assert.That(modules, Is.EqualTo(1));
            yield return Tap(Key.E); yield return new WaitForSeconds(.3f); Assert.That(Slot(3), Is.Null);
            log.Add(name + ": below 75% -> one delivery, repeated E presses do not duplicate (slots 0/1/2 filled, slot 3 empty)");
            player.enabled = true; yield return Tap(Key.G); yield return new WaitForSeconds(.5f); Object.Destroy(current); yield return null;
        }

        [UnityTest] public IEnumerator ExecutionRequiresStrictlyBelow75PercentT07() { yield return Threshold("T07_HitFeedback_v04", new Vector3(-10, 30.05f, 0)); Write("threshold-t07"); }
        [UnityTest] public IEnumerator ExecutionRequiresStrictlyBelow75PercentBigBot() { yield return Threshold("BigBot_Burst_v10", new Vector3(10, 30.05f, 0)); Write("threshold-bigbot"); }

        [UnityTest] public IEnumerator ThrottledNavigationStillPursuesMovingPlayer()
        {
            // Real lab NavMesh (floor y=0) and live AI on the optimised variant.
            Move(new Vector3(0, .05f, -17)); player.enabled = false;
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/SecondaryShotgun/T07_HitFeedback_v04_NavThrottle.prefab"), new Vector3(0, 0, -2), Quaternion.Euler(0, 180, 0));
            var e = go.GetComponent<EnemyController>(); Assert.That(e.DestinationUpdateThreshold, Is.EqualTo(.3f)); var start = go.transform.position;
            yield return null; yield return null; float end = Time.time + 5; while (Time.time < end && !(e.DetectionModule && e.KnownDetectedTarget)) yield return null;
            Assert.That(e.KnownDetectedTarget, Is.Not.Null, "detects player (15 m, detection 16 m)");
            Move(new Vector3(5, .05f, -17)); yield return new WaitForSeconds(.4f);
            float lateral = e.NavMeshAgent.destination.x; log.Add("player moved 5 m sideways: agent destination x=" + lateral.ToString("F2") + " after 0.4 s");
            yield return new WaitForSeconds(.8f);
            float approached = Vector3.Distance(start, player.transform.position) - Vector3.Distance(go.transform.position, player.transform.position);
            log.Add("approached " + approached.ToString("F2") + " m in 1.2 s after detection; in attack range " + e.IsTargetInAttackRange);
            Assert.That(approached > 1 || e.IsTargetInAttackRange, Is.True); Assert.That(lateral, Is.GreaterThan(2), "destination refreshed toward the moved player within 0.4 s");
            Object.Destroy(go); Write("nav-throttle");
        }

        [UnityTest] public IEnumerator CaptureAnimationPoses()
        {
            Time.captureDeltaTime = 1f / 60; var primary = Slot(0); var shotgun = Slot(1);
            player.PlayerCamera.transform.localRotation = Quaternion.identity; yield return new WaitForSeconds(.3f); Capture("pose-primary-idle");
            weapons.SwitchToWeaponIndex(1); Track(true); for (int i = 0; i < 90; i++) { if (i % 6 == 0) Capture("pose-switch-to-shotgun-" + i.ToString("00")); yield return null; Track(false); } log.Add("switch primary->shotgun: " + jumpReport); File.WriteAllLines(Reports + "/trace-switch.txt", trace); trace.Clear();
            yield return WaitEquipped(shotgun); yield return new WaitForSeconds(.3f); Capture("pose-shotgun-idle");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); yield return new WaitForSeconds(.5f); Capture("pose-shotgun-ads");
            log.Add("ADS: shotgun top viewport y=" + TopViewport(shotgun).ToString("F3") + " (crosshair 0.5)");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left)); yield return null; yield return null; Capture("pose-shotgun-ads-fire");
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.4f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null; Capture("pose-shotgun-fire-0"); yield return null; yield return null; Capture("pose-shotgun-fire-2"); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.2f);
            Keys(Key.W, Key.LeftShift); yield return new WaitForSeconds(.4f); Keys(Key.W, Key.LeftShift, Key.C); yield return null; yield return null;
            for (int i = 0; i < 48; i++) { if (i % 8 == 0) Capture("pose-shotgun-slide-" + i.ToString("00")); yield return null; }
            Keys(); yield return new WaitForSeconds(.8f);
            weapons.SwitchToWeaponIndex(0); Track(true); for (int i = 0; i < 90; i++) { if (i % 6 == 0) Capture("pose-switch-to-primary-" + i.ToString("00")); yield return null; Track(false); } log.Add("switch shotgun->primary: " + jumpReport);
            // Execution with the shotgun equipped.
            weapons.SwitchToWeaponIndex(1); yield return WaitEquipped(shotgun); player.enabled = false; Move(new Vector3(-10, 30.05f, -12)); yield return Spawn("T07_HitFeedback_v04", new Vector3(-10, 30.05f, -6));
            var h = current.GetComponent<Health>(); h.TakeDamage(h.MaxHealth * .3f, player.gameObject); Aim(1.2f); yield return null; yield return null; Assert.That(carrier.TryExecute(current.GetComponent<EnemyExecutionSource>()), Is.True);
            for (int i = 0; i < 72; i++) { if (i % 6 == 0) Capture("pose-execution-shotgun-" + i.ToString("00")); yield return null; }
            Time.captureDeltaTime = 0; weapons.SwitchToWeaponIndex(0); var primaryW = Slot(0); player.enabled = true; yield return WaitEquipped(primaryW); InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); yield return new WaitForSeconds(.5f); Capture("pose-primary-ads");
            log.Add("ADS: primary top viewport y=" + TopViewport(primaryW).ToString("F3")); InputSystem.QueueStateEvent(mouse, new MouseState());
            log.Add("captures written");
            Write("captures");
        }
    }
}
