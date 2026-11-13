using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 11/11/2026 — Hangar main menu ("capsule C-04"). Rebuilds the 3D backdrop of IntroMenu and restyles its
    /// existing industrial UI (RequiemMenuView, LoadSceneButton, options and credits are reused, not replaced).
    /// Idempotent: everything it creates lives under "HangarSet" or is named "HG_*" and is rebuilt on each run.
    /// IntroMenu.unity is backed up before the first change. Run: Tools/Iron Requiem/Menu/Build Hangar Intro.
    public static class HangarMenuAuthoring
    {
        const string Menu = "Tools/Iron Requiem/Menu/";
        internal const string ScenePath = "Assets/FPS/Scenes/IntroMenu.unity";
        const string ArtDir = "Assets/IronRequiem/Art/Menu";
        const string MatDir = ArtDir + "/Materials";
        const string TexDir = ArtDir + "/Textures";
        const string RobotDir = "Assets/IronRequiem/Art/Protagonist/CapsuleRobot";
        // 12/11/2026: rigged version of the same Tripo protagonist (Mixamo-style bones); used by the menu capsule and the Room00 wake-up cinematic.
        internal const string RigDir = "Assets/IronRequiem/Art/Protagonist/CapsuleRobotRig";
        internal const string RigModel = RigDir + "/CapsuleRobotRig.fbx";
        const string PrefabDir = "Assets/IronRequiem/Prefabs/Menu";
        const string Report = "Tools/IronRequiem/Reports/HangarMenu";
        const string Backup = "Tools/IronRequiem/Backups/20261111-hangar-menu";
        static readonly Color Cyan = new Color(.12f, .82f, .91f), Orange = new Color(1f, .55f, .14f);
        static StringBuilder log;
        static TMP_FontAsset font;
        /// Shared-builder entry for other tools (Room00_Hangar): sets the log and the TMP font used by the capsule plate.
        internal static void PrepareShared(StringBuilder sharedLog, TMP_FontAsset plateFont) { log = sharedLog; font = plateFont; }

        // Tuning kept here so screenshot iterations only touch numbers.
        static readonly Vector3 CamPos = new Vector3(0f, 1.45f, -3.1f);
        static readonly Vector3 CamEuler = new Vector3(-4f, -5f, 0f);
        const float CamFov = 50f;
        static readonly Vector3 PodPos = new Vector3(1.75f, 1.22f, .6f);
        static readonly Vector3 PodEuler = new Vector3(36f, 30f, 0f);
        const float RobotLength = 2.25f;
        static readonly Vector3 RobotEuler = new Vector3(0, 180, 0); // verified by capture: visor faces the pod front (-Z) / camera
        const float BridgeY = 5.3f, BridgeZ = 19f, BridgeHalf = 26f;
        const float BeltSpeed = 1.3f, BeltTile = 1.2f;
        // Belt look (12/11/2026): dark figures with a warm, low wash from below; no blown highlights.
        const float WashIntensity = 5.5f, WashRange = 7f, WashAngle = 54f;       // one shared spot per belt section (3)
        const float BloomThreshold = 1.1f, BloomIntensity = .6f, BloomScatter = .65f, BloomClamp = 10f; // clamp stops HDR spikes/NaN blobs
        const float GlowCap = 2.0f;                                              // max HDR channel for Unlit lamp parts
        const float FogDensity = .022f;
        // Cannon-charge/FX meshes that the enemy scripts normally hide; without those scripts they would glow permanently.
        static readonly string[] FxRendererNames = { "HotCore", "Halo", "Energy", "Spindle", "Rays", "Charge", "Muzzle", "Flash", "Flare", "Beam", "Indicator", "EyeBall", "Eye_", "Glow", "Orb", "VFX" }; // + eyes/indicators: belt robots are powered down

        [MenuItem(Menu + "Build Hangar Intro (IntroMenu)")]
        public static void Build()
        {
            log = new StringBuilder("Hangar intro build " + DateTime.Now + "\n");
            Directory.CreateDirectory(Report);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene open: " + SceneManager.GetSceneAt(i).path + ". Save or discard it first; nothing was changed.");
                Directory.CreateDirectory(Backup);
                var bak = Path.Combine(Backup, "IntroMenu.unity.bak");
                if (!File.Exists(bak)) { File.Copy(ScenePath, bak); File.Copy(ScenePath + ".meta", bak + ".meta", true); log.AppendLine("backup " + bak); }
                var runBak = "Tools/IronRequiem/Backups/" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "-hangar-build";
                Directory.CreateDirectory(runBak);
                foreach (var f in new[] { ScenePath, ArtDir + "/HG_MenuVolume.asset", PrefabDir + "/MenuWalker_T07.prefab", PrefabDir + "/MenuWalker_Carronero.prefab" })
                    if (File.Exists(f)) File.Copy(f, Path.Combine(runBak, Path.GetFileName(f)), true);
                log.AppendLine("run backup " + runBak);

                ConfigureImports();
                var m = BuildMaterials();
                var walkers = BuildWalkerPrefabs(m);
                RobotRigidSkin.Prepare(log);   // v30: same rigid robot (cut head/neck/hands + collars) as the Room00 cinematic

                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                font = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).Select(t => t.font).FirstOrDefault(f => f);
                foreach (var root in scene.GetRootGameObjects()) if (root.name == "HangarSet") Object.DestroyImmediate(root);

                var set = new GameObject("HangarSet").transform;
                var cam = SetupCamera(set);
                var lighting = BuildEnvironment(set, m);
                var pod = BuildCapsule(set, m, out var podFx);
                RobotRigidSkin.Apply(podFx.model, log, m.trim);
                var fx = BuildFx(set, m);
                var patrol = BuildPatrol(set, m, walkers, cam, podFx, lighting);
                var ui = BuildUi(scene, patrol, podFx, cam);
                Wire(set, podFx, lighting, patrol, ui, cam, fx);
                ConfigureRenderSettings();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                log.AppendLine("saved " + ScenePath);
                File.WriteAllText(Report + "/build.txt", log.ToString());
                Debug.Log("[HangarMenu] build ok\n" + log);
            }
            catch (Exception e)
            {
                log.AppendLine("ERROR " + e);
                File.WriteAllText(Report + "/build.txt", log.ToString());
                Debug.LogError("[HangarMenu] " + e);
            }
        }

        [MenuItem(Menu + "Debug detonate walker (play)")]
        static void DebugDetonate()
        {
            var p = Object.FindAnyObjectByType<BridgePatrol>();
            if (p) { p.DebugDetonateFirst(); Debug.Log("[HangarMenu] detonate kills=" + p.Kills); }
        }

        [MenuItem(Menu + "Debug spawn walker centre (play)")]
        static void DebugSpawn() { var p = Object.FindAnyObjectByType<BridgePatrol>(); if (p) p.DebugSpawnCentre(); }

        [MenuItem(Menu + "Debug report flicker (play)")]
        static void DebugFlicker()
        {
            var sb = new StringBuilder();
            var mpb = new MaterialPropertyBlock();
            foreach (var f in Object.FindObjectsByType<MenuFlicker>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(f);
                var views = so.FindProperty("emissive"); var em = so.FindProperty("emission").colorValue;
                sb.Append($"{f.name} mode={f.CurrentMode} level={f.Level:F2} emission={em} views={views.arraySize}");
                if (views.arraySize > 0 && views.GetArrayElementAtIndex(0).objectReferenceValue is Renderer r)
                {
                    var mat = r.sharedMaterial; r.GetPropertyBlock(mpb);
                    sb.Append($" r={r.name} vis={r.isVisible} mat={mat?.name} shader={mat?.shader.name} kwEmission={mat?.IsKeywordEnabled("_EMISSION")} matEm={(mat && mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor").ToString() : "n/a")} base={(mat && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").ToString() : "n/a")} mpbEm={(mpb.HasColor("_EmissionColor") ? mpb.GetColor("_EmissionColor").ToString() : "none")} kws={string.Join(" ", mat ? mat.shaderKeywords : new string[0])}");
                }
                sb.AppendLine();
            }
            sb.AppendLine("GPUResidentDrawer? " + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "none"));
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/flicker.txt", sb.ToString()); Debug.Log("[HangarMenu] flicker\n" + sb);
        }

        /// Detonates the robot closest to the middle and captures a few frames later (play mode runs slowly when the Editor is unfocused).
        [MenuItem(Menu + "Debug detonate + capture (play)")]
        static void DebugDetonateCapture() { DebugCapture(); DebugDetonate(); }

        /// Lists every bright thing in the menu: lights under HangarSet, walker renderers with their shaders and
        /// emission, and any renderer whose material carries an HDR colour above 1.5 (bloom candidates).
        [MenuItem(Menu + "Debug report emissive (play)")]
        static void DebugEmissive()
        {
            var sb = new StringBuilder();
            var set = GameObject.Find("HangarSet");
            if (!set) { Debug.LogWarning("[HangarMenu] no HangarSet"); return; }
            sb.AppendLine("== lights");
            foreach (var l in set.GetComponentsInChildren<Light>(true))
                sb.AppendLine($"{NodePath(l.transform)} type={l.type} on={l.enabled && l.gameObject.activeInHierarchy} I={l.intensity:F2} range={l.range:F1} angle={l.spotAngle:F0} col={l.color}");
            sb.AppendLine("== walker renderers (active walkers only)");
            foreach (var w in set.GetComponentsInChildren<BridgeWalker>(true))
            {
                if (!w.gameObject.activeInHierarchy) continue;
                foreach (var r in w.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    foreach (var m in r.sharedMaterials) if (m) sb.AppendLine($"{w.name}/{r.name} [{r.GetType().Name}] vis={r.isVisible} mat={m.name} shader={m.shader.name} hdrMax={MaxHdr(m):F2}");
                }
                foreach (var c in w.GetComponentsInChildren<Component>(true))
                    if (c is ParticleSystem || c is Light || c is TrailRenderer || c is LineRenderer || c.GetType().Name.Contains("LensFlare")) sb.AppendLine($"  !! {w.name} has {c.GetType().Name} on {c.name}");
            }
            sb.AppendLine("== bright materials anywhere in HangarSet (hdrMax > 1.5)");
            foreach (var r in set.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                foreach (var m in r.sharedMaterials) if (m && MaxHdr(m) > 1.5f) sb.AppendLine($"{NodePath(r.transform)} mat={m.name} shader={m.shader.name} hdrMax={MaxHdr(m):F2}");
            }
            var cam = Camera.main; var data = cam ? cam.GetUniversalAdditionalCameraData() : null;
            var vol = set.GetComponentInChildren<Volume>();
            if (vol && vol.sharedProfile && vol.sharedProfile.TryGet<Bloom>(out var bloom))
                sb.AppendLine($"== bloom threshold={bloom.threshold.value} intensity={bloom.intensity.value} scatter={bloom.scatter.value} clamp={bloom.clamp.value} stopNaN={(data ? data.stopNaN.ToString() : "-")} fog={RenderSettings.fogDensity}");
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/emissive.txt", sb.ToString()); Debug.Log("[HangarMenu] emissive report written");
        }

        static string NodePath(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

        /// Largest component among the material's colour properties (HDR emission, unlit base colour...).
        static float MaxHdr(Material m)
        {
            float best = 0; var sh = m.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                var n = sh.GetPropertyName(i);
                bool emissive = n.IndexOf("Emiss", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!emissive && n != "_BaseColor" && n != "_Color" && n != "_TintColor") continue;
                if (emissive && m.HasProperty("_EMISSION") == false && !m.IsKeywordEnabled("_EMISSION") && sh.name.Contains("Universal Render Pipeline/Lit")) continue;
                var c = m.GetColor(n); best = Mathf.Max(best, c.maxColorComponent);
            }
            return best;
        }

        [MenuItem(Menu + "Debug report walkers (play)")]
        static void DebugWalkers()
        {
            var sb = new StringBuilder();
            var cam = Camera.main;
            foreach (var w in Object.FindObjectsByType<BridgeWalker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"{w.name} alive={w.Alive} active={w.gameObject.activeSelf} pos={w.transform.position} screen={(cam ? cam.WorldToViewportPoint(w.Center).ToString("F2") : "-")}");
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/walkers.txt", sb.ToString()); Debug.Log("[HangarMenu] walkers\n" + sb);
        }

        [MenuItem(Menu + "Debug launch sequence (play)")]
        static void DebugLaunch()
        {
            var s = Object.FindAnyObjectByType<HangarLaunchSequence>();
            if (s) s.Play(() => Debug.Log("[HangarMenu] launch finished (scene load skipped in debug)"));
        }

        /// Renders the menu (3D + UI) at 1920x1080 regardless of the Game view size; files go to the bridge folder.
        [MenuItem(Menu + "Debug capture 1080p (play)")]
        static void DebugCapture()
        {
            var cam = Camera.main; if (!cam) return;
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.isRootCanvas && c.transform.Find("IndustrialShell"));
            int n = EditorPrefs.GetInt("IR.HangarCapture", 0) + 1; EditorPrefs.SetInt("IR.HangarCapture", n);
            var file = "Tools/IronRequiem/Bridge/hgc_" + n + ".png";
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var mode = canvas ? canvas.renderMode : RenderMode.ScreenSpaceOverlay; var plane = canvas ? canvas.planeDistance : 100;
            if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
            cam.targetTexture = rt;
            int frames = 0;
            void Tick()
            {
                if (++frames < 4) return;
                EditorApplication.update -= Tick;
                try
                {
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
                    RenderTexture.active = prev; File.WriteAllBytes(file, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                    Debug.Log("[HangarMenu] capture " + file);
                }
                finally
                {
                    if (cam) cam.targetTexture = null;
                    if (canvas) { canvas.renderMode = mode; canvas.worldCamera = null; canvas.planeDistance = plane; }
                    rt.Release(); Object.DestroyImmediate(rt);
                }
            }
            EditorApplication.update += Tick;
        }

        // ------------------------------------------------------------------ assets
        internal static void ConfigureImports()
        {
            AssetDatabase.Refresh();
            void Tex(string path, bool normal, bool srgb = true, bool sprite = false, bool clamp = false, int max = 2048)
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!ti) { log.AppendLine("missing texture " + path); return; }
                bool dirty = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
                if (sprite && ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!normal && ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; dirty = true; }
                if (ti.maxTextureSize != max) { ti.maxTextureSize = max; dirty = true; }
                var wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (ti.wrapMode != wrap) { ti.wrapMode = wrap; dirty = true; }
                if (!ti.alphaIsTransparency && !normal && path.Contains("Banner") | path.Contains("Soft") | path.Contains("Smoke") | path.Contains("Gradient")) { ti.alphaIsTransparency = true; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            Tex(RobotDir + "/CapsuleRobot_BaseColor.png", false);
            Tex(RobotDir + "/CapsuleRobot_Normal.png", true);
            Tex(RobotDir + "/CapsuleRobot_MetalSmooth.png", false, false);
            Tex(RobotDir + "/CapsuleRobot_Glow.png", false);
            Tex(TexDir + "/Hangar_Belt.png", false, true, false, false, 512);
            Tex(TexDir + "/Hangar_Chevron.png", false, true, false, false, 256);
            Tex(TexDir + "/Hangar_Plate_Albedo.png", false, true, false, false, 1024);
            Tex(TexDir + "/Hangar_Plate_Normal.png", true, true, false, false, 1024);
            Tex(TexDir + "/Hangar_Floor_Albedo.png", false, true, false, false, 512);
            Tex(TexDir + "/Hangar_Floor_Normal.png", true, true, false, false, 512);
            Tex(TexDir + "/Hangar_Hazard.png", false, true, false, false, 512);
            Tex(TexDir + "/Hangar_Banner.png", false, true, false, true, 512);
            Tex(TexDir + "/Hangar_SoftDot.png", false, true, false, true, 128);
            Tex(TexDir + "/Hangar_Smoke.png", false, true, false, true, 256);
            Tex(TexDir + "/Hangar_UIGradient.png", false, true, true, true, 256);
            Tex(RigDir + "/CapsuleRobotRig_BaseColor.png", false);
            Tex(RigDir + "/CapsuleRobotRig_Normal.png", true);
            Tex(RigDir + "/CapsuleRobotRig_MetalSmooth.png", false, false);
            Tex(RigDir + "/CapsuleRobotRig_Glow.png", false);
            var rig = AssetImporter.GetAtPath(RigModel) as ModelImporter;
            if (rig && (rig.materialImportMode != ModelImporterMaterialImportMode.None || rig.importAnimation || rig.animationType != ModelImporterAnimationType.Generic || rig.importCameras || rig.importLights))
            {
                // Generic rig, no clips: the cinematic poses the bones procedurally (WakeUpRobotPose)
                rig.materialImportMode = ModelImporterMaterialImportMode.None; rig.importAnimation = false; rig.animationType = ModelImporterAnimationType.Generic;
                rig.importCameras = false; rig.importLights = false; rig.importBlendShapes = false;
                rig.SaveAndReimport();
            }
            var mi = AssetImporter.GetAtPath(RobotDir + "/CapsuleRobot.fbx") as ModelImporter;
            if (mi && (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation || mi.importCameras || mi.importLights))
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false;
                mi.SaveAndReimport();
            }
            if (!mi) log.AppendLine("missing model " + RobotDir + "/CapsuleRobot.fbx");
        }

        internal sealed class Mats
        {
            public Material robotRig, robotRigGlow;
            public Material chevron, belt, robotGlow, podShell, plate, plateDark, floor, trim, glass, robot, hazard, banner, city, window, cyanGlow, orangeGlow, redGlow, lampWarm, bed, rubber, debris;
            public Material fxAdd, fxSmoke, fxSpark, fxDebris, fxDust;
            public Texture2D soft, smoke;
        }

        internal static Mats BuildMaterials()
        {
            Directory.CreateDirectory(MatDir);
            T Load<T>(string p) where T : Object => AssetDatabase.LoadAssetAtPath<T>(p);
            var plateA = Load<Texture2D>(TexDir + "/Hangar_Plate_Albedo.png"); var plateN = Load<Texture2D>(TexDir + "/Hangar_Plate_Normal.png");
            var floorA = Load<Texture2D>(TexDir + "/Hangar_Floor_Albedo.png"); var floorN = Load<Texture2D>(TexDir + "/Hangar_Floor_Normal.png");
            var m = new Mats
            {
                soft = Load<Texture2D>(TexDir + "/Hangar_SoftDot.png"), smoke = Load<Texture2D>(TexDir + "/Hangar_Smoke.png")
            };
            m.plate = Lit("HG_Plate", new Color(.36f, .38f, .4f), .35f, 1f, plateA, plateN, new Vector2(1, 1), albedoSmooth: true);
            m.plateDark = Lit("HG_PlateDark", new Color(.13f, .14f, .16f), .4f, 1f, plateA, plateN, new Vector2(2, 2), albedoSmooth: true);
            m.floor = Lit("HG_Floor", new Color(.42f, .44f, .46f), .3f, 1f, floorA, floorN, new Vector2(18, 18), albedoSmooth: true);
            m.podShell = Lit("HG_PodShell", new Color(.24f, .27f, .3f), .45f, .9f, plateA, plateN, new Vector2(1, 2), albedoSmooth: true);
            m.trim = Lit("HG_Trim", new Color(.07f, .075f, .08f), .5f, .55f);
            m.bed = Lit("HG_Bed", new Color(.05f, .06f, .07f), .4f, .35f);
            m.rubber = Lit("HG_Hose", new Color(.04f, .04f, .045f), 0f, .45f);
            m.debris = Lit("HG_Debris", new Color(.2f, .19f, .18f), .7f, .4f);
            m.hazard = Lit("HG_Hazard", Color.white, .3f, .4f, Load<Texture2D>(TexDir + "/Hangar_Hazard.png"));
            m.city = Lit("HG_City", new Color(.025f, .03f, .038f), .2f, .2f);
            // Light sources are Unlit HDR colour: no _EMISSION keyword to lose, bloom picks them up, MenuFlicker drives _BaseColor.
            m.window = Glow("HG_Window", new Color(.42f, .24f, .1f));
            m.cyanGlow = Glow("HG_CyanGlow", new Color(.25f, 1.7f, 2.1f));
            m.orangeGlow = Glow("HG_OrangeGlow", new Color(GlowCap, .85f, .2f));
            m.redGlow = Glow("HG_RedGlow", new Color(GlowCap, .18f, .1f));
            m.lampWarm = Glow("HG_LampWarm", new Color(GlowCap, 1.7f, 1.25f));
            m.glass = Lit("HG_Glass", new Color(.45f, .7f, .78f, .1f), 0f, .62f); Transparent(m.glass);
            m.banner = Lit("HG_Banner", Color.white, 0f, .2f, Load<Texture2D>(TexDir + "/Hangar_Banner.png")); Cutout(m.banner);
            m.robot = Lit("HG_ProtagonistRobot", Color.white, 1f, .9f, Load<Texture2D>(RobotDir + "/CapsuleRobot_BaseColor.png"), Load<Texture2D>(RobotDir + "/CapsuleRobot_Normal.png"));
            var ms = Load<Texture2D>(RobotDir + "/CapsuleRobot_MetalSmooth.png");
            if (ms) { m.robot.SetTexture("_MetallicGlossMap", ms); m.robot.EnableKeyword("_METALLICSPECGLOSSMAP"); m.robot.SetFloat("_Smoothness", .45f); }
            m.robotRig = Lit("HG_ProtagonistRig", Color.white, 1f, .9f, Load<Texture2D>(RigDir + "/CapsuleRobotRig_BaseColor.png"), Load<Texture2D>(RigDir + "/CapsuleRobotRig_Normal.png"));
            var rms = Load<Texture2D>(RigDir + "/CapsuleRobotRig_MetalSmooth.png");
            if (rms) { m.robotRig.SetTexture("_MetallicGlossMap", rms); m.robotRig.EnableKeyword("_METALLICSPECGLOSSMAP"); m.robotRig.SetFloat("_Smoothness", .45f); }
            m.robotRigGlow = ParticleMat("HG_ProtagonistRigGlow", Load<Texture2D>(RigDir + "/CapsuleRobotRig_Glow.png"), new Color(1, 1, 1, 1), true);
            m.belt = Lit("HG_Belt", new Color(.55f, .55f, .58f), .1f, 1f, Load<Texture2D>(TexDir + "/Hangar_Belt.png"), null, new Vector2(BridgeHalf * 2 / BeltTile, 1), albedoSmooth: true);
            m.chevron = Glow("HG_Chevron", new Color(1.3f, .55f, .12f));
            m.chevron.SetTexture("_BaseMap", Load<Texture2D>(TexDir + "/Hangar_Chevron.png")); m.chevron.SetTextureScale("_BaseMap", new Vector2(BridgeHalf * 2 / 1.0f, 1));
            m.robotGlow = ParticleMat("HG_ProtagonistGlow", Load<Texture2D>(RobotDir + "/CapsuleRobot_Glow.png"), new Color(1, 1, 1, 1), true);
            m.fxAdd = ParticleMat("HG_FxFire", m.soft, new Color(1f, .62f, .25f, 1f), true);
            m.fxSpark = ParticleMat("HG_FxSpark", m.soft, new Color(1f, .7f, .3f, 1f), true);
            m.fxSmoke = ParticleMat("HG_FxSmoke", m.smoke, new Color(.16f, .16f, .17f, .85f), false);
            m.fxDust = ParticleMat("HG_FxDust", m.soft, new Color(.8f, .85f, .9f, .35f), false);
            m.fxDebris = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/HG_FxDebris.mat");
            if (!m.fxDebris) { m.fxDebris = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")); AssetDatabase.CreateAsset(m.fxDebris, MatDir + "/HG_FxDebris.mat"); }
            m.fxDebris.SetColor("_BaseColor", new Color(.22f, .2f, .19f));
            EditorUtility.SetDirty(m.fxDebris);
            AssetDatabase.SaveAssets();
            return m;
        }

        internal static Material Lit(string name, Color c, float metal, float smooth, Texture2D albedo = null, Texture2D normal = null, Vector2? tiling = null, Color? emission = null, bool albedoSmooth = false)
        {
            var path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetColor("_BaseColor", c); mat.SetTexture("_BaseMap", albedo); mat.SetFloat("_Metallic", metal); mat.SetFloat("_Smoothness", smooth);
            mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            if (normal) { mat.SetTexture("_BumpMap", normal); mat.SetFloat("_BumpScale", 1); mat.EnableKeyword("_NORMALMAP"); } else { mat.SetTexture("_BumpMap", null); mat.DisableKeyword("_NORMALMAP"); }
            mat.SetFloat("_SmoothnessTextureChannel", albedoSmooth ? 1 : 0);
            if (albedoSmooth) mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); else mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (emission.HasValue) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emission.Value); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static Material Glow(string name, Color hdr)
        {
            var path = MatDir + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader; mat.SetColor("_BaseColor", hdr); mat.SetTexture("_BaseMap", null);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_BlendModePreserveSpecular", 0); // highlight scaled by alpha: no glare streak over the robot
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
        }

        internal static void Cutout(Material m)
        {
            m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0);
            m.renderQueue = (int)RenderQueue.AlphaTest; m.SetOverrideTag("RenderType", "TransparentCutout");
            EditorUtility.SetDirty(m);
        }

        internal static Material ParticleMat(string name, Texture2D tex, Color c, bool additive)
        {
            var path = MatDir + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", additive ? 2 : 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ walkers (gameplay-free copies of campaign enemies)
        static readonly Type[] KeepTypes = { typeof(Transform), typeof(Animator), typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer), typeof(LODGroup), typeof(CarroneroMotion) };

        static List<GameObject> BuildWalkerPrefabs(Mats m)
        {
            Directory.CreateDirectory(PrefabDir);
            var list = new List<GameObject>();
            void Add(string[] sources, string name, float speed, float stride, string label, float blast)
            {
                var src = sources.Select(AssetDatabase.LoadAssetAtPath<GameObject>).FirstOrDefault(g => g);
                if (!src) { log.AppendLine("walker source missing for " + name); return; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    inst.name = name; inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    foreach (var t in inst.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    // Remove gameplay in dependency order (dependants first) so Unity never refuses a removal.
                    for (int pass = 0; pass < 12; pass++)
                    {
                        var all = inst.GetComponentsInChildren<Component>(true).Where(c => c).ToArray();
                        var required = new HashSet<(GameObject, Type)>();
                        foreach (var c in all)
                            foreach (RequireComponent rc in c.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                                foreach (var t in new[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 }) if (t != null) required.Add((c.gameObject, t));
                        int removed = 0;
                        foreach (var c in all)
                        {
                            if (KeepTypes.Any(k => k.IsInstanceOfType(c))) continue;
                            var type = c.GetType();
                            if (required.Any(r => r.Item1 == c.gameObject && r.Item2.IsAssignableFrom(type) && all.Any(o => o && o != c && o.gameObject == c.gameObject && RequiresType(o, r.Item2)))) continue;
                            Object.DestroyImmediate(c, true); removed++;
                        }
                        if (removed == 0) break;
                    }
                    CleanWalkerVisuals(inst, name, m);
                    var renderers = inst.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
                    foreach (var r in renderers) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true; }
                    var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.up, Vector3.one * 2);
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    var box = inst.AddComponent<BoxCollider>(); box.isTrigger = true;
                    box.center = inst.transform.InverseTransformPoint(b.center); box.size = Vector3.Scale(b.size, new Vector3(1.35f, 1.15f, 1.35f));
                    foreach (var lg in inst.GetComponentsInChildren<LODGroup>(true))
                    {
                        var lods = lg.GetLODs();
                        if (lods.Length > 0) { lods[lods.Length - 1].screenRelativeTransitionHeight = .0005f; lg.SetLODs(lods); }
                    }
                    var animator = inst.GetComponentInChildren<Animator>(true);
                    if (animator) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
                    var walker = inst.AddComponent<BridgeWalker>();
                    walker.Configure(animator, speed, stride, label, Mathf.Clamp(b.size.y / 2.4f, .6f, 1.8f) * blast);
                    var path = PrefabDir + "/" + name + ".prefab";
                    var saved = PrefabUtility.SaveAsPrefabAsset(inst, path);
                    list.Add(saved);
                    log.AppendLine($"walker {name} from {AssetDatabase.GetAssetPath(src)} size={b.size} animator={(animator ? animator.runtimeAnimatorController?.name : "none")} comps={string.Join(",", inst.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name).Distinct())}");
                }
                finally { Object.DestroyImmediate(inst); }
            }
            Add(new[] { "Assets/IronRequiem/Prefabs/Campaign/T07_Mech.prefab", "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab" }, "MenuWalker_T07", 1.7f, 1.5f, "T-07", 1f);
            Add(new[] { "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab", "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab" }, "MenuWalker_Carronero", 2.8f, .55f, "CARROÑERO", .8f);
            return list;
        }

        /// Belt robots are powered down. Keep only the art the source prefab shows (enabled + active) and drawn
        /// with lit materials; remove hidden legacy art (old T-07 armour, BasicRobot EyeBall, old SawBot skin),
        /// eyes/indicators and FX (Unlit, Particles, additive, HDR > 1.5, FX names). LODGroups are rebuilt from
        /// the survivors, because a LODGroup re-enables every renderer it lists, hidden or not. Kept materials
        /// get an emission-free copy (Prefabs/Menu/Materials/MW_*), so eyes and glowing joints stay dark.
        static void CleanWalkerVisuals(GameObject inst, string name, Mats m)
        {
            var keep = new HashSet<Renderer>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string why = null;
                if (!r.enabled || !r.gameObject.activeInHierarchy) why = "hidden in source";
                else if (FxRendererNames.Any(n => r.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)) why = "fx name";
                else if (r.sharedMaterials.Any(x => !x || IsFxShader(x.shader))) why = "fx/eye material (unlit/particles/additive)";
                // lit parts with strong emission (saw edge, joints) stay, but get the dark copy below
                if (why == null) { keep.Add(r); continue; }
                log.AppendLine($"  removed {name}/{r.name} ({why}) mats={string.Join(",", r.sharedMaterials.Where(x => x).Select(x => x.name))}");
                var go = r.gameObject; var mf = go.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r, true); if (mf) Object.DestroyImmediate(mf, true);
            }
            foreach (var lg in inst.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lg.GetLODs();
                for (int i = 0; i < lods.Length; i++) lods[i].renderers = lods[i].renderers.Where(r => r && keep.Contains(r)).ToArray();
                lg.SetLODs(lods);
            }
            Directory.CreateDirectory(PrefabDir + "/Materials");
            foreach (var r in keep)
                r.sharedMaterials = r.sharedMaterials.Select(DarkCopy).ToArray();
        }

        static bool IsFxShader(Shader s)
        {
            var n = s ? s.name : "";
            return n.Contains("Unlit") || n.Contains("Particles") || n.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0 || n.Contains("Sprites") || n.Contains("Skybox");
        }

        /// Emission-free copy of a material for the powered-down belt robots (cached by name).
        static Material DarkCopy(Material src)
        {
            if (!src) return src;
            bool emissive = false; var sh = src.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                var n = sh.GetPropertyName(i);
                if ((n.IndexOf("Emiss", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0) && sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color && src.GetColor(n).maxColorComponent > 0) emissive = true;
            }
            if (!emissive && !src.IsKeywordEnabled("_EMISSION")) return src;
            var path = PrefabDir + "/Materials/MW_" + src.name + ".mat";
            var dst = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!dst) { dst = new Material(src); AssetDatabase.CreateAsset(dst, path); } else dst.CopyPropertiesFromMaterial(src);
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                var n = sh.GetPropertyName(i); var t = sh.GetPropertyType(i);
                bool em = n.IndexOf("Emiss", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!em) continue;
                if (t == UnityEngine.Rendering.ShaderPropertyType.Color) dst.SetColor(n, Color.black);
                else if (t == UnityEngine.Rendering.ShaderPropertyType.Float || t == UnityEngine.Rendering.ShaderPropertyType.Range) dst.SetFloat(n, 0);
                else if (t == UnityEngine.Rendering.ShaderPropertyType.Texture) dst.SetTexture(n, null);
            }
            dst.DisableKeyword("_EMISSION"); dst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(dst);
            return dst;
        }

        static bool RequiresType(Component c, Type t)
        {
            foreach (RequireComponent rc in c.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if (rc.m_Type0 == t || rc.m_Type1 == t || rc.m_Type2 == t) return true;
            return false;
        }

        // ------------------------------------------------------------------ scene
        internal static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero); go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        internal static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero); go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        internal static Transform Group(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }

        internal static Light MakeLight(string name, Transform parent, LightType type, Vector3 pos, Vector3 euler, Color c, float intensity, float range, float angle = 60, bool shadows = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>(); l.type = type; l.color = c; l.intensity = intensity; l.range = range; l.spotAngle = angle; l.innerSpotAngle = angle * .55f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None; l.renderMode = LightRenderMode.Auto; l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        internal static MenuFlicker Flicker(GameObject host, Light[] lights, Renderer[] views, Color emission, MenuFlicker.Mode mode, float outage, int seed, float hz = 1.2f)
        {
            var f = host.AddComponent<MenuFlicker>(); f.Configure(lights, views, emission, mode, outage, seed, hz); return f;
        }

        static Camera SetupCamera(Transform set)
        {
            var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.scene.path == ScenePath);
            if (!cam) { var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); go.tag = "MainCamera"; cam = go.GetComponent<Camera>(); }
            cam.transform.SetParent(null);
            cam.transform.SetPositionAndRotation(CamPos, Quaternion.Euler(CamEuler));
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.1f, .13f, .17f, 1);
            cam.fieldOfView = CamFov; cam.nearClipPlane = .05f; cam.farClipPlane = 260;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.stopNaN = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.antialiasingQuality = AntialiasingQuality.High;
            foreach (var old in cam.GetComponents<MenuCameraRig>()) Object.DestroyImmediate(old);
            cam.gameObject.AddComponent<MenuCameraRig>();
            // Volume
            var profilePath = ArtDir + "/HG_MenuVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            T Get<T>() where T : VolumeComponent { if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, profile); } c.active = true; return c; }
            var bloom = Get<Bloom>(); bloom.threshold.Override(BloomThreshold); bloom.intensity.Override(BloomIntensity); bloom.scatter.Override(BloomScatter); bloom.clamp.Override(BloomClamp);
            var vig = Get<Vignette>(); vig.intensity.Override(.38f); vig.smoothness.Override(.5f); vig.color.Override(Color.black);
            var col = Get<ColorAdjustments>(); col.postExposure.Override(.1f); col.contrast.Override(14f); col.saturation.Override(-14f); col.colorFilter.Override(new Color(.93f, .98f, 1f));
            var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.Neutral);
            var grain = Get<FilmGrain>(); grain.type.Override(FilmGrainLookup.Medium2); grain.intensity.Override(.28f); grain.response.Override(.7f);
            var ca = Get<ChromaticAberration>(); ca.intensity.Override(.12f);
            var lift = Get<LiftGammaGain>(); lift.lift.Override(new Vector4(.97f, 1f, 1.05f, 0f)); lift.gamma.Override(new Vector4(1f, 1f, 1f, .04f));
            EditorUtility.SetDirty(profile);
            var vol = new GameObject("HG_PostVolume").AddComponent<Volume>(); vol.transform.SetParent(set, false); vol.isGlobal = true; vol.priority = 20; vol.sharedProfile = profile;
            return cam;
        }

        internal sealed class Lighting { public MenuFlicker key, podStrips, bridgeA, bridgeB, bridgeC, beacon; public List<MenuFlicker> all = new List<MenuFlicker>(); }

        internal static Lighting BuildEnvironment(Transform set, Mats m)
        {
            var L = new Lighting();
            var env = Group("Environment", set);
            // Floor + hall shell
            var floor = Prim(PrimitiveType.Plane, "Floor", env, new Vector3(0, 0, 10), new Vector3(9, 1, 9), m.floor, null, false);
            floor.GetComponent<MeshRenderer>().receiveShadows = true;
            Box("WallLeft", env, new Vector3(-17, 9, 10), new Vector3(1, 18, 70), m.plateDark, null, false);
            Box("WallRight", env, new Vector3(17, 9, 10), new Vector3(1, 18, 70), m.plateDark, null, false);
            for (int i = 0; i < 6; i++)
            {
                float z = -2 + i * 7;
                Box("ColumnL" + i, env, new Vector3(-15.8f, 9, z), new Vector3(1.4f, 18, 1.4f), m.plate, null, false);
                Box("ColumnR" + i, env, new Vector3(15.8f, 9, z), new Vector3(1.4f, 18, 1.4f), m.plate, null, false);
                Box("Girder" + i, env, new Vector3(0, 15.5f, z), new Vector3(33, .9f, .6f), m.trim, null, false);
            }
            Box("CeilingSpine", env, new Vector3(0, 16.3f, 12), new Vector3(1.2f, .8f, 50), m.trim, null, false);
            // Foreground clutter behind the menu panel (frames the left side, catches rim light)
            var clutter = Group("Clutter", env);
            Box("CrateA", clutter, new Vector3(-3.4f, .55f, 1.8f), new Vector3(1.3f, 1.1f, 1.1f), m.plateDark, new Vector3(0, 18, 0));
            Box("CrateB", clutter, new Vector3(-3.1f, 1.55f, 1.9f), new Vector3(.9f, .9f, .9f), m.plate, new Vector3(0, -9, 0));
            Box("CrateC", clutter, new Vector3(-5.2f, .7f, 4.5f), new Vector3(1.6f, 1.4f, 1.4f), m.plateDark, new Vector3(0, 33, 0));
            for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cylinder, "Pipe" + i, clutter, new Vector3(-7f + i * .45f, 6f, 7.5f), new Vector3(.32f, 6f, .32f), i % 2 == 0 ? m.trim : m.plateDark, null, false);
            Prim(PrimitiveType.Cylinder, "PipeRun", clutter, new Vector3(4.6f, 3.4f, 3.4f), new Vector3(.4f, 3.4f, .4f), m.trim, null, false);
            Prim(PrimitiveType.Cylinder, "PipeRunB", clutter, new Vector3(5.1f, 3.4f, 3.6f), new Vector3(.25f, 3.4f, .25f), m.rubber, null, false);
            Box("Catwalk", clutter, new Vector3(6.5f, 3.1f, 6f), new Vector3(4, .15f, 9), m.trim, null, false);

            // Bridge
            var bridge = Group("Bridge", set); bridge.localPosition = new Vector3(0, BridgeY, BridgeZ);
            Box("Deck", bridge, Vector3.zero, new Vector3(BridgeHalf * 2, .35f, 3.2f), m.plate, null, false);
            Box("BeamFront", bridge, new Vector3(0, -.45f, -1.2f), new Vector3(BridgeHalf * 2, .55f, .3f), m.trim, null, false);
            Box("BeamBack", bridge, new Vector3(0, -.45f, 1.2f), new Vector3(BridgeHalf * 2, .55f, .3f), m.trim, null, false);
            foreach (float z in new[] { -1.55f, 1.55f })
            {
                Box("RailTop" + z, bridge, new Vector3(0, 1.05f, z), new Vector3(BridgeHalf * 2, .07f, .07f), m.trim, null, false);
                Box("RailMid" + z, bridge, new Vector3(0, .6f, z), new Vector3(BridgeHalf * 2, .05f, .05f), m.trim, null, false);
                for (float x = -BridgeHalf; x <= BridgeHalf; x += 2.2f) Box("Post", bridge, new Vector3(x, .55f, z), new Vector3(.08f, 1.05f, .08f), m.trim, null, false);
            }
            // Conveyor: belt body + scrolling surface, hazard guards, spinning roller caps along the front edge
            Box("BeltBody", bridge, new Vector3(0, .215f, 0), new Vector3(BridgeHalf * 2, .08f, 2.3f), m.rubber, null, false);
            var beltTop = Prim(PrimitiveType.Quad, "BeltSurface", bridge, new Vector3(0, .257f, 0), new Vector3(BridgeHalf * 2, 2.2f, 1), m.belt, new Vector3(90, 0, 0), false);
            // chevron strip on the front skirt: the belt surface can't be seen from below, the arrows show it moving
            var chevrons = Prim(PrimitiveType.Quad, "ChevronStrip", bridge, new Vector3(0, -.02f, -1.665f), new Vector3(BridgeHalf * 2, .2f, 1), m.chevron, null, false);
            var conveyorFx = bridge.gameObject.AddComponent<ConveyorBelt>();
            conveyorFx.Configure(new[] { beltTop.GetComponent<Renderer>(), chevrons.GetComponent<Renderer>() }, new[] { BeltTile, 1.0f }, BeltSpeed);
            // display lights from below the front edge so the carried robots read against the fog
            foreach (var x in new[] { -6.5f, 0f, 6.5f })
            {
                var from = new Vector3(x, -.8f, -3.4f); var to = new Vector3(x, 1.5f, 0);
                MakeLight("RobotWash", bridge, LightType.Spot, from, Quaternion.LookRotation(to - from).eulerAngles, new Color(1f, .74f, .52f), WashIntensity, WashRange, WashAngle);
            }
            foreach (float z in new[] { -1.25f, 1.25f }) Box("BeltGuard" + z, bridge, new Vector3(0, .33f, z), new Vector3(BridgeHalf * 2, .2f, .06f), m.hazard, null, false);
            for (float x = -BridgeHalf + .6f; x < BridgeHalf; x += 1.1f)
            {
                var cap = Prim(PrimitiveType.Cylinder, "Roller", bridge, new Vector3(x, -.3f, -1.62f), new Vector3(.2f, .05f, .2f), m.plate, new Vector3(90, 0, 0), false);
                Box("RollerMark", cap.transform, new Vector3(0, -1.05f, 0), new Vector3(.9f, .4f, .15f), m.hazard, null, false);
                cap.AddComponent<MenuSpin>().Configure(Vector3.forward, -BeltSpeed / .1f * Mathf.Rad2Deg);
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -BridgeHalf + 3 + i * 6;
                Box("Truss" + i, bridge, new Vector3(x, -1.2f, 0), new Vector3(.18f, 1.6f, 2.6f), m.trim, new Vector3(0, 0, i % 2 == 0 ? 35 : -35), false);
            }
            foreach (float x in new[] { -11f, 3f, 13f }) Box("Pillar", bridge, new Vector3(x, -BridgeY * .5f - .2f, 0), new Vector3(.9f, BridgeY - .4f, .9f), m.plateDark, null, false);
            // hidden deck collider: debris bounces on it; ignored by the menu click ray
            var deckCol = new GameObject("DeckCollider"); deckCol.transform.SetParent(bridge, false); deckCol.layer = 2;
            var bc = deckCol.AddComponent<BoxCollider>(); bc.size = new Vector3(BridgeHalf * 2, .35f, 3.2f);
            var floorCol = new GameObject("FloorCollider"); floorCol.transform.SetParent(env, false); floorCol.layer = 2; floorCol.transform.localPosition = new Vector3(0, -.25f, 10);
            floorCol.AddComponent<BoxCollider>().size = new Vector3(90, .5f, 90);

            // Bridge lamps (orange sodium, one failing)
            int seed = 3;
            foreach (var x in new[] { -13f, -3f, 7f, 17f })
            {
                var lamp = Group("BridgeLamp", bridge); lamp.localPosition = new Vector3(x, 0, 1.55f);
                Box("Pole", lamp, new Vector3(0, 1.6f, 0), new Vector3(.1f, 3.2f, .1f), m.trim, null, false);
                Box("Arm", lamp, new Vector3(0, 3.15f, -.6f), new Vector3(.1f, .1f, 1.2f), m.trim, null, false);
                var head = Box("Head", lamp, new Vector3(0, 3.05f, -1.15f), new Vector3(.5f, .12f, .28f), m.lampWarm, null, false);
                var l = MakeLight("Spot", lamp, LightType.Spot, new Vector3(0, 2.95f, -1.15f), new Vector3(90, 0, 0), new Color(1f, .58f, .25f), 14f, 10f, 85f);
                var f = Flicker(lamp.gameObject, new[] { l }, new[] { head.GetComponent<Renderer>() }, new Color(GlowCap, 1.1f, .38f), seed == 5 ? MenuFlicker.Mode.Failing : MenuFlicker.Mode.Hum, .35f, seed++);
                L.all.Add(f);
            }
            // Banners hanging above the bridge
            foreach (var x in new[] { -4.5f, 5.5f })
            {
                var q = Prim(PrimitiveType.Quad, "Banner", bridge, new Vector3(x, 4.6f, .2f), new Vector3(1.7f, 3.6f, 1), m.banner, new Vector3(0, 180, 0), false);
                q.transform.localRotation = Quaternion.Euler(0, 0, 0);
                Box("BannerRod", bridge, new Vector3(x, 6.45f, .2f), new Vector3(2f, .06f, .06f), m.trim, null, false);
                Box("BannerCable", bridge, new Vector3(x, 8.2f, .2f), new Vector3(.03f, 3.5f, .03f), m.trim, null, false);
            }
            MakeLight("BannerWash", bridge, LightType.Spot, new Vector3(.5f, 1.2f, -3.5f), new Vector3(-38, 0, 0), new Color(1f, .45f, .3f), 6f, 12f, 70f);
            // Distant city silhouettes (fogged), a few lit windows
            var city = Group("City", set);
            var rnd = new System.Random(11);
            for (int i = 0; i < 26; i++)
            {
                float x = -70 + i * 5.6f + (float)rnd.NextDouble() * 3, z = 42 + (float)rnd.NextDouble() * 34, h = 10 + (float)rnd.NextDouble() * 34, w = 4 + (float)rnd.NextDouble() * 6;
                var b = Box("Tower" + i, city, new Vector3(x, h * .5f - 1, z), new Vector3(w, h, w), m.city, null, false);
                int wins = rnd.Next(0, 3);
                for (int k = 0; k < wins; k++)
                    Prim(PrimitiveType.Quad, "Win", city, new Vector3(x + ((float)rnd.NextDouble() - .5f) * w * .8f, 2 + (float)rnd.NextDouble() * (h - 4), z - w * .5f - .05f), new Vector3(.35f, .6f, 1), m.window, null, false);
            }
            // Hall lights: weak moonlight from beyond the bridge, warm fill, rotating beacon
            var lights = Group("Lights", set);
            var moon = MakeLight("Moon", lights, LightType.Directional, Vector3.zero, new Vector3(28, 200, 0), new Color(.45f, .55f, .75f), .6f, 0);
            moon.shadows = LightShadows.None;
            MakeLight("WarmFill", lights, LightType.Point, new Vector3(-1.8f, .6f, 2.2f), Vector3.zero, new Color(1f, .5f, .2f), 1.6f, 6f);
            MakeLight("BridgeBounce", lights, LightType.Point, new Vector3(0, BridgeY - 1.5f, BridgeZ - 4), Vector3.zero, new Color(.35f, .45f, .6f), 4f, 16f);
            // hanging hall tubes, visible at the top of frame: one dying, two humming
            var hall = new[] { new Vector3(-6f, 6.6f, 9f), new Vector3(.5f, 6.9f, 12.5f), new Vector3(-11.5f, 6.4f, 14f) };
            for (int i = 0; i < hall.Length; i++)
            {
                var root = Group("HallTube" + i, lights); root.localPosition = hall[i];
                Box("Housing", root, new Vector3(0, .08f, 0), new Vector3(2.2f, .14f, .4f), m.trim, null, false);
                var tubeR = Box("Tube", root, Vector3.zero, new Vector3(2f, .06f, .1f), m.lampWarm, null, false).GetComponent<Renderer>();
                Box("Cable", root, new Vector3(-.9f, 2.2f, 0), new Vector3(.02f, 4.3f, .02f), m.trim, null, false);
                Box("CableB", root, new Vector3(.9f, 2.2f, 0), new Vector3(.02f, 4.3f, .02f), m.trim, null, false);
                var spot = MakeLight("Down", root, LightType.Spot, new Vector3(0, -.05f, 0), new Vector3(90, 0, 0), new Color(1f, .82f, .62f), 9f, 10f, 110f);
                L.all.Add(Flicker(root.gameObject, new[] { spot }, new[] { tubeR }, new Color(GlowCap, 1.7f, 1.25f), i == 0 ? MenuFlicker.Mode.Failing : MenuFlicker.Mode.Hum, .4f, 50 + i));
            }
            var beaconRoot = Group("Beacon", lights); beaconRoot.localPosition = new Vector3(14.6f, 11.5f, 8f);
            var beaconHead = Box("BeaconHead", beaconRoot, Vector3.zero, new Vector3(.4f, .3f, .4f), m.redGlow, null, false);
            var beacon = MakeLight("BeaconSpot", beaconRoot, LightType.Spot, Vector3.zero, new Vector3(25, 0, 0), new Color(1f, .12f, .08f), 6f, 22f, 32f);
            beaconRoot.gameObject.AddComponent<MenuSpin>();
            L.beacon = Flicker(beaconHead, new Light[0], new[] { beaconHead.GetComponent<Renderer>() }, new Color(GlowCap, .18f, .1f), MenuFlicker.Mode.Blink, 0, 9, .7f);
            L.all.Add(L.beacon);
            return L;
        }

        internal sealed class PodFx { public Transform root, canopy, robot, model, push, look; public Light visor; public Renderer canopyRenderer; public CapsuleRobotIdle idle; public MenuFlicker key, strips, indicators; public ParticleSystem vent; }

        internal static Transform BuildCapsule(Transform set, Mats m, out PodFx fx)
        {
            fx = new PodFx();
            var pod = Group("Capsule_C04", set); pod.localPosition = PodPos; pod.localRotation = Quaternion.Euler(PodEuler); fx.root = pod;
            // shell (local: Y = long axis, -Z = front/viewer)
            Box("BackShell", pod, new Vector3(0, 0, .45f), new Vector3(1.55f, 2.9f, .22f), m.plateDark);
            Box("Bed", pod, new Vector3(0, -.05f, .31f), new Vector3(1.25f, 2.6f, .06f), m.bed);
            foreach (var s in new[] { -1, 1 })
            {
                Box("Rail" + s, pod, new Vector3(.86f * s, 0, .05f), new Vector3(.22f, 3.0f, .78f), m.podShell);
                Box("RailLip" + s, pod, new Vector3(.8f * s, 0, -.37f), new Vector3(.1f, 2.85f, .06f), m.trim);
                Box("RailBolt" + s, pod, new Vector3(.97f * s, .9f, .05f), new Vector3(.06f, .5f, .5f), m.trim);
            }
            Box("HeadCap", pod, new Vector3(0, 1.62f, .05f), new Vector3(1.95f, .42f, .98f), m.podShell);
            Box("HeadVisor", pod, new Vector3(0, 1.58f, -.45f), new Vector3(1.4f, .18f, .06f), m.trim);
            Box("FootCap", pod, new Vector3(0, -1.66f, .05f), new Vector3(1.95f, .48f, .98f), m.podShell);
            Prim(PrimitiveType.Quad, "Hazard", pod, new Vector3(0, -1.66f, -.445f), new Vector3(1.7f, .3f, 1), m.hazard, new Vector3(0, 0, 0), false);
            // cyan strips along the inner rails + head bar, orange indicators on the right rail
            var strips = new List<Renderer>();
            foreach (var s in new[] { -1, 1 }) strips.Add(Box("Strip" + s, pod, new Vector3(.73f * s, -.05f, -.33f), new Vector3(.035f, 2.45f, .035f), m.cyanGlow, null, false).GetComponent<Renderer>());
            strips.Add(Box("StripHead", pod, new Vector3(0, 1.45f, -.4f), new Vector3(1.1f, .035f, .035f), m.cyanGlow, null, false).GetComponent<Renderer>());
            strips.Add(Box("StripFoot", pod, new Vector3(0, -1.45f, -.4f), new Vector3(1.1f, .035f, .035f), m.cyanGlow, null, false).GetComponent<Renderer>());
            var leds = new List<Renderer>();
            for (int i = 0; i < 4; i++) leds.Add(Box("Led" + i, pod, new Vector3(.88f, .5f - i * .11f, -.395f), new Vector3(.045f, .03f, .015f), m.orangeGlow, null, false).GetComponent<Renderer>());
            var tag = new GameObject("PlateC04"); tag.transform.SetParent(pod, false); tag.transform.localPosition = new Vector3(.86f, -.6f, -.4f); tag.transform.localRotation = Quaternion.identity;
            var tmp = tag.AddComponent<TextMeshPro>(); tmp.font = font; tmp.text = "C-04"; tmp.fontSize = .9f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(.85f, .88f, .86f);
            tmp.rectTransform.sizeDelta = new Vector2(.6f, .2f); tag.transform.localRotation = Quaternion.Euler(0, 0, 90);
            // canopy glass (front half visible; back faces culled)
            var canopy = Prim(PrimitiveType.Cylinder, "Canopy", pod, new Vector3(0, -.02f, -.06f), new Vector3(1.45f, 1.36f, .84f), m.glass, null, false);
            fx.canopy = canopy.transform; fx.canopyRenderer = canopy.GetComponent<Renderer>();
            Box("CanopyFrameTop", pod, new Vector3(0, 1.36f, -.42f), new Vector3(1.5f, .07f, .07f), m.trim, null, false);
            // hoses and support arm
            for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cylinder, "Hose" + i, pod, new Vector3(-.5f + i * .5f, 2.3f, .45f), new Vector3(.12f, .75f, .12f), m.rubber, new Vector3(-20 + i * 8, 0, -12 + i * 12), false);
            Box("Spine", pod, new Vector3(0, -.2f, .85f), new Vector3(.6f, 2.2f, .5f), m.trim);
            var basePlate = Box("PodBase", set, new Vector3(PodPos.x + .35f, .22f, PodPos.z + 1.3f), new Vector3(2.4f, .44f, 2.2f), m.plateDark, new Vector3(0, PodEuler.y, 0));
            Box("PodStrut", set, new Vector3(PodPos.x + .3f, .9f, PodPos.z + 1.35f), new Vector3(.7f, 1.4f, .7f), m.trim, new Vector3(-15, PodEuler.y, 0));
            // protagonist
            var rigged = AssetDatabase.LoadAssetAtPath<GameObject>(RigModel);
            var model = rigged ? rigged : AssetDatabase.LoadAssetAtPath<GameObject>(RobotDir + "/CapsuleRobot.fbx");
            var bodyMat = rigged ? m.robotRig : m.robot; var glowMat = rigged ? m.robotRigGlow : m.robotGlow;
            if (model)
            {
                var holder = Group("Protagonist", pod);
                // measure unparented at the origin (model axes), then fit its height along the pod's long axis
                var robot = (GameObject)PrefabUtility.InstantiatePrefab(model);
                robot.name = "CapsuleRobot"; robot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); robot.transform.localScale = Vector3.one;
                var rs = robot.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rs) { r.sharedMaterials = Enumerable.Repeat(bodyMat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray(); r.shadowCastingMode = ShadowCastingMode.On; }
                foreach (var smr in robot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.updateWhenOffscreen = true; }
                var raw = RendererBounds(rs);
                var size = raw.size; var centre = raw.center;
                // Tripo exports are normally Y-up; a Z-up export is stood up first.
                var upFix = size.z > size.y * 1.2f ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
                float height = upFix == Quaternion.identity ? size.y : size.z;
                log.AppendLine($"robot raw size={size} centre={centre} upFix={(upFix == Quaternion.identity ? "none" : "Z-up")}");
                float scale = RobotLength / Mathf.Max(.001f, height);
                robot.transform.SetParent(holder, false);
                var rot = Quaternion.Euler(RobotEuler) * upFix;
                robot.transform.localRotation = rot; robot.transform.localScale = Vector3.one * scale;
                robot.transform.localPosition = new Vector3(0, -.05f, .02f) - rot * (centre * scale);
                fx.robot = holder; fx.model = robot.transform;
                var visor = MakeLight("VisorGlow", pod, LightType.Point, new Vector3(0, 1.0f, -.55f), Vector3.zero, new Color(.35f, .9f, 1f), 1.4f, 1.1f); fx.visor = visor;
                // additive overlay of the armour's cyan lines (same mesh): off while the robot sleeps
                var overlays = new List<Renderer>();
                foreach (var mf in robot.GetComponentsInChildren<MeshFilter>(true))
                {
                    var o = new GameObject("GlowOverlay"); o.transform.SetParent(mf.transform, false);
                    o.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var ovr = o.AddComponent<MeshRenderer>(); ovr.sharedMaterial = glowMat; ovr.shadowCastingMode = ShadowCastingMode.Off; ovr.receiveShadows = false; ovr.enabled = false;
                    overlays.Add(ovr);
                }
                // skinned: a second renderer on the same bones draws the additive line pass
                foreach (var smr in robot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var o = new GameObject("GlowOverlay"); o.transform.SetParent(smr.transform.parent, false);
                    o.transform.localPosition = smr.transform.localPosition; o.transform.localRotation = smr.transform.localRotation; o.transform.localScale = smr.transform.localScale;
                    var ovr = o.AddComponent<SkinnedMeshRenderer>(); ovr.sharedMesh = smr.sharedMesh; ovr.bones = smr.bones; ovr.rootBone = smr.rootBone; ovr.localBounds = smr.localBounds;
                    ovr.sharedMaterial = glowMat; ovr.shadowCastingMode = ShadowCastingMode.Off; ovr.receiveShadows = false; ovr.updateWhenOffscreen = true; ovr.enabled = false;
                    overlays.Add(ovr);
                }
                fx.idle = holder.gameObject.AddComponent<CapsuleRobotIdle>(); fx.idle.Configure(robot.transform, visor, overlays.ToArray());
                log.AppendLine($"robot scale={scale:F3} bounds after fit={RendererBounds(rs).size}");
            }
            else log.AppendLine("robot model missing: " + RobotDir + "/CapsuleRobot.fbx");
            // pod lights: failing overhead hangar lamp, cyan interior, indicator LEDs
            var lampRoot = Group("OverheadLamp", set);
            lampRoot.position = pod.TransformPoint(new Vector3(-.4f, 1.2f, -2.2f)) + Vector3.up * 1.6f;
            lampRoot.rotation = Quaternion.LookRotation(pod.position + Vector3.up * .3f - lampRoot.position);
            Box("Housing", lampRoot, new Vector3(0, 0, -.12f), new Vector3(1.3f, .22f, .5f), m.trim, null, false);
            var tube = Box("Tube", lampRoot, new Vector3(0, 0, .02f), new Vector3(1.1f, .07f, .12f), m.lampWarm, null, false);
            var chain = Box("Chain", lampRoot, Vector3.zero, new Vector3(.03f, 3f, .03f), m.trim, null, false).transform; chain.SetPositionAndRotation(lampRoot.position + Vector3.up * 1.5f, Quaternion.identity);
            var key = MakeLight("KeySpot", lampRoot, LightType.Spot, new Vector3(0, 0, .1f), Vector3.zero, new Color(1f, .87f, .72f), 16f, 11f, 62f, true);
            key.shadowStrength = .85f;
            fx.key = Flicker(lampRoot.gameObject, new[] { key }, new[] { tube.GetComponent<Renderer>() }, new Color(GlowCap, 1.7f, 1.3f), MenuFlicker.Mode.Failing, .3f, 21);
            var cyan = MakeLight("PodCyan", pod, LightType.Point, new Vector3(0, .6f, -1.45f), Vector3.zero, new Color(.25f, .85f, 1f), .9f, 3f);
            MakeLight("PodRim", set, LightType.Point, PodPos + new Vector3(1.7f, 1.6f, 1.6f), Vector3.zero, new Color(.4f, .6f, 1f), 3f, 5.5f);
            fx.strips = Flicker(pod.gameObject, new[] { cyan }, strips.ToArray(), new Color(.15f, 1.1f, 1.4f), MenuFlicker.Mode.Hum, 0, 31);
            var ledHost = new GameObject("Indicators"); ledHost.transform.SetParent(pod, false);
            fx.indicators = Flicker(ledHost, new Light[0], leds.ToArray(), new Color(1.6f, .65f, .14f), MenuFlicker.Mode.Blink, 0, 41, .9f);
            // launch vent steam from both rails
            fx.vent = PS("VentSteam", pod, m.fxSmoke, ps =>
            {
                var main = ps.main; main.duration = 1.6f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f); main.startSize = new ParticleSystem.MinMaxCurve(.35f, .9f);
                main.startColor = new Color(.75f, .8f, .85f, .35f); main.gravityModifier = -.05f; main.maxParticles = 160; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var e = ps.emission; e.rateOverTime = 55; e.SetBursts(new[] { new ParticleSystem.Burst(0, 30) });
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.9f, 2.6f, .1f);
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.4f));
                var c = ps.colorOverLifetime; c.enabled = true; c.color = Fade(new Color(.8f, .85f, .9f));
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.6f, .6f);
            });
            fx.vent.transform.localPosition = new Vector3(0, 0, -.45f); fx.vent.transform.localRotation = Quaternion.Euler(180, 0, 0);
            var push = new GameObject("CameraPushTarget").transform; push.SetParent(pod, false); push.localPosition = new Vector3(0, .98f, -1.05f); fx.push = push;
            var look = new GameObject("CameraLookTarget").transform; look.SetParent(pod, false); look.localPosition = new Vector3(0, .9f, -.12f); fx.look = look;
            return pod;
        }

        internal static Bounds RendererBounds(Renderer[] rs) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }

        internal static ParticleSystem.MinMaxGradient Fade(Color c, float peak = 1)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, .15f), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        internal static ParticleSystem PS(string name, Transform parent, Material mat, Action<ParticleSystem> setup)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var e = ps.emission; e.rateOverTime = 0;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .3f;
            setup(ps);
            return ps;
        }

        sealed class FxSet { public MenuExplosionFx[] explosions; }

        static FxSet BuildFx(Transform set, Mats m)
        {
            var root = Group("Fx", set);
            // ambient dust in front of the camera
            PS("Dust", root, m.fxDust, ps =>
            {
                ps.transform.localPosition = new Vector3(.4f, 1.8f, 1.2f);
                var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.duration = 10; main.startLifetime = 12; main.startSpeed = .03f;
                main.startSize = new ParticleSystem.MinMaxCurve(.008f, .03f); main.maxParticles = 400; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startColor = new Color(.85f, .9f, 1f, .5f);
                var e = ps.emission; e.rateOverTime = 30;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(7, 4, 6);
                var n = ps.noise; n.enabled = true; n.strength = .08f; n.frequency = .2f; n.scrollSpeed = .05f;
                var c = ps.colorOverLifetime; c.enabled = true; c.color = Fade(Color.white, .9f);
            });
            // low steam leak by the pod base
            PS("FloorSteam", root, m.fxSmoke, ps =>
            {
                ps.transform.localPosition = new Vector3(-.9f, .05f, 2.6f); ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.duration = 5; main.startLifetime = new ParticleSystem.MinMaxCurve(4, 6);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .45f); main.startSize = new ParticleSystem.MinMaxCurve(.8f, 1.6f); main.maxParticles = 60;
                main.startColor = new Color(.6f, .66f, .72f, .1f); main.simulationSpace = ParticleSystemSimulationSpace.World;
                var e = ps.emission; e.rateOverTime = 5;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18; sh.radius = .15f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .5f, 1, 2.5f));
                var c = ps.colorOverLifetime; c.enabled = true; c.color = Fade(Color.white, .8f);
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.3f, .3f);
            });
            // explosions (2, round-robin)
            var clips = new[] { "Assets/FPS/Audio/SFX/Enemies/Hoverbot_Death.wav", "Assets/FPS/Audio/SFX/Enemies/Turret_Death.wav" }.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c).ToArray();
            var extra = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VisualPass/VFX/VP_VFX_HoverBot_Explosion.prefab");
            log.AppendLine("explosion clips=" + string.Join(",", clips.Select(c => c.name)) + " extraVfx=" + (extra ? extra.name : "none"));
            var group = new MenuExplosionFx[2];
            for (int i = 0; i < 2; i++)
            {
                var host = Group("Explosion" + i, root); host.localPosition = new Vector3(0, BridgeY + 1, BridgeZ);
                var fire = PS("Fire", host, m.fxAdd, ps =>
                {
                    var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(.8f, 2.1f); main.gravityModifier = -.25f; main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                    var c = ps.colorOverLifetime; c.enabled = true;
                    var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, .95f, .8f), 0), new GradientColorKey(new Color(1f, .5f, .12f), .3f), new GradientColorKey(new Color(.5f, .1f, .03f), 1) },
                        new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0, 1) });
                    c.color = g;
                    var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .4f, 1, 1.4f));
                    var sh = ps.shape; sh.radius = .6f;
                });
                var smoke = PS("Smoke", host, m.fxSmoke, ps =>
                {
                    var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 2.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.2f); main.gravityModifier = -.06f; main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f); main.startColor = new Color(.12f, .11f, .11f, .8f);
                    var c = ps.colorOverLifetime; c.enabled = true; c.color = Fade(Color.white, .85f);
                    var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .7f, 1, 2.2f));
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.5f, .5f);
                    var sh = ps.shape; sh.radius = .7f;
                });
                var sparks = PS("Sparks", host, m.fxSpark, ps =>
                {
                    var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, 1.3f); main.startSpeed = new ParticleSystem.MinMaxCurve(6, 17);
                    main.startSize = new ParticleSystem.MinMaxCurve(.04f, .09f); main.gravityModifier = 1.6f; main.maxParticles = 150; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .8f, .45f), new Color(1f, .45f, .1f));
                    var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .045f; r.lengthScale = 1.2f;
                    var c = ps.colorOverLifetime; c.enabled = true; c.color = Fade(Color.white);
                });
                var debris = PS("Debris", host, m.fxDebris, ps =>
                {
                    var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f); main.startSpeed = new ParticleSystem.MinMaxCurve(4, 10);
                    main.startSize = new ParticleSystem.MinMaxCurve(.08f, .28f); main.gravityModifier = 2.2f; main.maxParticles = 40; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.startRotation3D = true; main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0, 6.28f);
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true; rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-9, 9);
                    var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.quality = ParticleSystemCollisionQuality.Medium; col.bounce = .3f; col.dampen = .45f; col.lifetimeLoss = .15f;
                    var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"); r.alignment = ParticleSystemRenderSpace.Local;
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .5f; sh.rotation = new Vector3(-90, 0, 0);
                });
                foreach (var ps in new[] { fire, smoke, sparks, debris }) { var main = ps.main; main.loop = true; main.playOnAwake = true; main.duration = 1; }
                var flash = MakeLight("Flash", host, LightType.Point, Vector3.zero, Vector3.zero, new Color(1f, .6f, .25f), 0, 14f);
                var audio = host.gameObject.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0; audio.volume = .9f;
                var fx = host.gameObject.AddComponent<MenuExplosionFx>();
                fx.Configure(fire, smoke, sparks, debris, flash, extra, audio, clips);
                group[i] = fx;
            }
            return new FxSet { explosions = group };
        }

        static BridgePatrol BuildPatrol(Transform set, Mats m, List<GameObject> walkerPrefabs, Camera cam, PodFx pod, Lighting lighting)
        {
            var root = Group("BridgePatrol", set);
            var left = Group("LaneLeft", root); left.position = new Vector3(-14f, BridgeY + .257f, BridgeZ);
            var right = Group("LaneRight", root); right.position = new Vector3(14f, BridgeY + .257f, BridgeZ);
            var pool = new List<BridgeWalker>();
            int[] counts = { 4, 4 };
            for (int p = 0; p < walkerPrefabs.Count; p++)
                for (int i = 0; i < counts[Mathf.Min(p, counts.Length - 1)]; i++)
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(walkerPrefabs[p], root);
                    w.name = walkerPrefabs[p].name + "_" + i; w.transform.position = left.position;
                    pool.Add(w.GetComponent<BridgeWalker>());
                }
            var patrol = root.gameObject.AddComponent<BridgePatrol>();
            log.AppendLine("walkers in pool=" + pool.Count);
            return patrol;
        }

        // ------------------------------------------------------------------ UI
        sealed class Ui { public RectTransform shell, frame; public CanvasGroup frameGroup; public Image fade; public HangarStatusTag status; public RectTransform reticle; public CanvasGroup reticleGroup, counterGroup; public TMP_Text reticleLabel, counter; }

        static RectTransform UiRect(string name, Transform parent, Vector2 size, Vector2 pos, Vector2 anchor, Vector2 pivot)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing) Object.DestroyImmediate(existing.gameObject);
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.sizeDelta = size; r.anchoredPosition = pos; return r;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static TextMeshProUGUI UiText(string name, Transform parent, string value, float size, Vector2 dim, Vector2 pos, TextAlignmentOptions align, Color c, Vector2? anchor = null, Vector2? pivot = null)
        {
            var t = UiRect(name, parent, dim, pos, anchor ?? new Vector2(.5f, .5f), pivot ?? new Vector2(.5f, .5f)).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.text = value; t.fontSize = size; t.alignment = align; t.color = c; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; return t;
        }
        static Image UiImage(string name, Transform parent, Vector2 size, Vector2 pos, Color c, Vector2? anchor = null, Vector2? pivot = null)
        {
            var i = UiRect(name, parent, size, pos, anchor ?? new Vector2(.5f, .5f), pivot ?? new Vector2(.5f, .5f)).gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false; return i;
        }
        static RectTransform Child(Transform root, string name) => root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
        static void Place(RectTransform r, Vector2 size, Vector2 pos) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = pos; r.localScale = Vector3.one; }

        static Ui BuildUi(Scene scene, BridgePatrol patrol, PodFx pod, Camera cam)
        {
            var ui = new Ui();
            var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).First(c => c.transform.Find("IndustrialShell"));
            var shell = (RectTransform)canvas.transform.Find("IndustrialShell"); ui.shell = shell;
            var frame = (RectTransform)shell.Find("ConsoleFrame"); ui.frame = frame;
            var home = (RectTransform)frame.Find("Home"); var options = (RectTransform)frame.Find("OptionsPanel"); var credits = (RectTransform)frame.Find("CreditsPanel");
            var bg = shell.GetComponent<Image>(); if (bg) { bg.color = new Color(0, 0, 0, 0); bg.raycastTarget = false; }
            foreach (Transform t in shell) if (t.name.StartsWith("HullSeam")) t.gameObject.SetActive(false);
            // left readability shade
            var gradient = AssetDatabase.LoadAssetAtPath<Sprite>(TexDir + "/Hangar_UIGradient.png");
            var shade = UiImage("HG_LeftShade", shell, new Vector2(1250, 0), Vector2.zero, new Color(0, 0, 0, .78f), new Vector2(0, .5f), new Vector2(0, .5f));
            shade.rectTransform.anchorMin = new Vector2(0, 0); shade.rectTransform.anchorMax = new Vector2(0, 1); shade.rectTransform.sizeDelta = new Vector2(1250, 0);
            shade.sprite = gradient; shade.transform.SetSiblingIndex(0);
            // frame on the left
            frame.anchorMin = frame.anchorMax = new Vector2(0, .5f); frame.pivot = new Vector2(0, .5f);
            frame.sizeDelta = new Vector2(600, 760); frame.anchoredPosition = new Vector2(70, -40); frame.localScale = Vector3.one;
            var panel = frame.GetComponent<IndustrialPanelGraphic>(); if (panel) panel.Configure(new Color(.012f, .02f, .028f, .8f), new Color(.1f, .26f, .3f, .9f), 30);
            ui.frameGroup = frame.GetComponent<CanvasGroup>(); if (!ui.frameGroup) ui.frameGroup = frame.gameObject.AddComponent<CanvasGroup>();
            var textColor = new Color(.88f, .93f, .94f);
            void Restyle(string name, string text, float size, Vector2 dim, Vector2 pos, Color c)
            {
                var t = Child(frame, name)?.GetComponent<TMP_Text>(); if (!t) { log.AppendLine("ui missing " + name); return; }
                Place(t.rectTransform, dim, pos); t.text = text; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Left; t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            Restyle("SectorLabel", "// UNIDAD C-04  ·  PROTOCOLO REQUIEM", 15, new Vector2(520, 28), new Vector2(0, 340), new Color(.3f, .78f, .86f, .85f));
            Restyle("Title", "IRON\nREQUIEM<color=#FF8A1E>/</color>", 92, new Vector2(520, 200), new Vector2(0, 222), textColor);
            var title = Child(frame, "Title")?.GetComponent<TMP_Text>(); if (title) { title.lineSpacing = -22; title.characterSpacing = 3; title.fontStyle = FontStyles.Bold; title.textWrappingMode = TextWrappingModes.Normal; }
            Restyle("Subtitle", "HIERRO. RUIDO. SUPERVIVENCIA.", 17, new Vector2(520, 28), new Vector2(0, 104), new Color(.56f, .65f, .68f));
            Restyle("Footer", "IRON REQUIEM  ·  INTERFAZ V2", 13, new Vector2(520, 24), new Vector2(0, -352), new Color(.35f, .5f, .54f));
            UiImage("HG_Divider", frame, new Vector2(520, 2), new Vector2(0, 80), new Color(.15f, .4f, .45f, .8f));
            UiImage("HG_DividerAccent", frame, new Vector2(64, 4), new Vector2(-228, 80), Orange);
            // home buttons
            Place(home, new Vector2(520, 380), new Vector2(0, -120));
            string[] names = { "StartButton", "Options", "Credits", "Quit" }, labels = { "JUGAR", "OPCIONES", "CRÉDITOS", "SALIR" };
            for (int i = 0; i < names.Length; i++)
            {
                var b = Child(home, names[i]); if (!b) { log.AppendLine("ui missing button " + names[i]); continue; }
                Place(b, new Vector2(520, 64), new Vector2(0, 135 - i * 80));
                StyleButton(b.GetComponent<Button>(), labels[i]);
            }
            // options / credits fit in the narrow frame
            foreach (var p in new[] { options, credits }) if (p) { Place(p, p.sizeDelta, new Vector2(0, -110)); p.localScale = Vector3.one * .6f; }
            foreach (var n in new[] { "OptionsBack", "CreditsBack" }) { var b = Child(frame, n); if (b) StyleButton(b.GetComponent<Button>(), null); }
            // status tag (bottom right)
            var tag = UiRect("HG_StatusTag", shell, new Vector2(360, 100), new Vector2(-60, 60), new Vector2(1, 0), new Vector2(1, 0));
            var plateRect = UiRect("Plate", tag, new Vector2(250, 60), Vector2.zero, new Vector2(1, 0), new Vector2(1, 0));
            var plate = plateRect.gameObject.AddComponent<IndustrialPanelGraphic>(); plate.raycastTarget = false;
            var icon = UiImage("Icon", plateRect, new Vector2(14, 14), new Vector2(28, 0), Orange, new Vector2(0, .5f));
            var state = UiText("State", plateRect, "EN ESPERA", 24, new Vector2(200, 50), new Vector2(50, 0), TextAlignmentOptions.Left, Color.white, new Vector2(0, .5f), new Vector2(0, .5f));
            state.characterSpacing = 4;
            var detail = UiText("Detail", tag, "CÁPSULA C-04 · SOPORTE VITAL ESTABLE", 13, new Vector2(360, 24), new Vector2(0, 66), TextAlignmentOptions.Right, new Color(.55f, .63f, .66f), new Vector2(1, 0), new Vector2(1, 0));
            ui.status = tag.gameObject.AddComponent<HangarStatusTag>(); ui.status.Configure(state, detail, icon, plate);
            // kill counter (top right)
            var counter = UiText("HG_KillCounter", shell, "", 20, new Vector2(560, 40), new Vector2(-60, -48), TextAlignmentOptions.Right, Orange, new Vector2(1, 1), new Vector2(1, 1));
            counter.characterSpacing = 3; ui.counter = counter; ui.counterGroup = counter.gameObject.AddComponent<CanvasGroup>(); ui.counterGroup.alpha = 0; ui.counterGroup.blocksRaycasts = false;
            // reticle (follows the mouse over a hostile)
            var ret = UiRect("HG_Reticle", shell, new Vector2(56, 56), Vector2.zero, shell.pivot, new Vector2(.5f, .5f)); ui.reticle = ret;
            var rc = new Color(1f, .38f, .12f);
            UiImage("T", ret, new Vector2(3, 14), new Vector2(0, 22), rc); UiImage("B", ret, new Vector2(3, 14), new Vector2(0, -22), rc);
            UiImage("L", ret, new Vector2(14, 3), new Vector2(-22, 0), rc); UiImage("R", ret, new Vector2(14, 3), new Vector2(22, 0), rc);
            UiImage("Dot", ret, new Vector2(4, 4), Vector2.zero, rc);
            ui.reticleLabel = UiText("Label", ret, "HOSTIL", 13, new Vector2(220, 22), new Vector2(0, -44), TextAlignmentOptions.Center, rc);
            ui.reticleLabel.characterSpacing = 3;
            ui.reticleGroup = ret.gameObject.AddComponent<CanvasGroup>(); ui.reticleGroup.alpha = 0; ui.reticleGroup.blocksRaycasts = false; ui.reticleGroup.interactable = false;
            // fade overlay last
            ui.fade = UiImage("HG_Fade", shell, Vector2.zero, Vector2.zero, Color.black); Stretch(ui.fade.rectTransform); ui.fade.transform.SetAsLastSibling();
            EditorUtility.SetDirty(canvas.gameObject);
            return ui;
        }

        static void StyleButton(Button b, string label)
        {
            if (!b) return;
            b.transition = Selectable.Transition.None;
            var face = b.GetComponentsInChildren<IndustrialPanelGraphic>(true).FirstOrDefault(g => g.name == "IndustrialButtonFace"); if (!face) face = b.GetComponents<IndustrialPanelGraphic>().FirstOrDefault();
            if (face && face.gameObject != b.gameObject) Stretch(face.rectTransform);
            var text = b.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label != null) t.text = label;
                Stretch(t.rectTransform); t.margin = new Vector4(36, 0, 0, 0); t.alignment = TextAlignmentOptions.Left; t.fontSize = 25; t.characterSpacing = 6;
            }
            var glow = UiImage("HG_Glow", b.transform, Vector2.zero, Vector2.zero, new Color(1f, .5f, .1f, 0)); Stretch(glow.rectTransform); glow.transform.SetSiblingIndex(face && face.gameObject != b.gameObject ? 1 : 0);
            var accent = UiImage("HG_Accent", b.transform, new Vector2(6, 0), new Vector2(4, 0), Orange, new Vector2(0, .5f), new Vector2(0, .5f));
            accent.rectTransform.anchorMin = new Vector2(0, .14f); accent.rectTransform.anchorMax = new Vector2(0, .86f); accent.rectTransform.sizeDelta = new Vector2(6, 0);
            foreach (var old in b.GetComponents<HangarButtonFx>()) Object.DestroyImmediate(old);
            var fx = b.gameObject.AddComponent<HangarButtonFx>(); fx.Configure(face, text, accent, glow); fx.SetDefault(b.name == "StartButton");
        }

        // ------------------------------------------------------------------ wiring + render settings
        static void Wire(Transform set, PodFx pod, Lighting L, BridgePatrol patrol, Ui ui, Camera cam, FxSet fx)
        {
            var rig = cam.GetComponent<MenuCameraRig>();
            var walkers = patrol.GetComponentsInChildren<BridgeWalker>(true);
            var left = patrol.transform.Find("LaneLeft"); var right = patrol.transform.Find("LaneRight");
            patrol.Configure(cam, walkers, left, right, fx.explosions, rig, pod.idle, ui.status, new[] { pod.strips, pod.key });
            patrol.ConfigureConveyor(BeltSpeed);
            patrol.ConfigureHud(ui.shell, ui.reticle, ui.reticleGroup, ui.reticleLabel, ui.counterGroup, ui.counter);
            var seq = set.gameObject.AddComponent<HangarLaunchSequence>();
            var openClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/Weapons/Weapon_Cooling.wav");
            var audio = set.gameObject.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
            var master = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => s.outputAudioMixerGroup).FirstOrDefault(g => g);
            foreach (var s in set.GetComponentsInChildren<AudioSource>(true)) if (!s.outputAudioMixerGroup) s.outputAudioMixerGroup = master;
            var startup = new List<MenuFlicker> { pod.key, pod.strips }; startup.AddRange(L.all.Take(4));
            seq.SetLookTarget(pod.look);
            seq.Configure(new[] { pod.key, pod.strips }, startup.ToArray(), pod.canopy, pod.canopyRenderer, pod.idle, rig, pod.push, ui.fade, ui.frameGroup, ui.status, patrol, pod.vent, audio, openClip);
            log.AppendLine("wired: walkers=" + walkers.Length + " openClip=" + (openClip ? openClip.name : "none") + " mixer=" + (master ? master.name : "none"));
        }

        internal static void ConfigureRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.2f, .23f, .28f);
            RenderSettings.ambientEquatorColor = new Color(.13f, .145f, .17f);
            RenderSettings.ambientGroundColor = new Color(.08f, .08f, .085f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = FogDensity;
            RenderSettings.fogColor = new Color(.1f, .13f, .17f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = .35f;
        }
    }
}
