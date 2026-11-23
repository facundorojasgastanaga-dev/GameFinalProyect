using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using K = UnityEngine.InputSystem.Key;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// Weapon switch / stolen weapon / T-07 execution reproduction in the real campaign scene with virtual keys.
    /// Per-frame telemetry (weapons-<run>.csv) + first-person captures (weapons/<run>_<case>_<frame>.png).
    public static partial class ArmsFixPassTools
    {
        const string T07Prefab = "Assets/IronRequiem/Prefabs/Campaign/T07_Mech.prefab";
        static readonly Stack<IEnumerator> runStack = new Stack<IEnumerator>();
        static IEnumerator run; static double runWait; static Func<bool> runUntil; static string runName;
        static StringBuilder wcsv; static string wcase = ""; static float wcaseStart; static int lastFrame = -1, shotEvery = 3, switchedCount, caseFrame;
        static bool capture; static InputSettings prevInput, tmpInput;
        static GameObject target; static RobotHitFeedback targetFeedback; static Health targetHealth; static int targetDeaths;

        [MenuItem(Menu + "Weapons: B1-B4 + C (play, Map01)")] static void RunMain() => StartRun("main", MainScript());
        [MenuItem(Menu + "Weapons: B5 switch during station and execution (play, Map01)")] static void RunB5() => StartRun("b5", B5Script());

        static void StartRun(string name, IEnumerator script)
        {
            RequirePlay(); runName = name; run = script; runWait = 0; runUntil = null; runStack.Clear(); runStack.Push(script);
            wcsv = new StringBuilder("case,frame,t,timeScale,switchState,progress,activeIdx,active,activeRootOn,fpsRootOn,rep,forearmReplaced,attachGap,socketY,weaponScaleZ,shellScale,leftVisible,leftX,leftY,leftZ,disarming,beat,attackSerial,impacts,targetHp,deaths,switched,unlock,dock\n");
            prevInput = InputSystem.settings; tmpInput = Object.Instantiate(prevInput); tmpInput.hideFlags = HideFlags.DontSave;
            tmpInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            tmpInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = tmpInput;
            var w = Player.GetComponent<PlayerWeaponsManager>(); switchedCount = 0; w.OnSwitchedToWeapon += _ => switchedCount++;
            var h = Player.GetComponent<Health>(); if (h) h.Invincible = true;
            EditorApplication.update -= RunTick; EditorApplication.update += RunTick;
            Log($"weapons run '{name}' in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }
        static void EndRun(string why)
        {
            EditorApplication.update -= RunTick; Time.timeScale = 1; Keys();
            if (prevInput) InputSystem.settings = prevInput; if (tmpInput) Object.DestroyImmediate(tmpInput);
            Directory.CreateDirectory(Dir); File.WriteAllText($"{Dir}/weapons-{runName}.csv", wcsv.ToString());
            Log($"weapons run '{runName}' {why}");
        }
        static void RunTick()
        {
            if (!EditorApplication.isPlaying) { EndRun("aborted (play stopped)"); return; }
            try
            {
                Sample();
                if (EditorApplication.timeSinceStartup < runWait) return;
                if (runUntil != null) { if (!runUntil()) return; runUntil = null; }
                // nested coroutines: a yielded IEnumerator runs to completion before the parent continues
                while (runStack.Count > 0 && !runStack.Peek().MoveNext()) runStack.Pop();
                if (runStack.Count == 0) { EndRun("done"); return; }
                var cur = runStack.Peek().Current;
                if (cur is IEnumerator nested) runStack.Push(nested);
                else if (cur is float s) runWait = EditorApplication.timeSinceStartup + s;
                else if (cur is Func<bool> f) runUntil = f;
            }
            catch (Exception e) { Log("weapons run error: " + e); EndRun("failed"); }
        }
        static void Case(string name, int every = 3) { wcase = name; wcaseStart = Time.time; caseFrame = 0; capture = every > 0; shotEvery = Mathf.Max(1, every); Log("case " + name); }
        static void Idle() { wcase = ""; capture = false; }

        static void Sample()
        {
            if (string.IsNullOrEmpty(wcase) || Time.frameCount == lastFrame) return; lastFrame = Time.frameCount; caseFrame++;
            var p = Player; var cam = p.PlayerCamera.transform; var w = p.GetComponent<PlayerWeaponsManager>(); var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
            var melee = p.GetComponent<ProtagonistMelee>(); var a = w.GetActiveWeapon();
            Transform attach = a && a.WeaponRoot ? a.WeaponRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Attachment") : null;
            var sock = hands ? hands.RightModuleSocket : null;
            float gap = attach && sock && hands.RightForearmReplaced ? Vector3.Distance(attach.position, sock.position) : -1;
            var shell = a ? a.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "VP_StolenShell") : null;
            var cannon = Enumerable.Range(0, 9).Select(i => w.GetWeaponAtSlotIndex(i)).FirstOrDefault(x => x && x.GetComponent<ArmModuleVisual>());
            var amv = cannon ? cannon.GetComponent<ArmModuleVisual>() : null;
            var left = hands && hands.LeftHandBone ? cam.InverseTransformPoint(hands.LeftHandBone.position) : Vector3.zero;
            object disarm = hands ? Field<object>(hands, "disarm") : null; var beat = disarm != null ? Field<object>(disarm, "last") : null;
            wcsv.AppendLine(string.Join(",", new object[] { wcase, caseFrame, F(Time.time - wcaseStart), F(Time.timeScale), w.SwitchState, F(w.SwitchPhaseProgress), w.ActiveWeaponIndex, a ? a.name.Replace("(Clone)", "") : "none",
                a && a.WeaponRoot && a.WeaponRoot.activeInHierarchy ? 1 : 0, hands && hands.FpsRoot && hands.FpsRoot.gameObject.activeInHierarchy ? 1 : 0, hands ? hands.CurrentRepresentation.ToString() : "-", hands && hands.RightForearmReplaced ? 1 : 0,
                F(gap), F(w.WeaponParentSocket.localPosition.y), F(a && a.WeaponRoot ? a.WeaponRoot.transform.localScale.z : 0), F(shell ? shell.localScale.x : 0), hands && hands.LeftHandVisible ? 1 : 0, F(left.x), F(left.y), F(left.z),
                hands && hands.IsDisarming ? 1 : 0, beat ?? "-", melee ? melee.AttackSerial : -1, targetFeedback ? targetFeedback.ImpactCount : -1, targetHealth ? F(targetHealth.CurrentHealth) : "-", targetDeaths, switchedCount,
                amv ? amv.UnlockCount : -1, amv ? amv.DockCount : -1 }));
            if (capture && caseFrame % shotEvery == 1) Save(FpsShot(960, 540), "weapons", $"{runName}_{wcase}_{caseFrame:000}");
        }

        // ------------------------------------------------------------------ scripted steps
        static IEnumerator Tap(K key) { Keys(key); yield return .06f; Keys(); yield return .04f; }
        static IEnumerator Hold(K key, float seconds) { Keys(key); yield return seconds; Keys(); yield return .04f; }
        static IEnumerator SkipIntro()
        {
            var intro = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro)
            {
                double until = EditorApplication.timeSinceStartup + 10; yield return (Func<bool>)(() => intro.IsPlaying || intro.Finished || EditorApplication.timeSinceStartup > until);
                if (intro.IsPlaying) intro.RequestSkip();
                until = EditorApplication.timeSinceStartup + 8; yield return (Func<bool>)(() => !PlayerInputLock.Locked || EditorApplication.timeSinceStartup > until);
            }
            yield return .5f; Log($"intro skipped: intro={(bool)intro} locked={PlayerInputLock.Locked}");
        }
        static IEnumerator Station()
        {
            var st = Object.FindAnyObjectByType<WeaponMountStation>(); var p = Player;
            if (st && !st.Granted)
            {
                var move = p.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint();
                p.TeleportToCheckpoint(st.transform.position - st.transform.forward * .5f + Vector3.up * .03f); Physics.SyncTransforms(); yield return .3f;
                st.Begin(); yield return .3f; st.Skip(); yield return (Func<bool>)(() => !st.Busy); yield return .5f;
                // turn round to face the room for the robot tests
                p.SetViewAngles(st.transform.eulerAngles.y + 180, 0); yield return .3f;
            }
            var w = p.GetComponent<PlayerWeaponsManager>(); Log($"station: granted={(st ? st.Granted : false)} active={(w.GetActiveWeapon() ? w.GetActiveWeapon().name : "none")} state={w.SwitchState}");
        }
        static IEnumerator SpawnT07(float distance)
        {
            var p = Player; var f = Vector3.ProjectOnPlane(p.PlayerCamera.transform.forward, Vector3.up).normalized; if (f.sqrMagnitude < .1f) f = p.transform.forward;
            var pos = p.transform.position + f * distance;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
            target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(T07Prefab), pos, Quaternion.LookRotation(-f));
            targetFeedback = target.GetComponentInChildren<RobotHitFeedback>(); targetHealth = target.GetComponentInChildren<Health>(); targetDeaths = 0;
            if (targetHealth) targetHealth.OnDie += () => targetDeaths++;
            yield return .4f;
            foreach (var m in target.GetComponents<Unity.FPS.AI.EnemyMobile>()) m.enabled = false; // stands still, does not shoot
            if (targetHealth) targetHealth.CurrentHealth = targetHealth.MaxHealth * .5f;
            Aim(target.transform.position + Vector3.up * 1.1f); yield return .3f;
            var c = Player.GetComponent<StolenWeaponCarrier>(); var src = target.GetComponentInChildren<EnemyExecutionSource>();
            Log($"spawned {target.name} at {target.transform.position} hp={(targetHealth ? targetHealth.CurrentHealth + "/" + targetHealth.MaxHealth : "none")} feedback={(bool)targetFeedback} eligible={(src ? src.Eligible : false)} status={(c && src ? c.GetExecutionStatus(src).ToString() : "-")} candidate={(c && c.ExecutionCandidate ? c.ExecutionCandidate.name : "none")}");
        }
        static void Aim(Vector3 world)
        {
            var p = Player; var d = world - p.PlayerCamera.transform.position; var flat = Vector3.ProjectOnPlane(d, Vector3.up);
            p.SetViewAngles(Quaternion.LookRotation(flat).eulerAngles.y, -Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg);
        }

        static IEnumerator MainScript()
        {
            yield return SkipIntro();
            yield return Station();                                   // primary cannon mounted (slot 0)
            yield return SpawnT07(2.0f);
            Time.timeScale = .25f;
            Case("B2_execute", 2); yield return Tap(K.E); yield return 6.5f;   // ~1.6 s game time
            Case("B2_after", 0); yield return 2f;
            Time.timeScale = .5f;
            Case("B1_to1", 2); yield return Tap(K.Digit1); yield return 3f;
            Case("B1_to2", 2); yield return Tap(K.Digit2); yield return 3f;
            Case("B4_throw", 2); yield return Tap(K.G); yield return 3.5f;
            var module = Object.FindObjectsByType<StolenCannon>(FindObjectsSortMode.None).FirstOrDefault(m => m && !m.EquippedWeapon);
            if (module)
            {
                var pl = Player; var flat = Vector3.ProjectOnPlane(module.transform.position - pl.transform.position, Vector3.up);
                if (flat.magnitude > 1.6f) { var move = pl.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint(); pl.TeleportToCheckpoint(module.transform.position - flat.normalized * 1.3f + Vector3.up * .05f); Physics.SyncTransforms(); yield return .3f; }
                Aim(module.transform.position); yield return .4f;
                var car = pl.GetComponent<StolenWeaponCarrier>(); Log($"B3: module at {F(Vector3.Distance(module.transform.position, pl.PlayerCamera.transform.position))} m, pickup status={(car ? car.GetPickupStatus(module).ToString() : "-")} candidate={(car && car.Candidate ? car.Candidate.name : "none")}");
                Case("B3_pickup", 2); yield return Tap(K.E); yield return 3f;
            }
            else Log("B3: no module on the ground to pick up");
            Time.timeScale = 1; Idle();
        }

        static IEnumerator B5Script()
        {
            // execution with weapon presses and F in the middle (C2), then the station while holding the stolen weapon
            yield return SkipIntro();
            { var st0 = Object.FindAnyObjectByType<WeaponMountStation>(); var p0 = Player; if (st0) { p0.TeleportToCheckpoint(st0.transform.position - st0.transform.forward * .5f + Vector3.up * .03f); Physics.SyncTransforms(); p0.SetViewAngles(st0.transform.eulerAngles.y + 180, 0); yield return .4f; } }
            yield return SpawnT07(2.0f); Time.timeScale = .25f;
            Case("B5_exec_keys", 2); Keys(K.E); yield return .06f; Keys(); yield return 1.4f; // ~0.37 s
            yield return Tap(K.F); yield return 1.0f; yield return Tap(K.Digit1); yield return 4.5f;
            Case("B5_after", 0); yield return 1.5f; Time.timeScale = .5f;
            var st = Object.FindAnyObjectByType<WeaponMountStation>();
            if (st && !st.Granted)
            {
                var p = Player; p.TeleportToCheckpoint(st.transform.position - st.transform.forward * .5f + Vector3.up * .03f);
                p.transform.rotation = Quaternion.Euler(0, st.transform.eulerAngles.y, 0); p.PlayerCamera.transform.localRotation = Quaternion.identity; Physics.SyncTransforms(); yield return .5f;
                Case("B5_station", 4); st.Begin(); yield return 4f; yield return Tap(K.Digit2); yield return 5f; yield return Tap(K.Digit1); yield return (Func<bool>)(() => !st.Busy); yield return 1.5f;
            }
            Time.timeScale = 1; Idle();
        }
    }
}
