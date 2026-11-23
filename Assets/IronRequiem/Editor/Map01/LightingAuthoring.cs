using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor.Map01
{
    /// Map01 F2 lighting authoring (R5). Greybox only: one real-time Light per light anchor of Structure, a simple fixture mesh from
    /// Art/VisualOverhaul/Meshes, RenderSettings (trilight ambient + exp2 fog) per room, one baked reflection probe per room and one
    /// fill light per airlock chamber. States, blackouts, volumes and fixture art are F3. Plan: Docs/IronRequiem/Map01/05_ILUMINACION.md.
    /// Only the [Lighting] layer is touched. Idempotent: ClearLayer + rebuild. Data: Levels/Map01/Data/Lighting/R<NN>.json.
    public static class LightingAuthoring
    {
        // ------------------------------------------------------------------ data model (JSON)
        public sealed class Env { public float[] sky, equator, ground, fogColor; public float ambientIntensity = 1f, reflectionIntensity = 1.1f, fogDensity = 0.012f; }
        public sealed class Probe { public float[] center, size; }
        public sealed class Fixture
        {
            public string anchor, lum, kind = "P", mesh = "none", emissive = "cold", group, role;
            public float[] color, expectPos, aim;
            public float intensity, range, spot = 60f;
            public bool shadow, enabled = true;
        }
        /// tramo / exposureEV (Pase visual 12, F2): tramo de luminosidad (1..3) y exposicion en EV del Volume del cuarto. Opcionales: sin ellos no hay Volume.
        public sealed class RoomData { public string room, zone; public int tramo; public float exposureEV; public Env environment; public List<Probe> probes; public List<Fixture> lights; }
        sealed class Pair { public StructureRoom room; public RoomData data; }

        const string MeshFolder = "Assets/IronRequiem/Art/VisualOverhaul/Meshes/";
        const string MatFolder = "Assets/IronRequiem/Art/VisualOverhaul/Materials/";
        public const string ProbeFolder = Map01Paths.GeneratedRoot + "/Probes";
        const int MaxLit = 8, MaxShadow = 2, MaxPerObject = 4, MaxProbes = 2;

        // fill colour of each airlock chamber (05_ILUMINACION §L3.20): chromatic bridge between the zone left and the zone entered
        static readonly Dictionary<string, float[]> FillColor = new Dictionary<string, float[]>
        {
            { "C01", new[] { 1.0f, 0.86f, 0.70f } }, { "C02", new[] { 0.90f, 0.90f, 0.96f } }, { "C03", new[] { 0.86f, 0.92f, 1.0f } },
            { "C04", new[] { 0.86f, 0.92f, 1.0f } }, { "C05", new[] { 0.95f, 0.90f, 0.86f } }, { "C06", new[] { 0.86f, 0.94f, 0.92f } },
            { "C07", new[] { 0.95f, 0.93f, 0.88f } }, { "C08", new[] { 1.0f, 0.86f, 0.80f } }, { "C09", new[] { 0.95f, 0.85f, 0.82f } },
            { "C10", new[] { 0.86f, 0.96f, 0.94f } }, { "C11", new[] { 0.84f, 0.98f, 0.96f } }, { "C12", new[] { 0.84f, 0.98f, 0.96f } },
            { "C13", new[] { 0.95f, 0.92f, 0.86f } }, { "C14", new[] { 0.94f, 0.92f, 0.92f } }, { "C15", new[] { 0.84f, 0.94f, 1.0f } },
            { "C16", new[] { 0.80f, 0.94f, 1.0f } }, { "C17", new[] { 0.90f, 0.94f, 1.0f } },
        };

        // ------------------------------------------------------------------ menus
        [MenuItem(Map01Paths.MenuRoot + "Lighting/Build greybox all")]
        public static void BuildGreyboxAll()
        {
            var pairs = LoadAll();                       // validates every data file before touching any scene
            var log = new StringBuilder("Map01 Lighting: Build greybox all\n");
            foreach (var p in pairs) BuildRoom(p, log);
            BuildSessions(log);
            Map01Report.Write("lighting_build.txt", log.ToString());
        }

        /// Reconstruye SOLO la luz de los 18 cuartos (capa [Lighting]: luces, ambiente, niebla, probes y Volume de exposicion). No toca
        /// Structure, Doors, Encounters ni las escenas de sesion. Lo usa VisualOverhaulLuminosity.Apply (Pase visual 12).
        public static string BuildRoomsOnly()
        {
            var pairs = LoadAll();
            var log = new StringBuilder("Map01 Lighting: Build rooms only\n");
            foreach (var p in pairs) BuildRoom(p, log);
            Map01Report.Write("lighting_build_rooms.txt", log.ToString());
            return log.ToString();
        }

        [MenuItem(Map01Paths.MenuRoot + "Lighting/Bake reflection probes")]
        public static void BakeProbesAll()
        {
            Directory.CreateDirectory(ProbeFolder);
            AssetDatabase.Refresh();
            var log = new StringBuilder("Map01 Lighting: Bake reflection probes\n");
            var failed = new List<string>();
            foreach (var id in Map01Paths.RoomIds)
            {
                var room = Map01Json.Room(id);
                var scene = Map01Scenes.OpenRoom(room, false);
                var probes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)).ToArray();
                if (probes.Length == 0) { log.AppendLine(id + ": sin ReflectionProbe (ejecutar Build greybox all)"); failed.Add(id); continue; }
                for (int i = 0; i < probes.Length; i++)
                {
                    string path = ProbeFolder + "/" + id + "_" + (i + 1) + ".exr";
                    bool ok = Lightmapping.BakeReflectionProbe(probes[i], path);
                    log.AppendLine(id + " probe " + (i + 1) + (ok ? " ok -> " : " FALLO -> ") + path);
                    if (!ok) failed.Add(id + "#" + (i + 1));
                }
                Map01Scenes.Save(scene);
            }
            AssetDatabase.Refresh();
            Map01Report.Write("lighting_bake.txt", log.ToString());
            if (failed.Count > 0) throw new InvalidOperationException("Map01 Lighting: no se pudo hornear: " + string.Join(", ", failed));
        }

        [MenuItem(Map01Paths.MenuRoot + "Lighting/Validate all")]
        public static void ValidateAll()
        {
            var pairs = LoadAll();
            var errors = new List<string>(); var warns = new List<string>(); var log = new StringBuilder("Map01 Lighting: Validate all\n");
            foreach (var p in pairs) ValidateRoom(p, errors, warns, log);
            ValidateSessions(errors, warns, log);
            log.AppendLine("\nERRORES: " + errors.Count); foreach (var e in errors) log.AppendLine("  ERROR " + e);
            log.AppendLine("AVISOS: " + warns.Count); foreach (var w in warns) log.AppendLine("  aviso " + w);
            Map01Report.Write("lighting_validate.txt", log.ToString());
            if (errors.Count > 0) Debug.LogError("[Map01] Lighting: " + errors.Count + " errores (ver lighting_validate.txt)");
            else Debug.Log("[Map01] Lighting: validación OK (" + warns.Count + " avisos)");
        }

        // ------------------------------------------------------------------ loading and data checks
        static List<Pair> LoadAll()
        {
            var errors = new List<string>(); var list = new List<Pair>();
            foreach (var id in Map01Paths.RoomIds)
            {
                try
                {
                    var room = Map01Json.Room(id);
                    var data = Map01Json.Load<RoomData>(Map01Paths.Data("Lighting", id));
                    CheckData(id, data, errors);
                    list.Add(new Pair { room = room, data = data });
                }
                catch (Exception e) { errors.Add(id + ": " + e.Message); }
            }
            if (errors.Count > 0) throw new InvalidOperationException("Map01 Lighting: datos inválidos:\n" + string.Join("\n", errors));
            return list;
        }

        static void CheckData(string id, RoomData d, List<string> errors)
        {
            if (d == null) { errors.Add(id + ": JSON vacío"); return; }
            if (d.room != id) errors.Add(id + ".room = '" + d.room + "'");
            var e = d.environment;
            if (e == null || e.sky == null || e.sky.Length < 3 || e.equator == null || e.equator.Length < 3 || e.ground == null || e.ground.Length < 3 || e.fogColor == null || e.fogColor.Length < 3)
                errors.Add(id + ".environment: sky, equator, ground y fogColor necesitan 3 valores");
            if (d.lights == null || d.lights.Count == 0) { errors.Add(id + ".lights vacío"); return; }
            var seen = new HashSet<string>();
            foreach (var f in d.lights)
            {
                string at = id + " luz '" + f.anchor + "'";
                if (string.IsNullOrEmpty(f.anchor) || !f.anchor.StartsWith(id + ".A", StringComparison.Ordinal)) errors.Add(at + ": anchor debe empezar por " + id + ".A");
                else if (!seen.Add(f.anchor)) errors.Add(at + ": anchor repetido");
                if (f.kind != "P" && f.kind != "S") errors.Add(at + ".kind debe ser P o S");
                if (f.color == null || f.color.Length < 3) errors.Add(at + ".color necesita 3 valores");
                if (f.intensity < 0) errors.Add(at + ".intensity < 0");
                if (f.range <= 0) errors.Add(at + ".range <= 0");
                if (f.kind == "S" && (f.spot < 1 || f.spot > 179)) errors.Add(at + ".spot fuera de 1..179");
                if (f.shadow && f.kind != "S") errors.Add(at + ": sombra solo en spots (plan §L5.2)");
                if (f.mesh != "ceiling" && f.mesh != "emergency" && f.mesh != "none") errors.Add(at + ".mesh debe ser ceiling, emergency o none");
                if (f.emissive != "cold" && f.emissive != "amber" && f.emissive != "red") errors.Add(at + ".emissive debe ser cold, amber o red");
            }
            if (d.lights.Count(f => f.shadow) > MaxShadow) errors.Add(id + ": más de " + MaxShadow + " luces con sombra");
            if (d.probes != null && d.probes.Count > MaxProbes) errors.Add(id + ": más de " + MaxProbes + " reflection probes");
        }

        // ------------------------------------------------------------------ build: rooms
        static void BuildRoom(Pair p, StringBuilder log)
        {
            var room = p.room; var data = p.data;
            var scene = Map01Scenes.OpenRoom(room, false);
            var root = Map01Hierarchy.EnsureRoomRoot(scene, room);
            var layer = Map01Hierarchy.Layer(root, Map01Layers.Lighting);
            Map01Hierarchy.ClearLayer(layer);

            var anchors = new Dictionary<string, LightAnchor>();
            if (room.lightAnchors != null) foreach (var a in room.lightAnchors) if (a != null && !string.IsNullOrEmpty(a.id) && !anchors.ContainsKey(a.id)) anchors.Add(a.id, a);
            var used = new HashSet<string>();
            var made = new List<Light>();
            int lit = 0, shadows = 0;

            foreach (var f in data.lights)
            {
                if (!anchors.TryGetValue(f.anchor, out var a))
                {
                    if (f.role == "reserve") { log.AppendLine(room.id + ": reserva " + f.anchor + " sin anclaje en Estructura (se omite)"); continue; }
                    throw new InvalidOperationException(room.id + ": la luz '" + f.anchor + "' no tiene anclaje en Data/Structure/" + room.id + ".json (lightAnchors)");
                }
                used.Add(f.anchor);
                var l = CreateLight(layer, room, f, a, log, false);
                made.Add(l);
                if (l.enabled && l.intensity > 0.001f) lit++;
                if (l.shadows != LightShadows.None) shadows++;
            }
            // Anchors of R5 that the lighting data does not mention still get a neutral fill light, so no anchor is left dark.
            foreach (var kv in anchors)
            {
                if (used.Contains(kv.Key)) continue;
                if (!IsMine(kv.Value)) continue; // [R0 fix 18/11] R1 marks lighting anchors owner "Lighting"
                var def = new Fixture { anchor = kv.Key, lum = "default", kind = "P", color = new[] { 0.85f, 0.92f, 1f }, intensity = 12, range = 12, mesh = "none", role = "default" };
                var l = CreateLight(layer, room, def, kv.Value, log, true);
                made.Add(l); lit++;
                log.AppendLine(room.id + ": anclaje " + kv.Key + " sin entrada en el JSON de luz: luz neutra por defecto");
            }

            ApplyEnvironment(data.environment);
            BuildExposureVolume(layer, room, data, log);
            int probes = BuildProbes(layer, room, data, made);
            Map01Scenes.Save(scene);
            log.AppendLine(room.id + ": luces " + made.Count + " (encendidas " + lit + ", con sombra " + shadows + "), probes " + probes);
        }

        static Light CreateLight(Transform layer, StructureRoom room, Fixture f, LightAnchor a, StringBuilder log, bool isDefault)
        {
            var go = new GameObject(f.anchor);             // exact anchor name: Encounters (CorridorAlarm) finds lights by it
            Undo.RegisterCreatedObjectUndo(go, "Map01 light");
            go.transform.SetParent(layer, false);
            var pos = Map01Math.V3(a.pos);
            go.transform.localPosition = pos;
            if (f.expectPos != null && f.expectPos.Length >= 3 && Vector3.Distance(pos, Map01Math.V3(f.expectPos)) > 2.5f)
                log.AppendLine(room.id + ": AVISO " + f.anchor + " está en " + pos + " y el plan esperaba " + Map01Math.V3(f.expectPos) + " (¿numeración de anclajes distinta?)");

            var n = Map01Math.V3(a.normal);
            string kind = (a.kind ?? "").ToLowerInvariant();
            bool ceiling = kind.Contains("ceil") || kind.Contains("techo") || Mathf.Abs(n.y) > 0.5f || n.sqrMagnitude < 0.01f;
            if (!ceiling && room.interior != null)
            {
                var c = (Map01Math.V3(room.interior.min) + Map01Math.V3(room.interior.max)) * 0.5f;
                var toCentre = new Vector3(c.x - pos.x, 0, c.z - pos.z);
                if (Vector3.Dot(new Vector3(n.x, 0, n.z), toCentre) < 0) n = -n;   // wall normal must point into the room
            }
            Vector3 fwd;
            if (f.aim != null && f.aim.Length >= 3 && Map01Math.V3(f.aim).sqrMagnitude > 0.01f) fwd = Map01Math.V3(f.aim).normalized;
            else if (ceiling) fwd = Vector3.down;
            else fwd = f.kind == "S" ? (n.normalized + Vector3.down * 0.6f).normalized : n.normalized;
            go.transform.localRotation = Quaternion.LookRotation(fwd);

            var l = go.AddComponent<Light>();
            l.type = f.kind == "S" ? LightType.Spot : LightType.Point;
            l.color = new Color(f.color[0], f.color[1], f.color[2]);
            l.intensity = f.intensity;
            float range = f.range;
            if (a.maxRange > 0 && range > a.maxRange) { log.AppendLine(room.id + ": " + f.anchor + " alcance " + range + " recortado a maxRange " + a.maxRange); range = a.maxRange; }
            l.range = range;
            if (f.kind == "S") l.spotAngle = f.spot;
            l.shadows = f.shadow ? LightShadows.Soft : LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            l.enabled = f.enabled;

            if (!isDefault && f.mesh != "none") AddFixtureMesh(layer, go.transform, f, n, ceiling);
            return l;
        }

        static void AddFixtureMesh(Transform layer, Transform lightTr, Fixture f, Vector3 n, bool ceiling)
        {
            string meshName = f.mesh == "ceiling" ? "VO_CeilingLight" : "VO_EmergencyLight";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + meshName + ".asset");
            var housing = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "VO_Prop_Housing.mat");
            string lensName = f.emissive == "red" ? "VO_Light_Red" : f.emissive == "amber" ? "VO_Light_Amber" : "VO_Light_Cold";
            var lens = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + lensName + ".mat");
            if (!mesh || !housing || !lens) return;       // greybox: a missing kit asset only skips the visual
            var go = new GameObject(f.anchor + "_Lamp");
            Undo.RegisterCreatedObjectUndo(go, "Map01 lamp");
            go.transform.SetParent(layer, false);
            if (f.mesh == "ceiling") { go.transform.localPosition = lightTr.localPosition + Vector3.up * 0.2f; go.transform.localRotation = Quaternion.identity; }
            else
            {
                var back = ceiling ? Vector3.down : n.normalized;
                go.transform.localPosition = lightTr.localPosition + back * 0.03f;
                go.transform.localRotation = Quaternion.LookRotation(-back);
            }
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { housing, lens };   // shared materials only: no instances, SRP Batcher friendly
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        static void ApplyEnvironment(Env e)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Col(e.sky); RenderSettings.ambientEquatorColor = Col(e.equator); RenderSettings.ambientGroundColor = Col(e.ground);
            RenderSettings.ambientLight = Col(e.sky);
            RenderSettings.ambientIntensity = e.ambientIntensity; RenderSettings.reflectionIntensity = e.reflectionIntensity;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Col(e.fogColor); RenderSettings.fogDensity = e.fogDensity;
        }

        /// Pase visual 12 (F2): exposicion por tramo. Un Volume GLOBAL por cuarto (prioridad 5, peso 1, capa Default = la que ve la camara del
        /// jugador, volumeLayerMask 1) con ColorAdjustments.postExposure. Va en la escena del cuarto y no en Campaign_PostProfile porque (1) ese
        /// perfil forma parte de la pila del hangar protegido y no se edita, y (2) los cuartos se cargan aditivos y ZoneEnvironment.SceneFade.Bind
        /// recoge los Volume globales de cada escena de etapa y los funde con la entrada/salida del cuarto, asi la exposicion sube y baja con la ruta.
        /// Vive bajo [Lighting], que ClearLayer vacia en cada reconstruccion (idempotente). Sin exposureEV en el JSON no se crea nada.
        static void BuildExposureVolume(Transform layer, StructureRoom room, RoomData data, StringBuilder log)
        {
            if (data.exposureEV <= 0.0001f) return;
            int tramo = data.tramo > 0 ? data.tramo : VisualOverhaulLuminosity.TramoOfRoom(room.id);
            var profile = VisualOverhaulLuminosity.EnsureProfile(tramo);
            if (!profile) throw new InvalidOperationException(room.id + ": no se pudo crear/cargar el perfil de exposicion del tramo " + tramo);
            if (Mathf.Abs(VisualOverhaulLuminosity.ExposureOfTramo(tramo) - data.exposureEV) > 0.01f)
                log.AppendLine(room.id + ": AVISO exposureEV " + data.exposureEV + " distinto del perfil del tramo " + tramo + " (" + VisualOverhaulLuminosity.ExposureOfTramo(tramo) + ")");
            var go = new GameObject("Map01_Exposure_T" + tramo);
            Undo.RegisterCreatedObjectUndo(go, "Map01 exposure volume");
            go.transform.SetParent(layer, false);
            go.layer = 0;
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 5f; vol.weight = 1f; vol.sharedProfile = profile;
        }

        static Color Col(float[] c) => new Color(c[0], c[1], c[2], 1f);

        static int BuildProbes(Transform layer, StructureRoom room, RoomData data, List<Light> lights)
        {
            var boxes = new List<UnityEngine.Bounds>();
            if (data.probes != null && data.probes.Count > 0)
                foreach (var p in data.probes) boxes.Add(new UnityEngine.Bounds(Map01Math.V3(p.center), Map01Math.V3(p.size)));
            else
            {
                var box = room.interior ?? room.aabbLocal;
                if (box == null) throw new InvalidOperationException(room.id + ": sin interior ni aabbLocal para la reflection probe");
                var b = Map01Math.ToBounds(box);
                b.Expand(new Vector3(2f, 1f, 2f));
                boxes.Add(b);
            }
            int index = 0;
            foreach (var b in boxes)
            {
                index++;
                // The capture point must not sit inside a solid (central blocks, cores): use the lamp nearest to the box centre, 1 m lower.
                var capture = b.center; float best = float.MaxValue;
                foreach (var l in lights) { float d = (l.transform.localPosition - b.center).sqrMagnitude; if (d < best) { best = d; capture = l.transform.localPosition + Vector3.down; } }
                if (best > 144f) capture = b.center;   // no lamp within 12 m: capture from the box centre
                var go = new GameObject("ReflectionProbe_" + room.id + "_" + index);
                Undo.RegisterCreatedObjectUndo(go, "Map01 probe");
                go.transform.SetParent(layer, false); go.transform.localPosition = capture;
                var pr = go.AddComponent<ReflectionProbe>();
                pr.mode = ReflectionProbeMode.Baked; pr.boxProjection = true; pr.resolution = 128; pr.hdr = true; pr.importance = 1; pr.intensity = 1; pr.blendDistance = 2;
                pr.size = b.size; pr.center = b.center - capture;
            }
            return boxes.Count;
        }

        // ------------------------------------------------------------------ build: sessions (airlock fills, S00)
        static void BuildSessions(StringBuilder log)
        {
            for (int s = 0; s < Map01Paths.SessionScenes.Length; s++)
            {
                var scene = Map01Scenes.OpenSession(s);
                foreach (var cid in Map01Paths.ChamberIds)
                {
                    if (!Map01Paths.SessionsOfChamber(cid).Contains(s)) continue;
                    var ch = Map01Json.Chamber(cid);
                    var root = Map01Hierarchy.EnsureChamberRoot(scene, ch);
                    var layer = Map01Hierarchy.Layer(root, Map01Layers.Lighting);
                    Map01Hierarchy.ClearLayer(layer);
                    int n = cid == "S00" ? BuildVestibule(layer, ch) : BuildFill(layer, ch);
                    log.AppendLine("session " + (s + 1) + " " + cid + ": luces " + n);
                }
                Map01Scenes.Save(scene);
            }
        }

        static bool IsMine(LightAnchor a) => string.IsNullOrEmpty(a.owner) || string.Equals(a.owner, "R5", StringComparison.OrdinalIgnoreCase) || string.Equals(a.owner, "Lighting", StringComparison.OrdinalIgnoreCase);

        static int BuildFill(Transform layer, StructureChamber ch)
        {
            var anchor = ch.lightAnchors == null ? null : ch.lightAnchors.FirstOrDefault(a => a != null && a.id == ch.id + ".FILL");
            var worldCenter = Map01Math.V3(ch.worldCenter);
            Vector3 local = anchor != null ? Map01Math.V3(anchor.pos) - worldCenter : Vector3.up * Mathf.Max(1f, Map01Math.V3(ch.aabbSize).y * 0.5f - 1f);
            bool z = (ch.doorClass ?? "").StartsWith("Z", StringComparison.OrdinalIgnoreCase);
            var go = new GameObject(ch.id + ".FILL");
            Undo.RegisterCreatedObjectUndo(go, "Map01 fill");
            go.transform.SetParent(layer, false); go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            var c = FillColor.TryGetValue(ch.id, out var col) ? col : new[] { 0.95f, 0.95f, 1f };
            l.color = new Color(c[0], c[1], c[2]);
            l.intensity = z ? 9f : 6f; l.range = z ? 9f : 7f;
            l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Realtime;
            return 1;
        }

        static int BuildVestibule(Transform layer, StructureChamber ch)
        {
            var worldCenter = Map01Math.V3(ch.worldCenter);
            var mine = (ch.lightAnchors ?? new List<LightAnchor>()).Where(a => a != null && a.id != null && IsMine(a) && !a.id.Contains(".CY")).ToList();
            if (mine.Count == 0) mine.Add(new LightAnchor { id = "S00.FILL", pos = new[] { worldCenter.x, worldCenter.y + 3.5f, worldCenter.z } });
            Vector3 station = ch.sessionStart != null && ch.sessionStart.station != null
                ? (Map01Math.V3(ch.sessionStart.station.min) + Map01Math.V3(ch.sessionStart.station.max)) * 0.5f : worldCenter;
            int i = 0;
            foreach (var a in mine)
            {
                var go = new GameObject(a.id);            // exact anchor name
                Undo.RegisterCreatedObjectUndo(go, "Map01 S00 light");
                go.transform.SetParent(layer, false);
                var wp = Map01Math.V3(a.pos); go.transform.localPosition = wp - worldCenter;
                var l = go.AddComponent<Light>(); l.lightmapBakeType = LightmapBakeType.Realtime; l.shadows = LightShadows.None;
                if (i == 1)
                {   // second anchor: spot on the WeaponMountStation (the mandatory weapon before the first T-07)
                    l.type = LightType.Spot; l.spotAngle = 55; l.color = new Color(1f, 0.85f, 0.65f); l.intensity = 16; l.range = 9;
                    var dir = station - wp; if (dir.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
                else { l.type = LightType.Point; l.color = new Color(1f, 0.65f, 0.28f); l.intensity = 10; l.range = 8; }
                i++;
            }
            return mine.Count;
        }

        // ------------------------------------------------------------------ validation
        static void ValidateRoom(Pair p, List<string> errors, List<string> warns, StringBuilder log)
        {
            var room = p.room; string id = room.id;
            Scene scene = Map01Scenes.OpenRoom(room, false);
            var roots = scene.GetRootGameObjects();
            var rootGo = roots.FirstOrDefault(g => g.name == Map01Layers.RoomRootName(id));
            if (!rootGo) { errors.Add(id + ": no existe la raíz " + Map01Layers.RoomRootName(id)); return; }
            var layer = rootGo.transform.Find(Map01Layers.Lighting);
            if (!layer) { errors.Add(id + ": falta la capa " + Map01Layers.Lighting); return; }

            var lights = layer.GetComponentsInChildren<Light>(true).ToList();
            var names = new HashSet<string>();
            foreach (var l in lights) if (!names.Add(l.name)) errors.Add(id + ": nombre de luz repetido " + l.name);

            // anchors <-> lights (no anchor without a light, no orphan light)
            var anchorIds = new HashSet<string>();
            if (room.lightAnchors != null)
                foreach (var a in room.lightAnchors)
                {
                    if (a == null || string.IsNullOrEmpty(a.id) || !IsMine(a)) continue;
                    anchorIds.Add(a.id);
                    if (!names.Contains(a.id)) errors.Add(id + ": anclaje " + a.id + " sin luz");
                }
            foreach (var l in lights) if (!anchorIds.Contains(l.name)) errors.Add(id + ": luz huérfana '" + l.name + "' (sin anclaje de R5 en Estructura)");

            // counts. "Lit" = enabled with intensity > 0: alarm and phase lights start at intensity 0 and are driven by their components
            // (CorridorAlarm in R07, the boss lighting in R18), so they do not count (CC-15).
            var litLights = lights.Where(l => l.enabled && l.gameObject.activeInHierarchy && l.intensity > 0.001f).ToList();
            if (litLights.Count > MaxLit) errors.Add(id + ": " + litLights.Count + " luces encendidas (máx. " + MaxLit + ")");
            if (room.lightBudget != null && room.lightBudget.staticLights > 0 && litLights.Count > room.lightBudget.staticLights)
                warns.Add(id + ": " + litLights.Count + " luces encendidas, el presupuesto de Estructura es " + room.lightBudget.staticLights);
            var shadowed = lights.Where(l => l.enabled && l.shadows != LightShadows.None).ToList();
            if (shadowed.Count > MaxShadow) errors.Add(id + ": " + shadowed.Count + " luces con sombra (máx. " + MaxShadow + ")");
            foreach (var l in shadowed) if (l.type != LightType.Spot) errors.Add(id + ": " + l.name + " tiene sombra y no es spot");
            foreach (var l in lights) if (l.lightmapBakeType != LightmapBakeType.Realtime) errors.Add(id + ": " + l.name + " no es Realtime");

            var probes = layer.GetComponentsInChildren<ReflectionProbe>(true);
            if (probes.Length == 0) warns.Add(id + ": sin reflection probe");
            if (probes.Length > MaxProbes) errors.Add(id + ": " + probes.Length + " reflection probes (máx. " + MaxProbes + ")");
            foreach (var pr in probes) if (pr.mode != ReflectionProbeMode.Baked) errors.Add(id + ": " + pr.name + " no es Baked");
            if (p.data.exposureEV > 0.0001f && !layer.GetComponentsInChildren<Volume>(true).Any(v => v.isGlobal && v.sharedProfile)) warns.Add(id + ": sin Volume de exposicion (ejecutar Build rooms only)");
            if (RenderSettings.ambientMode != AmbientMode.Trilight) errors.Add(id + ": el ambiente no es Trilight");
            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.ExponentialSquared) errors.Add(id + ": la niebla no es exp2 activa");

            // saturated red / amber / green lights near a door frame are reserved for the door status (R4): warn
            if (room.doors != null)
                foreach (var d in room.doors)
                {
                    if (d == null || d.center == null) continue;
                    var dc = Map01Math.V3(d.center);
                    foreach (var l in litLights)
                    {
                        float mx = Mathf.Max(l.color.r, Mathf.Max(l.color.g, l.color.b)), mn = Mathf.Min(l.color.r, Mathf.Min(l.color.g, l.color.b));
                        bool saturated = mx > 0 && (mx - mn) / mx > 0.5f && l.color.b < 0.6f * mx;
                        if (saturated && Vector3.Distance(l.transform.localPosition, dc) < 6f)
                            warns.Add(id + ": " + l.name + " es rojo/ámbar/verde saturado a < 6 m de la puerta " + d.id);
                    }
                }

            // lights per object (pattern of VisualOverhaulDressing.CheckLightBudget: light sphere against renderer bounds)
            int max = 0, over = 0, hard = 0; var detail = new List<string>();
            var pts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(false))
                .Where(l => l.type != LightType.Directional && l.enabled && l.intensity > 0.001f).ToList();
            foreach (var g in roots)
                foreach (var r in g.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r || !r.enabled || r.forceRenderingOff) continue;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (r.transform.IsChildOf(layer)) continue;   // the lamp meshes themselves
                    var b = r.bounds; int raw = 0, eff = 0;
                    foreach (var l in pts)
                    {
                        float dist = Mathf.Sqrt(b.SqrDistance(l.transform.position));
                        if (dist < l.range) raw++;
                        if (dist < 0.85f * l.range) eff++;       // lights near the edge of their range contribute almost nothing
                    }
                    if (raw > max) max = raw;
                    if (raw > MaxPerObject) { over++; if (eff > MaxPerObject) { hard++; if (detail.Count < 8) detail.Add(r.name + " (" + eff + " efectivas, " + raw + " candidatas)"); } }
                }
            if (hard > 0) errors.Add(id + ": " + hard + " objetos con más de " + MaxPerObject + " luces efectivas: " + string.Join("; ", detail));
            if (over > hard) warns.Add(id + ": " + (over - hard) + " objetos con " + (MaxPerObject + 1) + "+ luces candidatas pero ≤ " + MaxPerObject + " efectivas (aportan en el borde del alcance)");
            log.AppendLine(id + ": luces " + lights.Count + ", encendidas " + litLights.Count + ", sombra " + shadowed.Count + ", probes " + probes.Length + ", máx. candidatas por objeto " + max + ", objetos > " + MaxPerObject + ": " + over + " (duros " + hard + ")");
        }

        static void ValidateSessions(List<string> errors, List<string> warns, StringBuilder log)
        {
            for (int s = 0; s < Map01Paths.SessionScenes.Length; s++)
            {
                var scene = Map01Scenes.OpenSession(s);
                var roots = scene.GetRootGameObjects();
                foreach (var cid in Map01Paths.ChamberIds)
                {
                    if (!Map01Paths.SessionsOfChamber(cid).Contains(s)) continue;
                    var root = roots.FirstOrDefault(g => g.name == cid);
                    var layer = root ? root.transform.Find(Map01Layers.Lighting) : null;
                    if (!layer) { errors.Add("S" + (s + 1) + " " + cid + ": falta " + Map01Layers.Lighting); continue; }
                    var lights = layer.GetComponentsInChildren<Light>(true);
                    if (lights.Length == 0) errors.Add("S" + (s + 1) + " " + cid + ": sin luz de relleno");
                    if (lights.Any(l => l.shadows != LightShadows.None)) errors.Add("S" + (s + 1) + " " + cid + ": las luces de esclusa no llevan sombra");
                    if (cid != "S00" && lights.Length > 1) warns.Add("S" + (s + 1) + " " + cid + ": más de una luz de R5 en la cámara");
                }
                log.AppendLine("session " + (s + 1) + " revisada");
            }
        }
    }
}
