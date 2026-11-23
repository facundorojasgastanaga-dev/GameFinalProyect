using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.FPS.UI;

namespace IronRequiem.Editor.Map01
{
    /// Map01 F2 session scenes (owner R4). Spec: Docs/IronRequiem/Map01/F2_PLAN.md §2 (steps 1, 7, 8) and 04_PUERTAS.md §Q1.1.
    /// RequiemCampaign.unity is only COPIED with AssetDatabase.CopyAsset; it is never opened for writing nor saved.
    public static class Map01SessionAuthoring
    {
        public const string SessionStartRoot = "Map01_SessionStart", SessionEndRoot = "Map01_SessionEnd";
        public const string WinScene = "WinScene";
        /// User request 18/11 («conecta el recorrido con el inicio»): S1 starts in the protected hangar, untouched, as stage 0 (prologue).
        public const string HangarStage = "Room00_Hangar", HangarScenePath = "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity";
        public const string HangarChamberPath = "HangarTransit/ConnectionChamber";
        public const string IntroMenuPath = "Assets/FPS/Scenes/IntroMenu.unity", LegacyCampaign = "RequiemCampaign";
        public const string HangarBackupDir = "Tools/IronRequiem/Backups/20261118-map01-hangar";
        public static bool StartsInHangar(int session) => session == 0;
        public const string EndText = "BÓVEDA DESPEJADA · MAPA 1 COMPLETADO";
        const float EndDelaySeconds = 5f;

        // ------------------------------------------------------------------ 1. Create session scenes

        [MenuItem(Map01Paths.MenuRoot + "Sessions/1 Create session scenes")]
        public static void CreateSessionScenes()
        {
            Map01Scenes.GuardForeignDirtyScenes(); Map01Scenes.SaveDirtyMap01();
            if (!File.Exists(Map01Paths.CampaignSource)) throw new FileNotFoundException("Map01: falta la escena fuente " + Map01Paths.CampaignSource);
            EnsureFolder(Map01Paths.SceneFolder);
            var log = new StringBuilder("Map01 Sessions/1 Create session scenes · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            for (int k = 0; k < Map01Paths.SessionScenes.Length; k++)
            {
                string path = Map01Paths.ScenePath(Map01Paths.SessionScenes[k]);
                if (!File.Exists(path))
                {
                    if (!AssetDatabase.CopyAsset(Map01Paths.CampaignSource, path)) throw new InvalidOperationException("Map01: no se pudo copiar " + Map01Paths.CampaignSource + " → " + path);
                    log.AppendLine(Map01Paths.SessionScenes[k] + ": copiada de RequiemCampaign.unity");
                }
                else log.AppendLine(Map01Paths.SessionScenes[k] + ": ya existía (solo se limpia)");
                var scene = Map01Scenes.OpenSession(k);
                CleanSession(scene, log);
                Map01Scenes.Save(scene);
            }
            Map01Report.Write("sessions_create.txt", log.ToString());
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/'), leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void CleanSession(Scene scene, StringBuilder log)
        {
            var roots = scene.GetRootGameObjects();
            var flow = DoorAuthoring.SessionFlow(scene) ?? throw new InvalidOperationException(scene.name + ": la copia no tiene CampaignFlow");

            // WeaponMountStation: in RequiemCampaign it is PersistentAirlocks/StartLobby/V5_MountStation (prefab MountStation_C04).
            var stations = roots.SelectMany(r => r.GetComponentsInChildren<WeaponMountStation>(true)).ToArray();
            if (stations.Length != 1) throw new InvalidOperationException(scene.name + ": se esperaba 1 WeaponMountStation y hay " + stations.Length);
            var stationRoot = OutermostRoot(stations[0].gameObject);
            var start = roots.FirstOrDefault(r => r.name == SessionStartRoot);
            if (!start) { start = new GameObject(SessionStartRoot); Undo.RegisterCreatedObjectUndo(start, "Map01 session start"); SceneManager.MoveGameObjectToScene(start, scene); }
            if (stationRoot.transform.parent != start.transform)
            {
                log.AppendLine("  estación '" + stationRoot.name + "' movida de " + PathOf(stationRoot.transform) + " a " + SessionStartRoot);
                Undo.SetTransformParent(stationRoot.transform, start.transform, "Map01 move station");
            }
            if (!stationRoot.activeSelf) { Undo.RecordObject(stationRoot, "Map01 station"); stationRoot.SetActive(true); }

            foreach (var name in new[] { "PersistentAirlocks", "LabSun" })
            {
                var go = roots.FirstOrDefault(r => r.name == name);
                if (!go) { log.AppendLine("  aviso: no hay root " + name); continue; }
                if (go.activeSelf) { Undo.RecordObject(go, "Map01 disable " + name); go.SetActive(false); }
                log.AppendLine("  " + name + " desactivado (no se borra)");
            }
            foreach (var blend in roots.SelectMany(r => r.GetComponentsInChildren<SectorLightingBlend>(true)))
                if (blend.enabled) { Undo.RecordObject(blend, "Map01 SectorLightingBlend off"); blend.enabled = false; log.AppendLine("  SectorLightingBlend apagado en " + blend.name); }

            // Never let an unwired copy load the old campaign stages (Room00_Hangar is protected): clear the stage list until step 2.
            bool wired = flow.StageScenes != null && flow.StageScenes.Length > 0 && flow.StageScenes.All(s => s != null && (s.StartsWith("Map01_", StringComparison.Ordinal) || s == HangarStage));
            if (!wired)
            {
                Undo.RecordObject(flow, "Map01 clear flow");
                flow.Configure(flow.Player, flow.Menu, flow.Message, new string[0], new CampaignAirlock[0], null);
                EditorUtility.SetDirty(flow);
                log.AppendLine("  CampaignFlow vaciado (stageScenes/airlocks/initialDoor) hasta Sessions/2 Wire sessions");
            }
            SetPrologueZero(flow); SetDirectEntry(scene, log);
            log.AppendLine("  se conservan: " + string.Join(", ", scene.GetRootGameObjects().Where(r => r.activeSelf).Select(r => r.name)));
        }

        // ------------------------------------------------------------------ 2. Wire sessions

        [MenuItem(Map01Paths.MenuRoot + "Sessions/2 Wire sessions")]
        public static void WireSessions()
        {
            Map01Scenes.GuardForeignDirtyScenes();
            var log = new StringBuilder("Map01 Sessions/2 Wire sessions · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            var errors = new List<string>();
            // Validate the data first (rooms' scene names, start chambers).
            var sceneNames = new Dictionary<string, string>();
            foreach (var id in Map01Paths.RoomIds)
            {
                try { var r = Map01Json.Room(id); if (string.IsNullOrEmpty(r.scene)) errors.Add(id + ": scene vacío"); else sceneNames[id] = r.scene; }
                catch (Exception e) { errors.Add(id + ": " + e.Message); }
            }
            var starts = new Dictionary<string, StructureChamber>();
            foreach (var id in Map01Paths.SessionStartChamber)
            {
                try { starts[id] = Map01Json.Chamber(id); } catch (Exception e) { errors.Add(id + ": " + e.Message); }
            }
            if (errors.Count > 0) DoorAuthoring.Fail("Sessions/2 Wire sessions: datos inválidos", errors, log, "sessions_wire.txt");

            for (int k = 0; k < Map01Paths.SessionScenes.Length; k++)
            {
                try { WireSession(k, sceneNames, starts, log); }
                catch (Exception e) { errors.Add(Map01Paths.SessionScenes[k] + ": " + e.Message); }
            }
            if (errors.Count > 0) DoorAuthoring.Fail("Sessions/2 Wire sessions: errores", errors, log, "sessions_wire.txt");
            log.AppendLine("OK");
            Map01Report.Write("sessions_wire.txt", log.ToString());
        }

        static void WireSession(int k, Dictionary<string, string> sceneNames, Dictionary<string, StructureChamber> starts, StringBuilder log)
        {
            var scene = Map01Scenes.OpenSession(k);
            var flow = DoorAuthoring.SessionFlow(scene) ?? throw new InvalidOperationException("no hay CampaignFlow (ejecutar Sessions/1)");
            if (!flow.Player || !flow.Menu || !flow.Message) throw new InvalidOperationException("CampaignFlow sin player, menu o message");
            var ids = Map01Paths.SessionStages[k];
            bool hangar = StartsInHangar(k);
            int offset = hangar ? 1 : 0; // S1: stage 0 = Room00_Hangar (prologue), R01 = stage 1 = «SECTOR 1»
            var stageScenes = (hangar ? new[] { HangarStage } : new string[0]).Concat(ids.Select(id => sceneNames[id])).ToArray();
            var airlocks = new CampaignAirlock[stageScenes.Length - 1];
            string startId = Map01Paths.SessionStartChamber[k];
            var startRoot = ChamberRoot(scene, startId);
            var startDestination = Door(startRoot, DoorAuthoring.DestinationName, startId);
            if (hangar) airlocks[0] = WireHangarAirlock(scene, flow, startDestination, log);
            for (int i = 0; i < ids.Length - 1; i++)
            {
                string chamberId = "C" + ids[i].Substring(1);
                var root = ChamberRoot(scene, chamberId);
                var airlock = DoorComponent<CampaignAirlock>(root, DoorAuthoring.AirlockName, chamberId);
                var source = Door(root, DoorAuthoring.SourceName, chamberId); var destination = Door(root, DoorAuthoring.DestinationName, chamberId);
                Undo.RecordObject(airlock, "Map01 wire airlock");
                airlock.Configure(flow, offset + i, source, destination);
                airlock.ConfigureExtras(new GameObject[0], root.GetComponentInChildren<AirlockCycle>(true));
                EditorUtility.SetDirty(airlock);
                airlocks[offset + i] = airlock;
                log.AppendLine("  airlocks[" + (offset + i) + "] = " + chamberId + " (sourceStage " + (offset + i) + ")");
            }
            // S1: no initialDoor (the hangar cinematic releases stage 0, as in RequiemCampaign); S00's door opens as an a0 extra.
            GameObject initialDoor = hangar ? null : startDestination;
            Undo.RecordObject(flow, "Map01 wire flow");
            flow.Configure(flow.Player, flow.Menu, flow.Message, stageScenes, airlocks, initialDoor);
            EditorUtility.SetDirty(flow);
            SetPrologue(flow, offset); SetDirectEntry(scene, log);
            if (hangar && !new SerializedObject(flow).FindProperty("startCover").objectReferenceValue) throw new InvalidOperationException("S1: CampaignFlow.startCover vacío (el prólogo del hangar lo necesita)");

            var startData = starts[startId];
            if (hangar)
            {
                // [R0 18/11] Verified in Play: at (0,0,0) the player had no floor in S1 (PersistentAirlocks is off) and fell to KillHeight
                // during the cinematic. Wait inside S00 (floor, out of every Shot); the cinematic teleports it to its StandPoint at the end.
                var s00Door = startRoot.Find(Map01Layers.Doors + "/DestinationDoor");
                PlacePlayer(flow.Player, startData, startRoot, s00Door ? s00Door.gameObject : startRoot.gameObject, log);
                log.AppendLine("  S1: espera la cinemática en S00; HangarIntroCinematic lo lleva al StandPoint al terminar");
            }
            else PlacePlayer(flow.Player, startData, startRoot, initialDoor, log);
            PlaceStation(scene, startData, startRoot, log);

            // Session chaining: departure handoff in S1-S3, end of map in S4.
            var end = scene.GetRootGameObjects().FirstOrDefault(r => r.name == SessionEndRoot);
            if (k + 1 < Map01Paths.SessionScenes.Length)
            {
                if (end) Undo.DestroyObjectImmediate(end);
                string departId = Map01Paths.SessionStartChamber[k + 1];
                var departRoot = ChamberRoot(scene, departId);
                var handoff = DoorComponent<Map01SessionHandoff>(departRoot, DoorAuthoring.HandoffName, departId);
                Undo.RecordObject(handoff, "Map01 wire handoff");
                handoff.ConfigureDeparture(flow, Door(departRoot, DoorAuthoring.SourceName, departId), Door(departRoot, DoorAuthoring.DestinationName, departId),
                    departRoot.GetComponentInChildren<AirlockCycle>(true), Map01Paths.SessionScenes[k + 1]);
                EditorUtility.SetDirty(handoff);
                log.AppendLine("  handoff " + departId + " → " + Map01Paths.SessionScenes[k + 1]);
            }
            else
            {
                if (!end) { end = new GameObject(SessionEndRoot); Undo.RegisterCreatedObjectUndo(end, "Map01 session end"); SceneManager.MoveGameObjectToScene(end, scene); }
                var handoff = end.GetComponent<Map01SessionHandoff>();
                if (!handoff) handoff = Undo.AddComponent<Map01SessionHandoff>(end); // no '??' on UnityEngine.Object (fake null in the Editor)
                Undo.RecordObject(handoff, "Map01 wire end");
                handoff.ConfigureEnd(flow, WinScene, EndText, EndDelaySeconds);
                EditorUtility.SetDirty(handoff);
                log.AppendLine("  fin de mapa: " + SessionEndRoot + " → " + WinScene + " a los " + EndDelaySeconds + " s");
            }
            log.AppendLine("== " + scene.name + ": stageScenes = [" + string.Join(", ", stageScenes) + "] · airlocks = " + airlocks.Length + " · prologueStages = " + offset
                + " · initialDoor = " + (initialDoor ? startId + "/" + DoorAuthoring.DestinationName : "null (lo libera la cinemática del hangar)"));
            Map01Scenes.Save(scene);
        }

        static Transform ChamberRoot(Scene scene, string chamberId)
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == chamberId);
            return go ? go.transform : throw new InvalidOperationException("falta el root de la cámara " + chamberId + " (Structure y Doors/Build all)");
        }

        static T DoorComponent<T>(Transform chamberRoot, string child, string chamberId) where T : UnityEngine.Component
        {
            var t = chamberRoot.Find(Map01Layers.Doors + "/" + child);
            var c = t ? t.GetComponent<T>() : null;
            if (!c) throw new InvalidOperationException(chamberId + ": falta [Doors]/" + child + " con " + typeof(T).Name + " (ejecutar Doors/Build all)");
            return c;
        }

        static GameObject Door(Transform chamberRoot, string name, string chamberId)
        {
            var t = chamberRoot.Find(Map01Layers.Doors + "/" + name);
            if (!t) throw new InvalidOperationException(chamberId + ": falta [Doors]/" + name + " (ejecutar Doors/Build all)");
            if (!t.GetComponent<Renderer>() || !t.GetComponent<BoxCollider>()) throw new InvalidOperationException(chamberId + "/" + name + ": el root necesita Renderer y BoxCollider (AirlockDoorMotion)");
            return t.gameObject;
        }

        static void PlacePlayer(PlayerCharacterController player, StructureChamber c, Transform chamberRoot, GameObject initialDoor, StringBuilder log)
        {
            Vector3 position; float floor = c.floorY;
            if (c.sessionStart?.playerStart != null && c.sessionStart.playerStart.Length >= 3) position = Map01Math.V3(c.sessionStart.playerStart);
            else
            {
                var marker = Map01Markers.Find(Map01Hierarchy.Layer(chamberRoot, Map01Layers.Structure), Map01Markers.PlayerStart);
                position = marker ? marker.position : new Vector3(Map01Math.V3(c.worldCenter).x, floor, Map01Math.V3(c.worldCenter).z);
            }
            if (Mathf.Abs(position.y - floor) < .05f) position.y = floor + .03f; // feet just above the floor (CampaignAuthoring pattern)
            Vector3 look = initialDoor.transform.position - position; look.y = 0;
            var rotation = look.sqrMagnitude > .01f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.Euler(0, c.sessionStart?.playerYaw ?? 0, 0);
            var root = OutermostRoot(player.gameObject).transform;
            Undo.RecordObject(root, "Map01 player start");
            root.SetPositionAndRotation(position, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            log.AppendLine("  player en " + position + " mirando a " + initialDoor.name + " (yaw " + rotation.eulerAngles.y.ToString("0") + ")");
        }

        static void PlaceStation(Scene scene, StructureChamber c, Transform chamberRoot, StringBuilder log)
        {
            var start = scene.GetRootGameObjects().FirstOrDefault(r => r.name == SessionStartRoot);
            var station = start ? start.GetComponentInChildren<WeaponMountStation>(true) : null;
            if (!station) throw new InvalidOperationException("no hay WeaponMountStation bajo " + SessionStartRoot + " (ejecutar Sessions/1)");
            var root = OutermostRoot(station.gameObject).transform;
            var marker = Map01Markers.Find(Map01Hierarchy.Layer(chamberRoot, Map01Layers.Structure), Map01Markers.Station);
            Vector3 position; Quaternion rotation = root.rotation;
            if (c.sessionStart?.station?.min != null && c.sessionStart.station.max != null)
            {
                var b = Map01Math.ToBounds(c.sessionStart.station);
                position = new Vector3(b.center.x, b.min.y, b.center.z);
            }
            else if (marker) position = marker.position;
            else throw new InvalidOperationException(c.id + ": sin sessionStart.station ni marcador Station");
            if (marker) rotation = marker.rotation;
            Undo.RecordObject(root, "Map01 station");
            root.SetPositionAndRotation(position, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            log.AppendLine("  estación en " + position + (marker ? " (rotación del marcador Station)" : " (rotación original)"));
        }

        static void SetPrologueZero(CampaignFlow flow) => SetPrologue(flow, 0);

        static void SetPrologue(CampaignFlow flow, int stages)
        {
            var so = new SerializedObject(flow);
            var p = so.FindProperty("prologueStages") ?? throw new InvalidOperationException("CampaignFlow.prologueStages no encontrado");
            if (p.intValue != stages) { p.intValue = stages; so.ApplyModifiedProperties(); }
        }

        /// S1 only: reactivates ONLY PersistentAirlocks/HangarTransit/ConnectionChamber (esclusa a0, copied from RequiemCampaign) and
        /// wires its CampaignAirlock as airlocks[0]: hangar door → corridor door, extras = VestibuleShutter + S00/DestinationDoor.
        static CampaignAirlock WireHangarAirlock(Scene scene, CampaignFlow flow, GameObject s00Door, StringBuilder log)
        {
            if (!File.Exists(HangarScenePath)) throw new InvalidOperationException("falta " + HangarScenePath);
            var persistent = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "PersistentAirlocks");
            if (!persistent) throw new InvalidOperationException("S1: falta el root PersistentAirlocks (copia de RequiemCampaign)");
            var transit = persistent.transform.Find("HangarTransit");
            var chamber = persistent.transform.Find(HangarChamberPath);
            if (!transit || !chamber) throw new InvalidOperationException("S1: falta PersistentAirlocks/" + HangarChamberPath);
            var hangarDoor = HangarPart(chamber, "ChamberHangarDoor").gameObject;
            var corridorDoor = HangarPart(chamber, "ChamberCorridorDoor").gameObject;
            var shutter = HangarPart(chamber, "VestibuleShutter").gameObject;
            var airlock = HangarPart(chamber, "ChamberAirlock").GetComponent<CampaignAirlock>();
            var cycle = chamber.GetComponent<AirlockCycle>();
            if (!airlock) throw new InvalidOperationException("S1: ChamberAirlock sin CampaignAirlock");
            if (!cycle) log.AppendLine("  aviso: ConnectionChamber sin AirlockCycle");
            int off = 0;
            foreach (Transform child in persistent.transform) if (child != transit && SetActive(child.gameObject, false)) off++;
            foreach (Transform child in transit) if (child != chamber && SetActive(child.gameObject, false)) off++;
            SetActive(chamber.gameObject, true); SetActive(transit.gameObject, true); SetActive(persistent, true);
            Undo.RecordObject(airlock, "Map01 wire a0");
            airlock.Configure(flow, 0, hangarDoor, corridorDoor);
            airlock.ConfigureExtras(new[] { shutter, s00Door }, cycle);
            EditorUtility.SetDirty(airlock);
            log.AppendLine("  a0 reactivada: PersistentAirlocks/" + HangarChamberPath + " (" + off + " hermanos desactivados uno a uno; el resto sigue inactivo)");
            log.AppendLine("  airlocks[0] = a0/ChamberAirlock (sourceStage 0): ChamberHangarDoor → ChamberCorridorDoor + extras [VestibuleShutter, S00/DestinationDoor]");
            return airlock;
        }

        public static Transform HangarPart(Transform chamber, string name)
        {
            var t = chamber.Find(name);
            if (!t) throw new InvalidOperationException("S1: falta " + HangarChamberPath + "/" + name);
            return t;
        }

        static bool SetActive(GameObject go, bool active)
        {
            if (go.activeSelf == active) return false;
            Undo.RecordObject(go, "Map01 active"); go.SetActive(active); return true;
        }

        /// S1: the player starts exactly as in RequiemCampaign (no transform overrides on the Player instance root); the hangar cinematic
        /// teleports him to its StandPoint at the handoff (HangarIntroCinematic.BeginHandoff, read only).
        static void RestoreCampaignPlayerStart(PlayerCharacterController player, StringBuilder log)
        {
            var root = OutermostRoot(player.gameObject).transform;
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                var so = new SerializedObject(root);
                foreach (var name in new[] { "m_LocalPosition", "m_LocalRotation", "m_LocalEulerAnglesHint" })
                {
                    var p = so.FindProperty(name);
                    if (p != null && p.prefabOverride) PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                }
            }
            log.AppendLine("  player como en RequiemCampaign (sin overrides de transform): " + root.position + "; la cinemática del hangar lo lleva a su StandPoint");
        }

        static void SetDirectEntry(Scene scene, StringBuilder log)
        {
            foreach (var menu in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatLabMenu>(true)))
            {
                var so = new SerializedObject(menu);
                var p = so.FindProperty("directGameplayEntry") ?? throw new InvalidOperationException("CombatLabMenu.directGameplayEntry no encontrado");
                if (!p.boolValue) { p.boolValue = true; so.ApplyModifiedProperties(); log.AppendLine("  CombatLabMenu.directGameplayEntry = true"); }
            }
        }

        static GameObject OutermostRoot(GameObject go) { var r = PrefabUtility.GetOutermostPrefabInstanceRoot(go); return r ? r : go; }

        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

        // ------------------------------------------------------------------ 3. Add to Build Settings

        [MenuItem(Map01Paths.MenuRoot + "Sessions/3 Add to Build Settings")]
        public static void AddToBuildSettings()
        {
            var log = new StringBuilder("Map01 Sessions/3 Add to Build Settings · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            var wanted = new List<string>();
            wanted.AddRange(Map01Paths.SessionScenes.Select(Map01Paths.ScenePath));
            foreach (var id in Map01Paths.RoomIds)
            {
                try { wanted.Add(Map01Paths.ScenePath(Map01Json.Room(id).scene)); }
                catch (Exception e) { log.AppendLine("  " + id + ": sin nombre de escena (" + e.Message + ")"); }
            }
            var scenes = EditorBuildSettings.scenes.ToList(); // only appended to: never removed nor reordered (F2_PLAN §4)
            int added = 0;
            foreach (var path in wanted)
            {
                var existing = scenes.FirstOrDefault(s => s.path == path);
                if (existing != null) { log.AppendLine("  ya estaba" + (existing.enabled ? "" : " (DESACTIVADA: no se cambia)") + ": " + path); continue; }
                if (!File.Exists(path)) { log.AppendLine("  NO EXISTE, no se añade: " + path); continue; }
                scenes.Add(new EditorBuildSettingsScene(path, true)); added++;
                log.AppendLine("  añadida: " + path);
            }
            if (added > 0) EditorBuildSettings.scenes = scenes.ToArray();
            log.AppendLine("Añadidas " + added + " de " + wanted.Count + " · total en Build Settings: " + scenes.Count);
            Map01Report.Write("sessions_buildsettings.txt", log.ToString());
        }

        // ------------------------------------------------------------------ 4/5. IntroMenu JUGAR → Map01 (user request 18/11, via R0)

        [MenuItem(Map01Paths.MenuRoot + "Sessions/4 Point menu PLAY to Map01")]
        public static void PointMenuPlayToMap01() => RetargetMenuPlay(LegacyCampaign, Map01Paths.SessionScenes[0]);

        [MenuItem(Map01Paths.MenuRoot + "Sessions/5 Revert menu PLAY to RequiemCampaign")]
        public static void RevertMenuPlay() => RetargetMenuPlay(Map01Paths.SessionScenes[0], LegacyCampaign);

        /// Changes ONLY LoadSceneButton.SceneName of the JUGAR button (the one RequiemMenuView reads) in IntroMenu (protected scene).
        /// Backs the scene up first, saves only if that single serialized line changed and restores the backup bytes otherwise.
        static void RetargetMenuPlay(string from, string to)
        {
            Map01Scenes.GuardForeignDirtyScenes(); Map01Scenes.SaveDirtyMap01();
            var log = new StringBuilder("Map01 Sessions · IntroMenu JUGAR: " + from + " → " + to + " · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            if (!File.Exists(IntroMenuPath)) throw new FileNotFoundException(IntroMenuPath);
            string targetPath = to == LegacyCampaign ? Map01Paths.CampaignSource : Map01Paths.ScenePath(to);
            if (!EditorBuildSettings.scenes.Any(sc => sc.enabled && sc.path == targetPath)) throw new InvalidOperationException("la escena destino " + targetPath + " no está activa en Build Settings (Sessions/3)");

            Directory.CreateDirectory(HangarBackupDir);
            string backup = HangarBackupDir + "/IntroMenu.unity.bak", stamped = HangarBackupDir + "/IntroMenu." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak";
            byte[] original = File.ReadAllBytes(IntroMenuPath);
            if (!File.Exists(backup)) File.WriteAllBytes(backup, original); // first copy = state before any Map01 change
            File.WriteAllBytes(stamped, original);
            log.AppendLine("  respaldo: " + backup + " (primero) y " + stamped);

            var scene = EditorSceneManager.OpenScene(IntroMenuPath, OpenSceneMode.Single);
            if (scene.isDirty) throw new InvalidOperationException("IntroMenu quedó modificada solo con abrirla: no se toca. Ciérrala sin guardar.");
            var buttons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LoadSceneButton>(true)).ToArray();
            var matches = buttons.Where(b => b.SceneName == from).ToArray();
            if (matches.Length == 0 && buttons.Any(b => b.SceneName == to)) { log.AppendLine("  ya apuntaba a " + to + ": no se guarda nada"); Map01Report.Write("menu_play.txt", log.ToString()); return; }
            if (matches.Length != 1) throw new InvalidOperationException("se esperaba 1 LoadSceneButton con SceneName = " + from + " y hay " + matches.Length + " (de " + buttons.Length + ")");
            var button = matches[0];
            var so = new SerializedObject(button);
            var property = so.FindProperty("SceneName") ?? throw new InvalidOperationException("LoadSceneButton.SceneName no encontrado");
            string previous = property.stringValue;
            property.stringValue = to;
            so.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            EditorSceneManager.SaveScene(scene);

            // Verify byte-level: exactly one line may differ, "SceneName: <from>" → "SceneName: <to>". Otherwise restore the original bytes.
            string before = System.Text.Encoding.UTF8.GetString(original).TrimStart((char)0xFEFF), after = File.ReadAllText(IntroMenuPath).TrimStart((char)0xFEFF);
            var a = before.Split('\n');
            // [R0 18/11] Unity 6000.5 reserialization inserts default "style: 0" lines (new serialized field at its default value;
            // the original has none): ignore exactly those insertions. Any other difference still restores the original.
            var b2 = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(after.Split('\n'), l => l.TrimEnd('\r').Trim() != "style: 0"));
            var changed = new List<int>();
            if (a.Length == b2.Length) for (int i = 0; i < a.Length; i++) if (a[i] != b2[i]) changed.Add(i);
            bool ok = a.Length == b2.Length && changed.Count == 1
                && a[changed[0]].TrimEnd('\r').Trim() == "SceneName: " + from && b2[changed[0]].TrimEnd('\r').Trim() == "SceneName: " + to;
            if (!ok)
            {
                // [R0 18/11] keep what Unity wrote, for review, before restoring the original bytes
                File.WriteAllText("Tools/IronRequiem/Backups/20261118-map01-hangar/IntroMenu.unity-rewritten.txt", after);
                File.WriteAllBytes(IntroMenuPath, original);
                AssetDatabase.ImportAsset(IntroMenuPath, ImportAssetOptions.ForceUpdate);
                EditorSceneManager.OpenScene(IntroMenuPath, OpenSceneMode.Single);
                log.AppendLine("  FALLO: Unity reescribió " + (a.Length == b2.Length ? changed.Count + " líneas" : "un número distinto de líneas") + "; IntroMenu restaurada byte a byte desde el respaldo");
                Map01Report.Write("menu_play.txt", log.ToString());
                throw new InvalidOperationException("IntroMenu: el guardado cambiaba más que SceneName; se restauró el original. Informe: " + Map01Paths.ReportRoot + "/menu_play.txt");
            }
            log.AppendLine("  " + PathOf(button.transform) + " · LoadSceneButton.SceneName: '" + previous + "' → '" + to + "' (línea " + (changed[0] + 1) + "; ninguna otra línea cambió)");
            Map01Report.Write("menu_play.txt", log.ToString());
        }
    }
}
