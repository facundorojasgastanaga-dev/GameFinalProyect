using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    /// Weapons Redesign v01 (B2): imports the three Blender exports (P-A pistol, PR-C primary, C-A T-07 cannon), builds
    /// URP materials, LOD visual prefabs and NEW weapon prefab variants of the campaign weapons. Existing prefabs, scenes
    /// and FBX stay untouched; the lab scene is a copy that points only its own references to the new variants.
    /// Fixed operations from Tools/IronRequiem/Requests/weapons-redesign.txt: "build", "lab", "verify" (also in Tools menu).
    [InitializeOnLoad]
    public static class WeaponsRedesignAuthoring
    {
        const string Request = "Tools/IronRequiem/Requests/weapons-redesign.txt";
        const string ReportDir = "Tools/IronRequiem/Reports/WeaponsRedesign/Unity";
        const string ArtRoot = "Assets/IronRequiem/Art/Weapons/Redesign_v01";
        const string VisualDir = "Assets/IronRequiem/Prefabs/Weapons/Visuals";
        const string VariantDir = "Assets/IronRequiem/Prefabs/Weapons/Redesign_v01";
        const string LabSource = "Assets/IronRequiem/Scenes/VisualOverhaul/MainScene_VisualLab.unity";
        public const string LabScene = "Assets/IronRequiem/Scenes/WeaponsRedesignLab.unity";
        const string StationPrefab = "Assets/IronRequiem/Prefabs/Levels/MountStation_C04.prefab";
        public const string ModuleAttachmentName = "ModuleAttachment", ModuleMuzzleName = "ModuleMuzzle";
        static readonly DateTime LoadedAt = DateTime.UtcNow;

        public sealed class Def
        {
            public string Key, Export, Name, BasePrefab, Variant; public bool Stolen; public float[] Lods; public Color Heat, Charge;
            public string Fbx => $"{ArtRoot}/{Name}/{Name}.fbx";
            public string Visual => $"{VisualDir}/{Name}_Visual.prefab";
        }

        public static readonly Def[] Defs =
        {
            new Def { Key = "pistol", Export = "Pistol_Redesign_v01", Name = "PistolMonolito", Stolen = false,
                BasePrefab = "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab",
                Variant = VariantDir + "/Weapon_IR_ArmCannon_Redesign_v01.prefab", Lods = new[] { .25f, .1f, .01f },
                Heat = new Color(2.4f, .7f, .12f) },
            new Def { Key = "primary", Export = "Primary_Redesign_v01", Name = "PrimaryEspina", Stolen = false,
                BasePrefab = "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab",
                Variant = VariantDir + "/Weapon_Shotgun_ArmModule_Redesign_v01.prefab", Lods = new[] { .25f, .1f, .01f },
                Heat = new Color(2.4f, .7f, .12f) },
            new Def { Key = "cannon", Export = "T07Cannon_Redesign_v01", Name = "T07CannonCrudo", Stolen = true,
                BasePrefab = "Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab",
                Variant = VariantDir + "/Weapon_T07_Redesign_v01.prefab", Lods = new[] { .18f, .06f, .003f },
                Heat = new Color(2.4f, .7f, .12f), Charge = new Color(1.6f, .25f, 2.2f) },
        };

        static WeaponsRedesignAuthoring()
        {
            EditorApplication.update += Tick;
            // Test callbacks must be re-registered after every domain reload (Play Mode entry/exit).
            if (SessionState.GetBool(TestKey, false)) EditorApplication.delayCall += RegisterTests;
        }
        const string TestKey = "IR.WeaponsRedesign.Tests";
        static void RegisterTests() { if (testApi) return; testApi = ScriptableObject.CreateInstance<TestRunnerApi>(); testApi.RegisterCallbacks(new Results()); }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            if (LoadedAt < File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/WeaponsRedesignAuthoring.cs")) return;
            var op = File.ReadAllText(Request).Trim(); File.Delete(Request); Directory.CreateDirectory(ReportDir);
            try
            {
                if (op == "build") { Build(); Verify(); }
                else if (op == "lab") { BuildLab(); Verify(); }
                else if (op == "verify") Verify();
                else if (op == "test") RunPlayTests();
                else throw new InvalidOperationException("Unknown fixed operation: " + op);
            }
            catch (Exception e) { File.WriteAllText(ReportDir + "/status.txt", "ERROR " + op + "\n" + e); Debug.LogException(e); }
        }

        [MenuItem("Tools/Iron Requiem/Weapons/Redesign v01/Build Assets and Variants")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(VariantDir);
            foreach (var d in Defs)
            {
                ImportArt(d);
                var mats = BuildMaterials(d);
                BuildVisual(d, mats);
                BuildVariant(d, mats);
            }
            File.WriteAllText(ReportDir + "/status.txt", "OK build " + DateTime.Now.ToString("s"));
        }

        static void ImportArt(Def d)
        {
            var src = "Tools/IronRequiem/Exports/" + d.Export + "/";
            var dst = $"{ArtRoot}/{d.Name}/";
            Directory.CreateDirectory(dst);
            foreach (var suffix in new[] { ".fbx", "_BaseColor.png", "_MetalSmooth.png", "_Emission.png" })
            {
                var from = src + d.Name + suffix; var to = dst + d.Name + suffix;
                if (!File.Exists(from)) throw new FileNotFoundException("Export missing", from);
                if (!File.Exists(to) || !File.ReadAllBytes(to).SequenceEqual(File.ReadAllBytes(from))) File.Copy(from, to, true); // derived copy; .meta/GUID kept
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceSynchronousImport);
            }
            foreach (var (suffix, srgb) in new[] { ("_BaseColor.png", true), ("_MetalSmooth.png", false), ("_Emission.png", true) })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst + d.Name + suffix);
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = srgb; ti.mipmapEnabled = true; ti.maxTextureSize = 1024;
                ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false;
                ti.SaveAndReimport();
            }
            var mi = (ModelImporter)AssetImporter.GetAtPath(d.Fbx);
            mi.globalScale = 1; mi.useFileScale = true; mi.bakeAxisConversion = false; // IR-ACOPLE-01: baking flips modules
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
            mi.importCameras = false; mi.importLights = false; mi.isReadable = false; mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.SaveAndReimport();
        }

        static Material[] BuildMaterials(Def d)
        {
            var dir = $"{ArtRoot}/{d.Name}/";
            Texture2D T(string s)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + d.Name + s + ".png");
                if (!tex) throw new FileNotFoundException("Texture missing", dir + d.Name + s + ".png");
                return tex;
            }
            Material Make(string suffix, Color emission)
            {
                var path = dir + d.Name + suffix + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                m.shader = Shader.Find("Universal Render Pipeline/Lit");
                m.SetTexture("_BaseMap", T("_BaseColor")); m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_MetallicGlossMap", T("_MetalSmooth")); m.SetFloat("_Metallic", 1); m.SetFloat("_Smoothness", 1);
                m.SetFloat("_SmoothnessTextureChannel", 0); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_EmissionMap", T("_Emission")); m.SetColor("_EmissionColor", emission); m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                return m;
            }
            var list = new List<Material> { Make("_Surface", Color.white), Make("_Heat", d.Heat) };
            if (d.Stolen) list.Add(Make("_Charge", d.Charge));
            AssetDatabase.SaveAssetIfDirty(list[0]); foreach (var m in list) AssetDatabase.SaveAssetIfDirty(m);
            return list.ToArray();
        }

        static void BuildVisual(Def d, Material[] mats)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(d.Fbx);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                root.name = d.Name + "_Visual";
                var lods = new LOD[3];
                for (int i = 0; i < 3; i++)
                {
                    var r = root.GetComponentsInChildren<MeshRenderer>(true).Single(x => x.name == d.Name + "_LOD" + i);
                    int slots = r.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                    if (slots != mats.Length) throw new InvalidOperationException($"{r.name}: {slots} submeshes, {mats.Length} materials");
                    r.sharedMaterials = mats;
                    lods[i] = new LOD(d.Lods[i], new Renderer[] { r });
                }
                var group = root.GetComponent<LODGroup>();
                if (!group) group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None; group.SetLODs(lods); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, d.Visual, out bool ok);
                if (!ok) throw new InvalidOperationException("Visual save failed: " + d.Visual);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static void BuildVariant(Def d, Material[] mats)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.BasePrefab);
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.Visual);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
                var wc = inst.GetComponent<WeaponController>();
                var muzzle = wc.WeaponMuzzle;
                Transform host;
                if (d.Stolen) host = inst.GetComponentInChildren<StolenShellFit>(true).transform;
                else host = inst.GetComponent<ArmModuleVisual>().ModuleRoot;
                // Hide the previous visual (renderers only: scripts, sockets, steam and audio keep working).
                foreach (var r in wc.WeaponRoot.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, host);
                vis.name = d.Name + "_Visual";
                var t = vis.transform;
                var visAttach = t.GetComponentsInChildren<Transform>(true).Single(x => x.name == "Attachment");
                var visMuzzle = t.GetComponentsInChildren<Transform>(true).Single(x => x.name == "Muzzle");
                // Exactly one "Attachment" per weapon (receptor contract): the module's own sockets are renamed.
                visAttach.name = ModuleAttachmentName; visMuzzle.name = ModuleMuzzleName;
                var ls = host.lossyScale; t.localScale = new Vector3(1 / ls.x, 1 / ls.y, 1 / ls.z);
                if (d.Stolen)
                {
                    // StolenShellFit seats the shell with +Z along the socket: the module sits at shell origin.
                    t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity;
                    var fit = new SerializedObject(host.GetComponent<StolenShellFit>());
                    fit.FindProperty("rearSocket").objectReferenceValue = visAttach; fit.ApplyModifiedPropertiesWithoutUndo();
                    var anchor = inst.GetComponent<StolenChargeAnchor>();
                    if (anchor) { var so = new SerializedObject(anchor); so.FindProperty("mouthSocket").objectReferenceValue = visMuzzle; so.ApplyModifiedPropertiesWithoutUndo(); }
                    // The magenta window follows the charge (MaterialPropertyBlock on the Charge_Emissive slot).
                    var glow = inst.GetComponent<ChargeWindowGlow>();
                    if (!glow) glow = inst.AddComponent<ChargeWindowGlow>();
                    glow.Configure(inst.GetComponent<CannonChargeShot>(), vis.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray(), mats[2]);
                }
                else
                {
                    // Module weapons: the static rear "Attachment" of the weapon stays where the forearm expects it;
                    // the new module is placed BY ITS ATTACHMENT on it, firing along the existing muzzle axis.
                    var legacy = wc.WeaponRoot.GetComponentsInChildren<Transform>(true).First(x => x.name == "Attachment");
                    t.rotation = Quaternion.LookRotation(muzzle.forward, wc.WeaponRoot.transform.up);
                    t.position += legacy.position - visAttach.position;
                    muzzle.position = visMuzzle.position; // shots, flash and charge leave the new mouth
                }
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;
                var heat = inst.GetComponent<OverheatBehavior>();
                if (heat) heat.OverheatingMaterial = mats[1];
                PrefabUtility.SaveAsPrefabAsset(inst, d.Variant, out bool ok);
                if (!ok) throw new InvalidOperationException("Variant save failed: " + d.Variant);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [MenuItem("Tools/Iron Requiem/Weapons/Redesign v01/Build Lab Scene (copy)")]
        public static void BuildLab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!File.Exists(LabScene) && !AssetDatabase.CopyAsset(LabSource, LabScene)) throw new InvalidOperationException("Copy failed");
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            foreach (var d in Defs)
            {
                var oldW = AssetDatabase.LoadAssetAtPath<GameObject>(d.BasePrefab); var newW = AssetDatabase.LoadAssetAtPath<GameObject>(d.Variant);
                map[oldW] = newW; map[oldW.GetComponent<WeaponController>()] = newW.GetComponent<WeaponController>();
            }
            // The station is placed FIRST so its weaponPrefab is remapped too; the v02 lab copy gets the same remap.
            var report = new StringBuilder();
            foreach (var path in new[] { LabScene, ProtagonistV02Authoring.LabScene })
            {
                if (!File.Exists(path)) continue;
                foreach (var s in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
                    if (s.path == path) throw new InvalidOperationException("Lab scene is open; close it first: " + path);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                int remapped = 0;
                try
                {
                    var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerCharacterController>(true)).FirstOrDefault();
                    if (player && !scene.GetRootGameObjects().Any(g => g.name.StartsWith("MountStation_C04")))
                    {
                        var station = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StationPrefab), scene);
                        var fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                        var pos = player.transform.position + fwd * 5;
                        if (Physics.Raycast(pos + Vector3.up * 3, Vector3.down, out var hit, 20)) pos = hit.point;
                        station.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-fwd, Vector3.up));
                    }
                    foreach (var go in scene.GetRootGameObjects())
                        foreach (var c in go.GetComponentsInChildren<Component>(true))
                        {
                            if (!c || c is Transform) continue;
                            var so = new SerializedObject(c); var p = so.GetIterator(); bool changed = false;
                            while (p.Next(true))
                                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue && map.TryGetValue(p.objectReferenceValue, out var to))
                                { p.objectReferenceValue = to; changed = true; remapped++; }
                            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    var stationWeapon = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WeaponMountStation>(true))
                        .Select(m => new SerializedObject(m).FindProperty("weaponPrefab").objectReferenceValue).FirstOrDefault();
                    EditorSceneManager.SaveScene(scene);
                    report.AppendLine($"lab {path} remapped references: {remapped}; station weapon: {(stationWeapon ? stationWeapon.name : "none")}");
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(ReportDir + "/lab.txt", report.ToString());
        }

        static TestRunnerApi testApi;
        [MenuItem("Tools/Iron Requiem/Weapons/Redesign v01/Run PlayMode Tests")]
        public static void RunPlayTests()
        {
            var open = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            File.WriteAllText(ReportDir + "/open-scenes.txt", string.Join("\n", open.Select(s => $"{s.name} | path='{s.path}' | dirty={s.isDirty} | loaded={s.isLoaded}")));
            // Only saved scene files can hold user work; the Test Runner's own InitTestScene has no path.
            var dirty = open.Where(s => s.isDirty && !string.IsNullOrEmpty(s.path) && !s.name.StartsWith("InitTestScene")).ToArray();
            if (dirty.Length > 0)
                throw new InvalidOperationException("Open scene(s) with unsaved changes (never saved by this tool): " + string.Join(", ", dirty.Select(s => $"'{s.name}' path='{s.path}'")));
            SessionState.SetBool(TestKey, true); RegisterTests();
            File.Delete(ReportDir + "/play-summary.txt"); File.Delete(ReportDir + "/play-cases.txt");
            testApi.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, groupNames = new[] { @"^IronRequiem\.Tests\.(WeaponsRedesignTests|ProtagonistV02Tests)" } }));
        }
        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            { if (!result.HasChildren) File.AppendAllText(ReportDir + "/play-cases.txt", $"{DateTime.Now:s} {result.Name}: {result.TestStatus} {result.Message}\n"); }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(ReportDir);
                TestRunnerApi.SaveResultToFile(result, ReportDir + "/play.xml");
                File.WriteAllText(ReportDir + "/play-summary.txt", $"{result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
                SessionState.SetBool(TestKey, false);
            }
        }

        [MenuItem("Tools/Iron Requiem/Weapons/Redesign v01/Verify")]
        public static void Verify()
        {
            var sb = new StringBuilder("{\n  \"unity\": \"" + Application.unityVersion + "\",\n  \"weapons\": [\n");
            string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "[{0:F4}, {1:F4}, {2:F4}]", v.x, v.y, v.z);
            var rows = new List<string>();
            foreach (var d in Defs)
            {
                var w = AssetDatabase.LoadAssetAtPath<GameObject>(d.Variant);
                if (!w) { rows.Add($"    {{\"key\": \"{d.Key}\", \"error\": \"variant missing\"}}"); continue; }
                var wc = w.GetComponent<WeaponController>(); var root = wc.WeaponRoot;
                var all = w.GetComponentsInChildren<Transform>(true);
                int attachments = all.Count(x => x.name == "Attachment");
                var ma = all.Single(x => x.name == ModuleAttachmentName); var mm = all.Single(x => x.name == ModuleMuzzleName);
                var legacy = all.FirstOrDefault(x => x.name == "Attachment");
                float attachErr = d.Stolen ? -1 : (ma.position - legacy.position).magnitude;
                float muzzleErr = d.Stolen ? -1 : (mm.position - wc.WeaponMuzzle.position).magnitude;
                var axis = (mm.position - ma.position).normalized;
                float axisAngle = Vector3.Angle(axis, wc.WeaponMuzzle.forward);
                var vis = all.Single(x => x.name == d.Name + "_Visual");
                var lods = vis.GetComponent<LODGroup>().GetLODs();
                var tris = lods.Select(l => { var m = l.renderers[0].GetComponent<MeshFilter>().sharedMesh; return Enumerable.Range(0, m.subMeshCount).Sum(s => (long)m.GetIndexCount(s)) / 3; }).ToArray();
                var enabledOld = w.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && !r.transform.IsChildOf(vis));
                var mats = lods[0].renderers[0].sharedMaterials;
                bool emission = mats.All(m => m && m.IsKeywordEnabled("_EMISSION") && m.GetTexture("_EmissionMap") && m.GetTexture("_BaseMap") && m.GetTexture("_MetallicGlossMap"));
                var heat = w.GetComponent<OverheatBehavior>();
                string extra = "";
                if (d.Stolen)
                {
                    var fit = new SerializedObject(w.GetComponentInChildren<StolenShellFit>(true)).FindProperty("rearSocket").objectReferenceValue;
                    var anc = w.GetComponent<StolenChargeAnchor>(); var mouth = anc ? new SerializedObject(anc).FindProperty("mouthSocket").objectReferenceValue : null;
                    var glowSo = w.GetComponent<ChargeWindowGlow>() ? new SerializedObject(w.GetComponent<ChargeWindowGlow>()) : null;
                    int glowTargets = glowSo != null ? glowSo.FindProperty("renderers").arraySize : 0;
                    extra = $", \"rearSocket\": \"{(fit ? fit.name : "null")}\", \"mouthSocket\": \"{(mouth ? mouth.name : "null")}\", \"chargeWindowRenderers\": {glowTargets}";
                }
                rows.Add(string.Format(CultureInfo.InvariantCulture,
                    "    {{\"key\": \"{0}\", \"variant\": \"{1}\", \"isVariant\": {2}, \"base\": \"{3}\", \"attachmentCount\": {4}, \"attachErr_m\": {5:F5}, \"muzzleErr_m\": {6:F5}, \"axisVsMuzzleForward_deg\": {7:F3}, \"moduleLength_m\": {8:F4}, \"visualLossyScale\": {9}, \"lodTris\": [{10}], \"lodHeights\": [{11}], \"oldRenderersEnabled\": {12}, \"materials\": [{13}], \"emissionOn\": {14}, \"overheatMaterial\": \"{15}\"{16}}}",
                    d.Key, d.Variant, (PrefabUtility.GetPrefabAssetType(w) == PrefabAssetType.Variant).ToString().ToLowerInvariant(),
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(w)), attachments, attachErr, muzzleErr, axisAngle,
                    (mm.position - ma.position).magnitude, V(vis.lossyScale), string.Join(", ", tris),
                    string.Join(", ", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F3", CultureInfo.InvariantCulture))),
                    enabledOld, string.Join(", ", mats.Select(m => "\"" + (m ? m.name : "null") + "\"")), emission.ToString().ToLowerInvariant(),
                    heat && heat.OverheatingMaterial ? heat.OverheatingMaterial.name : "none", extra));
            }
            sb.Append(string.Join(",\n", rows)).Append("\n  ],\n");
            sb.Append("  \"labScene\": ").Append(File.Exists(LabScene) ? "\"" + LabScene + "\"" : "null").Append("\n}\n");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/verify.json", sb.ToString());
            Debug.Log("[IronRequiem] Weapons Redesign v01 verified: " + ReportDir + "/verify.json");
        }
    }
}
