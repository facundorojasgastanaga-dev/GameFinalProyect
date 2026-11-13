using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace IronRequiem
{
    // Benchmark fixture only (SecondaryShotgunBenchmark scene, Player builds). Repeatable 20 s waves with the
    // current combat prefabs: 8 T-07 + 4 Big Bots with live AI, navigation and shooting, player primary and
    // shotgun fire through the Input System, staged Health damage (progressive destruction) and kills (body
    // explosions). Enemy health is invulnerable outside the scripted stages so every wave has the same load.
    public sealed class CombatLoadBenchmark : MonoBehaviour
    {
        [SerializeField] GameObject t07Prefab;
        [SerializeField] GameObject bigBotPrefab;
        [SerializeField] Vector3 playerPosition = new Vector3(0, .05f, -14);
        [SerializeField] Vector3 gridCenter = new Vector3(0, 0, 6);
        const int T07Count = 8, BigCount = 4, Count = T07Count + BigCount;
        readonly GameObject[] bots = new GameObject[Count];
        PlayerCharacterController player; PlayerWeaponsManager inventory; EnemyManager enemies; PatrolPath path;
        Keyboard keys; Mouse mouse; CombatFrameCapture capture; string output; int cycles = 15, action; bool profileRequested, profiling;
        readonly List<string> errors = new List<string>(); readonly System.Text.StringBuilder spawnLog = new System.Text.StringBuilder(); EnemySpawnQueue queue;

        public void Configure(GameObject t07, GameObject bigBot) { t07Prefab = t07; bigBotPrefab = bigBot; }
        static string Arg(string name, string fallback) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1]; return fallback; }

        void Awake()
        {
            if (Application.isEditor) { enabled = false; return; }
            var flow = Object.FindAnyObjectByType<GameFlowManager>(); if (flow) flow.enabled = false;
            var objectives = Object.FindAnyObjectByType<ObjectiveManager>(); if (objectives) objectives.enabled = false;
        }

        IEnumerator Start()
        {
            if (Application.isEditor) yield break;
            output = Arg("--iron-output", Application.persistentDataPath + "/CombatLoad/run"); Directory.CreateDirectory(Path.GetDirectoryName(output));
            Application.logMessageReceived += OnLog; int.TryParse(Arg("--iron-cycles", "15"), out cycles); cycles = Mathf.Clamp(cycles, 1, 15);
            profileRequested = Debug.isDebugBuild && Arg("--iron-profile", "0") == "1";
            UnityEngine.Random.InitState(9707); InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; Time.captureDeltaTime = 0; Time.timeScale = 1;
            yield return null; yield return null;
            // Windows display scaling (120 %) makes the desktop report 1600x900; exclusive fullscreen sets the real 1920x1080 mode.
            // The mode switch can be refused while the window is still being created: retry a few times.
            for (int attempt = 0; attempt < 5 && (Screen.width != 1920 || Screen.height != 1080 || Screen.fullScreenMode != FullScreenMode.ExclusiveFullScreen); attempt++)
            { Screen.SetResolution(1920, 1080, FullScreenMode.ExclusiveFullScreen); yield return new WaitForSecondsRealtime(2f); }
            File.WriteAllText(output + "-display.txt", "screen=" + Screen.width + "x" + Screen.height + " mode=" + Screen.fullScreenMode + " display=" + Display.main.systemWidth + "x" + Display.main.systemHeight + " refresh=" + Screen.currentResolution.refreshRateRatio);
            // Profiling-only runs may accept another resolution (CPU-bound attribution); measured runs never do.
            if ((Screen.width != 1920 || Screen.height != 1080) && Arg("--iron-any-resolution", "0") != "1") { File.WriteAllText(output + ".failed", "Requested 1920x1080 fullscreen but got " + Screen.width + "x" + Screen.height); Application.Quit(3); yield break; }
            try { Initialize(); } catch (Exception e) { File.WriteAllText(output + ".failed", e.ToString()); Application.Quit(2); yield break; }
            yield return null; yield return null;
            capture = new CombatFrameCapture(Time.realtimeSinceStartupAsDouble);
            yield return Wave(-1);
            capture.Save(output + "-warmup", errors.ToArray()); capture.Dispose(); capture = null;
            yield return new WaitForSecondsRealtime(2);
            errors.Clear(); capture = new CombatFrameCapture(Time.realtimeSinceStartupAsDouble);
            for (int wave = 0; wave < cycles; wave++) yield return Wave(wave);
            capture.Save(output, errors.ToArray()); capture.Dispose(); capture = null;
            InputSystem.QueueStateEvent(keys, new KeyboardState()); InputSystem.QueueStateEvent(mouse, new MouseState());
            File.WriteAllText(output + ".done", "Completed warmup and measured cycles.");
            yield return null; Application.Quit(errors.Count == 0 ? 0 : 2);
        }

        void Initialize()
        {
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (menu) { menu.BeginGame(); menu.enabled = false; }
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (waves) waves.enabled = false;
            // Scenes whose wave director uses the spawn queue are measured the same way: own queue, prewarm in calm
            // time (before the first wave and after each cleanup), one activation per frame.
            string spawnMode = Arg("--iron-spawn", "auto"); int.TryParse(Arg("--iron-activations", "1"), out int perFrame);
            if (waves && waves.SpawnQueue) waves.SpawnQueue.enabled = false;
            if (spawnMode == "queue" || (spawnMode == "auto" && waves && waves.SpawnQueue)) { queue = gameObject.AddComponent<EnemySpawnQueue>(); queue.Configure(1, Mathf.Max(1, perFrame)); PrewarmWave(); }
            File.WriteAllText(output + "-spawnmode.txt", "spawn=" + (queue ? "queue activations/frame=" + Mathf.Max(1, perFrame) : "direct"));
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); inventory = player.GetComponent<PlayerWeaponsManager>();
            player.GetComponent<Health>().Invincible = true; enemies = Object.FindAnyObjectByType<EnemyManager>(); path = Object.FindAnyObjectByType<PatrolPath>();
            foreach (var old in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) { enemies.UnregisterEnemy(old); Destroy(old.gameObject); }
            keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent();
            InputSystem.actions.devices = new InputDevice[] { keys, mouse }; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            inventory.OnAddedWeapon += (w, i) => w.OnShootProcessed += OnPlayerShot;
            for (int i = 0; i < 9; i++) { var w = inventory.GetWeaponAtSlotIndex(i); if (w) w.OnShootProcessed += OnPlayerShot; }
            if (!inventory.GetWeaponAtSlotIndex(1)) throw new InvalidOperationException("Secondary shotgun missing from benchmark player.");
        }

        void OnPlayerShot() { if (capture != null) capture.Result.playerShots++; }
        void OnEnemyShot() { if (capture != null) capture.Result.enemyShots++; }
        int waveT07Attacks, waveBigAttacks, waveBigWeaponShots;
        void OnEnemyDeath() { if (capture != null) capture.Result.deaths++; }
        void OnLog(string message, string stack, LogType type) { if ((type == LogType.Exception || type == LogType.Error) && errors.Count < 24 && !errors.Contains(message)) errors.Add(message + "\n" + stack); }

        Vector3 Slot(int i)
        {
            var wanted = gridCenter + new Vector3((i % 4 - 1.5f) * 6, 0, (i / 4 - 1) * 5);
            return NavMesh.SamplePosition(wanted, out var hit, 4, NavMesh.AllAreas) ? hit.position : wanted;
        }

        IEnumerator Wave(int cycle)
        {
            double start = Time.realtimeSinceStartupAsDouble; action = 1;
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.SetPositionAndRotation(playerPosition, Quaternion.identity); cc.enabled = true; player.CharacterVelocity = Vector3.zero;
            inventory.SwitchToWeaponIndex(0, true);
            bool profileSpawn = cycle == 2 && profileRequested && Arg("--iron-profile-window", "combat") == "spawn";
            if (profileSpawn) { UnityEngine.Profiling.Profiler.logFile = output + "-profile.raw"; UnityEngine.Profiling.Profiler.enableBinaryLog = true; UnityEngine.Profiling.Profiler.enabled = true; profiling = true; }
            waveT07Attacks = waveBigAttacks = waveBigWeaponShots = 0;
            if (queue)
            {
                // Slot order (T-07 first) by default. Note: in this fixed grid, simultaneous spawn lets the front T-07 bodies
                // block the Big Bots' line of fire (BigBotCombat.ClearSegment); with staggered arrival the Big Bots find a lane
                // and fire ~5x more (both orders measured), so queue runs carry more enemy fire than direct runs.
                bool bigFirst = Arg("--iron-spawn-order", "slot") == "big-first";
                for (int n = 0; n < Count; n++) { int i = bigFirst ? (n + T07Count) % Count : n; int slot = i; queue.Spawn(i < T07Count ? t07Prefab : bigBotPrefab, Slot(i), Quaternion.Euler(0, 180, 0), go => Hook(slot, go)); }
                File.AppendAllText(output + "-spawn.csv", cycle + ",queued,prewarmedT07=" + queue.Ready(t07Prefab) + ",prewarmedBig=" + queue.Ready(bigBotPrefab) + "\n");
            }
            else
            {
            // Spawn diagnostics: deserialize under an inactive holder, then activate (Awake/OnEnable). Same frame, same load as before.
            var holder = new GameObject("SpawnHolder"); holder.SetActive(false); var watch = new System.Diagnostics.Stopwatch();
            double instMs = 0, actMs = 0, instT07 = 0, instBig = 0, actT07 = 0, actBig = 0;
            for (int i = 0; i < Count; i++)
            {
                watch.Restart(); bots[i] = Instantiate(i < T07Count ? t07Prefab : bigBotPrefab, Slot(i), Quaternion.Euler(0, 180, 0), holder.transform);
                double a = watch.Elapsed.TotalMilliseconds; watch.Restart(); bots[i].transform.SetParent(null, true); double b = watch.Elapsed.TotalMilliseconds;
                instMs += a; actMs += b; if (i < T07Count) { instT07 += a; actT07 += b; } else { instBig += a; actBig += b; }
                Hook(i, bots[i]);
            }
            Destroy(holder);
            double spawnFrameStart = Time.realtimeSinceStartupAsDouble;
            spawnLog.AppendLine(cycle + "," + instMs.ToString("F2") + "," + actMs.ToString("F2") + "," + instT07.ToString("F2") + "," + instBig.ToString("F2") + "," + actT07.ToString("F2") + "," + actBig.ToString("F2") + "," + (spawnFrameStart - start) * 1000);
            File.WriteAllText(output + "-spawn.csv", "cycle,instantiateMs,activateMs,instT07Ms,instBigMs,actT07Ms,actBigMs,preSpawnMs\n" + spawnLog);
            }
            capture.Result.waves++;
            bool initialized = false, s1 = false, s2 = false, s3 = false, k1 = false, k2 = false, clean = false, toShotgun = false, toPrimary = false, toShotgun2 = false, photographed = false;
            while (Time.realtimeSinceStartupAsDouble - start < 20)
            {
                yield return null;
                double t = Time.realtimeSinceStartupAsDouble - start; action = 0;
                if (cycle == 2 && profileRequested)
                {
                    if (profileSpawn && t >= 1.5 && profiling) { UnityEngine.Profiling.Profiler.enabled = false; UnityEngine.Profiling.Profiler.enableBinaryLog = false; profiling = false; profileRequested = false; }
                    if (!profileSpawn && t >= 6 && t < 11 && !profiling) { UnityEngine.Profiling.Profiler.logFile = output + "-profile.raw"; UnityEngine.Profiling.Profiler.enableBinaryLog = true; UnityEngine.Profiling.Profiler.enabled = true; profiling = true; }
                    if (t >= 11 && profiling) { UnityEngine.Profiling.Profiler.enabled = false; UnityEngine.Profiling.Profiler.enableBinaryLog = false; profiling = false; profileRequested = false; }
                }
                if (!initialized && (!queue || queue.Pending == 0))
                {
                    int onNav = 0; foreach (var b in bots) if (b) { b.GetComponent<Health>().Invincible = true; var a = b.GetComponent<NavMeshAgent>(); if (a && a.isOnNavMesh) onNav++; }
                    if (onNav != Count) errors.Add("Expected " + Count + " NavMeshAgents on NavMesh, got " + onNav); initialized = true; action = 1;
                }
                if (t >= 5 && !toShotgun) { inventory.SwitchToWeaponIndex(1); toShotgun = true; action = 2; }
                if (t >= 10 && !toPrimary) { inventory.SwitchToWeaponIndex(0); toPrimary = true; action = 3; }
                if (t >= 13 && !toShotgun2) { inventory.SwitchToWeaponIndex(1); toShotgun2 = true; action = 4; }
                if (t >= 6 && !s1) { Stage(.73f); s1 = true; action = 5; }
                if (t >= 9 && !s2) { Stage(.48f); s2 = true; action = 6; }
                if (t >= 12 && !s3) { Stage(.23f); s3 = true; action = 7; }
                if (t >= 14 && !k1) { Kill(0, 4); Kill(T07Count, T07Count + 2); k1 = true; action = 8; }
                if (t >= 16 && !k2) { Kill(4, T07Count); Kill(T07Count + 2, Count); k2 = true; action = 9; }
                if (t >= 18.5 && !clean) { Cleanup(); clean = true; action = 10; if (queue && cycle < cycles - 1) PrewarmWave(); }
                Cursor.lockState = CursorLockMode.Locked;
                player.PlayerCamera.transform.rotation = Quaternion.Euler(4 + Mathf.Sin((float)t * .7f) * 3, Mathf.Sin((float)t * .3f) * 22, 0);
                if (cycle < 0 && t >= 10 && !photographed) { ScreenCapture.CaptureScreenshot(output + "-view.png"); photographed = true; }
                // Primary: held bursts. Shotgun: one press per 0.45 s (the weapon cadence limits actual shots).
                bool shotgun = inventory.GetActiveWeapon() == inventory.GetWeaponAtSlotIndex(1);
                bool fire = shotgun ? t % .45 < .06 : (t % 1.2 < .8);
                InputSystem.QueueStateEvent(keys, new KeyboardState()); InputSystem.QueueStateEvent(mouse, fire ? new MouseState().WithButton(MouseButton.Left) : new MouseState());
                capture.Tick((cycle < 0 ? 0 : cycle * 20) + Math.Min(20, t), cycle, action);
            }
            Cleanup();
            File.AppendAllText(output + "-shots.csv", cycle + ",t07Attacks=" + waveT07Attacks + ",bigAttacks=" + waveBigAttacks + ",bigWeaponShots=" + waveBigWeaponShots + "\n");
        }

        void Stage(float ratio)
        {
            foreach (var b in bots) if (b) { var h = b.GetComponent<Health>(); if (h.CurrentHealth <= 0) continue; h.Invincible = false; h.TakeDamage(Mathf.Max(0, h.CurrentHealth - h.MaxHealth * ratio), null); h.Invincible = true; }
        }
        void Kill(int from, int to) { for (int i = from; i < to; i++) if (bots[i]) { var h = bots[i].GetComponent<Health>(); h.Invincible = false; h.Kill(); } }
        void PrewarmWave() { queue.Prewarm(t07Prefab, T07Count); queue.Prewarm(bigBotPrefab, BigCount); }
        void Hook(int i, GameObject bot)
        {
            bots[i] = bot;
            var e = bot.GetComponent<EnemyController>(); if (path) e.PatrolPath = path; e.onAttack += OnEnemyShot; bool isBig = i >= T07Count; e.onAttack += () => { if (isBig) waveBigAttacks++; else waveT07Attacks++; };
            var big = bot.GetComponent<BigBotCombat>(); if (big) foreach (var w in bot.GetComponentsInChildren<WeaponController>(true)) { w.OnShoot += OnEnemyShot; w.OnShoot += () => waveBigWeaponShots++; }
            var h = bot.GetComponent<Health>(); h.OnDie += OnEnemyDeath; h.Invincible = true;
        }
        void Cleanup()
        {
            if (queue) queue.CancelPending();
            for (int i = 0; i < Count; i++)
            {
                if (!bots[i]) continue; var e = bots[i].GetComponent<EnemyController>();
                if (e && bots[i].GetComponent<Health>().CurrentHealth > 0) enemies.UnregisterEnemy(e);
                Destroy(bots[i]); bots[i] = null;
            }
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; if (capture != null) capture.Dispose(); }
    }
}
