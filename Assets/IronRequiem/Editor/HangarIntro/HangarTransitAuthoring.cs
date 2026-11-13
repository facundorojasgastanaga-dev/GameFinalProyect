using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 12/11/2026 — transit between Room00_Hangar and Room01_Foundry, built by "Build Room00_Hangar + campaign":
    ///   hangar (Room00) | connection chamber + vestibule (persistent, RequiemCampaign) | alarm corridor (Room00b) | lobby
    ///   airlock (persistent, existing) | Room01_Foundry.
    /// World z: hangar front -84.5 · chamber -84..-76 · vestibule -76..-70 · corridor -70..-35.5 · lobby -35..-26.
    /// The chamber has two doors and a pressurisation cycle; CampaignFlow unloads a sector only once the door behind the
    /// player is fully shut and the player is inside the airlock volume. What can be seen from the chamber (its walls,
    /// both doors, the vestibule behind the porthole and its inner shutter) lives in the persistent session scene.
    internal static class HangarTransitAuthoring
    {
        public const string CorridorName = "Room00b_AlarmCorridor";
        public const string CorridorScene = "Assets/IronRequiem/Scenes/Campaign/" + CorridorName + ".unity";
        const string Session = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string LevelArt = "Assets/IronRequiem/Art/Levels/Campaign";
        const string NavPath = LevelArt + "/" + CorridorName + "_NavMesh.asset";
        const string T07Prefab = "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";
        const string CarroneroPrefab = "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab";
        const string CorridorTitle = "PUENTE NORTE · ZONA DE TRÁNSITO";
        // transit layout (world metres)
        const float ChamberStart = -84f, ChamberEnd = -76f, VestibuleEnd = -70f, CorridorEnd = -35.5f;
        const float ChamberHalf = 4f, ChamberHeight = 4.8f;
        // v3: wider and taller corridor (was 11 x 6 m), few large covers, flush walls: room to sprint, slide and dash
        const float CorridorHalf = 9f, CorridorHeight = 9f, LobbyHalf = 5.5f, LobbyHeight = 6f;
        static StringBuilder log;

        static GameObject Box(string name, Transform parent, Vector3 worldPos, Vector3 size, Material mat, bool solid = true, Vector3? euler = null)
        {
            var go = HangarMenuAuthoring.Box(name, parent, Vector3.zero, size, mat, euler, true);
            go.transform.position = worldPos;
            if (solid) go.AddComponent<BoxCollider>();
            return go;
        }

        static TextMeshPro Sign(string name, Transform parent, Vector3 pos, float yaw, string text, float size, Color color, Vector2 rect)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var tmp = go.AddComponent<TextMeshPro>(); tmp.font = TMP_Settings.defaultFontAsset; tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color; tmp.rectTransform.sizeDelta = rect; tmp.characterSpacing = 6; return tmp;
        }

        // ------------------------------------------------------------------ corridor scene
        internal static void BuildCorridor(StringBuilder sharedLog, HangarMenuAuthoring.Mats m)
        {
            log = sharedLog;
            Scene scene;
            if (File.Exists(CorridorScene)) scene = EditorSceneManager.OpenScene(CorridorScene, OpenSceneMode.Single);
            else { scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorSceneManager.SaveScene(scene, CorridorScene); }
            foreach (var r in scene.GetRootGameObjects()) Object.DestroyImmediate(r);
            var root = new GameObject("AlarmCorridor").transform;
            var geo = HangarMenuAuthoring.Group("Geometry", root);
            float len = CorridorEnd - VestibuleEnd, mid = (CorridorEnd + VestibuleEnd) * .5f;
            Box("Floor", geo, new Vector3(0, -.25f, mid), new Vector3(CorridorHalf * 2 + 2, .5f, len), m.floor);
            Box("Ceiling", geo, new Vector3(0, CorridorHeight + .25f, mid), new Vector3(CorridorHalf * 2 + 2, .5f, len), m.plateDark);
            foreach (var s in new[] { -1, 1 }) Box("Wall", geo, new Vector3(s * (CorridorHalf + .5f), CorridorHeight * .5f, mid), new Vector3(1, CorridorHeight, len), m.plateDark);
            // caps where the corridor meets the narrower vestibule shutter and the lobby door (both narrower and lower)
            float capW = CorridorHalf - ChamberHalf + .5f, lobW = CorridorHalf - LobbyHalf + .5f;
            foreach (var s in new[] { -1, 1 })
            {
                Box("CapVestibule", geo, new Vector3(s * (ChamberHalf + capW * .5f), CorridorHeight * .5f, VestibuleEnd + .3f), new Vector3(capW, CorridorHeight, .6f), m.plateDark);
                Box("CapLobby", geo, new Vector3(s * (LobbyHalf + lobW * .5f), CorridorHeight * .5f, CorridorEnd - .35f), new Vector3(lobW, CorridorHeight, .5f), m.plateDark);
            }
            Box("CapVestibuleTop", geo, new Vector3(0, (ChamberHeight + CorridorHeight) * .5f, VestibuleEnd + .3f), new Vector3(ChamberHalf * 2 + .4f, CorridorHeight - ChamberHeight, .6f), m.plateDark);
            Box("CapLobbyTop", geo, new Vector3(0, (LobbyHeight + CorridorHeight) * .5f, CorridorEnd - .35f), new Vector3(LobbyHalf * 2 + .4f, CorridorHeight - LobbyHeight, .5f), m.plateDark);
            // pilasters sunk into the walls (only 0.2 m proud, rounded by the wall line: nothing to snag on) and roof girders
            for (float z = VestibuleEnd + 4; z < CorridorEnd - 2; z += 8)
            {
                foreach (var s in new[] { -1, 1 }) Box("Pilaster", geo, new Vector3(s * (CorridorHalf - .1f), CorridorHeight * .5f, z), new Vector3(.4f, CorridorHeight, .9f), m.plate);
                Box("Girder", geo, new Vector3(0, CorridorHeight - .3f, z), new Vector3(CorridorHalf * 2, .5f, .45f), m.trim, false);
            }
            for (int i = 0; i < 3; i++) HangarMenuAuthoring.Prim(PrimitiveType.Cylinder, "Pipe", geo, new Vector3(-CorridorHalf + .6f + i * .4f, CorridorHeight - .9f, mid), new Vector3(.26f, len * .5f, .26f), i % 2 == 0 ? m.trim : m.rubber, new Vector3(90, 0, 0), false);
            // cover: three big, clear blocks + the guard post; wide lanes on both sides for sprint/slide/dash
            var cover = HangarMenuAuthoring.Group("Cover", root);
            Box("CoverBlockA", cover, new Vector3(-4.6f, .75f, -60.5f), new Vector3(3.6f, 1.5f, 1.6f), m.plate);
            Box("CoverBlockB", cover, new Vector3(4.4f, .75f, -53f), new Vector3(3.6f, 1.5f, 1.6f), m.plate);
            Box("CoverBlockC", cover, new Vector3(-5.0f, .75f, -44.5f), new Vector3(3.6f, 1.5f, 1.6f), m.plate);
            foreach (var z in new[] { -60.5f, -53f, -44.5f }) Box("CoverStripe", cover, new Vector3(z == -53f ? 4.4f : (z == -60.5f ? -4.6f : -5.0f), 1.52f, z), new Vector3(3.62f, .04f, 1.62f), m.hazard, false);
            // raised guard post at the far end (T-07 on high ground) with a wide, shallow ramp
            Box("GuardPlatform", cover, new Vector3(5.6f, .7f, -40.2f), new Vector3(5f, 1.4f, 5f), m.plateDark);
            var ramp = Box("GuardRamp", cover, Vector3.zero, new Vector3(3.4f, .2f, 5.6f), m.trim, true, new Vector3(-14f, 0, 0));
            ramp.transform.position = new Vector3(5.6f, .62f, -45.3f);
            Box("GuardRail", cover, new Vector3(3.15f, 1.9f, -40.2f), new Vector3(.1f, 1f, 5f), m.hazard, false);
            // ducts: dark recesses in the walls with hanging grates; scavengers burst out of these
            var ducts = new[] { new Vector3(-1, 0, -64f), new Vector3(1, 0, -57f), new Vector3(-1, 0, -51f), new Vector3(1, 0, -66f) };
            var ductPoints = new List<Transform>();
            for (int i = 0; i < ducts.Length; i++)
            {
                float s = ducts[i].x, z = ducts[i].z; var dx = s * (CorridorHalf - .05f);
                Box("Duct", geo, new Vector3(dx, 1.0f, z), new Vector3(.12f, 2.0f, 2.0f), m.city, false);
                Box("DuctFrame", geo, new Vector3(dx - s * .05f, 2.05f, z), new Vector3(.1f, .12f, 2.2f), m.hazard, false);
                Box("DuctGrate", geo, new Vector3(dx - s * .35f, 1.2f, z + .6f), new Vector3(.05f, 1.7f, 1.9f), m.trim, false, new Vector3(0, 0, s * 35));
                var p = new GameObject("DuctSpawn" + i).transform; p.SetParent(root, false); p.position = new Vector3(dx - s * 1.6f, .05f, z); ductPoints.Add(p);
            }
            // T-07 guards: one behind the far-left cover, one on the raised post
            var guard1 = new GameObject("GuardSpawn0").transform; guard1.SetParent(root, false); guard1.position = new Vector3(-5.0f, .05f, -42.2f);
            var guard2 = new GameObject("GuardSpawn1").transform; guard2.SetParent(root, false); guard2.position = new Vector3(6.2f, 1.45f, -39.6f);
            // lights: half-lit work tubes + red alarm lights + rotating beacons
            var lights = HangarMenuAuthoring.Group("Lights", root);
            int seed = 90;
            for (float z = VestibuleEnd + 3; z < CorridorEnd - 1; z += 7)
            {
                var lamp = HangarMenuAuthoring.Group("Tube", lights); lamp.position = new Vector3(0, CorridorHeight - .6f, z);
                HangarMenuAuthoring.Box("Housing", lamp, new Vector3(0, .08f, 0), new Vector3(2.2f, .14f, .4f), m.trim, null, false);
                var tube = HangarMenuAuthoring.Box("Tube", lamp, Vector3.zero, new Vector3(2f, .06f, .1f), m.lampWarm, null, false).GetComponent<Renderer>();
                var spot = HangarMenuAuthoring.MakeLight("Down", lamp, LightType.Spot, new Vector3(0, -.05f, 0), new Vector3(90, 0, 0), new Color(1f, .82f, .65f), 14f, 13.5f, 125f);
                HangarMenuAuthoring.Flicker(lamp.gameObject, new[] { spot }, new[] { tube }, new Color(2f, 1.7f, 1.25f), seed % 3 == 0 ? MenuFlicker.Mode.Failing : MenuFlicker.Mode.Hum, .35f, seed++);
            }
            var red = new List<Light>(); var spinners = new List<Transform>(); var spinLights = new List<Light>(); var heads = new List<Renderer>();
            foreach (var z in new[] { -64f, -55f, -46f, -38f })
            {
                foreach (var s in new[] { -1, 1 })
                {
                    var l = HangarMenuAuthoring.MakeLight("AlarmRed", lights, LightType.Point, Vector3.zero, Vector3.zero, new Color(1f, .08f, .05f), 0, 9f);
                    l.transform.position = new Vector3(s * (CorridorHalf - .6f), 3.6f, z); red.Add(l);
                }
                var beacon = HangarMenuAuthoring.Group("Beacon", lights); beacon.position = new Vector3((z % 2 == 0 ? 1 : -1) * (CorridorHalf - .4f), 4.4f, z + 3);
                heads.Add(HangarMenuAuthoring.Box("BeaconHead", beacon, Vector3.zero, new Vector3(.32f, .28f, .32f), m.redGlow, null, false).GetComponent<Renderer>());
                var spin = HangarMenuAuthoring.Group("Spin", beacon);
                var bl = HangarMenuAuthoring.MakeLight("BeaconSpot", spin, LightType.Spot, Vector3.zero, new Vector3(20, 0, 0), new Color(1f, .1f, .06f), 0, 16f, 34f);
                spinners.Add(spin); spinLights.Add(bl);
            }
            // wall screens (only lit while the alarm runs)
            var screens = new List<TMP_Text>();
            foreach (var z in new[] { -61.5f, -49.5f, -39f })
            {
                foreach (var s in new[] { -1, 1 })
                {
                    Box("ScreenFrame", geo, new Vector3(s * (CorridorHalf - .04f), 2.9f, z), new Vector3(.08f, .8f, 2.4f), m.trim, false);
                    screens.Add(Sign("AlertScreen", geo, new Vector3(s * (CorridorHalf - .1f), 2.9f, z), s > 0 ? 90 : -90, "ALERTA · INTRUSIÓN DETECTADA", 2.2f, new Color(1f, .2f, .12f), new Vector2(2.3f, .7f)));
                }
            }
            // encounter on the existing round system: 2 short waves, guards + scavengers from the ducts
            var t07 = AssetDatabase.LoadAssetAtPath<GameObject>(T07Prefab); var saw = AssetDatabase.LoadAssetAtPath<GameObject>(CarroneroPrefab);
            if (!t07 || !saw) throw new Exception("Missing enemy prefabs for the corridor");
            var node = new GameObject("RoomEncounter", typeof(EnemySpawnQueue), typeof(CombatTwoRoomsLab), typeof(CampaignRoom));
            node.transform.SetParent(root, false);
            var queue = node.GetComponent<EnemySpawnQueue>(); queue.Configure(1, 1);
            CombatTwoRoomsLab.Group G(GameObject prefab, Transform at) => new CombatTwoRoomsLab.Group { Prefab = prefab, Count = 1, FixedPoint = at };
            var waves = new[]
            {
                // v4: the player arrives with the fist only (cannon comes after this corridor): 2 T-07 to steal a weapon
                // from + 1 Carroñero, then 2 Carroñeros (was 2 T-07 + 2, then 3)
                new CombatTwoRoomsLab.Wave { Groups = new[] { G(t07, guard1), G(t07, guard2), G(saw, ductPoints[0]) } },
                new CombatTwoRoomsLab.Wave { Groups = new[] { G(saw, ductPoints[2]), G(saw, ductPoints[3]) } },
            };
            var rounds = node.GetComponent<CombatTwoRoomsLab>();
            var slots = ductPoints.Concat(new[] { guard1, guard2 }).ToArray();
            rounds.Configure(null, waves, slots, Array.Empty<Transform>(), null, null); rounds.UseSpawnQueue(queue);
            var so = new SerializedObject(rounds);
            so.FindProperty("singleArena").boolValue = true; so.FindProperty("arenaTitle").stringValue = "PUENTE NORTE";
            so.FindProperty("firstRoom").boundsValue = new Bounds(new Vector3(0, CorridorHeight * .5f, -50.5f), new Vector3(CorridorHalf * 2, CorridorHeight + 2, 27f));
            so.FindProperty("playerClearance").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var room = node.GetComponent<CampaignRoom>(); room.Configure(rounds, null, queue); room.ConfigureTitle(CorridorTitle);
            // alarm presentation
            var sirenGo = new GameObject("Siren"); sirenGo.transform.SetParent(root, false); sirenGo.transform.position = new Vector3(0, 6f, -52f);
            var siren = sirenGo.AddComponent<AudioSource>(); siren.clip = HangarSectorAuthoring.SfxClip("IR_Corridor_Siren"); siren.loop = true; siren.playOnAwake = false; siren.spatialBlend = .35f; siren.volume = .55f;
            siren.outputAudioMixerGroup = HangarSectorAuthoring.Group("Ambient");
            var alarm = node.AddComponent<CorridorAlarm>();
            alarm.Configure(rounds, red.ToArray(), spinners.ToArray(), spinLights.ToArray(), heads.ToArray(), siren, HangarSectorAuthoring.SfxClip("IR_Corridor_Klaxon"), screens.ToArray());
            // lighting (rooms carry their own render settings)
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.16f, .17f, .2f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .03f; RenderSettings.fogColor = new Color(.06f, .07f, .09f);
            Bake(geo, cover);
            int onMesh = slots.Count(t => NavMesh.SamplePosition(t.position, out _, 1, NavMesh.AllAreas));
            log.AppendLine($"corridor: waves=2 (2 T-07 + 1 Carroñero, 2 Carroñeros) slots on mesh {onMesh}/{slots.Length}");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var entries = EditorBuildSettings.scenes.ToList();
            if (!entries.Any(e => e.path == CorridorScene)) { entries.Add(new EditorBuildSettingsScene(CorridorScene, true)); EditorBuildSettings.scenes = entries.ToArray(); }
            log.AppendLine("saved " + CorridorScene);
        }

        static void Bake(Transform geo, Transform cover)
        {
            Physics.SyncTransforms();
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = .9f; settings.agentHeight = 2.4f; settings.agentClimb = .35f; settings.agentSlope = 35; settings.overrideVoxelSize = true; settings.voxelSize = .12f;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(geo.parent, -1, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var bounds = new Bounds(new Vector3(0, 2, (VestibuleEnd + CorridorEnd) * .5f), new Vector3(CorridorHalf * 2, 5, CorridorEnd - VestibuleEnd));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (!data) throw new Exception("Corridor NavMesh bake failed");
            data.name = CorridorName + "_NavMesh";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (existing) { EditorUtility.CopySerialized(data, existing); data = existing; EditorUtility.SetDirty(existing); } else AssetDatabase.CreateAsset(data, NavPath);
            var surface = geo.parent.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.agentTypeID = 0; surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.navMeshData = data; surface.AddData();
            log.AppendLine("corridor navmesh sources=" + sources.Count);
        }

        // ------------------------------------------------------------------ persistent chamber + campaign wiring
        internal static void IntegrateCampaign(StringBuilder sharedLog, HangarMenuAuthoring.Mats m)
        {
            log = sharedLog;
            var scene = EditorSceneManager.OpenScene(Session, OpenSceneMode.Single);
            T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
            var flow = All<CampaignFlow>().Single();
            var persistent = scene.GetRootGameObjects().Single(g => g.name == "PersistentAirlocks").transform;
            var lobby = persistent.Find("StartLobby");
            var rear = lobby.Find("LobbyRear").gameObject; var initial = lobby.Find("InitialLoadingDoor").gameObject;
            var old = persistent.Find("HangarTransit"); if (old) Object.DestroyImmediate(old.gameObject);
            var transit = HangarMenuAuthoring.Group("HangarTransit", persistent);
            var chamber = BuildChamber(transit, m, flow, out var chamberAirlock);
            // lobby airlock (corridor -> Room01): reuse the existing trigger if present
            var lobbyTrigger = lobby.Find("HangarAirlock");
            if (!lobbyTrigger)
            {
                var go = new GameObject("HangarAirlock", typeof(BoxCollider), typeof(Rigidbody), typeof(CampaignAirlock));
                go.transform.SetParent(lobby, false); go.transform.position = new Vector3(0, 2, -30.2f);
                var box = go.GetComponent<BoxCollider>(); box.size = new Vector3(10, 4, 5); box.isTrigger = true;
                var rb = go.GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; lobbyTrigger = go.transform;
            }
            var lobbyAirlock = lobbyTrigger.GetComponent<CampaignAirlock>();
            // stage list and airlock order (rebuilt every time: idempotent)
            var others = flow.Airlocks.Where(a => a && a != lobbyAirlock && a.name != "ChamberAirlock").ToList();
            var stages = new List<string> { HangarSectorAuthoring.SectorName, CorridorName }; stages.AddRange(CampaignAuthoring_Names());
            var airlocks = new List<CampaignAirlock> { chamberAirlock, lobbyAirlock }; airlocks.AddRange(others);
            for (int i = 0; i < airlocks.Count; i++)
            {
                var a = airlocks[i]; var aso = new SerializedObject(a);
                aso.FindProperty("flow").objectReferenceValue = flow; aso.FindProperty("sourceStage").intValue = i;
                aso.ApplyModifiedPropertiesWithoutUndo();
            }
            lobbyAirlock.Configure(flow, 1, rear, initial);
            var so = new SerializedObject(flow);
            var sp = so.FindProperty("stageScenes"); sp.arraySize = stages.Count; for (int i = 0; i < stages.Count; i++) sp.GetArrayElementAtIndex(i).stringValue = stages[i];
            var ap = so.FindProperty("airlocks"); ap.arraySize = airlocks.Count; for (int i = 0; i < airlocks.Count; i++) ap.GetArrayElementAtIndex(i).objectReferenceValue = airlocks[i];
            so.FindProperty("initialDoor").objectReferenceValue = null;
            so.FindProperty("prologueStages").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!so.FindProperty("startCover").objectReferenceValue) log.AppendLine("WARN start cover missing");
            CombatV4Authoring.ApplyToSession(scene, lobby, m, log);   // v4: fist-only start, montage station, slide/fist feedback
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            log.AppendLine("stages: " + string.Join(", ", flow.StageScenes));
            for (int i = 0; i < flow.Airlocks.Length; i++) log.AppendLine($"airlock {i} {flow.Airlocks[i].name} source={flow.Airlocks[i].SourceStage} door={(flow.Airlocks[i].SourceDoor ? flow.Airlocks[i].SourceDoor.name : "-")}->{(flow.Airlocks[i].DestinationDoor ? flow.Airlocks[i].DestinationDoor.name : "-")}");
        }

        static string[] CampaignAuthoring_Names() => IronRequiem.Editor.CampaignAuthoring.Names;

        static Transform BuildChamber(Transform transit, HangarMenuAuthoring.Mats m, CampaignFlow flow, out CampaignAirlock airlock)
        {
            var root = HangarMenuAuthoring.Group("ConnectionChamber", transit);
            float cMid = (ChamberStart + ChamberEnd) * .5f, cLen = ChamberEnd - ChamberStart, vMid = (ChamberEnd + VestibuleEnd) * .5f, vLen = VestibuleEnd - ChamberEnd;
            float hall = cLen + vLen, hMid = (ChamberStart + VestibuleEnd) * .5f;
            Box("Floor", root, new Vector3(0, -.25f, hMid), new Vector3(ChamberHalf * 2 + 2, .5f, hall), m.floor);
            Box("Ceiling", root, new Vector3(0, ChamberHeight + .25f, hMid), new Vector3(ChamberHalf * 2 + 2, .5f, hall), m.plateDark);
            foreach (var s in new[] { -1, 1 }) Box("Wall", root, new Vector3(s * (ChamberHalf + .5f), ChamberHeight * .5f, hMid), new Vector3(1, ChamberHeight, hall), m.plateDark);
            // hazard trims and vents
            foreach (var s in new[] { -1, 1 })
            {
                Box("Trim", root, new Vector3(s * (ChamberHalf - .02f), .25f, cMid), new Vector3(.05f, .5f, cLen), m.hazard, false);
                for (int i = 0; i < 3; i++) Box("Vent", root, new Vector3(s * (ChamberHalf - .03f), 1.2f + i * .25f, cMid - 1.5f), new Vector3(.04f, .1f, 1.4f), m.trim, false);
                Box("Panel", root, new Vector3(s * (ChamberHalf - .06f), 1.6f, cMid + 1.8f), new Vector3(.1f, 1.1f, .8f), m.plate, false);
            }
            // doors: hangar side (source) and corridor side (destination, with an armoured porthole), inner shutter at the vestibule end
            var hangarDoor = Box("ChamberHangarDoor", root, new Vector3(0, ChamberHeight * .5f, ChamberStart), new Vector3(ChamberHalf * 2 + .4f, ChamberHeight, .6f), m.hazard);
            var corridorDoor = PortholeDoor(root, m, ChamberEnd);
            var shutter = Box("VestibuleShutter", root, new Vector3(0, ChamberHeight * .5f, VestibuleEnd - .25f), new Vector3(ChamberHalf * 2 + .4f, ChamberHeight, .5f), m.plateDark);
            HangarMenuAuthoring.Box("ShutterHazard", shutter.transform, new Vector3(0, -.38f, -.52f), new Vector3(1, .12f, .05f), m.hazard, null, false);
            // vestibule seen through the porthole: dead red alarm lamps, cables, a panel
            for (int i = 0; i < 3; i++) HangarMenuAuthoring.Prim(PrimitiveType.Cylinder, "Cable", root, Vector3.zero, new Vector3(.08f, vLen * .5f, .08f), m.rubber, new Vector3(90, 0, 0), false).transform.position = new Vector3(ChamberHalf - .3f - i * .15f, ChamberHeight - .4f - i * .1f, vMid);
            foreach (var z in new[] { ChamberEnd + 1.5f, VestibuleEnd - 1.5f })
                foreach (var s in new[] { -1, 1 })
                    Box("DeadAlarmLamp", root, new Vector3(s * (ChamberHalf - .1f), 3.4f, z), new Vector3(.18f, .26f, .26f), m.redGlow, false).GetComponent<Renderer>().SetPropertyBlock(Dim());
            Box("VestibulePanel", root, new Vector3(-ChamberHalf + .08f, 1.5f, vMid), new Vector3(.12f, 1.4f, 1.6f), m.plate, false);
            var vestLight = HangarMenuAuthoring.MakeLight("VestibuleDim", root, LightType.Point, Vector3.zero, Vector3.zero, new Color(.45f, .55f, .7f), .8f, 6f); vestLight.transform.position = new Vector3(0, ChamberHeight - .5f, vMid);
            // sign in the style of the UI
            Sign("TransitSign", root, new Vector3(0, ChamberHeight - .55f, ChamberEnd - .34f), 0, "TRÁNSITO C-04 → PUENTE NORTE", 2.4f, new Color(1f, .62f, .25f), new Vector2(7f, .6f));
            Sign("TransitSignBack", root, new Vector3(0, ChamberHeight - .55f, ChamberStart + .34f), 180, "HANGAR C-04", 2.4f, new Color(.3f, .85f, .95f), new Vector2(7f, .6f));
            // cycle presentation: amber -> green lamps, steam, hydraulics
            var lamps = new List<Light>(); var views = new List<Renderer>();
            foreach (var z in new[] { cMid - 2f, cMid + 2f })
            {
                var l = HangarMenuAuthoring.MakeLight("CycleLamp", root, LightType.Point, Vector3.zero, Vector3.zero, new Color(.25f, 1f, .45f), 2.5f, 7f); l.transform.position = new Vector3(0, ChamberHeight - .4f, z); lamps.Add(l);
                views.Add(Box("CycleStrip", root, new Vector3(0, ChamberHeight - .05f, z), new Vector3(2.2f, .06f, .14f), m.cyanGlow, false).GetComponent<Renderer>());
            }
            var steam = new List<ParticleSystem>();
            foreach (var s in new[] { -1, 1 })
                steam.Add(HangarMenuAuthoring.PS("CycleSteam", root, m.fxSmoke, ps =>
                {
                    ps.transform.position = new Vector3(s * (ChamberHalf - .1f), .2f, cMid); ps.transform.rotation = Quaternion.Euler(0, s > 0 ? -90 : 90, 0);
                    var main = ps.main; main.loop = true; main.duration = 1; main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(.5f, 1.2f); main.startColor = new Color(.8f, .85f, .9f, .35f); main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var e = ps.emission; e.rateOverTime = 40;
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(.1f, .3f, cLen * .8f);
                    var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.2f));
                    var c = ps.colorOverLifetime; c.enabled = true; c.color = HangarMenuAuthoring.Fade(Color.white, .8f);
                }));
            var audioGo = new GameObject("CycleAudio"); audioGo.transform.SetParent(root, false); audioGo.transform.position = new Vector3(0, 2, cMid);
            var audio = audioGo.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = .6f; audio.minDistance = 2; audio.maxDistance = 20; audio.outputAudioMixerGroup = HangarSectorAuthoring.Group("General");
            var cycle = root.gameObject.AddComponent<AirlockCycle>();
            cycle.Configure(lamps.ToArray(), views.ToArray(), steam.ToArray(), audio, HangarSectorAuthoring.SfxClip("IR_Airlock_Cycle"), HangarSectorAuthoring.SfxClip("IR_Airlock_Done"));
            // trigger volume inside the chamber
            var trigger = new GameObject("ChamberAirlock", typeof(BoxCollider), typeof(Rigidbody), typeof(CampaignAirlock));
            trigger.transform.SetParent(root, false); trigger.transform.position = new Vector3(0, 2, cMid + .3f);
            var box = trigger.GetComponent<BoxCollider>(); box.size = new Vector3(ChamberHalf * 2 - 1, 4, cLen - 2.2f); box.isTrigger = true;
            var rb = trigger.GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            airlock = trigger.GetComponent<CampaignAirlock>();
            airlock.Configure(flow, 0, hangarDoor, corridorDoor);
            airlock.ConfigureExtras(new[] { shutter }, cycle);
            log.AppendLine("chamber: doors " + hangarDoor.name + " / " + corridorDoor.name + " + shutter, cycle lamps=" + lamps.Count);
            return root;
        }

        static MaterialPropertyBlock Dim() { var b = new MaterialPropertyBlock(); b.SetColor("_BaseColor", new Color(.18f, .02f, .02f)); return b; }

        /// Corridor-side chamber door with an armoured porthole: the root (left panel) carries the full height so the airlock
        /// motion slides the whole door into the ceiling slot.
        static GameObject PortholeDoor(Transform parent, HangarMenuAuthoring.Mats m, float z)
        {
            float w = ChamberHalf * 2 + .4f, h = ChamberHeight, win = .8f, winY = 1.75f;
            float sideW = (w - win) * .5f;
            var door = Box("ChamberCorridorDoor", parent, new Vector3(-(win + sideW) * .5f, h * .5f, z), new Vector3(sideW, h, .6f), m.hazard);
            var t = door.transform;
            GameObject Part(string name, Vector3 world, Vector3 size, Material mat, bool solid = true) { var g = Box(name, parent, world, size, mat, solid); g.transform.SetParent(t, true); return g; }
            Part("DoorRight", new Vector3((win + sideW) * .5f, h * .5f, z), new Vector3(sideW, h, .6f), m.hazard);
            Part("DoorBelow", new Vector3(0, (winY - win * .5f) * .5f, z), new Vector3(win, winY - win * .5f, .6f), m.hazard);
            float topY = winY + win * .5f; Part("DoorAbove", new Vector3(0, (topY + h) * .5f, z), new Vector3(win, h - topY, .6f), m.hazard);
            var glass = Part("Porthole", new Vector3(0, winY, z), new Vector3(win, win, .06f), m.glass);
            foreach (var y in new[] { -1, 1 }) Part("PortholeBar", new Vector3(0, winY + y * win * .52f, z - .32f), new Vector3(win + .14f, .07f, .06f), m.trim, false);
            foreach (var x in new[] { -1, 1 }) Part("PortholeBar", new Vector3(x * win * .52f, winY, z - .32f), new Vector3(.07f, win + .14f, .06f), m.trim, false);
            Part("PortholeCross", new Vector3(0, winY, z - .33f), new Vector3(.03f, win, .03f), m.trim, false);
            return door;
        }
    }
}
