using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem.Editor.Map01
{
    /// Map01 F2 · Estructura (R1). Greybox de los 18 cuartos y de las esclusas S00/C01…C17 a partir de
    /// Assets/IronRequiem/Levels/Map01/Data/Structure/*.json (modelo StructureRoom / StructureChamber de Map01Core.cs).
    /// Idempotente: cada pasada vacía solo la capa [Structure] (ClearLayer) y la reconstruye con Undo.
    /// Spec: Docs/IronRequiem/Map01/01_ESTRUCTURA.md y F2_PLAN.md.
    public static class StructureAuthoring
    {
        const string Menu = Map01Paths.MenuRoot + "Structure/";
        const string MaterialFolder = "Assets/IronRequiem/Art/Environment/Bunker/";
        public const string NavFolder = Map01Paths.GeneratedRoot + "/NavMesh";
        public const string GeometryName = "Geometry";
        const float MaxTile = 10f;
        const int JumpArea = 2; // área integrada «Jump» (ProjectSettings/NavMeshAreas.asset)
        const int NotWalkableArea = 1; // área integrada «Not Walkable»
        const StaticEditorFlags SolidFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic; // el NavMesh se hornea desde colliders, no desde flags
        const StaticEditorFlags SoftFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly List<string> materialWarnings = new List<string>();

        // ------------------------------------------------------------------ menús
        [MenuItem(Menu + "Build all", priority = 10)]
        public static void BuildAllMenu() => Run("structure_build.txt", BuildAll);

        [MenuItem(Menu + "Bake NavMesh all", priority = 20)]
        public static void BakeAllMenu() => Run("structure_navmesh.txt", BakeAll);

        static void Run(string report, Func<StringBuilder, int> body)
        {
            var log = new StringBuilder();
            log.AppendLine("Map01 Structure · " + report + " · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            int errors;
            try { errors = body(log); }
            catch (Exception e)
            {
                log.AppendLine("EXCEPCIÓN: " + e);
                Map01Report.Write(report, log.ToString());
                Debug.LogException(e);
                throw;
            }
            log.AppendLine(errors == 0 ? "RESULTADO: OK" : "RESULTADO: " + errors + " errores");
            Map01Report.Write(report, log.ToString());
            if (errors > 0) Debug.LogError("[Map01] Structure " + report + ": " + errors + " errores. Ver " + Map01Paths.ReportRoot + "/" + report);
        }

        // ------------------------------------------------------------------ carga de datos
        public static List<StructureRoom> LoadRooms() => Map01Paths.RoomIds.Select(id => Map01Json.Room(id)).ToList();
        public static List<StructureChamber> LoadChambers() => Map01Paths.ChamberIds.Select(id => Map01Json.Chamber(id)).ToList();

        // ------------------------------------------------------------------ Build all
        public static int BuildAll(StringBuilder log)
        {
            var rooms = LoadRooms();
            var chambers = LoadChambers();
            int dataErrors = StructureValidation.ValidateData(rooms, chambers, log);
            if (dataErrors > 0)
                throw new InvalidOperationException("Map01 Structure: " + dataErrors + " errores en los datos; no se construye nada. Ver " + Map01Paths.ReportRoot + "/structure_build.txt");

            materials.Clear(); materialWarnings.Clear();
            foreach (var room in rooms)
            {
                var scene = Map01Scenes.OpenRoom(room, true);
                var root = Map01Hierarchy.EnsureRoomRoot(scene, room);
                var layer = Map01Hierarchy.Layer(root, Map01Layers.Structure);
                Map01Hierarchy.ClearLayer(layer);
                int pieces = BuildRoom(room, layer);
                Map01Scenes.Save(scene);
                log.AppendLine($"{room.id} {room.scene}: {pieces} piezas, {room.spawnZones?.Sum(z => SlotPoints(z).Count) ?? 0} slots, {room.lightAnchors?.Count ?? 0} anclajes");
            }

            for (int s = 0; s < Map01Paths.SessionScenes.Length; s++)
            {
                var inSession = chambers.Where(c => Map01Paths.SessionsOfChamber(c.id).Contains(s)).ToList();
                if (inSession.Count == 0) continue;
                string path = Map01Paths.ScenePath(Map01Paths.SessionScenes[s]);
                if (!File.Exists(path))
                {
                    log.AppendLine($"AVISO: falta la sesión {path} (la crea R4 en el paso 1). Se saltan: {string.Join(", ", inSession.Select(c => c.id))}");
                    continue;
                }
                var scene = Map01Scenes.OpenSession(s);
                foreach (var c in inSession)
                {
                    var root = Map01Hierarchy.EnsureChamberRoot(scene, c);
                    var layer = Map01Hierarchy.Layer(root, Map01Layers.Structure);
                    Map01Hierarchy.ClearLayer(layer);
                    int pieces = BuildChamber(c, layer);
                    log.AppendLine($"{c.id} en {Map01Paths.SessionScenes[s]}: {pieces} piezas");
                }
                Map01Scenes.Save(scene);
            }
            foreach (var w in materialWarnings) log.AppendLine("AVISO: " + w);
            return 0;
        }

        // ------------------------------------------------------------------ cuarto
        static int BuildRoom(StructureRoom room, Transform layer)
        {
            int before = CountChildren(layer);
            var geo = Child(layer, GeometryName);
            BuildEnvelope(room, geo);
            foreach (var b in room.blocks ?? new List<Block>()) BuildBlock(b, geo);
            BuildRoomMarkers(room, Child(layer, Map01Markers.Root));
            return CountChildren(layer) - before;
        }

        static void BuildEnvelope(StructureRoom room, Transform parent)
        {
            var env = Child(parent, "Envelope");
            Vector3 imin = Map01Math.V3(room.interior.min), imax = Map01Math.V3(room.interior.max);
            Vector3 amin = Map01Math.V3(room.aabbLocal.min), amax = Map01Math.V3(room.aabbLocal.max);

            // Suelo principal (y −1…0) menos las huellas de los fosos.
            var floor = new List<Rect2> { new Rect2(imin.x, imin.z, imax.x, imax.z) };
            foreach (var pit in (room.blocks ?? new List<Block>()).Where(b => b.type == "pit"))
            {
                var bb = BoxOf(pit);
                floor = Rect2.Subtract(floor, new Rect2(bb.min.x, bb.min.z, bb.max.x, bb.max.z));
            }
            for (int i = 0; i < floor.Count; i++)
                AddBox(env, "Floor_" + i, new Vector3(floor[i].x0, -1f, floor[i].z0), new Vector3(floor[i].x1, 0f, floor[i].z1), "Bunker_Floor", true, true, SolidFlags);

            // Muros de 1 m con los vanos de las puertas (solo el vano; la banda del marco es de la sesión, R4).
            var doors = room.doors ?? new List<DoorData>();
            WallWithHoles(env, "Wall_S", Axis.Z, imin.z, amin.z, imin.z, amin.x, amax.x, amin.y, amax.y, doors);
            WallWithHoles(env, "Wall_N", Axis.Z, imax.z, imax.z, amax.z, amin.x, amax.x, amin.y, amax.y, doors);
            WallWithHoles(env, "Wall_W", Axis.X, imin.x, amin.x, imin.x, imin.z, imax.z, amin.y, amax.y, doors);
            WallWithHoles(env, "Wall_E", Axis.X, imax.x, imax.x, amax.x, imin.z, imax.z, amin.y, amax.y, doors);

            // Techos (el más alto llega hasta el AABB; los intermedios miden 1 m).
            var ceilings = room.ceilings ?? new List<Ceiling>();
            for (int i = 0; i < ceilings.Count; i++)
            {
                var c = ceilings[i]; var r = Map01Math.ToBounds(c.region);
                float top = c.y >= imax.y - 0.01f ? amax.y : c.y + 1f;
                AddBox(env, "Ceiling_" + i, new Vector3(r.min.x, c.y, r.min.z), new Vector3(r.max.x, top, r.max.z), "Bunker_Ceiling", true, true, SolidFlags);
            }
        }

        enum Axis { X, Z }

        /// Muro perpendicular a `axis` en la franja [t0,t1] del eje; se extiende en [s0,s1] del otro eje horizontal y en [y0,y1].
        static void WallWithHoles(Transform parent, string name, Axis axis, float plane, float t0, float t1, float s0, float s1, float y0, float y1, List<DoorData> doors)
        {
            var rects = new List<Rect2> { new Rect2(s0, y0, s1, y1) };
            foreach (var d in doors)
            {
                var c = Map01Math.V3(d.center);
                float onPlane = axis == Axis.Z ? c.z : c.x;
                if (Mathf.Abs(onPlane - plane) > 0.05f) continue;
                var vano = Map01Math.V2(d.vano);
                float lateral = axis == Axis.Z ? c.x : c.z;
                rects = Rect2.Subtract(rects, new Rect2(lateral - vano.x / 2f, c.y, lateral + vano.x / 2f, c.y + vano.y));
            }
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                Vector3 min = axis == Axis.Z ? new Vector3(r.x0, r.z0, t0) : new Vector3(t0, r.z0, r.x0);
                Vector3 max = axis == Axis.Z ? new Vector3(r.x1, r.z1, t1) : new Vector3(t1, r.z1, r.x1);
                AddBox(parent, name + "_" + i, min, max, "Bunker_Wall", true, true, SolidFlags);
            }
        }

        static void BuildBlock(Block b, Transform parent)
        {
            string mat = string.IsNullOrEmpty(b.material) ? DefaultMaterial(b.type) : b.material;
            var box = BoxOf(b);
            switch (b.type)
            {
                case "pit": BuildPit(b, parent); break;
                case "ramp": BuildRamp(b, parent, mat, 0.5f, true); break;
                case "stair": BuildRamp(b, parent, mat, 0.25f, false); break;
                case "cylinder": BuildCylinder(b, parent, mat); break;
                case "water": AddBox(parent, b.id, box.min, box.max, mat, false, true, SoftFlags); break;
                default: AddBox(parent, b.id, box.min, box.max, mat, b.collision != "none", true, SolidFlags); break;
            }
        }

        /// Foso real: sin suelo (lo resta la envolvente), fondo a `depth` y revestimiento de 0,5 m en los cuatro lados.
        static void BuildPit(Block b, Transform parent)
        {
            var box = BoxOf(b); float depth = box.min.y;
            var g = Child(parent, b.id);
            AddBox(g, "Bottom", new Vector3(box.min.x, depth - 1f, box.min.z), new Vector3(box.max.x, depth, box.max.z), "Bunker_Floor", true, true, SolidFlags);
            const float t = 0.5f;
            AddBox(g, "LinerS", new Vector3(box.min.x, depth, box.min.z), new Vector3(box.max.x, 0f, box.min.z + t), "Bunker_Wall", true, true, SolidFlags);
            AddBox(g, "LinerN", new Vector3(box.min.x, depth, box.max.z - t), new Vector3(box.max.x, 0f, box.max.z), "Bunker_Wall", true, true, SolidFlags);
            AddBox(g, "LinerW", new Vector3(box.min.x, depth, box.min.z + t), new Vector3(box.min.x + t, 0f, box.max.z - t), "Bunker_Wall", true, true, SolidFlags);
            AddBox(g, "LinerE", new Vector3(box.max.x - t, depth, box.min.z + t), new Vector3(box.max.x, 0f, box.max.z - t), "Bunker_Wall", true, true, SolidFlags);
        }

        /// Rampa o escalera: relleno macizo escalonado desde el suelo local (y 0) y una losa inclinada encima
        /// (visible en "ramp", invisible en "stair": patrón V35_StairRamp). `rise` = sentido en que sube.
        static void BuildRamp(Block b, Transform parent, string mat, float stepHeight, bool slabVisible)
        {
            var c = Map01Math.V3(b.center); var s = Map01Math.V3(b.size);
            float h0 = c.y - s.y / 2f, h1 = c.y + s.y / 2f, dh = h1 - h0;
            string rise = b.rise ?? "";
            bool alongZ = rise.EndsWith("z", StringComparison.Ordinal);
            int sign = rise.StartsWith("-", StringComparison.Ordinal) ? -1 : 1;
            float length = alongZ ? s.z : s.x, width = alongZ ? s.x : s.z;
            float lowEnd = alongZ ? (sign > 0 ? c.z - s.z / 2f : c.z + s.z / 2f) : (sign > 0 ? c.x - s.x / 2f : c.x + s.x / 2f);
            var g = Child(parent, b.id);

            int k = Mathf.Max(1, Mathf.CeilToInt(dh / stepHeight - 1e-4f));
            for (int i = 0; i < k; i++)
            {
                float top = h0 + dh * i / k; // cara superior bajo la línea de la rampa
                if (top <= 0.01f) continue;
                float p0 = lowEnd + sign * length * i / k, p1 = lowEnd + sign * length * (i + 1) / k;
                float lo = Mathf.Min(p0, p1), hi = Mathf.Max(p0, p1);
                Vector3 min = alongZ ? new Vector3(c.x - width / 2f, 0f, lo) : new Vector3(lo, 0f, c.z - width / 2f);
                Vector3 max = alongZ ? new Vector3(c.x + width / 2f, top, hi) : new Vector3(hi, top, c.z + width / 2f);
                AddBox(g, "Step_" + i, min, max, mat, true, true, SolidFlags);
            }

            // Losa inclinada en tramos de ≤ 10 m.
            const float t = 0.4f;
            var forward = alongZ ? new Vector3(0f, dh, sign * length) : new Vector3(sign * length, dh, 0f);
            float hyp = forward.magnitude;
            var rot = Quaternion.LookRotation(forward.normalized, Vector3.up);
            var lowPoint = alongZ ? new Vector3(c.x, h0, lowEnd) : new Vector3(lowEnd, h0, c.z);
            int n = Mathf.Max(1, Mathf.CeilToInt(hyp / MaxTile - 1e-4f));
            for (int i = 0; i < n; i++)
            {
                var surfaceMid = lowPoint + forward.normalized * (hyp * (i + 0.5f) / n);
                var center = surfaceMid - rot * Vector3.up * (t / 2f);
                var go = Cube(g, "Slab_" + i, center, new Vector3(width, t, hyp / n), mat, true, slabVisible, SolidFlags);
                go.transform.localRotation = rot;
            }
        }

        static void BuildCylinder(Block b, Transform parent, string mat)
        {
            var c = Map01Math.V3(b.center); var s = Map01Math.V3(b.size);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = b.id;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = c;
            go.transform.localScale = new Vector3(s.x, s.y / 2f, s.z); // el cilindro primitivo mide 2 de alto y 1 de diámetro
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule) UnityEngine.Object.DestroyImmediate(capsule);
            if (b.collision != "none")
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(mat);
            GameObjectUtility.SetStaticEditorFlags(go, SolidFlags);
            Undo.RegisterCreatedObjectUndo(go, "Map01 Structure");
        }

        static void BuildRoomMarkers(StructureRoom room, Transform markers)
        {
            var slots = Child(markers, Map01Markers.Slots);
            foreach (var z in room.spawnZones ?? new List<SpawnZone>())
            {
                var zt = Child(slots, z.id);
                var pts = SlotPoints(z);
                for (int i = 0; i < pts.Count; i++) Point(zt, z.id + "_" + (i + 1), pts[i], Quaternion.identity);
            }
            var anchors = Child(markers, Map01Markers.Anchors);
            foreach (var a in room.lightAnchors ?? new List<LightAnchor>()) Point(anchors, a.id, Map01Math.V3(a.pos), Facing(Map01Math.V3(a.normal)));

            var openings = Child(markers, Map01Markers.Openings);
            foreach (var d in room.doors ?? new List<DoorData>()) Opening(openings, d.id, Map01Math.V3(d.center), Map01Math.V3(d.normal), Map01Math.V2(d.vano));
            foreach (var d in room.internalDoors ?? new List<InternalDoor>()) Opening(openings, d.id, Map01Math.V3(d.center), Map01Math.V3(d.normal), Map01Math.V2(d.vano));

            if (room.arena?.firstRoom != null) Volume(markers, Map01Markers.Arena, Map01Math.ToBounds(room.arena.firstRoom), false, true);
            if (room.landmark != null) Volume(markers, Map01Markers.Landmark, Map01Math.ToBounds(new MinMax { min = room.landmark.min, max = room.landmark.max }), false, true);
            if (room.release?.volume != null) Volume(markers, Map01Markers.Release, Map01Math.ToBounds(room.release.volume), true, true);
        }

        // ------------------------------------------------------------------ esclusa (coordenadas de MUNDO en los datos)
        static int BuildChamber(StructureChamber c, Transform layer)
        {
            int before = CountChildren(layer);
            var geo = Child(layer, GeometryName);
            var markers = Child(layer, Map01Markers.Root);
            Vector3 wc = Map01Math.V3(c.worldCenter);
            Vector3 aabb = Map01Math.V3(c.aabbSize), inner = Map01Math.V3(c.chamberInterior);
            bool alongZ = (c.axis ?? "+z").EndsWith("z", StringComparison.Ordinal);
            float h = inner.y, top = aabb.y - 0.5f;
            float hx = aabb.x / 2f, hz = aabb.z / 2f;

            AddBox(geo, "Floor", new Vector3(-hx, -0.5f, -hz), new Vector3(hx, 0f, hz), "Bunker_Floor", true, true, SolidFlags);
            AddBox(geo, "Roof", new Vector3(-hx, h, -hz), new Vector3(hx, top, hz), "Bunker_Ceiling", true, true, SolidFlags);

            float perpHalf = (alongZ ? inner.x : inner.z) / 2f;
            float intAlong = (alongZ ? inner.z : inner.x) / 2f, extAlong = (alongZ ? aabb.z : aabb.x) / 2f, extPerp = (alongZ ? aabb.x : aabb.z) / 2f;
            // Muros laterales de 0,5 m a lo largo de la cámara.
            if (alongZ)
            {
                AddBox(geo, "SideW", new Vector3(-perpHalf - 0.5f, 0f, -intAlong), new Vector3(-perpHalf, h, intAlong), "Bunker_Wall", true, true, SolidFlags);
                AddBox(geo, "SideE", new Vector3(perpHalf, 0f, -intAlong), new Vector3(perpHalf + 0.5f, h, intAlong), "Bunker_Wall", true, true, SolidFlags);
            }
            else
            {
                AddBox(geo, "SideS", new Vector3(-intAlong, 0f, -perpHalf - 0.5f), new Vector3(intAlong, h, -perpHalf), "Bunker_Wall", true, true, SolidFlags);
                AddBox(geo, "SideN", new Vector3(-intAlong, 0f, perpHalf), new Vector3(intAlong, h, perpHalf + 0.5f), "Bunker_Wall", true, true, SolidFlags);
            }
            // Marcos de los extremos (espesor de la banda) con el vano de su puerta; sin puerta = muro ciego (S00).
            var vano = c.doorClass == "A" ? new Vector2(5f, 4.5f) : new Vector2(8f, 6f);
            foreach (int end in new[] { -1, 1 })
            {
                Vector3? door = null;
                foreach (var d in new[] { c.sourceDoor, c.destinationDoor })
                {
                    if (d?.center == null) continue;
                    var local = Map01Math.V3(d.center) - wc;
                    float along = alongZ ? local.z : local.x;
                    if (Mathf.Sign(along) == end && Mathf.Abs(along) > 0.01f) door = local;
                }
                float a0 = end > 0 ? intAlong : -extAlong, a1 = end > 0 ? extAlong : -intAlong;
                var rects = new List<Rect2> { new Rect2(-extPerp, 0f, extPerp, h) };
                if (door.HasValue)
                {
                    float lateral = alongZ ? door.Value.x : door.Value.z;
                    rects = Rect2.Subtract(rects, new Rect2(lateral - vano.x / 2f, 0f, lateral + vano.x / 2f, vano.y));
                }
                for (int i = 0; i < rects.Count; i++)
                {
                    var r = rects[i];
                    Vector3 min = alongZ ? new Vector3(r.x0, r.z0, a0) : new Vector3(a0, r.z0, r.x0);
                    Vector3 max = alongZ ? new Vector3(r.x1, r.z1, a1) : new Vector3(a1, r.z1, r.x1);
                    AddBox(geo, (end < 0 ? "FrameStart_" : "FrameEnd_") + i, min, max, "Bunker_Wall", true, true, SolidFlags);
                }
            }

            // Marcadores.
            var anchors = Child(markers, Map01Markers.Anchors);
            foreach (var a in c.lightAnchors ?? new List<LightAnchor>()) Point(anchors, a.id, Map01Math.V3(a.pos) - wc, Facing(Map01Math.V3(a.normal)));
            var vents = Child(markers, "SteamVents");
            foreach (var v in c.steamVents ?? new List<SteamVent>()) Point(vents, v.id, Map01Math.V3(v.pos) - wc, Quaternion.identity);
            var dir = AxisVector(c.axis);
            var openings = Child(markers, Map01Markers.Openings);
            if (c.sourceDoor?.center != null) Opening(openings, "SourceDoor", Map01Math.V3(c.sourceDoor.center) - wc, dir, vano);
            if (c.destinationDoor?.center != null) Opening(openings, "DestinationDoor", Map01Math.V3(c.destinationDoor.center) - wc, dir, vano);
            if (c.trigger?.center != null)
            {
                var tb = Map01Math.ToBounds(c.trigger); tb.center -= wc;
                Volume(markers, Map01Markers.Trigger, tb, false, true);
            }
            if (c.sessionStart != null)
            {
                Point(markers, Map01Markers.PlayerStart, Map01Math.V3(c.sessionStart.playerStart) - wc, Quaternion.Euler(0f, c.sessionStart.playerYaw, 0f));
                if (c.sessionStart.station != null)
                {
                    var sb = Map01Math.ToBounds(c.sessionStart.station); sb.center -= wc;
                    Volume(markers, Map01Markers.Station, sb, false, true);
                }
            }
            return CountChildren(layer) - before;
        }

        static Vector3 AxisVector(string axis)
        {
            switch (axis) { case "+x": return Vector3.right; case "-x": return Vector3.left; case "-z": return Vector3.back; default: return Vector3.forward; }
        }

        // ------------------------------------------------------------------ Bake NavMesh all
        public static int BakeAll(StringBuilder log)
        {
            EnsureAssetFolder(NavFolder);
            int errors = 0;
            foreach (var room in LoadRooms())
            {
                var scene = Map01Scenes.OpenRoom(room, false);
                var root = Map01Hierarchy.EnsureRoomRoot(scene, room);
                var layer = Map01Hierarchy.Layer(root, Map01Layers.Structure);
                if (!layer.Find(GeometryName)) throw new InvalidOperationException(room.id + ": falta [Structure]/Geometry. Ejecuta antes Structure/Build all.");
                var data = BakeRoom(room, root, layer, log);
                Map01Scenes.Save(scene);
                errors += StructureValidation.ValidateNav(room, root, data, log);
            }
            AssetDatabase.SaveAssets();
            return errors;
        }

        /// Crea la carpeta con AssetDatabase (CreateAsset exige que la carpeta esté importada).
        static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        public static NavMeshBuildSettings BakeSettings()
        {
            var s = NavMesh.GetSettingsByID(0); // agente 0; valores forzados de F2_PLAN §3 (no toca ProjectSettings)
            s.agentRadius = 0.75f; s.agentHeight = 2f; s.agentClimb = 0.45f; s.agentSlope = 40f;
            s.overrideVoxelSize = true; s.voxelSize = 0.15f;
            return s;
        }

        /// Volúmenes Not Walkable (local al cuarto) para que no salgan islas de NavMesh fuera del juego (R0 18/11):
        /// tejado del cuarto (tapa del techo y de los muros), techos intermedios y tapas de sólidos cuya cota no es pisable.
        /// Una cota es pisable si coincide con un nivel, un slot, una puerta o la cima de una rampa. Se respetan los volúmenes Jump.
        public static List<Bounds> NotWalkableBoxes(StructureRoom room)
        {
            const float Below = 0.1f, Above = 2.5f, Tol = 0.05f;
            var result = new List<Bounds>();
            Bounds ix = Map01Math.ToBounds(room.interior), ab = Map01Math.ToBounds(room.aabbLocal);
            Bounds Box(float x0, float z0, float x1, float z1, float top) { var b = new Bounds(); b.SetMinMax(new Vector3(x0, top - Below, z0), new Vector3(x1, top + Above, z1)); return b; }

            result.Add(Box(ab.min.x, ab.min.z, ab.max.x, ab.max.z, ab.max.y));
            foreach (var c in room.ceilings ?? new List<Ceiling>())
            {
                if (c?.region == null || c.y >= ix.max.y - Tol) continue;
                var r = Map01Math.ToBounds(c.region);
                result.Add(Box(r.min.x, r.min.z, r.max.x, r.max.z, c.y + 1f));
            }

            var walkable = new List<float>();
            foreach (var l in room.levels ?? new List<Level>()) walkable.Add(l.y);
            foreach (var z in room.spawnZones ?? new List<SpawnZone>()) foreach (var p in SlotPoints(z)) walkable.Add(p.y);
            foreach (var d in room.doors ?? new List<DoorData>()) if (d?.center != null && d.center.Length >= 3) walkable.Add(d.center[1]);
            foreach (var b in room.blocks ?? new List<Block>()) if (b != null && (b.type == "ramp" || b.type == "stair")) walkable.Add(BoxOf(b).max.y);
            var jumps = (room.nav?.jumpAreaVolumes ?? new List<MinMax>()).Where(v => v?.min != null && v.max != null).Select(v => Map01Math.ToBounds(v)).ToList();

            foreach (var b in room.blocks ?? new List<Block>())
            {
                if (b == null || b.center == null || b.size == null) continue;
                switch (b.type)
                {
                    case "solid": case "wall": case "pillar": case "coverHigh": case "coverLow": case "rail": case "cylinder": break;
                    default: continue; // platform, catwalkOnPillars, ramp, stair, pit, water: pisables o sin tapa
                }
                var bb = BoxOf(b);
                float top = bb.max.y;
                if (top >= ix.max.y - Tol) continue;                       // toca el techo: no hay tapa expuesta
                if (walkable.Any(y => Mathf.Abs(y - top) < Tol)) continue;  // cota de juego
                var cap = new Vector3(bb.center.x, top, bb.center.z);
                if (jumps.Any(j => j.Contains(cap))) continue;
                bool covered = (room.blocks ?? new List<Block>()).Any(o => o != null && o != b && o.center != null && o.size != null && o.type != "water"
                    && BoxOf(o).min.y <= top + Tol && BoxOf(o).max.y > top + Tol
                    && BoxOf(o).min.x < bb.max.x && BoxOf(o).max.x > bb.min.x && BoxOf(o).min.z < bb.max.z && BoxOf(o).max.z > bb.min.z);
                if (covered) continue;                                       // algo apoya encima (pilares bajo pasarelas): no tocar
                result.Add(Box(bb.min.x, bb.min.z, bb.max.x, bb.max.z, top));
            }
            return result;
        }

        static NavMeshData BakeRoom(StructureRoom room, Transform root, Transform layer, StringBuilder log)
        {
            foreach (var old in layer.GetComponents<NavMeshSurface>()) { old.RemoveData(); Undo.DestroyObjectImmediate(old); }
            Physics.SyncTransforms();
            var markups = new List<NavMeshBuildMarkup>();
            var markerRoot = layer.Find(Map01Markers.Root);
            if (markerRoot) markups.Add(new NavMeshBuildMarkup { root = markerRoot, ignoreFromBuild = true, applyToChildren = true });
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(layer, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            sources.RemoveAll(src => src.component is Collider col && col.isTrigger);
            int jumps = 0;
            foreach (var v in room.nav?.jumpAreaVolumes ?? new List<MinMax>())
            {
                var b = Map01Math.ToBounds(v);
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(root.TransformPoint(b.center), Quaternion.identity, Vector3.one),
                    size = b.size,
                    area = JumpArea,
                });
                jumps++;
            }
            var blockedBoxes = NotWalkableBoxes(room);
            foreach (var b in blockedBoxes)
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(root.TransformPoint(b.center), Quaternion.identity, Vector3.one),
                    size = b.size,
                    area = NotWalkableArea,
                });
            log.AppendLine($"{room.id}: {blockedBoxes.Count} volúmenes Not Walkable (tejado, techos intermedios y tapas de sólidos)");
            var bounds = Map01Math.ToBounds(room.aabbLocal);
            bounds.Expand(2f);
            var data = NavMeshBuilder.BuildNavMeshData(BakeSettings(), sources, bounds, root.position, Quaternion.identity);
            if (!data) throw new InvalidOperationException(room.id + ": NavMeshBuilder.BuildNavMeshData devolvió null.");
            data.name = room.id + "_NavMesh";
            string path = NavFolder + "/" + room.id + "_NavMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);

            var surface = Undo.AddComponent<NavMeshSurface>(layer.gameObject);
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.15f;
            surface.navMeshData = data;
            surface.RemoveData();
            surface.AddData();
            log.AppendLine($"{room.id}: NavMesh {path} · {sources.Count} fuentes ({jumps} volúmenes Jump)");
            return data;
        }

        // ------------------------------------------------------------------ utilidades de geometría
        public static List<Vector3> SlotPoints(SpawnZone z)
        {
            var list = new List<Vector3>();
            if (z.points != null && z.points.Count > 0) { foreach (var p in z.points) list.Add(Map01Math.V3(p)); return list; }
            // Reparto de respaldo si faltan puntos: rejilla de 2 m dentro del rectángulo.
            Vector3 min = Map01Math.V3(z.min), max = Map01Math.V3(z.max);
            int n = Mathf.Max(1, z.slots);
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
            int rows = Mathf.CeilToInt(n / (float)cols);
            for (int i = 0; i < n; i++)
            {
                float u = cols == 1 ? 0.5f : (i % cols) / (float)(cols - 1), v = rows == 1 ? 0.5f : (i / cols) / (float)(rows - 1);
                list.Add(new Vector3(Mathf.Lerp(min.x, max.x, u), min.y, Mathf.Lerp(min.z, max.z, v)));
            }
            return list;
        }

        public static Bounds BoxOf(Block b) => new Bounds(Map01Math.V3(b.center), Map01Math.V3(b.size));

        static string DefaultMaterial(string type)
        {
            switch (type)
            {
                case "platform": return "Bunker_Floor";
                case "catwalkOnPillars": return "Bunker_Grating";
                case "ramp": case "rail": case "coverLow": return "Bunker_Hazard";
                case "coverHigh": case "pillar": case "cylinder": return "Bunker_RustMetal";
                case "water": return "Bunker_LampCold";
                default: return "Bunker_Wall";
            }
        }

        static Material Mat(string name)
        {
            if (materials.TryGetValue(name, out var m) && m) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (!m)
            {
                materialWarnings.Add("material no encontrado: " + MaterialFolder + name + ".mat (se usa Bunker_Wall)");
                m = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Bunker_Wall.mat");
            }
            materials[name] = m;
            return m;
        }

        /// Caja de min a max (local al padre), troceada en teselas de ≤ 10 m por eje. Una sola tesela se llama `name`; si hay varias, cuelgan de un grupo `name`.
        static void AddBox(Transform parent, string name, Vector3 min, Vector3 max, string mat, bool collider, bool renderer, StaticEditorFlags flags)
        {
            var size = max - min;
            if (size.x <= 0.001f || size.y <= 0.001f || size.z <= 0.001f) return;
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / MaxTile - 1e-4f)), ny = Mathf.Max(1, Mathf.CeilToInt(size.y / MaxTile - 1e-4f)), nz = Mathf.Max(1, Mathf.CeilToInt(size.z / MaxTile - 1e-4f));
            if (nx * ny * nz == 1) { Cube(parent, name, (min + max) / 2f, size, mat, collider, renderer, flags); return; }
            var g = Child(parent, name);
            var step = new Vector3(size.x / nx, size.y / ny, size.z / nz);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    for (int k = 0; k < nz; k++)
                    {
                        var tmin = min + Vector3.Scale(step, new Vector3(i, j, k));
                        Cube(g, name + "_" + i + "_" + j + "_" + k, tmin + step / 2f, step, mat, collider, renderer, flags);
                    }
        }

        static GameObject Cube(Transform parent, string name, Vector3 localCenter, Vector3 size, string mat, bool collider, bool renderer, StaticEditorFlags flags)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            if (!collider) { var bc = go.GetComponent<BoxCollider>(); if (bc) UnityEngine.Object.DestroyImmediate(bc); }
            if (renderer) go.GetComponent<MeshRenderer>().sharedMaterial = Mat(mat);
            else
            {
                var mr = go.GetComponent<MeshRenderer>(); if (mr) UnityEngine.Object.DestroyImmediate(mr);
                var mf = go.GetComponent<MeshFilter>(); if (mf) UnityEngine.Object.DestroyImmediate(mf);
            }
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            Undo.RegisterCreatedObjectUndo(go, "Map01 Structure");
            return go;
        }

        static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing) return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Map01 Structure");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Point(Transform parent, string name, Vector3 localPos, Quaternion rot)
        {
            var t = Child(parent, name);
            t.localPosition = localPos; t.localRotation = rot;
        }

        static void Opening(Transform parent, string name, Vector3 center, Vector3 normal, Vector2 vano)
        {
            var t = Child(parent, name);
            t.localPosition = center; t.localRotation = Facing(normal);
            var bc = Undo.AddComponent<BoxCollider>(t.gameObject);
            bc.center = new Vector3(0f, vano.y / 2f, 0f); bc.size = new Vector3(Mathf.Max(0.1f, vano.x), Mathf.Max(0.1f, vano.y), 1f);
            bc.isTrigger = true; bc.enabled = false;
        }

        static void Volume(Transform parent, string name, Bounds localBounds, bool enabledTrigger, bool isTrigger)
        {
            var t = Child(parent, name);
            t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity;
            var bc = Undo.AddComponent<BoxCollider>(t.gameObject);
            bc.center = localBounds.center; bc.size = localBounds.size;
            bc.isTrigger = isTrigger; bc.enabled = enabledTrigger;
        }

        static Quaternion Facing(Vector3 n)
        {
            if (n.sqrMagnitude < 1e-6f) return Quaternion.identity;
            n.Normalize();
            return Quaternion.LookRotation(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up);
        }

        static int CountChildren(Transform t) => t.GetComponentsInChildren<Transform>(true).Length;

        /// Rectángulo en un plano 2D (x0,z0)-(x1,z1) con resta de rectángulos alineados.
        public struct Rect2
        {
            public float x0, z0, x1, z1;
            public Rect2(float x0, float z0, float x1, float z1) { this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1; }
            public bool Valid => x1 - x0 > 0.001f && z1 - z0 > 0.001f;
            public static List<Rect2> Subtract(List<Rect2> rects, Rect2 hole)
            {
                var result = new List<Rect2>();
                foreach (var r in rects)
                {
                    float ix0 = Mathf.Max(r.x0, hole.x0), iz0 = Mathf.Max(r.z0, hole.z0), ix1 = Mathf.Min(r.x1, hole.x1), iz1 = Mathf.Min(r.z1, hole.z1);
                    if (ix1 - ix0 <= 0.001f || iz1 - iz0 <= 0.001f) { result.Add(r); continue; }
                    var parts = new[]
                    {
                        new Rect2(r.x0, r.z0, ix0, r.z1),   // izquierda
                        new Rect2(ix1, r.z0, r.x1, r.z1),   // derecha
                        new Rect2(ix0, r.z0, ix1, iz0),     // abajo
                        new Rect2(ix0, iz1, ix1, r.z1),     // arriba
                    };
                    foreach (var p in parts) if (p.Valid) result.Add(p);
                }
                return result;
            }
        }
    }
}
