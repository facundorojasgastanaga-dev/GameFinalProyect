using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// Mount station art pass (19/11/2026): helpers to fit the segmented gantry model to the five places the
    /// montage station lives in (RequiemCampaign lobby, Map01 campaign sessions S1-S4).
    /// "Measure clearance" opens each scene (never saves), casts rays in the station's own frame (x across the gate,
    /// y up, z into the station; the player stands at z = -0.55) and writes the free distance to the first
    /// non-station, non-trigger collider. Report: Tools/IronRequiem/Reports/MountStationArt/clearance.txt.
    public static class MountStationArtTools
    {
        internal const string Menu = "Tools/Iron Requiem/Mount Station Art/";
        internal const string ReportDir = "Tools/IronRequiem/Reports/MountStationArt";
        internal static readonly string[] Scenes =
        {
            "Assets/IronRequiem/Scenes/RequiemCampaign.unity",
            "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity",
            "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S2.unity",
            "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S3.unity",
            "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S4.unity",
        };
        static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        internal static void RequireCleanScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty) throw new InvalidOperationException("Open scene has unsaved changes: " + s.path);
            }
        }

        // ------------------------------------------------------------------ Play preview (nothing is saved)
        // Play a station scene (production station, dressed with the segmented model since 19/11), then run the arms-fix station
        // capture ("Tools/Iron Requiem/Arms Fix Pass/Station: run with captures (play)") with the tag below, so the
        // captures land in Reports/MountStationArt/<tag>/ and coverage counts moving parts only.
        [MenuItem(Menu + "Preview/Play RequiemCampaign")] static void PlayCampaign() => ArmsFixPassTools.PlayScene(Scenes[0]);
        [MenuItem(Menu + "Preview/Play Map01 S1")] static void PlayS1() => ArmsFixPassTools.PlayScene(Scenes[1]);
        [MenuItem(Menu + "Preview/Play Map01 S2")] static void PlayS2() => ArmsFixPassTools.PlayScene(Scenes[2]);
        [MenuItem(Menu + "Preview/Play Map01 S3")] static void PlayS3() => ArmsFixPassTools.PlayScene(Scenes[3]);
        [MenuItem(Menu + "Preview/Tag current (placeholder station)")] static void TagCurrent() => Tag("current");
        [MenuItem(Menu + "Preview/Tag segmented")] static void TagSegmented() => Tag("segmented");
        [MenuItem(Menu + "Preview/Tag production (dressed station)")] static void TagProduction() => Tag("production");
        static void Tag(string t)
        {
            EditorPrefs.SetString("IR_ArmsFixTag", "../MountStationArt/" + t);
            HookCoverage();
            Debug.Log("[MountStationArt] capture tag " + t + " (coverage: moving parts)");
        }
        // coverage of what moves (arms, clamps, rings, the hand proxy): the static gantry is not "the arm".
        // Re-hooked after every domain reload (entering Play reloads) while one of this pass's tags is active.
        [InitializeOnLoadMethod]
        static void HookCoverage()
        {
            if (!EditorPrefs.GetString("IR_ArmsFixTag", "").StartsWith("../MountStationArt/")) return;
            // main number: the arms and what moves with them (clamps, lock ring); the detached hand is the player's own
            // hand (a station-owned copy from TDetach on), reported in the second column with the arms
            static bool Moving(Renderer r) => r.GetComponentInParent<StationArmRig>(true) != null || r.GetComponentInParent<WeaponMountStation>(true) == null;
            static bool Hand(Renderer r) { for (var t = r.transform; t; t = t.parent) if (t.name == "HandProxy") return true; return false; }
            ArmsFixPassTools.CoverageFilter = r => Moving(r) && !Hand(r);
            ArmsFixPassTools.CoverageFilterAlt = Moving;
            MountStationArtProbe.Hook();
        }
        [MenuItem(Menu + "Preview/Restore arms-fix tag")]
        static void TagRestore() { EditorPrefs.SetString("IR_ArmsFixTag", "after"); ArmsFixPassTools.CoverageFilter = null; ArmsFixPassTools.CoverageFilterAlt = null; MountStationArtProbe.Unhook(); }

        /// Static fit of both stations in every scene, without instantiating anything: oriented boxes in the station's
        /// frame (the old placeholder posts/lintel, and the segmented posts/lintel/back-arm volumes read from the dressed
        /// production prefab) tested against solid scene colliders that are not the station's own. Never saves.
        [MenuItem(Menu + "Measure frame fit (5 scenes)")]
        static void MeasureFit()
        {
            RequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(MountStationArtBuilder.BasePrefab) ?? throw new InvalidOperationException("missing " + MountStationArtBuilder.BasePrefab);
            if (!variant.transform.Find("SA_Frame")) throw new InvalidOperationException("the production station is not dressed with the segmented model");
            // segmented volumes in station-local space: the refitted post colliders, plus each frame renderer's mesh box
            var boxes = new List<(string name, Vector3 c, Vector3 half, Quaternion r)>();
            foreach (var p in variant.GetComponentsInChildren<BoxCollider>(true).Where(b => !b.isTrigger))
            {
                var m = variant.transform.worldToLocalMatrix * p.transform.localToWorldMatrix;
                boxes.Add(("segmented:" + p.name, m.MultiplyPoint3x4(p.center), Vector3.Scale(p.size * .5f, m.lossyScale) * .95f, m.rotation));
            }
            foreach (var mr in variant.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.transform.IsChildOf(variant.transform.Find("SA_Frame"))))
            {
                var mesh = mr.GetComponent<MeshFilter>().sharedMesh; var m = variant.transform.worldToLocalMatrix * mr.transform.localToWorldMatrix;
                if (mr.name == "SA_Static") continue;                       // covered by the post boxes and the lintel box below
                boxes.Add(("segmented:" + mr.name, m.MultiplyPoint3x4(mesh.bounds.center), Vector3.Scale(mesh.bounds.extents, m.lossyScale) * .9f, m.rotation));
            }
            var lintel = variant.transform.Find("SA_Frame/SA_Static");
            var placeholder = new[]
            {
                ("placeholder:PostL", new Vector3(-2.3f, 1.7f, 0), new Vector3(.175f, 1.7f, .25f)), ("placeholder:PostR", new Vector3(2.3f, 1.7f, 0), new Vector3(.175f, 1.7f, .25f)),
                ("placeholder:Lintel", new Vector3(0, 3.55f, 0), new Vector3(2.5f, .175f, .275f)),
            };
            var sb = new StringBuilder("Mount station frame fit " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " (boxes shrunk 5-10%; '-' = clear)\n");
            try
            {
                foreach (var path in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var station = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WeaponMountStation>(true)).FirstOrDefault();
                    sb.Append("\n== ").Append(scene.name).Append('\n');
                    if (!station) { sb.Append("  NO STATION\n"); continue; }
                    var t = station.transform; Physics.SyncTransforms(); var buf = new Collider[32];
                    string Hits(Vector3 c, Vector3 half, Quaternion r)
                    {
                        int n = Physics.OverlapBoxNonAlloc(t.TransformPoint(c), half, buf, t.rotation * r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        var names = new List<string>();
                        for (int i = 0; i < n; i++) { var col = buf[i]; if (col.transform.IsChildOf(t) || col is CharacterController || col.GetComponentInParent<CharacterController>()) continue; names.Add(col.name); }
                        return names.Count == 0 ? "-" : string.Join(", ", names.Distinct());
                    }
                    foreach (var (name, c, half) in placeholder) sb.Append("  ").Append(name.PadRight(28)).Append(Hits(c, half, Quaternion.identity)).Append('\n');
                    foreach (var b in boxes) sb.Append("  ").Append(b.name.PadRight(28)).Append(Hits(b.c, b.half, b.r)).Append('\n');
                    // lintel of the segmented frame: the top band of the static mesh
                    if (lintel)
                    {
                        var mesh = lintel.GetComponent<MeshFilter>().sharedMesh; var m = variant.transform.worldToLocalMatrix * lintel.localToWorldMatrix;
                        var lo = m.MultiplyPoint3x4(mesh.bounds.min); var hi = m.MultiplyPoint3x4(mesh.bounds.max);
                        Vector3 mn = Vector3.Min(lo, hi), mx = Vector3.Max(lo, hi);
                        var c = new Vector3((mn.x + mx.x) * .5f, (2.55f + mx.y) * .5f, (mn.z + mx.z) * .5f);
                        var half = new Vector3((mx.x - mn.x) * .5f, (mx.y - 2.55f) * .5f, (mx.z - mn.z) * .5f) * .95f;
                        sb.Append("  ").Append("segmented:lintel band".PadRight(28)).Append(Hits(c, half, Quaternion.identity)).Append('\n');
                    }
                }
            }
            finally
            {
                File.WriteAllText(ReportDir + "/frame-fit.txt", sb.ToString());
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Debug.Log("[MountStationArt] frame fit written");
            }
        }

        [MenuItem(Menu + "Measure clearance (5 scenes)")]
        static void MeasureClearance()
        {
            RequireCleanScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(ReportDir);
            var sb = new StringBuilder("Mount station clearance " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n"
                + "frame: station local (x across, y up, z into the station). '-' = nothing within 8 m.\n");
            try
            {
                foreach (var path in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var station = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WeaponMountStation>(true)).FirstOrDefault();
                    sb.Append("\n== ").Append(scene.name);
                    if (!station) { sb.Append(": NO STATION\n"); continue; }
                    var t = station.transform;
                    sb.Append("  station '").Append(t.name).Append("' pos ").Append(t.position.ToString("F2")).Append(" yaw ").Append(F(t.eulerAngles.y)).Append(" lossyScale ").Append(t.lossyScale.ToString("F2")).Append('\n');
                    Physics.SyncTransforms();
                    float Ray(Vector3 local, Vector3 dirLocal)
                    {
                        var o = t.TransformPoint(local); var d = t.TransformDirection(dirLocal).normalized;
                        var hits = Physics.RaycastAll(o, d, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        float best = float.PositiveInfinity;
                        foreach (var h in hits)
                        {
                            if (h.collider.transform.IsChildOf(t)) continue;
                            if (h.collider is CharacterController || h.collider.GetComponentInParent<CharacterController>()) continue;
                            if (h.distance < best) best = h.distance;
                        }
                        return best;
                    }
                    string D(float v) => float.IsInfinity(v) ? "  -  " : F(v).PadLeft(5);
                    sb.Append("floor below (0,1,0): ").Append(D(Ray(new Vector3(0, 1, 0), Vector3.down))).Append('\n');
                    sb.Append("lateral -X | +X (from x=0) per height y and depth z\n      z:");
                    var zs = new[] { -1.5f, -1f, -.5f, 0f, .5f, 1f, 1.5f, 2f, 2.5f };
                    foreach (var z in zs) sb.Append(F(z).PadLeft(12));
                    sb.Append('\n');
                    foreach (var y in new[] { .3f, 1.2f, 2.2f, 3.2f, 4.0f, 4.6f })
                    {
                        sb.Append("y=").Append(F(y)).Append(' ');
                        foreach (var z in zs) sb.Append(" ").Append(D(Ray(new Vector3(0, y, z), Vector3.left))).Append('|').Append(D(Ray(new Vector3(0, y, z), Vector3.right)));
                        sb.Append('\n');
                    }
                    sb.Append("depth: back (+z from z=0) | front (-z from z=0) per x and y\n");
                    foreach (var y in new[] { .3f, 1.5f, 3.0f, 4.0f })
                    {
                        sb.Append("y=").Append(F(y)).Append(' ');
                        foreach (var x in new[] { -2f, -1f, 0f, 1f, 2f })
                            sb.Append(" x").Append(F(x)).Append(':').Append(D(Ray(new Vector3(x, y, 0), Vector3.forward))).Append('|').Append(D(Ray(new Vector3(x, y, 0), Vector3.back)));
                        sb.Append('\n');
                    }
                    sb.Append("ceiling (up from y=0.2) per x,z:");
                    foreach (var x in new[] { -2f, 0f, 2f })
                        foreach (var z in new[] { -.5f, .5f, 1.5f })
                            sb.Append(" (").Append(F(x)).Append(',').Append(F(z)).Append(")=").Append(D(Ray(new Vector3(x, .2f, z), Vector3.up)));
                    sb.Append('\n');
                }
            }
            finally
            {
                File.WriteAllText(ReportDir + "/clearance.txt", sb.ToString());
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Debug.Log("[MountStationArt] clearance written");
            }
        }
    }

    /// Mount station art (19/11/2026, refinement): measures, during an arms-fix station capture run, how each arm's
    /// model segments stay joined (smallest vertex distance link-tool and tool-claw), the wrist bend (tool axis vs claw
    /// axis) and the claw roll relative to the tool (angle, about the claw axis, between where the tool comes from and
    /// where the claw head expects it: the model's own rest relation). At every capture time it also renders two
    /// close-ups of arm A (zoomed first-person view, and a side view from the player's left) into the capture folder.
    /// Hooked with the "Tag ..." menus of MountStationArtTools; writes station-probe-<scene>.csv next to the capture
    /// (read-only: it never changes the station, the arms or the scene).
    static class MountStationArtProbe
    {
        static StringBuilder csv;
        static readonly Dictionary<Mesh, Vector3[]> Points = new Dictionary<Mesh, Vector3[]>();
        static readonly Dictionary<string, float> WorstGap = new Dictionary<string, float>(), WorstRoll = new Dictionary<string, float>();
        // claw frame -> direction toward the tool (claw-local, perpendicular to the claw axis), from the model
        static readonly Dictionary<string, Vector3> ToolSide = new Dictionary<string, Vector3>();

        internal static void Hook() { ArmsFixPassTools.SampleProbe = Sample; ArmsFixPassTools.ProbeFlush = Flush; csv = null; }
        internal static void Unhook() { ArmsFixPassTools.SampleProbe = null; ArmsFixPassTools.ProbeFlush = null; csv = null; }

        static Transform Find(Transform root, string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        static Transform Field(StationArmRig rig, string f) => (Transform)new SerializedObject(rig).FindProperty(f).objectReferenceValue;

        // local vertices of a mesh, thinned to at most ~700
        static Vector3[] Pts(Mesh m)
        {
            if (Points.TryGetValue(m, out var p)) return p;
            var v = m.vertices; int step = Mathf.Max(1, v.Length / 700);
            p = Enumerable.Range(0, v.Length / step).Select(i => v[i * step]).ToArray(); Points[m] = p; return p;
        }
        static List<Vector3> World(params Transform[] parts)
        {
            var list = new List<Vector3>();
            foreach (var t in parts)
            {
                if (!t) continue;
                var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                if (!mf || !mf.sharedMesh || !mr || !mr.enabled || !t.gameObject.activeInHierarchy) continue;
                var m = t.localToWorldMatrix; foreach (var q in Pts(mf.sharedMesh)) list.Add(m.MultiplyPoint3x4(q));
            }
            return list;
        }
        static float Gap(List<Vector3> a, List<Vector3> b)
        {
            if (a.Count == 0 || b.Count == 0) return -1;
            float best = float.MaxValue;
            foreach (var p in a) foreach (var q in b) { float d = (p - q).sqrMagnitude; if (d < best) best = d; }
            return Mathf.Sqrt(best);
        }

        // the model's tool side in the claw frame: the frame the builder maps onto the gripper (z = claw axis top->tip,
        // x = jaw separation ClawN->ClawP)
        static Vector3 ModelToolSide(string rigName)
        {
            if (ToolSide.TryGetValue(rigName, out var s)) return s;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MountStationArtBuilder.Fbx).transform;
            var station = AssetDatabase.LoadAssetAtPath<GameObject>(MountStationArtBuilder.BasePrefab);
            // which model side the rig took: the builder logs it, but the sign of the shoulder x decides it the same way
            var arm = station.transform.Find(rigName); bool plusX = arm.localPosition.x > 0;
            // model x is mirrored on import and the station turns it 180 degrees: FR lands at +x
            string side = plusX ? "FR" : "FL";
            Vector3 P(string n) { var t = model.Find(side + n); return model.InverseTransformPoint(t.position); }
            Vector3 C(string n) { var t = model.Find(side + n); var mf = t.GetComponent<MeshFilter>(); return model.InverseTransformPoint(t.TransformPoint(mf.sharedMesh.bounds.center)); }
            var zc = (P("_P5") - P("_P6")).normalized; var xc = Vector3.ProjectOnPlane(C("_ClawP") - C("_ClawN"), zc).normalized;
            var claw = Quaternion.LookRotation(zc, Vector3.Cross(zc, xc));
            var back = (P("_P2") - P("_P3")).normalized;                   // from the wrist toward the elbow, along the tool
            s = Quaternion.Inverse(claw) * back; s.z = 0; s = s.normalized; ToolSide[rigName] = s;
            return s;
        }

        static void Sample(WeaponMountStation station, float t, bool shot)
        {
            if (csv == null)
            {
                csv = new StringBuilder("t,arm,gapLinkTool,gapToolClaw,wristBend,clawRollVsTool\n");
                WorstGap.Clear(); WorstRoll.Clear();
            }
            foreach (var rig in station.GetComponentsInChildren<StationArmRig>(true))
            {
                var root = rig.transform; var fore = Field(rig, "fore"); var grip = Field(rig, "gripper");
                var link = World(Find(root, "SA_Link")); var tool = World(Find(root, "SA_Tool")); var claw = World(Find(root, "SA_ClawN"), Find(root, "SA_ClawP"));
                float g1 = Gap(link, tool), g2 = Gap(tool, claw);
                float bend = Vector3.Angle(fore.forward, grip.forward);
                // where the tool comes from, seen from the claw (claw-local, about its axis) vs the model's own relation
                var b = grip.InverseTransformDirection(-fore.forward); b.z = 0; float roll = 0;
                var want = ModelToolSide(rig.name);
                if (b.sqrMagnitude > 1e-4f) roll = Vector2.SignedAngle(new Vector2(want.x, want.y), new Vector2(b.x, b.y));
                csv.AppendLine($"{ArmsFixPassTools.F(t)},{rig.name},{ArmsFixPassTools.F(g1)},{ArmsFixPassTools.F(g2)},{bend:0.0},{roll:0.0}");
                // worst over the part of the run where the arm is out of its rest (hand arm 0.55-4.3 s, cannon arm 1.85-4.4 s)
                bool working = rig.name.Contains("Hand") ? t > .6f && t < 4.3f : t > 1.9f && t < 4.4f;
                if (working)
                {
                    WorstGap[rig.name] = Mathf.Max(WorstGap.TryGetValue(rig.name, out var w) ? w : 0, g2);
                    WorstRoll[rig.name] = Mathf.Max(WorstRoll.TryGetValue(rig.name, out var r) ? r : 0, Mathf.Abs(roll));
                }
            }
            if (shot) CloseUps(station, t);
        }

        static void CloseUps(WeaponMountStation station, float t)
        {
            var rig = station.GetComponentsInChildren<StationArmRig>(true).FirstOrDefault(r => r.name.Contains("Hand"));
            var player = ArmsFixPassTools.Player;
            if (!rig || !player) return;
            var pc = player.PlayerCamera; var grip = Field(rig, "gripper"); var fore = Field(rig, "fore");
            var focus = (grip.position + fore.position) * .5f;
            string n = $"{station.gameObject.scene.name}_{t:0.00}";
            var cam = ArmsFixPassTools.TempCamera("MSAProbeZoom", pc);
            try
            {
                cam.transform.SetPositionAndRotation(pc.transform.position, Quaternion.LookRotation(focus - pc.transform.position, Vector3.up));
                cam.fieldOfView = 32; cam.nearClipPlane = .03f;
                ArmsFixPassTools.Save(ArmsFixPassTools.RenderCam(cam, 960, 540), "station", n + "_clawA_zoom");
                // side view: from the player's left, a little above, looking across at the claw and the arm
                var f = Vector3.ProjectOnPlane(pc.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f);
                cam.transform.position = grip.position - r * 1.15f + Vector3.up * .35f + f * .15f;
                cam.transform.LookAt((grip.position + rig.Elbow) * .5f);
                cam.fieldOfView = 50; cam.cullingMask = ~0; cam.clearFlags = CameraClearFlags.Skybox;
                ArmsFixPassTools.Save(ArmsFixPassTools.RenderCam(cam, 960, 540), "station", n + "_clawA_side");
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        static void Flush(string scene)
        {
            if (csv == null) return;
            Directory.CreateDirectory(ArmsFixPassTools.Dir);
            File.WriteAllText($"{ArmsFixPassTools.Dir}/station-probe-{scene}.csv", csv.ToString());
            ArmsFixPassTools.Log($"probe {scene}: worst tool-claw gap " + string.Join(" ", WorstGap.Select(k => $"{k.Key}={ArmsFixPassTools.F(k.Value)}m"))
                + "; worst claw roll vs tool " + string.Join(" ", WorstRoll.Select(k => $"{k.Key}={k.Value:0}deg")));
            csv = null;
        }
    }
}
