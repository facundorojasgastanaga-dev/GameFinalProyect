using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// v35 read-only level audit. Opens RequiemCampaign with every stage scene additively (edit mode, nothing saved) and
    /// reports per room: renderers, triangles, static share, colliders, lights (mode/shadows), probes, LOD groups, NavMesh
    /// surfaces, bounds, ceiling clearance and interior walls; per airlock joint: floor heights sampled across the joint
    /// (steps, gaps); and renders 1920x1080 captures from each room entrance and from a high corner.
    /// Report: Tools/IronRequiem/Reports/Levels/audit_<tag>.txt · captures: Tools/IronRequiem/Bridge/v35/<tag>/
    public static class LevelAudit
    {
        const string Session = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        static string Tag => PlayerPrefs.GetString("IR_TourTag", "run");

        [MenuItem("Tools/Iron Requiem/Levels/Audit + captures (read only)")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // reuse the session scene if it is already open (even modified: it is never saved here); room scenes are opened
            // additively and closed again without saving
            var opened = new List<Scene>(); Scene active = SceneManager.GetActiveScene();
            var sb = new StringBuilder($"Level audit {System.DateTime.Now:yyyy-MM-dd HH:mm} tag={Tag}\n");
            try
            {
                var session = SceneManager.GetSceneByPath(Session);
                if (!session.isLoaded)
                {
                    if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) { Debug.LogError("[LevelAudit] hay escenas modificadas abiertas y RequiemCampaign no está abierta"); return; }
                    session = EditorSceneManager.OpenScene(Session, OpenSceneMode.Additive); opened.Add(session);
                }
                var flow = session.GetRootGameObjects().Select(g => g.GetComponentInChildren<CampaignFlow>(true)).First(f => f);
                var stages = flow.StageScenes; var rooms = new List<Scene>();
                foreach (var name in stages)
                {
                    var path = EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name) ?? AssetDatabase.FindAssets(name + " t:Scene").Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileNameWithoutExtension(p) == name);
                    var already = SceneManager.GetSceneByPath(path);
                    if (already.isLoaded) rooms.Add(already); else { var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); rooms.Add(sc); opened.Add(sc); }
                }
                Physics.SyncTransforms();
                sb.AppendLine("== Session " + session.name + ": airlocks " + flow.Airlocks.Length);
                Room(sb, session);
                foreach (var r in rooms) Room(sb, r);
                sb.AppendLine("\n== Uniones (alturas de piso alrededor de cada esclusa; paso = salto vertical entre muestras vecinas de 0,5 m)");
                for (int i = 0; i < flow.Airlocks.Length; i++) Joint(sb, i, flow.Airlocks[i], stages);
                var dir = "Tools/IronRequiem/Bridge/v35/" + Tag; Directory.CreateDirectory(dir);
                for (int i = 0; i < rooms.Count; i++) Captures(rooms[i], i == 0 ? null : flow.Airlocks[i - 1], dir);
            }
            finally
            {
                File.WriteAllText(Directory.CreateDirectory("Tools/IronRequiem/Reports/Levels").FullName + "/audit_" + Tag + ".txt", sb.ToString());
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                foreach (var sc in opened) if (sc.isLoaded) EditorSceneManager.CloseScene(sc, true);
                Debug.Log("[LevelAudit] listo");
            }
        }

        // every renderer/collider object of the level scenes with its transform, mesh, material and bounds
        [MenuItem("Tools/Iron Requiem/Levels/Dump geometry (read only)")]
        static void Dump()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var sb = new StringBuilder(); var opened = new List<Scene>();
            try
            {
                foreach (var path in EditorBuildSettings.scenes.Select(s => s.path).Where(p => p.Contains("/Campaign/") || p.EndsWith("RequiemCampaign.unity")))
                {
                    var sc = SceneManager.GetSceneByPath(path); if (!sc.isLoaded) { sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened.Add(sc); }
                    sb.AppendLine("##### " + sc.name);
                    foreach (var root in sc.GetRootGameObjects())
                    {
                        if (root.name.StartsWith("Player") || root.name.Contains("HUD") || root.name.Contains("Menu") || root.name == "GameManager" || root.name == "Scavengers") continue;
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        {
                            var r = t.GetComponent<Renderer>(); var c = t.GetComponent<Collider>(); var l = t.GetComponent<Light>();
                            if (!r && !c && !l && t != root.transform) continue;
                            var mf = t.GetComponent<MeshFilter>(); string bounds = r ? $" b={r.bounds.center:F1}/{r.bounds.size:F1}" : c ? $" cb={c.bounds.center:F1}/{c.bounds.size:F1}" : "";
                            sb.AppendLine($"{NodePath(t)}{(t.gameObject.activeInHierarchy ? "" : " [off]")} p={t.position:F1} e={t.eulerAngles:F0} s={t.lossyScale:F2}{(mf && mf.sharedMesh ? " mesh=" + mf.sharedMesh.name : "")}{(r && r.sharedMaterial ? " mat=" + r.sharedMaterial.name : "")}{(c ? " col=" + c.GetType().Name + (c.isTrigger ? "(trigger)" : "") : "")}{(l ? $" light={l.type}/{l.lightmapBakeType}/r{l.range:0}/i{l.intensity:0.0}" : "")}{bounds} L={LayerMask.LayerToName(t.gameObject.layer)} st={(int)GameObjectUtility.GetStaticEditorFlags(t.gameObject)}");
                        }
                    }
                }
            }
            finally
            {
                File.WriteAllText(Directory.CreateDirectory("Tools/IronRequiem/Reports/Levels").FullName + "/geometry_dump.txt", sb.ToString());
                foreach (var sc in opened) if (sc.isLoaded) EditorSceneManager.CloseScene(sc, true);
                Debug.Log("[LevelAudit] dump listo");
            }
        }
        static string NodePath(Transform t) { var s = t.name; for (var p = t.parent; p; p = p.parent) s = p.name + "/" + s; return s; }

        static void Room(StringBuilder sb, Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var rends = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            long tris = 0; foreach (var r in rends) { var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : r is SkinnedMeshRenderer s ? s.sharedMesh : null; if (m) tris += m.triangles.LongLength / 3; }
            int stat = rends.Count(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic));
            int occ = rends.Count(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.OccluderStatic));
            var cols = roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Where(c => c.enabled && c.gameObject.activeInHierarchy).ToArray();
            var lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).ToArray();
            var probes = roots.SelectMany(r => r.GetComponentsInChildren<ReflectionProbe>(true)).ToArray();
            var lods = roots.SelectMany(r => r.GetComponentsInChildren<LODGroup>(true)).Count();
            var mats = rends.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().Count();
            var navs = roots.SelectMany(r => r.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).ToArray();
            var audio = roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)).ToArray();
            Bounds b = default; bool any = false; foreach (var r in rends) { if (r is ParticleSystemRenderer) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            sb.AppendLine($"\n== {scene.name}: roots={roots.Length} renderers={rends.Length} tris={tris / 1000f:0.0}k batchingStatic={stat} occluderStatic={occ} materials={mats} colliders={cols.Length} (mesh={cols.Count(c => c is MeshCollider)}) lights={lights.Length} [{string.Join(", ", lights.GroupBy(l => l.type + "/" + l.lightmapBakeType + "/" + l.shadows).Select(g => g.Key + "×" + g.Count()))}] reflectionProbes={probes.Length} [{string.Join(",", probes.Select(p => p.mode.ToString()).Distinct())}] LODGroups={lods} navSurfaces={navs.Length} (data={navs.Count(n => n.navMeshData)}) audio={audio.Length} (streaming? see import)");
            sb.AppendLine($"   bounds center={b.center:F1} size={b.size:F1}  lightmaps={(Lightmapping.lightingDataAsset ? "sí" : "?")}");
            foreach (var r in roots) sb.AppendLine($"   root {r.name} active={r.activeSelf} children={r.GetComponentsInChildren<Transform>(true).Length - 1} renderers={r.GetComponentsInChildren<Renderer>(true).Length} colliders={r.GetComponentsInChildren<Collider>(true).Length}");
            // play space: sample floor + ceiling over the room footprint
            if (!any) return;
            var floors = new List<float>(); var ceilings = new List<float>(); int blocked = 0, samples = 0;
            for (float x = b.min.x + 1; x < b.max.x - 1; x += 2) for (float z = b.min.z + 1; z < b.max.z - 1; z += 2)
            {
                var top = new Vector3(x, b.max.y + 1, z);
                if (!Physics.Raycast(top, Vector3.down, out var floor, b.size.y + 3, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (floor.collider.gameObject.scene != scene) continue; samples++;
                // walkable floor: something below a free 2 m column
                if (Physics.Raycast(floor.point + Vector3.up * .05f, Vector3.up, out var ceil, 40, ~0, QueryTriggerInteraction.Ignore)) { if (ceil.distance < 2.2f) { blocked++; continue; } ceilings.Add(ceil.distance); }
                floors.Add(floor.point.y);
            }
            if (floors.Count > 0)
            {
                var levels = floors.GroupBy(y => Mathf.Round(y * 4) / 4).OrderByDescending(g => g.Count()).Take(6).Select(g => $"y={g.Key:0.00}×{g.Count()}");
                ceilings.Sort(); string Pct(float p) => ceilings.Count > 0 ? ceilings[(int)(ceilings.Count * p)].ToString("0.0") : "-";
                sb.AppendLine($"   piso (muestras cada 2 m): {floors.Count}/{samples} libres, {blocked} bajo obstáculo <2,2 m · niveles: {string.Join(" ", levels)} · techo libre p10/p50/p90 = {Pct(.1f)}/{Pct(.5f)}/{Pct(.9f)} m");
            }
        }

        static void Joint(StringBuilder sb, int index, CampaignAirlock airlock, string[] stages)
        {
            var box = airlock.GetComponent<Collider>().bounds; var c = box.center; float top = box.max.y + 2;
            // the long axis of the airlock volume is the walking direction
            // walking direction = from the source door to the destination door
            var walk = Vector3.ProjectOnPlane(airlock.DestinationDoor.transform.position - airlock.SourceDoor.transform.position, Vector3.up);
            Vector3 axis = walk.sqrMagnitude > .01f ? walk.normalized : (box.size.x > box.size.z ? Vector3.right : Vector3.forward), side = Vector3.Cross(Vector3.up, axis);
            float reach = walk.magnitude * .5f + 6;
            var line = new StringBuilder(); float? prev = null; float worst = 0; var stepsAt = new List<string>(); string lastScene = null; var sceneChanges = new List<string>();
            for (float t = -reach; t <= reach + .01f; t += .5f)
            {
                var p = c + axis * t; float best = float.NaN; string owner = "-", mat = "-";
                foreach (float s in new[] { -.6f, 0f, .6f })
                {
                    if (!Physics.Raycast(new Vector3(p.x, top, p.z) + side * s, Vector3.down, out var hit, top - box.min.y + 3, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (float.IsNaN(best) || hit.point.y > best) { best = hit.point.y; owner = hit.collider.gameObject.scene.name; var r = hit.collider.GetComponent<Renderer>(); mat = r && r.sharedMaterial ? r.sharedMaterial.name : hit.collider.name; }
                }
                line.Append(float.IsNaN(best) ? " ·" : $" {best:0.00}");
                if (owner != lastScene) { sceneChanges.Add($"{t:0.0}m→{owner}({mat})"); lastScene = owner; }
                if (prev.HasValue && !float.IsNaN(best)) { float d = Mathf.Abs(best - prev.Value); if (d > .015f && d < 1f) { stepsAt.Add($"{t:0.0}m Δ{d * 100:0.0}cm"); worst = Mathf.Max(worst, d); } }
                if (!float.IsNaN(best)) prev = best;
            }
            sb.AppendLine($"-- esclusa {index} ({stages[index]} → {stages[index + 1]}) centro={c:F1} eje={axis:F2} · peor escalón {worst * 100:0.0} cm · escalones: {(stepsAt.Count == 0 ? "ninguno" : string.Join(", ", stepsAt))}");
            sb.AppendLine("   alturas:" + line);
            sb.AppendLine("   escena/material del piso: " + string.Join(" | ", sceneChanges));
        }

        static void Captures(Scene room, CampaignAirlock entry, string dir)
        {
            var rends = room.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>()).Where(r => !(r is ParticleSystemRenderer)).ToArray(); if (rends.Length == 0) return;
            Bounds b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            SceneManager.SetActiveScene(room);
            var go = new GameObject("AuditCam") { hideFlags = HideFlags.HideAndDontSave }; var cam = go.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 75; cam.nearClipPlane = .05f; cam.farClipPlane = 600;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rt = new RenderTexture(1920, 1080, 24); var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); var prev = RenderTexture.active;
            try
            {
                Vector3 entrance; Vector3 look;
                if (entry) { var e = entry.GetComponent<Collider>().bounds; entrance = new Vector3(e.center.x, e.min.y + 1.7f, e.center.z); look = (new Vector3(b.center.x, entrance.y, b.center.z) - entrance).normalized; entrance += look * (Mathf.Max(e.extents.x, e.extents.z) + 1.5f); }
                else { entrance = new Vector3(b.center.x, b.min.y + 1.7f, b.min.z + 3); look = Vector3.forward; }
                Shot(cam, rt, tex, entrance, look, $"{dir}/{room.name}_entrada.png");
                var high = new Vector3(b.min.x + b.size.x * .1f, b.min.y + Mathf.Min(b.size.y * .8f, 14f), b.min.z + b.size.z * .1f);
                Shot(cam, rt, tex, high, (b.center - high).normalized, $"{dir}/{room.name}_alto.png");
            }
            finally { RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); }
        }
        static void Shot(Camera cam, RenderTexture rt, Texture2D tex, Vector3 pos, Vector3 dir, string file)
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir)); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply(); File.WriteAllBytes(file, tex.EncodeToPNG()); cam.targetTexture = null;
        }
    }
}
