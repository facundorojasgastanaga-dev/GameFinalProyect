using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace IronRequiem.Editor
{
    /// Imports the T-07 cannon copy re-pivoted in Blender (origin = rear attachment on the barrel axis, +Z = firing
    /// direction, +Y = away from the cut face) and builds a visual prefab variant with LODGroup, shared URP materials
    /// and Attachment/Muzzle sockets. Works in a preview scene: never opens, modifies or saves user scenes, and never
    /// touches the source FBX or existing gameplay prefabs.
    /// Fixed operations from Tools/IronRequiem/Requests/weapon-pivot.txt: "build" or "verify". Also in the Tools menu.
    [InitializeOnLoad]
    public static class WeaponPivotAuthoring
    {
        const string Request = "Tools/IronRequiem/Requests/weapon-pivot.txt";
        const string ReportDir = "Tools/IronRequiem/Reports/WeaponPivot";
        const string ExportFbx = "Tools/IronRequiem/Exports/T07_Cannon_PivotFixed/T07_Cannon_PivotFixed.fbx";
        const string ModelDir = "Assets/IronRequiem/Art/Weapons/T07CannonPivotFixed";
        const string Model = ModelDir + "/T07_Cannon_PivotFixed.fbx";
        const string Variant = "Assets/IronRequiem/Prefabs/Weapons/Visuals/T07_Cannon_PivotFixed_Visual.prefab";
        const string SurfaceMaterial = "Assets/IronRequiem/Art/VisualPass/StolenFPS/T07_URP_FPS.mat";
        const string CutMaterial = "Assets/IronRequiem/Art/VisualPass/StolenFPS/ExposedMechanism_FPS.mat";
        public const string SocketAttachment = "WeaponAttachment", SocketMuzzle = "WeaponMuzzle";
        const float BarrelLength = 0.6644f; // Blender measurement: rear attachment -> fitted mouth centre (m)
        static readonly float[] LodHeights = { 0.18f, 0.06f, 0.003f }; // same cannon thresholds as T07_Common_Optimized
        static readonly DateTime LoadedAt = DateTime.UtcNow;

        static WeaponPivotAuthoring() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            if (LoadedAt < File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/WeaponPivotAuthoring.cs")) return;
            var op = File.ReadAllText(Request).Trim(); File.Delete(Request); Directory.CreateDirectory(ReportDir);
            try
            {
                if (op == "build") Build();
                else if (op == "verify") Verify();
                else if (op == "test") RunTests();
                else throw new InvalidOperationException("Unknown fixed operation: " + op);
            }
            catch (Exception e) { File.WriteAllText(ReportDir + "/unity-status.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/Iron Requiem/Weapons/Build T07 Cannon Pivot Fixed Visual")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!File.Exists(ExportFbx)) throw new FileNotFoundException("Blender export missing", ExportFbx);
            Directory.CreateDirectory(ModelDir);
            // Derived asset of this tool (never the source): refreshed in place from the export, keeping its .meta/GUID.
            if (!File.Exists(Model) || !File.ReadAllBytes(Model).SequenceEqual(File.ReadAllBytes(ExportFbx))) File.Copy(ExportFbx, Model, true);
            AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceSynchronousImport);

            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            // Same conventions as the source T07_CannonClean.fbx import: file scale, no axis bake, no embedded materials.
            // Imported nodes therefore carry the usual -90 X / x100 node transform; the variant adds clean root sockets.
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = false;
            importer.importCameras = false; importer.importLights = false; importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var surface = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterial);
            var cut = AssetDatabase.LoadAssetAtPath<Material>(CutMaterial);
            if (!model || !surface || !cut) throw new InvalidOperationException("Model or materials missing.");

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                root.name = "T07_Cannon_PivotFixed_Visual";
                var lods = new LOD[3];
                for (int i = 0; i < 3; i++)
                {
                    var r = root.GetComponentsInChildren<MeshRenderer>(true).Single(x => x.name == "CannonClean_LOD" + i);
                    r.sharedMaterials = new[] { surface, cut };
                    lods[i] = new LOD(LodHeights[i], new Renderer[] { r });
                }
                // The importer already creates a LODGroup for *_LOD0/1/2 names; reuse it and override its thresholds.
                var group = root.GetComponent<LODGroup>();
                if (!group) group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods);
                group.RecalculateBounds();
                // Gameplay sockets at root level: unit scale, +Z = firing direction, +Y = away from the cut face.
                // Positioned on the imported Blender empties, which stay as measurement references.
                foreach (var (socket, source) in new[] { (SocketAttachment, "Attachment"), (SocketMuzzle, "Muzzle") })
                {
                    var reference = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == source);
                    var go = new GameObject(socket);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = root.transform.InverseTransformPoint(reference.position);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Variant, out bool ok);
                if (!ok) throw new InvalidOperationException("Prefab save failed.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Verify();
        }

        static TestRunnerApi testApi;

        /// Runs only WeaponPivotAssetTests (EditMode) in this Editor and saves NUnit XML next to the report.
        [MenuItem("Tools/Iron Requiem/Weapons/Test T07 Cannon Pivot Fixed Visual")]
        public static void RunTests()
        {
            if (!testApi) { testApi = ScriptableObject.CreateInstance<TestRunnerApi>(); testApi.RegisterCallbacks(new TestResults()); }
            testApi.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = new[] { @"^IronRequiem\.Tests\.WeaponPivotAssetTests" } }));
        }

        sealed class TestResults : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(ReportDir);
                TestRunnerApi.SaveResultToFile(result, ReportDir + "/edit-tests.xml");
                File.WriteAllText(ReportDir + "/edit-tests-summary.txt", string.Format("{0} passed={1} failed={2} skipped={3} inconclusive={4}",
                    result.TestStatus, result.PassCount, result.FailCount, result.SkipCount, result.InconclusiveCount));
            }
        }

        static string Sha(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        [MenuItem("Tools/Iron Requiem/Weapons/Verify T07 Cannon Pivot Fixed Visual")]
        public static void Verify()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Variant);
            if (!prefab) throw new FileNotFoundException("Variant missing", Variant);
            var root = prefab.transform;
            Transform Find(string n) => prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == n);
            var muzzle = Find(SocketMuzzle); var attach = Find(SocketAttachment);
            var mLocal = root.InverseTransformPoint(muzzle.position);
            var aLocal = root.InverseTransformPoint(attach.position);
            var mFwd = root.InverseTransformDirection(muzzle.forward);
            var importedOffset = Mathf.Max((muzzle.position - Find("Muzzle").position).magnitude, (attach.position - Find("Attachment").position).magnitude);
            var group = prefab.GetComponent<LODGroup>();
            var sb = new StringBuilder("{\n");
            void Add(string k, string v, bool last = false) => sb.Append("  \"").Append(k).Append("\": ").Append(v).Append(last ? "\n" : ",\n");
            string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "[{0:F5}, {1:F5}, {2:F5}]", v.x, v.y, v.z);
            Add("unity", "\"" + Application.unityVersion + "\"");
            Add("variant", "\"" + Variant + "\"");
            Add("isVariant", (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant).ToString().ToLowerInvariant());
            Add("variantBase", "\"" + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(prefab)) + "\"");
            Add("attachmentInRoot", V(aLocal));
            Add("muzzleInRoot", V(mLocal));
            Add("muzzleForwardInRoot", V(mFwd));
            Add("muzzleUpInRoot", V(root.InverseTransformDirection(muzzle.up)));
            Add("socketLossyScale", V(muzzle.lossyScale));
            Add("socketsVsImportedEmpties_m", importedOffset.ToString("F5", CultureInfo.InvariantCulture));
            Add("modelSha256", "\"" + Sha(Model) + "\"");
            Add("exportSha256", "\"" + Sha(ExportFbx) + "\"");
            Add("expectedMuzzleInRoot", V(new Vector3(0, 0, BarrelLength)));
            Add("muzzleError_m", (mLocal - new Vector3(0, 0, BarrelLength)).magnitude.ToString("F5", CultureInfo.InvariantCulture));
            var parts = prefab.GetComponentsInChildren<MeshFilter>(true).OrderBy(f => f.name).Select(f =>
            {
                var mesh = f.sharedMesh; var r = f.GetComponent<MeshRenderer>();
                var b = new Bounds(root.InverseTransformPoint(f.transform.TransformPoint(mesh.bounds.center)), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var c = mesh.bounds.center + Vector3.Scale(mesh.bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    b.Encapsulate(root.InverseTransformPoint(f.transform.TransformPoint(c)));
                }
                return string.Format(CultureInfo.InvariantCulture,
                    "    {{\"name\": \"{0}\", \"triangles\": {1}, \"vertices\": {2}, \"submeshes\": {3}, \"materials\": [{4}], \"localPosition\": {5}, \"localEuler\": {6}, \"localScale\": {7}, \"rootBoundsMin\": {8}, \"rootBoundsMax\": {9}}}",
                    f.name, Enumerable.Range(0, mesh.subMeshCount).Sum(s => (long)mesh.GetIndexCount(s)) / 3,mesh.vertexCount, mesh.subMeshCount,
                    string.Join(", ", r.sharedMaterials.Select(m => "\"" + (m ? AssetDatabase.GetAssetPath(m) : "null") + "\"")),
                    V(f.transform.localPosition), V(f.transform.localEulerAngles), V(f.transform.localScale), V(b.min), V(b.max));
            });
            sb.Append("  \"meshes\": [\n").Append(string.Join(",\n", parts)).Append("\n  ],\n");
            var lods = group ? group.GetLODs() : new LOD[0];
            // Same meshes as Unity imports them from the untouched source, for a like-for-like triangle comparison.
            var sourceMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/IronRequiem/Art/Weapons/T07CannonClean/T07_CannonClean.fbx").OfType<Mesh>().OrderBy(m => m.name)
                .Select(m => "\"" + m.name + "\": " + Enumerable.Range(0, m.subMeshCount).Sum(s => (long)m.GetIndexCount(s)) / 3);
            Add("sourceUnityTriangles", "{" + string.Join(", ", sourceMeshes) + "}");
            Add("lodCount", lods.Length.ToString());
            Add("lodHeights", "[" + string.Join(", ", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F4", CultureInfo.InvariantCulture))) + "]");
            Add("lodRenderers", "[" + string.Join(", ", lods.Select(l => "\"" + string.Join("+", l.renderers.Select(r => r ? r.name : "null")) + "\"")) + "]");
            Add("missingScripts", prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)).ToString(), true);
            sb.Append("}\n");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/unity-verify.json", sb.ToString());
            File.WriteAllText(ReportDir + "/unity-status.txt", "OK " + DateTime.Now.ToString("s"));
            Debug.Log("[IronRequiem] T07 cannon pivot visual verified: " + ReportDir + "/unity-verify.json");
        }
    }
}
