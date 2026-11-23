using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 19/11/2026, requested by the user: the black-and-yellow striped door boxes behind the montage station (lobby
    /// InitialLoadingDoor, transfer airlocks, Room01/Room02 doors, Map01 SourceDoor/DestinationDoor) become steel blast
    /// doors: two leaves with a centre seam, reinforcement ribs, rivets, a centre lock and a worn orange frame (the
    /// station's paint). Only the material changes: the door objects, their colliders and scripts stay as they are.
    /// 1 generates the textures and IR_BlastDoor.mat (procedural, no external assets); 2 swaps the striped material on
    /// those doors in the five station scenes and saves them (refuses if the open scene has unsaved changes); 3 renders
    /// a still. 4 (approved later the same day) does the hangar transit chamber: ChamberHangarDoor takes the blast door,
    /// the porthole door (ChamberCorridorDoor and its DoorRight/DoorAbove/DoorBelow pieces) a riveted steel plate, since a
    /// full door layout repeated on each piece would read as four doors. The stair edge marks, the chamber trims and the
    /// shutter strip keep their stripes. Re-running a level generator that assigns the striped material (Map01
    /// DoorAuthoring, campaign airlock authoring, HangarTransitAuthoring) brings it back: run 2 and 4 again.
    public static class StationSceneDoors
    {
        const string Menu = MountStationArtTools.Menu + "Doors/";
        const string Dir = "Assets/IronRequiem/Art/Levels/Doors";
        const string AlbedoPath = Dir + "/Textures/IR_BlastDoor_Albedo.png", NormalPath = Dir + "/Textures/IR_BlastDoor_Normal.png";
        internal const string MaterialPath = Dir + "/IR_BlastDoor.mat";
        static readonly string[] DoorNames = { "SourceDoor", "DestinationDoor", "InitialLoadingDoor", "Room01ExitDoor", "Room02EntryDoor" };
        static readonly string[] Striped = { "Bunker_Hazard", "VO_Env_Hazard" };
        static string Report => MountStationArtTools.ReportDir + "/doors";
        static void Log(string s) { Directory.CreateDirectory(Report); File.AppendAllText(Report + "/log.txt", DateTime.Now.ToString("HH:mm:ss ") + s + "\n"); Debug.Log("[StationDoors] " + s); }

        // ------------------------------------------------------------------------------------------ 1 textures + material
        [MenuItem(Menu + "1 Generate blast door material")]
        static void Generate()
        {
            const int N = 1024;
            var height = new float[N * N]; var albedo = new Color[N * N];
            var steelDark = new Color(.15f, .16f, .18f); var steelLight = new Color(.30f, .31f, .33f);
            var paint = new Color(.80f, .41f, .11f); var groove = new Color(.035f, .035f, .04f);
            float Noise(float u, float v, float f, float seed = 0) => Mathf.PerlinNoise(u * f + 13.1f + seed, v * f + 7.7f + seed * 2);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + .5f) / N, v = (y + .5f) / N;
                    float grime = .55f * Noise(u, v, 5) + .3f * Noise(u, v, 21, 3) + .15f * Noise(u, v, 67, 9);
                    // vertical streaks (rain of dirt from the ribs and rivets)
                    float streak = Mathf.PerlinNoise(u * 90f, v * 2.5f + 40f);
                    var c = Color.Lerp(steelDark, steelLight, grime * .8f) * Mathf.Lerp(.82f, 1f, streak);
                    float h = .4f;
                    float leafU = u < .5f ? u : 1 - u;                         // distance from the outer edge, per leaf
                    // reinforcement ribs: three raised bands across each leaf
                    foreach (var r in new[] { .24f, .5f, .76f })
                    {
                        float d = v - r;
                        if (Mathf.Abs(d) < .028f && leafU > .07f)
                        {
                            h = .62f; c = Color.Lerp(steelLight, new Color(.36f, .37f, .39f), grime) * (d > .019f ? 1.18f : d < -.019f ? .7f : 1f);
                        }
                    }
                    // diagonal brace on each leaf, between the ribs
                    float du = u < .5f ? u : 1 - u; float diag = Mathf.Abs((v - .26f) - (du - .07f) * 1.05f);
                    if (v > .27f && v < .49f && du > .08f && du < .45f && diag < .012f) { h = .56f; c = steelLight * 1.05f; }
                    diag = Mathf.Abs((v - .51f) - (du - .07f) * 1.05f);
                    if (v > .52f && v < .74f && du > .08f && du < .45f && diag < .012f) { h = .56f; c = steelLight * 1.05f; }
                    // centre seam between the leaves: a dark gap with lit bevels
                    float seam = Mathf.Abs(u - .5f);
                    if (seam < .004f) { h = .05f; c = groove; }
                    else if (seam < .011f) { h = Mathf.Lerp(.15f, .4f, (seam - .004f) / .007f); c *= u < .5f ? .75f : 1.25f; }
                    // painted frame, worn through to the steel in places, with a groove on its inner edge
                    float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                    if (edge < .06f)
                    {
                        bool worn = Noise(u, v, 38, 5) > .75f || Noise(u, v, 140, 8) > .87f;
                        c = worn ? Color.Lerp(steelDark, steelLight, grime) : paint * Mathf.Lerp(.72f, 1.05f, grime) * Mathf.Lerp(.85f, 1f, streak);
                        h = .7f - (edge < .008f ? .25f : 0);
                    }
                    else if (edge < .068f) { h = .1f; c = groove; }
                    // rivets on the frame and along the ribs
                    float Rivet(float cu, float cv) { float dx = (u - cu) * 1.35f, dy = v - cv; return Mathf.Sqrt(dx * dx + dy * dy); }
                    float best = 1;
                    for (int i = 1; i < 20; i++) { float t = i / 20f; best = Mathf.Min(best, Mathf.Min(Mathf.Min(Rivet(.03f, t), Rivet(.97f, t)), Mathf.Min(Rivet(t, .03f), Rivet(t, .97f)))); }
                    foreach (var r in new[] { .24f, .5f, .76f })
                        for (int i = 0; i < 6; i++) { float a = .1f + i * .07f; best = Mathf.Min(best, Mathf.Min(Rivet(a, r), Rivet(1 - a, r))); }
                    if (best < .0075f) { h = .85f - best * 30f; c = Color.Lerp(new Color(.45f, .46f, .48f), steelDark, best / .0075f); }
                    else if (best < .0095f) { h = .3f; c *= .55f; }
                    // centre lock: steel hub, painted ring, dark slot
                    float dl = Mathf.Sqrt((u - .5f) * (u - .5f) * 1.35f * 1.35f + (v - .5f) * (v - .5f));
                    if (dl < .095f)
                    {
                        if (dl > .078f) { c = paint * Mathf.Lerp(.7f, 1f, grime); h = .8f; }
                        else if (dl > .072f) { c = groove; h = .2f; }
                        else { c = Color.Lerp(new Color(.32f, .33f, .35f), new Color(.46f, .47f, .49f), grime); h = .9f - dl * .8f; }
                        if (Mathf.Abs(v - .5f) < .007f && Mathf.Abs(u - .5f) < .045f) { c = groove; h = .3f; }
                    }
                    // oil and soot toward the floor
                    c *= Mathf.Lerp(.65f, 1f, Mathf.SmoothStep(0, 1, v / .22f));
                    c.a = 1; albedo[y * N + x] = c; height[y * N + x] = h;
                }
            var normal = new Color[N * N]; const float strength = 3.2f;
            float H(int x, int y) => height[Mathf.Clamp(y, 0, N - 1) * N + Mathf.Clamp(x, 0, N - 1)];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var n = new Vector3(-(H(x + 1, y) - H(x - 1, y)) * strength, -(H(x, y + 1) - H(x, y - 1)) * strength, 1).normalized;
                    normal[y * N + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                }
            Directory.CreateDirectory(Dir + "/Textures");
            Save(albedo, N, AlbedoPath); Save(normal, N, NormalPath);
            AssetDatabase.ImportAsset(AlbedoPath, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(NormalPath, ImportAssetOptions.ForceUpdate);
            Configure(AlbedoPath, false); Configure(NormalPath, true);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!mat)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader missing");
                mat = new Material(shader) { name = "IR_BlastDoor" }; AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath)); mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath)); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Metallic", .2f); mat.SetFloat("_Smoothness", .34f);   // low metallic: a metal reflecting the warm lobby read as copper
            mat.SetTextureScale("_BaseMap", Vector2.one); mat.SetTextureScale("_BumpMap", Vector2.one);
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
            Log("generated " + AlbedoPath + ", " + NormalPath + ", " + MaterialPath);
        }
        static void Save(Color[] px, int n, string path)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            try { tex.SetPixels(px); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); }
            finally { Object.DestroyImmediate(tex); }
        }
        static void Configure(string path, bool normal)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = !normal;
            ti.maxTextureSize = 1024; ti.mipmapEnabled = true; ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------------------------------ 2 apply to scenes
        [MenuItem(Menu + "2 Apply blast doors to the station scenes (saves them)")]
        static void Apply()
        {
            MountStationArtTools.RequireCleanScenes();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) ?? throw new InvalidOperationException("generate the material first");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder("apply blast doors:");
            try
            {
                foreach (var path in MountStationArtTools.Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var counts = new SortedDictionary<string, int>(); int skipped = 0;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            if (!DoorNames.Contains(r.name)) continue;
                            if (Under(r.transform, "HangarTransit")) { skipped++; continue; }
                            var mats = r.sharedMaterials; bool changed = false;
                            for (int i = 0; i < mats.Length; i++) if (mats[i] && Striped.Contains(mats[i].name)) { mats[i] = mat; changed = true; }
                            if (!changed) continue;
                            r.sharedMaterials = mats; counts.TryGetValue(r.name, out var c); counts[r.name] = c + 1;
                        }
                    int total = counts.Values.Sum();
                    if (total > 0) { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("could not save " + path); }
                    sb.Append($"\n  {scene.name}: {total} doors -> IR_BlastDoor ({string.Join(", ", counts.Select(k => k.Key + " " + k.Value))})" + (skipped > 0 ? $"; {skipped} under HangarTransit kept" : "") + (total > 0 ? "; saved" : "; nothing to change"));
                }
            }
            finally
            {
                Log(sb.ToString());
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        static bool Under(Transform t, string name) { for (var p = t.parent; p; p = p.parent) if (p.name == name) return true; return false; }

        // ------------------------------------------------------------------------------------------ 4 hangar transit chamber
        const string PlateAlbedo = Dir + "/Textures/IR_DoorPlate_Albedo.png", PlateNormal = Dir + "/Textures/IR_DoorPlate_Normal.png";
        internal const string PlatePath = Dir + "/IR_DoorPlate.mat";
        static readonly string[] PortholeDoorParts = { "ChamberCorridorDoor", "DoorRight", "DoorAbove", "DoorBelow" };

        [MenuItem(Menu + "4 Apply to the hangar transit chamber doors (saves the scenes)")]
        static void ApplyTransit()
        {
            MountStationArtTools.RequireCleanScenes();
            var blast = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) ?? throw new InvalidOperationException("generate the blast door first (1)");
            GeneratePlate(); var plate = AssetDatabase.LoadAssetAtPath<Material>(PlatePath);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder("apply transit chamber doors:");
            try
            {
                foreach (var path in MountStationArtTools.Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var counts = new SortedDictionary<string, int>();
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var chamber in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "ConnectionChamber" && Under(t, "HangarTransit")))
                            foreach (var r in chamber.GetComponentsInChildren<MeshRenderer>(true))
                            {
                                var target = r.name == "ChamberHangarDoor" ? blast : PortholeDoorParts.Contains(r.name) ? plate : null;
                                if (!target) continue;
                                var mats = r.sharedMaterials; bool changed = false;
                                for (int i = 0; i < mats.Length; i++) if (mats[i] && mats[i].name == "HG_Hazard") { mats[i] = target; changed = true; }
                                if (!changed) continue;
                                r.sharedMaterials = mats; counts.TryGetValue(r.name, out var c); counts[r.name] = c + 1;
                            }
                    int total = counts.Values.Sum();
                    if (total > 0) { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("could not save " + path); }
                    sb.Append($"\n  {scene.name}: {total} pieces ({string.Join(", ", counts.Select(k => k.Key + " " + k.Value))})" + (total > 0 ? "; saved" : "; nothing to change"));
                }
            }
            finally
            {
                Log(sb.ToString());
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        // riveted steel plate for a door built from several boxes: a bevelled edge with a rivet row, grime and streaks,
        // nothing that would read as a whole door repeated on every piece
        static void GeneratePlate()
        {
            const int N = 512;
            var height = new float[N * N]; var albedo = new Color[N * N];
            var dark = new Color(.15f, .16f, .18f); var light = new Color(.30f, .31f, .33f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + .5f) / N, v = (y + .5f) / N;
                    float grime = .55f * Mathf.PerlinNoise(u * 5 + 31.3f, v * 5 + 2.1f) + .3f * Mathf.PerlinNoise(u * 21 + 5.5f, v * 21 + 9.9f) + .15f * Mathf.PerlinNoise(u * 67 + 1.7f, v * 67 + 4.2f);
                    float streak = Mathf.PerlinNoise(u * 60f, v * 2.5f + 80f);
                    var c = Color.Lerp(dark, light, grime * .8f) * Mathf.Lerp(.82f, 1f, streak); float h = .4f;
                    float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                    if (edge < .014f) { h = .15f + edge * 15f; c *= .62f; }
                    float best = 1;
                    for (int i = 1; i < 12; i++)
                    {
                        float t = i / 12f;
                        foreach (var (cu, cv) in new[] { (.04f, t), (.96f, t), (t, .04f), (t, .96f) }) { float dx = u - cu, dy = v - cv; best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy)); }
                    }
                    if (best < .011f) { h = .85f - best * 25f; c = Color.Lerp(new Color(.45f, .46f, .48f), dark, best / .011f); }
                    else if (best < .014f) { h = .3f; c *= .55f; }
                    c *= Mathf.Lerp(.65f, 1f, Mathf.SmoothStep(0, 1, v / .22f)); c.a = 1;
                    albedo[y * N + x] = c; height[y * N + x] = h;
                }
            var normal = new Color[N * N]; const float strength = 2.4f;
            float H(int x, int y) => height[Mathf.Clamp(y, 0, N - 1) * N + Mathf.Clamp(x, 0, N - 1)];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var n = new Vector3(-(H(x + 1, y) - H(x - 1, y)) * strength, -(H(x, y + 1) - H(x, y - 1)) * strength, 1).normalized;
                    normal[y * N + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                }
            Directory.CreateDirectory(Dir + "/Textures");
            Save(albedo, N, PlateAlbedo); Save(normal, N, PlateNormal);
            AssetDatabase.ImportAsset(PlateAlbedo, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(PlateNormal, ImportAssetOptions.ForceUpdate);
            Configure(PlateAlbedo, false); Configure(PlateNormal, true);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PlatePath);
            if (!mat)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader missing");
                mat = new Material(shader) { name = "IR_DoorPlate" }; AssetDatabase.CreateAsset(mat, PlatePath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PlateAlbedo)); mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PlateNormal)); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Metallic", .2f); mat.SetFloat("_Smoothness", .34f);
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
            Log("generated " + PlatePath);
        }

        // ------------------------------------------------------------------------------------------ 3 still
        [MenuItem(Menu + "3 Render door still")]
        static void Still()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) ?? throw new InvalidOperationException("generate the material first");
            var scene = EditorSceneManager.NewPreviewScene(); Directory.CreateDirectory(Report);
            try
            {
                GameObject Add(GameObject go) { UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); return go; }
                var door = Add(GameObject.CreatePrimitive(PrimitiveType.Cube)); door.transform.localScale = new Vector3(8, 6, .9f); door.transform.position = new Vector3(0, 3, 0);
                door.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var floor = Add(GameObject.CreatePrimitive(PrimitiveType.Quad)); floor.transform.rotation = Quaternion.Euler(90, 0, 0); floor.transform.localScale = Vector3.one * 30;
                var key = Add(new GameObject("Key")).AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.3f; key.transform.rotation = Quaternion.Euler(35, 20, 0);
                var spot = Add(new GameObject("Lamp")).AddComponent<Light>(); spot.type = LightType.Point; spot.range = 9; spot.intensity = 4; spot.color = new Color(1f, .75f, .45f); spot.transform.position = new Vector3(-2.5f, 5, -2.5f);
                var cam = Add(new GameObject("Cam")).AddComponent<Camera>(); cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .055f, .07f);
                cam.fieldOfView = 55; cam.transform.position = new Vector3(2.2f, 2.2f, -7.5f); cam.transform.LookAt(new Vector3(0, 2.8f, 0));
                var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes(Report + "/door-still.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
                Log("still " + Report + "/door-still.png");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
