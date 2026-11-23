using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    // Executions v02, F0 diagnosis (22/11/2026). Read-only evidence: records the CURRENT execution of each campaign
    // robot (T-07, SawBot, Big Bot) started with a real E through virtual Input System devices. Fixed 60 Hz simulated
    // time. Every 2nd frame: the player view (base camera + FPS overlay stack) and a side test camera, plus per-frame
    // telemetry (beats, hand vs grip, robot bone motion, floor gap, camera rotation, time scale, input locks, audio and
    // particles). Nothing is saved; the lab scene is unloaded at the end. Same lab and placement as ArmsFixPassTests.
    [TestFixture("before_v2")]
    [TestFixture("after")]
    [TestFixture("before_v3")]
    [TestFixture("after_v2")]
    [TestFixture("after_v3")]
    [TestFixture("after_v4")]
    [TestFixture("after_v5")]
    public sealed class ExecutionsF0Capture
    {
        public ExecutionsF0Capture(string folder) { Root = "Tools/IronRequiem/Reports/Executions/" + folder; }
        readonly string Root;
        const string Lab = "Assets/IronRequiem/Scenes/VisualOverhaul/MainScene_VisualLab.unity";
        const float Frame = 1f / 60, FloorTop = 30f;

        PlayerCharacterController player; StolenWeaponCarrier carrier; ProtagonistHandsPresentation hands; AdvancedPlayerMovement movement;
        BigBotExecutionSequence bigSeq; Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        bool background; float capture; Vector3 grip; float executedAt = -1; int executedFrame = -1;

        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = Frame; Time.timeScale = 1; AudioListener.pause = false;
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (menu) { menu.BeginGame(); menu.enabled = false; }
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (waves) waves.enabled = false;
            var flow = Object.FindAnyObjectByType<GameFlowManager>(); if (flow) flow.enabled = false;
            var objectives = Object.FindAnyObjectByType<ObjectiveManager>(); if (objectives) objectives.enabled = false;
            Time.timeScale = 1;
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.gameObject.SetActive(false);
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            hands = player.GetComponent<ProtagonistHandsPresentation>(); movement = player.GetComponent<AdvancedPlayerMovement>(); bigSeq = player.GetComponent<BigBotExecutionSequence>();
            player.GetComponent<Health>().Invincible = true;
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "ExecF0Floor"; floor.transform.position = new Vector3(0, 29.5f, 0); floor.transform.localScale = new Vector3(80, 1, 80);
            Move(new Vector3(0, 30.05f, -12)); Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(.6f);
            var weapons = player.GetComponent<PlayerWeaponsManager>(); weapons.SwitchToWeaponIndex(0);
            yield return new WaitForSeconds(1f);
            carrier.Executed += OnExecuted;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (carrier) carrier.Executed -= OnExecuted;
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1; Time.captureDeltaTime = capture; AudioListener.pause = false; Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("ExecF0Cleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }

        void OnExecuted(EnemyExecutionSource s, Vector3 g) { grip = g; executedFrame = Time.frameCount; }
        void Move(Vector3 p) { var c = player.GetComponent<CharacterController>(); c.enabled = false; player.transform.SetPositionAndRotation(p, Quaternion.identity); c.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        void Keys(params Key[] pressed) { Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keys, new KeyboardState(pressed)); }

        [UnityTest] public IEnumerator T07() { yield return Record("T07", "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab", 1.2f, 2.4f); }
        [UnityTest] public IEnumerator SawBot() { yield return Record("SawBot", "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab", 1.2f, 2.4f); }
        [UnityTest] public IEnumerator BigBot() { yield return Record("BigBot", "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab", 1.3f, 3.6f); }

        IEnumerator Record(string label, string prefabPath, float standOff, float seconds)
        {
            string dir = Root + "/" + label; Directory.CreateDirectory(dir + "/player"); Directory.CreateDirectory(dir + "/side");
            executedAt = -1; executedFrame = -1;
            // harness only: an EnemyController whose EnemyMobile is off before Start logs an NRE on detection
            LogAssert.ignoreFailingMessages = true;
            yield return new WaitForEndOfFrame();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); Assert.That(prefab, Is.Not.Null, prefabPath);
            var target = Object.Instantiate(prefab, new Vector3(0, 30.05f, -6), Quaternion.Euler(0, 180, 0));
            foreach (var b in new Behaviour[] { target.GetComponent<EnemyMobile>(), target.GetComponent<UnityEngine.AI.NavMeshAgent>(), target.GetComponent<BigBotCombat>() }) if (b) b.enabled = false;
            yield return null;
            var controller = target.GetComponent<EnemyController>(); if (controller) controller.enabled = false;
            yield return null; yield return new WaitForSeconds(.5f);
            var source = target.GetComponentInChildren<EnemyExecutionSource>(); Assert.That(source, Is.Not.Null, label + " execution source");
            var health = source.GetComponent<Health>();
            // real damage event (as in the game): the 75 / 50 % damage stages are applied BEFORE the execution, not by its first blow
            health.TakeDamage(health.MaxHealth * .5f, player.gameObject); yield return new WaitForSeconds(1.2f);
            // F0 finding: the campaign Big Bot ships with executionEnabled = false (v31). Enabled here, in this test
            // instance only, to record the existing BigBotExecutionSequence; the prefab is not changed.
            if (!source.ExecutionEnabled) { source.SetExecutionEnabled(true); File.AppendAllText(Root + "/notes.txt", label + ": prefab has executionEnabled=false; enabled at runtime in the capture only.\n"); }
            var cols = target.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray(); Assert.That(cols.Length, Is.GreaterThan(0));
            var bounds = cols[0].bounds; foreach (var c in cols) bounds.Encapsulate(c.bounds);
            bool big = label == "BigBot"; player.enabled = big;
            Move(new Vector3(bounds.center.x, bounds.min.y + .03f, bounds.min.z - standOff));
            player.PlayerCamera.transform.LookAt(big ? bounds.center + Vector3.up * .3f : bounds.center); Physics.SyncTransforms(); Cursor.lockState = CursorLockMode.Locked;
            yield return null; yield return null;
            var bb = target.GetComponent<BigBotCombat>();
            Assert.That(source.Eligible, Is.True, $"{label} eligible: enabled {source.ExecutionEnabled} hp {health.CurrentHealth}/{health.MaxHealth} reserved {source.IsReserved} delivered {source.Delivered} module {(source.ModulePrefab ? source.ModulePrefab.name : "null")} cannons {(bb ? bb.ActiveCannonCount : -1)}");
            Assert.That(carrier.ExecutionCandidate, Is.SameAs(source), label + ": E would execute (status " + carrier.ExecutionStatus + ")");

            // side test camera: perpendicular to the player -> robot line, framing both
            var pc = player.PlayerCamera.transform.position; var mid = (pc + bounds.center) * .5f;
            var side = new GameObject("ExecF0Side").AddComponent<Camera>(); side.enabled = false; side.fieldOfView = 50; side.nearClipPlane = .05f;
            float dist = big ? 7.5f : 5f; side.transform.position = mid + Vector3.right * dist + Vector3.up * (big ? 1.6f : .6f); side.transform.LookAt(mid + Vector3.up * (big ? .4f : 0));

            // robot rest data
            var skins = target.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled && s.gameObject.activeInHierarchy).ToArray();
            var bones = skins.SelectMany(s => s.bones).Where(b => b).Distinct().ToArray();
            var prevRot = bones.Select(b => Quaternion.Inverse(target.transform.rotation) * b.rotation).ToArray();
            var renderers = target.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            var rootStart = target.transform.position;
            var seenClips = new HashSet<(AudioSource, string)>(); var events = new List<string>();
            int impacts = 0, deaths = 0; float deathAt = -1;
            foreach (var d in target.GetComponentsInChildren<Damageable>(true)) d.Impact += (p, n, s) => { impacts++; events.Add($"{(Time.time - executedAt):F3} impact #{impacts} at {p.ToString("F2")}"); };
            health.OnDie += () => { deaths++; deathAt = Time.time - executedAt; events.Add($"{deathAt:F3} death #{deaths}"); };
            var csv = new StringBuilder("frame,real_s,game_s,timeScale,beat,leftVisible,handCamX,handCamY,handCamZ,handToGrip_m,busy,handover,scripted,bigRunning,camDeg,camPosDelta,robotBoneDegAvg,robotBoneDegMax,rootDx,rootDy,rootDz,floorGap_m,particles,newAudio,health\n");
            var camT = player.PlayerCamera.transform; var prevCamRot = camT.rotation; var prevCamPos = camT.position;
            int frames = Mathf.CeilToInt(seconds / Frame); int busyEnd = -1, scriptedEnd = -1, handsEnd = -1;

            executedAt = Time.time; grip = bounds.center; // time base = the frame E is pressed (Big Bot raises Executed only after its leap)
            Keys(Key.E); yield return null; Keys();
            Assert.That(carrier.Held, Is.Not.Null, label + ": execution started by E (weapon delivered)");
            for (int f = 0; f < frames; f++)
            {
                // sample after LateUpdate: hit reactions, motion accents, camera kicks and hand IK are applied in LateUpdate
                // and removed again in the next Update, so a plain `yield return null` probe would never see them
                yield return new WaitForEndOfFrame();
                float game = Time.time - executedAt, real = f * Frame;
                // bone motion in robot space since the previous frame
                float sum = 0, max = 0; int n = 0;
                for (int i = 0; i < bones.Length; i++) { if (!bones[i]) continue; var r = Quaternion.Inverse(target ? target.transform.rotation : Quaternion.identity) * bones[i].rotation; float a = Quaternion.Angle(prevRot[i], r); prevRot[i] = r; sum += a; max = Mathf.Max(max, a); n++; }
                float minY = float.PositiveInfinity; foreach (var r in renderers) if (r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff) minY = Mathf.Min(minY, r.bounds.min.y);
                int parts = 0; foreach (var ps in Object.FindObjectsByType<ParticleSystem>()) parts += ps.particleCount;
                var newAudio = new List<string>(); foreach (var a in Object.FindObjectsByType<AudioSource>()) if (a.isPlaying && a.clip) { if (seenClips.Add((a, a.clip.name))) { newAudio.Add(a.clip.name); events.Add($"{game:F3} audio {a.clip.name} vol {a.volume:F2} group {(a.outputAudioMixerGroup ? a.outputAudioMixerGroup.name : "none")}"); } }
                var hand = hands.LeftHandBone ? camT.InverseTransformPoint(hands.LeftHandBone.position) : Vector3.zero;
                float handGrip = hands.LeftHandBone ? Vector3.Distance(hands.LeftHandBone.position, grip) : -1;
                float camDeg = Quaternion.Angle(prevCamRot, camT.rotation); float camPos = Vector3.Distance(prevCamPos, camT.position); prevCamRot = camT.rotation; prevCamPos = camT.position;
                var root = target ? target.transform.position - rootStart : Vector3.zero;
                if (busyEnd < 0 && f > 0 && !carrier.ExecutionBusy) busyEnd = f;
                if (scriptedEnd < 0 && f > 0 && movement && !movement.IsScripted && !(bigSeq && bigSeq.IsRunning)) scriptedEnd = f;
                if (handsEnd < 0 && f > 0 && !hands.IsDisarming) handsEnd = f;
                csv.AppendLine(string.Join(",", f, real.ToString("F3"), game.ToString("F3"), Time.timeScale.ToString("F2"), hands.DisarmBeat, hands.LeftHandVisible ? 1 : 0, hand.x.ToString("F3"), hand.y.ToString("F3"), hand.z.ToString("F3"), handGrip.ToString("F3"),
                    carrier.ExecutionBusy ? 1 : 0, carrier.HandoverPending ? 1 : 0, movement && movement.IsScripted ? 1 : 0, bigSeq && bigSeq.IsRunning ? 1 : 0, camDeg.ToString("F2"), camPos.ToString("F3"),
                    (n > 0 ? sum / n : 0).ToString("F2"), max.ToString("F2"), root.x.ToString("F3"), root.y.ToString("F3"), root.z.ToString("F3"), float.IsInfinity(minY) ? "" : (minY - FloorTop).ToString("F3"), parts, string.Join("|", newAudio), health ? health.CurrentHealth.ToString("F1") : "-"));
                if (f % 2 == 0)
                {
                    string stamp = $"{f:000}_{real:F2}s";
                    FrameGrab.Save(player.PlayerCamera, $"{dir}/player/{stamp}.png", 640, 360);
                    FrameGrab.Save(side, $"{dir}/side/{stamp}.png", 640, 360);
                }
            }
            File.WriteAllText(dir + "/telemetry.csv", csv.ToString());
            var summary = new StringBuilder();
            summary.AppendLine($"{label} ({prefabPath}) executed with real E; 60 Hz fixed; frames every 2 ({frames} sampled).");
            summary.AppendLine($"impacts {impacts}, deaths {deaths} at {deathAt:F3} s (game time); grip {grip.ToString("F2")}; robot root moved {(target ? (target.transform.position - rootStart).ToString("F2") : "destroyed")}");
            summary.AppendLine($"busy (M1/F/1/2/G locked) ends at frame {busyEnd} = {(busyEnd * Frame):F3} s real; hands disarm ends at {(handsEnd * Frame):F3} s; scripted move / Big Bot sequence ends at {(scriptedEnd * Frame):F3} s real");
            if (big && bigSeq) summary.AppendLine($"BigBot TotalTime {bigSeq.TotalTime:F2} s, shots {bigSeq.ShotCount}");
            summary.AppendLine($"hand beats: yank {hands.DisarmYankTime:F3} s, wind-up {hands.DisarmWindUpTime:F3} s, strike {hands.DisarmStrikeTime:F3} s");
            summary.AppendLine("events:"); foreach (var e in events) summary.AppendLine("  " + e);
            File.WriteAllText(dir + "/summary.txt", summary.ToString());
            Object.Destroy(side.gameObject);
            Assert.That(deaths, Is.EqualTo(1), label + " one death");
        }
    }
}
