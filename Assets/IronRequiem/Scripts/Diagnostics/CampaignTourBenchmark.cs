using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    /// v35 diagnostics: plays the whole campaign by itself and measures every stage change and every combat room.
    /// Runs when the Player is started with "-irTour" (development build) or, in the Editor, when the Perf menu arms it
    /// (PlayerPrefs IR_Tour = 1, consumed once). The player is invincible, the intro is skipped, each combat room is
    /// sampled for a few seconds with its first round alive, then every round is cleared and the player is placed in the
    /// airlock. For each transition it records the longest frame, frames over 33/50 ms, GC allocated and collections,
    /// and the CampaignFlow phase timings (door, load to 90 %, activation, unload, settle, asset sweep).
    /// Report: Player → ir_tour.txt next to the executable; Editor → Tools/IronRequiem/Reports/Perf/tour_editor_<tag>.txt.
    public sealed class CampaignTourBenchmark : MonoBehaviour
    {
        const string Arg = "-irTour";
        [SerializeField, Min(1)] float combatSampleSeconds = 6f, idleSampleSeconds = 3f;
        CampaignFlow flow; PlayerCharacterController player;
        readonly StringBuilder report = new StringBuilder();
        readonly List<float> frames = new List<float>(8192);
        ProfilerRecorder gcAlloc, gcCollect, shaderCompile, awake, readObject, present;
        // v35 diagnostic (-irMarkers): every timing marker is recorded so the report can name where frames go
        static bool Markers;
        readonly List<ProfilerRecorder> probe = new List<ProfilerRecorder>(); readonly List<string> probeNames = new List<string>();
        double[] probeSum; string probeSpike = ""; float probeSpikeMs;
        void StartProbe()
        {
            var hs = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(hs);
            var seen = new HashSet<string>();
            foreach (var h in hs)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds || !seen.Add(d.Name)) continue;
                if (probe.Count >= 3000) break;
                probe.Add(ProfilerRecorder.StartNew(d.Category, d.Name, 1)); probeNames.Add(d.Name);
            }
            probeSum = new double[probe.Count];
            Debug.Log("[Tour] markers recorded: " + probe.Count);
        }
        void ProbeFrame(float ms)
        {
            if (probeSum == null) return;
            for (int i = 0; i < probe.Count; i++) probeSum[i] += probe[i].LastValue;
            if (ms > 25f && ms > probeSpikeMs) { probeSpikeMs = ms; probeSpike = Top(i => probe[i].LastValue / 1e6, 12); }
        }
        string Top(System.Func<int, double> value, int n)
        {
            var idx = new List<int>(); for (int i = 0; i < probe.Count; i++) if (value(i) > .05) idx.Add(i);
            idx.Sort((a, b) => value(b).CompareTo(value(a)));
            var t = new StringBuilder(); for (int k = 0; k < Mathf.Min(n, idx.Count); k++) t.Append($"{probeNames[idx[k]]} {value(idx[k]):0.00}; ");
            return t.ToString();
        }
        string ProbeText()
        {
            if (probeSum == null || splitFrames == 0) return "";
            int f = splitFrames; var avg = Top(i => probeSum[i] / f / 1e6, 25);
            return $"\n    MEDIA/frame: {avg}" + (probeSpikeMs > 0 ? $"\n    PICO {probeSpikeMs:0}ms: {probeSpike}" : "");
        }
        struct Spike { public float ms, at; public int phase; public float gc, shader, awake, read, present; }
        readonly List<Spike> spikes = new List<Spike>(); float began;
        ProfilerRecorder mainThread, renderThread; double mainSum, waitPresentSum; int splitFrames;
        bool recording; long gcBytes, gcPeak; int over33, over50; float peak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            bool armed = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), Arg) >= 0;
            if (Application.isEditor && PlayerPrefs.GetInt("IR_Tour", 0) == 1) { armed = true; PlayerPrefs.SetInt("IR_Tour", 0); PlayerPrefs.Save(); }
            if (!armed || FindAnyObjectByType<CampaignTourBenchmark>()) return;
            var args = System.Environment.GetCommandLineArgs();
            CampaignFlow.SkipWarm = System.Array.IndexOf(args, "-irNoWarm") >= 0;
            if (System.Array.IndexOf(args, "-irNoTeardown") >= 0) CampaignFlow.TeardownBudget = 0;
            Markers = System.Array.IndexOf(args, "-irMarkers") >= 0;
            var go = new GameObject("CampaignTourBenchmark"); DontDestroyOnLoad(go); go.AddComponent<CampaignTourBenchmark>();
        }

        void OnEnable()
        {
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            gcCollect = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "GC.Collect");
            shaderCompile = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shader.CreateGPUProgram");
            awake = ProfilerRecorder.StartNew(ProfilerCategory.Loading, "Loading.AwakeFromLoad");
            readObject = ProfilerRecorder.StartNew(ProfilerCategory.Loading, "Loading.ReadObject");
            present = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread");
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForGfxCommandsFromMainThread");
        }
        void OnDisable() { gcAlloc.Dispose(); gcCollect.Dispose(); shaderCompile.Dispose(); awake.Dispose(); readObject.Dispose(); present.Dispose(); mainThread.Dispose(); renderThread.Dispose(); foreach (var r in probe) r.Dispose(); }
        static float Ms(ProfilerRecorder r) => r.Valid ? r.LastValue / 1e6f : -1;
        IEnumerator Start()
        {
            // v35: the game restores the last window size; the tour wants the 1080p target
            if (Markers) StartProbe();
            if (!Application.isEditor && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-irFullHD") >= 0)
            {
                // Windowed dimensions can be constrained by desktop DPI scaling. Exclusive mode requests
                // the physical display mode; the guard below still rejects any substituted resolution.
                Screen.SetResolution(1920,1080,FullScreenMode.ExclusiveFullScreen);
                yield return new WaitForSecondsRealtime(2);
                if(Screen.width!=1920||Screen.height!=1080)
                {
                    Debug.LogError("[Tour] Requested 1920x1080; actual "+Screen.width+"x"+Screen.height+". No Full HD result recorded.");
                    Application.Quit(3);yield break;
                }
            }
            yield return Run();
        }

        void Update()
        {
            if (!recording) return;
            float ms = Time.unscaledDeltaTime * 1000f; frames.Add(ms); if (ms > peak) peak = ms; if (ms > 33.3f) over33++; if (ms > 50f) over50++;
            ProbeFrame(ms); splitFrames++; mainSum += Ms(mainThread); waitPresentSum += Ms(present);
            if (gcAlloc.Valid) { long b = gcAlloc.LastValue; gcBytes += b; if (b > gcPeak) gcPeak = b; }
            // keep the three longest frames with what the profiler markers spent in them (previous frame values)
            if (ms > 20f)
            {
                var sp = new Spike { ms = ms, at = Time.realtimeSinceStartup - began, phase = flow ? flow.Phase : -2, gc = Ms(gcCollect), shader = Ms(shaderCompile), awake = Ms(awake), read = Ms(readObject), present = Ms(present) };
                spikes.Add(sp); spikes.Sort((a, b) => b.ms.CompareTo(a.ms)); if (spikes.Count > 3) spikes.RemoveAt(3);
            }
        }
        void Begin() { frames.Clear(); spikes.Clear(); peak = 0; over33 = over50 = 0; gcBytes = gcPeak = 0; mainSum = waitPresentSum = 0; splitFrames = 0; probeSpike = ""; probeSpikeMs = 0; if (probeSum != null) System.Array.Clear(probeSum, 0, probeSum.Length); recording = true; began = Time.realtimeSinceStartup; }
        string SpikeText()
        {
            var t = new StringBuilder();
            foreach (var s in spikes) t.Append($" [{s.ms:0}ms @{s.at:0.00}s fase {(s.phase == -1 ? "llegada" : s.phase == 0 ? "puerta/carga" : s.phase == 1 ? "activación" : s.phase == 2 ? "descarga" : s.phase == 3 ? "asentado" : "?")} gc {s.gc:0.0} shader {s.shader:0.0} awake {s.awake:0.0} read {s.read:0.0} present {s.present:0.0}]");
            return t.ToString();
        }
        void End() { recording = false; }
        string Split() => splitFrames > 0 ? $" · main {mainSum / splitFrames:0.0} ms, espera present/GPU {waitPresentSum / splitFrames:0.0} ms" : "";
        (float avg, float p99) Stats()
        {
            if (frames.Count == 0) return (0, 0);
            var sorted = frames.ToArray(); System.Array.Sort(sorted); float sum = 0; foreach (var f in sorted) sum += f;
            return (sum / sorted.Length, sorted[Mathf.Min(sorted.Length - 1, (int)(sorted.Length * .99f))]);
        }

        IEnumerator Run()
        {
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            report.AppendLine($"Campaign tour {System.DateTime.Now:yyyy-MM-dd HH:mm} · {(Application.isEditor ? "EDITOR" : "PLAYER " + (Debug.isDebugBuild ? "development" : "release"))} · {SystemInfo.graphicsDeviceName} · {SystemInfo.processorType} · {Screen.width}x{Screen.height} · quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} · warm {(CampaignFlow.SkipWarm ? "off" : "on")} · teardown {(CampaignFlow.TeardownBudget > 0 ? CampaignFlow.TeardownBudget.ToString() : "off")} · vsync {QualitySettings.vSyncCount} · target {Application.targetFrameRate} · profiler {(UnityEngine.Profiling.Profiler.enabled ? "on" : "off")}{(Markers ? " · MARKERS" : "")}");
            if (!SceneManager.GetSceneByName("RequiemCampaign").isLoaded) SceneManager.LoadScene("RequiemCampaign", LoadSceneMode.Single);
            float limit = Time.realtimeSinceStartup + 180;
            while ((!(flow = FindAnyObjectByType<CampaignFlow>()) || flow.Busy || flow.CurrentStage != 0) && Time.realtimeSinceStartup < limit) yield return null;
            if (!flow || flow.CurrentStage != 0) { Finish("ERROR: campaign did not start"); yield break; }
            player = flow.Player; flow.Menu.BeginGame(); player.GetComponent<Health>().Invincible = true;
            if (flow.LastReport.scene != null) Line(flow.LastReport, -1, -1, 0, 0, 0, 0, "(first stage, behind the start cover)");
            var intro = FindAnyObjectByType<HangarIntroCinematic>();
            if (intro)
            {
                limit = Time.realtimeSinceStartup + 15; while (!intro.IsPlaying && !intro.Finished && Time.realtimeSinceStartup < limit) yield return null;
                intro.RequestSkip(); limit = Time.realtimeSinceStartup + 10;
                while ((!flow.CurrentRoom.Completed || PlayerInputLock.Locked) && Time.realtimeSinceStartup < limit) yield return null;
            }
            report.AppendLine("\nTRANSITIONS (ms / s)\nto | peak ms | >33ms | >50ms | GC alloc KB (max frame KB) | GC count | door s | load s | activate s (peak ms) | unload s (peak ms) | settle s | sweep s | total s");
            var combat = new StringBuilder("\nROOMS (frame time while the first round fights the invincible player)\nroom | avg ms | fps | p99 ms | max ms | >33ms | enemies\n");
            int stages = flow.StageScenes.Length;
            for (int stage = 0; stage < stages; stage++)
            {
                var room = flow.CurrentRoom; string name = room.gameObject.scene.name;
                if (room.Rounds) yield return FightRoom(room, name, combat);
                else { Begin(); yield return Wait(idleSampleSeconds); End(); var s = Stats(); combat.AppendLine($"{name} (no combat) | {s.avg:0.00} | {(s.avg > 0 ? 1000 / s.avg : 0):0} | {s.p99:0.00} | {peak:0.0} | {over33} | 0{Split()}{(spikes.Count > 0 ? " · picos:" + SpikeText() : "")}{ProbeText()}"); }
                if (stage == stages - 1) break;
                // exit door, then into the airlock
                limit = Time.realtimeSinceStartup + 15; while (!room.Completed && Time.realtimeSinceStartup < limit) yield return null;
                var airlock = flow.Airlocks[stage]; limit = Time.realtimeSinceStartup + 12;
                while (airlock.SourceDoor.activeSelf && Time.realtimeSinceStartup < limit) yield return null;
                int serial = flow.TransitionSerial; int gc0 = System.GC.CollectionCount(0);
                Begin(); limit = Time.realtimeSinceStartup + 20;
                while (!flow.Busy && Time.realtimeSinceStartup < limit) { PlaceInside(airlock); yield return new WaitForSeconds(.25f); }
                limit = Time.realtimeSinceStartup + 150;
                while ((flow.Busy || flow.TransitionSerial == serial) && Time.realtimeSinceStartup < limit) { if (!airlock.PlayerInside) PlaceInside(airlock); yield return null; }
                // a second of the arrival (door opening, environment blend, first spawns)
                yield return Wait(1f); End();
                if (flow.TransitionSerial == serial) { report.AppendLine($"{flow.StageScenes[stage + 1]} | TIMEOUT (stage {flow.CurrentStage} busy {flow.Busy})"); break; }
                Line(flow.LastReport, peak, over33, over50, gcBytes, gcPeak, System.GC.CollectionCount(0) - gc0, "· picos:" + SpikeText() + ProbeText());
                // step out of the airlock into the room
                yield return null;
            }
            report.Append(combat);
            Finish("done");
        }
        IEnumerator FightRoom(CampaignRoom room, string name, StringBuilder combat)
        {
            var rounds = room.Rounds; float limit = Time.realtimeSinceStartup + 60;
            // the arena starts when the player is inside it: walk in (floor under the arena centre)
            var arena = rounds.FirstRoomBounds;
            // stand where most spawn slots can reach the player (NavMesh path complete, outside the 6 m clearance):
            // candidates are the slots themselves and the midpoints between pairs of slots
            var slots = new List<Vector3>(); foreach (var t in rounds.FirstRoomSpawns) if (t && UnityEngine.AI.NavMesh.SamplePosition(t.position, out var sh, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) slots.Add(sh.position);
            var candidates = new List<Vector3>(slots); for (int a = 0; a < slots.Count; a++) for (int b = a + 1; b < slots.Count; b++) candidates.Add((slots[a] + slots[b]) * .5f);
            var path = new UnityEngine.AI.NavMeshPath(); Vector3 best = player.transform.position; int bestScore = -1;
            foreach (var c in candidates)
            {
                if (!UnityEngine.AI.NavMesh.SamplePosition(c, out var ch, 2, UnityEngine.AI.NavMesh.AllAreas) || !arena.Contains(ch.position + Vector3.up * .5f)) continue;
                int score = 0; foreach (var sp in slots) if (Vector3.Distance(sp, ch.position) > 7.5f && UnityEngine.AI.NavMesh.CalculatePath(sp, ch.position, UnityEngine.AI.NavMesh.AllAreas, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete) score++;
                if (score > bestScore) { bestScore = score; best = ch.position; }
            }
            if (bestScore > 0) { var move = player.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint(); player.TeleportToCheckpoint(best + Vector3.up * .05f); Physics.SyncTransforms(); }
            Debug.Log($"[Tour] {name}: stand {best} reachable slots {bestScore}/{slots.Count}");
            while (!(rounds.WaveIndex == 0 && rounds.PendingSpawns == 0 && rounds.LivingEnemies > 0) && !rounds.Completed && Time.realtimeSinceStartup < limit) yield return null;
            int enemies = rounds.LivingEnemies;
            Begin(); yield return Wait(combatSampleSeconds); End(); var s = Stats();
            combat.AppendLine($"{name} | {s.avg:0.00} | {(s.avg > 0 ? 1000 / s.avg : 0):0} | {s.p99:0.00} | {peak:0.0} | {over33} | {enemies}{Split()}{(spikes.Count > 0 ? " · picos:" + SpikeText() : "")}{ProbeText()}");
            if (Application.isEditor) Captures(name, arena); // after the sample: the renders take a long frame
            limit = Time.realtimeSinceStartup + 240;
            while (!rounds.Completed && Time.realtimeSinceStartup < limit)
            {
                foreach (var h in rounds.Living) if (h && h.CurrentHealth > 0) { h.Kill(); break; } // one per frame: the round logic sees every death
                yield return null;
            }
            if (!rounds.Completed) combat.AppendLine($"{name}: rounds not completed in time (wave {rounds.WaveIndex}, living {rounds.LivingEnemies}, pending {rounds.PendingSpawns})");
        }
        // 1920x1080 views of the arena for the before/after comparison: from the entrance side and from a high corner
        void Captures(string name, Bounds arena)
        {
            var dir = "Tools/IronRequiem/Bridge/v35/" + PlayerPrefs.GetString("IR_TourTag", "run") + "_play"; Directory.CreateDirectory(dir);
            // the player's own camera (URP post included) is moved for the two renders and put back the same frame
            var cam = player.PlayerCamera; var t = cam.transform; var lp = t.localPosition; var lr = t.localRotation; float fov = cam.fieldOfView; cam.fieldOfView = 75;
            var rt = new RenderTexture(1920, 1080, 24); var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); var prev = RenderTexture.active;
            try
            {
                var entry = flow.CurrentStage > 0 ? flow.Airlocks[flow.CurrentStage - 1].DestinationDoor.transform.position : player.transform.position;
                var floorY = player.transform.position.y; var eye = new Vector3(entry.x, floorY + 1.7f, entry.z); var toward = new Vector3(arena.center.x, eye.y, arena.center.z) - eye;
                if (toward.sqrMagnitude < 1) toward = player.transform.forward; eye += toward.normalized * 1.5f;
                Shot(cam, rt, tex, eye, toward, dir + "/" + name + "_entrada.png");
                var high = new Vector3(arena.min.x + 2, Mathf.Min(arena.max.y - 1, floorY + 9), arena.min.z + 2);
                Shot(cam, rt, tex, high, new Vector3(arena.center.x, floorY, arena.center.z) - high, dir + "/" + name + "_alto.png");
            }
            finally { t.localPosition = lp; t.localRotation = lr; cam.fieldOfView = fov; RenderTexture.active = prev; rt.Release(); Destroy(rt); Destroy(tex); }
        }
        static void Shot(Camera cam, RenderTexture rt, Texture2D tex, Vector3 pos, Vector3 look, string file)
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look)); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply(); File.WriteAllBytes(file, tex.EncodeToPNG()); cam.targetTexture = null;
        }
        void PlaceInside(CampaignAirlock airlock)
        {
            var box = airlock.GetComponent<Collider>().bounds; var top = new Vector3(box.center.x, box.max.y - .1f, box.center.z);
            var point = Physics.Raycast(top, Vector3.down, out var hit, box.size.y + 2, ~0, QueryTriggerInteraction.Ignore) ? hit.point + Vector3.up * .05f : new Vector3(box.center.x, box.min.y + .05f, box.center.z);
            var move = player.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint();
            player.TeleportToCheckpoint(point); Physics.SyncTransforms();
        }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        void Line(CampaignFlow.TransitionReport r, float pk, int o33, int o50, long gc, long gcMax, int collections, string note)
            => report.AppendLine($"{r.scene} | {(pk >= 0 ? pk : r.peakMs):0.0} | {o33} | {o50} | {gc / 1024f:0} ({gcMax / 1024f:0}) | {collections} | {r.doorSec:0.00} | {r.loadSec:0.00} | {r.activateSec:0.00} ({r.activatePeakMs:0}) | {r.unloadSec:0.00} ({r.unloadPeakMs:0}) | {r.settleSec:0.00} | {r.sweepSec:0.00} | {r.totalSec:0.00} {note}");
        void Finish(string status)
        {
            report.AppendLine("\nstatus: " + status);
            string path;
            if (Application.isEditor) { path = "Tools/IronRequiem/Reports/Perf/tour_editor_" + PlayerPrefs.GetString("IR_TourTag", "run") + ".txt"; Directory.CreateDirectory(Path.GetDirectoryName(path)); }
            else path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ir_tour.txt");
            File.WriteAllText(path, report.ToString()); Debug.Log("[Tour] " + status + " → " + path + "\n" + report);
            if (!Application.isEditor) Application.Quit();
            Destroy(gameObject);
        }
    }
}
