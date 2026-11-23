using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor.Map01
{
    /// Map01 F2 shared contract (owner: R0 coordinator). Every *Authoring script of Map01 compiles against this file.
    /// Roles may NOT edit it: ask R0. Spec: Docs/IronRequiem/Map01/MAP01_DESIGN.md §3-§4 and 01_ESTRUCTURA.md §12.
    public static class Map01Paths
    {
        public const string SceneFolder = "Assets/IronRequiem/Scenes/Map01_Bunker";
        public const string DataRoot = "Assets/IronRequiem/Levels/Map01/Data";
        public const string GeneratedRoot = "Assets/IronRequiem/Levels/Map01/Generated";
        public const string ReportRoot = "Tools/IronRequiem/Reports/Map01/F2";
        public const string CampaignSource = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        public const string MenuRoot = "Tools/Iron Requiem/Map01/";

        public static string Data(string role, string id) => DataRoot + "/" + role + "/" + id + ".json";
        public static string ScenePath(string sceneName) => SceneFolder + "/" + sceneName + ".unity";

        /// Approved U2 (four chained sessions). Index 0..3 -> S1..S4.
        public static readonly string[] SessionScenes = { "Map01_Campaign_S1", "Map01_Campaign_S2", "Map01_Campaign_S3", "Map01_Campaign_S4" };
        public static readonly string[][] SessionStages =
        {
            new[] { "R01", "R02", "R03", "R04", "R05" },
            new[] { "R06", "R07", "R08", "R09", "R10", "R11", "R12", "R13" },
            new[] { "R14", "R15", "R16", "R17" },
            new[] { "R18" },
        };
        /// Chamber where each session starts (S00 = vestibule) and the chambers whose destination door is that session's initialDoor.
        public static readonly string[] SessionStartChamber = { "S00", "C05", "C13", "C17" };

        public static IEnumerable<string> RoomIds => Enumerable.Range(1, 18).Select(i => "R" + i.ToString("00"));
        public static IEnumerable<string> ChamberIds => new[] { "S00" }.Concat(Enumerable.Range(1, 17).Select(i => "C" + i.ToString("00")));
        public static int SessionOfRoom(string roomId) { for (int s = 0; s < SessionStages.Length; s++) if (SessionStages[s].Contains(roomId)) return s; return -1; }
        /// Session whose scene hosts chamber C<NN> (the session of its source room). S00 -> S1. A start chamber of session k (C05, C13, C17)
        /// exists in BOTH the session that ends there (as exit, handled by Map01SessionHandoff) and session k (as start vestibule).
        public static int[] SessionsOfChamber(string chamberId)
        {
            if (chamberId == "S00") return new[] { 0 };
            int n = int.Parse(chamberId.Substring(1));
            int from = SessionOfRoom("R" + n.ToString("00")), to = SessionOfRoom("R" + (n + 1).ToString("00"));
            return from == to ? new[] { from } : new[] { from, to };
        }
    }

    public static class Map01Layers
    {
        public const string Structure = "[Structure]", Decor = "[Decor]", Encounters = "[Encounters]", Doors = "[Doors]", Lighting = "[Lighting]";
        public static readonly string[] All = { Structure, Decor, Encounters, Doors, Lighting };
        /// Root object of a room scene: "Map01_R01" placed at worldOrigin (yaw 0). Root of a chamber in a session scene: "C05".
        public static string RoomRootName(string roomId) => "Map01_" + roomId;
    }

    /// Marker convention written by StructureAuthoring under <room root>/[Structure]/Markers (local coordinates of the room root):
    ///   Markers/Slots/<zoneId>/<zoneId>_<n>    spawn slots (n from 1)        Markers/Anchors/<anchorId>   light anchors (R01.A1…)
    ///   Markers/Openings/<doorId>              door centers (R01_in…)        Markers/Arena                 BoxCollider (disabled) = firstRoom
    ///   Markers/Landmark                       BoxCollider (disabled)        Markers/Release               BoxCollider trigger volume (respites)
    /// In session scenes, under <chamberId>/[Structure]/Markers: Anchors/<C05.CY1…>, Openings/SourceDoor|DestinationDoor, Trigger, PlayerStart, Station.
    public static class Map01Markers
    {
        public const string Root = "Markers", Slots = "Slots", Anchors = "Anchors", Openings = "Openings", Arena = "Arena", Landmark = "Landmark", Release = "Release";
        public const string Trigger = "Trigger", PlayerStart = "PlayerStart", Station = "Station";
        public static Transform Find(Transform layer, params string[] path)
        {
            var t = layer.Find(Root); if (!t) return null;
            foreach (var p in path) { t = t.Find(p); if (!t) return null; }
            return t;
        }
        public static Transform[] SlotsOf(Transform structureLayer, string zoneId)
        {
            var z = Find(structureLayer, Slots, zoneId);
            return z ? z.Cast<Transform>().OrderBy(c => c.name, StringComparer.Ordinal).ToArray() : new Transform[0];
        }
    }

    public static class Map01Json
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        { MissingMemberHandling = MissingMemberHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented };
        public static T Load<T>(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Map01 data missing: " + path);
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings);
        }
        public static bool TryLoad<T>(string path, out T value) { value = default; if (!File.Exists(path)) return false; value = Load<T>(path); return true; }
        public static void Save<T>(string path, T value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonConvert.SerializeObject(value, Settings)); }
        public static StructureRoom Room(string id) => Load<StructureRoom>(Map01Paths.Data("Structure", id));
        public static StructureChamber Chamber(string id) => Load<StructureChamber>(Map01Paths.Data("Structure", id));
    }

    public static class Map01Scenes
    {
        /// Never save over a scene the user modified: refuse to switch scenes while a non-Map01 scene has unsaved changes.
        public static void GuardForeignDirtyScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Map01: salir de Play Mode antes de autorar.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty && !s.path.StartsWith(Map01Paths.SceneFolder + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Map01: la escena abierta '" + s.path + "' tiene cambios sin guardar. No se guarda ni se descarta: ciérrala tú primero.");
            }
        }
        /// Opens (Single) the scene of a room; creates an empty one if missing and allowed. Saves any dirty Map01 scene first.
        public static Scene OpenRoom(StructureRoom room, bool createIfMissing)
        {
            GuardForeignDirtyScenes(); SaveDirtyMap01();
            string path = Map01Paths.ScenePath(room.scene);
            if (File.Exists(path)) return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!createIfMissing) throw new FileNotFoundException("Map01 scene missing: " + path);
            Directory.CreateDirectory(Map01Paths.SceneFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }
        /// Opens (Single) a session scene. Sessions are created by Map01SessionAuthoring (R4), never here.
        public static Scene OpenSession(int session)
        {
            GuardForeignDirtyScenes(); SaveDirtyMap01();
            string path = Map01Paths.ScenePath(Map01Paths.SessionScenes[session]);
            if (!File.Exists(path)) throw new FileNotFoundException("Map01 session missing (run Map01SessionAuthoring first): " + path);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        public static void Save(Scene scene) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        public static void SaveDirtyMap01()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty && s.path.StartsWith(Map01Paths.SceneFolder + "/", StringComparison.Ordinal)) EditorSceneManager.SaveScene(s);
            }
        }
    }

    public static class Map01Hierarchy
    {
        /// Root "Map01_R01" at worldOrigin with the five layer children. Never moves an existing root's children.
        public static Transform EnsureRoomRoot(Scene scene, StructureRoom room)
        {
            string name = Map01Layers.RoomRootName(room.id);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (!root) { root = new GameObject(name); Undo.RegisterCreatedObjectUndo(root, "Map01 root"); SceneManager.MoveGameObjectToScene(root, scene); }
            root.transform.SetPositionAndRotation(Map01Math.V3(room.worldOrigin), Quaternion.identity);
            root.transform.localScale = Vector3.one;
            foreach (var layer in Map01Layers.All) EnsureChild(root.transform, layer);
            return root.transform;
        }
        /// Chamber root "C05" (or "S00") in a session scene, at worldCenter, with [Structure], [Doors], [Lighting].
        public static Transform EnsureChamberRoot(Scene scene, StructureChamber chamber)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == chamber.id);
            if (!root) { root = new GameObject(chamber.id); Undo.RegisterCreatedObjectUndo(root, "Map01 chamber"); SceneManager.MoveGameObjectToScene(root, scene); }
            root.transform.SetPositionAndRotation(Map01Math.V3(chamber.worldCenter), Quaternion.identity);
            EnsureChild(root.transform, Map01Layers.Structure); EnsureChild(root.transform, Map01Layers.Doors); EnsureChild(root.transform, Map01Layers.Lighting);
            return root.transform;
        }
        public static Transform EnsureChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t) return t;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Map01 layer");
            go.transform.SetParent(parent, false);
            return go.transform;
        }
        /// Deletes every child of ONE layer (idempotent rebuild). Never touches other layers.
        public static void ClearLayer(Transform layer)
        {
            if (!Map01Layers.All.Contains(layer.name)) throw new InvalidOperationException("ClearLayer only on a Map01 layer root, got " + layer.name);
            for (int i = layer.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(layer.GetChild(i).gameObject);
        }
        public static Transform Layer(Transform root, string layer) => root.Find(layer) ?? throw new InvalidOperationException(root.name + " missing layer " + layer);
    }

    public static class Map01Math
    {
        public static Vector3 V3(float[] a) => a == null || a.Length < 3 ? Vector3.zero : new Vector3(a[0], a[1], a[2]);
        public static Vector2 V2(float[] a) => a == null || a.Length < 2 ? Vector2.zero : new Vector2(a[0], a[1]);
        public static UnityEngine.Bounds ToBounds(MinMax m) { var b = new UnityEngine.Bounds(); b.SetMinMax(V3(m.min), V3(m.max)); return b; }
        public static UnityEngine.Bounds ToBounds(CenterSize c) => new UnityEngine.Bounds(V3(c.center), V3(c.size));
        /// Room-local bounds -> world bounds (yaw 0 everywhere in Map01).
        public static UnityEngine.Bounds ToWorld(UnityEngine.Bounds local, StructureRoom room) => new UnityEngine.Bounds(local.center + V3(room.worldOrigin), local.size);
    }

    public static class Map01Report
    {
        public static string Write(string name, string text)
        {
            Directory.CreateDirectory(Map01Paths.ReportRoot);
            string path = Map01Paths.ReportRoot + "/" + name;
            File.WriteAllText(path, text);
            Debug.Log("[Map01] " + name + " -> " + path);
            return path;
        }
    }

    // ---------------- Structure data model (01_ESTRUCTURA.md §12.1). Local coordinates of the room unless stated. ----------------
    public sealed class MinMax { public float[] min, max; }
    public sealed class CenterSize { public float[] center, size; }
    public sealed class Level { public string id; public float y; }
    public sealed class Ceiling { public MinMax region; public float y; }
    public sealed class DoorData
    {
        public string id, role; [JsonProperty("class")] public string doorClass;
        public float[] center, normal, vano, band; public MinMax freeVolume; public string airlock;
    }
    public sealed class PortalData { public float plane; public string axis; public float[] vano; public bool shutter; }
    public sealed class Block { public string id, type; public float[] center, size; public string rise, material, collision, tag; }
    public sealed class FreeVolume { public string id, kind; public float[] min, max; }
    public sealed class SpawnZone { public string id; public float[] min, max; public int slots; public string kind, rounds; public List<float[]> points; }
    public sealed class LandmarkData { public float[] min, max; public string note; }
    public sealed class LightAnchor
    {
        public string id, kind; public float[] pos, normal; public float maxRange; public bool shadowCandidate; public string activeWhen, owner;
    }
    public sealed class LightBudget { [JsonProperty("static")] public int staticLights; public int shadow, transient; }
    public sealed class ArenaData { public CenterSize firstRoom, secondRoom; }
    public sealed class Hazard
    {
        public string type, id; // gap | drop
        public float zNear, zFar, xMin, xMax, depth, upperFloorY, halfWidth; public List<float[]> returnsNear, returnsFar;
        public string edge; public float height; public bool rail;
    }
    public sealed class AmbushData { public float activationZ; public List<float[]> points; }
    /// Chase (R10 only). WORLD coordinates where ChaseCorridorEncounter uses them (finishZ, activationZ, courseOffset).
    public sealed class ChaseData
    {
        public float[] courseOffset, sawSpawn; public float halfWidth, finishZ; public CenterSize trigger, entranceDoor, exitDoor; public List<AmbushData> ambushes;
    }
    public sealed class InternalDoor { public string id; [JsonProperty("class")] public string doorClass; public float[] center, normal, vano; public string behaviour; }
    public sealed class NavData { public float agentClimb; public List<MinMax> jumpAreaVolumes; public string notes; }
    public sealed class ReleaseData { public string mode; public MinMax volume; }
    public sealed class PerfData { public float tileMax; public int subspaces; public List<string> occluders; }

    public sealed class StructureRoom
    {
        public string id, name, scene, zone, function, archetype;
        public float[] worldOrigin; public int yaw;
        public MinMax aabbLocal, interior;
        public List<Level> levels; public List<Ceiling> ceilings;
        public List<DoorData> doors; public PortalData portal;
        public List<Block> blocks; public List<FreeVolume> freeVolumes; public List<SpawnZone> spawnZones;
        public LandmarkData landmark; public List<LightAnchor> lightAnchors; public LightBudget lightBudget;
        public ArenaData arena; public List<Hazard> hazards; public ChaseData chase; public List<InternalDoor> internalDoors;
        public NavData nav; public ReleaseData release; public PerfData perf;
    }

    public sealed class ChamberDoor { public float[] center, band; }
    public sealed class SteamVent { public string id; public float[] pos; }
    /// sessionStart: WORLD coordinates (01_ESTRUCTURA §12.1 table). Present in S00, C05, C13, C17.
    public sealed class SessionStartData { public MinMax station; public float[] playerStart; public float playerYaw; }
    /// Chamber S00 / C<NN>. WORLD coordinates (worldCenter, door centers, trigger, anchors), like the §12.3 example.
    public sealed class StructureChamber
    {
        public string id, from, to; [JsonProperty("class")] public string doorClass; public string axis;
        public float[] worldCenter, aabbSize, chamberInterior; public float floorY;
        public ChamberDoor sourceDoor, destinationDoor; public CenterSize trigger;
        public List<LightAnchor> lightAnchors; public List<SteamVent> steamVents; public SessionStartData sessionStart;
    }
}
