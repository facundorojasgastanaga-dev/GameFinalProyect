using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Abandoned bunker lab (06/11/2026): copy of SecondaryShotgunLab with procedural concrete / rusted metal /
    // hazard / grating textures, world-scaled UVs (shared materials, SRP Batcher friendly), a ceiling, cold
    // ceiling fixtures and red/amber emergency lights, fog and a NavMesh rebaked in the new scene only.
    // Also configures the shotgun bank reload on Weapon_Shotgun_Secondary and the arm sleeves on the lab player.
    public static class BunkerPolishAuthoring
    {
        public const string Lab = "Assets/IronRequiem/Scenes/BunkerLab.unity";
        public const string Art = "Assets/IronRequiem/Art/Environment/Bunker";
        public const string Reports = "Tools/IronRequiem/Reports/BunkerPolish";
        public const float ReloadSeconds = 1.1f;

        // ---------- Shotgun ----------
        public static void ConfigureShotgun()
        {
            string flashPath = SecondaryShotgunAuthoring.Folder + "/MuzzleFlash_Shotgun_Secondary.prefab";
            var go = PrefabUtility.LoadPrefabContents(SecondaryShotgunAuthoring.Weapon);
            try
            {
                var w = go.GetComponent<WeaponController>(); w.AutomaticReload = false;
                if (w.MuzzleFlashPrefab && !File.Exists(flashPath))
                {
                    var preview = EditorSceneManager.NewPreviewScene();
                    try { var f = (GameObject)PrefabUtility.InstantiatePrefab(w.MuzzleFlashPrefab, preview); f.transform.localScale *= 1.8f; PrefabUtility.SaveAsPrefabAsset(f, flashPath); }
                    finally { EditorSceneManager.ClosePreviewScene(preview); }
                }
                if (File.Exists(flashPath)) w.MuzzleFlashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(flashPath);
                var cells = go.GetComponent<WeaponFuelCellHandler>();
                var old = w.WeaponRoot.transform.Find("ReloadSteam"); if (old) Object.DestroyImmediate(old.gameObject);
                var steam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_AngrySteam.prefab"), w.WeaponRoot.transform);
                steam.name = "ReloadSteam";
                var center = cells && cells.FuelCells.Length > 0 ? cells.FuelCells.Aggregate(Vector3.zero, (a, c) => a + c.transform.position) / cells.FuelCells.Length : w.WeaponRoot.transform.position;
                steam.transform.position = center; steam.transform.rotation = Quaternion.LookRotation(Vector3.up, -w.WeaponMuzzle.forward); steam.transform.localScale = Vector3.one * 0.5f;
                int fpsLayer = w.WeaponRoot.layer; foreach (var t in steam.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = fpsLayer;
                var ps = steam.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var p in ps) { var main = p.main; main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World; }
                var reload = go.GetComponent<CylinderBankReload>(); if (!reload) reload = go.AddComponent<CylinderBankReload>();
                reload.Configure(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/Weapons/Weapon_Cooling.wav"), ps.FirstOrDefault(), ReloadSeconds);
                PrefabUtility.SaveAsPrefabAsset(go, SecondaryShotgunAuthoring.Weapon);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        // ---------- Procedural textures ----------
        static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1442695040; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static float Noise(float x, float y, int s, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int W(int v) => ((v % period) + period) % period;
            float a = Hash(W(x0), W(y0), s), b = Hash(W(x0 + 1), W(y0), s), c = Hash(W(x0), W(y0 + 1), s), d = Hash(W(x0 + 1), W(y0 + 1), s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        static float Fbm(float u, float v, int s, int baseFreq = 4, int octaves = 5)
        { float sum = 0, amp = .5f, norm = 0; int f = baseFreq; for (int o = 0; o < octaves; o++) { sum += amp * Noise(u * f, v * f, s + o * 17, f); norm += amp; amp *= .5f; f *= 2; } return sum / norm; }

        const int Size = 512;
        delegate void Shade(float u, float v, out Color albedo, out float height);
        static (Texture2D albedo, Texture2D normal) Make(string name, Shade shade, float bump)
        {
            var albedo = new Color[Size * Size]; var height = new float[Size * Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) { shade(x / (float)Size, y / (float)Size, out var c, out var h); albedo[y * Size + x] = c; height[y * Size + x] = h; }
            var normal = new Color[Size * Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float H(int dx, int dy) => height[((y + dy + Size) % Size) * Size + (x + dx + Size) % Size];
                var n = new Vector3((H(-1, 0) - H(1, 0)) * bump, (H(0, -1) - H(0, 1)) * bump, 1).normalized;
                normal[y * Size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            return (Save(name + "_Albedo", albedo, false), Save(name + "_Normal", normal, true));
        }
        static Texture2D Save(string name, Color[] pixels, bool normal)
        {
            string path = Art + "/Textures/" + name + ".png"; Directory.CreateDirectory(Path.GetDirectoryName(path));
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, true, normal); t.SetPixels(pixels); t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path); var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; imp.sRGBTexture = !normal; imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 4; imp.maxTextureSize = 512; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Mat(string name, (Texture2D a, Texture2D n) tex, float tilingMetres, float smooth, float metal, Color emission = default)
        {
            string path = Art + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex.a); m.SetColor("_BaseColor", Color.white); m.SetTexture("_BumpMap", tex.n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1);
            // World-scaled UVs are in metres: one texture repeat every tilingMetres.
            m.SetTextureScale("_BaseMap", Vector2.one / tilingMetres); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (emission != default) { m.SetColor("_EmissionColor", emission); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            EditorUtility.SetDirty(m); return m;
        }
        static Material Emissive(string name, Color color, float intensity)
        {
            string path = Art + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color * .6f); m.SetColor("_EmissionColor", color * intensity); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(m); return m;
        }

        static void Concrete(float u, float v, out Color c, out float h, int seed, Color tint, float stains)
        {
            float n = Fbm(u, v, seed, 4, 6), fine = Fbm(u, v, seed + 99, 32, 3), stain = Fbm(u, v, seed + 7, 2, 4);
            // Hairline cracks: thin ridges of a low-frequency noise field.
            float crack = Mathf.Abs(Fbm(u, v, seed + 31, 3, 4) - .5f); float crackLine = Mathf.Clamp01(1 - crack / .0035f);
            float tone = .48f + (n - .5f) * .35f + (fine - .5f) * .12f - Mathf.Clamp01(stain - .6f) * stains * .6f - crackLine * .22f;
            c = tint * tone; c.a = 1; h = n * .6f + fine * .3f - crackLine * .5f;
        }

        public static void Build()
        {
            if (File.Exists(Lab)) throw new Exception("Preserve existing " + Lab);
            Directory.CreateDirectory(Art + "/Meshes"); Directory.CreateDirectory(Reports); AssetDatabase.Refresh();
            // Textures: 4 m concrete floor with tile seams, 3 m wall panels, 2 m rusted metal, hazard stripes, grating.
            var floorTex = Make("Bunker_ConcreteFloor", (float u, float v, out Color c, out float h) =>
            { Concrete(u, v, out c, out h, 11, new Color(.82f, .82f, .8f), .45f); float seam = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v), Mathf.Min(Mathf.Abs(u - .5f), Mathf.Abs(v - .5f))); if (seam < .004f) { c *= .55f; h -= .6f; } }, 6);
            var wallTex = Make("Bunker_ConcreteWall", (float u, float v, out Color c, out float h) =>
            {
                Concrete(u, v, out c, out h, 23, new Color(.78f, .8f, .8f), .3f);
                float streak = Fbm(u * 6, v * .5f, 41, 4, 3); if (v < .75f) c *= 1 - Mathf.Clamp01(streak - .55f) * .6f * (1 - v); // water/rust streaks
                float panel = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)); if (panel < .005f) { c *= .5f; h -= .7f; }
                foreach (var b in new[] { new Vector2(.06f, .06f), new Vector2(.94f, .06f), new Vector2(.06f, .94f), new Vector2(.94f, .94f) }) if ((new Vector2(u, v) - b).magnitude < .012f) { c = new Color(.25f, .24f, .22f); h += .5f; }
            }, 5);
            var rustTex = Make("Bunker_RustMetal", (float u, float v, out Color c, out float h) =>
            {
                float n = Fbm(u, v, 57, 4, 6), rust = Mathf.Clamp01((Fbm(u, v, 63, 3, 5) - .42f) * 3), scratch = Mathf.Abs(Noise(u * 220, v * 6, 71, 220) - .5f) < .02f ? 1 : 0;
                var steel = new Color(.36f, .38f, .40f) * (.85f + n * .3f); var rusty = Color.Lerp(new Color(.42f, .2f, .08f), new Color(.58f, .33f, .14f), Fbm(u, v, 77, 16, 3));
                c = Color.Lerp(steel, rusty, rust); c += Color.white * scratch * .08f * (1 - rust); c.a = 1; h = n * .4f + rust * .5f;
                float plate = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)); if (plate < .006f) { c *= .45f; h -= .8f; }
            }, 4);
            var hazardTex = Make("Bunker_Hazard", (float u, float v, out Color c, out float h) =>
            {
                bool yellow = Mathf.Repeat((u + v) * 4, 1) < .5f; float wear = Fbm(u, v, 91, 6, 5);
                c = yellow ? new Color(.86f, .66f, .08f) : new Color(.07f, .07f, .07f);
                if (wear > .62f) c = Color.Lerp(c, new Color(.4f, .38f, .36f) * (.6f + wear * .4f), Mathf.Clamp01((wear - .62f) * 6));
                c.a = 1; h = wear * .3f;
            }, 3);
            var gratingTex = Make("Bunker_Grating", (float u, float v, out Color c, out float h) =>
            {
                float gx = Mathf.Repeat(u * 16, 1), gy = Mathf.Repeat(v * 16, 1); bool bar = gx < .22f || gy < .22f;
                float rust = Fbm(u, v, 101, 4, 4); c = bar ? Color.Lerp(new Color(.32f, .33f, .34f), new Color(.38f, .22f, .12f), Mathf.Clamp01(rust * 1.4f - .4f)) : new Color(.03f, .03f, .035f); c.a = 1; h = bar ? .8f : 0;
            }, 8);
            var floorMat = Mat("Bunker_Floor", floorTex, 4, .25f, 0); var wallMat = Mat("Bunker_Wall", wallTex, 3, .15f, 0);
            var rustMat = Mat("Bunker_RustMetal", rustTex, 2, .35f, .55f); var hazardMat = Mat("Bunker_Hazard", hazardTex, 1.5f, .3f, .1f);
            var gratingMat = Mat("Bunker_Grating", gratingTex, 2, .4f, .6f); var ceilingMat = Mat("Bunker_Ceiling", wallTex, 4, .1f, 0);
            var coldLamp = Emissive("Bunker_LampCold", new Color(.78f, .9f, 1f), 6); var redLamp = Emissive("Bunker_LampRed", new Color(1f, .12f, .05f), 8); var amberLamp = Emissive("Bunker_LampAmber", new Color(1f, .55f, .1f), 7);

            if (!AssetDatabase.CopyAsset(SecondaryShotgunAuthoring.Lab, Lab)) throw new Exception("Cannot copy lab");
            var scene = EditorSceneManager.OpenScene(Lab);
            var geometry = scene.GetRootGameObjects().First(g => g.name == "TwoRoomsGeometry").transform;
            var gate = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ArenaGate");
            var meshes = new Dictionary<Vector3, Mesh>(); var log = new List<string>(); int tiles = 0;
            var sources = geometry.Cast<Transform>().ToList();
            // Ceiling closes the space at wall height (7 m). No shadow casting: the dimmed sun stays a soft fill.
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube); ceiling.name = "Ceiling"; ceiling.transform.SetParent(geometry, false); ceiling.transform.position = new Vector3(0, 7.25f, 25); ceiling.transform.localScale = new Vector3(55, .5f, 119);
            sources.Add(ceiling.transform);
            foreach (var t in sources)
            {
                var r = t.GetComponent<MeshRenderer>(); if (!r) continue; var s = t.localScale; string n = t.name;
                Material m = n == "Floor" ? floorMat : n == "Ceiling" ? ceilingMat : n.Contains("Cover") ? rustMat : wallMat;
                // Render tiles (≤ 10 m) with world-scaled UVs: shared materials, and each tile stays within the
                // forward renderer's 4 additional lights per object. The original keeps its collider for physics/NavMesh.
                r.enabled = false;
                int nx = Mathf.Max(1, Mathf.CeilToInt(s.x / 10)), nz = Mathf.Max(1, Mathf.CeilToInt(s.z / 10));
                var size = new Vector3(s.x / nx, s.y, s.z / nz);
                for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++)
                {
                    var tile = new GameObject(n + "_Tile", typeof(MeshFilter), typeof(MeshRenderer)); tile.transform.SetParent(t, false);
                    var offset = new Vector3((ix + .5f) / nx - .5f, 0, (iz + .5f) / nz - .5f);
                    tile.transform.localPosition = offset; tile.transform.localScale = new Vector3(1f / nx, 1, 1f / nz);
                    tile.GetComponent<MeshFilter>().sharedMesh = Box(meshes, size, t.position + Vector3.Scale(offset, s)); var tr = tile.GetComponent<MeshRenderer>(); tr.sharedMaterial = m;
                    tr.shadowCastingMode = n == "Ceiling" ? ShadowCastingMode.Off : ShadowCastingMode.On; GameObjectUtility.SetStaticEditorFlags(tile, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic); tiles++;
                }
                if (n == "Ceiling") Object.DestroyImmediate(t.GetComponent<Collider>()); // never part of navigation or hits above wall height
                if (n == "Ceiling") t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            if (gate) { var gr = gate.GetComponent<MeshRenderer>(); gr.sharedMaterial = hazardMat; gate.GetComponent<MeshFilter>().sharedMesh = Box(meshes, gate.transform.localScale, gate.transform.position, true); }
            // Corridor grating and hazard bands on the floor, plus hazard bands on the low covers' tops.
            var dressing = new GameObject("BunkerDressing").transform;
            Quad(dressing, meshes, "CorridorGrating", new Vector3(0, .012f, 29), new Vector2(12, 10), gratingMat);
            foreach (float z in new[] { 23.4f, 34.6f }) Quad(dressing, meshes, "HazardBand", new Vector3(0, .014f, z), new Vector2(12, .8f), hazardMat);
            foreach (var t in sources.Where(t => t && t.name == "LowCover")) Quad(dressing, meshes, "CoverHazard", t.position + Vector3.up * (t.localScale.y * .5f + .006f), new Vector2(t.localScale.x, .5f), hazardMat);
            foreach (var t in sources.Where(t => t && t.name == "Column")) { var band = GameObject.CreatePrimitive(PrimitiveType.Cube); band.name = "ColumnHazard"; Object.DestroyImmediate(band.GetComponent<Collider>()); band.transform.SetParent(dressing, false); band.transform.position = t.position + Vector3.down * (t.localScale.y * .5f - .6f); var bandSize = new Vector3(t.localScale.x + .04f, 1.2f, t.localScale.z + .04f); band.transform.localScale = bandSize; band.GetComponent<MeshFilter>().sharedMesh = Box(meshes, bandSize, band.transform.position, true); band.GetComponent<MeshRenderer>().sharedMaterial = hazardMat; }

            // Lights: cold fixtures in a grid (no shadows) + two shadowed key lights + red/amber emergency lights.
            var lights = new GameObject("BunkerLights").transform; int lightCount = 0, shadowed = 0;
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun) { sun.intensity = .55f; sun.color = new Color(.62f, .7f, .82f); sun.shadows = LightShadows.Soft; }
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.36f, .38f, .42f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .008f; RenderSettings.fogColor = new Color(.06f, .065f, .075f);
            RenderSettings.skybox = null; var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cams) if (cam.clearFlags == CameraClearFlags.Skybox) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor; }
            void Fixture(Vector3 p, Material lamp, Color color, float range, float intensity, bool shadows, Vector3 fixtureSize)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube); f.name = "LightFixture"; Object.DestroyImmediate(f.GetComponent<Collider>()); f.transform.SetParent(lights, false); f.transform.position = p; f.transform.localScale = fixtureSize;
                var fr = f.GetComponent<MeshRenderer>(); fr.sharedMaterial = lamp; fr.shadowCastingMode = ShadowCastingMode.Off;
                var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(f.transform, false); l.transform.position = p + Vector3.down * .3f;
                l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity; l.shadows = shadows ? LightShadows.Soft : LightShadows.None; l.lightmapBakeType = LightmapBakeType.Realtime; lightCount++; if (shadows) shadowed++;
            }
            foreach (float z in new[] { -22f, -6f, 10f, 44f, 60f, 76f }) foreach (float x in new[] { -13f, 13f }) Fixture(new Vector3(x, 6.85f, z), coldLamp, new Color(.75f, .88f, 1f), 20, 5.5f, false, new Vector3(2.4f, .12f, .5f));
            Fixture(new Vector3(0, 6.85f, 29), coldLamp, new Color(.75f, .88f, 1f), 14, 4.5f, false, new Vector3(2.4f, .12f, .5f));
            Fixture(new Vector3(0, 6.85f, -2), coldLamp, new Color(.8f, .9f, 1f), 26, 6f, true, new Vector3(3f, .12f, .6f));
            Fixture(new Vector3(0, 6.85f, 58), coldLamp, new Color(.8f, .9f, 1f), 26, 6f, true, new Vector3(3f, .12f, .6f));
            foreach (var p in new[] { new Vector3(-26.3f, 5.2f, -12), new Vector3(26.3f, 5.2f, 46), new Vector3(-6f, 5.6f, 23.3f), new Vector3(6f, 5.6f, 34.7f) }) Fixture(p, redLamp, new Color(1f, .15f, .08f), 10, 5f, false, new Vector3(.35f, .35f, .35f));
            foreach (var p in new[] { new Vector3(26.3f, 5.2f, -20), new Vector3(-26.3f, 5.2f, 70), new Vector3(0, 6.3f, -33.3f), new Vector3(0, 6.3f, 83.3f) }) Fixture(p, amberLamp, new Color(1f, .58f, .15f), 11, 4.5f, false, new Vector3(.35f, .35f, .35f));

            // NavMesh rebaked in this scene only, same settings as the original lab, into a new asset.
            var surface = geometry.GetComponent<Unity.AI.Navigation.NavMeshSurface>(); var oldData = surface ? surface.navMeshData : null;
            var settings = NavMesh.GetSettingsByID(0); settings.agentRadius = 2.25f; settings.agentHeight = 3.05f; settings.agentClimb = .25f; settings.agentSlope = 35; settings.overrideVoxelSize = true; settings.voxelSize = .12f;
            var navSources = new List<NavMeshBuildSource>(); Physics.SyncTransforms();
            NavMeshBuilder.CollectSources(geometry, -1, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), navSources);
            var data = NavMeshBuilder.BuildNavMeshData(settings, navSources, new Bounds(new Vector3(0, 3, 25), new Vector3(60, 18, 124)), Vector3.zero, Quaternion.identity);
            if (!data) throw new Exception("NavMesh failed"); AssetDatabase.CreateAsset(data, Art + "/BunkerNavMesh.asset");
            if (surface) { surface.navMeshData = data; EditorUtility.SetDirty(surface); }

            // Lab player: arm sleeves on.
            var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
            var so = new SerializedObject(hands); so.FindProperty("armSleeves").boolValue = true; so.FindProperty("sleeveRadius").floatValue = .038f; so.FindProperty("sleeveMaterial").objectReferenceValue = SleeveMaterial(); so.ApplyModifiedPropertiesWithoutUndo();
            AttachSpawnQueue();
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save bunker lab");
            var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == Lab)) { scenes.Add(new EditorBuildSettingsScene(Lab, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
            var tri = NavMesh.CalculateTriangulation();
            File.WriteAllText(Reports + "/bunker-build.txt", $"Render tiles {tiles}, unique box meshes {meshes.Count}, lights {lightCount} (shadowed {shadowed}) + sun 0.55 fill, ambient 0.36, sleeve radius 0.038, fog exp2 0.008, ceiling y=7.25 (no collider, no shadows). NavMesh rebaked: {tri.vertices.Length} vertices, {tri.indices.Length / 3} triangles (old asset untouched: {(oldData ? AssetDatabase.GetAssetPath(oldData) : "none")}).");
            IronRequiemSetupTools.WriteAudit();
        }

        // Dark armoured suit sleeve: plain URP/Lit, no stretched forearm texture on the cylinder.
        static Material SleeveMaterial()
        {
            string path = Art + "/Bunker_SuitSleeve.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", new Color(.16f, .17f, .18f)); m.SetFloat("_Smoothness", .35f); m.SetFloat("_Metallic", .2f); EditorUtility.SetDirty(m); return m;
        }
        // Wave director in the open scene: prewarm + one activation per frame (removes the one-frame wave spawn hitch).
        static void AttachSpawnQueue()
        {
            var lab = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (!lab) throw new Exception("CombatTwoRoomsLab missing");
            var queue = lab.GetComponent<EnemySpawnQueue>() ?? lab.gameObject.AddComponent<EnemySpawnQueue>(); queue.Configure(1, 1);
            var so = new SerializedObject(lab); so.FindProperty("spawnQueue").objectReferenceValue = queue; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(queue);
        }
        public static void AddSpawnQueue(params string[] scenePaths)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            foreach (string path in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single); AttachSpawnQueue();
                EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save " + path);
            }
            EditorSceneManager.RestoreSceneManagerSetup(setup); IronRequiemSetupTools.WriteAudit();
        }
        public static void Rebuild() { if (File.Exists(Lab)) AssetDatabase.DeleteAsset(Lab); Build(); }

        static void Quad(Transform parent, Dictionary<Vector3, Mesh> meshes, string name, Vector3 center, Vector2 size, Material m)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.position = center; go.transform.localScale = new Vector3(size.x, .004f, size.y);
            go.GetComponent<MeshFilter>().sharedMesh = Box(meshes, new Vector3(size.x, .004f, size.y), center); var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // Unit cube (scaled by the transform) whose UVs are in world metres for the given size.
        // UVs use the world position too, so adjacent tiles continue the pattern without visible repeats at seams.
        static Mesh Box(Dictionary<Vector3, Mesh> cache, Vector3 size, Vector3 worldCenter, bool unitScale = false)
        {
            var key = new Vector3(Mathf.Round(size.x * 100), Mathf.Round(size.y * 100), Mathf.Round(size.z * 100)) + new Vector3(Mathf.Round(worldCenter.x * 10) * 1e5f, Mathf.Round(worldCenter.y * 10) * 1e9f, Mathf.Round(worldCenter.z * 10) * 1e3f) * (unitScale ? -1 : 1);
            if (cache.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var idx = new List<int>();
            void Face(Vector3 normal, Vector3 up)
            {
                var right = Vector3.Cross(up, normal); int b = v.Count;
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i == 1 || i == 2) ? .5f : -.5f, sy = i >= 2 ? .5f : -.5f;
                    var p = normal * .5f + right * sx + up * sy; v.Add(p); n.Add(normal);
                    var w = Vector3.Scale(p, size) + worldCenter; uv.Add(new Vector2(Vector3.Dot(w, right), Vector3.Dot(w, up)));
                }
                // Unity front faces are clockwise: Cross(b-a, c-a) must point along the outward normal.
                bool cw = Vector3.Dot(Vector3.Cross(v[b + 2] - v[b], v[b + 1] - v[b]), normal) > 0;
                idx.AddRange(cw ? new[] { b, b + 2, b + 1, b, b + 3, b + 2 } : new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            Face(Vector3.up, Vector3.forward); Face(Vector3.down, Vector3.back); Face(Vector3.forward, Vector3.up); Face(Vector3.back, Vector3.up); Face(Vector3.right, Vector3.up); Face(Vector3.left, Vector3.up);
            var mesh = new Mesh { name = "BunkerBox_" + cache.Count }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(idx, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Art + "/Meshes/" + mesh.name + ".asset"); cache[key] = mesh; return mesh;
        }
    }
}
