using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace IronRequiem.EditorTools
{
    /// Play-mode checks for Room00_Hangar (driven through the file bridge): start, capture the cinematic at fixed
    /// times, skip, report the hand-off state, provoke a scavenger and walk into the lobby airlock.
    public static class HangarIntroDebug
    {
        const string Menu = "Tools/Iron Requiem/Hangar Intro/Debug/";
        const string Dir = "Tools/IronRequiem/Bridge";
        const string ReportFile = "Tools/IronRequiem/Reports/HangarIntro/play.txt";
        // capture marks follow the pose timing (the cinematic length changes between versions)
        static float[] Marks = { 1.0f, 3.0f, 4.4f, 5.25f, 6.8f, 7.9f, 9.0f, 11.2f, 13.8f, 16.5f, 18.8f };
        static float[] HandMarks = new float[0]; static int nextHand;
        static int next; static double handoffAt = -1, provokeAt = -1; static string prefix = "hi"; static readonly List<float> afterHandoff = new List<float>();

        static void Log(string line) { Directory.CreateDirectory(Path.GetDirectoryName(ReportFile)); File.AppendAllText(ReportFile, System.DateTime.Now.ToString("HH:mm:ss ") + line + "\n"); Debug.Log("[HangarIntro] " + line); }

        [MenuItem(Menu + "Start campaign (play)")]
        static void StartCampaign()
        {
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (!menu) { Log("no CombatLabMenu"); return; }
            if (!menu.IsPlaying) menu.BeginGame(); Log("BeginGame");
        }

        [MenuItem(Menu + "Auto-capture cinematic (play)")]
        static void AutoCapture()
        {
            File.WriteAllText(ReportFile, ""); next = 0; nextHand = 0; Marks = null; handoffAt = -1; afterHandoff.Clear(); afterHandoff.AddRange(new[] { 1.2f, 6f }); prefix = "hi";
            EditorApplication.update -= Tick; EditorApplication.update += Tick; Log("auto-capture armed");
        }

        [MenuItem(Menu + "Auto-capture cinematic as BEFORE (play)")]
        static void AutoCaptureBefore() { AutoCapture(); prefix = "pre"; }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); if (!cine) return;
            var director = cine.GetComponent<PlayableDirector>();
            var pose = Object.FindAnyObjectByType<WakeUpRobotPose>();
            if (Marks == null && pose) BuildMarks(pose.Times, (float)director.duration);
            if (Marks == null) return;
            if (cine.IsPlaying && pose && nextHand < HandMarks.Length && director.time >= HandMarks[nextHand])
            {
                HandCapture(pose, $"{prefix}_hand_{nextHand}_t{HandMarks[nextHand]:0.0}"); Log($"hand capture t={director.time:0.00}"); nextHand++;
            }
            if (cine.IsPlaying && next < Marks.Length && director.time >= Marks[next])
            {
                var cam = cine.GetComponentInChildren<Camera>(true);
                Capture(cam, $"{prefix}_{next}_t{Marks[next]:0.0}"); SideCapture(cine, $"{prefix}_side_{next}_t{Marks[next]:0.0}"); Log($"capture t={director.time:0.00} {State()}"); next++;
            }
            if (cine.Finished && handoffAt < 0) { handoffAt = EditorApplication.timeSinceStartup; Log("handoff " + State()); }
            if (handoffAt > 0 && afterHandoff.Count > 0 && EditorApplication.timeSinceStartup - handoffAt >= afterHandoff[0])
            {
                var player = Object.FindAnyObjectByType<PlayerCharacterController>();
                Capture(player.PlayerCamera, $"{prefix}_fp_{afterHandoff[0]:0.0}"); Log($"fp capture +{afterHandoff[0]} {State()}"); afterHandoff.RemoveAt(0);
                if (afterHandoff.Count == 0) EditorApplication.update -= Tick;
            }
        }

        [MenuItem(Menu + "Skip cinematic (play)")]
        static void Skip() { var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); if (cine) { cine.RequestSkip(); Log("skip requested " + State()); } }

        [MenuItem(Menu + "Restart from first room (play)")]
        static void Restart() { var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (menu) { Log("restart from first room"); menu.RestartFromFirstRoom(); } }

        [MenuItem(Menu + "Report state (play)")]
        static void Report() => Log(State());

        [MenuItem(Menu + "Capture player view (play)")]
        static void CapturePlayer() { var p = Object.FindAnyObjectByType<PlayerCharacterController>(); if (p) { Capture(p.PlayerCamera, "hi_player_" + System.DateTime.Now.ToString("HHmmss")); Log("player capture " + State()); } }

        [MenuItem(Menu + "Provoke scavenger 0 (play)")]
        static void Provoke()
        {
            var bot = Object.FindObjectsByType<ScavengerBehaviour>().OrderBy(b => b.name).FirstOrDefault(); var player = Object.FindAnyObjectByType<PlayerCharacterController>();
            if (!bot || !player) { Log("no scavenger/player"); return; }
            bot.GetComponent<Health>().TakeDamage(1, player.gameObject); Log("provoked " + bot.name + " " + State());
            provokeAt = EditorApplication.timeSinceStartup; EditorApplication.update -= AfterProvoke; EditorApplication.update += AfterProvoke;
        }
        static void AfterProvoke() { if (EditorApplication.timeSinceStartup - provokeAt < 2) return; EditorApplication.update -= AfterProvoke; Log("after provoke " + State()); }

        static void Teleport(Vector3 p, string label) { var player = Object.FindAnyObjectByType<PlayerCharacterController>(); if (!player) return; player.TeleportToCheckpoint(p); Log(label + " " + State()); }

        [MenuItem(Menu + "Walk into connection chamber (play)")] static void Chamber() => Teleport(new Vector3(0, .03f, -80f), "teleported to chamber");
        [MenuItem(Menu + "Enter alarm corridor (play)")] static void Corridor() => Teleport(new Vector3(0, .03f, -62f), "teleported into corridor");
        [MenuItem(Menu + "Walk into lobby airlock (play)")] static void Lobby() => Teleport(new Vector3(0, .03f, -30.5f), "teleported to lobby");

        [MenuItem(Menu + "Kill current room enemies (play)")]
        static void KillAll()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacterController>(); int n = 0;
            foreach (var e in Object.FindObjectsByType<EnemyController>()) { var h = e.GetComponent<Health>(); if (h && h.CurrentHealth > 0) { h.Kill(); n++; } }
            Log("killed " + n + " " + State());
        }

        // ---- options console (pause) + CCTV checks
        static RequiemMenuView OpenView()
        {
            var views = Object.FindObjectsByType<RequiemMenuView>(FindObjectsInactive.Include);
            return views.FirstOrDefault(v => v.isActiveAndEnabled) ?? views.FirstOrDefault();
        }

        [MenuItem(Menu + "Open pause options (play)")]
        static void PauseOptions()
        {
            // the campaign pause view is the RequiemMenuView with a pause owner (InGameMenuManager)
            var views = Object.FindObjectsByType<RequiemMenuView>(FindObjectsInactive.Include);
            var view = views.FirstOrDefault(v => PauseOwner(v)); if (!view) { Log("no pause RequiemMenuView among " + views.Length); return; }
            var owner = PauseOwner(view); if (!owner.IsPaused) owner.SetPauseMenuActivation(true);
            view.OpenOptions(); Log($"options open view={view.name} paused={owner.IsPaused} timeScale={Time.timeScale} {Cctv()}");
        }

        static InGameMenuManager PauseOwner(RequiemMenuView v) => new SerializedObject(v).FindProperty("pauseOwner").objectReferenceValue as InGameMenuManager;

        [MenuItem(Menu + "Close pause (play)")]
        static void ClosePause()
        {
            var view = Object.FindObjectsByType<RequiemMenuView>(FindObjectsInactive.Include).FirstOrDefault(v => PauseOwner(v));
            if (view) PauseOwner(view).ClosePauseMenu(); Log("pause closed " + Cctv());
        }

        /// Moves the visible faders to fixed values (the views' own listeners persist them), reopens options and reports
        /// what the controls read back. Run twice: once to set, once after a restart to check persistence.
        [MenuItem(Menu + "Probe options persistence (play)")]
        static void ProbeOptions()
        {
            var view = Object.FindObjectsByType<RequiemMenuView>(FindObjectsInactive.Exclude).FirstOrDefault(v => v.IsOptionsOpen);
            if (!view) { Log("probe: no open options panel"); return; }
            var so = new SerializedObject(view);
            var vol = so.FindProperty("volume").objectReferenceValue as UnityEngine.UI.Slider; var sens = so.FindProperty("sensitivity").objectReferenceValue as UnityEngine.UI.Slider;
            Log($"probe before vol={vol.value:0.00} sens={sens.value:0.00}");
            vol.value = Mathf.Approximately(vol.value, .37f) ? .8f : .37f; sens.value = Mathf.Lerp(sens.minValue, sens.maxValue, .6f);
            view.ShowHome(); view.OpenOptions();
            Log($"probe after reopen vol={vol.value:0.00} sens={sens.value:0.00} {Cctv()}");
        }

        [MenuItem(Menu + "Capture CCTV monitor (play)")]
        static void CaptureCctv()
        {
            var mon = Object.FindObjectsByType<CctvMonitor>(FindObjectsInactive.Exclude).FirstOrDefault(m => m.Target);
            if (!mon) { Log("cctv capture: no active monitor"); return; }
            var rt = mon.Target; var prev = RenderTexture.active; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try { RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(Dir, "cctv_" + System.DateTime.Now.ToString("HHmmss") + ".png"), tex.EncodeToPNG()); }
            finally { RenderTexture.active = prev; Object.DestroyImmediate(tex); }
            Log("cctv capture " + Cctv());
        }

        // ---- frame timing (editor play mode: indicative only, the player build is the reference)
        static readonly List<float> frameMs = new List<float>(); static long gcBytes; static int gcFrames; static double measureUntil = -1; static string measureTag;
        static Unity.Profiling.ProfilerRecorder gcRecorder, mainRecorder;

        [MenuItem(Menu + "Measure frames 5 s (play)")]
        static void Measure() => BeginMeasure("measure");

        static void BeginMeasure(string tag)
        {
            frameMs.Clear(); gcBytes = 0; gcFrames = 0; measureTag = tag; measureUntil = EditorApplication.timeSinceStartup + 5;
            gcRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
            mainRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 15);
            EditorApplication.update -= MeasureTick; EditorApplication.update += MeasureTick;
        }

        static void MeasureTick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= MeasureTick; return; }
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            if (gcRecorder.Valid) { gcBytes += gcRecorder.LastValue; gcFrames++; }
            if (EditorApplication.timeSinceStartup < measureUntil) return;
            EditorApplication.update -= MeasureTick;
            var sorted = frameMs.OrderBy(x => x).ToList(); int n = sorted.Count;
            float avg = n > 0 ? sorted.Average() : 0, p95 = n > 0 ? sorted[Mathf.Min(n - 1, (int)(n * .95f))] : 0, max = n > 0 ? sorted[n - 1] : 0;
            double main = mainRecorder.Valid && mainRecorder.Count > 0 ? mainRecorder.LastValue * 1e-6 : -1;
            Log($"{measureTag} frames={n} avg={avg:0.00}ms ({(avg > 0 ? 1000f / avg : 0):0} fps) p95={p95:0.00} max={max:0.00} mainThread={main:0.00}ms gc/frame={(gcFrames > 0 ? gcBytes / gcFrames : 0)}B screen={Screen.width}x{Screen.height} {Cctv()}");
            gcRecorder.Dispose(); mainRecorder.Dispose();
        }

        static string Cctv()
        {
            var mons = Object.FindObjectsByType<CctvMonitor>(FindObjectsInactive.Exclude);
            return $"cctv feed={(CctvFeed.Active ? CctvFeed.Active.gameObject.scene.name + "/" + CctvFeed.Active.name : "none")} monitors={mons.Length} signal={mons.Count(m => m.HasSignal)} rt={string.Join(",", mons.Select(m => m.Target ? m.Target.width + "x" + m.Target.height : "-"))}";
        }

        // overview shots of the v3 scrap yard (hangar-local camera poses) and of the corridor from its entrance
        [MenuItem(Menu + "Capture scrap yard + corridor views (play)")]
        static void CaptureViews()
        {
            var go = new GameObject("ViewCaptureCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 60; cam.nearClipPlane = .05f; cam.farClipPlane = 200; cam.enabled = false;
            try
            {
                var hangar = GameObject.Find("HangarSector");
                if (hangar)
                {
                    var h = hangar.transform; int i = 0;
                    foreach (var (pos, look) in new[] { (new Vector3(0, 2.2f, -1f), new Vector3(-9, 1, -16)), (new Vector3(0, 2.2f, -1f), new Vector3(9, 1, -16)), (new Vector3(0, 6f, -22f), new Vector3(0, 0, 6)), (new Vector3(-2, 2.5f, -12f), new Vector3(-14, 1.5f, 12)) })
                    { go.transform.position = h.TransformPoint(pos); go.transform.LookAt(h.TransformPoint(look)); Capture(cam, "v3_yard_" + i++); }
                }
                if (UnityEngine.SceneManagement.SceneManager.GetSceneByName("Room00b_AlarmCorridor").isLoaded)
                {
                    go.transform.position = new Vector3(0, 1.7f, -68.5f); go.transform.LookAt(new Vector3(0, 1.5f, -40)); Capture(cam, "v3_corridor_0");
                    go.transform.position = new Vector3(7.5f, 6.5f, -67f); go.transform.LookAt(new Vector3(-2, 0, -45)); Capture(cam, "v3_corridor_1");
                }
                Log("view captures " + State());
            }
            finally { Object.DestroyImmediate(go); }
        }

        [MenuItem(Menu + "Transition preload OFF - v2 order (play)")]
        static void PreloadOff() { var f = Object.FindAnyObjectByType<CampaignFlow>(); if (f) { f.PreloadNextStage = false; Log("transition preload OFF (baseline)"); } }
        [MenuItem(Menu + "Transition preload ON (play)")]
        static void PreloadOn() { var f = Object.FindAnyObjectByType<CampaignFlow>(); if (f) { f.PreloadNextStage = true; Log("transition preload ON"); } }

        [MenuItem(Menu + "Capture player + corridor (play)")]
        static void CaptureCorridor()
        {
            var p = Object.FindAnyObjectByType<PlayerCharacterController>(); if (!p) return;
            Capture(p.PlayerCamera, "hi_corr_" + System.DateTime.Now.ToString("HHmmss")); Log("corridor capture " + State());
        }

        static string State()
        {
            var s = new StringBuilder();
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (cine) { var d = cine.GetComponent<PlayableDirector>(); s.Append($"restart={CombatLabMenu.ResumedAfterRestart} cine playing={cine.IsPlaying} finished={cine.Finished} skipped={cine.WasSkipped} t={(d ? d.time : -1):0.00} "); }
            var p = Object.FindAnyObjectByType<PlayerCharacterController>();
            if (p) s.Append($"player={p.transform.position} yaw={p.transform.eulerAngles.y:0.0} pitch={p.PlayerCamera.transform.localEulerAngles.x:0.0} cam={p.PlayerCamera.enabled} lock={PlayerInputLock.Locked} ");
            var flow = Object.FindAnyObjectByType<CampaignFlow>();
            var alarm = Object.FindAnyObjectByType<CorridorAlarm>(); if (alarm) s.Append($"alarm={alarm.Alarmed} cleared={alarm.Cleared} ");
            var glass = Object.FindAnyObjectByType<CapsuleGlassBreak>(); if (glass) s.Append($"glass cracked={glass.IsCracked} shattered={glass.IsShattered} shards={glass.ActiveShards} ");
            if (flow) s.Append($"stage={flow.CurrentStage} busy={flow.Busy} room={(flow.CurrentRoom ? flow.CurrentRoom.gameObject.scene.name : "-")} completed={(flow.CurrentRoom && flow.CurrentRoom.Completed)} msg='{(flow.Message ? flow.Message.text : "")}' ");
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c => c.isRootCanvas && (c.name == "HUD" || c.name == "ArenaMessages"))) s.Append($"{c.name}={c.enabled} ");
            foreach (var b in Object.FindObjectsByType<ScavengerBehaviour>().OrderBy(b => b.name))
            {
                var m = b.GetComponent<EnemyMobile>(); var e = b.GetComponent<EnemyController>();
                s.Append($"\n  {b.name} pos={b.transform.position} scav={b.IsScavenging} state={(m ? m.AiState.ToString() : "-")} known={(e && e.KnownDetectedTarget)} hits={b.RummageHits} throws={b.Throws} carry={b.Carrying} {b.DebugState}");
            }
            return s.ToString();
        }

        // side view of the robot's legs (temporary camera beside the stand point)
        static void BuildMarks(WakeUpRobotPose.Timing T, float duration)
        {
            Marks = new[] { 1.0f, (T.fingersStart + T.cannonEnd) * .5f, T.punch1 + .05f, T.punch2 + .1f, (T.rightStepStart + T.rightStepEnd) * .5f, (T.leftStepEnd + T.crouch) * .5f,
                (T.crouch + T.stand) * .5f, (T.handStart + T.handEnd) * .5f, (T.turnStart + T.turnEnd) * .5f, (T.turnBackStart + T.turnBackEnd) * .5f, duration - .8f };
            float span = T.handEnd - T.handStart;
            HandMarks = new[] { T.handStart + span * .3f, T.handStart + span * .45f, T.handStart + span * .6f, T.handStart + span * .75f };
            Log("marks " + string.Join(",", Marks.Select(m => m.ToString("0.0"))) + " hand " + string.Join(",", HandMarks.Select(m => m.ToString("0.0"))));
        }

        // close-up of the right forearm and hand from two sides (checks the wrist roll for skin deformation)
        static void HandCapture(WakeUpRobotPose pose, string name)
        {
            var hand = pose.RightHand; var fore = pose.RightForearm; if (!hand || !fore) return;
            var mid = Vector3.Lerp(fore.position, hand.position, .6f); var axis = (hand.position - fore.position).normalized;
            var side = Vector3.Cross(Vector3.up, axis); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
            var go = new GameObject("HandCaptureCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 35; cam.nearClipPlane = .02f; cam.enabled = false;
            try
            {
                go.transform.position = mid + side * .75f + Vector3.up * .12f; go.transform.LookAt(mid); Capture(cam, name + "_a");
                // second view from above and in front of the wrist (the first one can be blocked by the shoulder)
                go.transform.position = mid + (side * .35f + Vector3.up * .6f - axis * .45f).normalized * .85f; go.transform.LookAt(mid); Capture(cam, name + "_b");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void SideCapture(HangarIntroCinematic cine, string name)
        {
            var stand = cine.transform.Find("StandPoint"); if (!stand) return;
            var go = new GameObject("SideCaptureCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 40; cam.nearClipPlane = .05f; cam.enabled = false;
            var right = Vector3.Cross(Vector3.up, stand.forward);
            go.transform.position = stand.position + right * 3.6f - stand.forward * .6f + Vector3.up * 1.1f; go.transform.LookAt(stand.position - stand.forward * .6f + Vector3.up * 1.0f);
            try { Capture(cam, name); } finally { Object.DestroyImmediate(go); }
        }

        static void Capture(Camera cam, string name)
        {
            if (!cam) return;
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; var target = cam.targetTexture; bool was = cam.enabled;
            try { cam.targetTexture = rt; cam.enabled = true; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG()); }
            finally { cam.targetTexture = target; cam.enabled = was; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
    }
}
