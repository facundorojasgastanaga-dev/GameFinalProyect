using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem.Editor.Map01
{
    /// Map01 F2 · Estructura (R1). Validación de datos (antes de construir) y lectura del NavMesh horneado
    /// (después del bake). Espejo del validador Python usado para generar Data/Structure/*.json.
    public static class StructureValidation
    {
        const string Menu = Map01Paths.MenuRoot + "Structure/";
        static readonly HashSet<string> Solid = new HashSet<string> { "solid", "wall", "platform", "coverLow", "coverHigh", "pillar", "rail", "catwalkOnPillars", "ramp", "stair", "cylinder" };
        static readonly HashSet<string> KnownTypes = new HashSet<string> { "solid", "wall", "platform", "coverLow", "coverHigh", "pillar", "rail", "catwalkOnPillars", "ramp", "stair", "cylinder", "pit", "water" };
        static readonly HashSet<string> Rises = new HashSet<string> { "+x", "-x", "+z", "-z" };
        const float Eps = 0.01f;
        const float SlotSnap = 0.75f;      // SafeSpawn de EnemySpawnQueue rechaza más de 0,75 m
        const float ApproachDepth = 4f;    // punto de aproximación: 4 m hacia dentro desde el vano

        [MenuItem(Menu + "Validate all", priority = 30)]
        public static void ValidateAllMenu()
        {
            var log = new StringBuilder();
            log.AppendLine("Map01 Structure · validación · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            int errors;
            try
            {
                var rooms = StructureAuthoring.LoadRooms();
                var chambers = StructureAuthoring.LoadChambers();
                errors = ValidateData(rooms, chambers, log);
                log.AppendLine("--- NavMesh (post-bake) ---");
                foreach (var room in rooms)
                {
                    string path = Map01Paths.ScenePath(room.scene);
                    if (!File.Exists(path)) { log.AppendLine("ERROR " + room.id + ": falta la escena " + path + " (Structure/Build all)"); errors++; continue; }
                    var scene = Map01Scenes.OpenRoom(room, false);
                    var root = Map01Hierarchy.EnsureRoomRoot(scene, room);
                    var layer = Map01Hierarchy.Layer(root, Map01Layers.Structure);
                    errors += ValidateHierarchy(room, layer, log);
                    var surface = layer.GetComponent<NavMeshSurface>();
                    if (!surface || !surface.navMeshData) { log.AppendLine("ERROR " + room.id + ": [Structure] sin NavMeshSurface con datos (Structure/Bake NavMesh all)"); errors++; continue; }
                    surface.RemoveData(); surface.AddData(); // asegura que el NavMesh de esta escena está cargado
                    errors += ValidateNav(room, root, surface.navMeshData, log);
                }
            }
            catch (Exception e)
            {
                log.AppendLine("EXCEPCIÓN: " + e);
                Map01Report.Write("structure_validate.txt", log.ToString());
                throw;
            }
            log.AppendLine(errors == 0 ? "RESULTADO: OK" : "RESULTADO: " + errors + " errores");
            Map01Report.Write("structure_validate.txt", log.ToString());
            if (errors > 0) Debug.LogError("[Map01] Structure: " + errors + " errores de validación. Ver " + Map01Paths.ReportRoot + "/structure_validate.txt");
        }

        // ================================================================== datos
        public static int ValidateData(List<StructureRoom> rooms, List<StructureChamber> chambers, StringBuilder log)
        {
            var errors = new List<string>(); var warnings = new List<string>();
            foreach (var r in rooms) ValidateRoom(r, errors, warnings);
            foreach (var c in chambers) ValidateChamber(c, errors, warnings);
            if (errors.Count == 0) ValidateWorld(rooms, chambers, errors);
            log.AppendLine("--- Datos: " + rooms.Count + " cuartos, " + chambers.Count + " esclusas ---");
            foreach (var e in errors) log.AppendLine("ERROR " + e);
            foreach (var w in warnings) log.AppendLine("AVISO " + w);
            return errors.Count;
        }

        static bool Has3(float[] a) => a != null && a.Length >= 3;
        static bool ValidMinMax(MinMax m) => m != null && Has3(m.min) && Has3(m.max) && m.max[0] > m.min[0] && m.max[1] > m.min[1] && m.max[2] > m.min[2];

        static void ValidateRoom(StructureRoom r, List<string> E, List<string> W)
        {
            string id = r?.id ?? "?";
            if (r == null) { E.Add("cuarto nulo"); return; }
            if (string.IsNullOrEmpty(r.scene)) E.Add(id + ".scene vacío");
            if (!Has3(r.worldOrigin)) E.Add(id + ".worldOrigin no tiene 3 componentes");
            if (r.yaw != 0) E.Add(id + ".yaw = " + r.yaw + " (Map01 usa yaw 0)");
            if (!ValidMinMax(r.interior)) { E.Add(id + ".interior ausente o min ≥ max"); return; }
            if (!ValidMinMax(r.aabbLocal)) { E.Add(id + ".aabbLocal ausente o min ≥ max"); return; }
            Bounds ix = Map01Math.ToBounds(r.interior), ab = Map01Math.ToBounds(r.aabbLocal);
            if (!ab.Contains(ix.min) || !ab.Contains(ix.max)) E.Add(id + ".aabbLocal no contiene interior");
            if (r.ceilings == null || r.ceilings.Count == 0) E.Add(id + ".ceilings vacío");
            else for (int i = 0; i < r.ceilings.Count; i++) if (!ValidMinMax(r.ceilings[i]?.region)) E.Add(id + ".ceilings[" + i + "].region inválida");
            var blocks = r.blocks ?? new List<Block>();

            // bloques
            var ids = new HashSet<string>();
            foreach (var b in blocks)
            {
                string tag = id + ".blocks[" + (b?.id ?? "?") + "]";
                if (b == null || string.IsNullOrEmpty(b.id)) { E.Add(id + ".blocks: bloque sin id"); continue; }
                if (!ids.Add(b.id)) E.Add(tag + ".id duplicado");
                if (!KnownTypes.Contains(b.type ?? "")) E.Add(tag + ".type desconocido '" + b.type + "'");
                if (!Has3(b.center) || !Has3(b.size)) { E.Add(tag + ".center/size sin 3 componentes"); continue; }
                if (b.size[0] <= 0 || b.size[1] <= 0 || b.size[2] <= 0) E.Add(tag + ".size no positivo");
                var bb = StructureAuthoring.BoxOf(b);
                if (bb.min.x < ix.min.x - Eps || bb.max.x > ix.max.x + Eps || bb.min.z < ix.min.z - Eps || bb.max.z > ix.max.z + Eps || bb.max.y > ix.max.y + Eps)
                    E.Add(tag + " fuera del interior (" + bb.min + "…" + bb.max + ")");
                if ((b.type == "ramp" || b.type == "stair") && !Rises.Contains(b.rise ?? "")) E.Add(tag + ".rise debe ser +x, -x, +z o -z");
                if (!string.IsNullOrEmpty(b.material) && !File.Exists("Assets/IronRequiem/Art/Environment/Bunker/" + b.material + ".mat")) W.Add(tag + ".material '" + b.material + "' no existe (se usará Bunker_Wall)");
            }

            // rellano libre de 2 m delante del extremo bajo de cada rampa o escalera (R0 18/11) y pendiente ≤ 30°
            foreach (var b in blocks)
            {
                if (b == null || (b.type != "ramp" && b.type != "stair") || !Rises.Contains(b.rise ?? "") || !Has3(b.center) || !Has3(b.size)) continue;
                string tag = id + ".blocks[" + b.id + "]";
                var bb = StructureAuthoring.BoxOf(b);
                bool alongX = b.rise[1] == 'x'; bool up = b.rise[0] == '+';
                float run = alongX ? bb.size.x : bb.size.z;
                float angle = Mathf.Atan2(bb.size.y, run) * Mathf.Rad2Deg;
                if (angle > 30.05f) W.Add(tag + " pendiente " + angle.ToString("0.0") + "° > 30°");
                float low = alongX ? (up ? bb.min.x : bb.max.x) : (up ? bb.min.z : bb.max.z);
                float l0 = up ? low - 2f : low, l1 = up ? low : low + 2f;
                Vector3 lmin = bb.min, lmax = bb.max;
                if (alongX) { lmin.x = l0; lmax.x = l1; } else { lmin.z = l0; lmax.z = l1; }
                lmin.y = bb.min.y + 0.01f; lmax.y = bb.min.y + 2f;
                if (lmin.x < ix.min.x - Eps || lmax.x > ix.max.x + Eps || lmin.z < ix.min.z - Eps || lmax.z > ix.max.z + Eps) { E.Add(tag + " rellano de 2 m fuera del interior"); continue; }
                var landing = new Bounds(); landing.SetMinMax(lmin, lmax);
                foreach (var o in blocks)
                    if (o != null && o != b && Solid.Contains(o.type ?? "") && Has3(o.center) && Has3(o.size) && Intersects(landing, StructureAuthoring.BoxOf(o)))
                        E.Add(tag + " rellano de 2 m bloqueado por " + o.id);
                if (!SurfaceAt(r, new Vector3(landing.center.x, bb.min.y, landing.center.z))) E.Add(tag + " rellano sin suelo a la cota " + bb.min.y);
            }

            // puertas
            var doors = r.doors ?? new List<DoorData>();
            if (!doors.Any(d => d?.role == "entry")) E.Add(id + ".doors sin puerta role=entry");
            foreach (var d in doors)
            {
                string tag = id + ".doors[" + (d?.id ?? "?") + "]";
                if (d == null || string.IsNullOrEmpty(d.id)) { E.Add(id + ".doors: puerta sin id"); continue; }
                if (!Has3(d.center) || !Has3(d.normal)) { E.Add(tag + ".center/normal sin 3 componentes"); continue; }
                if (d.vano == null || d.vano.Length < 2 || d.vano[0] <= 0 || d.vano[1] <= 0) E.Add(tag + ".vano inválido");
                if (string.IsNullOrEmpty(d.airlock)) E.Add(tag + ".airlock vacío");
                var c = Map01Math.V3(d.center);
                bool w = Mathf.Abs(c.x - ix.min.x) < Eps, e = Mathf.Abs(c.x - ix.max.x) < Eps, s = Mathf.Abs(c.z - ix.min.z) < Eps, n = Mathf.Abs(c.z - ix.max.z) < Eps;
                if (!(w || e || s || n)) { E.Add(tag + ".center " + c + " no está sobre un muro interior"); continue; }
                var probe = new Vector3(c.x + (e ? -0.5f : w ? 0.5f : 0f), c.y, c.z + (n ? -0.5f : s ? 0.5f : 0f));
                if (Mathf.Abs(c.y) > Eps && !SurfaceAt(r, probe)) E.Add(tag + " sin suelo a la cota " + c.y + " delante del vano");
                if (d.vano != null && d.vano.Length >= 2 && c.y + d.vano[1] > ix.max.y + Eps) E.Add(tag + ".vano supera el techo");
            }

            // volúmenes libres frente a sólidos
            foreach (var fv in r.freeVolumes ?? new List<FreeVolume>())
            {
                if (fv == null || !Has3(fv.min) || !Has3(fv.max)) { E.Add(id + ".freeVolumes: volumen sin min/max"); continue; }
                var fb = new Bounds(); fb.SetMinMax(Map01Math.V3(fv.min), Map01Math.V3(fv.max));
                foreach (var b in blocks.Where(b => b != null && Solid.Contains(b.type ?? "") && Has3(b.center) && Has3(b.size)))
                    if (Intersects(fb, StructureAuthoring.BoxOf(b)))
                        (fv.kind == "doorApproach" ? E : W).Add(id + ".freeVolumes[" + fv.id + "] invadido por " + b.id);
            }

            // slots
            Bounds? arena = r.arena?.firstRoom != null ? Map01Math.ToBounds(r.arena.firstRoom) : (Bounds?)null;
            var zoneIds = new HashSet<string>();
            foreach (var z in r.spawnZones ?? new List<SpawnZone>())
            {
                if (z == null || string.IsNullOrEmpty(z.id)) { E.Add(id + ".spawnZones: zona sin id"); continue; }
                if (!zoneIds.Add(z.id)) E.Add(id + ".spawnZones[" + z.id + "] id duplicado");
                bool big = z.kind == "big";
                float rad = big ? 2.5f : 0.8f, h = big ? 4.6f : 2.2f;
                var pts = StructureAuthoring.SlotPoints(z);
                if (z.points != null && z.slots > 0 && z.points.Count != z.slots) W.Add(id + ".spawnZones[" + z.id + "].points = " + z.points.Count + " ≠ slots " + z.slots);
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i]; string tag = id + "." + z.id + "_" + (i + 1);
                    if (arena.HasValue && !Inside(arena.Value, p, 1e-4f)) E.Add(tag + " fuera de arena.firstRoom " + p);
                    if (!SurfaceAt(r, p)) E.Add(tag + " sin superficie pisable a la cota " + p.y);
                    foreach (var b in blocks.Where(b => b != null && Solid.Contains(b.type ?? "") && Has3(b.center) && Has3(b.size)))
                        if (CylinderHits(p, rad, h, b)) E.Add(tag + " choca con " + b.id);
                    foreach (var fv in (r.freeVolumes ?? new List<FreeVolume>()).Where(f => f != null && f.kind == "doorApproach" && Has3(f.min) && Has3(f.max)))
                        if (p.x > fv.min[0] - rad && p.x < fv.max[0] + rad && p.z > fv.min[2] - rad && p.z < fv.max[2] + rad && p.y >= fv.min[1] - 0.1f && p.y <= fv.max[1])
                            E.Add(tag + " dentro de " + fv.id);
                }
                for (int a = 0; a < pts.Count; a++)
                    for (int b = a + 1; b < pts.Count; b++)
                        if (Vector3.Distance(pts[a], pts[b]) < (big ? 5.5f : 2f) - 1e-4f) E.Add(id + "." + z.id + " slots " + (a + 1) + " y " + (b + 1) + " a " + Vector3.Distance(pts[a], pts[b]).ToString("0.00") + " m");
            }

            // anclajes
            foreach (var a in r.lightAnchors ?? new List<LightAnchor>())
            {
                if (a == null || string.IsNullOrEmpty(a.id) || !Has3(a.pos)) { E.Add(id + ".lightAnchors: anclaje sin id/pos"); continue; }
                if (!a.id.StartsWith(id + ".", StringComparison.Ordinal)) E.Add(id + ".lightAnchors[" + a.id + "] no empieza por " + id + ".");
                var p = Map01Math.V3(a.pos);
                if (!Inside(ix, p, Eps)) E.Add(a.id + " fuera del interior " + p);
                foreach (var fv in (r.freeVolumes ?? new List<FreeVolume>()).Where(f => f != null && f.kind == "doorApproach" && Has3(f.min) && Has3(f.max)))
                {
                    var fb = new Bounds(); fb.SetMinMax(Map01Math.V3(fv.min), Map01Math.V3(fv.max));
                    if (Inside(fb, p, -1e-4f)) E.Add(a.id + " dentro de " + fv.id);
                }
            }

            // otros volúmenes
            if (r.release?.volume != null && !ValidMinMax(r.release.volume)) E.Add(id + ".release.volume inválido");
            if (r.landmark != null && !ValidMinMax(new MinMax { min = r.landmark.min, max = r.landmark.max })) E.Add(id + ".landmark inválido");
            var jumps = r.nav?.jumpAreaVolumes ?? new List<MinMax>();
            for (int i = 0; i < jumps.Count; i++) if (!ValidMinMax(jumps[i])) E.Add(id + ".nav.jumpAreaVolumes[" + i + "] inválido");
            foreach (var d in r.internalDoors ?? new List<InternalDoor>())
                if (d == null || string.IsNullOrEmpty(d.id) || !Has3(d.center) || !Has3(d.normal)) E.Add(id + ".internalDoors: puerta sin id/center/normal");
        }

        static void ValidateChamber(StructureChamber c, List<string> E, List<string> W)
        {
            if (c == null) { E.Add("esclusa nula"); return; }
            string id = c.id ?? "?";
            if (!Has3(c.worldCenter)) E.Add(id + ".worldCenter sin 3 componentes");
            if (!Has3(c.aabbSize) || !Has3(c.chamberInterior)) { E.Add(id + ".aabbSize/chamberInterior sin 3 componentes"); return; }
            if (c.doorClass != "Z" && c.doorClass != "A") E.Add(id + ".class debe ser Z o A");
            if (!new[] { "+x", "-x", "+z", "-z" }.Contains(c.axis)) E.Add(id + ".axis debe ser ±x o ±z");
            for (int i = 0; i < 3; i++) if (c.chamberInterior[i] <= 0 || c.aabbSize[i] < c.chamberInterior[i]) E.Add(id + ".chamberInterior[" + i + "] no cabe en aabbSize");
            if (c.destinationDoor?.center == null) E.Add(id + ".destinationDoor ausente");
            if (id != "S00" && c.sourceDoor?.center == null) E.Add(id + ".sourceDoor ausente");
            if (id != "S00" && c.trigger?.center == null) E.Add(id + ".trigger ausente");
            if (Map01Paths.SessionStartChamber.Contains(id) && c.sessionStart?.playerStart == null) E.Add(id + ".sessionStart.playerStart ausente (esclusa de arranque de sesión)");
            if (!Has3(c.worldCenter)) return;
            var wc = Map01Math.V3(c.worldCenter);
            bool alongZ = (c.axis ?? "").EndsWith("z", StringComparison.Ordinal);
            float half = (alongZ ? c.aabbSize[2] : c.aabbSize[0]) / 2f;
            foreach (var pair in new[] { ("sourceDoor", c.sourceDoor), ("destinationDoor", c.destinationDoor) })
            {
                if (pair.Item2?.center == null) continue;
                var local = Map01Math.V3(pair.Item2.center) - wc;
                float along = alongZ ? local.z : local.x;
                if (Mathf.Abs(Mathf.Abs(along) - half) > 0.02f) E.Add(id + "." + pair.Item1 + ".center no está en un extremo del AABB (a " + along.ToString("0.00") + " de " + half.ToString("0.00") + ")");
            }
            if (c.trigger?.center != null)
            {
                var tb = Map01Math.ToBounds(c.trigger);
                var ib = new Bounds(wc + Vector3.up * (c.chamberInterior[1] / 2f), Map01Math.V3(c.chamberInterior));
                if (!Inside(ib, tb.min, 0.02f) || !Inside(ib, tb.max, 0.02f)) E.Add(id + ".trigger sobresale de chamberInterior");
            }
        }

        static void ValidateWorld(List<StructureRoom> rooms, List<StructureChamber> chambers, List<string> E)
        {
            var boxes = new List<(string id, Bounds b)>();
            foreach (var r in rooms) boxes.Add((r.id, Map01Math.ToWorld(Map01Math.ToBounds(r.aabbLocal), r)));
            foreach (var c in chambers) boxes.Add((c.id, ChamberWorldBounds(c)));
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    if (Intersects(boxes[i].b, boxes[j].b, 0.001f)) E.Add("solape de mundo " + boxes[i].id + " – " + boxes[j].id);

            var byId = chambers.ToDictionary(c => c.id);
            foreach (var r in rooms)
                foreach (var d in r.doors ?? new List<DoorData>())
                {
                    if (d?.airlock == null || !byId.TryGetValue(d.airlock, out var c)) { E.Add(r.id + ".doors[" + d?.id + "].airlock '" + d?.airlock + "' no existe"); continue; }
                    var target = d.role == "entry" ? c.destinationDoor : c.sourceDoor;
                    var w = Map01Math.V3(d.center) + Map01Math.V3(r.worldOrigin);
                    var n = Map01Math.V3(d.normal);
                    var outer = d.role == "exit" ? w + n : w - n; // cara exterior del muro de 1 m
                    if (target?.center == null || Vector3.Distance(outer, Map01Math.V3(target.center)) > 0.02f)
                        E.Add(r.id + ".doors[" + d.id + "] no coincide con " + c.id + (d.role == "entry" ? ".destinationDoor" : ".sourceDoor") + " (" + outer + ")");
                }
        }

        public static Bounds ChamberWorldBounds(StructureChamber c)
        {
            var wc = Map01Math.V3(c.worldCenter); var s = Map01Math.V3(c.aabbSize);
            var b = new Bounds(); b.SetMinMax(new Vector3(wc.x - s.x / 2f, wc.y - 0.5f, wc.z - s.z / 2f), new Vector3(wc.x + s.x / 2f, wc.y + s.y - 0.5f, wc.z + s.z / 2f));
            return b;
        }

        /// ¿Hay suelo pisable a la cota p.y en (x,z)? Suelo 0 fuera de fosos, o cara superior de un bloque macizo.
        static bool SurfaceAt(StructureRoom r, Vector3 p)
        {
            var ix = Map01Math.ToBounds(r.interior);
            if (p.x < ix.min.x || p.x > ix.max.x || p.z < ix.min.z || p.z > ix.max.z) return false;
            var blocks = r.blocks ?? new List<Block>();
            if (Mathf.Abs(p.y) < Eps && !blocks.Any(b => b != null && b.type == "pit" && Has3(b.center) && Has3(b.size) && InsideXZ(StructureAuthoring.BoxOf(b), p, 0f))) return true;
            foreach (var b in blocks)
            {
                if (b == null || !Has3(b.center) || !Has3(b.size)) continue;
                if (b.type != "solid" && b.type != "platform" && b.type != "catwalkOnPillars" && b.type != "cylinder") continue;
                var bb = StructureAuthoring.BoxOf(b);
                if (InsideXZ(bb, p, 1e-4f) && Mathf.Abs(bb.max.y - p.y) < Eps) return true;
            }
            return false;
        }

        static bool CylinderHits(Vector3 p, float rad, float h, Block b)
        {
            var bb = StructureAuthoring.BoxOf(b);
            if (!(p.y + 0.08f < bb.max.y - Eps && bb.min.y < p.y + h)) return false;
            if (b.type == "cylinder")
            {
                float rr = b.size[0] / 2f;
                return new Vector2(p.x - b.center[0], p.z - b.center[2]).magnitude < rr + rad;
            }
            float dx = Mathf.Max(bb.min.x - p.x, 0f, p.x - bb.max.x), dz = Mathf.Max(bb.min.z - p.z, 0f, p.z - bb.max.z);
            return dx * dx + dz * dz < rad * rad + 1e-3f; // tocar cuenta como choque (R01.G2_2 a 0,80 m exactos de mezzRail)
        }

        static bool Intersects(Bounds a, Bounds b, float eps = Eps) =>
            a.min.x < b.max.x - eps && b.min.x < a.max.x - eps && a.min.y < b.max.y - eps && b.min.y < a.max.y - eps && a.min.z < b.max.z - eps && b.min.z < a.max.z - eps;
        static bool Inside(Bounds b, Vector3 p, float tol) =>
            p.x >= b.min.x - tol && p.x <= b.max.x + tol && p.y >= b.min.y - tol && p.y <= b.max.y + tol && p.z >= b.min.z - tol && p.z <= b.max.z + tol;
        static bool InsideXZ(Bounds b, Vector3 p, float tol) => p.x >= b.min.x - tol && p.x <= b.max.x + tol && p.z >= b.min.z - tol && p.z <= b.max.z + tol;

        // ================================================================== jerarquía construida
        static int ValidateHierarchy(StructureRoom room, Transform layer, StringBuilder log)
        {
            int errors = 0;
            if (!layer.Find(StructureAuthoring.GeometryName)) { log.AppendLine("ERROR " + room.id + ": falta [Structure]/Geometry"); errors++; }
            foreach (var z in room.spawnZones ?? new List<SpawnZone>())
            {
                int expected = StructureAuthoring.SlotPoints(z).Count, found = Map01Markers.SlotsOf(layer, z.id).Length;
                if (found != expected) { log.AppendLine("ERROR " + room.id + ": Markers/Slots/" + z.id + " tiene " + found + " de " + expected); errors++; }
            }
            foreach (var a in room.lightAnchors ?? new List<LightAnchor>())
                if (!Map01Markers.Find(layer, Map01Markers.Anchors, a.id)) { log.AppendLine("ERROR " + room.id + ": falta Markers/Anchors/" + a.id); errors++; }
            foreach (var d in room.doors ?? new List<DoorData>())
                if (!Map01Markers.Find(layer, Map01Markers.Openings, d.id)) { log.AppendLine("ERROR " + room.id + ": falta Markers/Openings/" + d.id); errors++; }
            return errors;
        }

        // ================================================================== NavMesh horneado
        /// Lee el NavMesh cargado de la escena del cuarto: cada slot a ≤ 0,75 m, camino completo a la aproximación de la
        /// puerta de entrada y al centro de la arena; entrada → salida; botiquines de Encounters (avisos).
        public static int ValidateNav(StructureRoom room, Transform root, NavMeshData data, StringBuilder log)
        {
            var E = new List<string>(); var W = new List<string>();
            string id = room.id;
            if (!data) E.Add(id + ": sin NavMeshData");
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0) E.Add(id + ": NavMesh vacío o no cargado");
            else
            {
                int jump = NavMesh.GetAreaFromName("Jump");
                int all = NavMesh.AllAreas, noJump = jump >= 0 ? all & ~(1 << jump) : all;
                bool gapRoom = (room.hazards ?? new List<Hazard>()).Any(h => h != null && h.type == "gap");
                var path = new NavMeshPath();

                var entry = (room.doors ?? new List<DoorData>()).FirstOrDefault(d => d.role == "entry");
                var exit = (room.doors ?? new List<DoorData>()).FirstOrDefault(d => d.role == "exit");
                Vector3? entryPt = entry != null ? Snap(root.TransformPoint(Map01Math.V3(entry.center) + Map01Math.V3(entry.normal) * ApproachDepth), 2f, all) : null;
                Vector3? exitPt = exit != null ? Snap(root.TransformPoint(Map01Math.V3(exit.center) - Map01Math.V3(exit.normal) * ApproachDepth), 2f, all) : null;
                if (entry != null && !entryPt.HasValue) E.Add(id + ": sin NavMesh en la aproximación de " + entry.id);
                if (exit != null && !exitPt.HasValue) E.Add(id + ": sin NavMesh en la aproximación de " + exit.id);

                Vector3? arenaPt = null;
                if (room.arena?.firstRoom != null)
                {
                    var ab = Map01Math.ToBounds(room.arena.firstRoom);
                    // [R0 fix 18/11] Snap at FLOOR level (the mid-height center used to land on roofs of solids: unreachable islands),
                    // then prefer a floor point connected to the entry approach, scanning the arena on a 4 m grid.
                    arenaPt = Snap(root.TransformPoint(new Vector3(ab.center.x, ab.min.y + 0.5f, ab.center.z)), 3f, all);
                    if (entryPt.HasValue && (!arenaPt.HasValue || !Complete(arenaPt.Value, entryPt.Value, all, path)))
                    {
                        Vector3? best = null; float bestD = float.MaxValue;
                        for (float gx = ab.min.x + 2f; gx < ab.max.x; gx += 4f)
                            for (float gz = ab.min.z + 2f; gz < ab.max.z; gz += 4f)
                            {
                                var cand = Snap(root.TransformPoint(new Vector3(gx, ab.min.y + 0.5f, gz)), 1.5f, all);
                                if (!cand.HasValue || !Complete(cand.Value, entryPt.Value, all, path)) continue;
                                float d = (new Vector2(gx, gz) - new Vector2(ab.center.x, ab.center.z)).sqrMagnitude;
                                if (d < bestD) { bestD = d; best = cand; }
                            }
                        if (best.HasValue) arenaPt = best;
                    }
                    if (!arenaPt.HasValue) arenaPt = Snap(root.TransformPoint(new Vector3(ab.center.x, ab.min.y, ab.center.z)), 10f, all);
                    if (!arenaPt.HasValue) E.Add(id + ": sin NavMesh cerca del centro de la arena");
                }

                if (entryPt.HasValue && exitPt.HasValue && !Complete(entryPt.Value, exitPt.Value, all, path))
                    (gapRoom ? W : E).Add(id + ": camino entrada → salida " + path.status + (gapRoom ? " (cuarto con grietas: se cruza saltando)" : ""));

                foreach (var z in room.spawnZones ?? new List<SpawnZone>())
                {
                    bool big = z.kind == "big";
                    bool unused = z.rounds == "conceptB" || z.rounds == "conceptC"; // jefe A elegido: B/C quedan como reserva
                    int mask = big ? noJump : all;
                    var pts = StructureAuthoring.SlotPoints(z);
                    for (int i = 0; i < pts.Count; i++)
                    {
                        string tag = id + "." + z.id + "_" + (i + 1);
                        var list = unused ? W : E;
                        var hit = Snap(root.TransformPoint(pts[i]), SlotSnap, mask);
                        if (!hit.HasValue) { list.Add(tag + ": a más de " + SlotSnap + " m del NavMesh" + (big ? " (máscara sin Jump)" : "")); continue; }
                        if (entryPt.HasValue && !Complete(hit.Value, entryPt.Value, mask, path))
                        {
                            var status = path.status;
                            // Entrada en plataforma a la que solo se sube por rampas Jump (R08 deck, R16 galería): con la máscara
                            // sin Jump del Big Bot no se llega, y es lo esperado si con todas las áreas sí (R0 18/11).
                            bool jumpEntry = big && Complete(hit.Value, entryPt.Value, all, path);
                            (gapRoom || unused || jumpEntry ? W : E).Add(tag + ": camino a la entrada " + status + (jumpEntry ? " (solo por áreas Jump; esperado con la máscara sin Jump)" : ""));
                        }
                        if (arenaPt.HasValue && !Complete(hit.Value, arenaPt.Value, mask, path)) list.Add(tag + ": camino al centro de la arena " + path.status);
                    }
                }

                // Botiquines (datos de R3, coordenadas locales): solo avisos.
                string encPath = Map01Paths.Data("Encounters", id);
                if (File.Exists(encPath))
                {
                    try
                    {
                        var health = JObject.Parse(File.ReadAllText(encPath))["health"] as JArray;
                        foreach (var h in health ?? new JArray())
                        {
                            var pos = (h["pos"] as JArray)?.Select(v => (float)v).ToArray();
                            if (!Has3(pos)) continue;
                            string tag = id + ".health[" + (string)h["id"] + "]";
                            var hit = Snap(root.TransformPoint(Map01Math.V3(pos)), SlotSnap, all);
                            if (!hit.HasValue) { W.Add(tag + ": a más de " + SlotSnap + " m del NavMesh"); continue; }
                            if (entryPt.HasValue && !Complete(hit.Value, entryPt.Value, all, path)) W.Add(tag + ": camino a la entrada " + path.status);
                        }
                    }
                    catch (Exception e) { W.Add(id + ": no se pudo leer " + encPath + " (" + e.Message + ")"); }
                }
            }
            foreach (var e in E) log.AppendLine("ERROR " + e);
            foreach (var w in W) log.AppendLine("AVISO " + w);
            log.AppendLine(id + ": NavMesh " + (E.Count == 0 ? "OK" : E.Count + " errores") + (W.Count > 0 ? ", " + W.Count + " avisos" : ""));
            return E.Count;
        }

        static Vector3? Snap(Vector3 p, float radius, int mask) => NavMesh.SamplePosition(p, out var hit, radius, mask) ? hit.position : (Vector3?)null;

        static bool Complete(Vector3 a, Vector3 b, int mask, NavMeshPath path) =>
            NavMesh.CalculatePath(a, b, mask, path) && path.status == NavMeshPathStatus.PathComplete;
    }
}
