using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor.Map01
{
    /// Data and scene checks for the Encounters layer (03_ENCUENTROS.md §A3-§A5, §A8.4 V1-V12, Parte B).
    public static class EncounterValidation
    {
        const string Menu = Map01Paths.MenuRoot + "Encounters/";
        const int MaxBodiesPerRoom = 36;

        public static float Units(List<EncGroup> groups) => groups.Sum(g => EncEnemies.Units(g.enemy) * g.count);
        public static int Bodies(List<EncGroup> groups) => groups.Sum(g => g.count);
        static int Count(List<EncGroup> groups, Func<string, bool> match) => groups.Where(g => match(g.enemy)).Sum(g => g.count);

        static IEnumerable<KeyValuePair<string, List<EncGroup>>> Compositions(EncWave w)
        {
            if (w.groups != null && w.groups.Count > 0) yield return new KeyValuePair<string, List<EncGroup>>("base", w.groups);
            if (w.variations != null) foreach (var v in w.variations) yield return new KeyValuePair<string, List<EncGroup>>(v.name ?? "variación", v.groups);
        }

        static bool IsBigZone(StructureRoom room, string zone)
        {
            var data = room.spawnZones?.FirstOrDefault(z => z.id == zone);
            if (data != null && !string.IsNullOrEmpty(data.kind)) return data.kind == "big";
            return zone.StartsWith("BS", StringComparison.Ordinal) || zone.StartsWith("BG", StringComparison.Ordinal);
        }

        static int SlotsOfZone(StructureRoom room, string zone)
        {
            var data = room.spawnZones?.FirstOrDefault(z => z.id == zone);
            if (data == null) return -1;
            return data.points != null && data.points.Count > 0 ? data.points.Count : data.slots;
        }

        /// Pure data checks (no scene). Appends "<ID>: <campo>: <motivo>" to errors / warnings.
        public static void ValidateData(EncRoom enc, StructureRoom room, List<string> errors, List<string> warnings)
        {
            string id = room.id;
            void Err(string field, string why) => errors.Add(id + ": " + field + ": " + why);
            void Warn(string field, string why) => warnings.Add(id + ": " + field + ": " + why);

            if (enc == null) { Err("Encounters", "JSON vacío"); return; }
            if (room.worldOrigin == null || room.worldOrigin.Length < 3) { Err("worldOrigin", "Estructura sin origen de mundo"); return; }
            if (enc.id != room.id) Err("id", "'" + enc.id + "' no coincide con Estructura '" + room.id + "'");
            if (string.IsNullOrWhiteSpace(enc.title)) Err("title", "vacío: el HUD mostraría «TRES RONDAS» (CampaignFlow.cs:273)");
            string kind = enc.kind ?? "rounds";
            if (kind != "rounds" && kind != "respite" && kind != "pursuit") Err("kind", "'" + kind + "' no es rounds | respite | pursuit");
            string expected = room.function == "respite" ? "respite" : room.function == "chase" ? "pursuit" : "rounds";
            if (!string.IsNullOrEmpty(room.function) && expected != kind) Warn("kind", "'" + kind + "' frente a function '" + room.function + "' de Estructura");

            foreach (var h in enc.health ?? new List<EncPickup>())
            {
                if (h.pos == null || h.pos.Length < 3) Err("health." + h.id, "pos sin 3 componentes");
                if (Mathf.Abs(h.heal - 25f) > .01f) Err("health." + h.id, "heal " + h.heal + " (debe ser 25; el prefab trae 40)");
            }

            if (kind == "respite")
            {
                if (enc.waves != null && enc.waves.Count > 0) Err("waves", "un respiro no lleva rondas");
                if ((room.release == null || room.release.volume == null) && (enc.release == null || enc.release.volume == null)) Err("release", "sin volumen (ni Estructura ni Encuentros): el respiro sería un soft-lock");
                return;
            }

            if (kind == "pursuit")
            {
                var c = enc.chase;
                if (c == null) { Err("chase", "falta"); return; }
                if (c.trigger == null || c.entranceDoor == null || c.exitDoor == null || c.carriage == null) Err("chase", "faltan trigger, entranceDoor, exitDoor o carriage");
                if (c.halfWidth <= 0 || c.halfWidth > 8.01f) Err("chase.halfWidth", c.halfWidth + " (A6.2: ≤ 8)");
                if (c.trigger != null && c.finishLocalZ <= Map01Math.V3(c.trigger.center).z) Err("chase.finishLocalZ", "no está por delante del trigger");
                foreach (var a in c.ambushes ?? new List<EncAmbush>())
                {
                    if (a.spawns == null || a.spawns.Count == 0 || a.spawns.Count > 2) Err("chase.ambush." + a.id, "debe tener 1–2 enemigos (A6.2)");
                    foreach (var s in a.spawns ?? new List<EncAmbushSpawn>())
                    {
                        if (s.enemy == "BIG" || s.enemy == "TUR") Err("chase.ambush." + a.id, s.enemy + " prohibido en persecución");
                        if (!EncEnemies.Paths.ContainsKey(s.enemy ?? "")) Err("chase.ambush." + a.id, "alias desconocido " + s.enemy);
                        else if (!AssetDatabase.LoadAssetAtPath<GameObject>(EncEnemies.Paths[s.enemy])) Err("chase.ambush." + a.id, "no existe " + EncEnemies.Paths[s.enemy]);
                        if (s.pos == null || s.pos.Length < 3 || s.pos[2] < a.activationLocalZ + 6f) Err("chase.ambush." + a.id, "punto a < 6 m por delante de la activación (ChaseCorridorEncounter.cs:115)");
                    }
                }
                ValidateGaps(enc, room, Err, Warn);
                return;
            }

            // ---- rounds
            if (room.arena == null || room.arena.firstRoom == null) Err("arena.firstRoom", "Estructura no define la arena");
            if (string.IsNullOrWhiteSpace(enc.arenaTitle)) Err("arenaTitle", "vacío");
            if (enc.seed == 0) Warn("seed", "0 = aleatoria por partida; en F2 se pide una semilla fija (A5.2)");
            if (enc.waves == null || enc.waves.Count == 0) { Err("waves", "sin rondas"); return; }
            if (enc.slotZones == null || enc.slotZones.Count == 0) { Err("slotZones", "sin zonas de spawn"); return; }

            foreach (var zone in enc.slotZones) if (SlotsOfZone(room, zone) < 0) Err("slotZones", "la zona '" + zone + "' no existe en spawnZones de Estructura");
            int listSlots = enc.slotZones.Sum(z => Math.Max(0, SlotsOfZone(room, z)));
            int bigSlots = enc.slotZones.Where(z => IsBigZone(room, z)).Sum(z => Math.Max(0, SlotsOfZone(room, z)));
            int normalZones = enc.slotZones.Count(z => !IsBigZone(room, z));
            var turretZones = new HashSet<string>();
            int roomBodies = 0;

            for (int i = 0; i < enc.waves.Count; i++)
            {
                var w = enc.waves[i]; string wf = "waves[" + (i + 1) + "]";
                var comps = Compositions(w).ToList();
                if (comps.Count == 0) { Err(wf, "sin groups ni variations"); continue; }
                int maxBodies = 0;
                var waveTurretZones = new HashSet<string>();
                KeyValuePair<string, List<EncGroup>>? reference = null;
                foreach (var comp in comps)
                {
                    string f = wf + "." + comp.Key;
                    if (comp.Value == null || comp.Value.Count == 0) { Err(f, "composición vacía"); continue; }
                    bool groupsOk = true;
                    foreach (var g in comp.Value)
                    {
                        if (g.enemy == null || !EncEnemies.Paths.ContainsKey(g.enemy)) { Err(f, "alias desconocido '" + g.enemy + "'"); groupsOk = false; }
                        if (g.count < 1) { Err(f, "count < 1"); groupsOk = false; }
                        if (g.fixedSlot != null)
                        {
                            if (string.IsNullOrEmpty(g.fixedSlot.zone) || SlotsOfZone(room, g.fixedSlot.zone) < 0) Err(f, "FixedPoint en zona inexistente '" + g.fixedSlot?.zone + "'");
                            if (g.count > 1 && g.enemy == "TUR") Err(f, "TUR con FixedPoint y count > 1: la segunda nunca cabe (A5.3)");
                        }
                        if (g.enemy == "TUR")
                        {
                            if (g.fixedSlot == null || g.count != 1) Err(f, "TUR exige count 1 y FixedPoint propio (A4.3)");
                            else waveTurretZones.Add(g.fixedSlot.zone);
                        }
                    }
                    if (!groupsOk) continue;
                    var groups = comp.Value;
                    float units = Units(groups); int bodies = Bodies(groups);
                    int exec = Count(groups, EncEnemies.Executable), snipers = Count(groups, EncEnemies.Sniper);
                    int big = Count(groups, e => e == "BIG"), tur = Count(groups, e => e == "TUR"), car = Count(groups, e => e == "CAR"), saw = Count(groups, e => e == "SAW");
                    int freeBig = groups.Where(g => g.enemy == "BIG" && g.fixedSlot == null).Sum(g => g.count);
                    maxBodies = Math.Max(maxBodies, bodies);
                    if (units > 8f) Err(f, units + " unidades (> 8, §5 de 00_INVENTARIO)");
                    if (bodies > 12) Err(f, bodies + " cuerpos (> 12)");
                    if (units >= 4f && exec < 1) Err(f, "≥ 4 unidades sin ejecutable (R-E1)");
                    if (big > 0 && (exec < 2 * big || snipers < 1)) Err(f, big + " BIG con " + exec + " ejecutables / " + snipers + " T07 (R-E2: ≥ 2 por BIG y ≥ 1 T07)");
                    if (tur > 0 && snipers < 1) Err(f, "TUR sin T07/T07M en la ronda (R-E3)");
                    if (car >= 6 && saw < 1) Err(f, car + " CAR sin SAW (R-E4)");
                    if (snipers > 4) Err(f, snipers + " T07/T07M (> 4, R-E6)");
                    if (freeBig > 0 && bigSlots < 2 * freeBig) Err(f, freeBig + " BIG libres con " + bigSlots + " slots grandes en la lista (V4: 2 por BIG)");
                    if (reference == null) reference = comp;
                    else
                    {
                        var r = reference.Value.Value;
                        if (Count(r, EncEnemies.Executable) != exec || Count(r, e => e == "BIG") != big || Count(r, e => e == "TUR") != tur || Mathf.Abs(Units(r) - units) > .51f)
                            Err(f, "rompe R-E7 frente a '" + reference.Value.Key + "' (fuentes, pesados o unidades ± 0,5)");
                    }
                }
                foreach (var zone in waveTurretZones)
                    if (!turretZones.Add(zone)) Err(wf, "slot fijo de TUR '" + zone + "' reutilizado en el cuarto: los restos de la primera podrían ocuparlo (A5.3)");
                roomBodies += maxBodies;
                int needed = Mathf.CeilToInt(1.5f * maxBodies);
                if (listSlots < needed) Err(wf + ".slots", listSlots + " slots en la lista < " + needed + " (1,5 × " + maxBodies + " cuerpos, S4)");
                int neededZones = maxBodies >= 8 ? 4 : 3;
                if (normalZones < neededZones && maxBodies > 3) Err(wf + ".slots", normalZones + " grupos normales < " + neededZones + " (S4)");
            }
            foreach (var alias in EncounterAuthoring.AllGroups(enc).Select(g => g.enemy).Where(a => a != null && EncEnemies.Paths.ContainsKey(a)).Distinct())
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(EncEnemies.Paths[alias])) Err("prefab." + alias, "no existe " + EncEnemies.Paths[alias] + " (las variantes Map01 las crea 'Create enemy variants')");
            if (roomBodies > MaxBodiesPerRoom) Warn("waves", roomBodies + " cuerpos en el cuarto (> " + MaxBodiesPerRoom + ", V11)");
            if (enc.waves.Count >= 4) Warn("waves", enc.waves.Count + " rondas: secondRoom y secondRoomSpawns se rellenan con la arena y los slots (A5.4)");
            if (enc.alarm != null)
                foreach (var anchor in enc.alarm.lightAnchors ?? new List<string>())
                    if (room.lightAnchors != null && !room.lightAnchors.Any(l => l.id == anchor)) Warn("alarm.lightAnchors", "'" + anchor + "' no existe en lightAnchors de Estructura");
            ValidateGaps(enc, room, Err, Warn);
        }

        static void ValidateGaps(EncRoom enc, StructureRoom room, Action<string, string> err, Action<string, string> warn)
        {
            if (enc.gaps == null) return;
            foreach (var g in enc.gaps)
            {
                if (g.zFar <= g.zNear) err("gaps." + g.id, "zFar ≤ zNear");
                if (g.xMax <= g.xMin) err("gaps." + g.id, "xMax ≤ xMin");
                if (g.triggerYMax <= g.triggerYMin) err("gaps." + g.id, "triggerYMax ≤ triggerYMin");
                float worldXLimit = Mathf.Max(Mathf.Abs(room.worldOrigin[0] + g.xMin), Mathf.Abs(room.worldOrigin[0] + g.xMax));
                if (g.halfWidth < worldXLimit) err("gaps." + g.id, "halfWidth " + g.halfWidth + " < |x de mundo| " + worldXLimit + " (GapFallRecovery.cs:34 usa la X de mundo; E-7)");
            }
        }

        // ---------------------------------------------------------------- menu
        [MenuItem(Menu + "Validate all")]
        public static void ValidateAll()
        {
            Map01Scenes.GuardForeignDirtyScenes();
            var errors = new List<string>(); var warnings = new List<string>();
            var report = new StringBuilder("Map01 · Encounters · Validate all · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n");
            ValidateVariants(errors, warnings, report);
            foreach (var id in Map01Paths.RoomIds)
            {
                StructureRoom room; EncRoom enc;
                try { room = Map01Json.Room(id); enc = EncounterAuthoring.LoadEncounter(id); }
                catch (Exception e) { errors.Add(id + ": " + e.Message); continue; }
                ValidateData(enc, room, errors, warnings);
                string scenePath = Map01Paths.ScenePath(room.scene);
                if (!File.Exists(scenePath)) { warnings.Add(id + ": escena sin crear (" + scenePath + "); solo se validaron datos."); continue; }
                try { ValidateScene(room, enc, errors, warnings, report); }
                catch (Exception e) { errors.Add(id + ": escena: " + e.Message); }
            }
            report.AppendLine().AppendLine("ERRORES (" + errors.Count + "):").AppendLine(string.Join("\n", errors));
            report.AppendLine().AppendLine("AVISOS (" + warnings.Count + "):").AppendLine(string.Join("\n", warnings));
            Map01Report.Write("encounters-validate.txt", report.ToString());
            if (errors.Count > 0) throw new InvalidOperationException("Map01 Encounters: " + errors.Count + " errores. Primero: " + errors[0]);
            Debug.Log("[Map01] Encounters: validación correcta (" + warnings.Count + " avisos).");
        }

        static void ValidateVariants(List<string> errors, List<string> warnings, StringBuilder report)
        {
            var mech = AssetDatabase.LoadAssetAtPath<GameObject>(EncEnemies.T07MechVariant);
            if (!mech) errors.Add("Variantes: falta " + EncEnemies.T07MechVariant);
            else
            {
                var joint = mech.GetComponentInChildren<DetachableCannon>(true);
                var so = joint ? new SerializedObject(joint) : null;
                if (so == null || EncounterAuthoring.Prop(so, "executionOnly").boolValue) errors.Add("Variantes: T07_Mech_Map01 sigue con executionOnly = 1");
                var visual = so != null ? EncounterAuthoring.Prop(so, "moduleVisual").objectReferenceValue as Transform : null;
                if (!visual || !visual.GetComponent<StolenCannon>()) errors.Add("Variantes: el módulo del T07_Mech_Map01 no es recogible (sin StolenCannon)");
                bool deathDetach = so != null && EncounterAuthoring.Prop(so, "enemy").objectReferenceValue;
                if (deathDetach) warnings.Add("Variantes: T07_Mech_Map01 suelta el cañón también al morir, incluida la ejecución (posible cañón duplicado; ver entrega de R3).");
                report.AppendLine("T07_Mech_Map01: unión recogible " + (visual && visual.GetComponent<StolenCannon>() ? "sí" : "NO") + "; suelta al morir: " + (deathDetach ? "sí" : "no"));
            }
            var big = AssetDatabase.LoadAssetAtPath<GameObject>(EncEnemies.BigBotVariant);
            int jump = NavMesh.GetAreaFromName("Jump");
            if (!big) errors.Add("Variantes: falta " + EncEnemies.BigBotVariant);
            else foreach (var agent in big.GetComponentsInChildren<NavMeshAgent>(true))
                {
                    // R0-D9: prefab mask = all areas (same spawn validation as the rest); Jump is removed at runtime by NavAreaMaskOnAwake.
                    long walkable = new SerializedObject(agent).FindProperty("m_WalkableMask").longValue;
                    if (jump < 0 || (walkable & (1L << jump)) == 0) errors.Add("Variantes: BigBot_Map01 (" + agent.name + ") excluye Jump en el prefab: la aparición se bloquearía con el jugador en un refugio (R0-D9)");
                    if (!agent.GetComponent<NavAreaMaskOnAwake>()) errors.Add("Variantes: BigBot_Map01 (" + agent.name + ") sin NavAreaMaskOnAwake: el Big Bot subiría por las rampas Jump");
                }
            var car = AssetDatabase.LoadAssetAtPath<GameObject>(EncEnemies.CarroneroVariant);
            if (!car) errors.Add("Variantes: falta " + EncEnemies.CarroneroVariant);
            else foreach (var source in car.GetComponentsInChildren<EnemyExecutionSource>(true))
                    if (source.ExecutionEnabled) errors.Add("Variantes: Carronero_Map01 sigue siendo ejecutable");
        }

        static void ValidateScene(StructureRoom room, EncRoom enc, List<string> errors, List<string> warnings, StringBuilder report)
        {
            string id = room.id;
            var scene = Map01Scenes.OpenRoom(room, false);
            var rootGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Map01Layers.RoomRootName(id));
            if (!rootGo) { errors.Add(id + ": sin root " + Map01Layers.RoomRootName(id)); return; }
            var layer = rootGo.transform.Find(Map01Layers.Encounters);
            if (!layer || layer.childCount == 0) { errors.Add(id + ": capa [Encounters] vacía: ejecuta Encounters/Build all"); return; }
            var rooms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CampaignRoom>(true)).ToArray();
            if (rooms.Length != 1) errors.Add(id + ": " + rooms.Length + " CampaignRoom en la escena (debe haber 1)");
            if (rooms.Length == 0) return;
            var campaignRoom = rooms[0];
            if (!campaignRoom.HasTitle) errors.Add(id + ": CampaignRoom sin prologueTitle");

            var pickups = layer.GetComponentsInChildren<HealthPickup>(true);
            if (pickups.Length != (enc.health?.Count ?? 0)) errors.Add(id + ": " + pickups.Length + " botiquines en escena frente a " + (enc.health?.Count ?? 0) + " en datos");
            foreach (var p in pickups) if (Mathf.Abs(p.HealAmount - 25f) > .01f) errors.Add(id + ": " + p.name + " cura " + p.HealAmount + " (≠ 25)");

            string kind = enc.kind ?? "rounds";
            if (kind == "respite")
            {
                if (!campaignRoom.IsPrologue) errors.Add(id + ": el respiro tiene rondas o persecución");
                var release = layer.GetComponentInChildren<Map01StageRelease>(true);
                if (!release) errors.Add(id + ": respiro sin Map01StageRelease (soft-lock)");
                else if (!release.GetComponent<BoxCollider>().isTrigger) errors.Add(id + ": el BoxCollider de Map01StageRelease no es trigger");
                report.AppendLine(id + ": respiro OK");
                return;
            }
            if (kind == "pursuit")
            {
                var chase = campaignRoom.Pursuit;
                if (!chase) { errors.Add(id + ": CampaignRoom.pursuit vacío"); return; }
                var so = new SerializedObject(chase);
                foreach (var field in new[] { "queue", "entranceDoor", "exitDoor", "carriage", "motor" })
                    if (!EncounterAuthoring.Prop(so, field).objectReferenceValue) errors.Add(id + ": ChaseCorridorEncounter." + field + " vacío");
                var box = chase.GetComponent<BoxCollider>();
                if (!box || !box.isTrigger) errors.Add(id + ": el trigger de la persecución no es trigger");
                if (chase.Ambushes == null || chase.Ambushes.Length != (enc.chase.ambushes?.Count ?? 0)) errors.Add(id + ": emboscadas en escena ≠ datos");
                else foreach (var a in chase.Ambushes)
                    {
                        if (a.Prefabs == null || a.Points == null || a.Prefabs.Length != a.Points.Length) { errors.Add(id + ": emboscada con Prefabs y Points de distinto tamaño"); continue; }
                        if (a.Prefabs.Any(p => !p) || a.Points.Any(p => !p)) errors.Add(id + ": emboscada con referencias nulas");
                        else if (a.Points.Any(p => p.position.z < a.ActivationZ + 6f)) errors.Add(id + ": punto de emboscada a < 6 m de su ActivationZ");
                    }
                ValidateGapScene(id, layer, enc, errors);
                CheckNavPoints(id, chase.Ambushes?.SelectMany(a => a.Points ?? new Transform[0]).Where(p => p).ToArray() ?? new Transform[0], NavMesh.AllAreas, warnings, "emboscada");
                report.AppendLine(id + ": persecución OK (" + (chase.Ambushes?.Length ?? 0) + " emboscadas)");
                return;
            }

            var lab = campaignRoom.Rounds;
            if (!lab) { errors.Add(id + ": CampaignRoom.rounds vacío"); return; }
            var lso = new SerializedObject(lab);
            if (!EncounterAuthoring.Prop(lso, "singleArena").boolValue) errors.Add(id + ": singleArena = false");
            if (!EncounterAuthoring.Prop(lso, "variedSpawns").boolValue) errors.Add(id + ": variedSpawns = false (el camino no variado lanza excepción, CombatTwoRoomsLab.cs:80)");
            if (!EncounterAuthoring.Prop(lso, "spawnQueue").objectReferenceValue) errors.Add(id + ": sin spawnQueue");
            var waves = EncounterAuthoring.Prop(lso, "waves");
            if (waves.arraySize != enc.waves.Count) errors.Add(id + ": " + waves.arraySize + " rondas en escena frente a " + enc.waves.Count);
            var first = ReadTransforms(EncounterAuthoring.Prop(lso, "firstRoomSpawns"));
            var second = ReadTransforms(EncounterAuthoring.Prop(lso, "secondRoomSpawns"));
            if (first.Length == 0) errors.Add(id + ": firstRoomSpawns vacío");
            if (enc.waves.Count >= 4 && second.Length == 0) errors.Add(id + ": ≥ 4 rondas con secondRoomSpawns vacío (soft-lock, A5.4)");
            var bounds = EncounterAuthoring.Prop(lso, "firstRoom").boundsValue;
            var expected = Map01Math.ToWorld(Map01Math.ToBounds(room.arena.firstRoom), room);
            if ((bounds.center - expected.center).sqrMagnitude > .01f || (bounds.size - expected.size).sqrMagnitude > .01f) errors.Add(id + ": firstRoom " + bounds + " ≠ arena de Estructura " + expected);
            foreach (var t in first) if (!bounds.Contains(t.position + Vector3.up * .1f)) errors.Add(id + ": slot " + t.name + " fuera de los bounds de la arena");
            var fixedPoints = new List<Transform>();
            for (int i = 0; i < waves.arraySize; i++)
            {
                var wave = waves.GetArrayElementAtIndex(i);
                foreach (var listName in new[] { "Groups" })
                    CheckGroups(id, i, wave.FindPropertyRelative(listName), errors, fixedPoints);
                var variations = wave.FindPropertyRelative("Variations");
                for (int v = 0; v < variations.arraySize; v++) CheckGroups(id, i, variations.GetArrayElementAtIndex(v).FindPropertyRelative("Groups"), errors, fixedPoints);
            }
            foreach (var t in fixedPoints) if (!bounds.Contains(t.position + Vector3.up * .1f)) errors.Add(id + ": FixedPoint " + t.name + " fuera de los bounds de la arena");
            CheckNavPoints(id, first.Concat(fixedPoints).Distinct().ToArray(), NavMesh.AllAreas, warnings, "slot");
            int jump = NavMesh.GetAreaFromName("Jump");
            if (jump >= 0)
            {
                var bigSlots = first.Concat(fixedPoints).Where(t => t.parent && (t.parent.name.StartsWith("BS", StringComparison.Ordinal) || t.parent.name.StartsWith("BG", StringComparison.Ordinal) || t.parent.name == "C0")).Distinct().ToArray();
                CheckNavPoints(id, bigSlots, NavMesh.AllAreas & ~(1 << jump), warnings, "slot grande (máscara sin Jump)");
            }
            if (enc.alarm != null)
            {
                var alarm = layer.GetComponentInChildren<CorridorAlarm>(true);
                if (!alarm) errors.Add(id + ": falta CorridorAlarm");
                else
                {
                    var aso = new SerializedObject(alarm);
                    var lights = EncounterAuthoring.Prop(aso, "alarmLights");
                    int found = 0;
                    for (int i = 0; i < lights.arraySize; i++)
                    {
                        var light = lights.GetArrayElementAtIndex(i).objectReferenceValue as Light;
                        if (!light) continue;
                        found++;
                        if (!light.enabled || !light.gameObject.activeSelf) warnings.Add(id + ": la luz de alarma " + light.name + " está desactivada; CorridorAlarm solo cambia su intensidad (CorridorAlarm.cs:80), así que no se verá");
                    }
                    if (found < (enc.alarm.lightAnchors?.Count ?? 0)) warnings.Add(id + ": CorridorAlarm con " + found + "/" + enc.alarm.lightAnchors.Count + " luces de alarma (¿falta Lighting?)");
                    if (EncounterAuthoring.Prop(aso, "rounds").objectReferenceValue != lab) errors.Add(id + ": CorridorAlarm no observa las rondas del cuarto");
                }
            }
            ValidateGapScene(id, layer, enc, errors);
            report.AppendLine(id + ": rondas OK (" + waves.arraySize + " rondas, " + first.Length + " slots)");
        }

        static void CheckGroups(string id, int wave, SerializedProperty groups, List<string> errors, List<Transform> fixedPoints)
        {
            if (groups == null) return;
            for (int g = 0; g < groups.arraySize; g++)
            {
                var group = groups.GetArrayElementAtIndex(g);
                if (!group.FindPropertyRelative("Prefab").objectReferenceValue) errors.Add(id + ": ronda " + (wave + 1) + " con Prefab nulo");
                if (group.FindPropertyRelative("Count").intValue < 1) errors.Add(id + ": ronda " + (wave + 1) + " con Count < 1");
                var point = group.FindPropertyRelative("FixedPoint").objectReferenceValue as Transform;
                if (point) fixedPoints.Add(point);
            }
        }

        static void ValidateGapScene(string id, Transform layer, EncRoom enc, List<string> errors)
        {
            int expected = enc.gaps?.Count ?? 0;
            var gaps = layer.GetComponentsInChildren<GapFallRecovery>(true);
            if (gaps.Length != expected) errors.Add(id + ": " + gaps.Length + " GapFallRecovery en escena frente a " + expected + " en datos");
            foreach (var g in gaps)
            {
                var so = new SerializedObject(g);
                if (ReadTransforms(EncounterAuthoring.Prop(so, "nearReturns")).Length == 0 || ReadTransforms(EncounterAuthoring.Prop(so, "farReturns")).Length == 0) errors.Add(id + ": " + g.name + " sin puntos de retorno");
                if (!g.GetComponent<BoxCollider>().isTrigger) errors.Add(id + ": " + g.name + " no es trigger");
            }
        }

        static Transform[] ReadTransforms(SerializedProperty array)
        {
            var list = new List<Transform>();
            for (int i = 0; i < array.arraySize; i++) { var t = array.GetArrayElementAtIndex(i).objectReferenceValue as Transform; if (t) list.Add(t); }
            return list.ToArray();
        }

        /// NavMesh read-back (gamedev:unity-navmesh): every point within 0.75 m of the NavMesh (SafeSpawn) and connected to the first one.
        static void CheckNavPoints(string id, Transform[] points, int mask, List<string> warnings, string label)
        {
            if (points.Length == 0) return;
            var tri = NavMesh.CalculateTriangulation();
            if (tri.indices == null || tri.indices.Length == 0) { warnings.Add(id + ": NavMesh no cargado en el Editor; no se comprobaron " + label + "s"); return; }
            var path = new NavMeshPath(); Vector3? reference = null;
            foreach (var p in points)
            {
                if (!NavMesh.SamplePosition(p.position, out var hit, .75f, mask)) { warnings.Add(id + ": " + label + " " + p.name + " a > 0,75 m del NavMesh (SafeSpawn lo rechazaría)"); continue; }
                if (reference == null) { reference = hit.position; continue; }
                if (!NavMesh.CalculatePath(hit.position, reference.Value, mask, path) || path.status != NavMeshPathStatus.PathComplete)
                    warnings.Add(id + ": " + label + " " + p.name + " sin camino completo hasta " + points[0].name + " (isla: «OLEADA EN ESPERA» si el jugador está al otro lado)");
            }
        }
    }
}
