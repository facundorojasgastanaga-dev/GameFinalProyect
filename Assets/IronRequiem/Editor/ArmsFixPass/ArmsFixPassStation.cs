using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    public static partial class ArmsFixPassTools
    {
        static WeaponMountStation station; static StringBuilder stationCsv; static readonly List<float> stationShots = new List<float>();
        static int stationShot; static float stationLastT = -1, stationLastCov = -1, stationMaxCov, stationMaxAlt = -1; static string stationMaxCovAt = "", stationMaxAltAt = "";
        static HashSet<Renderer> playerRenderersBefore; static readonly HashSet<string> stationWallHits = new HashSet<string>();
        static string Where => station ? station.gameObject.scene.name : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        /// Optional subset of the station parts used for the coverage number (mount station art pass: moving parts
        /// only, so the static gantry does not count as "arm in the frame"). Null = every station part (arms-fix rule).
        internal static Func<Renderer, bool> CoverageFilter;
        /// When set with CoverageFilter, a second coverage column (all station parts that pass this one) is written too.
        internal static Func<Renderer, bool> CoverageFilterAlt;
        /// Optional extra measurement per coverage sample (station, sequence time, is a capture time) and at the end
        /// (scene name); mount station art pass: arm joint gaps and close-ups. Null = nothing extra.
        internal static Action<WeaponMountStation, float, bool> SampleProbe;
        internal static Action<string> ProbeFlush;

        /// Station parts: renderers under the station plus anything its fields reference (clamps/rings created at
        /// runtime), except the cannon proxy, which is the player's weapon.
        static List<Renderer> StationParts(WeaponMountStation st)
        {
            var proxy = Field<GameObject>(st, "cannonProxy");
            var set = new HashSet<Renderer>(st.GetComponentsInChildren<Renderer>(true));
            foreach (var f in st.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
            {
                var v = f.GetValue(st);
                IEnumerable<object> items = v is IEnumerable e && !(v is string) ? e.Cast<object>() : new[] { v };
                foreach (var it in items)
                {
                    var go = it is GameObject g ? g : it is Component c ? c.gameObject : null;
                    // small station-owned pieces only (clamps, rings, pincers): never the player rig or a weapon
                    if (!go || go == proxy || go.GetComponentInParent<WeaponController>(true) || go.GetComponentInChildren<PlayerCharacterController>(true)
                        || go.GetComponentInChildren<ProtagonistHandsPresentation>(true) || go.GetComponentInChildren<Camera>(true)) continue;
                    var rs = go.GetComponentsInChildren<Renderer>(true); if (rs.Length > 8 && !go.transform.IsChildOf(st.transform)) continue;
                    foreach (var r in rs) set.Add(r);
                }
            }
            if (proxy) foreach (var r in proxy.GetComponentsInChildren<Renderer>(true)) set.Remove(r);
            set.RemoveWhere(r => !r || r.GetComponentInParent<WeaponController>(true) || (r.GetComponentInParent<ProtagonistHandsPresentation>(true) && !IsStationPiece(r.transform)));
            return set.Where(r => !(r is ParticleSystemRenderer)).ToList();
        }
        // pieces the station parents under the player's forearm are still station pieces (named by the station code)
        static bool IsStationPiece(Transform t)
        {
            for (var p = t; p; p = p.parent) { var n = p.name.ToLowerInvariant(); if (n.Contains("clamp") || n.Contains("station") || n.Contains("lockring") || n.Contains("pinza") || n.Contains("collar_st")) return true; }
            return false;
        }

        [MenuItem(Menu + "Station: run with captures (play)")]
        static void StationRun()
        {
            RequirePlay();
            station = Object.FindAnyObjectByType<WeaponMountStation>(); var player = Player;
            if (!station || !player) { Log("station or player missing in " + Where); return; }
            if (station.Granted) { Log("station already granted in " + Where); return; }
            var move = player.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint();
            player.TeleportToCheckpoint(station.transform.position - station.transform.forward * .5f + Vector3.up * .03f);
            player.transform.rotation = Quaternion.Euler(0, station.transform.eulerAngles.y, 0); player.PlayerCamera.transform.localRotation = Quaternion.identity; Physics.SyncTransforms();
            playerRenderersBefore = new HashSet<Renderer>(player.GetComponentsInChildren<Renderer>(true));
            stationCsv = new StringBuilder("t,coverage,wallHits,parts\n"); stationWallHits.Clear(); stationMaxCov = 0; stationMaxCovAt = "";
            stationShots.Clear(); stationShots.AddRange(new[] { .3f, .75f, 1.0f, 1.25f, 1.55f, 1.85f, 2.1f, 2.45f, 2.8f, 3.05f, 3.3f, 3.5f, 3.65f, 3.8f, 3.95f, 4.2f, 4.45f, 4.6f, 4.75f, 4.9f, 5.2f, 5.5f });
            stationShot = 0; stationLastT = -1; stationLastCov = -1;
            Log($"station run in {Where}: station={station.transform.position} yaw={station.transform.eulerAngles.y:0.0} parts={StationParts(station).Count}");
            Time.timeScale = .1f; station.Begin();
            EditorApplication.CallbackFunction tick = null; double timeout = EditorApplication.timeSinceStartup + 150;
            tick = () =>
            {
                if (!EditorApplication.isPlaying || !station || EditorApplication.timeSinceStartup > timeout) { EditorApplication.update -= tick; Time.timeScale = 1; StationFlush("aborted"); return; }
                if (station.Busy) StationSample();
                else if (stationLastT >= 0)
                {
                    EditorApplication.update -= tick; Time.timeScale = 1;
                    StationFinal();
                    StationFlush("done granted=" + station.Granted);
                }
            };
            EditorApplication.update += tick;
        }

        static void StationSample()
        {
            float t = station.SequenceTime; if (t == stationLastT) return;
            var cam = Player.PlayerCamera; var parts = StationParts(station);
            bool shot = stationShot < stationShots.Count && t >= stationShots[stationShot];
            if (shot || t - stationLastCov >= .05f)
            {
                float cov = Coverage(cam, CoverageFilter == null ? parts : parts.Where(CoverageFilter)); stationLastCov = t;
                if (cov > stationMaxCov) { stationMaxCov = cov; stationMaxCovAt = F(t); }
                float alt = CoverageFilterAlt == null ? -1 : Coverage(cam, parts.Where(CoverageFilterAlt));
                if (alt > stationMaxAlt) { stationMaxAlt = alt; stationMaxAltAt = F(t); }
                var hits = WallHits(parts, station.transform, Player.transform); foreach (var hh in hits) stationWallHits.Add(hh);
                stationCsv.AppendLine($"{F(t)},{F(cov)},{hits.Count},{string.Join(" ", hits.Distinct())}" + (alt >= 0 ? "," + F(alt) : ""));
                SampleProbe?.Invoke(station, t, shot);
            }
            while (stationShot < stationShots.Count && t >= stationShots[stationShot])
            {
                string n = $"{Where}_{stationShots[stationShot]:0.00}";
                Save(FpsShot(), "station", n + "_fps"); Save(Witness(), "station", n + "_witness"); stationShot++;
            }
            stationLastT = t;
        }

        /// Third-person view from behind/right of the player, so walls and the station arms read together.
        static Texture2D Witness()
        {
            var p = Player; var cam = TempCamera("ArmsFixWitness", p.PlayerCamera);
            try
            {
                var f = Vector3.ProjectOnPlane(p.PlayerCamera.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f);
                var eye = p.PlayerCamera.transform.position;
                // side view from whichever side has more room (walls stay in shot instead of hiding the camera)
                float Room(Vector3 dir) => Physics.Raycast(eye, dir, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 4f;
                var side = Room(r) >= Room(-r) ? r : -r; float d = Mathf.Clamp(Room(side) - .3f, .8f, 2.8f);
                cam.transform.position = eye + side * d - f * .4f + Vector3.up * .5f; cam.transform.LookAt(eye + f * 1.0f + Vector3.up * .2f);
                cam.fieldOfView = 70; cam.nearClipPlane = .05f; cam.cullingMask = ~0; cam.clearFlags = CameraClearFlags.Skybox;
                return RenderCam(cam, 960, 540);
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        static void StationFinal()
        {
            Save(FpsShot(), "station", Where + "_done_fps"); Save(Witness(), "station", Where + "_done_witness");
            var cam = Player.PlayerCamera.transform; var sb = new StringBuilder();
            foreach (var r in StationParts(station))
                if (r && r.enabled && r.gameObject.activeInHierarchy && Vector3.Distance(r.bounds.center, cam.position) < 1.6f)
                    sb.Append($" station-part:{Path(r.transform)}@{F(Vector3.Distance(r.bounds.center, cam.position))}m");
            foreach (var r in Player.GetComponentsInChildren<Renderer>(true))
                if (r && !playerRenderersBefore.Contains(r) && r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer))
                    sb.Append($" new-on-player:{Path(r.transform)}");
            Log($"station final ({Where}): leftover near camera/on player:{(sb.Length == 0 ? " none" : sb.ToString())}");
        }
        static string Path(Transform t) { var s = t.name; for (var p = t.parent; p && s.Length < 160; p = p.parent) s = p.name + "/" + s; return s; }

        static void StationFlush(string why)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText($"{Dir}/station-{Where}.csv", stationCsv?.ToString() ?? "");
            ProbeFlush?.Invoke(Where);
            Log($"station {why} in {Where}: maxCoverage={F(stationMaxCov)} at t={stationMaxCovAt}" + (stationMaxAlt >= 0 ? $" maxCoverageAlt={F(stationMaxAlt)} at t={stationMaxAltAt}" : "")
                + $" wallHits={(stationWallHits.Count == 0 ? "none" : string.Join(" | ", stationWallHits))}");
        }
    }
}
