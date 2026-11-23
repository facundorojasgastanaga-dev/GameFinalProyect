using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    /// VISUAL PASS (chat 12) F0, read only. Rooms are loaded additively by path in the Test Runner scene with time
    /// frozen; nothing is saved. Same display-value scale (0-255, Rec.709 luma) and enemy/background metric as the
    /// chat 11 probe (Editor/Vision/VisionF0Probe.cs) so the numbers compare.
    ///   A_ZoneCaptures      two fixed viewpoints per room, empty and with a T-07 at 5/15/25 m, 1920x1080
    ///   B_RobotLightCatalog every enemy prefab, every light state, front/back/far, plus a runtime dump of its lights
    /// Output: Tools/IronRequiem/Reports/VisualPassFable/F0/
    public sealed class VisualPassF0
    {
        // Output folder: F0 by default; a one-line label in out-label.txt redirects a later pass (e.g. "F2_despues").
        const string OutRoot = "Tools/IronRequiem/Reports/VisualPassFable/";
        static string Out => OutRoot + (File.Exists(OutRoot + "out-label.txt") ? File.ReadAllText(OutRoot + "out-label.txt").Trim() : "F0") + "/";
        const string Map01 = "Assets/IronRequiem/Scenes/Map01_Bunker", Legacy = "Assets/IronRequiem/Scenes/Campaign/";
        const string Prefabs = "Assets/IronRequiem/Prefabs/Campaign/";
        const int W = 1920, H = 1080, MaskLayer = 31;
        static readonly float[] Distances = { 5, 15, 25 };
        static readonly string[] LegacyRooms = { "Room00_Hangar", "Room00b_AlarmCorridor", "Room01_Foundry", "Room02_Breach", "Room03_Assembly", "Room04_Reactor", "Pursuit05" };
        static readonly string[] Enemies = { "T07_Current_Repaired", "T07_Mech", "SawBot_Current", "BigBot_Current", "HoverBot_Current", "Turret_Current", "Carronero" };
        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        StringBuilder log; bool background; float scale; Camera cam; UniversalAdditionalCameraData data;

        const string Session = "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity";
        /// The real Map01 session (player, managers, HUD and the campaign post volume) so enemies behave and look as in
        /// the game; its streaming is stopped and the hangar unloaded, then rooms are loaded additively by path.
        [UnitySetUp] public IEnumerator Setup()
        {
            LogAssert.ignoreFailingMessages = true;
            background = Application.runInBackground; Application.runInBackground = true; scale = 1; Time.timeScale = 1;
            Directory.CreateDirectory(Out);
            EditorSceneManager.LoadSceneInPlayMode(Session, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<CampaignFlow>(); Assert.That(flow, Is.Not.Null);
            float end = Time.realtimeSinceStartup + 90; while ((flow.Busy || flow.CurrentStage != 0) && Time.realtimeSinceStartup < end) yield return null;
            flow.Menu.BeginGame(); flow.Player.GetComponent<Health>().Invincible = true; yield return null;
            var intro = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro) { end = Time.realtimeSinceStartup + 10; while (!intro.IsPlaying && !intro.Finished && Time.realtimeSinceStartup < end) yield return null; intro.RequestSkip(); }
            end = Time.realtimeSinceStartup + 15; while (PlayerInputLock.Locked && Time.realtimeSinceStartup < end) yield return null;
            yield return flow.DrainPendingLoadsForShutdown(); flow.enabled = false;
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (m && m.GetType().Name == "GameFlowManager") m.enabled = false; // a kill must not end the session
            Time.timeScale = 0; // freeze AI and the player from here on
            sessionScene = flow.gameObject.scene;
            foreach (var s in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != sessionScene && s.isLoaded && s.name != "DontDestroyOnLoad" && !s.name.StartsWith("InitTestScene")).ToArray())
                yield return SceneManager.UnloadSceneAsync(s);
            sessionVolumes = string.Join(" + ", Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Where(v => v.enabled && v.isGlobal).Select(v => (v.sharedProfile ? v.sharedProfile.name : "null") + "@p" + v.priority + "w" + F(v.weight)));
            var pc = flow.Player.PlayerCamera;
            var cg = new GameObject("VisualPassF0_Camera"); cam = cg.AddComponent<Camera>(); data = cg.AddComponent<UniversalAdditionalCameraData>();
            cam.enabled = false; cam.fieldOfView = pc ? pc.fieldOfView : 60; cam.nearClipPlane = .05f; cam.farClipPlane = 400; cam.allowHDR = true;
            flow.Player.gameObject.SetActive(true);
            yield return null;
        }
        Scene sessionScene; string sessionVolumes = "";
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Application.runInBackground = background;
            var clean = SceneManager.CreateScene("VisualPassF0Cleanup" + Time.frameCount); SceneManager.SetActiveScene(clean);
            var session = Object.FindAnyObjectByType<CampaignFlow>(); if (session) { session.enabled = true; yield return session.DrainPendingLoadsForShutdown(); }
            foreach (var queue in Object.FindObjectsByType<EnemySpawnQueue>()) { queue.CancelPending(); queue.enabled = false; }
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != clean && s.isLoaded).OrderBy(s => s == sessionScene ? 1 : 0).ToArray();
            foreach (var s in loaded) if (s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
            Cursor.lockState = CursorLockMode.None; LogAssert.ignoreFailingMessages = false;
        }

        static List<string> Rooms()
        {
            var rooms = Directory.GetFiles(Map01, "Map01_R*.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            rooms.AddRange(LegacyRooms.Select(r => Legacy + r + ".unity").Where(File.Exists));
            return rooms;
        }

        IEnumerator Load(string path, Action<Scene> then)
        {
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            float end = Time.realtimeSinceStartup + 60; while (op != null && !op.isDone && Time.realtimeSinceStartup < end) yield return null;
            var scene = SceneManager.GetSceneByPath(path); Assert.That(scene.IsValid() && scene.isLoaded, Is.True, "load " + path);
            SceneManager.SetActiveScene(scene);
            for (int i = 0; i < 5; i++) yield return null;
            // a fading volume would stay at 0 with time frozen: use its authored weight
            foreach (var fade in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VolumeFadeIn>(true))) { var v = fade.GetComponent<Volume>(); if (v) v.weight = fade.Claim(); }
            Physics.SyncTransforms();
            then(scene);
        }

        // ------------------------------------------------------------------ A: zone captures
        // One test case per room so the queue watchdog sees progress. Delete zones.csv / rooms.csv before a full run.
        static IEnumerable<string> RoomNames() => Rooms().Select(Path.GetFileNameWithoutExtension);
        [UnityTest, Timeout(600000)] public IEnumerator A_Zone([ValueSource(nameof(RoomNames))] string onlyRoom)
        {
            LogAssert.ignoreFailingMessages = true; Directory.CreateDirectory(Out + "zones");
            log = new StringBuilder();
            var csv = new StringBuilder(File.Exists(Out + "zones.csv") ? "" : "room,vp,shot,dist_req,dist_real,frame_mean,frame_dark_pct,frame_bright_pct,enemy_px,enemy_val,bg_val,ratio,eye_px,verdict,capture,red_eye_px,verdict_con_ojo_rojo\n");
            var roomsCsv = new StringBuilder(File.Exists(Out + "rooms.csv") ? "" : "room,lights,point,spot,directional,shadowed,ambient_mode,ambient_sky,ambient_equator,ambient_ground,ambient_intensity,fog,fog_color,volumes,draw_calls_vp1,setpass_vp1,tris_vp1,draw_calls_vp2,setpass_vp2,tris_vp2,vp1,vp2\n");
            var t07 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "T07_Current_Repaired.prefab"); Assert.That(t07, Is.Not.Null);
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            foreach (var path in Rooms().Where(r => Path.GetFileNameWithoutExtension(r) == onlyRoom))
            {
                string room = Path.GetFileNameWithoutExtension(path); Scene scene = default;
                yield return Load(path, s => scene = s);
                var roots = scene.GetRootGameObjects();
                var lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(false)).Where(l => l.enabled).ToArray();
                var volumes = roots.SelectMany(r => r.GetComponentsInChildren<Volume>(false)).Where(v => v.enabled).ToArray();
                string vols = string.Join(" + ", volumes.Select(v => (v.sharedProfile ? v.sharedProfile.name : "null") + "@p" + v.priority + "w" + F(v.weight) + (v.isGlobal ? "" : "(local)")));
                var views = Viewpoints(scene);
                var perf = new List<string>();
                for (int v = 0; v < views.Count; v++)
                {
                    var (eye, target) = views[v]; string tag = room + "_vp" + (v + 1);
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
                    // one clean frame for the render counters (previous-frame values)
                    yield return null; Game(); yield return null;
                    perf.Add(draws.LastValue + "," + setpass.LastValue + "," + tris.LastValue);
                    var s0 = Shoot(null, Out + "zones/" + tag + "_vacio.jpg");
                    csv.AppendLine(string.Join(",", room, v + 1, "vacio", "", "", F(s0.mean), F(s0.dark), F(s0.bright), "", "", "", "", "", "", tag + "_vacio.jpg", "", ""));
                    var flatDir = Vector3.ProjectOnPlane(target - eye, Vector3.up).normalized; var floorEye = eye - Vector3.up * 1.65f;
                    foreach (float d in Distances)
                    {
                        var p = floorEye + flatDir * d; float real = d; string note = "";
                        if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position; else note = "sin_navmesh";
                        var chest = p + Vector3.up * 1.2f;
                        if (Physics.Linecast(eye, chest, out var block, ~0, QueryTriggerInteraction.Ignore)) note = "tapado_por_" + block.collider.name;
                        real = Vector3.Distance(floorEye, p);
                        var enemy = Object.Instantiate(t07, p, Quaternion.LookRotation(-flatDir, Vector3.up)); enemy.name = "VP_T07";
                        if (Prepare(enemy, p)) note = (note == "" ? "" : note + "+") + "se_autodestruye_y" + F(p.y);
                        yield return null; yield return null; // let MaterialPropertyBlocks settle
                        if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.ApplyNow(); if (EnemyEyeGlow.Instance) EnemyEyeGlow.Instance.ApplyNow(); yield return null; yield return null;
                        var rs = enemy.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
                        string file = tag + "_T07_" + d.ToString("0") + "m.jpg";
                        var s = Shoot(rs, Out + "zones/" + file);
                        string verdict = note.StartsWith("tapado") && s.px < 25 ? "NO_VISIBLE" : s.px < 25 ? "NO_VISIBLE" : (s.ratio >= 1.5f || s.eyePx >= 6) ? "legible" : s.ratio >= 1.25f ? "debil" : "ILEGIBLE";
                        csv.AppendLine(string.Join(",", room, v + 1, "T07" + (note == "" ? "" : "(" + note + ")"), F(d), F(real), F(s.mean), F(s.dark), F(s.bright), s.px, F(s.enemy), F(s.bg), F(s.ratio), s.eyePx, verdict, file, s.redPx, verdict == "legible" || s.redPx >= 6 ? "legible" : verdict));
                        Object.DestroyImmediate(enemy);
                    }
                }
                while (perf.Count < 2) perf.Add(",,");
                roomsCsv.AppendLine(string.Join(",", room, lights.Length, lights.Count(l => l.type == LightType.Point), lights.Count(l => l.type == LightType.Spot), lights.Count(l => l.type == LightType.Directional),
                    lights.Count(l => l.shadows != LightShadows.None), RenderSettings.ambientMode, Hex(RenderSettings.ambientSkyColor), Hex(RenderSettings.ambientEquatorColor), Hex(RenderSettings.ambientGroundColor), F(RenderSettings.ambientIntensity),
                    RenderSettings.fog ? RenderSettings.fogMode + " " + F(RenderSettings.fogDensity) : "off", Hex(RenderSettings.fogColor), "\"" + sessionVolumes + " | " + vols + "\"", perf[0], perf[1],
                    views.Count > 0 ? "\"" + P(views[0].eye) + " -> " + P(views[0].target) + "\"" : "", views.Count > 1 ? "\"" + P(views[1].eye) + " -> " + P(views[1].target) + "\"" : ""));
                log.AppendLine(room + ": lights=" + lights.Length + " views=" + views.Count + " roomVolumes=" + vols + " sessionVolumes=" + sessionVolumes);
                File.AppendAllText(Out + "zones.csv", csv.ToString()); File.AppendAllText(Out + "rooms.csv", roomsCsv.ToString()); File.AppendAllText(Out + "zones-log.txt", log.ToString());
                yield return SceneManager.UnloadSceneAsync(scene); yield return null;
            }
        }

        /// Runtime-only preparation of a spawned enemy for a capture. Returns true when the game would destroy it at
        /// once: EnemyController.EnsureIsWithinLevelBounds removes any enemy below SelfDestructYHeight (-20 by default).
        static bool Prepare(GameObject enemy, Vector3 p)
        {
            foreach (var agent in enemy.GetComponentsInChildren<NavMeshAgent>(true)) if (!agent.isOnNavMesh) agent.enabled = false;
            bool doomed = false;
            foreach (var ec in enemy.GetComponentsInChildren<EnemyController>(true)) { if (p.y < ec.SelfDestructYHeight) doomed = true; ec.SelfDestructYHeight = -100000; }
            // the health bar faces Camera.main, not the capture camera: hide it
            foreach (var bar in enemy.GetComponentsInChildren<Unity.FPS.UI.WorldspaceHealthBar>(true)) { if (bar.HealthBarPivot) bar.HealthBarPivot.gameObject.SetActive(false); bar.enabled = false; }
            return doomed;
        }

        /// Two reproducible viewpoints per room: on the NavMesh, at about a third of the room from each end of its
        /// longer axis, eye height 1.65 m, looking at the centre of play (spawn centroid, or the middle of the room).
        List<(Vector3 eye, Vector3 target)> Viewpoints(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var cols = roots.SelectMany(r => r.GetComponentsInChildren<Collider>(false)).Where(c => c.enabled && !c.isTrigger).ToArray();
            var result = new List<(Vector3, Vector3)>(); if (cols.Length == 0) return result;
            var b = cols[0].bounds; foreach (var c in cols) b.Encapsulate(c.bounds);
            var spawns = new List<Vector3>();
            foreach (var lab in roots.SelectMany(r => r.GetComponentsInChildren<CombatTwoRoomsLab>(true)))
                foreach (var name in new[] { "firstRoomSpawns", "secondRoomSpawns" })
                    if (typeof(CombatTwoRoomsLab).GetField(name, Any)?.GetValue(lab) is Transform[] pts) spawns.AddRange(pts.Where(t => t).Select(t => t.position));
            var centre = spawns.Count > 0 ? spawns.Aggregate(Vector3.zero, (a, p) => a + p) / spawns.Count : b.center;
            if (spawns.Count == 0)
                foreach (var mb in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(false)).Where(m => m && m.GetType().Name == "NavMeshSurface"))
                    if (mb.GetType().GetProperty("navMeshData")?.GetValue(mb) is NavMeshData nav && nav) { centre = mb.transform.TransformPoint(nav.sourceBounds.center); b = new Bounds(centre, nav.sourceBounds.size); break; }
            if (NavMesh.SamplePosition(centre, out var ch, 12f, NavMesh.AllAreas)) centre = ch.position; else if (Physics.Raycast(centre + Vector3.up * 2, Vector3.down, out var fh, 60, ~0, QueryTriggerInteraction.Ignore)) centre = fh.point;
            var target = centre + Vector3.up * 1.2f;
            bool longX = b.size.x >= b.size.z; var along = longX ? Vector3.right : Vector3.forward; var across = longX ? Vector3.forward : Vector3.right;
            float la = (longX ? b.size.x : b.size.z) * .32f, sa = (longX ? b.size.z : b.size.x) * .32f;
            var candidates = new List<(Vector3 eye, float clear, int group)>();
            void Try(Vector3 offset, int group)
            {
                if (!NavMesh.SamplePosition(centre + offset, out var h, 8f, NavMesh.AllAreas)) return;
                var eye = h.position + Vector3.up * 1.65f; var to = target - eye; float dist = to.magnitude; if (dist < 6) return;
                float clear = Physics.Raycast(eye, to.normalized, out var hit, dist, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : dist;
                candidates.Add((eye, clear, group));
            }
            foreach (float k in new[] { 1f, .75f, .5f }) { Try(-along * la * k, 0); Try(along * la * k, 1); Try(-across * sa * k, 2); Try(across * sa * k, 3); }
            foreach (int g in new[] { 0, 1, 2, 3 })
            {
                var best = candidates.Where(c => c.group == g).OrderByDescending(c => c.clear).FirstOrDefault();
                if (best.clear >= 6 && result.Count < 2) result.Add((best.eye, target));
            }
            return result;
        }

        // ------------------------------------------------------------------ B: robot light catalog
        [UnityTest, Timeout(1200000)] public IEnumerator B_RobotLightCatalog()
        {
            LogAssert.ignoreFailingMessages = true; Directory.CreateDirectory(Out + "robots");
            log = new StringBuilder("VisualPass F0 robot light catalog " + DateTime.Now.ToString("s") + "\n");
            Scene scene = default; yield return Load(Map01 + "/Map01_R01_ControlAcceso.unity", s => scene = s);
            var views = Viewpoints(scene); Assert.That(views.Count, Is.GreaterThan(0));
            var (eye, target) = views[0]; var flat = Vector3.ProjectOnPlane(target - eye, Vector3.up).normalized; var floorEye = eye - Vector3.up * 1.65f;
            log.AppendLine("stage Map01_R01_ControlAcceso vp1 eye=" + P(eye) + " ambient=" + Hex(RenderSettings.ambientSkyColor) + " fog=" + F(RenderSettings.fogDensity));
            foreach (var name in Enemies)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab"); if (!prefab) { log.AppendLine(name + ": prefab missing"); continue; }
                foreach (float d in new[] { 5f, 15f })
                {
                    var p = floorEye + flat * d; if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
                    var enemy = Object.Instantiate(prefab, p, Quaternion.LookRotation(-flat, Vector3.up)); enemy.name = "CAT_" + name;
                    Prepare(enemy, p);
                    yield return null; yield return null;
                    if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.ApplyNow(); if (EnemyEyeGlow.Instance) EnemyEyeGlow.Instance.ApplyNow(); yield return null; yield return null;
                    var rs = enemy.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
                    var bounds = rs.Length > 0 ? rs[0].bounds : new Bounds(p + Vector3.up, Vector3.one); foreach (var r in rs) bounds.Encapsulate(r.bounds);
                    if (d == 5f) Dump(name, enemy, bounds);
                    if (d == 5f)
                    {
                        // where the eye manager put the eye, in the robot's own axes relative to the centre of its bounds
                        var ctrl = enemy.GetComponent<EnemyController>(); var tr = enemy.transform;
                        if (EnemyEyeGlow.Instance && ctrl && EnemyEyeGlow.Instance.TryGetEye(ctrl, out var snap))
                        {
                            var rel = snap.Position - bounds.center;
                            log.AppendLine("  OJO " + snap.Kind + " " + snap.State + " dibujado=" + snap.Drawn + " hdr=" + F(snap.Hdr) + " nucleo=" + F(snap.CoreSize) + " halo=" + F(snap.HaloSize)
                                + " | respecto al centro del cuerpo: derecha=" + F(Vector3.Dot(rel, tr.right)) + " arriba=" + F(rel.y) + " adelante=" + F(Vector3.Dot(rel, tr.forward))
                                + " | caja del cuerpo: alto=" + F(bounds.size.y) + " fondo(adelante)=" + F(Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(tr.forward.x), 0, Mathf.Abs(tr.forward.z))))) + " (semi)");
                            var head = enemy.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Head");
                            if (head) { var hr = head.position - bounds.center; log.AppendLine("  hueso Head: derecha=" + F(Vector3.Dot(hr, tr.right)) + " arriba=" + F(hr.y) + " adelante=" + F(Vector3.Dot(hr, tr.forward))); }
                        }
                        else log.AppendLine("  OJO: no gestionado por EnemyEyeGlow");
                    }
                    // close camera framed on the robot (front), then from behind; far shot from the room viewpoint
                    float size = Mathf.Max(bounds.size.y, bounds.size.x) * 1.15f, back = Mathf.Max(2.2f, size / (2 * Mathf.Tan(20 * Mathf.Deg2Rad)));
                    var esl = enemy.GetComponent<EnemyStateLights>();
                    var states = new List<(string tag, object state, Color? colour)> { ("spawn", null, null) };
                    var eyeCtrl = enemy.GetComponent<EnemyController>();
                    var eyeStates = new Dictionary<string, EnemyEyeState> { { "patrulla", EnemyEyeState.Patrol }, { "alerta", EnemyEyeState.Alert }, { "ataque", EnemyEyeState.Attack }, { "aviso", EnemyEyeState.Warning }, { "muerto", EnemyEyeState.Dead } };
                    if (!esl && EnemyEyeGlow.Instance) foreach (var k in eyeStates.Keys) states.Add((k, "eye", null));
                    if (esl)
                    {
                        Color Get(string f) => (Color)typeof(EnemyStateLights).GetField(f, Any).GetValue(esl);
                        var T = typeof(EnemyStateLights.LightState);
                        states.Add(("patrulla", Enum.Parse(T, "Patrol"), Get("patrol"))); states.Add(("alerta", Enum.Parse(T, "Alert"), Get("alert")));
                        states.Add(("ataque", Enum.Parse(T, "Attack"), Get("attack"))); states.Add(("aviso", Enum.Parse(T, "Warning"), Get("warning"))); states.Add(("muerto", Enum.Parse(T, "Dead"), Color.black));
                    }
                    foreach (var (tag, state, colour) in states)
                    {
                        // the red eyes are driven by EnemyEyeGlow: force its state too (EnemyStateLights is suspended on managed robots)
                        if (EnemyEyeGlow.Instance && eyeCtrl) { EnemyEyeGlow.Instance.ForceState(eyeCtrl, eyeStates.TryGetValue(tag, out var es) ? es : (EnemyEyeState?)null); EnemyEyeGlow.Instance.ApplyNow(); }
                        if (state is string) { yield return null; }
                        else if (state != null && !EnemyEyeGlow.Instance)
                        {
                            esl.enabled = false; // freeze the state machine and write the state colour the same way the component does
                            typeof(EnemyStateLights).GetField("applied", Any).SetValue(esl, new Color(-1, -1, -1, -1));
                            typeof(EnemyStateLights).GetMethod("Set", Any).Invoke(esl, new[] { state, (object)colour.Value });
                            yield return null;
                        }
                        if (d == 5f)
                        {
                            cam.fieldOfView = 40;
                            var front = bounds.center + (flat * -1 * back * .94f + Vector3.Cross(Vector3.up, flat) * back * .34f); // 3/4 front (the robot faces the viewpoint)
                            cam.transform.SetPositionAndRotation(front, Quaternion.LookRotation(bounds.center - front, Vector3.up));
                            var s = Shoot(rs, Out + "robots/" + name + "_" + tag + "_frente.jpg", 1280, 720);
                            log.AppendLine("  " + name + " " + tag + " frente: enemy=" + F(s.enemy) + " bg=" + F(s.bg) + " ratio=" + F(s.ratio) + " eyePeak=" + F(s.eyePeak) + " brightPx(>=235)=" + s.hotPx + " haloPx(outside mask, >=2x bg and >=120)=" + s.haloPx);
                            if (tag == "spawn" || tag == "ataque")
                            {
                                var behind = bounds.center + (flat * back * .94f + Vector3.Cross(Vector3.up, flat) * back * .34f);
                                cam.transform.SetPositionAndRotation(behind, Quaternion.LookRotation(bounds.center - behind, Vector3.up));
                                Shoot(rs, Out + "robots/" + name + "_" + tag + "_espalda.jpg", 1280, 720);
                            }
                            cam.fieldOfView = 60;
                        }
                        else if (tag == "spawn" || tag == "patrulla" || tag == "ataque")
                        {
                            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(bounds.center - eye, Vector3.up));
                            var s = Shoot(rs, Out + "robots/" + name + "_" + tag + "_15m.jpg", 1280, 720);
                            log.AppendLine("  " + name + " " + tag + " 15m: px=" + s.px + " enemy=" + F(s.enemy) + " bg=" + F(s.bg) + " ratio=" + F(s.ratio) + " eyePx=" + s.eyePx + " haloPx=" + s.haloPx);
                        }
                    }
                    Object.DestroyImmediate(enemy); yield return null;
                }
                File.WriteAllText(Out + "robots.txt", log.ToString());
            }
        }

        void Dump(string name, GameObject enemy, Bounds bounds)
        {
            log.AppendLine("== " + name + " bounds=" + bounds.size.ToString("F2") + " scale=" + enemy.transform.lossyScale.ToString("F2"));
            var esl = enemy.GetComponent<EnemyStateLights>(); var listed = esl ? esl.Lights.Where(r => r).ToArray() : new Renderer[0];
            log.AppendLine("  EnemyStateLights=" + (esl ? "si (" + listed.Length + " renderers)" : "no") + " EnemyPowerIndicators=" + (enemy.GetComponent<EnemyPowerIndicators>() ? "si" : "no")
                + " scripts=" + string.Join(",", enemy.GetComponents<MonoBehaviour>().Where(m => m).Select(m => m.GetType().Name)));
            foreach (var l in enemy.GetComponentsInChildren<Light>(true))
                log.AppendLine("  LIGHT " + Rel(l.transform, enemy.transform) + " " + l.type + " enabled=" + (l.enabled && l.gameObject.activeInHierarchy) + " color=" + Hex(l.color) + " intensity=" + F(l.intensity) + " range=" + F(l.range) + " shadows=" + l.shadows);
            var block = new MaterialPropertyBlock();
            foreach (var r in enemy.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) { if (r.enabled && r.gameObject.activeInHierarchy) log.AppendLine("  FX " + r.GetType().Name + " " + Rel(r.transform, enemy.transform) + " mat=" + (r.sharedMaterial ? r.sharedMaterial.name : "null")); continue; }
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (!m) continue;
                    bool emissive = m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION") && m.GetColor("_EmissionColor").maxColorComponent > .01f;
                    bool unlit = m.shader.name.Contains("Unlit"); bool isListed = listed.Contains(r);
                    bool eyeName = r.name.ToLowerInvariant().Contains("eye") || r.name.ToLowerInvariant().Contains("visor") || r.name.ToLowerInvariant().Contains("indicator") || r.name.ToLowerInvariant().Contains("light") || r.name.ToLowerInvariant().Contains("core");
                    if (!emissive && !unlit && !isListed && !eyeName) continue;
                    r.GetPropertyBlock(block, i); if (block.isEmpty) r.GetPropertyBlock(block);
                    string mpb = block.isEmpty ? "-" : "em=" + HexHdr(block.GetColor("_EmissionColor")) + " base=" + HexHdr(block.GetColor("_BaseColor"));
                    log.AppendLine("  LUZ " + Rel(r.transform, enemy.transform) + "[" + i + "] " + r.GetType().Name + (r.enabled && r.gameObject.activeInHierarchy ? "" : " (inactivo)") + " mat=" + m.name + " shader=" + m.shader.name
                        + " base=" + (m.HasProperty("_BaseColor") ? HexHdr(m.GetColor("_BaseColor")) : "-") + " emission=" + (m.HasProperty("_EmissionColor") ? HexHdr(m.GetColor("_EmissionColor")) + (m.IsKeywordEnabled("_EMISSION") ? "" : "(keyword off)") : "-")
                        + " queue=" + m.renderQueue + " size=" + r.bounds.size.ToString("F2") + " distToBodyCentre=" + F(Vector3.Distance(r.bounds.center, bounds.center)) + (isListed ? " [EnemyStateLights]" : "") + " MPB:" + mpb);
                }
            }
        }

        // ------------------------------------------------------------------ C: Carronero frame sequences
        /// Real combat bot chasing the invincible player in Map01 R01, filmed from the side under a neutral runtime light
        /// rig (animation reading, not lighting). Contact sheets + measured foot slide. Nothing is saved.
        [UnityTest, Timeout(600000)] public IEnumerator C_CarroneroSequences()
        {
            LogAssert.ignoreFailingMessages = true; Directory.CreateDirectory(Out + "carronero");
            log = new StringBuilder("VisualPass F0 Carronero sequences " + DateTime.Now.ToString("s") + " (60 fps fixed capture step)\n");
            Scene scene = default; yield return Load(Map01 + "/Map01_R01_ControlAcceso.unity", s => scene = s);
            var views = Viewpoints(scene); Assert.That(views.Count, Is.GreaterThan(0));
            var flow = Object.FindAnyObjectByType<CampaignFlow>(); var player = flow.Player;
            var (eye, target) = views[0]; var flat = Vector3.ProjectOnPlane(target - eye, Vector3.up).normalized; var floorEye = eye - Vector3.up * 1.65f;
            // neutral light for reading the animation
            var rig = new GameObject("VP_AnimLight"); var sun = rig.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.1f; sun.color = new Color(1f, .97f, .92f); rig.transform.rotation = Quaternion.Euler(48, 30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.42f, .44f, .48f); RenderSettings.fog = false;
            player.GetComponent<AdvancedPlayerMovement>().ResetForCheckpoint(); player.TeleportToCheckpoint(floorEye + Vector3.up * .05f); Physics.SyncTransforms();
            float capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1;
            // F0 filmed the base prefab; later passes film the Map01 combat variant (the one the new animation lives on)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Out.EndsWith("/F0/") ? Prefabs + "Carronero.prefab" : "Assets/IronRequiem/Prefabs/Map01/Enemies/Carronero_Map01.prefab");
            cam.fieldOfView = 38;

            GameObject bot = null; NavMeshAgent agent = null; SawBotCombat combat = null; Health health = null; Transform footL = null, footR = null;
            IEnumerator Spawn(float dist)
            {
                if (bot) Object.Destroy(bot);
                var p = floorEye + flat * dist; if (NavMesh.SamplePosition(p, out var h, 4f, NavMesh.AllAreas)) p = h.position;
                bot = Object.Instantiate(prefab, p, Quaternion.LookRotation(-flat, Vector3.up)); bot.name = "VP_Carronero";
                foreach (var ec in bot.GetComponentsInChildren<EnemyController>(true)) ec.SelfDestructYHeight = -100000;
                foreach (var lg in bot.GetComponentsInChildren<LODGroup>(true)) lg.ForceLOD(0); // the capture camera is not the one that picks the LOD: film the close-up level
                agent = bot.GetComponent<NavMeshAgent>(); combat = bot.GetComponent<SawBotCombat>(); health = bot.GetComponent<Health>();
                var motion = bot.GetComponent<CarroneroMotion>();
                Transform Leg(string f) => typeof(CarroneroMotion).GetField(f, Any).GetValue(motion) as Transform;
                var ll = Leg("leftLeg"); var rl = Leg("rightLeg"); footL = ll && ll.childCount > 0 ? ll.GetChild(0) : ll; footR = rl && rl.childCount > 0 ? rl.GetChild(0) : rl;
                yield return null; yield return null;
            }
            // side camera that follows the bot; "front" = 3/4 from the front
            void Aim(bool front)
            {
                var c = bot.transform.position + Vector3.up * .75f; var f = bot.transform.forward; var side = Vector3.Cross(Vector3.up, f);
                var pos = front ? c + f * 3.1f + side * 1.6f + Vector3.up * .25f : c + side * 3.6f + Vector3.up * .15f;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(c - pos, Vector3.up));
            }
            var frames = new List<Color32[]>(); const int CW = 480, CH = 270;
            float slideSum = 0; int slideN = 0; Vector3 lastL = default, lastR = default; bool haveLast = false;
            void Frame(bool front)
            {
                if (!bot) return;
                Aim(front); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing; cam.cullingMask = ~(1 << MaskLayer); cam.clearFlags = CameraClearFlags.Skybox;
                frames.Add(Grab(CW, CH));
            }
            void Slide()
            {
                if (!footL || !footR) return;
                if (haveLast)
                {
                    bool leftDown = footL.position.y <= footR.position.y; // the lower foot is the one carrying the weight
                    var d = Vector3.ProjectOnPlane(leftDown ? footL.position - lastL : footR.position - lastR, Vector3.up);
                    slideSum += d.magnitude * 60f; slideN++;
                }
                lastL = footL.position; lastR = footR.position; haveLast = true;
            }
            void Sheet(string name, string caption)
            {
                if (frames.Count == 0) { log.AppendLine(name + ": sin frames. " + caption); return; }
                int cols = 8, rows = Mathf.CeilToInt(frames.Count / (float)cols); var sheet = new Texture2D(cols * CW, rows * CH, TextureFormat.RGB24, false);
                var black = new Color32[cols * CW * rows * CH]; sheet.SetPixels32(black);
                for (int i = 0; i < frames.Count; i++) sheet.SetPixels32((i % cols) * CW, (rows - 1 - i / cols) * CH, CW, CH, frames[i]);
                sheet.Apply(); File.WriteAllBytes(Out + "carronero/" + name + ".jpg", sheet.EncodeToJPG(86)); Object.DestroyImmediate(sheet);
                log.AppendLine(name + ": " + frames.Count + " frames. " + caption + (slideN > 0 ? " | velocidad horizontal media del pie de apoyo = " + F(slideSum / slideN) + " m/s (0 = sin patinar)" : ""));
                frames.Clear(); slideSum = 0; slideN = 0; haveLast = false;
                File.WriteAllText(Out + "carronero.txt", log.ToString());
            }
            IEnumerator Wait(Func<bool> until, float seconds) { float end = Time.realtimeSinceStartup + seconds * 3; while (bot && !until() && Time.realtimeSinceStartup < end) { if (Time.timeScale <= 0) Time.timeScale = 1; yield return null; } }

            // 1 run (as the AI chases, default speed)
            yield return Spawn(15); yield return Wait(() => agent.velocity.magnitude > 5.5f, 5);
            var ctrl = bot.GetComponent<EnemyController>(); log.AppendLine("deteccion: rango=" + (ctrl.DetectionModule ? F(ctrl.DetectionModule.DetectionRange) : "?") + " objetivo conocido=" + (ctrl.KnownDetectedTarget ? ctrl.KnownDetectedTarget.name : "ninguno") + " distancia al jugador=" + F(Vector3.Distance(bot.transform.position, player.transform.position)));
            float runSpeed = agent.velocity.magnitude; for (int i = 0; i < 24; i++) { Frame(false); Slide(); yield return null; }
            Sheet("01_correr", "persecucion real, velocidad del agente " + F(runSpeed) + " m/s (speed " + F(agent.speed) + "), 1 celda = 1 frame (1/60 s)");
            // 2 attack: from the approach to the first seconds of sawing
            yield return Wait(() => Vector3.Distance(bot.transform.position, player.transform.position) < 4.5f, 6);
            float t0 = Time.time, tAttack = -1; for (int i = 0; i < 32; i++) { if (tAttack < 0 && combat && combat.IsAttacking) tAttack = Time.time - t0; Frame(false); yield return null; yield return null; }
            Sheet("02_atacar", "desde 4,5 m del jugador, vista lateral; 1 celda = 2 frames (1/30 s). IsAttacking pasa a true a los " + (tAttack < 0 ? "(no ocurrio)" : F(tAttack) + " s") + " del inicio de la tira; distancia final " + F(Vector3.Distance(bot.transform.position, player.transform.position)) + " m");
            // 3 hit reaction while attacking
            for (int i = 0; i < 16; i++) { if (i == 3) health.TakeDamage(4, player.gameObject); Frame(false); yield return null; }
            Sheet("03_dano", "TakeDamage(4) en la celda 4; 1 celda = 1 frame. Vida " + F(health.CurrentHealth) + "/" + F(health.MaxHealth));
            // 4 turn: the player jumps behind the bot
            var behind = bot.transform.position - bot.transform.forward * 7f; if (NavMesh.SamplePosition(behind, out var bh, 4f, NavMesh.AllAreas)) behind = bh.position;
            player.TeleportToCheckpoint(behind + Vector3.up * .05f); Physics.SyncTransforms(); float yaw0 = bot.transform.eulerAngles.y;
            for (int i = 0; i < 24; i++) { Frame(false); yield return null; yield return null; }
            Sheet("04_girar", "el jugador aparece 7 m detras; 1 celda = 2 frames. Giro total " + F(Mathf.Abs(Mathf.DeltaAngle(yaw0, bot.transform.eulerAngles.y))) + " grados en 0,8 s");
            // 5 death
            yield return Wait(() => agent.velocity.magnitude > 3f, 2);
            for (int i = 0; i < 32; i++) { if (i == 4 && health) health.Kill(); if (!bot) break; Frame(false); yield return null; yield return null; }
            Sheet("05_morir", "Kill() en la celda 5; 1 celda = 2 frames");
            for (int i = 0; i < 90; i++) yield return null;
            // 6 walk: agent speed forced to 1.8 m/s only for this capture, to see the cycle the gait code produces at low speed
            player.TeleportToCheckpoint(floorEye + Vector3.up * .05f); Physics.SyncTransforms();
            yield return Spawn(15); agent.speed = 1.8f; yield return Wait(() => agent.velocity.magnitude > 1.5f, 4); for (int i = 0; i < 30; i++) yield return null;
            float walkSpeed = agent.velocity.magnitude; for (int i = 0; i < 32; i++) { Frame(false); Slide(); yield return null; yield return null; }
            Sheet("06_andar", "velocidad forzada a 1,8 m/s solo para esta captura (medida " + F(walkSpeed) + " m/s); 1 celda = 2 frames");
            // 7 idle and scavenging pose (the three public targets ScavengerBehaviour drives), AI off so it stays put
            yield return Spawn(8); foreach (var mb in bot.GetComponents<MonoBehaviour>()) if (mb is EnemyController || mb is EnemyMobile || mb is SawBotCombat) mb.enabled = false; if (agent.isOnNavMesh) agent.isStopped = true;
            for (int i = 0; i < 24; i++) yield return null;
            for (int i = 0; i < 16; i++) { Frame(true); for (int k = 0; k < 4; k++) yield return null; }
            Sheet("07_reposo", "IA apagada; 1 celda = 4 frames");
            var m2 = bot.GetComponent<CarroneroMotion>(); m2.CrouchTarget = 1; m2.DigTarget = 1; m2.ReachTarget = 1;
            for (int i = 0; i < 24; i++) { Frame(true); for (int k = 0; k < 4; k++) yield return null; }
            Sheet("08_hurgar", "CrouchTarget, DigTarget y ReachTarget a 1 (lo que hace ScavengerBehaviour); 1 celda = 4 frames");

            Time.captureDeltaTime = capture; Time.timeScale = 0; cam.fieldOfView = 60; if (bot) Object.Destroy(bot); Object.Destroy(rig);
            File.WriteAllText(Out + "carronero.txt", log.ToString());
        }

        // ------------------------------------------------------------------ D/E: F1 mock-ups in engine (runtime only)
        const string F1 = "Tools/IronRequiem/Reports/VisualPassFable/F1/";
        struct Level { public string tag; public float ambient, lights, range, exposure, fog; public bool torch; }
        static readonly Level[] Levels =
        {
            new Level { tag = "0_actual", ambient = 1, lights = 1, range = 1, exposure = 0, fog = 1 },
            new Level { tag = "1_legible", ambient = 2.2f, lights = 1.5f, range = 1.25f, exposure = .6f, fog = .8f },
            new Level { tag = "2_luminoso", ambient = 3.2f, lights = 2f, range = 1.4f, exposure = 1f, fog = .65f },
            new Level { tag = "3_actual_linterna", ambient = 1, lights = 1, range = 1, exposure = 0, fog = 1, torch = true },
            new Level { tag = "4_legible_linterna", ambient = 2.2f, lights = 1.5f, range = 1.25f, exposure = .6f, fog = .8f, torch = true },
        };
        Volume exposureVolume; ColorAdjustments exposureCa; Light torchLight;
        Color amb0, amb1, amb2; float fog0; Light[] roomLights; float[] li0, lr0;
        void CaptureRoomState(Scene scene)
        {
            amb0 = RenderSettings.ambientSkyColor; amb1 = RenderSettings.ambientEquatorColor; amb2 = RenderSettings.ambientGroundColor; fog0 = RenderSettings.fogDensity;
            roomLights = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(false)).Where(l => l.enabled).ToArray();
            li0 = roomLights.Select(l => l.intensity).ToArray(); lr0 = roomLights.Select(l => l.range).ToArray();
            if (!exposureVolume)
            {
                var go = new GameObject("VP_F1_Exposure"); exposureVolume = go.AddComponent<Volume>(); exposureVolume.isGlobal = true; exposureVolume.priority = 5;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>(); exposureCa = profile.Add<ColorAdjustments>(false); exposureCa.postExposure.overrideState = true; exposureVolume.sharedProfile = profile;
                var tg = new GameObject("VP_F1_Torch"); tg.transform.SetParent(cam.transform, false); tg.transform.localPosition = new Vector3(.28f, -.18f, .1f);
                torchLight = tg.AddComponent<Light>(); torchLight.type = LightType.Spot; torchLight.range = 30; torchLight.spotAngle = 64; torchLight.innerSpotAngle = 36; torchLight.intensity = 90; torchLight.color = new Color(.82f, .9f, 1f); torchLight.shadows = LightShadows.None; torchLight.enabled = false;
            }
        }
        void Apply(Level v)
        {
            RenderSettings.ambientSkyColor = amb0 * v.ambient; RenderSettings.ambientEquatorColor = amb1 * v.ambient; RenderSettings.ambientGroundColor = amb2 * v.ambient; RenderSettings.ambientLight = RenderSettings.ambientSkyColor;
            RenderSettings.fogDensity = fog0 * v.fog;
            for (int i = 0; i < roomLights.Length; i++) if (roomLights[i]) { roomLights[i].intensity = li0[i] * v.lights; roomLights[i].range = lr0[i] * v.range; }
            exposureCa.postExposure.value = v.exposure; exposureVolume.weight = v.exposure > 0 ? 1 : 0; torchLight.enabled = v.torch;
        }

        static readonly string[] F1Rooms = { "Map01_R01_ControlAcceso", "Map01_R03_Dormitorios", "Map01_R07_Comunicaciones", "Map01_R11_Decantadores", "Map01_R14_Fundicion", "Map01_R17_CentroMando", "Map01_R18_Boveda" };
        [UnityTest, Timeout(600000)] public IEnumerator D_F1Luminosity([ValueSource(nameof(F1Rooms))] string room)
        {
            LogAssert.ignoreFailingMessages = true; Directory.CreateDirectory(F1 + "lum");
            var csv = new StringBuilder(File.Exists(F1 + "lum.csv") ? "" : "room,level,ambient_x,lights_x,range_x,exposure_ev,fog_x,torch,empty_mean,empty_dark_pct,empty_bright_pct,t07_15m_enemy,t07_15m_bg,t07_15m_ratio,verdict,room_lights\n");
            Scene scene = default; yield return Load(Map01 + "/" + room + ".unity", s => scene = s);
            var views = Viewpoints(scene); Assert.That(views.Count, Is.GreaterThan(0));
            var (eye, target) = views[0]; var flat = Vector3.ProjectOnPlane(target - eye, Vector3.up).normalized; var floorEye = eye - Vector3.up * 1.65f;
            CaptureRoomState(scene);
            var t07 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "T07_Current_Repaired.prefab");
            foreach (var v in Levels)
            {
                Apply(v); cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up)); yield return null;
                var s0 = Shoot(null, F1 + "lum/" + room + "_" + v.tag + "_vacio.jpg");
                // enemy at the first distance with a clear line of sight (15 m, else 10, else 6)
                Vector3 p = floorEye + flat * 15; foreach (float d in new[] { 15f, 10f, 6f }) { p = floorEye + flat * d; if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position; if (!Physics.Linecast(eye, p + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) break; }
                var enemy = Object.Instantiate(t07, p, Quaternion.LookRotation(-flat, Vector3.up)); Prepare(enemy, p); yield return null; yield return null;
                if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.ApplyNow(); if (EnemyEyeGlow.Instance) EnemyEyeGlow.Instance.ApplyNow(); yield return null; yield return null;
                var rs = enemy.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
                var s = Shoot(rs, F1 + "lum/" + room + "_" + v.tag + "_T07.jpg");
                string verdict = s.px < 25 ? "NO_VISIBLE" : (s.ratio >= 1.5f || s.eyePx >= 6) ? "legible" : s.ratio >= 1.25f ? "debil" : "ILEGIBLE";
                csv.AppendLine(string.Join(",", room, v.tag, F(v.ambient), F(v.lights), F(v.range), F(v.exposure), F(v.fog), v.torch ? 1 : 0, F(s0.mean), F(s0.dark), F(s0.bright), F(s.enemy), F(s.bg), F(s.ratio), verdict, roomLights.Length + (v.torch ? 1 : 0)));
                Object.DestroyImmediate(enemy);
            }
            Apply(Levels[0]); File.AppendAllText(F1 + "lum.csv", csv.ToString());
            yield return SceneManager.UnloadSceneAsync(scene); yield return null;
        }

        // Eyes: A = always red, state by intensity; B = red eyes + a body indicator that carries the state colour.
        // Emissive core + soft halo quads with an HDR cap (no dynamic Light), added at runtime to the spawned enemy only.
        [UnityTest, Timeout(600000)] public IEnumerator E_F1Eyes()
        {
            LogAssert.ignoreFailingMessages = true; Directory.CreateDirectory(F1 + "eyes");
            log = new StringBuilder("F1 eye mock-ups " + DateTime.Now.ToString("s") + " (HDR cap 4, bloom threshold 1, clamp 12)\n");
            Scene scene = default; yield return Load(Map01 + "/Map01_R01_ControlAcceso.unity", s => scene = s);
            var views = Viewpoints(scene); var (eye, target) = views[0]; var flat = Vector3.ProjectOnPlane(target - eye, Vector3.up).normalized; var floorEye = eye - Vector3.up * 1.65f;
            CaptureRoomState(scene);
            // soft round sprite and additive material, runtime only
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false); var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float r = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f; float a = Mathf.Clamp01(1 - r); a = a * a; px[y * 64 + x] = new Color(1, 1, 1, a); }
            tex.SetPixels32(px); tex.Apply();
            var glow = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); glow.SetFloat("_Surface", 1); glow.SetFloat("_Blend", 2); glow.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); glow.SetInt("_DstBlend", (int)BlendMode.One); glow.SetInt("_ZWrite", 0);
            glow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glow.DisableKeyword("_ALPHATEST_ON"); glow.renderQueue = 3100; glow.SetTexture("_BaseMap", tex);
            var quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx"); var block = new MaterialPropertyBlock(); var billboards = new List<Transform>();
            Transform Quad(Transform parent, Vector3 world, float size, Color hdr, bool billboard)
            {
                var q = new GameObject("VP_Glow"); q.transform.SetParent(parent, true); q.transform.position = world; q.transform.localScale = Vector3.one * size / Mathf.Max(.001f, parent.lossyScale.x);
                q.AddComponent<MeshFilter>().sharedMesh = quadMesh; var mr = q.AddComponent<MeshRenderer>(); mr.sharedMaterial = glow; mr.shadowCastingMode = ShadowCastingMode.Off;
                block.Clear(); block.SetColor("_BaseColor", hdr); mr.SetPropertyBlock(block); if (billboard) billboards.Add(q.transform); return q.transform;
            }
            var red = new Color(1f, .06f, .03f);
            var options = new (string tag, float eyeHdr, float halo, Color? indicator)[]
            {
                ("0_actual", 0, 0, null),
                ("A_patrulla", 1.6f, .55f, null), ("A_alerta", 2.6f, .8f, null), ("A_ataque", 4f, 1.15f, null),
                ("B_patrulla", 2.6f, .8f, new Color(.2f, 1.6f, 1.9f)), ("B_alerta", 2.6f, .8f, new Color(2.4f, 2.4f, 2.6f)), ("B_ataque", 2.6f, .8f, new Color(2.6f, .5f, 2.2f)),
            };
            foreach (var name in new[] { "T07_Current_Repaired", "BigBot_Current", "HoverBot_Current", "Carronero", "SawBot_Current", "Turret_Current" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
                foreach (var level in new[] { Levels[0], Levels[1] })
                    foreach (float d in new[] { 5f, 15f })
                    {
                        Apply(level);
                        var p = floorEye + flat * d; if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
                        foreach (var o in options)
                        {
                            if (d == 15f && (o.tag == "A_alerta" || o.tag == "B_alerta")) continue;
                            var enemy = Object.Instantiate(prefab, p, Quaternion.LookRotation(-flat, Vector3.up)); Prepare(enemy, p); yield return null; yield return null;
                            if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.ApplyNow(); if (EnemyEyeGlow.Instance) EnemyEyeGlow.Instance.ApplyNow(); yield return null; yield return null;
                            var rs = enemy.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
                            var bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
                            billboards.Clear();
                            if (o.eyeHdr > 0)
                            {
                                var esl = enemy.GetComponent<EnemyStateLights>(); if (esl) esl.enabled = false;
                                var eyes = esl ? esl.Lights.Where(r => r && r.enabled && r.gameObject.activeInHierarchy).ToList() : new List<Renderer>();
                                if (eyes.Count == 0) eyes = rs.Where(r => r.name == "CoreIndicator" || r.name == "Sphere" || r.name == "EyeBall").ToList();
                                if (eyes.Count == 0 && name.StartsWith("Turret")) eyes = rs.Where(r => r.name == "TurretTop").ToList();
                                foreach (var e in eyes)
                                {
                                    // red on the eye itself (base + emission, every material slot the state lights already drive)
                                    for (int i = 0; i < e.sharedMaterials.Length; i++) { e.GetPropertyBlock(block, i); block.SetColor("_BaseColor", red * Mathf.Min(o.eyeHdr, 4)); block.SetColor("_EmissionColor", red * Mathf.Min(o.eyeHdr, 4)); e.SetPropertyBlock(block, i); }
                                    float eyeSize = Mathf.Clamp(Mathf.Max(e.bounds.size.x, e.bounds.size.y), .05f, .3f);
                                    var c = name.StartsWith("Turret") ? e.bounds.center + enemy.transform.forward * e.bounds.extents.z * .85f : e.bounds.center;
                                    Quad(e.transform, c, Mathf.Max(.14f, eyeSize * 1.5f), red * Mathf.Min(o.eyeHdr, 4), true);            // core, capped HDR
                                    Quad(e.transform, c, Mathf.Max(.14f, eyeSize * 1.5f) * (2.6f + o.halo * 2.6f), red * (1.1f * o.halo), true); // soft halo
                                }
                                if (o.indicator.HasValue)
                                {
                                    // state bar on the chest, facing where the robot looks
                                    var f = enemy.transform.forward; var at = bounds.center + Vector3.up * bounds.extents.y * .18f + f * (bounds.extents.z * .72f);
                                    var bar = Quad(enemy.transform, at, 1, o.indicator.Value, false); bar.rotation = Quaternion.LookRotation(-f, Vector3.up);
                                    float w = Mathf.Clamp(bounds.size.x * .22f, .16f, .7f); bar.localScale = new Vector3(w, w * .22f, 1) / Mathf.Max(.001f, enemy.transform.lossyScale.x);
                                }
                                log.AppendLine(name + " " + o.tag + ": ojos=" + eyes.Count + " (" + string.Join(",", eyes.Select(e => e.name + " " + F(Mathf.Max(e.bounds.size.x, e.bounds.size.y)) + " m")) + ")");
                            }
                            void Face() { foreach (var b in billboards) if (b) b.rotation = Quaternion.LookRotation(b.position - cam.transform.position, Vector3.up); }
                            string file = name + "_" + level.tag + "_" + o.tag + "_" + d.ToString("0") + "m.jpg";
                            if (d == 5f)
                            {
                                cam.fieldOfView = 40; float size = Mathf.Max(bounds.size.y, bounds.size.x) * 1.15f, back = Mathf.Max(2.2f, size / (2 * Mathf.Tan(20 * Mathf.Deg2Rad)));
                                var front = bounds.center + (flat * -1 * back * .94f + Vector3.Cross(Vector3.up, flat) * back * .34f);
                                cam.transform.SetPositionAndRotation(front, Quaternion.LookRotation(bounds.center - front, Vector3.up)); Face();
                                var s = Shoot(rs, F1 + "eyes/" + file, 1280, 720); cam.fieldOfView = 60;
                                log.AppendLine("  " + file + " enemy=" + F(s.enemy) + " bg=" + F(s.bg) + " ratio=" + F(s.ratio) + " eyePeak=" + F(s.eyePeak) + " hotPx=" + s.hotPx + " haloPx=" + s.haloPx);
                            }
                            else
                            {
                                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(bounds.center - eye, Vector3.up)); Face();
                                var s = Shoot(rs, F1 + "eyes/" + file, 1280, 720);
                                log.AppendLine("  " + file + " px=" + s.px + " enemy=" + F(s.enemy) + " bg=" + F(s.bg) + " ratio=" + F(s.ratio) + " eyePx=" + s.eyePx + " haloPx=" + s.haloPx);
                            }
                            Object.DestroyImmediate(enemy); yield return null;
                        }
                    }
                File.WriteAllText(F1 + "eyes.txt", log.ToString());
            }
            Apply(Levels[0]); Object.DestroyImmediate(glow); Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ rendering and metric (as VisionF0Probe)
        struct Stats { public int px, eyePx, hotPx, haloPx, redPx; public float enemy, bg, ratio, eyePeak, mean, dark, bright; }
        void Game()
        {
            data.renderPostProcessing = true; data.volumeLayerMask = ~0; data.volumeTrigger = cam.transform; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cam.cullingMask = ~(1 << MaskLayer); cam.clearFlags = CameraClearFlags.Skybox;
            var rt = RenderTexture.GetTemporary(64, 36, 24); cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        }
        Stats Shoot(Renderer[] enemy, string file, int w = W, int h = H)
        {
            // the eye manager sizes and fades the eyes from Camera.main: put the player camera where the capture camera is
            var main = Camera.main;
            if (main && main != cam) { main.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation); if (EnemyEyeGlow.Instance) EnemyEyeGlow.Instance.ApplyNow(); }
            data.renderPostProcessing = true; data.volumeLayerMask = ~0; data.volumeTrigger = cam.transform; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cam.cullingMask = ~(1 << MaskLayer); cam.clearFlags = CameraClearFlags.Skybox;
            var game = Grab(w, h); var s = new Stats();
            double sum = 0; int dark = 0, bright = 0;
            for (int i = 0; i < game.Length; i += 3) { float v = Value(game[i]); sum += v; if (v < 10) dark++; if (v > 235) bright++; }
            int n = (game.Length + 2) / 3; s.mean = (float)(sum / n); s.dark = 100f * dark / n; s.bright = 100f * bright / n;
            if (enemy != null && enemy.Length > 0)
            {
                var layers = enemy.Select(r => r.gameObject.layer).ToArray();
                try
                {
                    data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
                    for (int i = 0; i < enemy.Length; i++) enemy[i].gameObject.layer = MaskLayer;
                    cam.cullingMask = 1 << MaskLayer; cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black; var black = Grab(w, h); cam.backgroundColor = Color.white; var white = Grab(w, h);
                    var mask = new bool[game.Length]; int minX = w, minY = h, maxX = -1, maxY = -1, px = 0;
                    for (int i = 0; i < mask.Length; i++)
                    {
                        mask[i] = (white[i].r - black[i].r + white[i].g - black[i].g + white[i].b - black[i].b) / 3f < .5f;
                        if (!mask[i]) continue; px++; int x = i % w, y = i / w; if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                    s.px = px;
                    if (px > 0)
                    {
                        int gx = (maxX - minX) * 3 / 10 + 4, gy = (maxY - minY) * 3 / 10 + 4;
                        int x0 = Mathf.Max(0, minX - gx), x1 = Mathf.Min(w - 1, maxX + gx), y0 = Mathf.Max(0, minY - gy), y1 = Mathf.Min(h - 1, maxY + gy);
                        double se = 0, sb = 0; int nb = 0; var values = new List<float>(px); var ring = new List<float>();
                        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                        {
                            int i = y * w + x; float v = Value(game[i]);
                            // red eye glow: a saturated bright red pixel on or next to the silhouette. The luma criterion (>= 170) can never be met by pure red (luma <= 54).
                            var c = game[i]; if (c.r >= 185 && c.r > 2.2f * c.g && c.r > 2.2f * c.b) s.redPx++;
                            if (mask[i]) { se += v; values.Add(v); } else { sb += v; nb++; ring.Add(v); }
                        }
                        s.enemy = (float)(se / Mathf.Max(1, values.Count)); s.bg = nb > 0 ? (float)(sb / nb) : 0;
                        s.ratio = (Mathf.Max(s.enemy, s.bg) + 5f) / (Mathf.Min(s.enemy, s.bg) + 5f);
                        values.Sort(); s.eyePeak = values[values.Count - 1];
                        float eyeThreshold = Mathf.Max(170, s.bg * 2.2f); s.eyePx = values.Count(v => v >= eyeThreshold); s.hotPx = values.Count(v => v >= 235);
                        // glow spilling outside the silhouette (bloom halo): ring pixels clearly brighter than the ring average
                        float haloThreshold = Mathf.Max(120, s.bg * 2f); s.haloPx = ring.Count(v => v >= haloThreshold);
                    }
                }
                finally { for (int i = 0; i < enemy.Length; i++) if (enemy[i]) enemy[i].gameObject.layer = layers[i]; }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.SetPixels32(game); tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToJPG(88)); Object.DestroyImmediate(tex);
            return s;
        }
        Color32[] Grab(int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); var prev = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); return tex.GetPixels32(); }
            finally { cam.targetTexture = null; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
        static float Value(Color32 c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string P(Vector3 v) => "(" + F(v.x) + ";" + F(v.y) + ";" + F(v.z) + ")";
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        static string HexHdr(Color c) => "(" + F(c.r) + ";" + F(c.g) + ";" + F(c.b) + ")";
        static string Rel(Transform t, Transform root) { var p = t.name; while (t.parent && t.parent != root) { t = t.parent; p = t.name + "/" + p; if (p.Length > 90) break; } return p; }
    }
}
