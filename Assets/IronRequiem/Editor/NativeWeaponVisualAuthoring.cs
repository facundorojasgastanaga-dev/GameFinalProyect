using System;
using System.IO;
using System.Linq;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class NativeWeaponVisualAuthoring
    {
        public const string Weapon = "Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_NativeVisual_v06.prefab";
        public const string Player = "Assets/IronRequiem/Prefabs/Player/Player_NativeVisual_v06.prefab";
        public const string Lab = "Assets/IronRequiem/Scenes/NativeWeaponVisualLab.unity";
        const string Art = "Assets/IronRequiem/Art/Weapons/NativeVisualV06", Reports = "Tools/IronRequiem/Reports/NativeWeaponVisual", Queue = "Tools/IronRequiem/Requests/native-weapon-visual.txt", Key = "IRON.NativeWeaponVisual";
        static readonly DateTime LoadedAt = DateTime.UtcNow;
        static TestRunnerApi api;
        static NativeWeaponVisualAuthoring() { EditorApplication.update += Tick; if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register; }
        static bool Dirty() => Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + ".restore", false) && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".at", 0) > 3) { SessionState.EraseBool(Key + ".restore"); if (!Dirty()) { EditorSceneManager.OpenScene(Lab); IronRequiemSetupTools.WriteAudit(); } }
            if (!File.Exists(Queue)) return;
            foreach (var p in new[] { "Assets/IronRequiem/Editor/NativeWeaponVisualAuthoring.cs", "Assets/IronRequiem/Tests/PlayMode/NativeWeaponVisualTests.cs" }) if (File.GetLastWriteTimeUtc(p) > LoadedAt) return;
            var op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Reports);
            try { if (Dirty()) throw new InvalidOperationException("Preserving dirty user scene"); if (op == "build") { Build(); File.WriteAllText(Queue, "test"); } else if (op == "polish") { Polish(); File.WriteAllText(Queue, "test"); } else if (op == "test") { Register(); SessionState.SetBool(Key, true); api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "IronRequiem.Tests.NativeWeaponVisualTests" } })); } else throw new InvalidOperationException("Unknown native weapon operation"); }
            catch (Exception e) { File.WriteAllText(Reports + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static void Variant(string source, string dest)
        {
            if (File.Exists(dest)) throw new InvalidOperationException("Preserve existing " + dest);
            var scene = EditorSceneManager.NewPreviewScene(); try { var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), scene); go.name = Path.GetFileNameWithoutExtension(dest); PrefabUtility.SaveAsPrefabAsset(go, dest); } finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static Transform Named(GameObject go, string name) => go.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        static void Build()
        {
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            AssetDatabase.CopyAsset(HitWeaponAuthoring.Art + "/Weapon_PlayerSurface.mat", Art + "/PlayerSurface.mat");
            AssetDatabase.CopyAsset(HitWeaponAuthoring.Art + "/Weapon_PlayerCyanEnergy.mat", Art + "/PlayerCyanEnergy.mat");
            var surface = AssetDatabase.LoadAssetAtPath<Material>(Art + "/PlayerSurface.mat"); var energy = AssetDatabase.LoadAssetAtPath<Material>(Art + "/PlayerCyanEnergy.mat");
            Variant("Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab", Weapon);
            var go = PrefabUtility.LoadPrefabContents(Weapon); var previous = PrefabUtility.LoadPrefabContents(HitWeaponAuthoring.Weapon);
            try
            {
                var target = go.GetComponent<WeaponController>(); var current = previous.GetComponent<WeaponController>();
                var nativeRoot = target.WeaponRoot; var nativeMuzzle = target.WeaponMuzzle; var nativeAnimator = target.WeaponAnimator; float nativeRecoil = target.RecoilForce;
                EditorUtility.CopySerialized(current, target);
                target.WeaponRoot = nativeRoot; target.WeaponMuzzle = nativeMuzzle; target.WeaponAnimator = nativeAnimator; target.RecoilForce = nativeRecoil;
                target.WeaponMuzzle.localPosition = current.WeaponMuzzle.localPosition;
                foreach (var r in nativeRoot.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
                var visual = Object.Instantiate(Named(previous, "PrimaryModuleVisual").gameObject, nativeRoot.transform);
                visual.name = "PrimaryModuleVisual";
                var previousVisual = Named(previous, "PrimaryModuleVisual"); visual.transform.localPosition = previousVisual.localPosition; visual.transform.localRotation = previousVisual.localRotation; visual.transform.localScale = previousVisual.localScale;
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = surface;
                var cells = go.GetComponent<WeaponFuelCellHandler>(); var oldCells = previous.GetComponent<WeaponFuelCellHandler>();
                for (int i = 0; i < cells.FuelCells.Length; i++)
                {
                    // Keep native moving transforms and controller bindings. Fit copied geometry inside those transforms.
                    var native = cells.FuelCells[i].transform; var old = oldCells.FuelCells[i].transform;
                    var oldFilter = old.GetComponent<MeshFilter>(); var filter = native.GetComponent<MeshFilter>(); var mesh = Object.Instantiate(oldFilter.sharedMesh); mesh.name = "CapsuleNativePivot_" + i;
                    mesh.vertices = mesh.vertices.Select(v => native.InverseTransformPoint(old.TransformPoint(v))).ToArray();
                    mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, Art + "/Capsule_" + i + ".asset"); filter.sharedMesh = mesh;
                    var renderer = native.GetComponent<MeshRenderer>(); renderer.enabled = true; renderer.sharedMaterials = old.GetComponent<MeshRenderer>().sharedMaterials.Select((m, index) => index == 0 ? surface : energy).ToArray();
                    var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder); stem.name = "MechanicalPiston"; Object.DestroyImmediate(stem.GetComponent<Collider>()); stem.transform.SetParent(native, false);
                    var axis = native.parent.up; var bounds = renderer.bounds; var origin = bounds.center;
                    float bottom = mesh.vertices.Min(v => Vector3.Dot(native.TransformPoint(v) - origin, axis));
                    stem.transform.SetPositionAndRotation(origin + axis * (bottom - .022f), native.parent.rotation);
                    stem.transform.localScale = new Vector3(.008f / native.lossyScale.x, .035f / native.lossyScale.y, .008f / native.lossyScale.z);
                    stem.GetComponent<MeshRenderer>().sharedMaterial = surface;
                }
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main; main.playOnAwake = false; ps.gameObject.SetActive(false); }
                var heat = go.GetComponent<OverheatBehavior>(); heat.OverheatingMaterial = energy; heat.SteamVfxEmissionRateMax = 0;
                PrefabUtility.SaveAsPrefabAsset(go, Weapon);
                File.WriteAllText(Reports + "/source.txt", "Direct variant of Weapon_Blaster. Native WeaponFuelCellHandler and PistonSocket transforms unchanged. No Animator/clips in source. Original recoil=" + nativeRecoil + "; current gameplay fields copied, projectile unchanged. Existing PlayerWeaponsManager owns ADS, bob and equip transitions. No runtime code changed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); PrefabUtility.UnloadPrefabContents(previous); }
            Variant(HitWeaponAuthoring.Player, Player);
            go = PrefabUtility.LoadPrefabContents(Player);
            try { var manager = go.GetComponent<PlayerWeaponsManager>(); var replacement = AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>(); for (int i = 0; i < manager.StartingWeapons.Count; i++) if (manager.StartingWeapons[i] == AssetDatabase.LoadAssetAtPath<GameObject>(HitWeaponAuthoring.Weapon).GetComponent<WeaponController>()) manager.StartingWeapons[i] = replacement; var data = new SerializedObject(go.GetComponent<ProtagonistHandsPresentation>()); data.FindProperty("pistolPrefab").objectReferenceValue = replacement; data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(go, Player); } finally { PrefabUtility.UnloadPrefabContents(go); }
            if (File.Exists(Lab)) throw new InvalidOperationException("Preserve existing lab"); AssetDatabase.CopyAsset(HitWeaponAuthoring.Lab, Lab); var scene = EditorSceneManager.OpenScene(Lab);
            var oldPlayer = Object.FindAnyObjectByType<PlayerCharacterController>().gameObject; var newPlayer = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player), scene); newPlayer.transform.SetPositionAndRotation(oldPlayer.transform.position, oldPlayer.transform.rotation); newPlayer.transform.localScale = oldPlayer.transform.localScale; Object.DestroyImmediate(oldPlayer); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Polish(); File.WriteAllText(Reports + "/build.txt", "Variant and separate lab saved. MainScene untouched.");
        }
        static void Polish()
        {
            var go = PrefabUtility.LoadPrefabContents(Weapon);
            try
            {
                var cells = go.GetComponent<WeaponFuelCellHandler>(); var root = go.GetComponent<WeaponController>().WeaponRoot.transform;
                for (int i = 0; i < cells.FuelCells.Length; i++)
                {
                    var native = cells.FuelCells[i].transform; var legacy = native.Find("MechanicalPiston"); if (legacy) Object.DestroyImmediate(legacy.gameObject);
                    var previous = root.Find("TelescopicSupport_" + i); if (previous) Object.DestroyImmediate(previous.gameObject);
                    var renderer = native.GetComponent<MeshRenderer>(); var origin = renderer.bounds.center; var axis = native.parent.up;
                    float bottom = native.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v => Vector3.Dot(native.TransformPoint(v) - origin, axis));
                    Vector3 center = origin + axis * bottom; var support = new GameObject("TelescopicSupport_" + i); support.transform.SetParent(root, false);
                    var vertices = new Vector3[16]; var weights = new BoneWeight[16]; var uv = new Vector2[16]; var triangles = new System.Collections.Generic.List<int>();
                    // Two rings follow existing native transforms: stationary socket and moving capsule. No extra animation authority.
                    for (int ring = 0; ring < 2; ring++) for (int j = 0; j < 8; j++)
                    {
                        int index = ring * 8 + j; float angle = j * Mathf.PI / 4;
                        Vector3 radial = (native.parent.right * Mathf.Cos(angle) + native.parent.forward * Mathf.Sin(angle)) * .0035f;
                        vertices[index] = root.InverseTransformPoint(center + axis * (ring == 0 ? -.010f : .006f) + radial);
                        weights[index] = new BoneWeight { boneIndex0 = ring, weight0 = 1 }; uv[index] = new Vector2(.515f, .61f);
                        if (ring == 0) { int next = (j + 1) % 8; triangles.AddRange(new[] { j, j + 8, next, next, j + 8, next + 8 }); }
                    }
                    var mesh = new Mesh { name = "NativeTelescopicSupport_" + i }; mesh.vertices = vertices; mesh.triangles = triangles.ToArray(); mesh.uv = uv; mesh.boneWeights = weights; mesh.bindposes = new[] { native.parent.worldToLocalMatrix * root.localToWorldMatrix, native.worldToLocalMatrix * root.localToWorldMatrix }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string meshPath = Art + "/TelescopicSupport_" + i + ".asset"; if (File.Exists(meshPath)) { EditorUtility.CopySerialized(mesh, AssetDatabase.LoadAssetAtPath<Mesh>(meshPath)); Object.DestroyImmediate(mesh); } else AssetDatabase.CreateAsset(mesh, meshPath);
                    var skin = support.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath); skin.bones = new[] { native.parent, native }; skin.rootBone = native.parent; skin.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "/PlayerSurface.mat"); skin.localBounds = new Bounds(root.InverseTransformPoint(center), Vector3.one * .25f); skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var gradient = new Gradient(); var cyan = new Color(.0868f, .4873f, .5433f); var orange = new Color(.8311f, .4324f, .0587f);
                gradient.SetKeys(new[] { new GradientColorKey(cyan * .2f, 0), new GradientColorKey(cyan * .7f, .6f), new GradientColorKey(orange, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                go.GetComponent<OverheatBehavior>().OverheatGradient = gradient;
                PrefabUtility.SaveAsPrefabAsset(go, Weapon); AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t) { } public void TestStarted(ITestAdaptor t) { } public void TestFinished(ITestResultAdaptor t) { }
            public void OnError(string m) { SessionState.EraseBool(Key); File.WriteAllText(Reports + "/runner-error.txt", m); }
            public void RunFinished(ITestResultAdaptor r) { if (!SessionState.GetBool(Key, false)) return; SessionState.EraseBool(Key); TestRunnerApi.SaveResultToFile(r, Reports + "/play.xml"); File.WriteAllText(Reports + "/status.txt", r.ResultState + " passed=" + r.PassCount + " failed=" + r.FailCount); SessionState.SetBool(Key + ".restore", true); SessionState.SetFloat(Key + ".at", (float)EditorApplication.timeSinceStartup); }
        }
    }
}
