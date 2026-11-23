using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem.Editor.Map01
{
    /// R0 diagnostic (read only): top-down PNG of each room's baked NavMesh (colour = height) with slots (red), entry/exit approaches (green/blue)
    /// and, in white, the area connected to the entry approach. Output: Tools/IronRequiem/Reports/Map01/F2/navmesh/R<NN>.png.
    public static class Map01NavDebug
    {
        const float PxPerM = 6f;

        [MenuItem(Map01Paths.MenuRoot + "Debug/NavMesh top views")]
        public static void All()
        {
            var log = new StringBuilder();
            foreach (var id in Map01Paths.RoomIds) { try { One(id, log); } catch (System.Exception e) { log.AppendLine(id + " EXCEPTION " + e.Message); } }
            Map01Report.Write("navmesh/index.txt", log.ToString());
        }

        static void One(string id, StringBuilder log)
        {
            var room = Map01Json.Room(id);
            var scene = Map01Scenes.OpenRoom(room, false);
            var tri = NavMesh.CalculateTriangulation();
            var o = Map01Math.V3(room.worldOrigin);
            var a = Map01Math.ToBounds(room.aabbLocal);
            int w = Mathf.CeilToInt(a.size.x * PxPerM), h = Mathf.CeilToInt(a.size.z * PxPerM);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = Enumerable.Repeat(new Color(.08f, .08f, .1f), w * h).ToArray();
            // reachable test per triangle centroid from the entry approach
            var entry = room.doors?.FirstOrDefault(d => d.role == "entry");
            Vector3? entryPt = null;
            if (entry != null && NavMesh.SamplePosition(o + Map01Math.V3(entry.center) + Map01Math.V3(entry.normal) * 3f, out var eh, 2f, NavMesh.AllAreas)) entryPt = eh.position;
            var path = new NavMeshPath(); int reach = 0, total = 0;
            for (int t = 0; t < tri.indices.Length; t += 3)
            {
                Vector3 p0 = tri.vertices[tri.indices[t]] - o, p1 = tri.vertices[tri.indices[t + 1]] - o, p2 = tri.vertices[tri.indices[t + 2]] - o;
                if (!a.Contains((p0 + p1 + p2) / 3f) && !a.Contains(p0)) continue;
                total++;
                var cw = (p0 + p1 + p2) / 3f + o; bool ok = false;
                if (entryPt.HasValue && NavMesh.CalculatePath(entryPt.Value, cw, NavMesh.AllAreas, path)) ok = path.status == NavMeshPathStatus.PathComplete;
                if (ok) reach++;
                float hy = Mathf.InverseLerp(a.min.y, a.max.y, (p0.y + p1.y + p2.y) / 3f);
                var col = ok ? Color.Lerp(new Color(.55f, .6f, .55f), Color.white, hy) : Color.Lerp(new Color(.6f, .15f, .15f), new Color(1f, .6f, .2f), hy);
                if (tri.areas[t / 3] == 2) col = Color.Lerp(col, Color.cyan, .5f);
                Fill(px, w, h, a, p0, p1, p2, col);
            }
            void Dot(Vector3 local, Color c, int r) { int cx = (int)((local.x - a.min.x) * PxPerM), cz = (int)((local.z - a.min.z) * PxPerM); for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) { int X = cx + x, Y = cz + y; if (X >= 0 && Y >= 0 && X < w && Y < h) px[Y * w + X] = c; } }
            foreach (var z in room.spawnZones ?? new System.Collections.Generic.List<SpawnZone>()) foreach (var p in StructureAuthoring.SlotPoints(z)) Dot(p, z.kind == "big" ? Color.magenta : Color.red, 3);
            foreach (var d in room.doors ?? new System.Collections.Generic.List<DoorData>()) Dot(Map01Math.V3(d.center) + (d.role == "entry" ? 1 : -1) * Map01Math.V3(d.normal) * 3f, d.role == "entry" ? Color.green : Color.blue, 4);
            tex.SetPixels(px); tex.Apply();
            string file = Map01Paths.ReportRoot + "/navmesh/" + id + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine($"{id}: triangles {total}, reachable from entry {reach} ({(total > 0 ? 100f * reach / total : 0):0}%), scene {scene.name}");
        }

        static void Fill(Color[] px, int w, int h, Bounds a, Vector3 p0, Vector3 p1, Vector3 p2, Color c)
        {
            Vector2 A = new Vector2((p0.x - a.min.x) * PxPerM, (p0.z - a.min.z) * PxPerM), B = new Vector2((p1.x - a.min.x) * PxPerM, (p1.z - a.min.z) * PxPerM), C = new Vector2((p2.x - a.min.x) * PxPerM, (p2.z - a.min.z) * PxPerM);
            int x0 = Mathf.Max(0, (int)Mathf.Min(A.x, Mathf.Min(B.x, C.x))), x1 = Mathf.Min(w - 1, (int)Mathf.Max(A.x, Mathf.Max(B.x, C.x)) + 1);
            int y0 = Mathf.Max(0, (int)Mathf.Min(A.y, Mathf.Min(B.y, C.y))), y1 = Mathf.Min(h - 1, (int)Mathf.Max(A.y, Mathf.Max(B.y, C.y)) + 1);
            float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y); if (Mathf.Abs(area) < 1e-5f) return;
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                var P = new Vector2(x + .5f, y + .5f);
                float w0 = ((B.x - P.x) * (C.y - P.y) - (C.x - P.x) * (B.y - P.y)) / area, w1 = ((C.x - P.x) * (A.y - P.y) - (A.x - P.x) * (C.y - P.y)) / area, w2 = 1 - w0 - w1;
                if (w0 >= 0 && w1 >= 0 && w2 >= 0) px[y * w + x] = c;
            }
        }
    }
}
