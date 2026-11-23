using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IronRequiem.Tests
{
    /// Map01 F2 (R0): automated traversal of each session with an invincible player that is teleported (not walked) through
    /// every stage. It checks the FLOW: rounds spawn and complete, respites release, the chase completes, every arena shutter
    /// ends open, every airlock transition happens, and each session hands off to the next one (S4 -> WinScene).
    /// It does NOT prove that the player can walk the geometry: that is the manual playthrough (PARADA F2).
    [Category("Map01F2")]
    public sealed class Map01F2Tests
    {
        static readonly string[] Sessions = { "Map01_Campaign_S1", "Map01_Campaign_S2", "Map01_Campaign_S3", "Map01_Campaign_S4" };
        static readonly string[] Next = { "Map01_Campaign_S2", "Map01_Campaign_S3", "Map01_Campaign_S4", "WinScene" };
        const string Report = "Tools/IronRequiem/Reports/Map01/F2/tests";
        readonly List<string> exceptions = new List<string>();
        readonly List<string> trace = new List<string>();

        void OnLog(string m, string st, LogType t) { if (t == LogType.Exception) exceptions.Add(m + "\n" + st); }

        [UnityTest, Timeout(1500000)]
        public IEnumerator SessionTraversal([Values(0, 1, 2, 3)] int session)
        {
            Application.logMessageReceived += OnLog;
            try
            {
                SceneManager.LoadScene(Sessions[session], LoadSceneMode.Single);
                yield return null; yield return null;
                var flow = Object.FindAnyObjectByType<CampaignFlow>();
                Assert.IsNotNull(flow, "CampaignFlow missing in " + Sessions[session]);
                var player = flow.Player; Assert.IsNotNull(player, "Player not bound");
                var health = player.GetComponent<Health>(); if (health) health.Invincible = true;
                int stages = flow.StageScenes.Length;
                Log($"{Sessions[session]}: {stages} etapas, {flow.Airlocks.Length} esclusas");
                for (int s = 0; s < stages; s++)
                {
                    float t0 = Time.realtimeSinceStartup;
                    while (!(flow.CurrentStage == s && !flow.Busy && flow.CurrentRoom)) { if (Time.realtimeSinceStartup - t0 > 60) Assert.Fail($"stage {s} ({flow.StageScenes[s]}) never became current/ready"); yield return null; }
                    var room = flow.CurrentRoom; var scene = room.gameObject.scene;
                    float roomStart = Time.realtimeSinceStartup;
                    if (room.Rounds)
                    {
                        // Stand on the connected floor of the arena, next to a spawn slot (like a player walking in), never on a roof.
                        var spots = (room.Rounds.FirstRoomSpawns ?? new Transform[0]).Where(t => t).OrderBy(t => t.position.y).ToArray();
                        int spot = 0;
                        void Enter() { if (spots.Length > 0) { var p = spots[spot % spots.Length].position; Teleport(player, p + new Vector3(1.5f, 0f, 1.5f), 2f); spot++; } else Teleport(player, room.Rounds.FirstRoomBounds.center, 6f); }
                        Enter();
                        int kills = 0, retries = 0;
                        while (!room.Completed)
                        {
                            foreach (var h in room.Rounds.Living.ToArray()) if (h && h.CurrentHealth > 0) { h.Kill(); kills++; }
                            if (room.Rounds.WaitingForSpawnSpace && Time.frameCount % 120 == 0) { Enter(); retries++; }
                            if (Time.realtimeSinceStartup - roomStart > 240) { Log(Diagnose(room.Rounds, player)); }
                            if (Time.realtimeSinceStartup - roomStart > 240) Assert.Fail($"{scene.name}: rounds not completed (wave {room.Rounds.WaveIndex}, pending {room.Rounds.PendingSpawns}, waiting {room.Rounds.WaitingForSpawnSpace})");
                            yield return null;
                        }
                        Log($"  {scene.name}: rondas completadas, {kills} bajas, {Time.realtimeSinceStartup - roomStart:0.0} s, reposicionamientos por espera {retries}");
                    }
                    else if (room.Pursuit)
                    {
                        var chase = room.Pursuit; var box = chase.GetComponent<BoxCollider>();
                        Teleport(player, box.bounds.center, 20);
                        float tc = Time.realtimeSinceStartup;
                        while (!chase.Running) { if (Time.realtimeSinceStartup - tc > 20) Assert.Fail(scene.name + ": chase never started"); yield return null; }
                        float finishZ = (float)typeof(ChaseCorridorEncounter).GetField("finishZ", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chase);
                        var offset = (Vector3)typeof(ChaseCorridorEncounter).GetField("courseOffset", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chase);
                        Teleport(player, new Vector3(offset.x, player.transform.position.y, finishZ + 3f), 10);
                        while (!room.Completed) { if (Time.realtimeSinceStartup - tc > 60) Assert.Fail(scene.name + ": chase not completed"); yield return null; }
                        Log($"  {scene.name}: persecución completada");
                    }
                    else
                    {
                        var release = Object.FindObjectsByType<Map01StageRelease>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.gameObject.scene == scene);
                        Assert.IsNotNull(release, scene.name + ": respite without Map01StageRelease (soft-lock)");
                        Teleport(player, release.GetComponent<BoxCollider>().bounds.center, 10);
                        while (!room.Completed) { if (Time.realtimeSinceStartup - roomStart > 20) Assert.Fail(scene.name + ": respite never released"); yield return null; }
                        Log($"  {scene.name}: respiro liberado");
                    }
                    // every arena shutter of this room must end OPEN (inactive door) after completion: never a soft-lock
                    float ts = Time.realtimeSinceStartup;
                    var shutters = Object.FindObjectsByType<ArenaShutter>(FindObjectsInactive.Include).Where(a => a.gameObject.scene == scene).ToArray();
                    while (shutters.Any(a => a.Door && a.Door.activeSelf)) { if (Time.realtimeSinceStartup - ts > 8) Assert.Fail(scene.name + ": shutter still closed after completion: " + string.Join(",", shutters.Where(a => a.Door && a.Door.activeSelf).Select(a => a.name))); yield return null; }
                    if (shutters.Length > 0) Log($"    {shutters.Length} persianas abiertas al terminar");

                    if (s < stages - 1)
                    {
                        var airlock = flow.Airlocks[s];
                        var trig = airlock.GetComponent<BoxCollider>();
                        float ta = Time.realtimeSinceStartup;
                        while (flow.CurrentStage == s)
                        {
                            if (!airlock.PlayerInside || Time.frameCount % 30 == 0) Teleport(player, trig.bounds.center, 4);
                            if (Time.realtimeSinceStartup - ta > 45) Assert.Fail($"airlock {airlock.name} (stage {s}) transition did not happen");
                            yield return null;
                        }
                        Log($"  esclusa {airlock.transform.parent?.parent?.name ?? airlock.name}: transición en {Time.realtimeSinceStartup - ta:0.0} s");
                    }
                }
                // hand-off to the next session (or WinScene from S4)
                var handoff = Object.FindAnyObjectByType<Map01SessionHandoff>();
                Assert.IsNotNull(handoff, "Map01SessionHandoff missing in " + Sessions[session]);
                float th = Time.realtimeSinceStartup;
                // End of map (S4) does not "arm" a departure door: it loads WinScene after its delay.
                while (handoff.Mode == Map01SessionHandoff.HandoffMode.Departure && !handoff.Armed) { if (Time.realtimeSinceStartup - th > 20) Assert.Fail("handoff never armed"); yield return null; }
                var handoffTrigger = handoff.GetComponent<BoxCollider>();
                while (SceneManager.GetActiveScene().name != Next[session])
                {
                    if (handoff && handoff.Mode == Map01SessionHandoff.HandoffMode.Departure && handoffTrigger && (!handoff.PlayerInside || Time.frameCount % 30 == 0)) Teleport(player, handoffTrigger.bounds.center, 4);
                    if (Time.realtimeSinceStartup - th > 60) Assert.Fail($"{Sessions[session]} did not hand off to {Next[session]}");
                    yield return null;
                }
                Log($"  → {Next[session]} cargada");
                Assert.IsEmpty(exceptions, "Exceptions during traversal:\n" + string.Join("\n", exceptions.Take(5)));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Directory.CreateDirectory(Report);
                File.AppendAllText(Report + "/traversal.txt", string.Join("\n", trace) + "\n" + (exceptions.Count > 0 ? "EXCEPTIONS:\n" + string.Join("\n", exceptions.Take(5)) + "\n" : ""));
            }
        }

        void Log(string s) { trace.Add(s); Debug.Log("[Map01Test] " + s); }

        /// Replays CombatTwoRoomsLab.SafeSpawn (CombatTwoRoomsLab.cs:117-128) for the current wave's prefabs on every slot and
        /// reports why each candidate is refused, with the player where it is now.
        static string Diagnose(CombatTwoRoomsLab lab, PlayerCharacterController player)
        {
            var sb = new System.Text.StringBuilder("    DIAGNÓSTICO espera: jugador " + player.transform.position + "\n");
            var plan = (CombatTwoRoomsLab.Group[][])typeof(CombatTwoRoomsLab).GetField("plan", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lab);
            int index = lab.WaveIndex; var bounds = lab.FirstRoomBounds; var path = new NavMeshPath(); var cols = new Collider[64];
            foreach (var g in plan[Mathf.Max(0, index)])
            {
                var agent = g.Prefab.GetComponentInChildren<NavMeshAgent>(true);
                float radius = agent ? Mathf.Max(.35f, agent.radius) : .75f, height = agent ? Mathf.Max(radius * 2, agent.height) : 2; int mask = agent ? agent.areaMask : NavMesh.AllAreas;
                var pts = g.FixedPoint ? new[] { g.FixedPoint } : lab.FirstRoomSpawns;
                var reasons = new Dictionary<string, int>();
                foreach (var t in pts)
                {
                    string r;
                    if (!NavMesh.SamplePosition(t.position, out var hit, .75f, mask)) r = "sin NavMesh";
                    else if (!bounds.Contains(hit.position)) r = "fuera de bounds";
                    else if (Vector3.Distance(hit.position, player.transform.position) < 6 + radius) r = "cerca del jugador";
                    else if (!NavMesh.SamplePosition(player.transform.position, out var tgt, 3, mask)) r = "jugador sin NavMesh (3 m)";
                    else if (!NavMesh.CalculatePath(hit.position, tgt.position, mask, path) || path.status != NavMeshPathStatus.PathComplete) r = "camino " + path.status;
                    else
                    {
                        var pos = hit.position; int n = Physics.OverlapCapsuleNonAlloc(pos + Vector3.up * (radius + .08f), pos + Vector3.up * (height - radius + .08f), radius, cols, -1, QueryTriggerInteraction.Ignore);
                        var b = Enumerable.Range(0, n).Select(i => cols[i]).FirstOrDefault(c => !c.transform.IsChildOf(player.transform) && c.bounds.max.y > pos.y + .08f);
                        r = b ? "cápsula: " + b.name : "OK";
                    }
                    reasons[r] = reasons.TryGetValue(r, out var c2) ? c2 + 1 : 1;
                }
                sb.AppendLine($"      {g.Prefab.name} x{g.Count}{(g.FixedPoint ? " FIJO " + g.FixedPoint.name : "")} mask {mask}: " + string.Join("; ", reasons.Select(kv => kv.Key + " " + kv.Value)));
            }
            return sb.ToString();
        }

        static void Teleport(PlayerCharacterController player, Vector3 target, float radius)
        {
            var p = NavMesh.SamplePosition(target, out var hit, Mathf.Max(2f, radius), NavMesh.AllAreas) ? hit.position + Vector3.up * .1f : target;
            player.TeleportToCheckpoint(p);
        }
    }
}
