using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual pass F2 (09/11/2026). New tileable textures generated for the campaign (steel wall panels with rivets
    // and an orange accent rib, tread floor plates, corrugated ceiling with rusty beams), used through new
    // IronRequiem/HardSurface campaign materials in world triplanar mode (the room boxes are scaled cubes: object UVs
    // stretch; triplanar keeps a constant metres-per-tile). Only the five campaign room scenes are re-pointed;
    // VO_Env_* materials, the labs and the FPS template stay untouched.
    public static class VisualPassF2
    {
        const string Tex = "Assets/IronRequiem/Art/VisualPass/Textures";
        const string Mats = "Assets/IronRequiem/Art/VisualPass/Materials";
        const string Rooms = "Assets/IronRequiem/Scenes/Campaign/";
        const string Backup = "Tools/IronRequiem/Backups/20261109-visualpass-f2";
        static readonly string[] Scenes = { "Room01_Foundry", "Room02_Breach", "Room03_Assembly", "Room04_Reactor", "Pursuit05" };

        struct Spec { public string Name, Tex, Source; public float Scale, Metallic, Smooth, Dirt, Wear, Bump; public Color Tint; }
        static readonly Spec[] Specs =
        {
            new Spec { Name = "CP_Wall",    Tex = "CP_WallPanels",  Source = "VO_Env_Wall",    Scale = 3f, Metallic = .55f, Smooth = .38f, Dirt = .28f, Wear = .55f, Bump = 1f,  Tint = Color.white },
            new Spec { Name = "CP_Floor",   Tex = "CP_FloorPlate",  Source = "VO_Env_Floor",   Scale = 2f, Metallic = .6f,  Smooth = .32f, Dirt = .22f, Wear = .6f,  Bump = .9f, Tint = Color.white },
            new Spec { Name = "CP_Ceiling", Tex = "CP_CeilingRibs", Source = "VO_Env_Ceiling", Scale = 3f, Metallic = .45f, Smooth = .22f, Dirt = .15f, Wear = .3f,  Bump = 1f,  Tint = new Color(.9f,.9f,.92f,1) },
        };

        [MenuItem("Tools/Iron Requiem/Visual Pass/F2 M1 - Campaign materials")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("IRON REQUIEM VisualPass: stop Play Mode first."); return; }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => EditorSceneManager.GetSceneByPath(s.path).isDirty)) { Debug.LogError("IRON REQUIEM VisualPass: save open scenes first."); return; }
            Directory.CreateDirectory(Mats); Directory.CreateDirectory(Backup); Directory.CreateDirectory(VisualPassF1.Report);
            var log = new StringBuilder();

            // 1. importer settings
            foreach (var path in Directory.GetFiles(Tex, "*.png"))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                bool normal = path.EndsWith("_Normal.png"), mask = path.EndsWith("_Mask.png");
                imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = !(normal || mask);
                imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.anisoLevel = 4; imp.maxTextureSize = 1024;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }

            // 2. materials
            var map = new Dictionary<Material, Material>();
            foreach (var s in Specs)
            {
                string src = "Assets/IronRequiem/Art/VisualOverhaul/Materials/" + s.Source + ".mat", dst = Mats + "/" + s.Name + ".mat";
                var source = AssetDatabase.LoadAssetAtPath<Material>(src);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(dst);
                if (!mat) { mat = new Material(source); AssetDatabase.CreateAsset(mat, dst); }
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + s.Tex + "_Albedo.png"));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + s.Tex + "_Normal.png"));
                mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + s.Tex + "_Mask.png"));
                mat.SetColor("_BaseColor", s.Tint);
                mat.SetFloat("_Metallic", s.Metallic); mat.SetFloat("_Smoothness", s.Smooth); mat.SetFloat("_BumpScale", s.Bump);
                Toggle(mat, "_UseTriplanar", "_TRIPLANAR", true); mat.SetFloat("_TriplanarScale", s.Scale); mat.SetFloat("_TriplanarSharpness", 8);
                Toggle(mat, "_UseNormalMap", "_NORMALMAP", true);
                Toggle(mat, "_UseDirt", "_DIRT", true); mat.SetFloat("_DirtAmount", s.Dirt); mat.SetFloat("_DirtScale", 4f); mat.SetFloat("_DirtFloorHeight", 1.0f);
                Toggle(mat, "_UseEdgeWear", "_EDGEWEAR", true); mat.SetFloat("_WearAmount", s.Wear); mat.SetFloat("_WearFromMask", 1f);
                Toggle(mat, "_UsePanels", "_PANELS", false); Toggle(mat, "_UseRim", "_RIM", false);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                map[source] = mat;
                log.AppendLine("MAT " + dst + " <- " + src + " triplanar " + s.Scale + "m");
            }
            var bunkerCeiling = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Environment/Bunker/Bunker_Ceiling.mat");
            if (bunkerCeiling) map[bunkerCeiling] = map.Values.First(m => m.name == "CP_Ceiling");
            AssetDatabase.SaveAssets();

            // 3. re-point campaign rooms only
            try
            {
                foreach (var name in Scenes)
                {
                    string path = Rooms + name + ".unity";
                    if (!File.Exists(Path.Combine(Backup, name + ".unity"))) foreach (var f in new[] { path, path + ".meta" }) File.Copy(f, Path.Combine(Backup, Path.GetFileName(f)), true);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int swaps = 0;
                    foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
                    {
                        var mats = r.sharedMaterials; bool changed = false;
                        for (int i = 0; i < mats.Length; i++) if (mats[i] && map.TryGetValue(mats[i], out var rep)) { mats[i] = rep; changed = true; swaps++; }
                        if (changed) { Undo.RecordObject(r, "F2 materials"); r.sharedMaterials = mats; PrefabUtility.RecordPrefabInstancePropertyModifications(r); }
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    log.AppendLine("SCENE " + name + " swaps=" + swaps);
                }
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            File.WriteAllText(VisualPassF1.Report + "/f2-m1-materials.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass F2 M1:\n" + log);
        }


        // M2 (user request 09/11): (a) the rooms have openings that showed the skybox/outside of the map, and
        // (b) the falls had nothing below. (a) Room scenes drop the skybox, the session camera clears to near-black,
        // and each room gets an inward-facing dark steel shell 8 m outside its geometry (6 quads, no colliders,
        // no shadows, static). (b) Every GapFallRecovery trigger gets an emissive lava pool at its mid height.
        [MenuItem("Tools/Iron Requiem/Visual Pass/F2 M2 - Close voids + lava in falls")]
        public static void VoidsAndLava()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => EditorSceneManager.GetSceneByPath(s.path).isDirty)) { Debug.LogError("IRON REQUIEM VisualPass: save open scenes first."); return; }
            var log = new StringBuilder();
            Directory.CreateDirectory(Backup); File.Copy("Assets/IronRequiem/Scenes/RequiemCampaign.unity", Path.Combine(Backup, "RequiemCampaign.before-m2.unity"), true);
            var wall = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/CP_Wall.mat");
            var shellMat = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/CP_VoidShell.mat");
            if (!shellMat) { shellMat = new Material(wall); AssetDatabase.CreateAsset(shellMat, Mats + "/CP_VoidShell.mat"); }
            shellMat.SetColor("_BaseColor", new Color(.32f, .32f, .34f, 1)); Toggle(shellMat, "_UseDirt", "_DIRT", false); Toggle(shellMat, "_UseEdgeWear", "_EDGEWEAR", false);
            var lavaMat = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/CP_Lava.mat");
            if (!lavaMat) { lavaMat = new Material(wall); AssetDatabase.CreateAsset(lavaMat, Mats + "/CP_Lava.mat"); }
            lavaMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/CP_Lava_Albedo.png"));
            lavaMat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/CP_Lava_Normal.png"));
            lavaMat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/CP_Lava_Mask.png"));
            lavaMat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/CP_Lava_Emission.png"));
            lavaMat.SetColor("_BaseColor", Color.white); lavaMat.SetFloat("_Metallic", 0); lavaMat.SetFloat("_Smoothness", .55f);
            Toggle(lavaMat, "_UseTriplanar", "_TRIPLANAR", true); lavaMat.SetFloat("_TriplanarScale", 4f);
            Toggle(lavaMat, "_UseDirt", "_DIRT", false); Toggle(lavaMat, "_UseEdgeWear", "_EDGEWEAR", false);
            Toggle(lavaMat, "_UsePanels", "_PANELS", true); lavaMat.SetColor("_EmissionColor", new Color(3f, .8f, .12f, 1)); lavaMat.SetFloat("_PulseSpeed", .35f); lavaMat.SetFloat("_PulseAmount", .3f);
            EditorUtility.SetDirty(shellMat); EditorUtility.SetDirty(lavaMat); AssetDatabase.SaveAssets();
            var geometryMats = new HashSet<string> { "CP_Wall", "CP_Floor", "CP_Ceiling", "VO_Env_RustMetal" };
            try
            {
                foreach (var name in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(Rooms + name + ".unity", OpenSceneMode.Single);
                    log.AppendLine("SCENE " + name + " skybox " + (RenderSettings.skybox ? RenderSettings.skybox.name : "none") + " -> none");
                    RenderSettings.skybox = null;
                    foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "VisualPass_VoidShell" || g.name == "VisualPass_LavaPools")) Object.DestroyImmediate(old);
                    var rs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.sharedMaterial && geometryMats.Contains(r.sharedMaterial.name)).ToArray();
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); b.Expand(16f);
                        var shell = new GameObject("VisualPass_VoidShell"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shell, scene);
                        void Face(string n, Vector3 pos, Vector3 euler, Vector2 size)
                        {
                            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>());
                            q.name = n; q.transform.SetParent(shell.transform, false); q.transform.position = pos; q.transform.eulerAngles = euler; q.transform.localScale = new Vector3(size.x, size.y, 1);
                            var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = shellMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                            GameObjectUtility.SetStaticEditorFlags(q, StaticEditorFlags.BatchingStatic);
                        }
                        var c = b.center; var e = b.extents; var sz = b.size;
                        Face("East", new Vector3(b.max.x, c.y, c.z), new Vector3(0, 90, 0), new Vector2(sz.z, sz.y));
                        Face("West", new Vector3(b.min.x, c.y, c.z), new Vector3(0, -90, 0), new Vector2(sz.z, sz.y));
                        Face("North", new Vector3(c.x, c.y, b.max.z), new Vector3(0, 0, 0), new Vector2(sz.x, sz.y));
                        Face("South", new Vector3(c.x, c.y, b.min.z), new Vector3(0, 180, 0), new Vector2(sz.x, sz.y));
                        Face("Top", new Vector3(c.x, b.max.y, c.z), new Vector3(-90, 0, 0), new Vector2(sz.x, sz.z));
                        Face("Bottom", new Vector3(c.x, b.min.y, c.z), new Vector3(90, 0, 0), new Vector2(sz.x, sz.z));
                        log.AppendLine("  shell bounds " + b);
                    }
                    var gaps = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GapFallRecovery>(true)).ToArray();
                    if (gaps.Length > 0)
                    {
                        var pools = new GameObject("VisualPass_LavaPools"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pools, scene);
                        foreach (var gap in gaps)
                        {
                            var bc = gap.GetComponent<BoxCollider>(); var gb = bc.bounds;
                            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>());
                            q.name = "Lava_" + gap.name; q.transform.SetParent(pools.transform, false);
                            q.transform.position = new Vector3(gb.center.x, gb.center.y, gb.center.z); q.transform.eulerAngles = new Vector3(90, 0, 0);
                            q.transform.localScale = new Vector3(gb.size.x + 1f, gb.size.z + 1f, 1);
                            var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = lavaMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            GameObjectUtility.SetStaticEditorFlags(q, StaticEditorFlags.BatchingStatic);
                            log.AppendLine("  lava " + gap.name + " trigger " + gb + " -> y=" + gb.center.y.ToString("F2"));
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                // session camera background
                var session = EditorSceneManager.OpenScene("Assets/IronRequiem/Scenes/RequiemCampaign.unity", OpenSceneMode.Single);
                foreach (var cam in session.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Where(c => c.name == "Main Camera"))
                {
                    log.AppendLine("CAMERA " + cam.name + " clear " + cam.clearFlags + " -> SolidColor");
                    Undo.RecordObject(cam, "Void background"); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.012f, .012f, .016f, 1);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                }
                EditorSceneManager.MarkSceneDirty(session); EditorSceneManager.SaveScene(session);
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            File.WriteAllText(VisualPassF1.Report + "/f2-m2-voids-lava.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass F2 M2:\n" + log);
        }


        // D1 (user report 09/11): seam between floor and door at sector 2, doors popping, odd lighting on room change.
        // - A 18 cm steel sill under every airlock door (closes the floor seam, collider included).
        // - CampaignSession gets SectorLightingBlend (eases ambient/fog between scenes) and the session scene's own
        //   lighting becomes a dim neutral Trilight + fog (it was Flat .55 bright, shown while sectors swap).
        // - Each room grade volume gets VolumeFadeIn. CP_Ceiling is brightened (ceilings read black).
        // Door slide motion itself is runtime code (AirlockDoorMotion, called from CampaignFlow).
        [MenuItem("Tools/Iron Requiem/Visual Pass/F2 D1 - Door sills + transition lighting")]
        public static void DoorsAndTransitions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => EditorSceneManager.GetSceneByPath(s.path).isDirty)) { Debug.LogError("IRON REQUIEM VisualPass: save open scenes first."); return; }
            Directory.CreateDirectory(Backup); File.Copy("Assets/IronRequiem/Scenes/RequiemCampaign.unity", Path.Combine(Backup, "RequiemCampaign.before-d1.unity"), true);
            var log = new StringBuilder();
            var ceiling = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/CP_Ceiling.mat");
            if (ceiling) { ceiling.SetColor("_BaseColor", new Color(2.2f, 2.2f, 2.3f, 1)); EditorUtility.SetDirty(ceiling); }
            var sillMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/VisualOverhaul/Materials/VO_Env_RustMetal.mat");
            try
            {
                var session = EditorSceneManager.OpenScene("Assets/IronRequiem/Scenes/RequiemCampaign.unity", OpenSceneMode.Single);
                var airlocks = session.GetRootGameObjects().FirstOrDefault(g => g.name == "PersistentAirlocks");
                foreach (var old in session.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "VisualPass_DoorSill").Select(t => t.gameObject).ToArray()) Object.DestroyImmediate(old);
                var doorNames = new HashSet<string> { "Room01ExitDoor", "Room02EntryDoor", "SourceDoor", "DestinationDoor", "InitialLoadingDoor" };
                foreach (var door in airlocks.GetComponentsInChildren<Transform>(true).Where(t => doorNames.Contains(t.name)).ToArray())
                {
                    var r = door.GetComponent<Renderer>(); if (!r) continue; var b = r.bounds;
                    bool thinX = b.size.x < b.size.z;
                    var sill = GameObject.CreatePrimitive(PrimitiveType.Cube); sill.name = "VisualPass_DoorSill";
                    sill.transform.SetParent(door.parent, true);
                    sill.transform.position = new Vector3(b.center.x, b.min.y + .02f - .09f, b.center.z);
                    sill.transform.rotation = Quaternion.identity;
                    sill.transform.localScale = thinX ? new Vector3(3f, .18f, b.size.z + .4f) : new Vector3(b.size.x + .4f, .18f, 3f);
                    if (sill.transform.parent) { var ls = sill.transform.parent.lossyScale; sill.transform.localScale = new Vector3(sill.transform.localScale.x / ls.x, sill.transform.localScale.y / ls.y, sill.transform.localScale.z / ls.z); }
                    var mr = sill.GetComponent<MeshRenderer>(); mr.sharedMaterial = sillMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(sill, StaticEditorFlags.BatchingStatic);
                    log.AppendLine("SILL " + door.name + " bottom=" + b.min.y.ToString("F2") + " at " + sill.transform.position);
                }
                var flow = session.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CampaignFlow>(true)).FirstOrDefault();
                if (flow && !flow.GetComponent<SectorLightingBlend>()) { flow.gameObject.AddComponent<SectorLightingBlend>(); log.AppendLine("SectorLightingBlend on " + flow.name); }
                log.AppendLine("SESSION lighting before " + RenderSettings.ambientMode + " " + RenderSettings.ambientSkyColor + " fog=" + RenderSettings.fog);
                RenderSettings.skybox = null; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientIntensity = 1;
                RenderSettings.ambientSkyColor = new Color(.27f, .28f, .31f); RenderSettings.ambientEquatorColor = new Color(.19f, .2f, .22f); RenderSettings.ambientGroundColor = new Color(.19f, .2f, .22f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = new Color(.06f, .065f, .075f); RenderSettings.fogDensity = .016f;
                EditorSceneManager.MarkSceneDirty(session); EditorSceneManager.SaveScene(session);

                foreach (var name in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(Rooms + name + ".unity", OpenSceneMode.Single);
                    var grade = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "VisualPass_SectorGrade");
                    if (grade && !grade.GetComponent<VolumeFadeIn>()) { grade.AddComponent<VolumeFadeIn>(); log.AppendLine("VolumeFadeIn " + name); }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
            File.WriteAllText(VisualPassF1.Report + "/f2-d1-doors-transitions.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass F2 D1:\n" + log);
        }


        // S1: set dressing + shredder threat (09/11). Everything static-batched, shared materials, no lights, no shadows
        // casters (the game is CPU-bound). Rebuildable: the tool deletes its own VisualPass_Dressing root first.
        struct Room { public string Scene, Title, Sub; public float[] EastSkip; }
        static readonly Room[] DressRooms =
        {
            new Room { Scene = "Room01_Foundry",  Title = "SECTOR 01", Sub = "FUNDICIÓN",  EastSkip = new[] { 17f, 33f } },
            new Room { Scene = "Room02_Breach",   Title = "SECTOR 02", Sub = "BRECHA",     EastSkip = new[] { -16f, -4f } },
            new Room { Scene = "Room03_Assembly", Title = "SECTOR 03", Sub = "MONTAJE",    EastSkip = new float[0] },
            new Room { Scene = "Room04_Reactor",  Title = "SECTOR 04", Sub = "REACTOR",    EastSkip = new float[0] },
        };

        [MenuItem("Tools/Iron Requiem/Visual Pass/F2 S1 - Set dressing + shredder FX")]
        public static void Dressing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => EditorSceneManager.GetSceneByPath(s.path).isDirty)) { Debug.LogError("IRON REQUIEM VisualPass: save open scenes first."); return; }
            var log = new StringBuilder();
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/VisualOverhaul/Materials/VO_Env_RustMetal.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/CP_Cable.mat");
            if (!dark) { dark = new Material(AssetDatabase.LoadAssetAtPath<Material>(Mats + "/CP_Wall.mat")); AssetDatabase.CreateAsset(dark, Mats + "/CP_Cable.mat"); }
            dark.SetColor("_BaseColor", new Color(.12f, .12f, .13f, 1)); dark.SetFloat("_Smoothness", .55f); Toggle(dark, "_UseDirt", "_DIRT", false); Toggle(dark, "_UseEdgeWear", "_EDGEWEAR", false);
            var warn = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/CP_WarningStrobe.mat");
            if (!warn) { warn = new Material(dark); AssetDatabase.CreateAsset(warn, Mats + "/CP_WarningStrobe.mat"); }
            warn.SetColor("_BaseColor", new Color(.35f, .05f, .03f, 1));
            warn.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/IronRequiem/Art/VisualOverhaul/Textures/VO_Mask_PanelsOnly.png"));
            warn.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            Toggle(warn, "_UseTriplanar", "_TRIPLANAR", false); Toggle(warn, "_UseNormalMap", "_NORMALMAP", false);
            Toggle(warn, "_UsePanels", "_PANELS", true); warn.SetColor("_EmissionColor", new Color(5f, .35f, .08f, 1)); warn.SetFloat("_PulseSpeed", 2.4f); warn.SetFloat("_PulseAmount", .95f);
            EditorUtility.SetDirty(dark); EditorUtility.SetDirty(warn); AssetDatabase.SaveAssets();
            var sparkSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_HitSparks.prefab");
            var sparkMat = sparkSource ? sparkSource.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(r => r.sharedMaterial).FirstOrDefault(m => m) : null;
            var font = AssetDatabase.FindAssets("t:TMP_FontAsset LiberationSans SDF").Select(g => AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(f => f);
            int objects = 0;
            GameObject Box(Transform parent, string n, Vector3 pos, Vector3 size, Material m, Quaternion? rot = null, PrimitiveType type = PrimitiveType.Cube)
            {
                var g = GameObject.CreatePrimitive(type); Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = n;
                g.transform.SetParent(parent, false); g.transform.position = pos; g.transform.rotation = rot ?? Quaternion.identity; g.transform.localScale = size;
                var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic); objects++; return g;
            }
            try
            {
                foreach (var room in DressRooms)
                {
                    var scene = EditorSceneManager.OpenScene(Rooms + room.Scene + ".unity", OpenSceneMode.Single);
                    foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "VisualPass_Dressing")) Object.DestroyImmediate(old);
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.sharedMaterial).ToArray();
                    var floors = all.Where(r => r.sharedMaterial.name == "CP_Floor").ToArray(); var ceilings = all.Where(r => r.sharedMaterial.name == "CP_Ceiling").ToArray();
                    if (floors.Length == 0 || ceilings.Length == 0) { log.AppendLine("SKIP " + room.Scene); continue; }
                    var fb = floors[0].bounds; foreach (var r in floors) fb.Encapsulate(r.bounds);
                    float floorY = floors.Max(r => r.bounds.max.y); var high = ceilings.Where(r => r.bounds.min.y > floorY + 3f).ToArray(); float ceilY = high.Length > 0 ? high.Min(r => r.bounds.min.y) : floorY + 8f;
                    var root = new GameObject("VisualPass_Dressing"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene); objects = 0;
                    float x0 = fb.min.x + .45f, x1 = fb.max.x - .45f, z0 = fb.min.z + 1f, z1 = fb.max.z - 1f;
                    // wall pipes (two runs per long wall) + brackets, split around east-wall doors
                    foreach (var side in new[] { -1, 1 })
                    {
                        float x = side < 0 ? x0 : x1;
                        var spans = new List<Vector2> { new Vector2(z0, z1) };
                        if (side > 0 && room.EastSkip.Length == 2) spans = new List<Vector2> { new Vector2(z0, room.EastSkip[0]), new Vector2(room.EastSkip[1], z1) };
                        foreach (var sp in spans.Where(v => v.y - v.x > 2))
                        {
                            float len = sp.y - sp.x, mid = (sp.x + sp.y) * .5f;
                            foreach (var (h, rad) in new[] { (ceilY - .55f, .32f), (ceilY - 1.05f, .22f) })
                                Box(root.transform, "Pipe", new Vector3(x - side * (rad * .5f), h, mid), new Vector3(rad, len * .5f, rad), metal, Quaternion.Euler(90, 0, 0), PrimitiveType.Cylinder);
                            Box(root.transform, "CableTray", new Vector3(x - side * .12f, ceilY - 1.6f, mid), new Vector3(.18f, .12f, len), dark);
                            for (float z = sp.x + 1; z < sp.y; z += 4) Box(root.transform, "PipeBracket", new Vector3(x - side * .25f, ceilY - .8f, z), new Vector3(.5f, 1.1f, .12f), dark);
                        }
                    }
                    // ceiling cross beams
                    for (float z = z0 + 2; z < z1; z += 6) Box(root.transform, "CeilingBeam", new Vector3(fb.center.x, ceilY - .25f, z), new Vector3(fb.size.x - .2f, .5f, .35f), metal);
                    // corner risers
                    foreach (var cx in new[] { x0 + .2f, x1 - .2f }) foreach (var cz in new[] { z0, z1 })
                        Box(root.transform, "Riser", new Vector3(cx, (floorY + ceilY) * .5f, cz), new Vector3(.28f, (ceilY - floorY) * .5f, .28f), metal, null, PrimitiveType.Cylinder);
                    // stencil sign on the west wall, facing the room
                    if (font)
                    {
                        foreach (var (text, y, size, color) in new[] { (room.Title, floorY + 4.6f, 9f, new Color(1f, .45f, .08f)), (room.Sub, floorY + 3.3f, 5f, new Color(.85f, .85f, .82f)) })
                        {
                            var g = new GameObject("Sign_" + text); g.transform.SetParent(root.transform, false);
                            g.transform.position = new Vector3(fb.min.x + .06f, y, fb.center.z); g.transform.rotation = Quaternion.Euler(0, -90, 0);
                            var tmp = g.AddComponent<TMPro.TextMeshPro>(); tmp.font = font; tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.characterSpacing = 8; tmp.rectTransform.sizeDelta = new Vector2(30, 3);
                            objects++;
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    log.AppendLine("DRESS " + room.Scene + " floorY=" + floorY + " ceilY=" + ceilY + " floor=" + fb + " objects=" + objects);
                }
                // Pursuit05: shredder threat
                var pursuit = EditorSceneManager.OpenScene(Rooms + "Pursuit05.unity", OpenSceneMode.Single);
                foreach (var old in pursuit.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name.StartsWith("VisualPass_")).Where(t => t.name == "VisualPass_ShredSparks" || t.name == "VisualPass_Strobe").Select(t => t.gameObject).ToArray()) Object.DestroyImmediate(old);
                int sparks = 0, strobes = 0;
                foreach (var saw in pursuit.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "GiantSaw").ToArray())
                {
                    var r = saw.GetComponentInChildren<Renderer>(); var b = r ? r.bounds : new Bounds(saw.position, Vector3.one * 4);
                    var go = new GameObject("VisualPass_ShredSparks"); go.transform.SetParent(saw, true); go.transform.position = new Vector3(b.center.x, b.min.y + .3f, b.center.z - b.extents.z * .9f); go.transform.rotation = Quaternion.Euler(-60, 180, 0);
                    var ps = go.AddComponent<ParticleSystem>(); var main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f); main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f); main.startSize = new ParticleSystem.MinMaxCurve(.025f, .06f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(3f, 1.4f, .3f), new Color(2.5f, .5f, .1f)); main.gravityModifier = 1.6f; main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var em = ps.emission; em.rateOverTime = 45;
                    var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28; shape.radius = Mathf.Max(.3f, b.extents.x * .6f);
                    var col = ps.colorOverLifetime; col.enabled = true; var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, .3f, .1f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); col.color = grad;
                    var pr = go.GetComponent<ParticleSystemRenderer>(); pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = .04f; pr.lengthScale = 1.5f; pr.maxParticleSize = .04f; pr.sharedMaterial = sparkMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    sparks++;
                }
                foreach (var frame in pursuit.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "ShredderSideFrame" || t.name == "MachineFrame").ToArray())
                {
                    var r = frame.GetComponent<Renderer>(); if (!r) continue; var b = r.bounds;
                    var s = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(s.GetComponent<Collider>()); s.name = "VisualPass_Strobe";
                    s.transform.SetParent(frame, true); s.transform.position = new Vector3(b.center.x, b.max.y + .18f, b.center.z); s.transform.rotation = Quaternion.identity;
                    var ls = frame.lossyScale; s.transform.localScale = new Vector3(.45f / ls.x, .3f / ls.y, .45f / ls.z);
                    var mr = s.GetComponent<MeshRenderer>(); mr.sharedMaterial = warn; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; strobes++;
                }
                EditorSceneManager.MarkSceneDirty(pursuit); EditorSceneManager.SaveScene(pursuit);
                log.AppendLine("PURSUIT sparks=" + sparks + " strobes=" + strobes + " sparkMat=" + (sparkMat ? sparkMat.name : "none") + " font=" + (font ? font.name : "none"));
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            File.WriteAllText(VisualPassF1.Report + "/f2-s1-dressing.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass F2 S1:\n" + log);
        }

        static void Toggle(Material m, string prop, string keyword, bool on)
        {
            m.SetFloat(prop, on ? 1 : 0);
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }
    }
}
