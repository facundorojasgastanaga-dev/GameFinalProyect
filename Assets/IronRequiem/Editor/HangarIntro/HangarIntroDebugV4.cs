using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// v4 play-mode checks driven through the file bridge: cinematic ending camera, JUGAR visor, the conveyor pit
    /// (view + fall/recovery), fist loadout, montage station, charged punch on a real Carroñero and a Big Bot blast,
    /// each with captures and a frame-time / GC sample around the event.
    public static class HangarIntroDebugV4
    {
        const string Menu = "Tools/Iron Requiem/Hangar Intro/Debug/";
        const string Dir = "Tools/IronRequiem/Bridge";
        const string ReportFile = "Tools/IronRequiem/Reports/HangarIntro/v4_play.txt";
        static readonly List<(double at, Action act)> queue = new List<(double, Action)>();
        static ProfilerRecorder mainThread, gcAlloc; static float worstMs; static long worstGc; static string sampling;

        static void Log(string line) { Directory.CreateDirectory(Path.GetDirectoryName(ReportFile)); File.AppendAllText(ReportFile, DateTime.Now.ToString("HH:mm:ss ") + line + "\n"); Debug.Log("[V4] " + line); }
        static void Schedule(double delay, Action act) { queue.Add((EditorApplication.timeSinceStartup + delay, act)); EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) { queue.Clear(); StopSample(); EditorApplication.update -= Tick; return; }
            if (sampling != null && mainThread.Valid)
            {
                worstMs = Mathf.Max(worstMs, mainThread.LastValue / 1e6f);
                if (gcAlloc.Valid) worstGc = Math.Max(worstGc, gcAlloc.LastValue);
            }
            for (int i = queue.Count - 1; i >= 0; i--)
                if (EditorApplication.timeSinceStartup >= queue[i].at) { var a = queue[i].act; queue.RemoveAt(i); try { a(); } catch (Exception e) { Log("ERROR " + e.Message); } }
            if (queue.Count == 0 && sampling == null) EditorApplication.update -= Tick;
        }
        static void StartSample(string label)
        {
            StopSample(); worstMs = 0; worstGc = 0; sampling = label;
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);
        }
        static void StopSample()
        {
            if (sampling == null) return;
            Log($"profile '{sampling}': worst main thread {worstMs:F1} ms, worst GC alloc in frame {worstGc} B (Editor play mode)");
            sampling = null; mainThread.Dispose(); gcAlloc.Dispose();
        }
        static PlayerCharacterController Player => Object.FindAnyObjectByType<PlayerCharacterController>();
        static void Shot(string name) { var p = Player; if (p) Capture(p.PlayerCamera, name); }
        static void Capture(Camera cam, string name)
        {
            if (!cam) return;
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; var target = cam.targetTexture; bool was = cam.enabled;
            try { cam.targetTexture = rt; cam.enabled = true; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG()); }
            finally { cam.targetTexture = target; cam.enabled = was; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
        static void Place(Vector3 p, float yaw)
        {
            var player = Player; if (!player) return;
            var move = player.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint();
            player.TeleportToCheckpoint(p); player.transform.rotation = Quaternion.Euler(0, yaw, 0); player.PlayerCamera.transform.localRotation = Quaternion.identity; Physics.SyncTransforms();
        }
        static string Loadout()
        {
            var w = Player ? Player.GetComponent<PlayerWeaponsManager>() : null; if (!w) return "no player";
            var names = Enumerable.Range(0, 9).Select(w.GetWeaponAtSlotIndex).Where(x => x).Select(x => x.name.Replace("(Clone)", "") + (x.PermanentMelee ? "*" : ""));
            var heat = Object.FindAnyObjectByType<WeaponHeatHud>(); var g = heat ? heat.GetComponent<CanvasGroup>() : null;
            return "weapons=[" + string.Join(", ", names) + "] active=" + (w.GetActiveWeapon() ? w.GetActiveWeapon().name : "-") + " heatHudAlpha=" + (g ? g.alpha.ToString("F2") : "n/a");
        }

        [MenuItem(Menu + "V4 report loadout (play)")] static void ReportLoadout() => Log(Loadout());

        // hangar-local -> world (Room00_Hangar origin (0,0,-109), yaw 180)
        static Vector3 Hangar(float x, float y, float z) => new Vector3(-x, y, -109f - z);

        [MenuItem(Menu + "V4 capture cinematic ending (play)")]
        static void CinematicEnd()
        {
            File.AppendAllText(ReportFile, "");
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); if (!cine) { Log("no cinematic"); return; }
            var director = cine.GetComponent<PlayableDirector>(); double duration = director.duration;
            var marks = new[] { 4.0, 3.0, 2.2, 1.5, .9, .4, .1 }; int next = 0;
            void Poll()
            {
                if (!EditorApplication.isPlaying || !cine) { EditorApplication.update -= Poll; return; }
                if (next < marks.Length && cine.IsPlaying && director.time >= duration - marks[next])
                {
                    var cam = cine.GetComponentInChildren<Camera>(true);
                    var pod = cine.transform.root.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => c.name.Contains("Pod") || c.GetComponentInChildren<Renderer>() && c.size.y > 3f && c.size.z < 1.2f);
                    float clearance = pod ? Vector3.Distance(pod.ClosestPoint(cam.transform.position), cam.transform.position) : -1;
                    Capture(cam, $"v4end_{next}_t-{marks[next]:0.0}"); Log($"ending capture t={director.time:0.00}/{duration:0.00} cam={cam.transform.position} capsuleClearance={clearance:F2} m");
                    next++;
                }
                if (cine.Finished) { EditorApplication.update -= Poll; Schedule(1.0, () => { Shot("v4end_fp"); Log("first person after hand-off " + Loadout()); }); }
            }
            EditorApplication.update -= Poll; EditorApplication.update += Poll; Log("ending capture armed (" + duration.ToString("0.0") + " s)");
        }

        [MenuItem(Menu + "V4 JUGAR visor check (play IntroMenu)")]
        static void Jugar()
        {
            var view = Object.FindObjectsByType<RequiemMenuView>(FindObjectsSortMode.None).FirstOrDefault(v => v.gameObject.scene.name == "IntroMenu");
            var robot = Object.FindAnyObjectByType<CapsuleRobotIdle>();
            if (!view || !robot) { Log("JUGAR check: no menu view / capsule robot"); return; }
            view.Primary(); Log("JUGAR pressed");
            foreach (var t in new[] { .3, .9, 1.5, 1.9, 2.4, 2.9 })
            {
                double at = t;
                Schedule(t, () => { var cam = Camera.main; if (cam) Capture(cam, $"v4jugar_t{at:0.0}"); var visor = robot.GetComponentInChildren<Light>(true); Log($"JUGAR t={at:0.0} robotDark={robot.IsDark} visorLight={(visor ? visor.intensity.ToString("F2") : "n/a")}"); });
            }
        }

        [MenuItem(Menu + "V4 pit view + fall test (play)")]
        static void Pit()
        {
            Log("pit: before " + Loadout());
            Place(Hangar(0, .03f, 9.5f), 180); // yard side, facing the belt (hangar +Z is world -Z)
            Schedule(.4, () => Shot("v4pit_view"));
            Schedule(.6, () => { Place(Hangar(-6f, .03f, 12.0f), 150); });
            Schedule(1.0, () => Shot("v4pit_edge"));
            Schedule(1.3, () =>
            {
                var p = Player; var h = p.GetComponent<Health>(); var rec = Object.FindObjectsByType<GapFallRecovery>(FindObjectsSortMode.None).FirstOrDefault(g => g.name == "PitFallRecovery");
                Log($"pit: recovery volume={(rec ? "yes" : "MISSING")} hp={h.CurrentHealth:F0} invincible={h.Invincible}");
                bool inv = h.Invincible; h.Invincible = false; float hp = h.CurrentHealth; int before = rec ? rec.RecoveryCount : -1;
                Place(Hangar(0, .5f, 18f), 180); // over the void
                Schedule(.35, () => Shot("v4pit_fall"));
                Schedule(2.6, () => { var pos = Player.transform.position; Log($"pit: after fall pos={pos} hp {hp:F0}->{h.CurrentHealth:F0} recoveries {before}->{(rec ? rec.RecoveryCount : -1)} grounded={Player.IsGrounded}"); Shot("v4pit_returned"); h.Invincible = inv; });
            });
        }

        [MenuItem(Menu + "V4 charged punch on a Carroñero (play)")]
        static void Punch()
        {
            var player = Player; var melee = player ? player.GetComponent<ProtagonistMelee>() : null; var weapons = player ? player.GetComponent<PlayerWeaponsManager>() : null;
            var victim = Object.FindObjectsByType<Health>(FindObjectsSortMode.None).Where(h => h && h.CurrentHealth > 0 && h.GetComponent<EnemyController>()).OrderBy(h => Vector3.Distance(h.transform.position, player.transform.position)).FirstOrDefault();
            if (!melee || !victim) { Log("punch: no melee/victim"); return; }
            var box = victim.GetComponent<Collider>(); var c = box ? box.bounds.center : victim.transform.position + Vector3.up;
            var flat = Vector3.ProjectOnPlane(victim.transform.forward, Vector3.up).normalized; if (flat.sqrMagnitude < .1f) flat = Vector3.forward;
            var stand = c - flat * 1.3f; // behind it: a scavenger faces its scrap pile stand.y = victim.transform.position.y + .03f;
            Place(stand, Quaternion.LookRotation(flat).eulerAngles.y); player.PlayerCamera.transform.LookAt(c);
            float hp = victim.CurrentHealth; var enemy = victim.GetComponent<EnemyController>();
            // v5: hold F (right-hand strike; with a weapon on the forearm it is the mechanical bash)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            void Mouse(bool down) { if (kb != null) UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, down ? new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F) : new UnityEngine.InputSystem.LowLevel.KeyboardState()); }
            Schedule(.5, () => { Mouse(true); var mi = player.GetComponent<MechanicInput>(); Log("punch: F held on " + victim.name + " hp=" + hp + " armed=" + !melee.Unarmed + " canAct=" + (mi && mi.CanAct) + " locked=" + PlayerInputLock.Locked + " cursor=" + Cursor.lockState + " attacking=" + melee.IsAttacking); });
            Schedule(1.1, () => { Shot("v4punch_charge"); Log($"punch: charging={melee.IsCharging} level={melee.ChargeLevel:F2} Fheld={(UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.isPressed)}"); });
            Schedule(1.85, () => { Shot("v4punch_full"); Log($"punch: full={melee.FullyCharged}"); player.PlayerCamera.transform.LookAt(c); });
            Schedule(1.95, () => { var b = victim.GetComponent<Collider>(); var cc = b ? b.bounds.center : victim.transform.position + Vector3.up; var f2 = Vector3.ProjectOnPlane(victim.transform.forward, Vector3.up).normalized; var st = cc - f2 * 1.3f; st.y = victim.transform.position.y + .03f; Place(st, Quaternion.LookRotation(f2).eulerAngles.y); player.PlayerCamera.transform.LookAt(cc); });
            Schedule(2.0, () => { StartSample("heavy punch (no captures in window)"); Mouse(false); });
            Schedule(2.9, () => StopSample());
            Schedule(3.0, () => { Shot("v4punch_after"); Log($"punch: armedBash={melee.ArmedStrike} heavy={melee.LastWasHeavy} full={melee.LastWasFull} hits={melee.LastHitCount} hp {hp:F0}->{victim.CurrentHealth:F0} staggered={(enemy ? enemy.IsStaggered.ToString() : "n/a")} timeScale={Time.timeScale:F2}"); });
        }

        [MenuItem(Menu + "V4 Big Bot blast (play)")]
        static void Blast()
        {
            var player = Player; if (!player) return;
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Projectiles/Projectile_BigBot_Blast_v4.prefab");
            var fx = projectile ? projectile.GetComponent<BigBotBlastFx>() : null;
            var blast = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VFX/FX_BigBot_Blast_v4.prefab");
            var big = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab").GetComponentInChildren<BigBotCombat>(true);
            Log($"blast: BigBot damage={big.Damage} radius={big.ExplosionRadius} explosive={big.ExplosiveShots} blastFx={(fx ? "yes" : "MISSING")}");
            var cam = player.PlayerCamera.transform; var point = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 7f;
            if (Physics.Raycast(point + Vector3.up * 3, Vector3.down, out var hit, 10, -1, QueryTriggerInteraction.Ignore)) point = hit.point;
            // warm-up blast (first use instantiates the pool), then a measured one without captures, then a visual one
            FxPool.Spawn(blast, point + Vector3.up * .05f + Vector3.right * 3, Quaternion.LookRotation(Vector3.up), 6f);
            Schedule(1.5, () => { StartSample("Big Bot blast (pooled, no captures)"); FxPool.Spawn(blast, point + Vector3.up * .05f, Quaternion.LookRotation(Vector3.up), 6f); DistanceCameraImpulse.Emit(point, 18f, .9f, .5f); });
            Schedule(3.0, () => StopSample());
            Schedule(3.6, () => { FxPool.Spawn(blast, point + Vector3.up * .05f, Quaternion.LookRotation(Vector3.up), 6f); DistanceCameraImpulse.Emit(point, 18f, .9f, .5f); });
            Schedule(3.66, () => Shot("v4blast_0"));
            Schedule(3.85, () => Shot("v4blast_1"));
            Schedule(4.3, () => Shot("v4blast_2"));
            Schedule(5.6, () => Shot("v4blast_3"));
        }

        [MenuItem(Menu + "V4 montage station test (play)")]
        static void Station() => StationRun(false);
        [MenuItem(Menu + "V5 montage station SKIP test (play)")]
        static void StationSkip() => StationRun(true);
        static void StationRun(bool skip)
        {
            var station = Object.FindAnyObjectByType<WeaponMountStation>(); if (!station) { Log("station: MISSING"); return; }
            Log("station: before " + Loadout() + " granted=" + station.Granted);
            Place(station.transform.position - station.transform.forward * 3f + Vector3.up * .03f, station.transform.eulerAngles.y);
            Schedule(.3, () => Shot(skip ? "v5skip_approach" : "v5station_approach"));
            Schedule(.5, () => Place(station.transform.position - station.transform.forward * .5f + Vector3.up * .03f, station.transform.eulerAngles.y));
            Schedule(.9, () => { Shot(skip ? "v5skip_prompt" : "v5station_prompt"); station.Begin(); Log("station: begin busy=" + station.Busy + " locked=" + PlayerInputLock.Locked); });
            if (skip)
            {
                Schedule(2.4, () => { Shot("v5skip_mid"); station.Skip(); Log($"station: skipped at t~1.5 granted={station.Granted} busy={station.Busy} locked={PlayerInputLock.Locked} " + Loadout()); });
                Schedule(2.8, () => Shot("v5skip_end"));
                return;
            }
            var marks = new[] { .5, 1.0, 1.4, 1.8, 2.3, 2.9, 3.4, 3.75, 4.2, 4.65, 5.0, 5.4 };
            foreach (var m in marks) { double at = m; Schedule(.9 + at, () => { Shot($"v5station_t{at:0.00}"); Log($"station: t={station.SequenceTime:0.00} locked={PlayerInputLock.Locked}"); }); }
            Schedule(.9 + WeaponMountStation.Duration + .5, () => { Shot("v5station_done"); Log($"station: done granted={station.Granted} busy={station.Busy} locked={PlayerInputLock.Locked} " + Loadout()); });
        }
    }
}
