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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Arms-fix pass (19/11/2026): weapon switch / stolen weapon / T-07 execution regressions B1-B5 and C.
    // Lab: the visual-overhaul lab (BunkerLab protagonist rig; slot 0 = Weapon_IR_ArmCannon_VO, which has ArmModuleVisual
    // AND WeaponDeployMotion, like the campaign arm cannon) on a test platform, with the campaign T-07 (T07_Mech, stolen
    // weapon Weapon_T07_Temporary_Optimized with StolenShellFit). The lab robots are switched off so nothing else can hit
    // the target. Real keys through virtual Input System devices (E, F, G, 1, 2, M1), fixed 60 Hz simulated time, no
    // window focus. Coroutines resume after Update, so every probe reads the pose the previous LateUpdate left.
    public sealed class ArmsFixPassTests
    {
        const string Lab = "Assets/IronRequiem/Scenes/VisualOverhaul/MainScene_VisualLab.unity";
        const string T07 = "Assets/IronRequiem/Prefabs/Campaign/T07_Mech.prefab";
        const string Reports = "Tools/IronRequiem/Reports/ArmsFixPass/tests";
        const float Frame = 1f / 60;

        PlayerCharacterController player; PlayerWeaponsManager weapons; StolenWeaponCarrier carrier; ProtagonistHandsPresentation hands; ProtagonistMelee melee;
        WeaponController cannon; ArmModuleVisual module; GameObject target; Health targetHealth; RobotHitFeedback feedback; EnemyExecutionSource source;
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices; string savedOverrides;
        bool background; float capture, executedAt = -1; int switches; WeaponController lastSwitched;
        readonly List<string> log = new List<string>();
        readonly Dictionary<WeaponController, Transform> attachments = new Dictionary<WeaponController, Transform>();

        [UnitySetUp] public IEnumerator Setup()
        {
            log.Clear(); attachments.Clear(); switches = 0; lastSwitched = null; executedAt = -1; Directory.CreateDirectory(Reports);
            capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = Frame; Time.timeScale = 1; AudioListener.pause = false;
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (menu) { menu.BeginGame(); menu.enabled = false; }
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (waves) waves.enabled = false;
            var flow = Object.FindAnyObjectByType<GameFlowManager>(); if (flow) flow.enabled = false;
            var objectives = Object.FindAnyObjectByType<ObjectiveManager>(); if (objectives) objectives.enabled = false;
            Time.timeScale = 1;
            // only the robot under test: lab robots could shoot it (extra impacts) or the player
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); weapons = player.GetComponent<PlayerWeaponsManager>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            hands = player.GetComponent<ProtagonistHandsPresentation>(); melee = player.GetComponent<ProtagonistMelee>();
            player.GetComponent<Health>().Invincible = true;
            // default bindings for the test (the user's saved remaps, e.g. Melee on the right mouse button, are restored in Cleanup)
            savedOverrides = InputSystem.actions.SaveBindingOverridesAsJson(); InputSystem.actions.RemoveAllBindingOverrides();
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "ArmsFixFloor"; floor.transform.position = new Vector3(0, 29.5f, 0); floor.transform.localScale = new Vector3(80, 1, 80);
            Move(new Vector3(0, 30.05f, -12)); Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(.6f);
            weapons.SwitchToWeaponIndex(0); yield return WaitUp();
            cannon = weapons.GetWeaponAtSlotIndex(0); module = cannon ? cannon.GetComponent<ArmModuleVisual>() : null;
            Assert.That(module, Is.Not.Null, "slot 0 is the arm cannon module");
            Assert.That(module.OverridesSwitchMotion, Is.True, "the cannon animates its own switch");
            Assert.That(cannon.GetComponent<WeaponDeployMotion>(), Is.Not.Null, "the cannon also carries the generic deploy (the B1 stack)");
            Assert.That(melee, Is.Not.Null); Assert.That(hands, Is.Not.Null); Assert.That(carrier, Is.Not.Null);
            weapons.OnSwitchedToWeapon += CountSwitch; carrier.Executed += OnExecuted;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (weapons) weapons.OnSwitchedToWeapon -= CountSwitch; if (carrier) carrier.Executed -= OnExecuted;
            if (savedOverrides != null) { InputSystem.actions.RemoveAllBindingOverrides(); if (savedOverrides.Length > 0) InputSystem.actions.LoadBindingOverridesFromJson(savedOverrides); savedOverrides = null; }
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1; Time.captureDeltaTime = capture; AudioListener.pause = false; Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("ArmsFixCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }

        void CountSwitch(WeaponController weapon) { switches++; lastSwitched = weapon; }
        void OnExecuted(EnemyExecutionSource executed, Vector3 grip) => executedAt = Time.time;
        void Move(Vector3 p) { var c = player.GetComponent<CharacterController>(); c.enabled = false; player.transform.SetPositionAndRotation(p, Quaternion.identity); c.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        void Keys(params Key[] pressed) { Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keys, new KeyboardState(pressed)); }
        void Fire(bool held) { var s = new MouseState(); if (held) s = s.WithButton(MouseButton.Left); InputSystem.QueueStateEvent(mouse, s); }
        IEnumerator Tap(Key key) { Keys(key); yield return null; Keys(); yield return null; }
        IEnumerator WaitUp(float seconds = 4) { float t = 0; while (weapons.SwitchState != PlayerWeaponsManager.WeaponSwitchState.Up && t < seconds) { t += Time.deltaTime; yield return null; } Assert.That(weapons.SwitchState, Is.EqualTo(PlayerWeaponsManager.WeaponSwitchState.Up), "switch finished"); }
        void Write(string name) { File.WriteAllText(Reports + "/" + name + ".txt", string.Join("\n", log)); }

        Transform Attachment(WeaponController w)
        {
            if (!w) return null;
            if (!attachments.TryGetValue(w, out var a)) { a = w.WeaponRoot ? w.WeaponRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Attachment") : null; attachments[w] = a; }
            return a;
        }

        // Campaign layout: slot 0 arm cannon, slot 1 the stolen weapon (the lab shotgun is removed while it is not in hand).
        void CampaignInventory()
        {
            var shotgun = weapons.GetWeaponAtSlotIndex(1);
            if (shotgun && shotgun != cannon) Assert.That(weapons.RemoveWeapon(shotgun), Is.True, "lab shotgun removed");
            Assert.That(weapons.GetActiveWeapon(), Is.SameAs(cannon));
        }

        IEnumerator SpawnT07()
        {
            // Spawn at the end of the frame: a coroutine resumes between Update and LateUpdate, so a robot created there
            // gets LateUpdate (T07CannonSocket reads EnemyController.DetectionModule) before its Start assigns it.
            yield return new WaitForEndOfFrame();
            target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(T07), new Vector3(0, 30.05f, -6), Quaternion.Euler(0, 180, 0));
            // no AI decisions or navigation from the first frame (the lab spot has no NavMesh); EnemyController keeps one frame
            // so its Start assigns the DetectionModule, then it is switched off too
            foreach (var b in new Behaviour[] { target.GetComponent<EnemyMobile>(), target.GetComponent<UnityEngine.AI.NavMeshAgent>() }) if (b) b.enabled = false;
            yield return null;
            var controller = target.GetComponent<EnemyController>(); if (controller) controller.enabled = false;
            yield return null; yield return null;
            source = target.GetComponentInChildren<EnemyExecutionSource>(); Assert.That(source, Is.Not.Null, "T-07 execution source");
            targetHealth = source.GetComponent<Health>(); feedback = target.GetComponentInChildren<RobotHitFeedback>();
            Assert.That(targetHealth, Is.Not.Null); Assert.That(feedback, Is.Not.Null, "T-07 hit feedback");
            targetHealth.CurrentHealth = targetHealth.MaxHealth * .5f;
            var colliders = target.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray(); Assert.That(colliders.Length, Is.GreaterThan(0));
            var bounds = colliders[0].bounds; foreach (var c in colliders) bounds.Encapsulate(c.bounds);
            // 1.2 m in front of the robot, looking at its body (same placement as BunkerPolish / VisualOverhaul executions)
            player.enabled = false; Move(new Vector3(bounds.center.x, bounds.min.y + .03f, bounds.min.z - 1.2f));
            player.PlayerCamera.transform.LookAt(bounds.center); Physics.SyncTransforms(); Cursor.lockState = CursorLockMode.Locked;
            yield return null; yield return null;
            Assert.That(source.Eligible, Is.True, "T-07 below 75 %");
            Assert.That(carrier.ExecutionCandidate, Is.SameAs(source), "E would execute this T-07 (status " + carrier.ExecutionStatus + ")");
            Assert.That(carrier.ExecutionStatus, Is.EqualTo(CannonPickupStatus.Ready));
        }

        // Real E, then wait until the hands are free and the stolen weapon is up and absorbed.
        IEnumerator ExecuteWithE()
        {
            Keys(Key.E); yield return null; Keys();
            Assert.That(carrier.Held, Is.Not.Null, "E delivers the weapon");
            float t = 0; while ((carrier.ExecutionBusy || weapons.SwitchState != PlayerWeaponsManager.WeaponSwitchState.Up) && t < 5) { t += Time.deltaTime; yield return null; }
            Assert.That(carrier.ExecutionBusy, Is.False, "execution finished"); Assert.That(weapons.GetActiveWeapon(), Is.SameAs(carrier.Held.EquippedWeapon), "stolen weapon in hand");
            Assert.That(targetHealth.CurrentHealth, Is.LessThanOrEqualTo(0f), "T-07 dead");
            yield return new WaitForSeconds(.6f); // absorb lock and its hit-stop are over
        }

        // ---------------------------------------------------------------- (a) C + B2 + B5
        [UnityTest] public IEnumerator T07ExecutionWithRealE_TwoBlowsGripNotPunchDeferredSwitchAndBusyHandsIgnoreKeys()
        {
            yield return SpawnT07();
            var cam = player.PlayerCamera; var camT = cam.transform;
            int impacts0 = feedback.ImpactCount, serial0 = melee.AttackSerial, switches0 = switches, cannonShots = 0, deaths = 0, deathFrame = -1;
            float dieAt = -1; var impactTimes = new List<float>(); var impactFrames = new List<int>();
            foreach (var d in target.GetComponentsInChildren<Damageable>(true)) d.Impact += (p, n, s) => { impactFrames.Add(Time.frameCount); impactTimes.Add(Time.time - executedAt); };
            targetHealth.OnDie += () => { deaths++; deathFrame = Time.frameCount; dieAt = Time.time - executedAt; };
            void Shot() => cannonShots++; cannon.OnShootProcessed += Shot;
            float yank = hands.DisarmYankTime, strike = hands.DisarmStrikeTime, windUp = hands.DisarmWindUpTime;
            log.Add($"beats: yank {yank:F3} s, wind-up {windUp:F3} s, strike {strike:F3} s");
            Assert.That(yank, Is.EqualTo(.34f).Within(.001f), "YankTime unchanged"); Assert.That(strike, Is.EqualTo(.732f).Within(.001f), "StrikeTime unchanged");

            // E (real key): weapon and Held delivered now, but the right arm keeps the cannon until the wrench
            Keys(Key.E); yield return null; Keys();
            Assert.That(executedAt, Is.GreaterThanOrEqualTo(0f), "execution started by E");
            Assert.That(carrier.Held, Is.Not.Null, "weapon delivered at E"); var stolen = carrier.Held.EquippedWeapon;
            Assert.That(targetHealth.CurrentHealth, Is.GreaterThan(0f), "alive at E");
            Assert.That(carrier.ExecutionBusy, Is.True); Assert.That(carrier.HandoverPending, Is.True, "visual switch deferred");
            Assert.That(weapons.GetActiveWeapon(), Is.SameAs(cannon), "no switch at E"); Assert.That(weapons.SwitchState, Is.EqualTo(PlayerWeaponsManager.WeaponSwitchState.Up));

            // keys pressed while the hands are busy: M1 before the wrench, F during grab / wind-up / recover, 2 before the
            // wrench, G and 1 before the punch has landed (switch/throw are free again after the impact beat, ~0.83 s;
            // melee only once the left hand has finished its recover). All must be ignored.
            var schedule = new List<(float at, string what)> { (.05f, "M1 down"), (.15f, "F"), (.25f, "2"), (.30f, "M1 up"), (.55f, "F"), (.72f, "G"), (.78f, "1"), (1.0f, "F") };
            int next = 0; bool keyDown = false;
            float maxReachZ = float.MinValue, maxClosureEarly = 0, punchClosure = 0, maxRecoverX = float.MinValue, switchStartAt = -1;
            int visibleFrames = 0, idle = 0; bool wasVisible = false; Vector3 lastVisibleViewport = default; Vector3 hiddenAtViewport = default; float hiddenAt = -1;
            for (int frame = 0; frame < 180; frame++)
            {
                if (!hands.IsDisarming && frame > 10 && ++idle > 5) break; // a few frames past the end of the presentation
                float now = Time.time - executedAt, sampled = now - Time.deltaTime; // probes read the previous LateUpdate
                if (keyDown) { Keys(); keyDown = false; }
                else if (next < schedule.Count && now >= schedule[next].at)
                {
                    var what = schedule[next++].what; log.Add($"{now:F3} s: press {what}");
                    if (what == "M1 down") Fire(true); else if (what == "M1 up") Fire(false);
                    else { Keys(what == "F" ? Key.F : what == "G" ? Key.G : what == "1" ? Key.Digit1 : Key.Digit2); keyDown = true; }
                }
                if (switchStartAt < 0 && weapons.SwitchState != PlayerWeaponsManager.WeaponSwitchState.Up) switchStartAt = now;
                if (switchStartAt < 0) Assert.That(weapons.GetActiveWeapon(), Is.SameAs(cannon), "cannon stays on the arm until the wrench");
                bool visible = hands.LeftHandVisible;
                if (visible)
                {
                    visibleFrames++;
                    var local = camT.InverseTransformPoint(hands.LeftHandBone.position);
                    if (sampled < yank) maxReachZ = Mathf.Max(maxReachZ, local.z);
                    if (sampled < windUp - .04f) maxClosureEarly = Mathf.Max(maxClosureEarly, hands.LeftFistClosure);
                    if (sampled >= strike - .03f && sampled <= strike + .06f) punchClosure = Mathf.Max(punchClosure, hands.LeftFistClosure);
                    if (sampled > strike + .1f) maxRecoverX = Mathf.Max(maxRecoverX, local.x);
                    lastVisibleViewport = cam.WorldToViewportPoint(hands.LeftHandBone.position);
                    if (frame % 3 == 0) log.Add($"{sampled:F3} s left hand cam {local.ToString("F3")} closure {hands.LeftFistClosure:F2} state {weapons.SwitchState} active {(weapons.GetActiveWeapon() ? weapons.GetActiveWeapon().name : "none")}");
                }
                else if (wasVisible && hiddenAt < 0) { hiddenAt = sampled; hiddenAtViewport = lastVisibleViewport; }
                wasVisible = visible;
                yield return null;
            }
            if (keyDown) Keys(); Fire(false); cannon.OnShootProcessed -= Shot;
            int impacts = feedback.ImpactCount - impacts0;
            log.Add($"impacts (RobotHitFeedback) {impacts}, Damageable impacts at [{string.Join(", ", impactTimes.Select(x => x.ToString("F3")))}] s, deaths {deaths} at {dieAt:F3} s (frame {deathFrame}, impact frames {string.Join(",", impactFrames)})");
            log.Add($"visual switch started at {switchStartAt:F3} s; switches {switches - switches0} (last {(lastSwitched ? lastSwitched.name : "none")}); cannon shots during handover {cannonShots}");
            log.Add($"left hand: max depth before the wrench {maxReachZ:F3} m, max fist closure before the wind-up {maxClosureEarly:F2}, at the punch {punchClosure:F2}, max x on recover {maxRecoverX:F3}, hidden at {hiddenAt:F3} s from viewport {hiddenAtViewport.ToString("F2")}; visible frames {visibleFrames}");
            log.Add($"attack serial {serial0} -> {melee.AttackSerial}; held {(carrier.Held ? carrier.Held.name : "none")}");
            Write("a-t07-execution");

            // exactly two connecting blows: the wrench and the punch; the kill lands with the punch; nothing after death
            Assert.That(impacts, Is.EqualTo(2), "RobotHitFeedback impacts");
            Assert.That(impactFrames.Count, Is.EqualTo(2), "Damageable impacts");
            Assert.That(deaths, Is.EqualTo(1)); Assert.That(dieAt, Is.EqualTo(strike).Within(.05f), "death at DisarmStrikeTime");
            Assert.That(impactTimes[0], Is.EqualTo(yank).Within(.05f), "blow 1 = wrench at YankTime");
            Assert.That(impactFrames.All(f => f <= deathFrame), Is.True, "no impact after death");
            // C: the grab is a grab, not a punch into the air
            Assert.That(visibleFrames, Is.GreaterThan(30), "left hand shown for the disarm");
            Assert.That(maxReachZ, Is.LessThanOrEqualTo(.6f), "no forward thrust before the wrench");
            Assert.That(maxClosureEarly, Is.LessThan(.01f), "no fist while grabbing / wrenching");
            Assert.That(punchClosure, Is.GreaterThan(.5f), "the punch is a fist");
            Assert.That(maxRecoverX, Is.LessThan(.05f), "recover does not sweep across the view");
            Assert.That(hiddenAt, Is.GreaterThan(0f), "left hand hidden after the recover");
            Assert.That(hiddenAtViewport.z <= 0 || hiddenAtViewport.x < 0 || hiddenAtViewport.x > 1 || hiddenAtViewport.y < 0 || hiddenAtViewport.y > 1, Is.True, "hidden only once out of view (no pop)");
            // B2: the stolen weapon reaches the arm right after blow 1, once
            Assert.That(switchStartAt, Is.GreaterThanOrEqualTo(impactTimes[0] - .0001f), "visual switch not before the wrench");
            // executions-v02 (approved F1): the wrenched weapon travels in the left hand and reaches the right arm at the wind-up (0.52 s)
            Assert.That(switchStartAt, Is.LessThanOrEqualTo(windUp + 2 * Frame + .02f), "visual switch at the wind-up, after the hand-off");
            Assert.That(switches - switches0, Is.EqualTo(1), "one switch (to the stolen weapon)"); Assert.That(lastSwitched, Is.SameAs(stolen));
            // B5: M1 / F / 1 / 2 / G while busy were ignored
            Assert.That(cannonShots, Is.Zero, "outgoing cannon does not fire during the hand-over");
            Assert.That(melee.AttackSerial, Is.EqualTo(serial0), "F ignored during the execution");
            Assert.That(carrier.Held, Is.Not.Null, "G ignored during the execution"); Assert.That(weapons.GetActiveWeapon(), Is.SameAs(stolen), "1 / 2 ignored during the execution");
            // and the hands are free again afterwards
            yield return Tap(Key.F); yield return null;
            Assert.That(melee.AttackSerial, Is.EqualTo(serial0 + 1), "F works once the execution is over");
        }

        // ---------------------------------------------------------------- (b) B1
        int blinkFrames, scaleFrames, gapFrames, slowFrames, shrinkFrames; float maxGap;
        IEnumerator SwitchWithKey(Key key, WeaponController to, Transform shell, Vector3 shellScale, Vector3 cannonScale, string label)
        {
            int switches0 = switches, unlock0 = module.UnlockCount, dock0 = module.DockCount; bool toCannon = to == cannon;
            Keys(key); yield return null; Keys();
            float t = 0; int frames = 0;
            while (t < 4 && frames < 300)
            {
                Probe(shell, shellScale, cannonScale, !toCannon);
                if (frames > 1 && weapons.SwitchState == PlayerWeaponsManager.WeaponSwitchState.Up && weapons.GetActiveWeapon() == to) break;
                t += Time.deltaTime; frames++; yield return null;
            }
            for (int i = 0; i < 20; i++) { Probe(shell, shellScale, cannonScale, !toCannon); yield return null; }
            log.Add($"{label}: {t:F3} s, switches +{switches - switches0}, cannon unlock +{module.UnlockCount - unlock0} dock +{module.DockCount - dock0}");
            Assert.That(weapons.GetActiveWeapon(), Is.SameAs(to), label + " active"); Assert.That(weapons.SwitchState, Is.EqualTo(PlayerWeaponsManager.WeaponSwitchState.Up));
            Assert.That(switches - switches0, Is.EqualTo(1), label + " OnSwitchedToWeapon once"); Assert.That(lastSwitched, Is.SameAs(to));
            Assert.That(module.UnlockCount - unlock0, Is.EqualTo(toCannon ? 0 : 1), label + " cannon unlock");
            Assert.That(module.DockCount - dock0, Is.EqualTo(toCannon ? 1 : 0), label + " cannon dock");
        }
        void Probe(Transform shell, Vector3 shellScale, Vector3 cannonScale, bool toStolen)
        {
            if (!hands.FpsRoot.gameObject.activeSelf) blinkFrames++;
            if (Vector3.Distance(cannon.transform.localScale, cannonScale) > 1e-4f) scaleFrames++;
            var active = weapons.GetActiveWeapon(); var attach = Attachment(active);
            if (active && attach && active.WeaponRoot.activeInHierarchy && hands.RightForearmReplaced)
            {
                float gap = Vector3.Distance(attach.position, hands.RightModuleSocket.position); maxGap = Mathf.Max(maxGap, gap); if (gap > .006f) gapFrames++;
            }
            if (toStolen && Time.timeScale < .99f) slowFrames++;
            if (shell && shell.gameObject.activeInHierarchy && Vector3.Distance(shell.localScale, shellScale) > 1e-3f) shrinkFrames++;
        }

        [UnityTest] public IEnumerator Digit1And2SwitchCannonAndStolenWeaponWithoutRigBlinkDoubleSwitchOrDeployScale()
        {
            CampaignInventory(); yield return SpawnT07(); yield return ExecuteWithE();
            var stolen = carrier.Held.EquippedWeapon; Assert.That(weapons.GetWeaponAtSlotIndex(1), Is.SameAs(stolen), "stolen weapon in slot 1 (key 2)");
            var shell = stolen.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "VP_StolenShell"); var shellScale = shell ? shell.localScale : Vector3.one;
            // WeaponDeployMotion scales the weapon's own transform; at rest it is the prefab scale (1)
            var cannonScale = cannon.transform.localScale; blinkFrames = scaleFrames = gapFrames = slowFrames = shrinkFrames = 0; maxGap = 0;
            log.Add($"cannon rest scale {cannonScale.ToString("F3")}, WeaponRoot scale {cannon.WeaponRoot.transform.localScale.ToString("F3")}");
            for (int round = 0; round < 2; round++)
            {
                yield return SwitchWithKey(Key.Digit1, cannon, shell, shellScale, cannonScale, "round " + round + " key 1 -> cannon");
                yield return SwitchWithKey(Key.Digit2, stolen, shell, shellScale, cannonScale, "round " + round + " key 2 -> stolen");
            }
            log.Add($"FPS rig off frames {blinkFrames}, cannon scale off frames {scaleFrames}, Attachment-socket gap > 6 mm frames {gapFrames} (max {maxGap * 1000:F1} mm), slowed frames switching to the stolen weapon {slowFrames}, shell shrunk frames {shrinkFrames}");
            Write("b-switch-1-2");
            Assert.That(blinkFrames, Is.Zero, "FPS arm rig never switched off");
            Assert.That(scaleFrames, Is.Zero, "cannon root scale stays 1 (no generic deploy on the module)");
            Assert.That(gapFrames, Is.Zero, "forearm stays on the Attachment");
            Assert.That(slowFrames, Is.Zero, "re-show without hit-stop"); Assert.That(shrinkFrames, Is.Zero, "re-show seated, no absorb");
        }

        // ---------------------------------------------------------------- (b2) B3: forced switch (E pickup) during a switch
        [UnityTest] public IEnumerator ForcedSwitchWhileCannonDocksReversesWithoutReplayingUnlock()
        {
            CampaignInventory(); yield return SpawnT07(); yield return ExecuteWithE();
            var stolen = carrier.Held.EquippedWeapon; int stolenSlot = 1; Assert.That(weapons.GetWeaponAtSlotIndex(stolenSlot), Is.SameAs(stolen));
            int switches0 = switches, unlock0 = module.UnlockCount, dock0 = module.DockCount; blinkFrames = 0;
            Keys(Key.Digit1); yield return null; Keys();
            float t = 0, lastStep = 0; var socket = weapons.WeaponParentSocket.localPosition;
            while (!(weapons.GetActiveWeapon() == cannon && weapons.SwitchState == PlayerWeaponsManager.WeaponSwitchState.PutUpNew && weapons.SwitchPhaseProgress >= .25f) && t < 4)
            {
                t += Time.deltaTime; if (!hands.FpsRoot.gameObject.activeSelf) blinkFrames++; yield return null;
                lastStep = Vector3.Distance(weapons.WeaponParentSocket.localPosition, socket); socket = weapons.WeaponParentSocket.localPosition;
            }
            Assert.That(weapons.SwitchState, Is.EqualTo(PlayerWeaponsManager.WeaponSwitchState.PutUpNew), "cannon docking"); Assert.That(weapons.SwitchPhaseProgress, Is.LessThan(.7f), "not docked yet");
            float progress = weapons.SwitchPhaseProgress;
            // what an E pickup does: a forced switch to the stolen slot in the middle of the switch
            weapons.SwitchToWeaponIndex(stolenSlot, true);
            Assert.That(weapons.SwitchState, Is.EqualTo(PlayerWeaponsManager.WeaponSwitchState.PutDownPrevious), "reversed, not restarted");
            // (progress was read one frame earlier: one 60 Hz frame of a 0.3 s phase is 0.056)
            Assert.That(weapons.SwitchPhaseProgress, Is.EqualTo(1 - progress).Within(.1f), "continues from the same progress, not from 0");
            yield return null;
            float step = Vector3.Distance(weapons.WeaponParentSocket.localPosition, socket);
            log.Add($"socket step before the forced switch {lastStep * 1000:F1} mm, after {step * 1000:F1} mm");
            Assert.That(step, Is.LessThanOrEqualTo(lastStep + .005f), "socket keeps its motion (no snap)");
            t = 0; while (!(weapons.GetActiveWeapon() == stolen && weapons.SwitchState == PlayerWeaponsManager.WeaponSwitchState.Up) && t < 4) { t += Time.deltaTime; if (!hands.FpsRoot.gameObject.activeSelf) blinkFrames++; yield return null; }
            log.Add($"forced switch at dock progress {progress:F2}: switches +{switches - switches0}, unlock +{module.UnlockCount - unlock0}, dock +{module.DockCount - dock0}, rig off frames {blinkFrames}");
            Write("b3-forced-switch");
            Assert.That(weapons.GetActiveWeapon(), Is.SameAs(stolen));
            Assert.That(switches - switches0, Is.EqualTo(2), "to the cannon, then to the stolen weapon: no extra restart");
            Assert.That(module.UnlockCount - unlock0, Is.Zero, "a module that never docked does not replay its unlock");
            Assert.That(module.DockCount - dock0, Is.Zero, "and never docked");
            Assert.That(blinkFrames, Is.Zero);
        }

        // ---------------------------------------------------------------- (c) B4
        // Expected for ONE clean switch: the thrown weapon was the active one, so RemoveWeapon hands the arm straight to the
        // primary: it is shown at once (one OnSwitchedToWeapon) and docks (dock +1). It was not in hand, so nothing ejects
        // it (unlock +0). Before: auto-switch + forced switch = 2 events, unlock +1, dock +2, ChangeWeaponSfx twice.
        [UnityTest] public IEnumerator GThrowHandsOverToTheCannonWithASingleSwitch()
        {
            CampaignInventory(); yield return SpawnT07(); yield return ExecuteWithE();
            var held = carrier.Held; var cannonScale = cannon.transform.localScale;
            int switches0 = switches, unlock0 = module.UnlockCount, dock0 = module.DockCount; blinkFrames = scaleFrames = 0;
            Keys(Key.G); yield return null; Keys();
            Assert.That(carrier.Held, Is.Null, "G throws");
            for (int i = 0; i < 75; i++) { if (!hands.FpsRoot.gameObject.activeSelf) blinkFrames++; if (Vector3.Distance(cannon.transform.localScale, cannonScale) > 1e-4f) scaleFrames++; yield return null; }
            yield return WaitUp();
            log.Add($"G: switches +{switches - switches0} (last {(lastSwitched ? lastSwitched.name : "none")}), cannon unlock +{module.UnlockCount - unlock0} dock +{module.DockCount - dock0}, rig off frames {blinkFrames}, scale frames {scaleFrames}, module state {held.State}");
            Write("c-g-throw");
            Assert.That(held.State, Is.Not.EqualTo(StolenCannonState.Equipped), "module released to the world");
            Assert.That(weapons.GetActiveWeapon(), Is.SameAs(cannon));
            Assert.That(switches - switches0, Is.EqualTo(1), "exactly one OnSwitchedToWeapon"); Assert.That(lastSwitched, Is.SameAs(cannon));
            Assert.That(module.UnlockCount - unlock0, Is.Zero, "primary not ejected");
            Assert.That(module.DockCount - dock0, Is.EqualTo(1), "primary docks once");
            Assert.That(blinkFrames, Is.Zero); Assert.That(scaleFrames, Is.Zero);
        }
    }
}
