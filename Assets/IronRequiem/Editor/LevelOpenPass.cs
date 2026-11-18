using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// v35 level pass (14/11/2026): opens up and dresses the campaign rooms, keeping the existing layout, GUIDs and systems.
    /// Per room: ceiling raised (roof moved up, lamps follow as its children; new wall/column extensions with world-scaled UVs,
    /// headers over the door openings), side catwalks with ramps (simple box colliders), larger covers, spawn slots and
    /// pickups moved out of the new footprints, identity dressing on walls and ceiling (no colliders, static batching, no
    /// shadows), sector light tint (same 4 lights and intensity), painted floor chevrons that point to the exit, emissive
    /// frame on the exit, NavMesh rebuilt into the existing asset and the sector reflection probe re-baked.
    /// Session: invisible ramp collider over the stair between Room01 and Room02 (smooth sprint/slide, the steps stay visible).
    /// Nothing is deleted: replaced objects go to an inactive "Quarantine_v35" root in their scene.
    /// Scenes are copied to Tools/IronRequiem/Backups/20261113-v35-levels-perf/scenes first.
    public static class LevelOpenPass
    {
        const string Folder = "Assets/IronRequiem/Scenes/Campaign/";
        const string Session = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string MeshDir = "Assets/IronRequiem/Art/Levels/Campaign/V35";
        const string Backup = "Tools/IronRequiem/Backups/20261113-v35-levels-perf/scenes";
        const string Report = "Tools/IronRequiem/Reports/Levels/open_pass.txt";
        static readonly Dictionary<Vector3, Mesh> cubes = new Dictionary<Vector3, Mesh>();
        static StringBuilder log;
        static Material wall, ceiling, metal, dark, hazard, lava, cold, guide, cyan, orange, window;

        enum Identity { Foundry, Breach, Assembly, Reactor }
        struct Deck { public float x0, x1, z0, z1, rampLowZ; }
        class Spec
        {
            public string scene, geometry; public Identity identity; public float floorY, oldCeilBottom, newCeilBottom, zMin, zMax;
            public Deck[] decks; public Vector3[] headers, headerSizes; public Vector2[] path; public Vector3 exitCenter; public bool exitOnX;
            public Color light; public Vector3[] pickups;
        }

        static Spec[] Specs => new[]
        {
            new Spec { scene = "Room01_Foundry", geometry = "TwoFloorsGeometry", identity = Identity.Foundry, floorY = 0, oldCeilBottom = 7f, newCeilBottom = 12f, zMin = -34, zMax = 31.5f,
                decks = new[] { new Deck { x0 = -26.5f, x1 = -21f, z0 = -12f, z1 = 14f, rampLowZ = -23f } },
                headers = new[] { new Vector3(27, 9.75f, 25) }, headerSizes = new[] { new Vector3(1, 4.5f, 12) },
                path = new[] { new Vector2(0, -22), new Vector2(0, 8), new Vector2(14, 22), new Vector2(23, 25) }, exitCenter = new Vector3(27, 0, 25), exitOnX = true,
                light = new Color(1f, .66f, .42f), pickups = new[] { new Vector3(-23.75f, 0, 6) } },
            new Spec { scene = "Room02_Breach", geometry = "TwoFloorsGeometry", identity = Identity.Breach, floorY = 8, oldCeilBottom = 16f, newCeilBottom = 21f, zMin = -34, zMax = 27,
                decks = new[] { new Deck { x0 = -26.5f, x1 = -21f, z0 = -26f, z1 = -15f, rampLowZ = -4.5f }, new Deck { x0 = -26.5f, x1 = -21f, z0 = 13f, z1 = 25f, rampLowZ = 3.5f }, new Deck { x0 = 21f, x1 = 26.5f, z0 = 13f, z1 = 25f, rampLowZ = 3.5f } },
                headers = new[] { new Vector3(27, 18.5f, -10), new Vector3(0, 18.5f, 27) }, headerSizes = new[] { new Vector3(1, 5, 8), new Vector3(10, 5, 1) },
                path = new[] { new Vector2(23, -10), new Vector2(8, -10), new Vector2(0, -5), new Vector2(0, 6), new Vector2(0, 24) }, exitCenter = new Vector3(0, 8, 27),
                light = new Color(.74f, .84f, 1f), pickups = new[] { new Vector3(-23.75f, 8, -21), new Vector3(23.75f, 8, 19) } },
            new Spec { scene = "Room03_Assembly", geometry = "AssemblyGeometry", identity = Identity.Assembly, floorY = 8, oldCeilBottom = 16f, newCeilBottom = 21f, zMin = 39, zMax = 91,
                decks = new[] { new Deck { x0 = -26.5f, x1 = -21f, z0 = 67f, z1 = 86f, rampLowZ = 56f }, new Deck { x0 = 21f, x1 = 26.5f, z0 = 67f, z1 = 86f, rampLowZ = 56f } },
                headers = new[] { new Vector3(0, 18.5f, 39), new Vector3(0, 18.5f, 91) }, headerSizes = new[] { new Vector3(10, 5, 1), new Vector3(10, 5, 1) },
                path = new[] { new Vector2(0, 42), new Vector2(0, 88) }, exitCenter = new Vector3(0, 8, 91),
                light = new Color(1f, .93f, .78f), pickups = new[] { new Vector3(-23.75f, 8, 80) } },
            new Spec { scene = "Room04_Reactor", geometry = "ReactorGeometry", identity = Identity.Reactor, floorY = 8, oldCeilBottom = 16f, newCeilBottom = 21f, zMin = 103, zMax = 155,
                decks = new[] { new Deck { x0 = -26.5f, x1 = -21f, z0 = 131f, z1 = 150f, rampLowZ = 120f }, new Deck { x0 = 21f, x1 = 26.5f, z0 = 131f, z1 = 150f, rampLowZ = 120f } },
                headers = new[] { new Vector3(0, 18.5f, 103), new Vector3(0, 18.5f, 155) }, headerSizes = new[] { new Vector3(10, 5, 1), new Vector3(10, 5, 1) },
                path = new[] { new Vector2(0, 106), new Vector2(0, 118), new Vector2(-8, 129), new Vector2(0, 140), new Vector2(0, 152) }, exitCenter = new Vector3(0, 8, 155),
                light = new Color(.55f, .95f, 1f), pickups = new[] { new Vector3(23.75f, 8, 144) } },
        };

        [MenuItem("Tools/Iron Requiem/Levels/v35 Open + dress rooms (writes scenes)")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            log = new StringBuilder($"v35 level pass {System.DateTime.Now:yyyy-MM-dd HH:mm}\n");
            Directory.CreateDirectory(Backup);
            foreach (var s in Specs) BackupScene(Folder + s.scene + ".unity");
            BackupScene(Folder + "Room00b_AlarmCorridor.unity"); BackupScene(Session);
            LoadMaterials();
            // leave whatever is open (its unsaved changes are not ours) without saving it
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                foreach (var s in Specs) Room(s);
                Corridor();
                SessionRamp();
            }
            catch (System.Exception e) { log.AppendLine("ERROR " + e); Debug.LogException(e); }
            finally
            {
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, log.ToString());
                EditorSceneManager.OpenScene(Session, OpenSceneMode.Single);
                Debug.Log("[LevelOpenPass] listo → " + Report);
            }
        }

        // re-creates only the floor guide (chevrons + exit frame) and refreshes the v35 emissive materials
        [MenuItem("Tools/Iron Requiem/Levels/v35 Rebuild floor guide (writes scenes)")]
        static void RebuildGuide()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            log = new StringBuilder($"v35 floor guide rebuild {System.DateTime.Now:HH:mm}\n"); LoadMaterials();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var s in Specs)
            {
                var scene = EditorSceneManager.OpenScene(Folder + s.scene + ".unity", OpenSceneMode.Single);
                var footprints = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>()).Where(r => r.name == "Catwalk_V35" || r.name == "Ramp_V35").Select(r => { var b = r.bounds; b.Expand(new Vector3(0, 4, 0)); return b; }).ToList();
                var root = Root(scene, "V35_FloorGuide"); Physics.SyncTransforms();
                int n = Arrows(root, s.path, s.floorY, footprints); ExitFrame(root, s); log.AppendLine($"{s.scene}: flechas {n}");
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            var corridor = EditorSceneManager.OpenScene(Folder + "Room00b_AlarmCorridor.unity", OpenSceneMode.Single);
            var croot = Root(corridor, "V35_FloorGuide"); Physics.SyncTransforms();
            float y = Physics.Raycast(new Vector3(0, 6, -52), Vector3.down, out var hit, 12, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0;
            log.AppendLine($"corredor: flechas {Arrows(croot, new[] { new Vector2(0, -68), new Vector2(0, -36) }, y, new List<Bounds>())}");
            EditorSceneManager.MarkSceneDirty(corridor); EditorSceneManager.SaveScene(corridor);
            AssetDatabase.SaveAssets(); File.AppendAllText(Report, "\n" + log); EditorSceneManager.OpenScene(Session, OpenSceneMode.Single); Debug.Log("[LevelOpenPass] guía reconstruida");
        }
        static void BackupScene(string path) { var dst = Path.Combine(Backup, Path.GetFileName(path) + ".bak"); if (!File.Exists(dst)) File.Copy(path, dst); }

        static Material Find(string name) { var guid = AssetDatabase.FindAssets(name + " t:Material").FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name); return guid != null ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)) : null; }
        static void LoadMaterials()
        {
            wall = Find("CP_Wall"); ceiling = Find("CP_Ceiling"); metal = Find("VO_Env_RustMetal"); dark = Find("CP_Cable") ?? metal; hazard = Find("VO_Env_Hazard"); lava = Find("CP_Lava"); cold = Find("VO_Light_Cold");
            Directory.CreateDirectory(MeshDir);
            guide = Emissive("IR_V35_FloorGuide", new Color(1f, .55f, .05f), 1.4f);
            cyan = Emissive("IR_V35_ReactorCyan", new Color(.05f, .75f, 1f), 1.6f);
            orange = Emissive("IR_V35_FoundryGlow", new Color(1f, .38f, .05f), 2.4f);
            window = Emissive("IR_V35_NightWindow", new Color(.18f, .3f, .5f), .9f);
            log.AppendLine($"materials wall={wall} metal={metal} hazard={hazard} lava={lava} cold={cold}");
        }
        static Material Emissive(string name, Color color, float strength)
        {
            var path = MeshDir + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(cold ? cold : metal); AssetDatabase.CreateAsset(m, path); }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color * .35f + Color.black);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color * .35f + Color.black);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * strength); }
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(m); return m;
        }

        // cube with world-scaled UVs (texel density matches the rest of the room), cached per size as an asset
        static Mesh Cube(Vector3 size)
        {
            size = new Vector3(Mathf.Round(size.x * 100) / 100, Mathf.Round(size.y * 100) / 100, Mathf.Round(size.z * 100) / 100);
            if (cubes.TryGetValue(size, out var mesh) && mesh) return mesh;
            var path = $"{MeshDir}/V35Cube_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}.asset".Replace(',', '.');
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(tmp);
                var v = mesh.vertices; var n = mesh.normals; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++) { var q = Vector3.Scale(v[i], size); uv[i] = Mathf.Abs(n[i].x) > .5f ? new Vector2(q.z, q.y) : Mathf.Abs(n[i].y) > .5f ? new Vector2(q.x, q.z) : new Vector2(q.x, q.y); }
                mesh.uv = uv; mesh.RecalculateTangents(); mesh.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(mesh, path);
            }
            cubes[size] = mesh; return mesh;
        }
        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, bool solid, Quaternion? rot = null, bool shadows = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot ?? Quaternion.identity); go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = Cube(size);
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, solid ? StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }
        static GameObject Cyl(Transform parent, string name, Vector3 center, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(go.GetComponent<Collider>()); go.name = name; go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot ?? Quaternion.identity); go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); return go;
        }
        static Transform Root(Scene scene, string name) { var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name); if (old) Quarantine(scene, old); var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go.transform; }
        static void Quarantine(Scene scene, GameObject g)
        {
            var q = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Quarantine_v35");
            if (!q) { q = new GameObject("Quarantine_v35"); SceneManager.MoveGameObjectToScene(q, scene); q.SetActive(false); }
            g.transform.SetParent(q.transform, true); g.name += "_q" + q.transform.childCount;
        }

        // ------------------------------------------------------------------------------------------------ rooms
        static void Room(Spec s)
        {
            var scene = EditorSceneManager.OpenScene(Folder + s.scene + ".unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == "V35_Open")) { log.AppendLine($"== {s.scene}: ya aplicado, se omite"); return; }
            var geo = scene.GetRootGameObjects().First(g => g.name == s.geometry).transform;
            float delta = s.newCeilBottom - s.oldCeilBottom; log.AppendLine($"\n== {s.scene} ({s.identity}) techo {s.oldCeilBottom - s.floorY:0.0} → {s.newCeilBottom - s.floorY:0.0} m libres");
            var open = new GameObject("V35_Open").transform; open.SetParent(geo, false); // under the geometry root: collected by the NavMesh build
            // 1. roof up (fixtures + lights are children and follow)
            foreach (var t in geo.Cast<Transform>().ToArray())
            {
                var r = t.GetComponent<Renderer>(); if (!r || !t.GetComponent<Collider>()) continue;
                bool roof = r.sharedMaterial == ceiling && r.bounds.size.y < 1.2f && r.bounds.size.x > 20 && Mathf.Abs(r.bounds.min.y - s.oldCeilBottom) < .6f;
                if (roof) { t.position += Vector3.up * delta; log.AppendLine($"  techo {t.name} sube {delta} m"); }
            }
            // 2. walls and columns that reached the old ceiling: extension block on top, same material, world-scaled UVs
            int ext = 0;
            foreach (var t in geo.Cast<Transform>().ToArray())
            {
                var r = t.GetComponent<Renderer>(); var c = t.GetComponent<BoxCollider>(); if (!r || !c || t == open) continue;
                var b = r.bounds; if (b.size.y < 5 || Mathf.Abs(b.max.y - s.oldCeilBottom) > 1.1f || b.min.y > s.floorY + 1.5f) continue;
                if (r.sharedMaterial == ceiling && b.size.x > 20) continue;
                float top = s.newCeilBottom, bottom = b.max.y; var size = new Vector3(b.size.x, top - bottom, b.size.z);
                var center = new Vector3(b.center.x, (top + bottom) * .5f, b.center.z); Inset(ref center, ref size, s);
                Box(open, t.name + "_V35Ext", center, size, r.sharedMaterial == ceiling ? ceiling : wall, true); ext++;
            }
            for (int i = 0; i < s.headers.Length; i++) { var hc = s.headers[i]; var hs = s.headerSizes[i]; Inset(ref hc, ref hs, s); Box(open, "DoorHeader_V35", hc, hs, wall, true); }
            log.AppendLine($"  extensiones de muro/columna {ext}, dinteles {s.headers.Length}");
            // 3. bigger, clearer covers (old ones to quarantine)
            int covers = 0;
            foreach (var t in geo.Cast<Transform>().Where(t => t.name == "LowCover" || t.name == "HighCover" || t.name == "RingCover").ToArray())
            {
                var r = t.GetComponent<Renderer>(); var b = r.bounds; var grow = t.name == "HighCover" ? new Vector3(1.5f, 0, .5f) : new Vector3(2f, .1f, .5f);
                var size = b.size + grow; var center = new Vector3(b.center.x, s.floorY + size.y * .5f, b.center.z);
                Box(open, t.name + "_V35", center, size, r.sharedMaterial, true, null, true); Quarantine(scene, t.gameObject); covers++;
            }
            log.AppendLine($"  coberturas agrandadas {covers}");
            // 4. catwalks + ramps (player verticality; enemies use them where the NavMesh fits)
            var footprints = new List<Bounds>();
            foreach (var d in s.decks)
            {
                float top = s.floorY + 3.8f, w = d.x1 - d.x0, cx = (d.x0 + d.x1) * .5f;
                var deck = Box(open, "Catwalk_V35", new Vector3(cx, top - .2f, (d.z0 + d.z1) * .5f), new Vector3(w, .4f, d.z1 - d.z0), metal, true, null, true);
                float high = d.rampLowZ < d.z0 ? d.z0 : d.z1, run = Mathf.Abs(high - d.rampLowZ), len = Mathf.Sqrt(run * run + 3.8f * 3.8f), ang = Mathf.Atan2(3.8f, run) * Mathf.Rad2Deg * (d.rampLowZ < high ? -1 : 1);
                var mid = new Vector3(cx, s.floorY + 1.9f, (high + d.rampLowZ) * .5f); var rot = Quaternion.Euler(ang, 0, 0);
                Box(open, "Ramp_V35", mid + rot * Vector3.down * .2f, new Vector3(w, .4f, len), metal, true, rot, true);
                // edges: hazard strips on the open side, light posts (visual only)
                float edgeX = d.x0 < 0 ? d.x1 - .12f : d.x0 + .12f;
                Box(open, "CatwalkEdge_V35", new Vector3(edgeX, top + .01f, (d.z0 + d.z1) * .5f), new Vector3(.24f, .02f, d.z1 - d.z0), hazard, false);
                for (float z = d.z0 + 1; z <= d.z1 - .5f; z += 3) Box(open, "RailPost_V35", new Vector3(edgeX, top + .55f, z), new Vector3(.1f, 1.1f, .1f), dark, false);
                Box(open, "RailTop_V35", new Vector3(edgeX, top + 1.1f, (d.z0 + d.z1) * .5f), new Vector3(.12f, .08f, d.z1 - d.z0), metal, false);
                for (float z = d.z0 + 2; z < d.z1 - 1; z += 6) Box(open, "CatwalkStrut_V35", new Vector3(cx, s.floorY + (3.6f) * .5f, z), new Vector3(.35f, 3.6f, .35f), dark, true);
                footprints.Add(new Bounds(new Vector3(cx, s.floorY + 2, (Mathf.Min(d.z0, d.rampLowZ) + Mathf.Max(d.z1, d.rampLowZ)) * .5f), new Vector3(w, 4, Mathf.Abs(Mathf.Max(d.z1, d.rampLowZ) - Mathf.Min(d.z0, d.rampLowZ)))));
            }
            log.AppendLine($"  pasarelas {s.decks.Length} (altura +3,8 m, rampas de ~19°)");
            // 5. spawn slots and pickups out of the new footprints (agent radius 2.25 m + margin)
            MoveSlots(scene, s, footprints, open);
            int p = 0; var pickups = scene.GetRootGameObjects().Where(g => g.name == "Pickup_Health").ToArray();
            for (int i = 0; i < s.pickups.Length && i < pickups.Length; i++) { pickups[i].transform.position = s.pickups[i] + Vector3.up * (3.8f + .5f); p++; }
            log.AppendLine($"  pickups de vida movidos a pasarelas: {p}");
            // 6. existing dressing near the old ceiling moves up with it; corner risers grow
            var dress = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "VisualPass_Dressing");
            if (dress)
            {
                int moved = 0;
                foreach (Transform t in dress.transform)
                {
                    var r = t.GetComponent<Renderer>(); if (!r) continue;
                    if (t.name == "Riser") { var b = r.bounds; float h = s.newCeilBottom - s.floorY; t.localScale = new Vector3(t.localScale.x, h * .5f, t.localScale.z); t.position = new Vector3(t.position.x, s.floorY + h * .5f, t.position.z); moved++; }
                    else if (r.bounds.center.y > s.floorY + (s.oldCeilBottom - s.floorY) * .55f) { t.position += Vector3.up * delta; moved++; }
                }
                log.AppendLine($"  dressing existente reubicado {moved}");
            }
            // 7. identity dressing + guidance
            var deco = Root(scene, "V35_Dressing");
            CommonDressing(deco, s);
            switch (s.identity) { case Identity.Foundry: Foundry(deco, s); break; case Identity.Breach: Breach(deco, s); break; case Identity.Assembly: Assembly(deco, s, geo); break; case Identity.Reactor: Reactor(deco, s, geo); break; }
            var guideRoot = Root(scene, "V35_FloorGuide"); Physics.SyncTransforms();
            int arrows = Arrows(guideRoot, s.path, s.floorY, footprints); ExitFrame(guideRoot, s);
            log.AppendLine($"  decoración {deco.GetComponentsInChildren<Renderer>().Length} piezas · flechas {arrows}");
            // 8. sector light tint (count and intensity unchanged)
            foreach (var l in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))) l.color = s.light;
            // 9. NavMesh into the existing asset, reflection probe
            Physics.SyncTransforms(); RebuildNav(scene, geo, s); RebakeProbe(scene, s);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        // perimeter pieces are pulled 3 cm toward the room so their outer face never shares a plane with the airlock walls
        static void Inset(ref Vector3 center, ref Vector3 size, Spec s)
        {
            float cz = (s.zMin + s.zMax) * .5f;
            if (size.x < size.z && size.x <= 1.01f && Mathf.Abs(center.x) > 20) { size.x -= .06f; center.x -= Mathf.Sign(center.x) * .03f; }
            else if (size.z < size.x && size.z <= 1.01f && Mathf.Abs(center.z - cz) > 20) { size.z -= .06f; center.z -= Mathf.Sign(center.z - cz) * .03f; }
        }
        static void MoveSlots(Scene scene, Spec s, List<Bounds> footprints, Transform open)
        {
            var rounds = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CombatTwoRoomsLab>(true)).FirstOrDefault(); if (!rounds) return;
            var solid = open.GetComponentsInChildren<Collider>().Select(c => c.bounds).ToList();
            int moved = 0;
            foreach (var slot in rounds.FirstRoomSpawns.Where(t => t))
            {
                bool Blocked(Vector3 p) => footprints.Any(f => { var e = f; e.Expand(new Vector3(5.5f, 0, 5.5f)); return e.Contains(new Vector3(p.x, f.center.y, p.z)); }) || solid.Any(b => { var e = b; e.Expand(new Vector3(5f, 0, 5f)); return e.Contains(new Vector3(p.x, b.center.y, p.z)) && b.max.y > s.floorY + .3f; });
                if (!Blocked(slot.position)) continue;
                var start = slot.position; var toward = new Vector3(-Mathf.Sign(start.x), 0, 0); if (Mathf.Abs(start.x) < 1) toward = Vector3.right;
                for (float k = 1; k < 20; k++) { var cand = start + toward * k; if (!Blocked(cand)) { slot.position = cand; moved++; break; } }
            }
            log.AppendLine($"  spawns movidos fuera de pasarelas/coberturas: {moved}/{rounds.FirstRoomSpawns.Length}");
        }

        // ------------------------------------------------------------------------------------------------ dressing
        static void CommonDressing(Transform root, Spec s)
        {
            float f = s.floorY, c = s.newCeilBottom, h = c - f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 26.35f;
                // structural pilasters every 8 m and a crane rail under the ceiling
                for (float z = s.zMin + 4; z < s.zMax - 2; z += 8) Box(root, "Pilaster", new Vector3(x, f + h * .5f, z), new Vector3(.3f, h, .7f), metal, false);
                Box(root, "CraneRail", new Vector3(side * 25.9f, c - 1.1f, (s.zMin + s.zMax) * .5f), new Vector3(.6f, .5f, s.zMax - s.zMin - 1), dark, false);
                // high "windows" to the night city in the new upper band
                for (float z = s.zMin + 8; z < s.zMax - 4; z += 8)
                {
                    Box(root, "WindowFrame", new Vector3(side * 26.42f, s.oldCeilBottom + 2.2f, z), new Vector3(.12f, 2.9f, 3.4f), dark, false);
                    Box(root, "WindowGlow", new Vector3(side * 26.34f, s.oldCeilBottom + 2.2f, z), new Vector3(.04f, 2.5f, 3f), window, false);
                }
                // wall fans
                foreach (float z in new[] { s.zMin + (s.zMax - s.zMin) * .3f, s.zMin + (s.zMax - s.zMin) * .7f })
                {
                    Cyl(root, "WallFan", new Vector3(side * 26.2f, c - 3.2f, z), new Vector3(2.6f, .25f, 2.6f), metal, Quaternion.Euler(0, 0, 90));
                    Cyl(root, "WallFanHub", new Vector3(side * 26.0f, c - 3.2f, z), new Vector3(.7f, .32f, .7f), dark, Quaternion.Euler(0, 0, 90));
                    foreach (float a in new[] { 0f, 60f, 120f }) Box(root, "FanBlade", new Vector3(side * 26.05f, c - 3.2f, z), new Vector3(.05f, 2.3f, .32f), dark, false, Quaternion.Euler(a, 0, 0));
                }
            }
            // gantry crane across the room with a chain and a hook
            float gz = s.zMin + (s.zMax - s.zMin) * .62f;
            Box(root, "GantryBeam", new Vector3(0, c - 1.6f, gz), new Vector3(52, .9f, 1.1f), metal, false);
            Box(root, "GantryTrolley", new Vector3(-7, c - 2.3f, gz), new Vector3(2.2f, .9f, 1.6f), dark, false);
            Cyl(root, "Chain", new Vector3(-7, c - 5.2f, gz), new Vector3(.12f, 2.6f, .12f), dark);
            Box(root, "Hook", new Vector3(-7, c - 8f, gz), new Vector3(.9f, .7f, .35f), metal, false);
            // hanging cables
            var rng = new System.Random(s.scene.GetHashCode());
            for (int i = 0; i < 8; i++)
            {
                float x = (float)(rng.NextDouble() * 40 - 20), z = s.zMin + 4 + (float)rng.NextDouble() * (s.zMax - s.zMin - 8), len = 1.5f + (float)rng.NextDouble() * 3;
                Cyl(root, "HangingCable", new Vector3(x, c - len * .5f, z), new Vector3(.06f, len * .5f, .06f), dark, Quaternion.Euler((float)rng.NextDouble() * 12 - 6, 0, (float)rng.NextDouble() * 12 - 6));
            }
        }
        static void Foundry(Transform root, Spec s)
        {
            float f = s.floorY, c = s.newCeilBottom;
            // crucibles hung from the crane rails, molten tops
            foreach (var p in new[] { new Vector3(-18, 0, -20), new Vector3(16, 0, 4) })
            {
                Cyl(root, "Crucible", new Vector3(p.x, c - 4.2f, p.z), new Vector3(3.6f, 1.6f, 3.6f), dark);
                Cyl(root, "CrucibleRim", new Vector3(p.x, c - 2.55f, p.z), new Vector3(3.9f, .12f, 3.9f), metal);
                Cyl(root, "MoltenTop", new Vector3(p.x, c - 2.45f, p.z), new Vector3(3.2f, .04f, 3.2f), lava ? lava : orange);
                foreach (float a in new[] { 0f, 120f, 240f }) Cyl(root, "CrucibleChain", new Vector3(p.x + Mathf.Cos(a * Mathf.Deg2Rad) * 1.6f, c - 1.2f, p.z + Mathf.Sin(a * Mathf.Deg2Rad) * 1.6f), new Vector3(.08f, 1.2f, .08f), dark);
            }
            // furnace mouths in the west and south walls (glow set into the wall, frame in front)
            foreach (float z in new[] { -18f, 0f, 18f })
            {
                Box(root, "FurnaceFrame", new Vector3(-26.38f, f + 2f, z), new Vector3(.2f, 3.6f, 4.6f), metal, false);
                Box(root, "FurnaceGlow", new Vector3(-26.3f, f + 1.9f, z), new Vector3(.06f, 2.8f, 3.8f), orange, false);
            }
            foreach (float x in new[] { -14f, 14f })
            {
                Box(root, "FurnaceFrame", new Vector3(x, f + 2f, s.zMin + .62f), new Vector3(4.6f, 3.6f, .2f), metal, false);
                Box(root, "FurnaceGlow", new Vector3(x, f + 1.9f, s.zMin + .55f), new Vector3(3.8f, 2.8f, .06f), orange, false);
            }
            // molten drip channels down the north wall
            foreach (float x in new[] { -18f, -6f, 6f, 18f }) Box(root, "MoltenRun", new Vector3(x, f + (c - f) * .5f, s.zMax - .55f), new Vector3(.5f, c - f - 1, .06f), orange, false);
        }
        static void Breach(Transform root, Spec s)
        {
            float f = s.floorY, c = s.newCeilBottom;
            // fallen girders leaning on the walls, high up (nothing on the playable floor)
            Box(root, "FallenGirder", new Vector3(-24.5f, f + 8.5f, -6), new Vector3(.9f, .9f, 15), metal, false, Quaternion.Euler(28, 8, 0));
            Box(root, "FallenGirder", new Vector3(24.5f, f + 9.5f, 8), new Vector3(.9f, .9f, 13), metal, false, Quaternion.Euler(-32, -6, 0));
            Box(root, "SnappedBeam", new Vector3(10, c - 2.5f, -14), new Vector3(16, .8f, .9f), metal, false, Quaternion.Euler(0, 12, 18));
            // torn ceiling: a glowing rift with hanging plates and rebar
            Box(root, "CeilingRift", new Vector3(-4, c - .05f, 0), new Vector3(14, .05f, 5), window, false);
            for (int i = 0; i < 7; i++)
            {
                float x = -10 + i * 2.2f;
                Box(root, "HangingPlate", new Vector3(x, c - 1.1f - (i % 3) * .5f, -2.7f + (i % 2) * 5.4f), new Vector3(1.8f, .12f, 2.4f), metal, false, Quaternion.Euler(35 + i * 9, i * 17, 20 - i * 6));
                Cyl(root, "Rebar", new Vector3(x + .6f, c - 1.6f, (i % 2 == 0 ? -2.5f : 2.5f)), new Vector3(.05f, 1.6f, .05f), dark, Quaternion.Euler(15 - i * 4, 0, i * 7 - 20));
            }
            // cracked wall plates hanging off the side walls
            foreach (float z in new[] { -20f, 14f }) foreach (float side in new[] { -1f, 1f })
                Box(root, "LoosePanel", new Vector3(side * 26.1f, f + 6.5f, z), new Vector3(.12f, 3.2f, 2.6f), dark, false, Quaternion.Euler(0, 0, side * 14));
        }
        static void Assembly(Transform root, Spec s, Transform geo)
        {
            float f = s.floorY, c = s.newCeilBottom, cz = (s.zMin + s.zMax) * .5f;
            // ceiling robot arms above the benches
            foreach (var p in new[] { new Vector3(-10, 0, cz - 8), new Vector3(-10, 0, cz + 8), new Vector3(10, 0, cz - 8), new Vector3(10, 0, cz + 8) })
            {
                Cyl(root, "ArmBase", new Vector3(p.x, c - .4f, p.z), new Vector3(1.6f, .4f, 1.6f), dark);
                var a = Quaternion.Euler(0, p.x < 0 ? 30 : -30, 35); var e1 = new Vector3(p.x, c - 2.2f, p.z);
                Box(root, "ArmUpper", e1, new Vector3(.7f, 3.4f, .7f), metal, false, a);
                var elbow = e1 + a * Vector3.down * 1.7f; var b = Quaternion.Euler(0, p.x < 0 ? 30 : -30, -40);
                Box(root, "ArmFore", elbow + b * Vector3.down * 1.4f, new Vector3(.55f, 2.8f, .55f), metal, false, b);
                var hand = elbow + b * Vector3.down * 2.8f;
                Box(root, "ArmTool", hand, new Vector3(.9f, .5f, .9f), dark, false, b);
                Box(root, "ArmTip", hand + Vector3.down * .4f, new Vector3(.18f, .5f, .18f), cyan, false);
            }
            // overhead conveyor with hanging chassis along both walls
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 19f; Box(root, "OverheadConveyor", new Vector3(x, c - 2.2f, cz), new Vector3(.8f, .4f, s.zMax - s.zMin - 4), dark, false);
                for (float z = s.zMin + 5; z < s.zMax - 4; z += 4.5f)
                {
                    Cyl(root, "Hanger", new Vector3(x, c - 3.1f, z), new Vector3(.08f, .8f, .08f), metal);
                    Box(root, "RobotChassis", new Vector3(x, c - 4.6f, z), new Vector3(1.2f, 1.5f, .8f), metal, false, Quaternion.Euler(0, z * 7 % 30 - 15, 0));
                    Box(root, "ChassisEye", new Vector3(x, c - 4.1f, z - .41f), new Vector3(.5f, .12f, .04f), cyan, false);
                }
            }
            // wall racks with robot heads
            foreach (float side in new[] { -1f, 1f }) for (float z = s.zMin + 6; z < s.zMax - 6; z += 10)
            {
                Box(root, "WallRack", new Vector3(side * 26.1f, f + 6.4f, z), new Vector3(.4f, .15f, 4), metal, false);
                for (float k = -1.2f; k <= 1.21f; k += 1.2f) Box(root, "RobotHead", new Vector3(side * 25.9f, f + 6.85f, z + k), new Vector3(.6f, .7f, .6f), dark, false);
            }
        }
        static void Reactor(Transform root, Spec s, Transform geo)
        {
            float f = s.floorY, c = s.newCeilBottom, cz = (s.zMin + s.zMax) * .5f;
            var core = geo.Find("ReactorCore"); float coreTop = core ? core.GetComponent<Renderer>().bounds.max.y : f + 5.5f;
            // energy column from the core to the ceiling, rings around it
            Cyl(root, "EnergyColumn", new Vector3(0, (coreTop + c) * .5f, cz), new Vector3(1.4f, (c - coreTop) * .5f, 1.4f), cyan);
            for (int i = 0; i < 3; i++)
            {
                float y = coreTop + 2f + i * 2.4f;
                Cyl(root, "ContainmentRing", new Vector3(0, y, cz), new Vector3(5.2f - i * .6f, .14f, 5.2f - i * .6f), metal);
                Cyl(root, "RingGlow", new Vector3(0, y - .1f, cz), new Vector3(4.6f - i * .6f, .06f, 4.6f - i * .6f), cyan);
            }
            Cyl(root, "CeilingCollector", new Vector3(0, c - .5f, cz), new Vector3(6, .5f, 6), dark);
            // cyan conduits along the wall bases (on the wall, not the floor) and up the walls
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, "BaseConduit", new Vector3(side * 26.38f, f + .45f, cz), new Vector3(.14f, .16f, s.zMax - s.zMin - 2), cyan, false);
                for (float z = s.zMin + 6; z < s.zMax - 4; z += 12) Box(root, "RiserConduit", new Vector3(side * 26.36f, f + (c - f) * .5f, z), new Vector3(.12f, c - f - .5f, .2f), cyan, false);
            }
            // radial conduits on the ceiling from the collector
            for (int i = 0; i < 6; i++) { var rot = Quaternion.Euler(0, i * 60, 0); Box(root, "CeilingConduit", new Vector3(0, c - .2f, cz) + rot * Vector3.forward * 12, new Vector3(.3f, .14f, 18), cyan, false, rot); }
        }

        // ------------------------------------------------------------------------------------------------ floor guidance
        static int Arrows(Transform root, Vector2[] path, float floorY, List<Bounds> keepOut)
        {
            int n = 0;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                var a = path[i]; var b = path[i + 1]; var dir = (b - a).normalized; float len = (b - a).magnitude;
                for (float t = 1.5f; t < len - .5f; t += 5f)
                {
                    var p = a + dir * t; var top = new Vector3(p.x, floorY + 2.5f, p.y);
                    if (!Physics.Raycast(top, Vector3.down, out var hit, 4, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - floorY) > .08f) continue; // over a gap or a cover: skip
                    if (keepOut.Any(k => k.Contains(new Vector3(p.x, k.center.y, p.y)))) continue;
                    var yaw = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y)); var at = new Vector3(p.x, hit.point.y + .012f, p.y);
                    // chevron ">" pointing along the path: two bars meeting at the tip
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var arm = yaw * Quaternion.Euler(0, -side * 38, 0); // each bar leans toward the centre line: tip forward
                        Box(root, "FloorChevron", at + yaw * Vector3.back * .45f + arm * Vector3.back * .0f + yaw * Vector3.right * side * .42f, new Vector3(.28f, .015f, 1.35f), guide, false, arm);
                    }
                    n++;
                }
            }
            return n;
        }
        static void ExitFrame(Transform root, Spec s)
        {
            var e = s.exitCenter; float f = s.floorY;
            if (s.exitOnX) { foreach (float dz in new[] { -6.1f, 6.1f }) Box(root, "ExitFrameGlow", new Vector3(e.x - .55f, f + 3.6f, e.z + dz), new Vector3(.08f, 7.2f, .25f), guide, false); Box(root, "ExitFrameGlow", new Vector3(e.x - .55f, f + 7.35f, e.z), new Vector3(.08f, .25f, 12.4f), guide, false); }
            else { foreach (float dx in new[] { -5.1f, 5.1f }) Box(root, "ExitFrameGlow", new Vector3(e.x + dx, f + 4f, e.z - .55f), new Vector3(.25f, 8, .08f), guide, false); Box(root, "ExitFrameGlow", new Vector3(e.x, f + 8.1f, e.z - .55f), new Vector3(10.4f, .25f, .08f), guide, false); }
        }

        // ------------------------------------------------------------------------------------------------ bake
        static void RebuildNav(Scene scene, Transform geo, Spec s)
        {
            var surface = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).FirstOrDefault();
            if (!surface || !surface.navMeshData) { log.AppendLine("  NavMesh: sin superficie"); return; }
            var settings = NavMesh.GetSettingsByID(surface.agentTypeID); settings.agentRadius = 2.25f; settings.agentHeight = 3.05f; settings.agentClimb = .25f; settings.agentSlope = 35; settings.overrideVoxelSize = true; settings.voxelSize = .12f;
            var sources = new List<NavMeshBuildSource>(); NavMeshBuilder.CollectSources(surface.transform, -1, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var bounds = surface.navMeshData.sourceBounds; bounds.Encapsulate(bounds.center + Vector3.up * (bounds.extents.y + 6));
            bool ok = NavMeshBuilder.UpdateNavMeshData(surface.navMeshData, settings, sources, bounds);
            surface.RemoveData(); surface.AddData(); EditorUtility.SetDirty(surface.navMeshData);
            var tri = NavMesh.CalculateTriangulation(); log.AppendLine($"  NavMesh actualizado ({(ok ? "ok" : "FALLÓ")}) fuentes={sources.Count} vértices={tri.vertices.Length}");
        }
        static void RebakeProbe(Scene scene, Spec s)
        {
            var probe = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)).FirstOrDefault(); if (!probe) return;
            probe.size = new Vector3(probe.size.x, Mathf.Max(probe.size.y, (s.newCeilBottom - s.floorY) + 2), probe.size.z);
            var path = probe.bakedTexture ? AssetDatabase.GetAssetPath(probe.bakedTexture) : $"Assets/IronRequiem/Art/Levels/Campaign/{s.scene}_Reflection.exr";
            bool ok = Lightmapping.BakeReflectionProbe(probe, path); log.AppendLine($"  sonda de reflexión re-horneada {(ok ? "ok" : "FALLÓ")} → {path}");
        }

        // ------------------------------------------------------------------------------------------------ corridor + session
        static void Corridor()
        {
            var scene = EditorSceneManager.OpenScene(Folder + "Room00b_AlarmCorridor.unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == "V35_FloorGuide")) { log.AppendLine("== corredor: ya aplicado"); return; }
            var root = Root(scene, "V35_FloorGuide"); Physics.SyncTransforms();
            // floor height under the corridor centre
            float y = Physics.Raycast(new Vector3(0, 6, -52), Vector3.down, out var hit, 12, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0;
            int n = Arrows(root, new[] { new Vector2(0, -68), new Vector2(0, -36) }, y, new List<Bounds>());
            log.AppendLine($"\n== Room00b_AlarmCorridor: flechas {n} (piso y={y:0.00}); sin cambios de geometría (pasillo de alarma, ya 20 m de ancho)");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        static void SessionRamp()
        {
            var scene = EditorSceneManager.OpenScene(Session, OpenSceneMode.Single);
            var parent = scene.GetRootGameObjects().First(g => g.name == "PersistentAirlocks").transform;
            if (parent.Find("V35_StairRamp")) { log.AppendLine("== sesión: rampa ya existe"); return; }
            // steps rise 0.2 m every 0.8 m from z=24.8 (y 0) to z=-7.2 (y 8): one invisible plane on the nosings
            float z0 = 24.8f, z1 = -7.2f, rise = 8f, run = z0 - z1, len = Mathf.Sqrt(run * run + rise * rise), ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(ang, 0, 0); var mid = new Vector3(31, rise * .5f, (z0 + z1) * .5f); // rises toward -z
            var ramp = new GameObject("V35_StairRamp", typeof(BoxCollider)); ramp.transform.SetParent(parent, false);
            ramp.transform.SetPositionAndRotation(mid + rot * Vector3.down * .15f, rot); ramp.GetComponent<BoxCollider>().size = new Vector3(7.8f, .3f, len + .2f);
            GameObjectUtility.SetStaticEditorFlags(ramp, StaticEditorFlags.BatchingStatic);
            log.AppendLine($"\n== RequiemCampaign: rampa invisible sobre la escalera ({ang:0.0}°, {len:0.0} m)");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
