using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor.Map01
{
    /// Read-only checks of R4's F2 work (sessions + doors). Opens each Map01 scene, never modifies or saves anything.
    /// Writes Tools/IronRequiem/Reports/Map01/F2/doors_validate.txt and throws at the end if any error was found.
    public static class DoorValidation
    {
        [MenuItem(Map01Paths.MenuRoot + "Doors/Validate all")]
        public static void ValidateAll()
        {
            Map01Scenes.GuardForeignDirtyScenes();
            var log = new StringBuilder("Map01 Doors/Validate all · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            var errors = new List<string>(); int warnings = 0;
            void Error(string m) { errors.Add(m); log.AppendLine("  ERROR " + m); }
            void Warn(string m) { warnings++; log.AppendLine("  aviso " + m); }

            var build = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToHashSet();
            for (int k = 0; k < Map01Paths.SessionScenes.Length; k++)
            {
                string name = Map01Paths.SessionScenes[k], path = Map01Paths.ScenePath(name);
                log.AppendLine("== " + name);
                if (!File.Exists(path)) { Error(name + ": no existe"); continue; }
                if (!build.Contains(path)) Error(name + ": no está (activa) en Build Settings");
                try { ValidateSession(k, build, Error, Warn); } catch (Exception e) { Error(name + ": " + e.Message); }
            }
            foreach (var id in Map01Paths.RoomIds)
            {
                StructureRoom room;
                try { room = Map01Json.Room(id); } catch (Exception e) { Error(id + ": " + e.Message); continue; }
                string path = Map01Paths.ScenePath(room.scene ?? "");
                if (!File.Exists(path)) { Error(id + ": no existe la escena " + path); continue; }
                if (!build.Contains(path)) Error(id + ": " + room.scene + " no está (activa) en Build Settings");
                var doors = DoorAuthoring.ShutterDoors(room).Where(d => (d.doorClass ?? "").ToUpperInvariant() == "P" || (d.doorClass ?? "").ToUpperInvariant() == "B").ToList();
                if (doors.Count == 0) continue;
                log.AppendLine("== " + id + " · " + doors.Count + " persianas");
                try { ValidateRoom(room, doors, Error, Warn); } catch (Exception e) { Error(id + ": " + e.Message); }
            }
            log.AppendLine("Errores: " + errors.Count + " · avisos: " + warnings);
            Map01Report.Write("doors_validate.txt", log.ToString());
            if (errors.Count > 0) throw new InvalidOperationException("Doors/Validate all: " + errors.Count + " error(es). Primero: " + errors[0] + " · informe: " + Map01Paths.ReportRoot + "/doors_validate.txt");
            Debug.Log("[Map01] Doors/Validate all: OK (" + warnings + " avisos)");
        }

        static void ValidateSession(int k, HashSet<string> build, Action<string> error, Action<string> warn)
        {
            string tag = Map01Paths.SessionScenes[k];
            var scene = Map01Scenes.OpenSession(k);
            var roots = scene.GetRootGameObjects();
            var flow = DoorAuthoring.SessionFlow(scene);
            if (!flow) { error(tag + ": sin CampaignFlow"); return; }
            var so = new SerializedObject(flow);
            bool hangar = Map01SessionAuthoring.StartsInHangar(k);
            int offset = hangar ? 1 : 0;
            if (so.FindProperty("prologueStages").intValue != offset) error(tag + ": prologueStages ≠ " + offset);
            var ids = Map01Paths.SessionStages[k];
            var expected = (hangar ? new[] { Map01SessionAuthoring.HangarStage } : new string[0]).Concat(ids.Select(id => Map01Json.Room(id).scene)).ToArray();
            if (flow.StageScenes == null || !flow.StageScenes.SequenceEqual(expected)) error(tag + ": stageScenes = [" + string.Join(", ", flow.StageScenes ?? new string[0]) + "], se esperaba [" + string.Join(", ", expected) + "]");
            foreach (var s in expected)
            {
                string path = s == Map01SessionAuthoring.HangarStage ? Map01SessionAuthoring.HangarScenePath : Map01Paths.ScenePath(s);
                if (!build.Contains(path)) error(tag + ": la etapa " + s + " no está en Build Settings");
            }
            var airlocks = flow.Airlocks ?? new CampaignAirlock[0];
            if (airlocks.Length != expected.Length - 1) error(tag + ": airlocks.Length = " + airlocks.Length + ", se esperaba " + (expected.Length - 1));
            for (int i = 0; i < airlocks.Length; i++)
            {
                var a = airlocks[i];
                if (!a) { error(tag + ": airlocks[" + i + "] vacío"); continue; }
                if (a.SourceStage != i) error(tag + ": airlocks[" + i + "].sourceStage = " + a.SourceStage + " ≠ " + i);
                if (hangar && i == 0) { ValidateHangarAirlock(roots, a, tag, error); continue; }
                int r = i - offset; string chamberId = r >= 0 && r < ids.Length ? "C" + ids[r].Substring(1) : "?";
                if (a.transform.root.name != chamberId) error(tag + ": airlocks[" + i + "] no está en " + chamberId);
                CheckTrigger(a.gameObject, tag + "/" + chamberId + "/Airlock", error);
                CheckDoor(a.SourceDoor, tag + "/" + chamberId + "/SourceDoor", true, error);
                CheckDoor(a.DestinationDoor, tag + "/" + chamberId + "/DestinationDoor", true, error);
                CheckGap(chamberId, warn, error);
            }
            // initialDoor (S2-S4) / hangar prologue (S1), and the sealed source door of the start chamber
            string startId = Map01Paths.SessionStartChamber[k];
            var initial = (GameObject)so.FindProperty("initialDoor").objectReferenceValue;
            var startRoot = roots.FirstOrDefault(r => r.name == startId);
            if (!startRoot) error(tag + ": falta el root " + startId);
            else if (hangar)
            {
                if (initial) error(tag + ": initialDoor debe ser null (el hangar se libera con su cinemática)");
                if (!so.FindProperty("startCover").objectReferenceValue) error(tag + ": startCover vacío");
                var s00Door = startRoot.transform.Find(Map01Layers.Doors + "/" + DoorAuthoring.DestinationName);
                CheckDoor(s00Door ? s00Door.gameObject : null, tag + "/S00/DestinationDoor", true, error);
                if (airlocks.Length > 0 && airlocks[0] && s00Door && !(airlocks[0].ExtraDestinationDoors ?? new GameObject[0]).Contains(s00Door.gameObject)) error(tag + ": S00/DestinationDoor no está en extraDestinationDoors de a0");
                var opening = startRoot.transform.Find(Map01Layers.Structure + "/" + Map01Markers.Root + "/" + Map01Markers.Openings + "/SourceDoor");
                if (!opening) warn(tag + ": S00 sin abertura hacia el vestíbulo de a0 (S00.json sourceDoor + Structure/Build all)");
                if (flow.Player)
                {
                    var player = PrefabUtility.GetOutermostPrefabInstanceRoot(flow.Player.gameObject);
                    var pso = player ? new SerializedObject(player.transform) : null;
                    if (pso != null && (pso.FindProperty("m_LocalPosition").prefabOverride || pso.FindProperty("m_LocalRotation").prefabOverride)) error(tag + ": el jugador tiene overrides de posición (en RequiemCampaign no los tiene)");
                }
            }
            else
            {
                var expectedInitial = startRoot.transform.Find(Map01Layers.Doors + "/" + DoorAuthoring.DestinationName);
                if (!expectedInitial || initial != expectedInitial.gameObject) error(tag + ": initialDoor no es " + startId + "/[Doors]/DestinationDoor");
                CheckDoor(initial, tag + "/initialDoor", true, error);
                if (startId != "S00") CheckDoor(startRoot.transform.Find(Map01Layers.Doors + "/" + DoorAuthoring.SourceName)?.gameObject, tag + "/" + startId + "/SourceDoor (sellada)", true, error);
                var chamber = Map01Json.Chamber(startId);
                if (chamber.sessionStart?.playerStart != null && flow.Player)
                {
                    float d = Vector3.Distance(flow.Player.transform.position, Map01Math.V3(chamber.sessionStart.playerStart));
                    if (d > 1.5f) error(tag + ": el jugador está a " + d.ToString("0.0") + " m del PlayerStart");
                }
            }
            // Session chaining
            var handoffs = roots.SelectMany(r => r.GetComponentsInChildren<Map01SessionHandoff>(true)).ToArray();
            if (k + 1 < Map01Paths.SessionScenes.Length)
            {
                string departId = Map01Paths.SessionStartChamber[k + 1];
                var h = handoffs.FirstOrDefault(x => x.Mode == Map01SessionHandoff.HandoffMode.Departure);
                if (handoffs.Length != 1 || !h) error(tag + ": se esperaba 1 Map01SessionHandoff de salida (hay " + handoffs.Length + ")");
                else
                {
                    if (h.NextScene != Map01Paths.SessionScenes[k + 1]) error(tag + ": el handoff carga '" + h.NextScene + "'");
                    if (h.transform.root.name != departId) error(tag + ": el handoff no está en " + departId);
                    CheckTrigger(h.gameObject, tag + "/" + departId + "/Handoff", error);
                    CheckDoor(h.SourceDoor, tag + "/" + departId + "/SourceDoor", true, error);
                    if (!build.Contains(Map01Paths.ScenePath(h.NextScene))) error(tag + ": la sesión siguiente no está en Build Settings");
                    if (roots.SelectMany(r => r.GetComponentsInChildren<CampaignAirlock>(true)).Any(a => a.transform.root.name == departId)) error(tag + ": " + departId + " tiene CampaignAirlock (debe gestionarla el handoff)");
                }
            }
            else
            {
                var h = handoffs.FirstOrDefault(x => x.Mode == Map01SessionHandoff.HandoffMode.EndOfMap);
                if (handoffs.Length != 1 || !h) error(tag + ": se esperaba 1 Map01SessionHandoff de fin de mapa (hay " + handoffs.Length + ")");
                else if (!build.Contains("Assets/FPS/Scenes/" + h.NextScene + ".unity")) warn(tag + ": la escena de victoria '" + h.NextScene + "' no se encontró en Build Settings por esa ruta");
            }
            // Menu, cleanup, station
            foreach (var menu in roots.SelectMany(r => r.GetComponentsInChildren<CombatLabMenu>(true)))
                if (!new SerializedObject(menu).FindProperty("directGameplayEntry").boolValue) error(tag + ": CombatLabMenu.directGameplayEntry = false");
            foreach (var n in hangar ? new[] { "LabSun" } : new[] { "PersistentAirlocks", "LabSun" })
            {
                var go = roots.FirstOrDefault(r => r.name == n);
                if (go && go.activeSelf) error(tag + ": " + n + " sigue activo");
            }
            if (hangar) ValidateOnlyA0Active(roots, tag, error);
            if (k == 0) CheckIntroMenu(warn);
            if (roots.SelectMany(r => r.GetComponentsInChildren<SectorLightingBlend>(true)).Any(b => b.enabled)) error(tag + ": SectorLightingBlend sigue encendido");
            var start = roots.FirstOrDefault(r => r.name == Map01SessionAuthoring.SessionStartRoot);
            var stations = roots.SelectMany(r => r.GetComponentsInChildren<WeaponMountStation>(true)).ToArray();
            if (!start || stations.Length != 1 || !stations[0].transform.IsChildOf(start.transform)) error(tag + ": la WeaponMountStation debe ser única y estar bajo " + Map01SessionAuthoring.SessionStartRoot);
        }

        /// S1: airlocks[0] is the copied a0 (RequiemCampaign HangarTransit/ConnectionChamber), wired by Map01SessionAuthoring.
        static void ValidateHangarAirlock(GameObject[] roots, CampaignAirlock a, string tag, Action<string> error)
        {
            var persistent = roots.FirstOrDefault(r => r.name == "PersistentAirlocks");
            var chamber = persistent ? persistent.transform.Find(Map01SessionAuthoring.HangarChamberPath) : null;
            if (!chamber) { error(tag + ": falta PersistentAirlocks/" + Map01SessionAuthoring.HangarChamberPath); return; }
            if (!a.transform.IsChildOf(chamber) || a.name != "ChamberAirlock") error(tag + ": airlocks[0] no es a0/ChamberAirlock");
            if (!a.gameObject.activeInHierarchy) error(tag + ": a0/ChamberAirlock está inactivo");
            if (!a.SourceDoor || a.SourceDoor.name != "ChamberHangarDoor") error(tag + ": a0.sourceDoor no es ChamberHangarDoor");
            if (!a.DestinationDoor || a.DestinationDoor.name != "ChamberCorridorDoor") error(tag + ": a0.destinationDoor no es ChamberCorridorDoor");
            if (!(a.ExtraDestinationDoors ?? new GameObject[0]).Any(d => d && d.name == "VestibuleShutter")) error(tag + ": a0 sin VestibuleShutter en extraDestinationDoors");
            CheckTrigger(a.gameObject, tag + "/a0/ChamberAirlock", error);
            foreach (var door in new[] { a.SourceDoor, a.DestinationDoor }) if (door && !door.activeInHierarchy) error(tag + ": " + door.name + " inactiva (cerrada = activa)");
        }

        static void ValidateOnlyA0Active(GameObject[] roots, string tag, Action<string> error)
        {
            var persistent = roots.FirstOrDefault(r => r.name == "PersistentAirlocks");
            if (!persistent) { error(tag + ": falta PersistentAirlocks"); return; }
            var transit = persistent.transform.Find("HangarTransit");
            var chamber = persistent.transform.Find(Map01SessionAuthoring.HangarChamberPath);
            foreach (Transform child in persistent.transform) if (child != transit && child.gameObject.activeSelf) error(tag + ": PersistentAirlocks/" + child.name + " está activo (solo a0)");
            if (transit) foreach (Transform child in transit) if (child != chamber && child.gameObject.activeSelf) error(tag + ": HangarTransit/" + child.name + " está activo (solo a0)");
        }

        /// Read-only text check of the protected menu scene (never opened here).
        static void CheckIntroMenu(Action<string> warn)
        {
            var path = Map01SessionAuthoring.IntroMenuPath;
            if (!File.Exists(path)) return;
            var text = File.ReadAllText(path);
            if (!text.Contains("SceneName: " + Map01Paths.SessionScenes[0])) warn("IntroMenu: JUGAR no apunta a " + Map01Paths.SessionScenes[0] + " (Sessions/4 Point menu PLAY to Map01)");
        }

        static void CheckDoor(GameObject door, string tag, bool closed, Action<string> error)
        {
            if (!door) { error(tag + ": vacía"); return; }
            if (!door.GetComponent<Renderer>()) error(tag + ": el root no tiene Renderer (AirlockDoorMotion.Height)");
            var box = door.GetComponent<BoxCollider>();
            if (!box || box.isTrigger) error(tag + ": el root necesita un BoxCollider sólido");
            if (door.activeSelf != closed) error(tag + ": activeSelf = " + door.activeSelf + " (cerrada = activa)");
            if (door.GetComponent<Rigidbody>()) error(tag + ": no debe tener Rigidbody");
        }

        static void CheckTrigger(GameObject go, string tag, Action<string> error)
        {
            var box = go.GetComponent<BoxCollider>(); var body = go.GetComponent<Rigidbody>();
            if (!box || !box.isTrigger) error(tag + ": BoxCollider trigger");
            if (!body || !body.isKinematic || body.useGravity) error(tag + ": Rigidbody cinemático sin gravedad");
        }

        static void CheckGap(string chamberId, Action<string> warn, Action<string> error)
        {
            StructureChamber c;
            try { c = Map01Json.Chamber(chamberId); } catch (Exception e) { error(chamberId + ": " + e.Message); return; }
            if (c.trigger?.center == null) return;
            foreach (var door in new[] { c.sourceDoor, c.destinationDoor })
            {
                if (door?.center == null) continue;
                float gap = DoorAuthoring.TriggerGap(c, door);
                if (gap < DoorAuthoring.MinTriggerGap - .05f) error(chamberId + ": trigger a " + gap.ToString("0.00") + " m de la hoja (AT-1)");
                else if (gap > 1.8f) warn(chamberId + ": trigger a " + gap.ToString("0.00") + " m de la hoja: un jugador dentro de la cámara pero fuera del trigger provoca un Abort (AT-2)");
            }
        }

        static void ValidateRoom(StructureRoom room, List<InternalDoor> doors, Action<string> error, Action<string> warn)
        {
            var scene = Map01Scenes.OpenRoom(room, false);
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == Map01Layers.RoomRootName(room.id));
            if (!root) { error(room.id + ": falta el root " + Map01Layers.RoomRootName(room.id)); return; }
            var layer = root.transform.Find(Map01Layers.Doors);
            if (!layer) { error(room.id + ": falta la capa [Doors]"); return; }
            var slots = DoorAuthoring.SlotPositions(root.transform);
            Vector3 origin = Map01Math.V3(room.worldOrigin);
            if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignRoom>(true)).Count() != 1) warn(room.id + ": debe haber exactamente 1 CampaignRoom (lo crea Encuentros); sin él la persiana no actúa");
            foreach (Transform child in layer)
                if (!child.GetComponent<ArenaShutter>()) warn(room.id + ": [Doors]/" + child.name + " no es una persiana de R4");
            foreach (var d in doors)
            {
                string tag = room.id + "/" + d.id;
                var host = layer.Find(d.id);
                var shutter = host ? host.GetComponent<ArenaShutter>() : null;
                if (!shutter) { error(tag + ": falta [Doors]/" + d.id + " con ArenaShutter"); continue; }
                var door = shutter.Door;
                if (!door || door.transform.parent != host) { error(tag + ": la puerta debe ser hija del host"); continue; }
                if (!door.GetComponent<Renderer>()) error(tag + ": la puerta no tiene Renderer");
                var box = door.GetComponent<BoxCollider>(); if (!box || box.isTrigger) error(tag + ": la puerta necesita un BoxCollider sólido");
                var obstacle = door.GetComponent<NavMeshObstacle>();
                if (!obstacle || !obstacle.carving || obstacle.shape != NavMeshObstacleShape.Box) error(tag + ": NavMeshObstacle Box con carve");
                var so = new SerializedObject(shutter);
                bool startClosed = so.FindProperty("startClosed").boolValue, portal = so.FindProperty("requirePlayerInArena").boolValue;
                float minDistance = so.FindProperty("minPlayerDistance").floatValue;
                var sealedVolume = so.FindProperty("sealedVolume").boundsValue;
                if (door.activeSelf != startClosed) error(tag + ": activeSelf = " + door.activeSelf + " y startClosed = " + startClosed);
                if (sealedVolume.size.sqrMagnitude <= 0) error(tag + ": sin volumen sellado");
                foreach (var slot in slots)
                    if (sealedVolume.Contains(slot.Value + origin)) error(tag + ": el slot " + slot.Key + " está dentro del volumen sellado (R-P4: «OLEADA EN ESPERA»)");
                if (portal && room.arena?.firstRoom?.center != null)
                {
                    var arena = Map01Math.ToWorld(Map01Math.ToBounds(room.arena.firstRoom), room);
                    float best = 0;
                    foreach (var corner in Corners(arena)) best = Mathf.Max(best, Mathf.Sqrt(sealedVolume.SqrDistance(corner)));
                    if (best < minDistance + 1f) error(tag + ": la arena no llega a " + (minDistance + 1) + " m del volumen sellado: el pórtico nunca cerraría");
                    if (arena.Intersects(sealedVolume)) warn(tag + ": los bounds de la arena se solapan con el volumen sellado (revisar E-5)");
                }
            }
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++) yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }
    }
}
