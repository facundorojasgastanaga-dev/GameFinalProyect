using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor.Map01
{
    /// Data/Doors/R<NN>.json (owner R4). Sealed volumes in ROOM-LOCAL coordinates, matched to Structure internalDoors by geometry
    /// (a door seals the volume whose face it sits on), so the data does not depend on R1's door IDs. Spec: 04_PUERTAS.md §Q2.
    public sealed class DoorsRoomData { public string id, notes; public List<DoorSealedVolume> sealedVolumes; }
    public sealed class DoorSealedVolume
    {
        public string id, kind; public float[] min, max;          // kind: portal | wing | cage | boss
        public int? closeOnWave, openOnWave; public bool? startClosed, klaxon; public float? warningSeconds, minPlayerDistance;
    }

    /// Resolved behaviour of one interior shutter.
    public sealed class ShutterPlan
    {
        public InternalDoor door; public string kind, volumeId, source; public Bounds sealedLocal;
        public int closeOnWave = 0, openOnWave = -1; public bool startClosed, klaxon = true, requirePlayerInArena = true;
        public float warningSeconds = .5f, minPlayerDistance = 4f;
    }

    /// Map01 F2 doors (owner R4). Greybox doors that keep the AirlockDoorMotion contract:
    ///   sessions: C<NN>/[Doors]/SourceDoor, DestinationDoor, Airlock (CampaignAirlock) or Handoff (Map01SessionHandoff);
    ///   rooms:    [Doors]/<internalDoorId> with ArenaShutter (host, always active) + child "Door" (root moved by AirlockDoorMotion).
    /// Idempotent: clears only the [Doors] layer of each chamber/room, rebuilds with Undo, saves and writes a report.
    public static class DoorAuthoring
    {
        public const string HazardMaterial = "Assets/IronRequiem/Art/Environment/Bunker/Bunker_Hazard.mat";
        public const string LampMaterial = "Assets/IronRequiem/Art/Environment/Bunker/Bunker_LampAmber.mat";
        public const string KlaxonClip = "Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Corridor_Klaxon.wav";
        public const string MixerPath = "Assets/FPS/Audio/MainAudioMixer.mixer";
        public const string SourceName = "SourceDoor", DestinationName = "DestinationDoor", AirlockName = "Airlock", HandoffName = "Handoff", ShutterDoorName = "Door", LampName = "WarningLamp";
        public const float MinTriggerGap = 1.0f; // 04_PUERTAS AT-1

        public enum ChamberRole { SessionStart, Normal, Departure }

        // ------------------------------------------------------------------ menus

        [MenuItem(Map01Paths.MenuRoot + "Doors/Build all")]
        public static void BuildAll()
        {
            Map01Scenes.GuardForeignDirtyScenes();
            var log = new StringBuilder("Map01 Doors/Build all · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            var errors = new List<string>();
            var chambers = LoadChambers(errors);
            var rooms = LoadRooms(errors);
            ValidateChambers(chambers, errors);
            ValidateRooms(rooms, errors);
            if (errors.Count > 0) Fail("Doors/Build all: datos inválidos", errors, log, "doors_build.txt");

            var hazard = LoadAsset<Material>(HazardMaterial); var lamp = LoadAsset<Material>(LampMaterial);
            var klaxon = LoadAsset<AudioClip>(KlaxonClip); var general = GeneralGroup();
            for (int s = 0; s < Map01Paths.SessionScenes.Length; s++) BuildSession(s, chambers, hazard, log, errors);
            foreach (var room in rooms) BuildRoom(room, hazard, lamp, klaxon, general, log, errors);
            if (errors.Count > 0) Fail("Doors/Build all: errores al construir (lo demás se guardó)", errors, log, "doors_build.txt");
            log.AppendLine("OK");
            Map01Report.Write("doors_build.txt", log.ToString());
        }

        // ------------------------------------------------------------------ data

        static Dictionary<string, StructureChamber> LoadChambers(List<string> errors)
        {
            var map = new Dictionary<string, StructureChamber>();
            foreach (var id in Map01Paths.ChamberIds)
            {
                try { map[id] = Map01Json.Chamber(id); }
                catch (Exception e) { errors.Add(id + ": no se pudo leer Structure/" + id + ".json (" + e.Message + ")"); }
            }
            return map;
        }

        static List<StructureRoom> LoadRooms(List<string> errors)
        {
            var list = new List<StructureRoom>();
            foreach (var id in Map01Paths.RoomIds)
            {
                try { list.Add(Map01Json.Room(id)); }
                catch (Exception e) { errors.Add(id + ": no se pudo leer Structure/" + id + ".json (" + e.Message + ")"); }
            }
            return list;
        }

        public static DoorsRoomData DoorsData(string roomId)
        {
            Map01Json.TryLoad<DoorsRoomData>(Map01Paths.Data("Doors", roomId), out var data);
            return data;
        }

        public static Vector2 ChamberVano(string cls)
        {
            switch ((cls ?? "").Trim().ToUpperInvariant())
            {
                case "Z": return new Vector2(8f, 6f);
                case "A": case "F": case "F-A": return new Vector2(5f, 4.5f);
                default: throw new InvalidOperationException("clase de esclusa desconocida '" + cls + "' (Z, A o F)");
            }
        }
        public static float ChamberLeaf(string cls) => (cls ?? "").Trim().ToUpperInvariant() == "Z" ? .9f : .6f;

        public static ChamberRole RoleIn(string chamberId, int session)
        {
            if (Map01Paths.SessionStartChamber[session] == chamberId) return ChamberRole.SessionStart;
            if (session + 1 < Map01Paths.SessionStartChamber.Length && Map01Paths.SessionStartChamber[session + 1] == chamberId) return ChamberRole.Departure;
            return ChamberRole.Normal;
        }

        /// Horizontal unit vector from a door center toward the chamber center (dominant axis), i.e. into the chamber.
        public static Vector3 Inward(StructureChamber c, ChamberDoor door)
        {
            Vector3 d = Map01Math.V3(c.worldCenter) - Map01Math.V3(door.center); d.y = 0;
            if (d.sqrMagnitude < 1e-4f) throw new InvalidOperationException(c.id + ": el centro de la puerta coincide con worldCenter");
            return Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
        }

        /// World center and world size of the greybox leaf of a chamber door (inside its band, closed pose).
        public static void ChamberLeafPose(StructureChamber c, ChamberDoor door, out Vector3 center, out Vector3 size)
        {
            var vano = ChamberVano(c.doorClass); float t = ChamberLeaf(c.doorClass);
            var inward = Inward(c, door); var band = door.band;
            float depth = band != null && band.Length >= 3 ? band[2] : t;
            Vector3 p = Map01Math.V3(door.center);
            center = p + inward * (depth * .5f) + Vector3.up * (vano.y * .5f);
            size = Mathf.Abs(inward.x) > .5f ? new Vector3(t, vano.y, vano.x) : new Vector3(vano.x, vano.y, t);
        }

        // ------------------------------------------------------------------ validation (before building)

        static void ValidateChambers(Dictionary<string, StructureChamber> chambers, List<string> errors)
        {
            foreach (var pair in chambers)
            {
                var c = pair.Value; string id = pair.Key;
                if (c == null) { errors.Add(id + ": JSON vacío"); continue; }
                if (c.id != id) errors.Add(id + ": campo id = '" + c.id + "'");
                if (c.worldCenter == null || c.worldCenter.Length < 3) errors.Add(id + ": worldCenter");
                try { ChamberVano(c.doorClass); } catch (Exception e) { errors.Add(id + ": class · " + e.Message); continue; }
                bool needsSource = id != "S00";
                if (needsSource) CheckDoor(c, c.sourceDoor, "sourceDoor", errors);
                CheckDoor(c, c.destinationDoor, "destinationDoor", errors);
                if (id != "S00")
                {
                    if (c.trigger?.center == null || c.trigger.size == null || c.trigger.size.Length < 3) { errors.Add(id + ": trigger {center, size}"); continue; }
                    foreach (var door in new[] { c.sourceDoor, c.destinationDoor })
                    {
                        if (door?.center == null) continue;
                        float gap = TriggerGap(c, door);
                        if (gap < MinTriggerGap - .05f) errors.Add(id + ": trigger a " + gap.ToString("0.00") + " m de una hoja (AT-1 exige ≥ 1,0)");
                    }
                }
                foreach (int s in Map01Paths.SessionsOfChamber(id))
                    if (!File.Exists(Map01Paths.ScenePath(Map01Paths.SessionScenes[s]))) errors.Add(id + ": falta la sesión " + Map01Paths.SessionScenes[s] + " (ejecutar Sessions/1 Create session scenes)");
                if (Map01Paths.SessionStartChamber.Contains(id) && c.sessionStart == null) errors.Add(id + ": sessionStart (playerStart/station) en una cámara de arranque");
            }
        }

        static void CheckDoor(StructureChamber c, ChamberDoor door, string field, List<string> errors)
        {
            if (door?.center == null || door.center.Length < 3) { errors.Add(c.id + ": " + field + ".center"); return; }
            var vano = ChamberVano(c.doorClass);
            if (door.band == null || door.band.Length < 3) { errors.Add(c.id + ": " + field + ".band [w,h,d]"); return; }
            if (door.band[0] + .01f < vano.x || door.band[1] + .01f < vano.y) errors.Add(c.id + ": " + field + ".band " + door.band[0] + "×" + door.band[1] + " < vano " + vano.x + "×" + vano.y);
            try { Inward(c, door); } catch (Exception e) { errors.Add(c.id + ": " + field + " · " + e.Message); }
        }

        /// Distance along the chamber axis between the chamber-side face of a leaf and the nearest face of the trigger (AT-1).
        public static float TriggerGap(StructureChamber c, ChamberDoor door)
        {
            ChamberLeafPose(c, door, out var center, out var size);
            var inward = Inward(c, door); int axis = Mathf.Abs(inward.x) > .5f ? 0 : 2;
            float leafFace = center[axis] + inward[axis] * size[axis] * .5f;
            var tc = Map01Math.V3(c.trigger.center); var ts = Map01Math.V3(c.trigger.size);
            float triggerFace = tc[axis] - inward[axis] * ts[axis] * .5f;
            return (triggerFace - leafFace) * inward[axis];
        }

        static void ValidateRooms(List<StructureRoom> rooms, List<string> errors)
        {
            foreach (var room in rooms)
            {
                if (room == null) continue;
                if (string.IsNullOrEmpty(room.scene)) { errors.Add(room.id + ": scene"); continue; }
                if (room.worldOrigin == null || room.worldOrigin.Length < 3) errors.Add(room.id + ": worldOrigin");
                var doors = ShutterDoors(room);
                if (doors.Count == 0) continue;
                if (!File.Exists(Map01Paths.ScenePath(room.scene))) { errors.Add(room.id + ": falta la escena " + room.scene + " (ejecutar Structure/Build all)"); continue; }
                var data = DoorsData(room.id);
                foreach (var d in doors)
                {
                    string tag = room.id + "/" + (d.id ?? "?");
                    if (string.IsNullOrEmpty(d.id)) errors.Add(tag + ": id");
                    string cls = (d.doorClass ?? "").ToUpperInvariant();
                    if (cls != "P" && cls != "B") continue; // E (HatchDoor) y F son de F3
                    if (d.center == null || d.center.Length < 3) { errors.Add(tag + ": center"); continue; }
                    if (d.vano == null || d.vano.Length < 2 || d.vano[0] < 3.9f || d.vano[1] < 3.9f) { errors.Add(tag + ": vano [w,h] (≥ 4 × 4)"); continue; }
                    if (DoorAxis(d) < 0) { errors.Add(tag + ": normal horizontal (±x o ±z)"); continue; }
                    try
                    {
                        var plan = Plan(room, d, data);
                        foreach (var zone in room.spawnZones ?? new List<SpawnZone>())
                        {
                            if (zone?.min == null || zone.max == null) continue;
                            var zb = new Bounds(); zb.SetMinMax(Map01Math.V3(zone.min), Map01Math.V3(zone.max) + Vector3.up * .1f);
                            if (plan.sealedLocal.Intersects(zb)) errors.Add(tag + ": la zona de spawn " + zone.id + " cae dentro del volumen sellado '" + plan.volumeId + "' (R-P4)");
                        }
                        if (room.arena?.firstRoom?.center == null) errors.Add(tag + ": arena.firstRoom (bounds de la arena)");
                    }
                    catch (Exception e) { errors.Add(tag + ": " + e.Message); }
                }
            }
        }

        /// Interior doors R4 builds in F2: classes P and B, except in the chase room (its gates belong to ChaseCorridorEncounter).
        public static List<InternalDoor> ShutterDoors(StructureRoom room)
        {
            if (room?.internalDoors == null || room.chase != null) return new List<InternalDoor>();
            return room.internalDoors.Where(d => d != null).ToList();
        }

        // ------------------------------------------------------------------ shutter plan (behaviour + sealed volume)

        /// 0 = door normal along X, 2 = along Z, -1 = invalid.
        public static int DoorAxis(InternalDoor d)
        {
            if (d.normal == null || d.normal.Length < 3) return -1;
            float x = Mathf.Abs(d.normal[0]), z = Mathf.Abs(d.normal[2]);
            if (x < .5f && z < .5f) return -1;
            return x >= z ? 0 : 2;
        }

        public static ShutterPlan Plan(StructureRoom room, InternalDoor d, DoorsRoomData data)
        {
            var plan = new ShutterPlan { door = d };
            string cls = (d.doorClass ?? "").ToUpperInvariant();
            Vector3 c = Map01Math.V3(d.center); int axis = DoorAxis(d), lat = axis == 0 ? 2 : 0;
            float tolerance = cls == "B" ? 3.5f : 1.0f, best = float.MaxValue;
            DoorSealedVolume match = null; Bounds matchBox = default;
            foreach (var sv in data?.sealedVolumes ?? new List<DoorSealedVolume>())
            {
                if (sv?.min == null || sv.max == null) throw new InvalidOperationException("Doors/" + room.id + ".json: sealedVolume " + sv?.id + " sin min/max");
                var box = new Bounds(); box.SetMinMax(Map01Math.V3(sv.min), Map01Math.V3(sv.max));
                bool inLateral = c[lat] >= box.min[lat] - 1f && c[lat] <= box.max[lat] + 1f;
                bool inHeight = c.y >= box.min.y - 1f && c.y <= box.max.y;
                float along = Mathf.Min(Mathf.Abs(c[axis] - box.min[axis]), Mathf.Abs(c[axis] - box.max[axis]));
                if (inLateral && inHeight && along <= tolerance && along < best) { best = along; match = sv; matchBox = box; }
            }
            if (match != null)
            {
                plan.source = "Doors/" + room.id + ".json"; plan.volumeId = match.id; plan.sealedLocal = matchBox;
                plan.kind = string.IsNullOrEmpty(match.kind) ? (cls == "B" ? "boss" : "portal") : match.kind.ToLowerInvariant();
                KindDefaults(plan);
                if (match.closeOnWave.HasValue) plan.closeOnWave = match.closeOnWave.Value;
                if (match.openOnWave.HasValue) plan.openOnWave = match.openOnWave.Value;
                if (match.startClosed.HasValue) plan.startClosed = match.startClosed.Value;
                if (match.klaxon.HasValue) plan.klaxon = match.klaxon.Value;
                if (match.warningSeconds.HasValue) plan.warningSeconds = match.warningSeconds.Value;
                if (match.minPlayerDistance.HasValue) plan.minPlayerDistance = match.minPlayerDistance.Value;
            }
            else
            {
                // Fallback: R1's behaviour string + an automatic portal volume between the portal plane and the entry door.
                plan.source = "behaviour de Structure (sin volumen en Doors/" + room.id + ".json)";
                plan.kind = cls == "B" ? "boss" : "portal"; KindDefaults(plan);
                ParseBehaviour(d.behaviour, plan);
                plan.sealedLocal = AutoPortalVolume(room, d, axis);
                plan.volumeId = "auto";
            }
            if (plan.sealedLocal.size.x <= .1f || plan.sealedLocal.size.y <= .1f || plan.sealedLocal.size.z <= .1f)
                throw new InvalidOperationException("volumen sellado vacío (" + plan.volumeId + ")");
            return plan;
        }

        public static void KindDefaults(ShutterPlan p)
        {
            switch (p.kind)
            {
                case "wing": p.closeOnWave = 0; p.openOnWave = -1; p.startClosed = false; p.requirePlayerInArena = false; p.minPlayerDistance = 0; p.warningSeconds = 1f; p.klaxon = true; break;
                case "cage": p.closeOnWave = -1; p.openOnWave = 2; p.startClosed = true; p.requirePlayerInArena = false; p.minPlayerDistance = 0; p.warningSeconds = 0; p.klaxon = false; break;
                case "boss": p.closeOnWave = 0; p.openOnWave = -1; p.startClosed = false; p.requirePlayerInArena = true; p.minPlayerDistance = 4; p.warningSeconds = 1f; p.klaxon = true; break;
                case "portal": p.closeOnWave = 0; p.openOnWave = -1; p.startClosed = false; p.requirePlayerInArena = true; p.minPlayerDistance = 4; p.warningSeconds = .5f; p.klaxon = true; break;
                default: throw new InvalidOperationException("kind desconocido '" + p.kind + "' (portal, wing, cage, boss)");
            }
        }

        /// R1 tokens: closeOnWave<N>, openOnWave<N>, openOnComplete, startClosed, neverClose (separated by '_').
        public static void ParseBehaviour(string behaviour, ShutterPlan p)
        {
            if (string.IsNullOrEmpty(behaviour)) return;
            foreach (var token in behaviour.Split('_'))
            {
                var m = Regex.Match(token, @"^closeOnWave(\d+)$"); if (m.Success) { p.closeOnWave = int.Parse(m.Groups[1].Value); continue; }
                m = Regex.Match(token, @"^openOnWave(\d+)$"); if (m.Success) { p.openOnWave = int.Parse(m.Groups[1].Value); continue; }
                if (token == "openOnComplete") { p.openOnWave = -1; continue; }
                if (token == "startClosed") { p.startClosed = true; continue; }
                if (token == "neverClose") { p.closeOnWave = -1; continue; }
            }
        }

        static Bounds AutoPortalVolume(StructureRoom room, InternalDoor d, int axis)
        {
            var entry = room.doors?.FirstOrDefault(x => x?.role == "entry" && x.center != null);
            if (entry == null || room.interior?.min == null || room.interior.max == null)
                throw new InvalidOperationException("sin volumen sellado: añadir sealedVolumes en Data/Doors/" + room.id + ".json (no hay entrada ni interior para calcularlo)");
            Vector3 c = Map01Math.V3(d.center), e = Map01Math.V3(entry.center), imin = Map01Math.V3(room.interior.min), imax = Map01Math.V3(room.interior.max);
            if (Mathf.Abs(c[axis] - e[axis]) < .5f) throw new InvalidOperationException("sin volumen sellado: la P no está entre la entrada y la arena; añadir sealedVolumes en Data/Doors/" + room.id + ".json");
            Vector3 min = imin, max = imax;
            min[axis] = Mathf.Min(c[axis], e[axis]); max[axis] = Mathf.Max(c[axis], e[axis]);
            var b = new Bounds(); b.SetMinMax(min, max); return b;
        }

        // ------------------------------------------------------------------ sessions

        static void BuildSession(int s, Dictionary<string, StructureChamber> chambers, Material hazard, StringBuilder log, List<string> errors)
        {
            var scene = Map01Scenes.OpenSession(s);
            var flow = SessionFlow(scene);
            log.AppendLine("== " + Map01Paths.SessionScenes[s] + (flow ? "" : " (SIN CampaignFlow)"));
            if (!flow) { errors.Add(Map01Paths.SessionScenes[s] + ": no hay CampaignFlow (ejecutar Sessions/1)"); return; }
            var stages = Map01Paths.SessionStages[s];
            foreach (var id in Map01Paths.ChamberIds)
            {
                if (!Map01Paths.SessionsOfChamber(id).Contains(s) || !chambers.TryGetValue(id, out var c)) continue;
                try
                {
                    var role = RoleIn(id, s);
                    var root = Map01Hierarchy.EnsureChamberRoot(scene, c);
                    var layer = Map01Hierarchy.Layer(root, Map01Layers.Doors);
                    Map01Hierarchy.ClearLayer(layer);
                    GameObject source = null, destination;
                    if (id != "S00") source = BuildChamberDoor(layer, c, c.sourceDoor, SourceName, hazard);
                    destination = BuildChamberDoor(layer, c, c.destinationDoor, DestinationName, hazard);
                    string extra = "";
                    if (role == ChamberRole.Normal)
                    {
                        int index = Array.IndexOf(stages, "R" + id.Substring(1));
                        if (index < 0 || index >= stages.Length - 1) throw new InvalidOperationException("la cámara no une dos etapas de " + Map01Paths.SessionScenes[s]);
                        var trigger = Trigger(layer, c, AirlockName);
                        var airlock = trigger.AddComponent<CampaignAirlock>();
                        airlock.Configure(flow, index, source, destination);
                        airlock.ConfigureExtras(new GameObject[0], root.GetComponentInChildren<AirlockCycle>(true));
                        EditorUtility.SetDirty(airlock);
                        extra = "CampaignAirlock sourceStage=" + index;
                    }
                    else if (role == ChamberRole.Departure)
                    {
                        var trigger = Trigger(layer, c, HandoffName);
                        var handoff = trigger.AddComponent<Map01SessionHandoff>();
                        handoff.ConfigureDeparture(flow, source, destination, root.GetComponentInChildren<AirlockCycle>(true), Map01Paths.SessionScenes[s + 1]);
                        EditorUtility.SetDirty(handoff);
                        extra = "Map01SessionHandoff → " + Map01Paths.SessionScenes[s + 1];
                    }
                    else extra = "arranque: DestinationDoor = initialDoor" + (source ? "; SourceDoor sellada" : "");
                    log.AppendLine("  " + id + " [" + c.doorClass + "] " + role + " · " + extra);
                }
                catch (Exception e) { errors.Add(Map01Paths.SessionScenes[s] + "/" + id + ": " + e.Message); }
            }
            Map01Scenes.Save(scene);
        }

        public static CampaignFlow SessionFlow(Scene scene)
        {
            var flows = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignFlow>(true)).ToArray();
            if (flows.Length > 1) throw new InvalidOperationException(scene.name + ": hay " + flows.Length + " CampaignFlow");
            return flows.Length == 1 ? flows[0] : null;
        }

        static GameObject BuildChamberDoor(Transform layer, StructureChamber c, ChamberDoor door, string name, Material hazard)
        {
            ChamberLeafPose(c, door, out var center, out var size);
            var go = Slab(layer, name, center, size, hazard);
            go.SetActive(true); // closed pose (AirlockDoorMotion captures it on the first Set)
            return go;
        }

        static GameObject Trigger(Transform layer, StructureChamber c, string name)
        {
            var go = new GameObject(name, typeof(BoxCollider), typeof(Rigidbody));
            Undo.RegisterCreatedObjectUndo(go, "Map01 airlock trigger");
            go.transform.SetParent(layer, false);
            go.transform.SetPositionAndRotation(Map01Math.V3(c.trigger.center), Quaternion.identity);
            var box = go.GetComponent<BoxCollider>(); box.size = Map01Math.V3(c.trigger.size); box.center = Vector3.zero; box.isTrigger = true;
            var body = go.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; // CampaignAuthoring.SetTrigger pattern
            return go;
        }

        // ------------------------------------------------------------------ rooms

        static void BuildRoom(StructureRoom room, Material hazard, Material lamp, AudioClip klaxon, AudioMixerGroup general, StringBuilder log, List<string> errors)
        {
            var doors = ShutterDoors(room);
            if (room.chase != null && (room.internalDoors?.Count ?? 0) > 0) log.AppendLine("== " + room.id + ": compuertas de persecución → ChaseCorridorEncounter (R3); no se construyen aquí");
            string path = Map01Paths.ScenePath(room.scene);
            if (!File.Exists(path)) { if (doors.Count > 0) errors.Add(room.id + ": falta la escena " + path); return; }
            var scene = Map01Scenes.OpenRoom(room, false);
            var root = Map01Hierarchy.EnsureRoomRoot(scene, room);
            var layer = Map01Hierarchy.Layer(root, Map01Layers.Doors);
            Map01Hierarchy.ClearLayer(layer);
            var data = DoorsData(room.id);
            var slots = SlotPositions(root);
            log.AppendLine("== " + room.id + " (" + room.scene + ") · " + doors.Count + " puertas internas · " + slots.Count + " slots de Estructura");
            Vector3 origin = Map01Math.V3(room.worldOrigin);
            var arena = room.arena?.firstRoom?.center != null ? Map01Math.ToWorld(Map01Math.ToBounds(room.arena.firstRoom), room) : new Bounds(Vector3.zero, Vector3.zero);
            foreach (var d in doors)
            {
                string cls = (d.doorClass ?? "").ToUpperInvariant();
                if (cls != "P" && cls != "B") { log.AppendLine("  " + d.id + " [" + cls + "] no se construye en F2 (" + (cls == "E" ? "HatchDoor es de F3" : "clase sin uso") + ")"); continue; }
                try
                {
                    var plan = Plan(room, d, data);
                    foreach (var slot in slots) if (plan.sealedLocal.Contains(slot.Value)) throw new InvalidOperationException("el slot " + slot.Key + " está dentro del volumen sellado '" + plan.volumeId + "' (R-P4)");
                    BuildShutter(layer, origin, plan, arena, hazard, lamp, plan.klaxon ? klaxon : null, general);
                    log.AppendLine("  " + d.id + " [" + cls + "] " + plan.kind + " · volumen " + plan.volumeId + " (" + plan.source + ") · cierra W" + plan.closeOnWave + " · abre " + (plan.openOnWave < 0 ? "Completed" : "W" + plan.openOnWave) + (plan.startClosed ? " · empieza cerrada" : "") + " · aviso " + plan.warningSeconds + " s");
                    var r1 = new ShutterPlan { kind = plan.kind }; KindDefaults(r1); ParseBehaviour(d.behaviour, r1);
                    if (!string.IsNullOrEmpty(d.behaviour) && (r1.closeOnWave != plan.closeOnWave || r1.openOnWave != plan.openOnWave || r1.startClosed != plan.startClosed))
                        log.AppendLine("    aviso: behaviour de Structure '" + d.behaviour + "' difiere del plan de Puertas (manda Puertas, valores de R3)");
                }
                catch (Exception e) { errors.Add(room.id + "/" + d.id + ": " + e.Message); }
            }
            Map01Scenes.Save(scene);
        }

        /// Structure slot markers (room-local positions), keyed "zone/slot".
        public static Dictionary<string, Vector3> SlotPositions(Transform roomRoot)
        {
            var result = new Dictionary<string, Vector3>();
            var structure = roomRoot.Find(Map01Layers.Structure);
            var slotsRoot = structure ? Map01Markers.Find(structure, Map01Markers.Slots) : null;
            if (!slotsRoot) return result;
            foreach (Transform zone in slotsRoot)
                foreach (Transform slot in zone)
                    result[zone.name + "/" + slot.name] = roomRoot.InverseTransformPoint(slot.position);
            return result;
        }

        static void BuildShutter(Transform layer, Vector3 origin, ShutterPlan plan, Bounds arenaWorld, Material hazard, Material lampMaterial, AudioClip klaxon, AudioMixerGroup general)
        {
            var d = plan.door; int axis = DoorAxis(d);
            float w = d.vano[0], h = d.vano[1], t = (d.doorClass ?? "").ToUpperInvariant() == "B" ? 1f : .3f;
            Vector3 local = Map01Math.V3(d.center);
            var host = new GameObject(d.id);
            Undo.RegisterCreatedObjectUndo(host, "Map01 shutter");
            host.transform.SetParent(layer, false);
            host.transform.SetPositionAndRotation(origin + local, Quaternion.identity);

            Vector3 size = axis == 0 ? new Vector3(t, h, w) : new Vector3(w, h, t);
            Vector3 doorCenter = host.transform.position + Vector3.up * (h * .5f);
            var door = Slab(host.transform, ShutterDoorName, doorCenter, size, hazard);
            var obstacle = door.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = Vector3.zero; obstacle.size = Vector3.one; // scaled by the transform
            obstacle.carving = true; obstacle.carveOnlyStationary = true; obstacle.carvingTimeToStationary = .1f; obstacle.carvingMoveThreshold = .1f;
            door.SetActive(plan.startClosed); // closed pose either way; inactive = open (AirlockDoorMotion contract)

            // Warning strip above the lintel, on the arena face (the side away from the sealed volume) so the player sees it.
            Vector3 away = Vector3.zero; away[axis] = Mathf.Sign(local[axis] - plan.sealedLocal.center[axis]);
            var lamp = Slab(host.transform, LampName, host.transform.position + Vector3.up * (h + .3f) + away * (t * .5f + .3f), axis == 0 ? new Vector3(.25f, .25f, w) : new Vector3(w, .25f, .25f), lampMaterial);
            UnityEngine.Object.DestroyImmediate(lamp.GetComponent<Collider>());
            var lampRenderer = lamp.GetComponent<MeshRenderer>(); lampRenderer.enabled = false; lampRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            AudioSource audio = null;
            if (klaxon)
            {
                audio = host.AddComponent<AudioSource>();
                audio.playOnAwake = false; audio.loop = false; audio.spatialBlend = 1f; audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.minDistance = 3f; audio.maxDistance = 35f; audio.outputAudioMixerGroup = general;
            }

            var sealedWorld = new Bounds(plan.sealedLocal.center + origin, plan.sealedLocal.size);
            var shutter = host.AddComponent<ArenaShutter>();
            shutter.Configure(door, plan.closeOnWave, plan.openOnWave, plan.startClosed, plan.requirePlayerInArena, plan.minPlayerDistance, plan.warningSeconds,
                sealedWorld, doorCenter, size * .5f, arenaWorld, new Renderer[] { lampRenderer }, audio, klaxon);
            EditorUtility.SetDirty(shutter);
        }

        // ------------------------------------------------------------------ helpers

        /// Greybox box with Renderer + solid BoxCollider on the same object (AirlockDoorMotion.Height reads the root Renderer).
        public static GameObject Slab(Transform parent, string name, Vector3 worldCenter, Vector3 worldSize, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Map01 door greybox");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldCenter, Quaternion.identity);
            go.transform.localScale = worldSize; // parents are unscaled Map01 roots/layers
            if (material) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) Debug.LogWarning("[Map01] Doors: no se encontró " + path);
            return asset;
        }

        static AudioMixerGroup GeneralGroup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath); // shared global mixer: only referenced, never modified
            return mixer ? mixer.FindMatchingGroups("General").FirstOrDefault(g => g.name == "General") : null;
        }

        public static void Fail(string title, List<string> errors, StringBuilder log, string report)
        {
            log.AppendLine("ERRORES (" + errors.Count + "):");
            foreach (var e in errors) log.AppendLine("  - " + e);
            Map01Report.Write(report, log.ToString());
            throw new InvalidOperationException(title + " · " + errors.Count + " error(es). Primero: " + errors[0] + " · informe: " + Map01Paths.ReportRoot + "/" + report);
        }
    }
}
