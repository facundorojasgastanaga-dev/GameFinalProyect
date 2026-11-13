using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026): variant scenes. MainScene, BunkerLab and BunkerBenchmark are only COPIED; the copies
    // are edited through Editor APIs. Build Settings are not touched (benchmark builds pass their scene explicitly).
    public static class VisualOverhaulScenes
    {
        public const string Folder = "Assets/IronRequiem/Scenes/VisualOverhaul";
        public const string Main = "Assets/FPS/Scenes/MainScene.unity";
        public const string Lab = Folder + "/MainScene_VisualLab.unity";
        public const string BunkerLab = "Assets/IronRequiem/Scenes/BunkerLab.unity";
        public const string BunkerBench = "Assets/IronRequiem/Scenes/BunkerBenchmark.unity";
        public const string BenchA = Folder + "/VisualBench_A.unity";
        public const string BenchB = Folder + "/VisualBench_B.unity";
        public static string Report => VisualOverhaulDriver.Reports;

        static string G(string path) => AssetDatabase.AssetPathToGUID(path);

        // Replaces every object reference to a prefab (its GameObject or any of its components) with the matching
        // object of another prefab, on all components under `roots`.
        public static int RemapPrefabs(IEnumerable<GameObject> roots, Dictionary<string, string> map)
        {
            var assets = map.ToDictionary(kv => AssetDatabase.LoadAssetAtPath<GameObject>(kv.Key), kv => AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value));
            int n = 0;
            foreach (var root in roots)
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (!c || c is Transform) continue; var so = new SerializedObject(c); var it = so.GetIterator(); bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || !it.objectReferenceValue) continue;
                        var v = it.objectReferenceValue; var go = v as GameObject ?? (v as Component)?.gameObject; if (!go) continue;
                        var key = assets.Keys.FirstOrDefault(k => k == go); if (!key) continue;
                        var dest = assets[key]; Object replacement = v is GameObject ? dest : dest.GetComponent(v.GetType());
                        if (replacement) { it.objectReferenceValue = replacement; changed = true; n++; }
                    }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }
            return n;
        }

        static int ReplaceEnemyInstances(Scene scene, Func<string, string> mapper)
        {
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).ToList())
                {
                    if (!t || !t.GetComponent<Unity.FPS.AI.EnemyController>()) continue;
                    var src = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)); var dst = mapper(src);
                    if (dst == null || !File.Exists(dst)) continue;
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(t.gameObject, AssetDatabase.LoadAssetAtPath<GameObject>(dst), new PrefabReplacingSettings { logInfo = false }, InteractionMode.AutomatedAction); n++;
                }
            return n;
        }

        static Material Swap(Material m, Dictionary<string, string> byName)
        {
            if (!m) return m; foreach (var kv in byName) if (m.name.StartsWith(kv.Key, StringComparison.Ordinal)) return VisualOverhaulAssets.Load(kv.Value);
            return m;
        }

        static int SwapMaterials(IEnumerable<Renderer> renderers, Dictionary<string, string> byName)
        {
            int n = 0;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue; var mats = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < mats.Length; i++) { var s = Swap(mats[i], byName); if (s && s != mats[i]) { mats[i] = s; ch = true; } }
                if (ch) { r.sharedMaterials = mats; n++; }
            }
            return n;
        }

        static LightingSettings Lighting(string path)
        {
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (!ls) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, path); }
            ls.bakedGI = true; ls.realtimeGI = false; ls.mixedBakeMode = MixedLightingMode.IndirectOnly; ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.directSampleCount = 16; ls.indirectSampleCount = 128; ls.environmentSampleCount = 64; ls.maxBounces = 2; ls.lightmapResolution = 4; ls.lightmapMaxSize = 1024;
            ls.directionalityMode = LightmapsMode.NonDirectional; ls.ao = false; ls.compressLightmaps = true; ls.autoGenerate = false; ls.filteringMode = LightingSettings.FilterMode.Auto;
            EditorUtility.SetDirty(ls); return ls;
        }

        // Static geometry contributes bounce light but receives it from light probes (no lightmaps, no UV2 needed).
        static void PrepareStaticGI(IEnumerable<Renderer> renderers)
        {
            foreach (var r in renderers)
            {
                if (!(r is MeshRenderer) || !r.enabled) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0 && (flags & StaticEditorFlags.ContributeGI) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                ((MeshRenderer)r).receiveGI = ReceiveGI.LightProbes; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Simple; // static: contributes bounce to the probes, does not sample them (CPU)
            }
        }

        static void SetMixed(IEnumerable<Light> lights) { foreach (var l in lights) if (l && l.type != LightType.Directional && !l.GetComponentInParent<LightFlicker>()) l.lightmapBakeType = LightmapBakeType.Mixed; }

        static GameObject MovePlayerFrom(string sourceScene, Scene into, string[] roots, Vector3 position, Quaternion rotation)
        {
            var src = EditorSceneManager.OpenScene(sourceScene, OpenSceneMode.Additive); GameObject player = null;
            try
            {
                foreach (var r in src.GetRootGameObjects().Where(g => roots.Contains(g.name)).ToList())
                { SceneManager.MoveGameObjectToScene(r, into); if (r.GetComponent<PlayerWeaponsManager>()) player = r; }
            }
            finally { EditorSceneManager.CloseScene(src, true); } // closed WITHOUT saving: the source scene file is untouched
            if (player) { player.transform.SetPositionAndRotation(position, rotation); }
            return player;
        }

        static Dictionary<string, string> WeaponMap(bool visual) => visual
            ? new Dictionary<string, string> {
                { VisualOverhaulWeapons.CannonSource, VisualOverhaulWeapons.Cannon }, { "Assets/IronRequiem/Prefabs/Weapons/Visuals/Weapon_IR_ArmModule_v07.prefab", VisualOverhaulWeapons.Cannon },
                { VisualOverhaulWeapons.ShotgunSource, VisualOverhaulWeapons.Shotgun }, { "Assets/IronRequiem/Prefabs/Weapons/Weapon_Shotgun_HeatOnly.prefab", VisualOverhaulWeapons.Shotgun },
                { "Assets/IronRequiem/Prefabs/SecondaryShotgun/Weapon_Shotgun_Secondary.prefab", VisualOverhaulWeapons.Shotgun } }
            : new Dictionary<string, string> {
                { "Assets/IronRequiem/Prefabs/Weapons/Visuals/Weapon_IR_ArmModule_v07.prefab", VisualOverhaulWeapons.CannonSource },
                { "Assets/IronRequiem/Prefabs/Weapons/Weapon_Shotgun_HeatOnly.prefab", VisualOverhaulWeapons.ShotgunSource },
                { "Assets/IronRequiem/Prefabs/SecondaryShotgun/Weapon_Shotgun_Secondary.prefab", VisualOverhaulWeapons.ShotgunSource } };

        static string EnemyVO(string src) { var p = VisualOverhaulEnemies.VariantPath(src); return File.Exists(p) ? p : null; }

        static void Save(Scene scene) { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save " + scene.path); }

        // ---------------- MainScene_VisualLab ----------------
        public static string BuildLab()
        {
            Directory.CreateDirectory(Folder); var log = new StringBuilder();
            if (File.Exists(Lab)) throw new Exception("Preserve existing " + Lab + " (delete it explicitly to rebuild)");
            if (!AssetDatabase.CopyAsset(Main, Lab)) throw new Exception("Cannot copy MainScene");
            var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Single);
            // Player: the protagonist rig (modular forearm, sleeves, melee/execution, heat HUD) replaces the template player.
            var old = scene.GetRootGameObjects().First(g => g.GetComponent<PlayerWeaponsManager>());
            var pos = old.transform.position; var rot = old.transform.rotation; Object.DestroyImmediate(old);
            var player = MovePlayerFrom(BunkerLab, scene, new[] { "Player_ModularArm_v07", "GameHUD", "InGameMenu" }, pos, rot);
            int remapped = RemapPrefabs(new[] { player }, WeaponMap(true));
            var pwm = player.GetComponent<PlayerWeaponsManager>();
            log.AppendLine($"player: Player_ModularArm_v07 (from BunkerLab, sleeves on) at {pos:F1}; weapon refs remapped {remapped}; starting weapons {string.Join(", ", pwm.StartingWeapons.Select(w => w ? w.name : "null"))}");
            // Enemies -> visual variants (same AI / health / weapons).
            log.AppendLine("enemy instances replaced " + ReplaceEnemyInstances(scene, EnemyVO));
            // Level materials -> HardSurface triplanar bunker set.
            var level = scene.GetRootGameObjects().First(g => g.name == "Level");
            var levelRenderers = level.GetComponentsInChildren<Renderer>(true).ToList();
            log.AppendLine("level renderers re-materialed " + SwapMaterials(levelRenderers, new Dictionary<string, string> { { "Floor_", "VO_Env_FloorTri" }, { "Wall_", "VO_Env_WallTri" }, { "Ramp_", "VO_Env_MetalTri" } }));
            // Light: dim cool fill instead of the orange sun; dark solid background (closed bunker).
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.type == LightType.Directional)) { l.intensity = .35f; l.color = new Color(.62f, .7f, .82f); l.shadows = LightShadows.Soft; }
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (cam.clearFlags == CameraClearFlags.Skybox || cam.clearFlags == CameraClearFlags.SolidColor) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.055f, .06f, .07f); }
            var lb = new Bounds(levelRenderers[0].bounds.center, Vector3.zero); foreach (var r in levelRenderers) if (r.bounds.size.y < 30) lb.Encapsulate(r.bounds);
            var dressLog = VisualOverhaulDressing.Dress(new VisualOverhaulDressing.Options { AddCeiling = true, CeilingY = lb.max.y + .05f, LightSpacing = 15, FixtureRange = 12.5f, FixtureIntensity = 9.5f, Decals = 24, Pipes = 22, ReflectionCell = 36, Vents = 10, Emergency = 10, Flicker = 3, SteamVents = 2 }, out var added);
            log.Append(dressLog);
            // Forward renderer budget: at most 4 additional lights per object; split offending static renderers.
            var all = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            log.AppendLine("light budget before split: " + VisualOverhaulDressing.CheckLightBudget(levelRenderers, all, 4, out var over));
            var afterRenderers = levelRenderers;
            log.AppendLine("splitting tested (6-8 m tiles): it did not lower the count (walls next to an emergency light), so renderers stay whole; URP keeps the 4 most important lights per object on the listed ones.");
            File.WriteAllLines(Report + "/lab-light-budget-over.txt", over.Select(r => r.name + " " + r.bounds.center.ToString("F1") + " " + r.bounds.size.ToString("F1")));
            // Mesh decals: cameras keep the default renderer (no decal renderer feature).
            // Baked indirect light into probes (static contributes bounce, everything samples probes).
            PrepareStaticGI(afterRenderers); SetMixed(Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Lightmapping.lightingSettings = Lighting(Folder + "/VO_Lab.lighting");
            // NavMesh: rebaked in this scene only, into a new asset (dressing is excluded from the build).
            log.AppendLine(RebakeNav(scene, Folder + "/MainScene_VisualLab_NavMesh.asset"));
            Save(scene);
            File.WriteAllText(Report + "/lab-build.txt", log.ToString());
            return log.ToString();
        }

        static string RebakeNav(Scene scene, string asset)
        {
            var surfaces = Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new StringBuilder();
            foreach (var s in surfaces)
            {
                var before = s.navMeshData ? AssetDatabase.GetAssetPath(s.navMeshData) : "none";
                s.RemoveData(); s.navMeshData = null; s.BuildNavMesh();
                if (!s.navMeshData) throw new Exception("NavMesh build failed on " + s.name);
                string path = surfaces.Length == 1 ? asset : asset.Replace(".asset", "_" + s.name + ".asset");
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(s.navMeshData, path); EditorUtility.SetDirty(s);
                var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
                sb.AppendLine($"navmesh {s.name}: rebaked -> {path} (previous {before} untouched), {tri.vertices.Length} verts");
            }
            return sb.ToString();
        }

        // Deletes ONLY the generated lab variant (scene, its lighting folder, navmesh and lighting settings) to rebuild it.
        public static string RebuildLab()
        {
            foreach (var p in new[] { Lab, Folder + "/MainScene_VisualLab", Folder + "/MainScene_VisualLab_NavMesh.asset" }) if (AssetDatabase.LoadMainAssetAtPath(p) || AssetDatabase.IsValidFolder(p)) AssetDatabase.DeleteAsset(p);
            return BuildLab();
        }

        public static string RebuildBenchB()
        {
            foreach (var p in new[] { BenchB, Folder + "/VisualBench_B" }) if (AssetDatabase.LoadMainAssetAtPath(p) || AssetDatabase.IsValidFolder(p)) AssetDatabase.DeleteAsset(p);
            return BuildBench("B");
        }

        // Benchmark B only: adds the diagnostic switches component (inactive without --vo-off).
        public static string AddAblation()
        {
            var scene = EditorSceneManager.OpenScene(BenchB, OpenSceneMode.Single);
            if (!Object.FindAnyObjectByType<VisualOverhaulAblation>()) new GameObject("VO_Ablation").AddComponent<VisualOverhaulAblation>();
            Save(scene); return "ok";
        }

        // Starts the probe bake of the open variant scene; FinishBake saves once it is done.
        public static string Bake(string scenePath)
        {
            if (!scenePath.StartsWith(Folder)) throw new Exception("Only variant scenes are baked");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!Lightmapping.BakeAsync()) throw new Exception("BakeAsync refused");
            return "bake started " + scenePath;
        }
        public static string BakeState() => "running=" + Lightmapping.isRunning + " scene=" + SceneManager.GetActiveScene().path + " dirty=" + SceneManager.GetActiveScene().isDirty + " probes=" + (LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0) + " lightmaps=" + LightmapSettings.lightmaps.Length;
        public static string FinishBake()
        {
            if (Lightmapping.isRunning) return "still running";
            var scene = SceneManager.GetActiveScene(); if (!scene.path.StartsWith(Folder)) throw new Exception("Active scene is not a variant");
            Save(scene); return "saved " + BakeState();
        }

        // ---------------- Benchmark A / B (same fixture, same load) ----------------
        public static string BuildBench(string which)
        {
            bool visual = which == "B"; string dest = visual ? BenchB : BenchA; var log = new StringBuilder("bench " + which + ": ");
            if (File.Exists(dest)) throw new Exception("Preserve existing " + dest);
            if (!AssetDatabase.CopyAsset(BunkerBench, dest)) throw new Exception("Cannot copy benchmark");
            var scene = EditorSceneManager.OpenScene(dest, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            log.Append("weapon refs " + RemapPrefabs(roots, WeaponMap(visual)) + "; ");
            if (visual)
            {
                var runner = Object.FindAnyObjectByType<CombatLoadBenchmark>(); var so = new SerializedObject(runner);
                foreach (var f in new[] { "t07Prefab", "bigBotPrefab" }) { var p = so.FindProperty(f); var src = AssetDatabase.GetAssetPath(p.objectReferenceValue); var vo = EnemyVO(src); if (vo == null) throw new Exception("Missing VO variant of " + src); p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(vo); }
                so.ApplyModifiedPropertiesWithoutUndo();
                var geo = roots.Where(g => g.name == "TwoRoomsGeometry" || g.name == "ArenaGate" || g.name == "BunkerDressing" || g.name == "BunkerLights").SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToList();
                log.Append("renderers re-materialed " + SwapMaterials(geo, new Dictionary<string, string> {
                    { "Bunker_Floor", "VO_Env_Floor" }, { "Bunker_Wall", "VO_Env_Wall" }, { "Bunker_Ceiling", "VO_Env_Ceiling" }, { "Bunker_RustMetal", "VO_Env_RustMetal" },
                    { "Bunker_Hazard", "VO_Env_Hazard" }, { "Bunker_Grating", "VO_Env_Grating" }, { "Bunker_LampCold", "VO_Light_Cold" }, { "Bunker_LampRed", "VO_Light_Red" }, { "Bunker_LampAmber", "VO_Light_Amber" } }) + "; ");
                // Two of the existing cold fixtures flicker (same light count as A).
                var fixtures = roots.First(g => g.name == "BunkerLights").GetComponentsInChildren<Light>(true).Where(l => l.color.b > .9f && l.shadows == LightShadows.None).Take(2).ToList();
                foreach (var l in fixtures) { var r = l.GetComponentInParent<MeshRenderer>(); var f = r.gameObject.AddComponent<LightFlicker>(); f.Configure(l, r, VisualOverhaulAssets.Load("VO_Light_Cold"), VisualOverhaulAssets.Load("VO_Light_Dead"), (int)(l.transform.position.z * 10) & 1023); l.lightmapBakeType = LightmapBakeType.Realtime; }
                log.Append(VisualOverhaulDressing.Dress(new VisualOverhaulDressing.Options { Fixtures = false, CeilingY = 7.25f, Decals = 12, ReflectionCell = 40, Pipes = 16, Vents = 8, Emergency = 0, Flicker = 0, SteamVents = 1, FogDensity = .008f, Fog = RenderSettings.fogColor }, out _));
                var all = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                log.Append("light budget: " + VisualOverhaulDressing.CheckLightBudget(geo, all, 4, out _) + "; ");
                PrepareStaticGI(geo); SetMixed(all);
                Lightmapping.lightingSettings = Lighting(Folder + "/VO_Bench.lighting");
            }
            Save(scene);
            File.WriteAllText(Report + "/bench-" + which + ".txt", log.ToString());
            return log.ToString();
        }
    }
}
