using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem.Editor.Map01
{
    /// R0 diagnostic (read only): replays CombatTwoRoomsLab.SafeSpawn's NavMesh + capsule checks (CombatTwoRoomsLab.cs:117-128)
    /// on every spawn transform and every FixedPoint of every room, for each prefab used in that room, with ALL layers loaded.
    /// Lists the colliders that would block a spawn. Output: Tools/IronRequiem/Reports/Map01/F2/spawn_debug.txt
    public static class Map01SpawnDebug
    {
        [MenuItem(Map01Paths.MenuRoot + "Debug/Spawn capsule check")]
        public static void Run()
        {
            var log = new StringBuilder(); int bad = 0;
            var blockers = new Collider[64];
            foreach (var id in Map01Paths.RoomIds)
            {
                var room = Map01Json.Room(id);
                Map01Scenes.OpenRoom(room, false);
                Physics.SyncTransforms();
                var lab = Object.FindAnyObjectByType<CombatTwoRoomsLab>(FindObjectsInactive.Include);
                if (!lab) { log.AppendLine(id + ": sin rondas"); continue; }
                var so = new SerializedObject(lab);
                var pts = new System.Collections.Generic.List<Transform>();
                void AddArr(string f) { var p = so.FindProperty(f); for (int i = 0; p != null && i < p.arraySize; i++) if (p.GetArrayElementAtIndex(i).objectReferenceValue is Transform t) pts.Add(t); }
                AddArr("firstRoomSpawns"); AddArr("secondRoomSpawns");
                var prefabs = new System.Collections.Generic.HashSet<GameObject>(); var fixedPts = new System.Collections.Generic.List<(Transform, GameObject)>();
                var waves = so.FindProperty("waves");
                for (int w = 0; w < waves.arraySize; w++)
                {
                    var wave = waves.GetArrayElementAtIndex(w);
                    void Groups(SerializedProperty gs) { for (int g = 0; gs != null && g < gs.arraySize; g++) { var gp = gs.GetArrayElementAtIndex(g); var pf = gp.FindPropertyRelative("Prefab").objectReferenceValue as GameObject; var fx = gp.FindPropertyRelative("FixedPoint").objectReferenceValue as Transform; if (pf) prefabs.Add(pf); if (pf && fx) fixedPts.Add((fx, pf)); } }
                    Groups(wave.FindPropertyRelative("Groups"));
                    var vars = wave.FindPropertyRelative("Variations");
                    for (int v = 0; vars != null && v < vars.arraySize; v++) Groups(vars.GetArrayElementAtIndex(v).FindPropertyRelative("Groups"));
                }
                int roomBad = 0;
                void Check(Transform t, GameObject prefab, bool isFixed)
                {
                    var agent = prefab.GetComponentInChildren<NavMeshAgent>(true);
                    float radius = agent ? Mathf.Max(.35f, agent.radius) : .75f, height = agent ? Mathf.Max(radius * 2, agent.height) : 2;
                    int mask = agent ? agent.areaMask : NavMesh.AllAreas;
                    string tag = $"{id} {(isFixed ? "FIXED " : "")}{t.name} [{prefab.name} r{radius:0.##}]";
                    if (!NavMesh.SamplePosition(t.position, out var hit, .75f, mask)) { log.AppendLine("  NO-NAVMESH " + tag); roomBad++; return; }
                    var pos = hit.position;
                    Vector3 bottom = pos + Vector3.up * (radius + .08f), top = pos + Vector3.up * (height - radius + .08f);
                    int n = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, blockers, -1, QueryTriggerInteraction.Ignore);
                    var hits = Enumerable.Range(0, n).Select(i => blockers[i]).Where(b => b.bounds.max.y > pos.y + .08f).ToArray();
                    if (hits.Length > 0) { roomBad++; log.AppendLine("  BLOQUEADO " + tag + " por " + string.Join(", ", hits.Take(4).Select(b => PathOf(b.transform)))); }
                }
                // big prefab on every BS* slot, the smallest common prefab on the rest
                var big = prefabs.FirstOrDefault(p => { var a = p.GetComponentInChildren<NavMeshAgent>(true); return a && a.radius > 1.5f; });
                var small = prefabs.Where(p => p != big).FirstOrDefault();
                foreach (var t in pts.Distinct()) { if (t.name.StartsWith("BS") && big) Check(t, big, false); else if (small) Check(t, small, false); }
                foreach (var (t, pf) in fixedPts.Distinct()) Check(t, pf, true);
                log.AppendLine($"{id}: {pts.Distinct().Count()} spawns, {fixedPts.Count} fijos, {roomBad} con problema");
                bad += roomBad;
            }
            log.Insert(0, "Spawn capsule check · total con problema: " + bad + "\n");
            Map01Report.Write("spawn_debug.txt", log.ToString());
        }

        static string PathOf(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
    }
}
