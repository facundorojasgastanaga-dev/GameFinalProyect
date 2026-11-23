using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor.Map01
{
    // ---------------- Encounter data model (03_ENCUENTROS.md §A8 + Parte B). Room-local coordinates unless stated. ----------------
    // File: Assets/IronRequiem/Levels/Map01/Data/Encounters/R<NN>.json. Owner: R3.

    public sealed class EncRoom
    {
        public string schema, id, kind, archetype, title, arenaTitle, notes; // kind: rounds | respite | pursuit
        public int seed;
        public float playerClearance = 6f, separationPadding = .5f;
        public float[] spawnInterval;
        public List<string> slotZones;   // zones copied to firstRoomSpawns (= secondRoomSpawns)
        public List<EncWave> waves;
        public List<EncPickup> health;
        public EncRelease release;       // fallback only: the Structure `release` volume wins
        public EncChase chase;
        public List<EncGap> gaps;
        public EncAlarm alarm;
    }
    public sealed class EncWave { public string name; public List<EncGroup> groups; public List<EncComposition> variations; }
    public sealed class EncComposition { public string name; public List<EncGroup> groups; }
    public sealed class EncGroup { public string enemy; public int count = 1; [JsonProperty("fixed")] public EncFixed fixedSlot; }
    /// FixedPoint: a slot of `zone`; with `near` (local), the slot of that zone nearest to it; otherwise the first one.
    public sealed class EncFixed { public string zone; public float[] near; }
    public sealed class EncPickup { public string id; public float[] pos; public float heal = 25f; public bool optional; public string note; }
    public sealed class EncRelease { public string mode; public MinMax volume; public float delay; }
    public sealed class EncChase
    {
        public CenterSize trigger, entranceDoor, exitDoor;
        public float[] carriage, courseCenterLocal;
        public float finishLocalZ, halfWidth = 8f, sawSpeed = 8f, warningSeconds = 2f, bladeRpm = 240f, sawContactOffset = 2.1f, sawDamage = 100f, damageInterval = .35f;
        public bool counterRotateBlades = true;
        public string motorClip;
        public List<EncAmbush> ambushes;
    }
    public sealed class EncAmbush { public string id; public float activationLocalZ; public List<EncAmbushSpawn> spawns; }
    public sealed class EncAmbushSpawn { public string enemy; public float[] pos; }
    public sealed class EncGap
    {
        public string id;
        public float zNear, zFar, xMin, xMax, triggerYMin = -8f, triggerYMax = -3f, upperFloorLocalY, halfWidth, returnOffset = 4f, damage = 20f;
        public float[] returnXs;
    }
    public sealed class EncAlarm
    {
        public List<string> lightAnchors;
        public List<float[]> beacons;
        public List<EncScreen> screens;
        public float[] siren;
        public string sirenClip, klaxonClip, alertText, clearText;
    }
    public sealed class EncScreen { public float[] pos; public float yaw; public string text; }

    /// Enemy aliases used by the encounter data (03_ENCUENTROS.md, códigos de enemigo).
    public static class EncEnemies
    {
        public const string VariantFolder = "Assets/IronRequiem/Prefabs/Map01/Enemies";
        public const string T07MechVariant = VariantFolder + "/T07_Mech_Map01.prefab";
        public const string BigBotVariant = VariantFolder + "/BigBot_Map01.prefab";
        public const string CarroneroVariant = VariantFolder + "/Carronero_Map01.prefab";
        public const string T07MechSource = "Assets/IronRequiem/Prefabs/Campaign/T07_Mech.prefab";
        public const string BigBotSource = "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab";
        public const string CarroneroSource = "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab";
        public const string HealthPickup = "Assets/FPS/Prefabs/Pickups/Pickup_Health.prefab";

        public static readonly Dictionary<string, string> Paths = new Dictionary<string, string>
        {
            { "T07", "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab" },
            { "T07M", T07MechVariant },
            { "SAW", "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab" },
            { "BIG", BigBotVariant },
            { "HOV", "Assets/IronRequiem/Prefabs/Campaign/HoverBot_Current.prefab" },
            { "TUR", "Assets/IronRequiem/Prefabs/Campaign/Turret_Current.prefab" },
            { "CAR", CarroneroVariant },
        };
        public static float Units(string alias) => alias == "CAR" ? .5f : alias == "BIG" ? 2f : 1f;
        public static bool Executable(string alias) => alias == "T07" || alias == "T07M" || alias == "SAW";
        public static bool Sniper(string alias) => alias == "T07" || alias == "T07M";
        public static GameObject Load(string alias)
        {
            if (alias == null || !Paths.TryGetValue(alias, out var path)) throw new ArgumentException("Alias de enemigo desconocido: " + alias);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new FileNotFoundException("Prefab de enemigo ausente (" + alias + "): " + path + ". Ejecuta 'Encounters/Create enemy variants'.");
            return prefab;
        }
    }

    public static class EncounterAuthoring
    {
        const string Menu = Map01Paths.MenuRoot + "Encounters/";
        /// U3 híbrido: false keeps the tested DetachableCannon wiring (the Mech also drops its cannon when it dies, see the delivery note).
        /// true clears DetachableCannon.enemy so only the joint detaches the cannon (no death drop, no duplicate after an execution).
        const bool CutMechDeathDetach = false;
        const float PickupLift = .5f;

        public static EncRoom LoadEncounter(string id) => Map01Json.Load<EncRoom>(Map01Paths.Data("Encounters", id));

        // ---------------------------------------------------------------- menus
        [MenuItem(Menu + "Create enemy variants")]
        public static void CreateEnemyVariants()
        {
            Map01Scenes.GuardForeignDirtyScenes();
            var report = new StringBuilder("Map01 · Encounters · variantes de enemigo · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            CreateEnemyVariantsInternal(report);
            Map01Report.Write("encounters-variants.txt", report.ToString());
        }

        [MenuItem(Menu + "Build all")]
        public static void BuildAll()
        {
            Map01Scenes.GuardForeignDirtyScenes();
            var report = new StringBuilder("Map01 · Encounters · Build all · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n");
            CreateEnemyVariantsInternal(report);

            // 1) Validate every room's data before touching any scene.
            var plan = new List<KeyValuePair<StructureRoom, EncRoom>>();
            var errors = new List<string>(); var warnings = new List<string>();
            foreach (var id in Map01Paths.RoomIds)
            {
                try
                {
                    var room = Map01Json.Room(id); var enc = LoadEncounter(id);
                    EncounterValidation.ValidateData(enc, room, errors, warnings);
                    plan.Add(new KeyValuePair<StructureRoom, EncRoom>(room, enc));
                }
                catch (Exception e) { errors.Add(id + ": " + e.Message); }
            }
            if (errors.Count > 0)
            {
                report.AppendLine("ERRORES DE DATOS (no se tocó ninguna escena):").AppendLine(string.Join("\n", errors));
                Map01Report.Write("encounters-build.txt", report.ToString());
                throw new InvalidOperationException("Map01 Encounters: " + errors.Count + " errores de datos. Primero: " + errors[0]);
            }

            // 2) Build room by room.
            foreach (var pair in plan)
            {
                try { BuildRoom(pair.Key, pair.Value, report, warnings); }
                catch (Exception e)
                {
                    report.AppendLine("ERROR " + pair.Key.id + ": " + e);
                    Map01Report.Write("encounters-build.txt", report.ToString());
                    throw new InvalidOperationException("Map01 Encounters: fallo construyendo " + pair.Key.id + ": " + e.Message, e);
                }
            }
            if (warnings.Count > 0) report.AppendLine().AppendLine("AVISOS:").AppendLine(string.Join("\n", warnings));
            Map01Report.Write("encounters-build.txt", report.ToString());
        }

        // ---------------------------------------------------------------- enemy variants (F2_PLAN §4)
        internal static void CreateEnemyVariantsInternal(StringBuilder report)
        {
            EnsureFolder(EncEnemies.VariantFolder);
            MakeVariant(EncEnemies.T07MechSource, EncEnemies.T07MechVariant, ConfigureMech, report);
            MakeVariant(EncEnemies.BigBotSource, EncEnemies.BigBotVariant, ConfigureBigBot, report);
            MakeVariant(EncEnemies.CarroneroSource, EncEnemies.CarroneroVariant, ConfigureCarronero, report);
            AssetDatabase.SaveAssets();
        }

        static void MakeVariant(string sourcePath, string variantPath, Action<GameObject, StringBuilder> configure, StringBuilder report)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source) throw new FileNotFoundException("Prefab fuente ausente: " + sourcePath);
            bool created = false;
            if (!File.Exists(variantPath))
            {
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                    instance.name = Path.GetFileNameWithoutExtension(variantPath);
                    PrefabUtility.SaveAsPrefabAsset(instance, variantPath, out bool ok);
                    if (!ok) throw new InvalidOperationException("No se pudo crear la variante " + variantPath);
                    created = true;
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            var contents = PrefabUtility.LoadPrefabContents(variantPath);
            try
            {
                configure(contents, report);
                PrefabUtility.SaveAsPrefabAsset(contents, variantPath, out bool saved);
                if (!saved) throw new InvalidOperationException("No se pudo guardar la variante " + variantPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (!asset || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant || PrefabUtility.GetCorrespondingObjectFromSource(asset) != source)
                throw new InvalidOperationException(variantPath + " no es una variante directa de " + sourcePath);
            report.AppendLine((created ? "CREADA  " : "ACTUAL. ") + variantPath + " ← " + sourcePath);
        }

        static void ConfigureMech(GameObject root, StringBuilder report)
        {
            var joints = root.GetComponentsInChildren<DetachableCannon>(true);
            if (joints.Length != 1) throw new InvalidOperationException("T07_Mech_Map01: se esperaba 1 DetachableCannon y hay " + joints.Length);
            var so = new SerializedObject(joints[0]);
            Prop(so, "executionOnly").boolValue = false;
            if (CutMechDeathDetach) Prop(so, "enemy").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            var visual = Prop(so, "moduleVisual").objectReferenceValue as Transform;
            var stolen = visual ? visual.GetComponent<StolenCannon>() : null;
            string weapon = stolen && stolen.WeaponPrefab ? stolen.WeaponPrefab.name : "NINGUNA";
            report.AppendLine("  T07_Mech_Map01: executionOnly = 0; moduleVisual = " + (visual ? visual.name : "NULO") +
                              "; StolenCannon = " + (stolen ? "sí" : "NO") + "; arma = " + weapon +
                              "; desprendimiento al morir = " + (CutMechDeathDetach ? "cortado (enemy = null)" : "activo (cableado original)"));
            if (!stolen) report.AppendLine("  AVISO: el módulo desprendido no tiene StolenCannon: caerá decorativo y desaparecerá a los 30 s (DetachableCannon.cs:69).");
        }

        static void ConfigureBigBot(GameObject root, StringBuilder report)
        {
            // R0-D9: the prefab keeps ALL areas (spawn validation of CombatTwoRoomsLab reads the prefab mask, :111,123);
            // NavAreaMaskOnAwake removes Jump from each instance's agent at runtime.
            var agents = root.GetComponentsInChildren<NavMeshAgent>(true);
            if (agents.Length == 0) throw new InvalidOperationException("BigBot_Map01: sin NavMeshAgent.");
            foreach (var agent in agents)
            {
                agent.areaMask = NavMesh.AllAreas;
                EditorUtility.SetDirty(agent);
                if (!agent.GetComponent<NavAreaMaskOnAwake>()) agent.gameObject.AddComponent<NavAreaMaskOnAwake>();
            }
            report.AppendLine("  BigBot_Map01: areaMask del prefab = todas las áreas; NavAreaMaskOnAwake quita Jump en Awake (" + agents.Length + " agente(s))");
        }

        static void ConfigureCarronero(GameObject root, StringBuilder report)
        {
            var sources = root.GetComponentsInChildren<EnemyExecutionSource>(true);
            foreach (var source in sources)
            {
                var so = new SerializedObject(source);
                Prop(so, "executionEnabled").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            report.AppendLine("  Carronero_Map01: executionEnabled = 0 en " + sources.Length + " EnemyExecutionSource" +
                              (sources.Length == 0 ? " (el prefab no tiene ninguno: ya no era ejecutable)" : ""));
        }

        // ---------------------------------------------------------------- room build
        sealed class Ctx
        {
            public StructureRoom room; public EncRoom enc; public Transform root, structure, layer; public Scene scene;
            public StringBuilder report; public List<string> warnings;
            public readonly Dictionary<string, List<Transform>> zones = new Dictionary<string, List<Transform>>();
            public Vector3 World(float[] local) => root.TransformPoint(Map01Math.V3(local));
        }

        static void BuildRoom(StructureRoom room, EncRoom enc, StringBuilder report, List<string> warnings)
        {
            var scene = Map01Scenes.OpenRoom(room, false);
            if (!scene.GetRootGameObjects().Any(g => g.name == Map01Layers.RoomRootName(room.id)))
                throw new InvalidOperationException(room.id + ": la escena " + room.scene + " no tiene " + Map01Layers.RoomRootName(room.id) + ". Ejecuta antes Structure/Build all.");
            var root = Map01Hierarchy.EnsureRoomRoot(scene, room);
            var ctx = new Ctx { room = room, enc = enc, root = root, scene = scene, report = report, warnings = warnings,
                                structure = Map01Hierarchy.Layer(root, Map01Layers.Structure), layer = Map01Hierarchy.Layer(root, Map01Layers.Encounters) };

            // Resolve everything that can fail before clearing our layer.
            var slotSources = ResolveSlotSources(ctx);
            var lights = enc.alarm != null ? FindAnchorLights(ctx, enc.alarm.lightAnchors) : new Light[0];

            Map01Hierarchy.ClearLayer(ctx.layer);
            report.AppendLine("== " + room.id + " · " + enc.kind + " · " + room.scene);

            CreateSlotTransforms(ctx, slotSources);
            switch (enc.kind)
            {
                case "respite": BuildRespite(ctx); break;
                case "pursuit": BuildPursuit(ctx); break;
                default: BuildRounds(ctx, lights); break;
            }
            BuildHealth(ctx);
            if (enc.gaps != null && enc.gaps.Count > 0) BuildGaps(ctx);
            Map01Scenes.Save(scene);
            report.AppendLine();
        }

        sealed class SlotSource { public string zone, name; public Vector3 local; }

        static List<SlotSource> ResolveSlotSources(Ctx ctx)
        {
            var needed = new List<string>();
            if (ctx.enc.slotZones != null) needed.AddRange(ctx.enc.slotZones);
            foreach (var group in AllGroups(ctx.enc)) if (group.fixedSlot != null && !needed.Contains(group.fixedSlot.zone)) needed.Add(group.fixedSlot.zone);
            var list = new List<SlotSource>();
            foreach (var zone in needed)
            {
                var markers = Map01Markers.SlotsOf(ctx.structure, zone);
                if (markers.Length > 0)
                {
                    foreach (var m in markers) list.Add(new SlotSource { zone = zone, name = m.name, local = ctx.root.InverseTransformPoint(m.position) });
                    continue;
                }
                var data = ctx.room.spawnZones?.FirstOrDefault(z => z.id == zone);
                if (data != null && data.points != null && data.points.Count > 0)
                {
                    for (int i = 0; i < data.points.Count; i++) list.Add(new SlotSource { zone = zone, name = zone + "_" + (i + 1), local = Map01Math.V3(data.points[i]) });
                    ctx.warnings.Add(ctx.room.id + ": zona " + zone + " sin marcadores en [Structure]; se usan los puntos del JSON de Estructura.");
                    continue;
                }
                throw new InvalidOperationException(ctx.room.id + ": la zona de spawn '" + zone + "' no tiene marcadores ([Structure]/Markers/Slots/" + zone + ") ni puntos en el JSON de Estructura. Ejecuta antes Structure/Build all.");
            }
            return list;
        }

        static void CreateSlotTransforms(Ctx ctx, List<SlotSource> sources)
        {
            if (sources.Count == 0) return;
            var spawns = Child(ctx.layer, "Spawns");
            foreach (var zone in sources.GroupBy(source => source.zone))
            {
                var zoneRoot = Child(spawns, zone.Key);
                var list = new List<Transform>();
                foreach (var s in zone)
                {
                    var t = Child(zoneRoot, s.name);
                    t.position = ctx.root.TransformPoint(s.local);
                    list.Add(t);
                }
                ctx.zones[zone.Key] = list;
            }
        }

        static Transform[] SlotList(Ctx ctx)
        {
            var list = new List<Transform>();
            foreach (var zone in ctx.enc.slotZones ?? new List<string>()) list.AddRange(ctx.zones[zone]);
            return list.ToArray();
        }

        static Transform FixedPoint(Ctx ctx, EncFixed fixedSlot)
        {
            if (fixedSlot == null) return null;
            var slots = ctx.zones[fixedSlot.zone];
            if (fixedSlot.near == null) return slots[0];
            var target = ctx.World(fixedSlot.near);
            return slots.OrderBy(t => (t.position - target).sqrMagnitude).First();
        }

        static CombatTwoRoomsLab.Group[] Groups(Ctx ctx, List<EncGroup> groups) =>
            groups.Select(g => new CombatTwoRoomsLab.Group { Prefab = EncEnemies.Load(g.enemy), Count = g.count, FixedPoint = FixedPoint(ctx, g.fixedSlot) }).ToArray();

        static void BuildRounds(Ctx ctx, Light[] alarmLights)
        {
            var enc = ctx.enc;
            var node = Child(ctx.layer, "Encounter").gameObject;
            var queue = node.AddComponent<EnemySpawnQueue>(); queue.Configure(1, 1);
            var lab = node.AddComponent<CombatTwoRoomsLab>();
            var campaignRoom = node.AddComponent<CampaignRoom>();

            var waves = enc.waves.Select(w =>
            {
                var baseGroups = w.groups != null && w.groups.Count > 0 ? w.groups : w.variations[0].groups;
                return new CombatTwoRoomsLab.Wave
                {
                    Groups = Groups(ctx, baseGroups),
                    Variations = (w.variations ?? new List<EncComposition>()).Select(v => new CombatTwoRoomsLab.Composition { Name = v.name, Groups = Groups(ctx, v.groups) }).ToArray(),
                };
            }).ToArray();
            var slots = SlotList(ctx);
            int maxBodies = enc.waves.Max(w => (w.groups != null && w.groups.Count > 0 ? new[] { w.groups } : new List<EncGroup>[0])
                                                .Concat((w.variations ?? new List<EncComposition>()).Select(v => v.groups)).Max(c => EncounterValidation.Bodies(c)));
            int needed = Mathf.CeilToInt(1.5f * maxBodies);
            if (slots.Length < needed) throw new InvalidOperationException(ctx.room.id + ": " + slots.Length + " slots reales en [Structure] < " + needed + " (1,5 × " + maxBodies + " cuerpos, S4)");
            var centroids = enc.slotZones.Where(z => ctx.zones[z].Count > 0).Select(z => new KeyValuePair<string, Vector3>(z, ctx.zones[z].Aggregate(Vector3.zero, (s, t) => s + t.position) / ctx.zones[z].Count)).ToList();
            for (int i = 0; i < centroids.Count; i++)
                for (int j = i + 1; j < centroids.Count; j++)
                {
                    float d = Vector3.Distance(centroids[i].Value, centroids[j].Value);
                    bool big = centroids[i].Key.StartsWith("B", StringComparison.Ordinal) || centroids[j].Key.StartsWith("B", StringComparison.Ordinal);
                    if (d < 15f && !big) ctx.warnings.Add(ctx.room.id + ": grupos " + centroids[i].Key + " y " + centroids[j].Key + " a " + d.ToString("0.0") + " m (< 15 m, S4)");
                }
            lab.Configure(null, waves, slots, slots, null, null);
            lab.UseSpawnQueue(queue);
            lab.ConfigureVariety(true, enc.seed);

            var arena = Map01Math.ToWorld(Map01Math.ToBounds(ctx.room.arena.firstRoom), ctx.room);
            float minInterval = enc.spawnInterval != null && enc.spawnInterval.Length > 0 ? enc.spawnInterval[0] : .2f;
            float maxInterval = enc.spawnInterval != null && enc.spawnInterval.Length > 1 ? enc.spawnInterval[1] : .4f;
            var so = new SerializedObject(lab);
            Prop(so, "singleArena").boolValue = true;
            Prop(so, "arenaTitle").stringValue = enc.arenaTitle;
            Prop(so, "firstRoom").boundsValue = arena;
            Prop(so, "secondRoom").boundsValue = arena; // A5.4: rounds >= 4 use secondRoom*; same arena and same slots.
            Prop(so, "playerClearance").floatValue = enc.playerClearance;
            Prop(so, "minimumSpawnInterval").floatValue = minInterval;
            Prop(so, "maximumSpawnInterval").floatValue = maxInterval;
            Prop(so, "separationPadding").floatValue = enc.separationPadding;
            Prop(so, "gatePreparationSeconds").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            campaignRoom.Configure(lab, null, queue);
            campaignRoom.ConfigureTitle(enc.title);
            EditorUtility.SetDirty(queue); EditorUtility.SetDirty(lab); EditorUtility.SetDirty(campaignRoom);

            for (int i = 0; i < enc.waves.Count; i++)
            {
                var w = enc.waves[i];
                var comps = new List<List<EncGroup>>();
                if (w.groups != null && w.groups.Count > 0) comps.Add(w.groups);
                if (w.variations != null) comps.AddRange(w.variations.Select(v => v.groups));
                string text = string.Join(" | ", comps.Select(c => string.Join(" + ", c.Select(g => g.enemy + "×" + g.count + (g.fixedSlot != null ? "@" + g.fixedSlot.zone : "")))
                                                         + " (" + EncounterValidation.Units(c) + " u / " + EncounterValidation.Bodies(c) + " c)"));
                ctx.report.AppendLine("  Ronda " + (i + 1) + (w.variations != null && w.variations.Count > 0 ? " [variaciones]" : "") + ": " + text);
            }
            ctx.report.AppendLine("  Slots en la lista: " + slots.Length + " (" + string.Join(", ", enc.slotZones) + ") · bounds de mundo " + arena.center + " / " + arena.size + " · semilla " + enc.seed);
            if (enc.alarm != null) BuildAlarm(ctx, lab, alarmLights);
        }

        static void BuildRespite(Ctx ctx)
        {
            var node = Child(ctx.layer, "Encounter").gameObject;
            var campaignRoom = node.AddComponent<CampaignRoom>();
            campaignRoom.ConfigurePrologue(ctx.enc.title);
            EditorUtility.SetDirty(campaignRoom);

            MinMax volume = ctx.room.release != null && ctx.room.release.volume != null ? ctx.room.release.volume : ctx.enc.release?.volume;
            string modeText = ctx.room.release != null && !string.IsNullOrEmpty(ctx.room.release.mode) ? ctx.room.release.mode : ctx.enc.release?.mode;
            if (volume == null) throw new InvalidOperationException(ctx.room.id + ": respiro sin volumen `release` (ni en Estructura ni en Encuentros).");
            if (ctx.room.release == null) ctx.warnings.Add(ctx.room.id + ": el JSON de Estructura no trae `release`; se usa el volumen de reserva de Encuentros.");
            var bounds = Map01Math.ToBounds(volume);
            var go = Child(ctx.layer, "Release").gameObject;
            go.transform.localPosition = bounds.center;
            var release = go.AddComponent<Map01StageRelease>();
            var box = go.GetComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.zero; box.size = bounds.size;
            var so = new SerializedObject(release);
            bool onStart = string.Equals(modeText, "OnStart", StringComparison.OrdinalIgnoreCase);
            Prop(so, "mode").enumValueIndex = onStart ? (int)Map01StageRelease.ReleaseMode.OnStart : (int)Map01StageRelease.ReleaseMode.OnEnterVolume;
            Prop(so, "delaySeconds").floatValue = ctx.enc.release != null ? ctx.enc.release.delay : 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.report.AppendLine("  Respiro: CampaignRoom en modo prólogo (\"" + ctx.enc.title + "\") · Map01StageRelease " + (onStart ? "OnStart" : "OnEnterVolume") + " en local " + bounds.center + " / " + bounds.size);
        }

        static void BuildPursuit(Ctx ctx)
        {
            var c = ctx.enc.chase;
            var origin = Map01Math.V3(ctx.room.worldOrigin);
            var node = Child(ctx.layer, "Encounter").gameObject;
            var queue = node.AddComponent<EnemySpawnQueue>(); queue.Configure(1, 1);
            var campaignRoom = node.AddComponent<CampaignRoom>();

            var chaseRoot = Child(ctx.layer, "Chase");
            var trigger = Map01Math.ToBounds(c.trigger);
            var encounterGo = Child(chaseRoot, "ChaseEncounter").gameObject;
            encounterGo.transform.localPosition = trigger.center;
            var chase = encounterGo.AddComponent<ChaseCorridorEncounter>();
            var box = encounterGo.GetComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.zero; box.size = trigger.size;

            var entrance = GreyBlock(chaseRoot, "EntranceDoor_Greybox", Map01Math.ToBounds(c.entranceDoor), "Hazard", true);
            var exit = GreyBlock(chaseRoot, "ExitDoor_Greybox", Map01Math.ToBounds(c.exitDoor), "Hazard", true);
            var carriage = BuildCarriage(chaseRoot, Map01Math.V3(c.carriage), c.motorClip, ctx, out var blades, out var motor);

            var ambushRoot = Child(chaseRoot, "Ambushes");
            var ambushes = new List<ChaseCorridorEncounter.Ambush>();
            foreach (var a in c.ambushes ?? new List<EncAmbush>())
            {
                var aRoot = Child(ambushRoot, a.id);
                var prefabs = new List<GameObject>(); var points = new List<Transform>();
                for (int i = 0; i < a.spawns.Count; i++)
                {
                    var p = Child(aRoot, "P" + (i + 1) + "_" + a.spawns[i].enemy);
                    p.position = ctx.World(a.spawns[i].pos);
                    prefabs.Add(EncEnemies.Load(a.spawns[i].enemy)); points.Add(p);
                }
                ambushes.Add(new ChaseCorridorEncounter.Ambush { ActivationZ = origin.z + a.activationLocalZ, Prefabs = prefabs.ToArray(), Points = points.ToArray() });
            }

            chase.Configure(null, null, queue, entrance, exit, carriage, blades, motor, null, ambushes.ToArray());
            var cc = Map01Math.V3(c.courseCenterLocal);
            var courseOffset = new Vector3(origin.x + cc.x, origin.y + cc.y, 0f);
            float finishZ = origin.z + c.finishLocalZ;
            var so = new SerializedObject(chase);
            Prop(so, "sawSpeed").floatValue = c.sawSpeed;
            Prop(so, "warningSeconds").floatValue = c.warningSeconds;
            Prop(so, "bladeRpm").floatValue = c.bladeRpm;
            Prop(so, "counterRotateBlades").boolValue = c.counterRotateBlades;
            Prop(so, "sawContactOffset").floatValue = c.sawContactOffset;
            Prop(so, "sawDamage").floatValue = c.sawDamage;
            Prop(so, "damageInterval").floatValue = c.damageInterval;
            Prop(so, "finishZ").floatValue = finishZ;
            Prop(so, "halfWidth").floatValue = c.halfWidth;
            Prop(so, "courseOffset").vector3Value = courseOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
            campaignRoom.Configure(null, chase, queue);
            campaignRoom.ConfigureTitle(ctx.enc.title);
            EditorUtility.SetDirty(queue); EditorUtility.SetDirty(chase); EditorUtility.SetDirty(campaignRoom);

            // Cross-check with the Structure data (R1 writes chase values in WORLD coordinates where the component uses them).
            var s = ctx.room.chase;
            if (s != null)
            {
                if (Mathf.Abs(s.finishZ - finishZ) > .5f) ctx.warnings.Add(ctx.room.id + ": finishZ de Encuentros " + finishZ + " ≠ Estructura " + s.finishZ);
                if (s.courseOffset != null && s.courseOffset.Length >= 2 && Mathf.Abs(s.courseOffset[0] - courseOffset.x) > .5f) ctx.warnings.Add(ctx.room.id + ": courseOffset.x de Encuentros " + courseOffset.x + " ≠ Estructura " + s.courseOffset[0]);
                if (s.ambushes != null && s.ambushes.Count == ambushes.Count)
                    for (int i = 0; i < ambushes.Count; i++)
                        if (Mathf.Abs(s.ambushes[i].activationZ - ambushes[i].ActivationZ) > .5f) ctx.warnings.Add(ctx.room.id + ": ActivationZ " + (i + 1) + " de Encuentros " + ambushes[i].ActivationZ + " ≠ Estructura " + s.ambushes[i].activationZ);
            }
            ctx.report.AppendLine("  Persecución: finishZ " + finishZ + " · courseOffset " + courseOffset + " · halfWidth " + c.halfWidth + " · sierra " + c.sawSpeed + " m/s tras " + c.warningSeconds + " s · emboscadas " +
                                  string.Join(", ", ambushes.Select((a, i) => "A" + (i + 1) + " z " + a.ActivationZ + " [" + string.Join("+", a.Prefabs.Select(p => p.name)) + "]")));
        }

        static Transform BuildCarriage(Transform parent, Vector3 local, string motorClip, Ctx ctx, out Transform[] blades, out AudioSource motor)
        {
            var carriage = Child(parent, "SawCarriage_Greybox");
            carriage.localPosition = local;
            // Frame (no colliders: damage is the swept plane of ChaseCorridorEncounter, ChaseCorridorEncounter.cs:91).
            Visual(carriage, "Beam", new Vector3(0, 7.6f, 0), new Vector3(16, .8f, 1.2f), "Hazard");
            Visual(carriage, "PostW", new Vector3(-7.6f, 4f, 0), new Vector3(.8f, 8f, 1.2f), "Hazard");
            Visual(carriage, "PostE", new Vector3(7.6f, 4f, 0), new Vector3(.8f, 8f, 1.2f), "Hazard");
            var list = new List<Transform>();
            foreach (float y in new[] { 1.3f, 3.5f, 5.7f })
            {
                // Pivot local Z = world X, so Rotate(Vector3.forward, Space.Self) rolls the drum (ChaseCorridorEncounter.cs:87).
                var pivot = Child(carriage, "Roller_" + list.Count);
                pivot.localPosition = new Vector3(0, y, 1.2f); pivot.localRotation = Quaternion.Euler(0, 90, 0);
                var drum = Visual(pivot, "Drum", Vector3.zero, new Vector3(1.8f, 7.6f, 1.8f), "Hazard", PrimitiveType.Cylinder);
                drum.localRotation = Quaternion.Euler(90, 0, 0);
                for (int i = 0; i < 12; i++)
                {
                    float angle = (i % 3) * 120f + (i / 3) * 30f, along = -6f + (i / 3) * 4f;
                    var tooth = Visual(pivot, "Tooth_" + i, new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 1f, Mathf.Sin(angle * Mathf.Deg2Rad) * 1f, along), new Vector3(.3f, .5f, .3f), "LampRed");
                    tooth.localRotation = Quaternion.Euler(0, 0, angle - 90f);
                }
                list.Add(pivot);
            }
            blades = list.ToArray();
            motor = carriage.gameObject.AddComponent<AudioSource>();
            motor.playOnAwake = false; motor.loop = true; motor.spatialBlend = .8f; motor.volume = .7f; motor.rolloffMode = AudioRolloffMode.Linear; motor.maxDistance = 90f;
            if (!string.IsNullOrEmpty(motorClip))
            {
                motor.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(motorClip);
                if (!motor.clip) ctx.warnings.Add(ctx.room.id + ": clip de motor no encontrado: " + motorClip);
            }
            else ctx.warnings.Add(ctx.room.id + ": la trituradora no tiene clip de motor en F2 (el de la sierra está en la lista roja); ChaseCorridorEncounter no reproduce nada sin clip.");
            return carriage;
        }

        static void BuildGaps(Ctx ctx)
        {
            var origin = Map01Math.V3(ctx.room.worldOrigin);
            var gapsRoot = Child(ctx.layer, "Gaps");
            foreach (var g in ctx.enc.gaps)
            {
                var go = Child(gapsRoot, "Gap_" + g.id).gameObject;
                var center = new Vector3((g.xMin + g.xMax) * .5f, (g.triggerYMin + g.triggerYMax) * .5f, (g.zNear + g.zFar) * .5f);
                go.transform.localPosition = center;
                var recovery = go.AddComponent<GapFallRecovery>();
                var box = go.GetComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.zero;
                box.size = new Vector3(g.xMax - g.xMin, g.triggerYMax - g.triggerYMin, g.zFar - g.zNear);
                var xs = g.returnXs != null && g.returnXs.Length > 0 ? g.returnXs : new[] { 0f };
                var near = new List<Transform>(); var far = new List<Transform>();
                var returns = Child(go.transform, "Returns");
                for (int i = 0; i < xs.Length; i++)
                {
                    var n = Child(returns, "Near_" + (i + 1)); n.position = ctx.root.TransformPoint(new Vector3(xs[i], g.upperFloorLocalY, g.zNear - g.returnOffset)); near.Add(n);
                    var f = Child(returns, "Far_" + (i + 1)); f.position = ctx.root.TransformPoint(new Vector3(xs[i], g.upperFloorLocalY, g.zFar + g.returnOffset)); far.Add(f);
                }
                recovery.Configure(null, near.ToArray(), far.ToArray());
                var so = new SerializedObject(recovery);
                Prop(so, "damage").floatValue = g.damage;
                Prop(so, "repeatDelay").floatValue = .75f;
                Prop(so, "upperFloorHeight").floatValue = origin.y + g.upperFloorLocalY;
                Prop(so, "gapNearZ").floatValue = origin.z + g.zNear;
                Prop(so, "gapFarZ").floatValue = origin.z + g.zFar;
                Prop(so, "halfWidth").floatValue = g.halfWidth;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(recovery);
                ctx.report.AppendLine("  Grieta " + g.id + ": z de mundo " + (origin.z + g.zNear) + "…" + (origin.z + g.zFar) + " · upperFloorHeight " + (origin.y + g.upperFloorLocalY) + " · halfWidth " + g.halfWidth + " · " + xs.Length + " retornos por orilla");
            }
            var hazards = ctx.room.hazards?.Where(h => h.type == "gap").ToList();
            if (hazards != null && hazards.Count != ctx.enc.gaps.Count) ctx.warnings.Add(ctx.room.id + ": Estructura declara " + hazards.Count + " grietas y Encuentros " + ctx.enc.gaps.Count);
        }

        static void BuildHealth(Ctx ctx)
        {
            if (ctx.enc.health == null || ctx.enc.health.Count == 0) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EncEnemies.HealthPickup);
            if (!prefab) throw new FileNotFoundException("Botiquín ausente: " + EncEnemies.HealthPickup);
            var healthRoot = Child(ctx.layer, "Health");
            foreach (var h in ctx.enc.health)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ctx.scene);
                Undo.RegisterCreatedObjectUndo(go, "Map01 pickup");
                go.name = "Pickup_Health_" + h.id;
                go.transform.SetParent(healthRoot, true);
                go.transform.position = ctx.World(h.pos) + Vector3.up * PickupLift;
                var pickup = go.GetComponent<HealthPickup>();
                if (!pickup) throw new InvalidOperationException("Pickup_Health sin HealthPickup");
                var so = new SerializedObject(pickup);
                Prop(so, "HealAmount").floatValue = h.heal;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            ctx.report.AppendLine("  Botiquines: " + ctx.enc.health.Count + " × " + ctx.enc.health[0].heal + " PV" + (ctx.enc.health.Any(h => h.optional) ? " (incluye opcionales)" : ""));
        }

        static Light[] FindAnchorLights(Ctx ctx, List<string> anchors)
        {
            var result = new List<Light>();
            if (anchors == null) return result.ToArray();
            var lighting = ctx.root.Find(Map01Layers.Lighting);
            var all = lighting ? lighting.GetComponentsInChildren<Light>(true) : new Light[0];
            foreach (var id in anchors)
            {
                var light = all.FirstOrDefault(l => l.name == id || (l.transform.parent && l.transform.parent.name == id));
                if (light) result.Add(light);
                else ctx.warnings.Add(ctx.room.id + ": luz de alarma '" + id + "' no encontrada en [Lighting] (R5 aún no la ha creado). CorridorAlarm queda sin ella; vuelve a ejecutar Build all tras Lighting.");
            }
            return result.ToArray();
        }

        static void BuildAlarm(Ctx ctx, CombatTwoRoomsLab lab, Light[] lights)
        {
            var a = ctx.enc.alarm;
            var root = Child(ctx.layer, "Alarm");
            var beacons = new List<Transform>(); var heads = new List<Renderer>();
            if (a.beacons != null)
                for (int i = 0; i < a.beacons.Count; i++)
                {
                    var pivot = Child(root, "Beacon_" + (i + 1));
                    pivot.position = ctx.World(a.beacons[i]);
                    heads.Add(Visual(pivot, "Head", Vector3.zero, new Vector3(.5f, .3f, .5f), "LampRed").GetComponent<Renderer>());
                    heads.Add(Visual(pivot, "Lens", new Vector3(0, 0, .32f), new Vector3(.18f, .26f, .4f), "LampRed").GetComponent<Renderer>());
                    beacons.Add(pivot);
                }
            AudioSource siren = null;
            if (a.siren != null)
            {
                var sirenGo = Child(root, "Siren");
                sirenGo.position = ctx.World(a.siren);
                siren = sirenGo.gameObject.AddComponent<AudioSource>();
                siren.playOnAwake = false; siren.loop = true; siren.spatialBlend = .5f; siren.volume = .55f; siren.rolloffMode = AudioRolloffMode.Linear; siren.maxDistance = 70f;
                siren.clip = string.IsNullOrEmpty(a.sirenClip) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(a.sirenClip);
                if (!siren.clip) ctx.warnings.Add(ctx.room.id + ": clip de sirena no encontrado: " + a.sirenClip);
            }
            var klaxon = string.IsNullOrEmpty(a.klaxonClip) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(a.klaxonClip);
            if (!string.IsNullOrEmpty(a.klaxonClip) && !klaxon) ctx.warnings.Add(ctx.room.id + ": clip de klaxon no encontrado: " + a.klaxonClip);
            var screens = new List<TMP_Text>();
            if (a.screens != null)
                for (int i = 0; i < a.screens.Count; i++)
                {
                    var s = a.screens[i];
                    var go = new GameObject("Screen_" + (i + 1));
                    Undo.RegisterCreatedObjectUndo(go, "Map01 screen");
                    go.transform.SetParent(root, false);
                    var tmp = go.AddComponent<TextMeshPro>();
                    go.transform.position = ctx.World(s.pos);
                    go.transform.rotation = Quaternion.Euler(0, s.yaw, 0);
                    tmp.text = string.IsNullOrEmpty(s.text) ? ctx.enc.arenaTitle : s.text;
                    tmp.fontSize = 3f; tmp.color = new Color(1f, .25f, .15f); tmp.alignment = TextAlignmentOptions.Center;
                    tmp.rectTransform.sizeDelta = new Vector2(4.5f, 1.4f);
                    screens.Add(tmp);
                }
            var alarm = root.gameObject.AddComponent<CorridorAlarm>();
            alarm.Configure(lab, lights, beacons.ToArray(), new Light[0], heads.ToArray(), siren, klaxon, screens.ToArray());
            var so = new SerializedObject(alarm);
            if (!string.IsNullOrEmpty(a.alertText)) Prop(so, "alertText").stringValue = a.alertText;
            if (!string.IsNullOrEmpty(a.clearText)) Prop(so, "clearText").stringValue = a.clearText;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(alarm);
            ctx.report.AppendLine("  Alarma: CorridorAlarm con " + lights.Length + "/" + (a.lightAnchors?.Count ?? 0) + " luces de anclaje, " + beacons.Count + " balizas, " + screens.Count + " pantallas, sirena " + (siren && siren.clip ? siren.clip.name : "sin clip"));
        }

        // ---------------------------------------------------------------- helpers
        internal static IEnumerable<EncGroup> AllGroups(EncRoom enc)
        {
            if (enc.waves == null) yield break;
            foreach (var w in enc.waves)
            {
                if (w.groups != null) foreach (var g in w.groups) yield return g;
                if (w.variations != null) foreach (var v in w.variations) if (v.groups != null) foreach (var g in v.groups) yield return g;
            }
        }

        internal static SerializedProperty Prop(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null) throw new InvalidOperationException(so.targetObject.GetType().Name + " no tiene el campo serializado '" + name + "'");
            return p;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Map01 encounters");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Environment/Bunker/Bunker_" + name + ".mat");

        /// Primitive without collider (presentation only).
        static Transform Visual(Transform parent, string name, Vector3 localPosition, Vector3 localScale, string material, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>(); if (collider) Object.DestroyImmediate(collider);
            Undo.RegisterCreatedObjectUndo(go, "Map01 encounters");
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition; go.transform.localScale = localScale;
            var mat = Mat(material); if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        /// Solid greybox block (BoxCollider kept): chase doors.
        static GameObject GreyBlock(Transform parent, string name, UnityEngine.Bounds local, string material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!solid) { var collider = go.GetComponent<Collider>(); if (collider) Object.DestroyImmediate(collider); }
            Undo.RegisterCreatedObjectUndo(go, "Map01 encounters");
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local.center; go.transform.localScale = local.size;
            var mat = Mat(material); if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
