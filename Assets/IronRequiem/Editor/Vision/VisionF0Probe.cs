using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools.Vision
{
    /// Chat 11 · F0 (READ ONLY). Measures how readable each enemy is at its spawn points in Map01 and in the current
    /// campaign rooms. In Play Mode, inside an unsaved empty scene, every room scene is loaded additively (its own
    /// lights and RenderSettings), the enemy prefab of each spawn is placed frozen (timeScale 0) and rendered from 5, 15
    /// and 25 m at eye height with the campaign post profile. An enemy-only mask gives enemy vs background values.
    /// Nothing is saved: the previous scene is reopened afterwards. Output: Tools/IronRequiem/Reports/Vision/F0/.
    /// Also counts lights per room (type, shadows, realtime point/spot) for the flashlight budget.
    [InitializeOnLoad]
    public static class VisionF0Probe
    {
        const string Menu = "Tools/Iron Requiem/Vision/";
        const string Out = "Tools/IronRequiem/Reports/Vision/F0";
        const string Key = "IR.VisionF0.";
        const int W = 960, H = 540, MaskLayer = 31, MaxSpawnsPerRoom = 10;
        static readonly float[] Distances = { 5, 15, 25 };
        static readonly string[] CampaignRooms =
        {
            "Assets/IronRequiem/Scenes/Campaign/Room00b_AlarmCorridor.unity", "Assets/IronRequiem/Scenes/Campaign/Room01_Foundry.unity",
            "Assets/IronRequiem/Scenes/Campaign/Room02_Breach.unity", "Assets/IronRequiem/Scenes/Campaign/Room03_Assembly.unity",
            "Assets/IronRequiem/Scenes/Campaign/Room04_Reactor.unity", "Assets/IronRequiem/Scenes/Campaign/Pursuit05.unity"
        };
        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static VisionF0Probe() { EditorApplication.playModeStateChanged += OnPlayMode; }

        // ------------------------------------------------------------------ F1 prototypes (same pipeline, nothing saved)
        // Worst F0 spots (room scene, spawn transform). Variants are rendered on the same frame for comparison.
        static readonly (string room, string spawn)[] F1Spots =
        {
            ("Map01_R10_TunelTrituradora", "P1_T07"), ("Map01_R07_Comunicaciones", "G3_2"), ("Map01_R11_Decantadores", "G1_2"),
            ("Map01_R02_PozoMontacargas", "G1_1"), ("Map01_R14_Fundicion", "G1_1"), ("Room00b_AlarmCorridor", "DuctSpawn2")
        };
        static readonly string[] F1Variants = { "base", "A_linterna", "B_pulso", "C_pasivo", "AC_linterna_pasivo" };
        // cold white-blue: not the red / amber / green reserved for doors, and not the cyan of the patrol eye
        static readonly Color VisionTint = new Color(.72f, .86f, 1f);
        static bool f1Mode, f3Mode; static StringBuilder f1Csv; static float userIntensity = 1;
        static string PhaseDir => f3Mode ? "F3" : "F1";
        static string[] Variants => f3Mode ? new[] { "antes", "despues" } : F1Variants;

        [MenuItem(Menu + "F1 Prototype captures A-B-C (read only)")]
        public static void StartF1() { SessionState.SetBool(Key + "f3", false); SessionState.SetBool(Key + "f1", true); Start(); }

        [MenuItem(Menu + "F3 Before-after captures with the real system (read only)")]
        public static void StartF3() { SessionState.SetBool(Key + "f1", false); SessionState.SetBool(Key + "f3", true); Start(); }

        [MenuItem(Menu + "F0 Measure enemy legibility (read only)")]
        public static void StartF0() { SessionState.SetBool(Key + "f1", false); SessionState.SetBool(Key + "f3", false); Start(); }

        static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene: " + SceneManager.GetSceneAt(i).path);
            SessionState.SetString(Key + "previous", SceneManager.GetActiveScene().path);
            SessionState.SetString(Key + "startScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key + "run", true);
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // unsaved, discarded afterwards
            Directory.CreateDirectory(Out + "/captures");
            EditorApplication.isPlaying = true;
        }

        /// Validation: in a real game session, the player camera vs the probe camera from the same pose (mean values).
        [MenuItem(Menu + "F0 Validate probe vs player camera (play)")]
        public static void Validate()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
            var player = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>(); var cam = player.PlayerCamera;
            Directory.CreateDirectory(Out);
            var a = Grab(cam);
            var go = new GameObject("VisionF0_Camera") { hideFlags = HideFlags.DontSave }; var probe = go.AddComponent<Camera>(); var data = go.AddComponent<UniversalAdditionalCameraData>();
            probe.enabled = false; probe.fieldOfView = cam.fieldOfView; probe.nearClipPlane = .05f; probe.farClipPlane = 400; probe.allowHDR = true;
            data.renderPostProcessing = true; data.volumeLayerMask = ~0; data.volumeTrigger = go.transform; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            probe.cullingMask = ~(1 << MaskLayer); go.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
            var b = Grab(probe); Object.DestroyImmediate(go);
            float ma = a.Average(c => Value(c)), mb = b.Average(c => Value(c));
            var camData = cam.GetUniversalAdditionalCameraData();
            string line = $"{DateTime.Now:s} scene={SceneManager.GetActiveScene().name} pos={cam.transform.position} playerCam mean={F(ma)} probe mean={F(mb)} | player cam: hdr={cam.allowHDR} post={camData.renderPostProcessing} mask={cam.cullingMask} volumeMask={(int)camData.volumeLayerMask} fov={F(cam.fieldOfView)} ambient={RenderSettings.ambientMode}/{F(RenderSettings.ambientSkyColor.grayscale)} fog={F(RenderSettings.fogDensity)}";
            File.AppendAllText(Out + "/validate.txt", line + "\n"); Debug.Log("[VisionF0] " + line);
            foreach (var (px, n) in new[] { (a, "player"), (b, "probe") }) { var t = new Texture2D(W, H, TextureFormat.RGBA32, false); t.SetPixels32(px); t.Apply(); File.WriteAllBytes($"{Out}/validate_{n}.jpg", t.EncodeToJPG(85)); Object.DestroyImmediate(t); }
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + "run", false)) { SessionState.EraseBool(Key + "run"); SessionState.SetBool(Key + "restore", true); Begin(); }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "restore", false))
            {
                SessionState.EraseBool(Key + "restore");
                string start = SessionState.GetString(Key + "startScene", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(start) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(start);
                string prev = SessionState.GetString(Key + "previous", "");
                if (!string.IsNullOrEmpty(prev) && File.Exists(prev)) EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
            }
        }

        // ------------------------------------------------------------------ state machine (EditorApplication.update)
        static List<string> rooms; static int roomIndex; static int waitUntilFrame; static AsyncOperation loading; static Scene loaded;
        static StringBuilder csv, lightsCsv, log; static Volume post; static int errors;
        enum Phase { LoadRoom, Settle, Measure, Unload, Done }
        static Phase phase;

        static void Begin()
        {
            rooms = Directory.GetFiles("Assets/IronRequiem/Scenes/Map01_Bunker", "Map01_R*.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            rooms.AddRange(CampaignRooms.Where(File.Exists));
            f3Mode = SessionState.GetBool(Key + "f3", false); f1Mode = f3Mode || SessionState.GetBool(Key + "f1", false); userIntensity = RequiemSettings.VisionIntensity;
            if (f1Mode)
            {
                rooms = rooms.Where(p => F1Spots.Any(s => Path.GetFileNameWithoutExtension(p) == s.room)).ToList();
                f1Csv = new StringBuilder("room,spawn,enemy,variant,dist_req,dist_real,enemy_px,enemy_val,bg_val,ratio,eye_px,verdict,room_lights_with_variant,capture\n");
                Directory.CreateDirectory(Out + "/../" + PhaseDir + "/captures");
            }
            csv = new StringBuilder("room,spawn,enemy,dist_req,dist_real,enemy_px,enemy_val,bg_val,ratio,eye_peak,eye_px,verdict,capture,illuminance\n");
            lightsCsv = new StringBuilder("room,lights_enabled,directional,point,spot,area_or_baked,shadowed_point_spot,realtime_point_spot,ambient_mode,ambient_sky,fog\n");
            log = new StringBuilder(DateTime.Now.ToString("s") + " VisionF0 rooms=" + rooms.Count + "\n");
            errors = 0; Application.logMessageReceived += CountErrors;
            Time.timeScale = 0;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/IronRequiem/Art/VisualPass/Campaign_PostProfile.asset");
            var go = new GameObject("VisionF0_Post"); post = go.AddComponent<Volume>(); post.isGlobal = true; post.priority = 0; post.sharedProfile = profile;
            roomIndex = 0; phase = Phase.LoadRoom;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        static void CountErrors(string m, string s, LogType t) { if (t == LogType.Exception || t == LogType.Error) { errors++; if (errors < 60) log.AppendLine("  log " + t + ": " + m.Split('\n')[0]); } }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (Time.frameCount < waitUntilFrame) return;
            try
            {
                switch (phase)
                {
                    case Phase.LoadRoom:
                        if (roomIndex >= rooms.Count) { phase = Phase.Done; break; }
                        loading = EditorSceneManager.LoadSceneAsyncInPlayMode(rooms[roomIndex], new LoadSceneParameters(LoadSceneMode.Additive));
                        phase = Phase.Settle; break;
                    case Phase.Settle:
                        if (loading != null && !loading.isDone) return;
                        loaded = SceneManager.GetSceneByPath(rooms[roomIndex]);
                        if (loaded.IsValid()) SceneManager.SetActiveScene(loaded);
                        waitUntilFrame = Time.frameCount + 4; phase = Phase.Measure; break;
                    case Phase.Measure:
                        MeasureRoom(rooms[roomIndex], loaded);
                        loading = SceneManager.UnloadSceneAsync(loaded); phase = Phase.Unload; break;
                    case Phase.Unload:
                        if (loading != null && !loading.isDone) return;
                        roomIndex++; waitUntilFrame = Time.frameCount + 2; phase = Phase.LoadRoom; break;
                    case Phase.Done:
                        EditorApplication.update -= Tick; Application.logMessageReceived -= CountErrors; Time.timeScale = 1;
                        if (f3Mode) RequiemSettings.SetVisionIntensity(userIntensity);
                        if (f1Mode) File.WriteAllText(Out + "/../" + PhaseDir + (f3Mode ? "/before-after.csv" : "/prototypes.csv"), f1Csv.ToString());
                        else { File.WriteAllText(Out + "/legibility.csv", csv.ToString()); File.WriteAllText(Out + "/lights.csv", lightsCsv.ToString()); }
                        log.AppendLine("errors logged during run (room gameplay without a player): " + errors);
                        File.WriteAllText(Out + "/log.txt", log.ToString());
                        Debug.Log("[VisionF0] done: " + Out);
                        EditorApplication.isPlaying = false; break;
                }
            }
            catch (Exception e)
            {
                log.AppendLine("EXCEPTION " + rooms.ElementAtOrDefault(roomIndex) + ": " + e);
                File.WriteAllText(Out + "/log.txt", log.ToString());
                if (phase == Phase.Measure && loaded.IsValid()) { loading = SceneManager.UnloadSceneAsync(loaded); phase = Phase.Unload; }
                else { roomIndex++; phase = Phase.LoadRoom; }
            }
        }

        // ------------------------------------------------------------------ per room
        static void MeasureRoom(string path, Scene scene)
        {
            string room = Path.GetFileNameWithoutExtension(path);
            if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.Rescan(); // light list of this room for the illuminance column
            var roots = scene.GetRootGameObjects();
            // lights
            var lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(false)).Where(l => l.enabled).ToArray();
            int dir = lights.Count(l => l.type == LightType.Directional), point = lights.Count(l => l.type == LightType.Point), spot = lights.Count(l => l.type == LightType.Spot);
            int other = lights.Length - dir - point - spot;
            int shadowed = lights.Count(l => (l.type == LightType.Point || l.type == LightType.Spot) && l.shadows != LightShadows.None);
            int realtime = lights.Count(l => (l.type == LightType.Point || l.type == LightType.Spot) && l.lightmapBakeType != LightmapBakeType.Baked);
            lightsCsv.AppendLine(string.Join(",", room, lights.Length, dir, point, spot, other, shadowed, realtime, RenderSettings.ambientMode,
                F(RenderSettings.ambientSkyColor.grayscale), RenderSettings.fog ? "exp2 " + F(RenderSettings.fogDensity) : "off"));

            // spawns and the enemies that use them
            var spawns = new List<(Transform t, GameObject prefab)>();
            foreach (var lab in roots.SelectMany(r => r.GetComponentsInChildren<CombatTwoRoomsLab>(true)))
            {
                var prefabs = new List<GameObject>(); var fixedPts = new List<(Transform, GameObject)>();
                if (typeof(CombatTwoRoomsLab).GetField("waves", Any)?.GetValue(lab) is Array waves)
                    foreach (var w in waves) { if (w == null) continue; CollectGroups(w.GetType().GetField("Groups")?.GetValue(w) as Array, prefabs, fixedPts);
                        if (w.GetType().GetField("Variations")?.GetValue(w) is Array vars) foreach (var v in vars) if (v != null) CollectGroups(v.GetType().GetField("Groups")?.GetValue(v) as Array, prefabs, fixedPts); }
                var big = prefabs.FirstOrDefault(p => { var a = p.GetComponentInChildren<NavMeshAgent>(true); return a && a.radius > 1.5f; });
                var small = prefabs.FirstOrDefault(p => p != big) ?? big;
                foreach (var name in new[] { "firstRoomSpawns", "secondRoomSpawns" })
                    if (typeof(CombatTwoRoomsLab).GetField(name, Any)?.GetValue(lab) is Transform[] pts)
                        foreach (var t in pts) if (t) spawns.Add((t, t.name.StartsWith("BS") && big ? big : small));
                spawns.AddRange(fixedPts);
            }
            foreach (var chase in roots.SelectMany(r => r.GetComponentsInChildren<ChaseCorridorEncounter>(true)))
                if (chase.Ambushes != null) foreach (var a in chase.Ambushes) if (a?.Points != null) for (int i = 0; i < a.Points.Length; i++)
                    if (a.Points[i] && a.Prefabs != null && a.Prefabs.Length > 0) spawns.Add((a.Points[i], a.Prefabs[Mathf.Min(i, a.Prefabs.Length - 1)]));
            spawns = spawns.Where(s => s.prefab).GroupBy(s => s.t).Select(g => g.First()).ToList();
            if (spawns.Count == 0) { log.AppendLine(room + ": no spawns found"); return; }
            // view towards the centre of the arena (centroid of the spawns; scene bounds if they coincide)
            var centroid = spawns.Aggregate(Vector3.zero, (acc, s) => acc + s.t.position) / spawns.Count;
            var picked = spawns.Count <= MaxSpawnsPerRoom ? spawns : Enumerable.Range(0, MaxSpawnsPerRoom).Select(i => spawns[i * spawns.Count / MaxSpawnsPerRoom]).ToList();
            if (f1Mode)
            {
                foreach (var spot2 in F1Spots.Where(s => s.room == room))
                {
                    var match = spawns.FirstOrDefault(s => Clean(s.t.name) == spot2.spawn);
                    if (match.t) PrototypeSpawn(room, match.t, match.prefab, centroid, point + spot);
                    else log.AppendLine(room + ": F1 spawn not found " + spot2.spawn);
                }
                return;
            }
            log.AppendLine($"{room}: lights={lights.Length} (P{point}/S{spot}/D{dir}) spawns={spawns.Count} measured={picked.Count}");
            foreach (var (t, prefab) in picked) MeasureSpawn(room, t, prefab, centroid);
        }
        static void CollectGroups(Array groups, List<GameObject> prefabs, List<(Transform, GameObject)> fixedPts)
        {
            if (groups == null) return;
            foreach (var g in groups.OfType<CombatTwoRoomsLab.Group>()) { if (!g.Prefab) continue; if (!prefabs.Contains(g.Prefab)) prefabs.Add(g.Prefab); if (g.FixedPoint) fixedPts.Add((g.FixedPoint, g.Prefab)); }
        }

        static void MeasureSpawn(string room, Transform spawn, GameObject prefab, Vector3 centroid)
        {
            var floor = spawn.position;
            if (Physics.Raycast(floor + Vector3.up * 1.5f, Vector3.down, out var down, 6f, ~0, QueryTriggerInteraction.Ignore)) floor = down.point;
            var dir = Vector3.ProjectOnPlane(centroid - floor, Vector3.up);
            if (dir.sqrMagnitude < 4) dir = Vector3.ProjectOnPlane(-spawn.forward, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; dir.Normalize();
            var enemy = Object.Instantiate(prefab, floor, Quaternion.LookRotation(dir, Vector3.up));
            enemy.name = "VisionF0_" + prefab.name;
            foreach (var agent in enemy.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            var bounds = new Bounds(floor + Vector3.up, Vector3.one); bool first = true;
            var renderers = enemy.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
            foreach (var r in renderers) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
            var aim = bounds.center;
            try
            {
                foreach (float d in Distances)
                {
                    var eye = floor + dir * d + Vector3.up * 1.65f; float real = d;
                    var toEye = eye - aim;
                    if (Physics.Raycast(aim, toEye.normalized, out var block, toEye.magnitude, ~0, QueryTriggerInteraction.Ignore) && !block.transform.IsChildOf(enemy.transform))
                    { real = Mathf.Max(1.5f, Vector3.Distance(Vector3.ProjectOnPlane(block.point - floor, Vector3.up), Vector3.zero) - .4f); eye = floor + dir * real + Vector3.up * 1.65f; }
                    string capture = $"{room}_{Clean(spawn.name)}_{d:0}m.jpg";
                    var stats = Render(eye, aim, renderers, Out + "/captures/" + capture);
                    string verdict = stats.px < 25 ? "NO_VISIBLE" : (stats.ratio >= 1.5f || stats.eyePx >= 6) ? "legible" : stats.ratio >= 1.25f ? "debil" : "ILEGIBLE";
                    csv.AppendLine(string.Join(",", room, Clean(spawn.name), prefab.name, F(d), F(real), stats.px, F(stats.enemy), F(stats.bg), F(stats.ratio), F(stats.eyePeak), stats.eyePx, verdict, capture, EnemyDarkReadability.Instance ? F(EnemyDarkReadability.Instance.Illuminance(aim, enemy.transform)) : "-"));
                }
            }
            finally { Object.DestroyImmediate(enemy); }
        }

        static void PrototypeSpawn(string room, Transform spawn, GameObject prefab, Vector3 centroid, int roomPointSpot)
        {
            var floor = spawn.position;
            if (Physics.Raycast(floor + Vector3.up * 1.5f, Vector3.down, out var down, 6f, ~0, QueryTriggerInteraction.Ignore)) floor = down.point;
            var dir = Vector3.ProjectOnPlane(centroid - floor, Vector3.up);
            if (dir.sqrMagnitude < 4) dir = Vector3.ProjectOnPlane(-spawn.forward, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; dir.Normalize();
            var enemy = Object.Instantiate(prefab, floor, Quaternion.LookRotation(dir, Vector3.up)); enemy.name = "VisionF1_" + prefab.name;
            // captured in the spawn frame (before Health.Start): mark it alive so the real system treats it as a live enemy
            var health = enemy.GetComponent<Unity.FPS.Game.Health>(); if (f3Mode && health && health.CurrentHealth <= 0) health.CurrentHealth = health.MaxHealth;
            foreach (var agent in enemy.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            var renderers = enemy.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(floor + Vector3.up, Vector3.one); foreach (var r in renderers) b.Encapsulate(r.bounds);
            var aim = b.center; var originals = renderers.Select(r => r.sharedMaterials).ToArray();
            log.AppendLine($"F1 {room} {spawn.name} {prefab.name}: " + string.Join("; ", renderers.Take(6).Select(r => r.name + "=" + string.Join("+", r.sharedMaterials.Where(m => m).Select(m => m.name + "(" + m.shader.name + ")")))));
            try
            {
                foreach (float d in Distances)
                {
                    var eye = floor + dir * d + Vector3.up * 1.65f; float real = d; var toEye = eye - aim;
                    if (Physics.Raycast(aim, toEye.normalized, out var block, toEye.magnitude, ~0, QueryTriggerInteraction.Ignore) && !block.transform.IsChildOf(enemy.transform))
                    { real = Mathf.Max(1.5f, Vector3.ProjectOnPlane(block.point - floor, Vector3.up).magnitude - .4f); eye = floor + dir * real + Vector3.up * 1.65f; }
                    float bgBase = 0;
                    foreach (var variant in Variants)
                    {
                        Light flash = null; var clones = new List<Material>();
                        try
                        {
                            if (variant.Contains("linterna"))
                            {
                                // A: shoulder lamp, aligned with the view, no shadows (budget), cold white
                                var view = Quaternion.LookRotation(aim - eye, Vector3.up);
                                var lgo = new GameObject("VisionF1_Flashlight") { hideFlags = HideFlags.DontSave };
                                lgo.transform.SetPositionAndRotation(eye + view * new Vector3(.12f, -.08f, .1f), view);
                                flash = lgo.AddComponent<Light>(); flash.type = LightType.Spot; flash.color = VisionTint; flash.intensity = FlashIntensity;
                                flash.range = 30; flash.spotAngle = 55; flash.innerSpotAngle = 32; flash.shadows = LightShadows.None;
                            }
                            if (variant.StartsWith("B") || variant.Contains("pasivo"))
                            {
                                float k = Mathf.Clamp01((45 - bgBase) / 35f);
                                Color rim = variant.StartsWith("B") ? VisionTint * 3.2f : VisionTint * Mathf.Lerp(.6f, 2f, k);
                                float power = variant.StartsWith("B") ? 2f : 2.6f;
                                for (int i = 0; i < renderers.Length; i++)
                                {
                                    var mats = originals[i].Select(m => { var v = VisionMaterial(m, rim, power); if (v != m) clones.Add(v); return v; }).ToArray();
                                    renderers[i].sharedMaterials = mats;
                                }
                            }
                            string capture = $"{room}_{Clean(spawn.name)}_{variant}_{d:0}m.jpg";
                            if (f3Mode) { RequiemSettings.SetVisionIntensity(variant == "despues" ? Mathf.Max(userIntensity, 1) : 0); if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.ApplyNow(); }
                            var s = Render(eye, aim, renderers, Out + "/../" + PhaseDir + "/captures/" + capture);
                            if (variant == "base") bgBase = s.bg;
                            string verdict = s.px < 25 ? "NO_VISIBLE" : (s.ratio >= 1.5f || s.eyePx >= 6) ? "legible" : s.ratio >= 1.25f ? "debil" : "ILEGIBLE";
                            f1Csv.AppendLine(string.Join(",", room, Clean(spawn.name), prefab.name, variant, F(d), F(real), s.px, F(s.enemy), F(s.bg), F(s.ratio), s.eyePx, verdict, roomPointSpot + (flash ? 1 : 0), capture));
                        }
                        finally
                        {
                            for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].sharedMaterials = originals[i];
                            foreach (var m in clones) Object.DestroyImmediate(m);
                            if (flash) Object.DestroyImmediate(flash.gameObject);
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(enemy); }
        }
        const float FlashIntensity = 35f;
        static Shader hardSurface;
        /// Runtime-only copy with the fresnel rim on. Eyes / indicators (emissive materials) are left untouched.
        static Material VisionMaterial(Material src, Color rim, float power)
        {
            if (!src) return src;
            if (src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor") && src.GetColor("_EmissionColor").maxColorComponent > .4f) return src;
            if (src.name.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0 || src.name.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0) return src;
            if (!hardSurface) hardSurface = Shader.Find("IronRequiem/HardSurface");
            Material m;
            if (src.shader == hardSurface) m = new Material(src);
            else
            {
                m = new Material(hardSurface);
                if (src.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", src.GetTexture("_BaseMap")); m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap")); m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap")); }
                if (src.HasProperty("_BaseColor")) m.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap")) { m.SetTexture("_BumpMap", src.GetTexture("_BumpMap")); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_UseNormalMap", 1); }
                if (src.HasProperty("_Metallic")) m.SetFloat("_Metallic", src.GetFloat("_Metallic"));
                if (src.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
            }
            m.hideFlags = HideFlags.DontSave; m.name = src.name + "_VisionRim";
            m.EnableKeyword("_RIM"); m.SetFloat("_UseRim", 1); m.SetColor("_RimColor", rim); m.SetFloat("_RimPower", power);
            return m;
        }

        struct Stats { public int px, eyePx; public float enemy, bg, ratio, eyePeak; }
        /// Game render (base camera, post on) + enemy mask (enemy renderers alone on MaskLayer, over black and white).
        static Stats Render(Vector3 eye, Vector3 aim, Renderer[] enemy, string file)
        {
            var go = new GameObject("VisionF0_Camera") { hideFlags = HideFlags.DontSave }; var cam = go.AddComponent<Camera>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            cam.enabled = false; cam.fieldOfView = 60; cam.nearClipPlane = .05f; cam.farClipPlane = 400; cam.allowHDR = true;
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye, Vector3.up));
            var layers = enemy.Select(r => r.gameObject.layer).ToArray();
            try
            {
                data.renderPostProcessing = true; data.volumeLayerMask = ~0; data.volumeTrigger = go.transform; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                cam.cullingMask = ~(1 << MaskLayer); cam.clearFlags = CameraClearFlags.Skybox;
                var game = Grab(cam);
                data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
                for (int i = 0; i < enemy.Length; i++) enemy[i].gameObject.layer = MaskLayer;
                cam.cullingMask = 1 << MaskLayer; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black; var black = Grab(cam);
                cam.backgroundColor = Color.white; var white = Grab(cam);
                var mask = new bool[game.Length]; int minX = W, minY = H, maxX = -1, maxY = -1, px = 0;
                for (int i = 0; i < mask.Length; i++)
                {
                    mask[i] = (white[i].r - black[i].r + white[i].g - black[i].g + white[i].b - black[i].b) / 3f < .5f;
                    if (!mask[i]) continue; px++; int x = i % W, y = i / W; if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
                var s = new Stats { px = px };
                if (px > 0)
                {
                    // background ring: the enemy's bounding box grown by 60 %, minus the enemy itself
                    int gx = (maxX - minX) * 3 / 10 + 4, gy = (maxY - minY) * 3 / 10 + 4;
                    int x0 = Mathf.Max(0, minX - gx), x1 = Mathf.Min(W - 1, maxX + gx), y0 = Mathf.Max(0, minY - gy), y1 = Mathf.Min(H - 1, maxY + gy);
                    double se = 0, sb = 0; int nb = 0; var values = new List<float>(px);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        int i = y * W + x; float v = Value(game[i]);
                        if (mask[i]) { se += v; values.Add(v); } else { sb += v; nb++; }
                    }
                    s.enemy = (float)(se / Mathf.Max(1, values.Count)); s.bg = nb > 0 ? (float)(sb / nb) : 0;
                    s.ratio = (Mathf.Max(s.enemy, s.bg) + 5f) / (Mathf.Min(s.enemy, s.bg) + 5f);
                    values.Sort(); s.eyePeak = values[values.Count - 1];
                    float eyeThreshold = Mathf.Max(170, s.bg * 2.2f); s.eyePx = values.Count(v => v >= eyeThreshold);
                    // outline the measured enemy in the saved capture (F0 only; F1 captures stay clean for visual review)
                    if (!f1Mode) for (int i = 0; i < game.Length; i++) if (mask[i] && (i % W == 0 || !mask[i - 1] || i % W == W - 1 || !mask[i + 1])) game[i] = new Color32(255, 0, 255, 255);
                }
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.SetPixels32(game); tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToJPG(80)); Object.DestroyImmediate(tex);
                return s;
            }
            finally { for (int i = 0; i < enemy.Length; i++) if (enemy[i]) enemy[i].gameObject.layer = layers[i]; Object.DestroyImmediate(go); }
        }
        static Color32[] Grab(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32); var tex = new Texture2D(W, H, TextureFormat.RGBA32, false); var prev = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); return tex.GetPixels32(); }
            finally { cam.targetTexture = null; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
        /// Display value 0-255 (same scale as the gray-card values of 05_ILUMINACION §L1.3), from Rec.709 luma.
        static float Value(Color32 c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Clean(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
    }
}
