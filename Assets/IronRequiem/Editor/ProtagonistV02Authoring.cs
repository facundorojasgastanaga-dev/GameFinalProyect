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
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    /// IR-09 v02: imports the refined protagonist (rigid modular hands, IR-ACOPLE-01 wrist) and creates NEW variants:
    /// Protagonist_FPS_v02 / Protagonist_Body_v02 (variants of the v01 prefabs with v02 meshes and bind), v02 hand pose
    /// libraries baked from the exported pose clips, a player variant of Player_ModularArm_v07 and a lab copy.
    /// v01 assets, prefabs and scenes are not modified. Requests: Tools/IronRequiem/Requests/protagonist-v02.txt
    /// ("build", "lab", "verify").
    [InitializeOnLoad]
    public static class ProtagonistV02Authoring
    {
        const string Request = "Tools/IronRequiem/Requests/protagonist-v02.txt";
        const string ReportDir = "Tools/IronRequiem/Reports/ProtagonistV02/Unity";
        const string Export = "Tools/IronRequiem/Exports/IR_Protagonist_Refined_v01/";
        const string ArtDir = "Assets/IronRequiem/Art/Player/ProtagonistV02/Rigged/";
        public const string FpsFbx = ArtDir + "IR_Protagonist_v02_FPS_Rigged.fbx", BodyFbx = ArtDir + "IR_Protagonist_v02_Rigged.fbx";
        const string V01Fps = "Assets/IronRequiem/Art/Player/ProtagonistV01/Protagonist_FPS.prefab";
        const string V01Body = "Assets/IronRequiem/Art/Player/ProtagonistV01/Protagonist_Body.prefab";
        const string V01FpsFbx = "Assets/IronRequiem/Art/Player/ProtagonistV01/Rigged/IR_Protagonist_FPS_Rigged.fbx";
        public const string FpsVariant = "Assets/IronRequiem/Art/Player/ProtagonistV02/Protagonist_FPS_v02.prefab";
        public const string BodyVariant = "Assets/IronRequiem/Art/Player/ProtagonistV02/Protagonist_Body_v02.prefab";
        const string PoseDir = "Assets/IronRequiem/Art/Player/HandPoses/V02";
        const string PlayerBase = "Assets/IronRequiem/Prefabs/Player/Player_ModularArm_v07.prefab";
        public const string PlayerVariant = "Assets/IronRequiem/Prefabs/Player/Player_ModularArm_ProtagonistV02.prefab";
        const string LabSource = "Assets/IronRequiem/Scenes/WeaponsRedesignLab.unity";
        public const string LabScene = "Assets/IronRequiem/Scenes/ProtagonistV02Lab.unity";
        static readonly string[] Fingers = { "Index", "Middle", "Ring", "Little", "Thumb" };
        public static readonly string[] Poses = { "Open", "Relaxed", "Fist", "Grip", "Inspect" };
        static readonly DateTime LoadedAt = DateTime.UtcNow;
        static readonly StringBuilder log = new StringBuilder();

        static ProtagonistV02Authoring() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            if (LoadedAt < File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/ProtagonistV02Authoring.cs")) return;
            var op = File.ReadAllText(Request).Trim(); File.Delete(Request); Directory.CreateDirectory(ReportDir);
            try
            {
                if (op == "build") { Build(); Verify(); }
                else if (op == "lab") { BuildLab(); Verify(); }
                else if (op == "verify") Verify();
                else throw new InvalidOperationException("Unknown fixed operation: " + op);
                File.WriteAllText(ReportDir + "/status.txt", "OK " + op + " " + DateTime.Now.ToString("s") + "\n" + log);
            }
            catch (Exception e) { File.WriteAllText(ReportDir + "/status.txt", "ERROR " + op + "\n" + e + "\n" + log); Debug.LogException(e); }
        }

        [MenuItem("Tools/Iron Requiem/Protagonist v02/Build")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            log.Clear();
            Import(FpsFbx, V01FpsFbx); Import(BodyFbx, "Assets/IronRequiem/Art/Player/ProtagonistV01/Rigged/IR_Protagonist_Rigged.fbx");
            var fpsModel = AssetDatabase.LoadAssetAtPath<GameObject>(FpsFbx); var bodyModel = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx);
            MakeVariant(V01Fps, FpsVariant, fpsModel); MakeVariant(V01Body, BodyVariant, bodyModel);
            var left = BakeLibrary(fpsModel, true); var right = BakeLibrary(fpsModel, false);
            MakePlayer(fpsModel, bodyModel, left, right);
        }

        static void Import(string target, string v01)
        {
            Directory.CreateDirectory(ArtDir);
            var src = Export + Path.GetFileName(target);
            if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(src))) File.Copy(src, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var a = (ModelImporter)AssetImporter.GetAtPath(v01); var b = (ModelImporter)AssetImporter.GetAtPath(target);
            b.globalScale = a.globalScale; b.useFileScale = a.useFileScale; b.bakeAxisConversion = a.bakeAxisConversion;
            b.animationType = a.animationType; b.avatarSetup = a.avatarSetup; b.optimizeGameObjects = false;
            b.materialImportMode = a.materialImportMode; b.importAnimation = true; b.importCameras = false; b.importLights = false;
            b.isReadable = a.isReadable; b.importNormals = a.importNormals; b.importTangents = a.importTangents;
            b.SaveAndReimport();
            log.AppendLine($"import {target}: animationType {b.animationType}, scale {b.globalScale}/{b.useFileScale}, bake {b.bakeAxisConversion}");
        }

        static Dictionary<string, Transform> ByName(Transform root)
        {
            var d = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (!d.ContainsKey(t.name)) d[t.name] = t;
            return d;
        }

        /// Swaps v02 meshes onto same-named SkinnedMeshRenderers under rigRoot and sets the rest/bind of their bones.
        static int ApplyV02(Transform rigRoot, GameObject model, string label)
        {
            var mine = ByName(rigRoot); var theirs = ByName(model.transform); int swapped = 0; var bones = new HashSet<Transform>();
            foreach (var smr in rigRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var src = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == smr.name);
                if (!src) continue;
                var mapped = src.bones.Select(b => b && mine.TryGetValue(b.name, out var t) ? t : null).ToArray();
                if (mapped.Any(b => !b)) throw new InvalidOperationException($"{label}/{smr.name}: bones missing under {rigRoot.name}");
                smr.sharedMesh = src.sharedMesh; smr.bones = mapped;
                smr.rootBone = src.rootBone && mine.TryGetValue(src.rootBone.name, out var rb) ? rb : smr.rootBone;
                smr.localBounds = src.localBounds; swapped++;
                foreach (var b in mapped) bones.Add(b);
            }
            float maxMove = 0;
            foreach (var b in bones)
            {
                if (!theirs.TryGetValue(b.name, out var s)) continue;
                maxMove = Mathf.Max(maxMove, (b.localPosition - s.localPosition).magnitude);
                b.localPosition = s.localPosition; b.localRotation = s.localRotation; b.localScale = s.localScale;
            }
            // Unique IR-ACOPLE-01 sockets: align the same-named guides (duplicates .001/.002 are hidden).
            foreach (var name in new[] { "Wrist_R", "Wrist_L", "ModuleSocket_R", "ModuleSocket_L", "Elbow_R", "Elbow_L" })
                if (mine.TryGetValue(name, out var t) && theirs.TryGetValue(name, out var s))
                {
                    var rel = Matrix4x4.TRS(model.transform.InverseTransformPoint(s.position), Quaternion.Inverse(model.transform.rotation) * s.rotation, Vector3.one);
                    var anchor = mine.TryGetValue(model.transform.GetChild(0).name, out var a) ? a.parent : rigRoot;
                    t.SetPositionAndRotation(anchor.TransformPoint(rel.GetPosition()), anchor.rotation * rel.rotation);
                }
            foreach (var t in rigRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(".001") || t.name.EndsWith(".002")) t.gameObject.SetActive(false);
            log.AppendLine($"{label}: swapped {swapped} renderers, {bones.Count} bones re-bound (max bone move {maxMove * 1000:F1} mm)");
            return swapped;
        }

        static void MakeVariant(string basePath, string outPath, GameObject model)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath), preview);
                inst.name = Path.GetFileNameWithoutExtension(outPath);
                if (ApplyV02(inst.transform, model, inst.name) == 0) throw new InvalidOperationException("No renderer swapped for " + basePath);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                PrefabUtility.SaveAsPrefabAsset(inst, outPath, out bool ok); if (!ok) throw new InvalidOperationException("Save failed " + outPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        public static AnimationClip PoseClip(string fbx, string pose) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).Single(c => c.name.EndsWith("IR_v02_" + pose));

        static HandPoseLibrary BakeLibrary(GameObject model, bool left)
        {
            string side = left ? "L" : "R";
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                var all = ByName(inst.transform); var hand = all["Hand_" + side];
                var skin = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Hand_" + side + "_FPS");
                var bones = skin.bones; var bind = skin.sharedMesh.bindposes; var index = new Dictionary<Transform, int>();
                for (int i = 0; i < bones.Length; i++) if (bones[i]) index[bones[i]] = i;
                var names = Fingers.SelectMany(f => Enumerable.Range(1, 3).Select(j => f + j + "_" + side)).ToArray();
                var chain = names.Select(n => all[n]).ToArray();
                // Bind local rotations from the mesh bind matrices (never from a sampled pose).
                var bindLocal = chain.Select(t => index.TryGetValue(t, out int c) && index.TryGetValue(t.parent, out int p)
                    ? Quaternion.Inverse(bind[p].inverse.rotation) * bind[c].inverse.rotation : t.localRotation).ToArray();
                var sampled = new Quaternion[Poses.Length][]; Vector3 openTip = Vector3.zero, fistTip = Vector3.zero;
                for (int k = 0; k < Poses.Length; k++)
                {
                    PoseClip(FpsFbx, Poses[k]).SampleAnimation(inst, 0);
                    sampled[k] = chain.Select(t => t.localRotation).ToArray();
                    if (Poses[k] == "Open") openTip = all["Middle3_" + side].position;
                    if (Poses[k] == "Fist") fistTip = all["Middle3_" + side].position;
                }
                for (int i = 0; i < chain.Length; i++) chain[i].localRotation = bindLocal[i];
                var direction = (all["Middle1_" + side].position - hand.position).normalized;
                var across = (all["Index1_" + side].position - all["Little1_" + side].position).normalized;
                var normal = Vector3.Cross(across, direction).normalized;
                if (Vector3.Dot(normal, fistTip - openTip) < 0) normal = -normal;
                var data = new HandPoseLibrary.BonePose[chain.Length];
                for (int i = 0; i < chain.Length; i++)
                    data[i] = new HandPoseLibrary.BonePose { name = names[i], bind = bindLocal[i], open = sampled[0][i], relaxed = sampled[1][i],
                        fist = sampled[2][i], grip = sampled[3][i], inspect = sampled[4][i] };
                Directory.CreateDirectory(PoseDir);
                var path = PoseDir + "/FPS_" + side + ".asset";
                var lib = AssetDatabase.LoadAssetAtPath<HandPoseLibrary>(path);
                if (!lib) { lib = ScriptableObject.CreateInstance<HandPoseLibrary>(); AssetDatabase.CreateAsset(lib, path); }
                lib.Configure(data, hand.InverseTransformDirection(direction), hand.InverseTransformDirection(normal));
                EditorUtility.SetDirty(lib); AssetDatabase.SaveAssetIfDirty(lib);
                log.AppendLine($"library {path}: {data.Length} bones; palm {hand.InverseTransformDirection(normal):F3}; forward {hand.InverseTransformDirection(direction):F3}");
                return lib;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static void MakePlayer(GameObject fpsModel, GameObject bodyModel, HandPoseLibrary left, HandPoseLibrary right)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerBase), preview);
                inst.name = Path.GetFileNameWithoutExtension(PlayerVariant);
                var hands = inst.GetComponent<ProtagonistHandsPresentation>();
                ApplyV02(hands.FpsRoot, fpsModel, "player FPS");
                var body = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == "Body_LOD0");
                if (body) ApplyV02(body.transform.parent, bodyModel, "player body"); else log.AppendLine("player body: no Body_LOD0 (shadow body absent)");
                BuildReceiver(hands);
                var controller = hands.GetComponent<HandPoseController>();
                if (!controller) controller = hands.gameObject.AddComponent<HandPoseController>();
                controller.Configure(hands.LeftHandBone, left, hands.RightHand, right, inst.GetComponent<PlayerCharacterController>().PlayerCamera.transform);
                hands.ConfigureHandPoses(controller);
                PrefabUtility.SaveAsPrefabAsset(inst, PlayerVariant, out bool ok); if (!ok) throw new InvalidOperationException("Save failed " + PlayerVariant);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        /// v07 modular arm: the right forearm receiver is the proximal .14 m of the FPS forearm (kept next to the elbow
        /// while a module replaces the rest), with its hidden proximal end stretched behind the camera. Same algorithm as
        /// ModularArmAuthoring, applied to the v02 forearm so the visible stub matches the refined arm.
        static void BuildReceiver(ProtagonistHandsPresentation hands)
        {
            var receiver = hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Forearm_R_Receiver");
            if (!receiver) { log.AppendLine("receiver: none in base player"); return; }
            var full = hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Forearm_R_FPS");
            var all = ByName(hands.FpsRoot); var bone = all["Forearm_R"]; var hand = all["Hand_R"];
            int boneIndex = Array.IndexOf(full.bones, bone); if (boneIndex < 0) throw new InvalidOperationException("Forearm binding missing");
            var axis = bone.InverseTransformPoint(hand.position).normalized; var mesh = UnityEngine.Object.Instantiate(full.sharedMesh);
            float worldScale = bone.TransformVector(axis).magnitude; var bind = mesh.bindposes[boneIndex]; var inverseBind = bind.inverse;
            var source = mesh.vertices;
            var along = source.Select(v => Vector3.Dot(bind.MultiplyPoint3x4(v), axis) * worldScale).ToArray();
            const float retained = .14f; var tris = mesh.triangles;
            var kept = Enumerable.Range(0, tris.Length / 3).Where(i => Enumerable.Range(0, 3).All(j => along[tris[i * 3 + j]] <= retained + .004f))
                .SelectMany(i => new[] { tris[i * 3], tris[i * 3 + 1], tris[i * 3 + 2] }).ToArray();
            if (kept.Length < 12 || kept.Length == tris.Length) throw new InvalidOperationException($"Invalid v02 forearm partition {kept.Length}/{tris.Length}");
            mesh.subMeshCount = 1; mesh.triangles = kept;
            var moved = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                float extension = .18f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .075f, along[i])));
                moved[i] = inverseBind.MultiplyPoint3x4(bind.MultiplyPoint3x4(source[i]) - axis * (extension / worldScale));
            }
            mesh.vertices = moved; mesh.RecalculateBounds(); mesh.name = "Protagonist_RightForearm_Receiver_v02";
            var path = "Assets/IronRequiem/Art/Player/ProtagonistV02/Protagonist_RightForearm_Receiver_v02.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); }
            else AssetDatabase.CreateAsset(mesh, path);
            receiver.sharedMesh = mesh; receiver.bones = full.bones; receiver.rootBone = full.rootBone; receiver.localBounds = mesh.bounds;
            receiver.sharedMaterials = full.sharedMaterials.Take(1).ToArray();
            log.AppendLine($"receiver v02: {kept.Length / 3}/{tris.Length / 3} triangles retained (proximal {retained} m) -> {path}");
        }

        [MenuItem("Tools/Iron Requiem/Protagonist v02/Build Lab (copy)")]
        public static void BuildLab()
        {
            log.Clear();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!File.Exists(LabScene) && !AssetDatabase.CopyAsset(LabSource, LabScene)) throw new InvalidOperationException("Copy failed");
            var scene = EditorSceneManager.OpenScene(LabScene, OpenSceneMode.Additive);
            try
            {
                var pc = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerCharacterController>(true)).Single();
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(pc.gameObject);
                var target = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVariant);
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) != target)
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(root, target, new PrefabReplacingSettings { logInfo = false }, InteractionMode.AutomatedAction);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("lab player -> " + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetOutermostPrefabInstanceRoot(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerCharacterController>(true)).Single().gameObject))));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [MenuItem("Tools/Iron Requiem/Protagonist v02/Verify")]
        public static void Verify()
        {
            string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "[{0:F4}, {1:F4}, {2:F4}]", v.x, v.y, v.z);
            var sb = new StringBuilder("{\n");
            foreach (var (path, fbx) in new[] { (FpsVariant, FpsFbx), (BodyVariant, BodyFbx) })
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path); var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (!p) { sb.Append($"  \"{Path.GetFileNameWithoutExtension(path)}\": null,\n"); continue; }
                var theirs = ByName(model.transform);
                var rows = p.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s =>
                {
                    var m = s.sharedMesh; int maxInfluence = 0; var bpv = m.GetBonesPerVertex(); for (int i = 0; i < bpv.Length; i++) maxInfluence = Mathf.Max(maxInfluence, bpv[i]);
                    long tris = Enumerable.Range(0, m.subMeshCount).Sum(k => (long)m.GetIndexCount(k)) / 3;
                    float boneErr = s.bones.Where(b => b && theirs.ContainsKey(b.name)).Select(b => (b.localPosition - theirs[b.name].localPosition).magnitude).DefaultIfEmpty(0).Max();
                    return $"    {{\"name\": \"{s.name}\", \"mesh\": \"{AssetDatabase.GetAssetPath(m).Split('/').Last()}\", \"tris\": {tris}, \"maxBonesPerVertex\": {maxInfluence}, \"boneRestError_m\": {boneErr.ToString("F6", CultureInfo.InvariantCulture)}}}";
                });
                sb.Append($"  \"{Path.GetFileNameWithoutExtension(path)}\": {{\"isVariant\": {(PrefabUtility.GetPrefabAssetType(p) == PrefabAssetType.Variant).ToString().ToLowerInvariant()}, \"base\": \"{AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(p))}\", \"renderers\": [\n{string.Join(",\n", rows)}\n  ]}},\n");
            }
            foreach (var side in new[] { "L", "R" })
            {
                var lib = AssetDatabase.LoadAssetAtPath<HandPoseLibrary>(PoseDir + "/FPS_" + side + ".asset");
                if (!lib) { sb.Append($"  \"FPS_{side}\": null,\n"); continue; }
                float maxFist = lib.Bones.Max(b => Quaternion.Angle(b.open, b.fist));
                sb.Append($"  \"FPS_{side}\": {{\"bones\": {lib.Bones.Length}, \"names\": \"{string.Join(",", lib.Bones.Select(b => b.name))}\", \"maxOpenToFist_deg\": {maxFist.ToString("F1", CultureInfo.InvariantCulture)}, \"palm\": {V(lib.PalmNormal)}, \"forward\": {V(lib.FingerDirection)}}},\n");
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVariant);
            if (player)
            {
                var c = player.GetComponent<HandPoseController>(); var hands = player.GetComponent<ProtagonistHandsPresentation>();
                var fpsMeshes = hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => s.name + ":" + (s.sharedMesh ? AssetDatabase.GetAssetPath(s.sharedMesh).Split('/').Last() : "null"));
                sb.Append($"  \"player\": {{\"isVariant\": {(PrefabUtility.GetPrefabAssetType(player) == PrefabAssetType.Variant).ToString().ToLowerInvariant()}, \"handPoseController\": {(c ? "true" : "false")}, \"fpsMeshes\": \"{string.Join("; ", fpsMeshes)}\"}},\n");
            }
            sb.Append($"  \"lab\": {(File.Exists(LabScene) ? "\"" + LabScene + "\"" : "null")}\n}}\n");
            Directory.CreateDirectory(ReportDir); File.WriteAllText(ReportDir + "/verify.json", sb.ToString());
        }
    }
}
