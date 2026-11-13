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
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Arm continuity, execution threshold/health bar, T-07 execution timing, shotgun reload and bunker lab.
    // Scene from Reports/BunkerPolish/scene.txt (before = SecondaryShotgunLab). Virtual devices only.
    public sealed class BunkerPolishTests
    {
        const string Reports = "Tools/IronRequiem/Reports/BunkerPolish";
        PlayerCharacterController player; PlayerWeaponsManager weapons; StolenWeaponCarrier carrier; ProtagonistHandsPresentation hands;
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        bool background; float capture; GameObject current; readonly List<string> log = new List<string>();
        string Scene => File.Exists(Reports + "/scene.txt") ? File.ReadAllText(Reports + "/scene.txt").Trim() : "Assets/IronRequiem/Scenes/SecondaryShotgunLab.unity";
        string Tag => File.Exists(Reports + "/tag.txt") ? File.ReadAllText(Reports + "/tag.txt").Trim() : "before";
        string PrefabFolder => File.Exists(Reports + "/prefabs.txt") ? File.ReadAllText(Reports + "/prefabs.txt").Trim() : "Assets/IronRequiem/Prefabs/SecondaryShotgun/";

        [UnitySetUp] public IEnumerator Setup()
        {
            Directory.CreateDirectory(Reports);
            capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = 0; Time.timeScale = 1;
            EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (menu) { menu.BeginGame(); menu.enabled = false; }
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (waves) waves.enabled = false;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); weapons = player.GetComponent<PlayerWeaponsManager>(); carrier = player.GetComponent<StolenWeaponCarrier>(); hands = player.GetComponent<ProtagonistHandsPresentation>();
            player.GetComponent<Health>().Invincible = true;
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "PolishFloor"; floor.transform.position = new Vector3(0, 29.5f, 0); floor.transform.localScale = new Vector3(80, 1, 80);
            Move(new Vector3(0, 30.05f, -20)); Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.5f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1; Time.captureDeltaTime = capture; AudioListener.pause = false; Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("PolishCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }

        void Move(Vector3 p) { var c = player.GetComponent<CharacterController>(); c.enabled = false; player.transform.SetPositionAndRotation(p, Quaternion.identity); c.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        void Keys(params Key[] pressed) { Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keys, new KeyboardState(pressed)); }
        IEnumerator Tap(params Key[] pressed) { Keys(pressed); yield return null; yield return null; Keys(); yield return null; }
        void Write(string name) { File.WriteAllText(Reports + "/" + Tag + "-" + name + ".txt", string.Join("\n", log)); log.Clear(); }
        void Capture(string name)
        {
            var camera = player.PlayerCamera; var rt = new RenderTexture(960, 540, 24); var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); var old = RenderTexture.active; var target = camera.targetTexture;
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); File.WriteAllBytes(Reports + "/" + Tag + "-" + name + ".png", tex.EncodeToPNG()); }
            finally { camera.targetTexture = target; RenderTexture.active = old; Object.Destroy(rt); Object.Destroy(tex); }
        }
        IEnumerator Spawn(string name, Vector3 position)
        {
            current = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab"), position, Quaternion.Euler(0, 180, 0));
            var m = current.GetComponent<EnemyMobile>(); if (m) m.enabled = false; var e = current.GetComponent<EnemyController>(); e.enabled = false;
            var big = current.GetComponent<BigBotCombat>(); if (big) big.enabled = false; var agent = current.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent) agent.enabled = false;
            yield return null; yield return null;
        }
        void Aim(float gap)
        {
            var box = current.GetComponent<Collider>(); Move(new Vector3(box.bounds.center.x, box.bounds.min.y + .03f, box.bounds.min.z - gap));
            player.PlayerCamera.transform.LookAt(box.bounds.center); Physics.SyncTransforms(); Cursor.lockState = CursorLockMode.Locked;
        }

        // Arm continuity: the elbow (cut) end of a visible FPS forearm inside the view is a visible cut unless the
        // sleeve covers it. cutFrames counts such frames; armFrames counts frames with a visible forearm.
        int cutFrames, armFrames; float worstInside;
        void ArmProbe()
        {
            var cam = player.PlayerCamera;
            void One(Transform forearm, Renderer view, Renderer sleeve)
            {
                if (!forearm || !view || !view.enabled || !view.gameObject.activeInHierarchy) return; armFrames++;
                var v = cam.WorldToViewportPoint(forearm.position); bool inside = v.z > cam.nearClipPlane && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1;
                if (inside) worstInside = Mathf.Max(worstInside, Mathf.Min(Mathf.Min(v.x, 1 - v.x), Mathf.Min(v.y, 1 - v.y)));
                if (inside && !(sleeve && sleeve.enabled)) cutFrames++;
            }
            One(hands.LeftForearm, hands.LeftForearmView, hands.LeftSleeve); One(hands.RightForearm, hands.RightForearmView, hands.RightSleeve);
        }

        [UnityTest] public IEnumerator DiagnoseArmsAndCapture()
        {
            Time.captureDeltaTime = 1f / 60; player.PlayerCamera.transform.localRotation = Quaternion.identity;
            foreach (int slot in new[] { 0, 1 })
            {
                weapons.SwitchToWeaponIndex(slot); yield return new WaitForSeconds(1.2f);
                yield return Tap(Key.F);
                for (int i = 0; i < 40; i++) { if (i % 4 == 0) Capture("punch-slot" + slot + "-" + i.ToString("00")); ArmProbe(); yield return null; }
                yield return new WaitForSeconds(.4f);
            }
            log.Add("punch: forearm frames " + armFrames + ", elbow end inside view " + worstInside.ToString("F3") + ", uncovered cut frames " + cutFrames + ", sleeves " + hands.ArmSleeves); cutFrames = 0;
            // Executions.
            foreach (var name in new[] { "T07_HitFeedback_v04_NavThrottle", "BigBot_Burst_v10_NavThrottle" })
            {
                worstInside = 0; armFrames = 0; cutFrames = 0; player.enabled = name.StartsWith("BigBot"); Move(new Vector3(0, 30.05f, -12)); yield return Spawn(name, new Vector3(0, 30.05f, -6));
                var h = current.GetComponent<Health>(); h.TakeDamage(h.MaxHealth * .3f, player.gameObject); Aim(1.2f); yield return null; yield return null;
                var source = current.GetComponent<EnemyExecutionSource>(); Assert.That(carrier.TryExecute(source), Is.True, name);
                float hitTime = -1, dieTime = -1, start = Time.time; int hits = 0; float last = h.CurrentHealth;
                h.OnDie += () => dieTime = Time.time - start;
                for (int i = 0; i < 240; i++)
                {
                    if (i % 6 == 0) Capture("exec-" + name.Substring(0, 6) + "-" + i.ToString("000")); ArmProbe(); yield return null;
                    if (h && h.CurrentHealth < last) { hits++; hitTime = Time.time - start; last = h.CurrentHealth; }
                    if (!carrier.ExecutionInProgress && !hands.IsDisarming && i > 30) break;
                }
                log.Add(name + ": dieAt " + dieTime.ToString("F3") + " s, strikeTime " + hands.DisarmStrikeTime.ToString("F3") + " s, forearm frames " + armFrames + ", uncovered cut frames " + cutFrames + ", health drops " + hits); if (hands.ArmSleeves) Assert.That(cutFrames, Is.Zero, name + " cut visible");
                player.enabled = true; yield return Tap(Key.G); yield return new WaitForSeconds(.6f); if (current) Object.Destroy(current); yield return null;
            }
            Write("diagnose-arms");
        }

        [UnityTest] public IEnumerator DiagnoseHealthBars()
        {
            foreach (var name in new[] { "T07_HitFeedback_v04_NavThrottle", "BigBot_Burst_v10_NavThrottle" })
            {
                yield return Spawn(name, new Vector3(0, 30.05f, 0)); var h = current.GetComponent<Health>();
                foreach (float ratio in new[] { .9f, .76f, .75f, .6f, .5f })
                {
                    h.CurrentHealth = h.MaxHealth * ratio; yield return null; yield return null;
                    var bars = current.GetComponentsInChildren<Unity.FPS.UI.WorldspaceHealthBar>(true);
                    var info = string.Join("; ", bars.Select(b => "bar health=" + (b.Health == h ? "root" : b.Health ? b.Health.name : "null") + " fill=" + b.HealthBarImage.fillAmount.ToString("F3") + " type=" + b.HealthBarImage.type + " method=" + b.HealthBarImage.fillMethod + " scaleX=" + b.HealthBarImage.rectTransform.localScale.x.ToString("F2")));
                    log.Add(name + " health " + ratio + " eligible=" + current.GetComponent<EnemyExecutionSource>().Eligible + " healths=" + current.GetComponentsInChildren<Health>(true).Length + " | " + info);
                }
                Object.Destroy(current); yield return null;
            }
            Write("diagnose-bars");
        }

        [UnityTest] public IEnumerator ExecutionBelow75PercentForAllEnemies()
        {
            foreach (var name in new[] { "T07_HitFeedback_v04_NavThrottle", "BigBot_Burst_v10_NavThrottle" })
            {
                yield return Spawn(name, new Vector3(0, 30.05f, 0)); var h = current.GetComponent<Health>(); var src = current.GetComponent<EnemyExecutionSource>();
                foreach (float r in new[] { .9f, .76f, .75f, .7499f, .51f, .5f, .499f })
                {
                    h.CurrentHealth = h.MaxHealth * r; yield return null;
                    var bar = current.GetComponentInChildren<Unity.FPS.UI.WorldspaceHealthBar>(true);
                    Assert.That(src.Eligible, Is.EqualTo(r < .75f), name + " at " + r); if (bar) Assert.That(bar.HealthBarImage.fillAmount, Is.EqualTo(r).Within(.002f));
                    log.Add(name + " " + (r * 100).ToString("F2") + "% -> eligible " + src.Eligible + (bar ? " bar " + bar.HealthBarImage.fillAmount.ToString("F3") : ""));
                }
                // A legacy per-prefab value cannot relax the game rule.
                var field = typeof(EnemyExecutionSource).GetField("healthThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); field.SetValue(src, .95f);
                h.CurrentHealth = h.MaxHealth * .8f; yield return null; Assert.That(src.Eligible, Is.False); log.Add(name + " legacy threshold 0.95 at 80% -> eligible " + src.Eligible);
                Object.Destroy(current); yield return null;
            }
            yield return Spawn("HoverBot_CombatPolish_NavThrottle", new Vector3(0, 30.05f, 0));
            log.Add("HoverBot: execution component " + (current.GetComponent<EnemyExecutionSource>() != null) + " (no stealable module, cannot be executed)"); Object.Destroy(current); yield return null;
            Write("threshold");
        }

        [UnityTest] public IEnumerator T07DiesOnSecondBlowWithTwoImpacts()
        {
            Time.captureDeltaTime = 1f / 60; player.enabled = false; Move(new Vector3(0, 30.05f, -12)); yield return Spawn("T07_HitFeedback_v04_NavThrottle", new Vector3(0, 30.05f, -6));
            var h = current.GetComponent<Health>(); h.TakeDamage(h.MaxHealth * .3f, player.gameObject); Aim(1.2f); yield return null; yield return null;
            var feedback = current.GetComponent<RobotHitFeedback>(); int impacts0 = feedback ? feedback.ImpactCount : 0; int deaths = 0; float dieAt = -1, start = Time.time; h.OnDie += () => { deaths++; dieAt = Time.time - start; };
            var src = current.GetComponent<EnemyExecutionSource>(); Assert.That(carrier.TryExecute(src), Is.True);
            Assert.That(h.CurrentHealth, Is.GreaterThan(0), "alive at E"); Assert.That(carrier.Held, Is.Not.Null, "weapon delivered at E");
            float yankImpactAt = -1; for (int i = 0; i < 120; i++) { yield return null; if (feedback && yankImpactAt < 0 && feedback.ImpactCount > impacts0) yankImpactAt = Time.time - start; if (i % 6 == 0) Capture("t07-exec-" + i.ToString("000")); }
            int impacts = feedback ? feedback.ImpactCount - impacts0 : -1;
            log.Add("first blow (wrench) impact at " + yankImpactAt.ToString("F3") + " s; death at " + dieAt.ToString("F3") + " s; strike " + hands.DisarmStrikeTime.ToString("F3") + " s; impacts " + impacts + "; deaths " + deaths);
            Assert.That(deaths, Is.EqualTo(1)); Assert.That(dieAt, Is.EqualTo(hands.DisarmStrikeTime).Within(.05f)); Assert.That(yankImpactAt, Is.GreaterThan(.1f).And.LessThan(dieAt - .1f)); Assert.That(impacts, Is.GreaterThanOrEqualTo(2));
            Assert.That(weapons.GetWeaponAtSlotIndex(3), Is.Null); Assert.That(src.Delivered, Is.True);
            Write("t07-timing");
        }

        [UnityTest] public IEnumerator ShotgunReloadsBankOfFourWithVent()
        {
            var shotgun = weapons.GetWeaponAtSlotIndex(1); weapons.SwitchToWeaponIndex(1); yield return new WaitForSeconds(1.3f); player.enabled = false;
            var reload = shotgun.GetComponent<CylinderBankReload>(); Assert.That(reload, Is.Not.Null); Assert.That(shotgun.AutomaticReload, Is.False);
            Assert.That(shotgun.GetCurrentAmmo(), Is.EqualTo(4));
            Assert.That(shotgun.HandleShootInputs(true, true, false), Is.True); yield return new WaitForSeconds(3); Assert.That(shotgun.GetCurrentAmmo(), Is.EqualTo(3), "no trickle regeneration"); log.Add("1 shot + 3 s wait: ammo 3 (no regeneration)");
            for (int k = 0; k < 3; k++) { yield return new WaitForSeconds(shotgun.DelayBetweenShots + .05f); Assert.That(shotgun.HandleShootInputs(true, true, false), Is.True); if (k == 2) Capture("shotgun-fire-flash"); }
            float empty = Time.time; yield return null; Assert.That(reload.IsReloading, Is.True);
            int frame = 0; bool blocked = true;
            while (reload.IsReloading && Time.time - empty < 3) { if (frame % 6 == 0) Capture("shotgun-reload-" + frame.ToString("000")); if (shotgun.HandleShootInputs(true, true, false)) blocked = false; frame++; yield return null; }
            float took = Time.time - empty; log.Add("reload after 4th shot: " + took.ToString("F2") + " s (configured " + reload.Duration.ToString("F2") + "), vented " + reload.Vented + ", shots blocked " + blocked + ", ammo after " + shotgun.GetCurrentAmmo());
            Assert.That(blocked, Is.True); Assert.That(reload.Vented, Is.True); Assert.That(shotgun.GetCurrentAmmo(), Is.EqualTo(4)); Assert.That(took, Is.EqualTo(reload.Duration).Within(.1f));
            // Switching away mid-reload pauses it; switching back resumes it.
            for (int k = 0; k < 4; k++) { yield return new WaitForSeconds(shotgun.DelayBetweenShots + .05f); shotgun.HandleShootInputs(true, true, false); }
            yield return new WaitForSeconds(.4f); Assert.That(reload.IsReloading, Is.True); weapons.SwitchToWeaponIndex(0); yield return new WaitForSeconds(2.5f);
            Assert.That(reload.IsReloading, Is.True, "paused while holstered"); weapons.SwitchToWeaponIndex(1); float back = Time.time; while (reload.IsReloading && Time.time - back < 4) yield return null;
            log.Add("switch away mid-reload: paused; resumed and finished " + (Time.time - back).ToString("F2") + " s after re-equip (incl. raise); ammo " + shotgun.GetCurrentAmmo());
            Assert.That(shotgun.GetCurrentAmmo(), Is.EqualTo(4));
            Write("shotgun-reload");
        }

        [UnityTest] public IEnumerator BunkerIsClosedLitAndNavigable()
        {
            if (!Scene.Contains("Bunker")) { Assert.Ignore("bunker only"); yield break; }
            Move(new Vector3(0, .05f, -28)); player.PlayerCamera.transform.localRotation = Quaternion.identity; yield return new WaitForSeconds(.3f); Capture("bunker-room1");
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(-60, 0, 0); yield return null; Capture("bunker-ceiling");
            var up = new Ray(player.PlayerCamera.transform.position, Vector3.up);
            bool covered = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Any(r => r.enabled && r.name.StartsWith("Ceiling") && r.bounds.IntersectRay(up));
            Assert.That(covered, Is.True, "ceiling covers the view"); Assert.That(player.PlayerCamera.clearFlags, Is.Not.EqualTo(CameraClearFlags.Skybox));
            int lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.type == LightType.Point); log.Add("point lights " + lights + ", fog " + RenderSettings.fog);
            var gate = GameObject.Find("ArenaGate"); if (gate) gate.SetActive(false); yield return null; yield return null;
            var path = new UnityEngine.AI.NavMeshPath(); Assert.That(UnityEngine.AI.NavMesh.CalculatePath(new Vector3(0, 0, -20), new Vector3(0, 0, 70), UnityEngine.AI.NavMesh.AllAreas, path), Is.True);
            log.Add("NavMesh path room1 -> room2 (gate open): " + path.status + " corners " + path.corners.Length); Assert.That(path.status, Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathComplete));
            Move(new Vector3(0, .05f, 50)); player.PlayerCamera.transform.localRotation = Quaternion.identity; yield return new WaitForSeconds(.2f); Capture("bunker-room2");
            Move(new Vector3(0, .05f, 18)); yield return new WaitForSeconds(.2f); Capture("bunker-corridor");
            Write("bunker");
        }

        // Diagnostic: same 8 T-07 + 4 Big Bot grid as the benchmark, spawned directly vs through the queue; counts attacks.
        [UnityTest] public IEnumerator DiagnoseShotsDirectVsQueue()
        {
            if (!Scene.Contains("Bunker")) { Assert.Ignore("bunker only"); yield break; }
            var t07 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/SecondaryShotgun/T07_HitFeedback_v04_NavThrottle.prefab");
            var big = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/SecondaryShotgun/BigBot_Burst_v10_NavThrottle.prefab");
            var path = Object.FindAnyObjectByType<PatrolPath>();
            foreach (var old in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Move(new Vector3(0, .05f, -14)); yield return null;
            foreach (int mode in new[] { 0, 1, 0, 1 })
            {
                var bots = new GameObject[12]; int[] shots = new int[2]; EnemySpawnQueue queue = null;
                Vector3 Slot(int i) { var w = new Vector3(0, 0, 6) + new Vector3((i % 4 - 1.5f) * 6, 0, (i / 4 - 1) * 5); return UnityEngine.AI.NavMesh.SamplePosition(w, out var h, 4, UnityEngine.AI.NavMesh.AllAreas) ? h.position : w; }
                void Hook(int i, GameObject b)
                {
                    bots[i] = b; var e = b.GetComponent<EnemyController>(); if (path) e.PatrolPath = path; e.onAttack += () => shots[0]++;
                    if (b.GetComponent<BigBotCombat>()) foreach (var w in b.GetComponentsInChildren<WeaponController>(true)) w.OnShoot += () => shots[1]++;
                    b.GetComponent<Health>().Invincible = true;
                }
                if (mode == 1) { queue = new GameObject("DiagQueue").AddComponent<EnemySpawnQueue>(); queue.Prewarm(t07, 8); queue.Prewarm(big, 4); while (queue.PrewarmPending > 0) yield return null; }
                for (int i = 0; i < 12; i++) { var p = i < 8 ? t07 : big; int s = i; if (mode == 0) Hook(i, Object.Instantiate(p, Slot(i), Quaternion.Euler(0, 180, 0))); else queue.Spawn(p, Slot(i), Quaternion.Euler(0, 180, 0), g => Hook(s, g)); }
                float t0 = Time.time; while (Time.time - t0 < 10) { player.PlayerCamera.transform.rotation = Quaternion.identity; yield return null; }
                int hooks = 0; foreach (var b in bots) if (b) hooks++;
                var sub = bots.Where(b => b).Select(b => { var e = b.GetComponent<EnemyController>(); return b.name + "@" + b.transform.position.ToString("F1"); });
                log.Add((mode == 0 ? "direct" : "queue ") + ": T07 attacks " + shots[0] + ", BigBot weapon shots " + shots[1] + ", bots " + hooks + " | " + string.Join("; ", sub.Take(3)));
                foreach (var b in bots) if (b) Object.Destroy(b); if (queue) Object.Destroy(queue.gameObject); yield return new WaitForSeconds(.5f);
            }
            Write("shots-direct-vs-queue");
        }

        // Real wave director: wave 1 is prewarmed while idle, then its enemies come in one per frame (never a whole
        // wave in one frame), each on the NavMesh, and the wave cannot be counted as cleared while some are still queued.
        [UnityTest] public IEnumerator WaveEnemiesArriveOnePerFrameFromPrewarm()
        {
            if (!Scene.Contains("Bunker")) { Assert.Ignore("bunker only"); yield break; }
            var lab = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); Assert.That(lab.SpawnQueue, Is.Not.Null, "spawn queue assigned");
            var queue = lab.SpawnQueue; int wait = 0; while (queue.PrewarmPending > 0 && wait++ < 600) yield return null;
            Assert.That(queue.PrewarmPending, Is.EqualTo(0), "prewarm finished while idle");
            var known = new HashSet<EnemyController>(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None));
            log.Add("prewarm done after " + wait + " frames; active enemies before wave " + known.Count);
            Move(new Vector3(0, .05f, -14)); lab.enabled = true;
            int frames = 0, maxPerFrame = 0, total = 0; bool sawPendingWithNoneAlive = false;
            while (frames++ < 240)
            {
                yield return null;
                int arrived = 0; foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) if (known.Add(e)) { arrived++; var a = e.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (a) Assert.That(a.isOnNavMesh, Is.True, e.name + " on NavMesh"); }
                if (arrived > 0) log.Add("frame " + frames + ": +" + arrived + " (alive " + lab.LivingEnemies + ", pending " + lab.PendingSpawns + ")");
                maxPerFrame = Mathf.Max(maxPerFrame, arrived); total += arrived;
                if (lab.PendingSpawns > 0 && lab.LivingEnemies == 0 && total > 0) sawPendingWithNoneAlive = true;
                if (lab.WaveIndex == 0 && lab.PendingSpawns == 0 && total > 0) break;
            }
            log.Add("wave 1: " + total + " enemies, max per frame " + maxPerFrame + ", wave index " + lab.WaveIndex + ", living " + lab.LivingEnemies);
            Write("spawn-queue");
            Assert.That(lab.WaveIndex, Is.EqualTo(0), "still wave 1");
            Assert.That(total, Is.GreaterThan(1)); Assert.That(lab.LivingEnemies, Is.EqualTo(total));
            Assert.That(maxPerFrame, Is.EqualTo(1), "one activation per frame");
            Assert.That(sawPendingWithNoneAlive, Is.False);
        }
    }
}
