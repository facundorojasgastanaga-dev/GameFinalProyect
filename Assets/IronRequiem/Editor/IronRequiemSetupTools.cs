using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    // Local, explicit, one-shot setup requests. No socket, arbitrary code execution or scene writes.
    [InitializeOnLoad]
    public static class IronRequiemSetupTools
    {
        const string ModelPath = "Assets/IronRequiem/Art/Enemies/Exterminador/Models/Meshy_AI_Rustborn_Sentinel_20261028_texture.fbx";
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static readonly string Reports = Path.Combine(Root, "Tools/IronRequiem/Reports");
        static readonly string RequestPath = Path.Combine(Root, "Tools/IronRequiem/Requests/request.json");
        static AddRequest add;
        static Request active;
        static double nextCheck;
        static double auditAfter;

        [Serializable] public class Request { public string id; public string operation; public string package; public string version; public string path; }
        [Serializable] public class Result { public string id; public string operation; public string status; public string detail; public string utc; }
        [Serializable] public class Package { public string name; public string version; public string source; }
        [Serializable] public class Node { public string path; public string type; public Vector3 position; public Vector3 rotation; public Vector3 scale; }
        [Serializable] public class MeshData { public string name; public int vertices; public long triangles; public int subMeshes; public int bindPoses; public int blendShapes; public Bounds bounds; }
        [Serializable] public class Skin { public string name; public int bones; public string rootBone; }
        [Serializable] public class MaterialData { public string name; public string shader; public bool supported; public string[] textures; }
        [Serializable] public class SceneData { public string path; public bool dirty; public int missingScripts; }
        [Serializable] public class Audit
        {
            public string utc; public string unity; public string pipeline; public bool playing;
            public string graphicsDevice; public bool supportsComputeShaders;
            public Package[] packages; public SceneData[] scenes; public Node[] modelHierarchy;
            public MeshData[] meshes; public Skin[] skins; public MaterialData[] materials;
            public string[] clips; public string[] avatars; public string importerType; public float importerScale;
            public string[] compilationErrors;
            public string dotweenVersion; public bool dotweenModulesAssembly; public string dotweenSettingsPath;
        }

        static IronRequiemSetupTools()
        {
            EditorApplication.update += Update;
            CompilationPipeline.compilationStarted += _ => SessionState.SetString("IronRequiem.CompileErrors", "");
            CompilationPipeline.assemblyCompilationFinished += (_, messages) =>
            {
                var errors = messages.Where(m => m.type == CompilerMessageType.Error).Select(m => m.message).ToArray();
                if (errors.Length > 0) SessionState.SetString("IronRequiem.CompileErrors", string.Join("\n", errors));
            };
            auditAfter = EditorApplication.timeSinceStartup + 3;
        }

        static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (add != null)
            {
                if (!add.IsCompleted) return;
                WriteResult(active, add.Status == StatusCode.Success ? "resolved" : "failed",
                    add.Status == StatusCode.Success ? add.Result.packageId : add.Error.message);
                add = null;
                active = null;
                auditAfter = EditorApplication.timeSinceStartup + 5;
            }
            if (auditAfter > 0 && EditorApplication.timeSinceStartup >= auditAfter)
            {
                auditAfter = 0;
                WriteAudit();
            }
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(RequestPath)) return;
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            if (request == null || string.IsNullOrWhiteSpace(request.id) || SessionState.GetString("IronRequiem.LastRequest", "") == request.id) return;
            SessionState.SetString("IronRequiem.LastRequest", request.id);
            try
            {
                if (request.operation == "audit") { WriteAudit(); WriteResult(request, "verified", "Read-only audit complete."); }
                else if (request.operation == "refresh")
                {
                    AssetDatabase.Refresh();
                    WriteResult(request, "refreshed", "AssetDatabase refresh requested; compilation verification follows separately.");
                    auditAfter = EditorApplication.timeSinceStartup + 5;
                }
                else if (request.operation == "install")
                {
                    bool allowed = (request.package == "com.unity.animation.rigging" && request.version == "1.4.1") ||
                                   (request.package == "com.unity.cinemachine" && request.version == "3.1.7");
                    if (!allowed) throw new InvalidOperationException("Package/version is outside this setup's stable allowlist.");
                    SessionState.SetString("IronRequiem.CompileErrors", "");
                    active = request;
                    WriteResult(request, "requested", request.package + "@" + request.version);
                    add = Client.Add(request.package + "@" + request.version);
                }
                else if (request.operation == "configure-dotween")
                {
                    // These are the publisher's setup operations, verified in the downloaded DLL.
                    // Fresh installation has no legacy files to remove. Save only the new settings.
                    var settings = CallDOTween("DG.DOTweenEditor.UI.DOTweenUtilityWindow", "GetDOTweenSettings") as UnityEngine.Object;
                    if (!settings)
                    {
                        var utilityType = AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType("DG.DOTweenEditor.UI.DOTweenUtilityWindow")).First(t => t != null);
                        var connect = utilityType.GetMethod("ConnectToSource", BindingFlags.NonPublic | BindingFlags.Static);
                        // Publisher parameters: src, createIfMissing, fullSetup. Do not move existing assets.
                        settings = connect?.Invoke(null, new object[] { null, true, false }) as UnityEngine.Object;
                    }
                    if (!settings) throw new InvalidOperationException("DOTween settings could not be initialized.");
                    CallDOTween("DG.DOTweenEditor.UI.DOTweenUtilityWindowModules", "ApplyModulesSettings");
                    CallDOTween("DG.DOTweenEditor.ASMDEFManager", "CreateAllASMDEF");
                    AssetDatabase.SaveAssetIfDirty(settings);
                    WriteResult(request, "configured", "Publisher module setup and module assembly definition created.");
                    auditAfter = EditorApplication.timeSinceStartup + 5;
                }
                else throw new InvalidOperationException("Unknown setup operation.");
            }
            catch (Exception e) { WriteResult(request, "failed", e.ToString()); }
        }

        static object CallDOTween(string typeName, string methodName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
            var method = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null) throw new InvalidOperationException("Official DOTween setup method unavailable: " + typeName + "." + methodName);
            return method.Invoke(null, null);
        }

        static void WriteResult(Request request, string status, string detail)
        {
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Path.Combine(Reports, "request-" + request.id + ".json"), JsonUtility.ToJson(new Result
            { id = request.id, operation = request.operation, status = status, detail = detail, utc = DateTime.UtcNow.ToString("O") }, true));
        }

        [MenuItem("Tools/Iron Requiem/Audit Project and T-07 (Read Only)")]
        public static void WriteAudit()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var objects = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var scenes = new List<SceneData>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                int missing = scene.isLoaded ? scene.GetRootGameObjects().Sum(go => go.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))) : 0;
                scenes.Add(new SceneData { path = scene.path, dirty = scene.isDirty, missingScripts = missing });
            }
            var report = new Audit
            {
                utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "Built-in",
                playing = EditorApplication.isPlaying, graphicsDevice = SystemInfo.graphicsDeviceName, supportsComputeShaders = SystemInfo.supportsComputeShaders,
                scenes = scenes.ToArray(),
                packages = PackageInfo.GetAllRegisteredPackages().Select(p => new Package { name = p.name, version = p.version, source = p.source.ToString() }).ToArray(),
                importerType = importer ? importer.animationType.ToString() : "missing", importerScale = importer ? importer.globalScale : 0,
                modelHierarchy = model ? model.GetComponentsInChildren<Transform>(true).Select(t => new Node { path = AnimationUtility.CalculateTransformPath(t, model.transform), type = t.GetComponent<SkinnedMeshRenderer>() ? "SkinnedMeshRenderer" : t.GetComponent<MeshFilter>() ? "MeshFilter" : "Transform", position = t.localPosition, rotation = t.localEulerAngles, scale = t.localScale }).ToArray() : Array.Empty<Node>(),
                meshes = objects.OfType<Mesh>().Select(m => new MeshData { name = m.name, vertices = m.vertexCount, triangles = Enumerable.Range(0, m.subMeshCount).Sum(i => (long)(m.GetTopology(i) == MeshTopology.Triangles ? m.GetIndexCount(i) / 3 : 0)), subMeshes = m.subMeshCount, bindPoses = m.bindposeCount, blendShapes = m.blendShapeCount, bounds = m.bounds }).ToArray(),
                skins = model ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => new Skin { name = s.name, bones = s.bones.Length, rootBone = s.rootBone ? s.rootBone.name : "" }).ToArray() : Array.Empty<Skin>(),
                materials = objects.OfType<Material>().Select(m => new MaterialData { name = m.name, shader = m.shader ? m.shader.name : "missing", supported = m.shader && m.shader.isSupported, textures = m.GetTexturePropertyNames().Where(p => m.GetTexture(p)).Select(p => p + ": " + m.GetTexture(p).name).ToArray() }).ToArray(),
                clips = objects.OfType<AnimationClip>().Select(c => c.name + " (" + c.length + " s; curves=" + AnimationUtility.GetCurveBindings(c).Length + ")").ToArray(),
                avatars = objects.OfType<Avatar>().Select(a => a.name + ": valid=" + a.isValid + ", human=" + a.isHuman).ToArray(),
                compilationErrors = SessionState.GetString("IronRequiem.CompileErrors", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries),
                dotweenVersion = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DG.Tweening.DOTween")).FirstOrDefault(t => t != null)?.GetField("Version", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)?.ToString() ?? "not installed",
                dotweenModulesAssembly = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "DOTween.Modules"),
                dotweenSettingsPath = AssetDatabase.FindAssets("DOTweenSettings").Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => p.EndsWith("/DOTweenSettings.asset", StringComparison.Ordinal)) ?? ""
            };
            Directory.CreateDirectory(Reports);
            // Another program may hold the report open (Win32 1224 = file mapped by a viewer or editor): that is not an Editor error.
            try
            {
                File.WriteAllText(Path.Combine(Reports, "unity-audit.json"), JsonUtility.ToJson(report, true));
                Debug.Log("IRON REQUIEM: read-only audit written to Tools/IronRequiem/Reports/unity-audit.json");
            }
            catch (IOException e) { Debug.LogWarning("IRON REQUIEM: audit not written, unity-audit.json is open in another program (" + e.Message + ")"); }
        }
    }
}
