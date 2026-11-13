using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Named local authoring job; writes new derived assets/lab, never saves original scenes.
    [InitializeOnLoad]
    public static class T07DestructionBuilder
    {
        public const string Art = "Assets/IronRequiem/Art/Enemies/T07Destructible";
        public const string Enemy = "Assets/IronRequiem/Prefabs/Enemies/T07_Common_Destructible.prefab";
        public const string Burst = "Assets/IronRequiem/Prefabs/VFX/T07_BodyExplosion.prefab";
        public const string Lab = "Assets/IronRequiem/Scenes/T07DestructionLab.unity";
        public const string Reports = "Tools/IronRequiem/Reports/T07Destruction";
        const string Request = "Tools/IronRequiem/Requests/t07-destruction.json";
        const string Model = Art + "/T07_ArmorRig.fbx";
        static readonly string[] Names = { "Pectoral_L", "Shoulder_R", "Pectoral_R", "Shoulder_L" };
        static T07DestructionBuilder() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            string operation = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                if (operation != "build") throw new InvalidOperationException("Unknown destruction operation");
                Build(); File.WriteAllText(Reports + "/build-status.txt", "SUCCESS " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception e) { File.WriteAllText(Reports + "/build-status.txt", e.ToString()); Debug.LogException(e); }
        }
        public static void Build()
        {
            T07AnimationBuilder.RequireClean();
            if (!File.Exists("Tools/IronRequiem/Backups/20261030-t07-destruction/baseline.json")) throw new InvalidOperationException("Missing preservation backup");
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.optimizeGameObjects = false;
            importer.importCameras = importer.importLights = importer.addCollider = false; importer.isReadable = true; importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Enemies/T07New/Materials/T07_URP.mat");
            var materials = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                string texturePath = Art + "/DamageAlbedo_" + (i + 1) + ".png";
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                textureImporter.sRGBTexture = true; textureImporter.mipmapEnabled = true; textureImporter.maxTextureSize = 1024;
                textureImporter.textureCompression = TextureImporterCompression.CompressedHQ; textureImporter.SaveAndReimport();
                string path = Art + "/DamageStage_" + (i + 1) + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(sourceMaterial); AssetDatabase.CreateAsset(material, path); }
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); materials[i] = material;
            }
            var inside = AssetDatabase.LoadAssetAtPath<Material>(Art + "/ExposedMechanism.mat");
            if (!inside)
            {
                inside = new Material(Shader.Find("Universal Render Pipeline/Lit")); inside.SetColor("_BaseColor", new Color(.07f, .10f, .12f));
                inside.SetFloat("_Metallic", .6f); inside.SetFloat("_Smoothness", .3f); AssetDatabase.CreateAsset(inside, Art + "/ExposedMechanism.mat");
            }
            T07ScenePromotion.InScratchScene(() =>
            {
                BuildBurst();
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(T07OptimizationBuilder.Enemy));
                try
                {
                    go.name = "T07_Common_Destructible";
                    var driver = go.GetComponent<T07AnimationDriver>();
                    var sourceSkins = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("Body_LOD")).OrderBy(s => s.name).ToArray();
                    if (sourceSkins.Length != 3) throw new InvalidOperationException("Expected three original body skins");
                    var bones = sourceSkins[0].bones.ToDictionary(b => b.name);
                    var bodyGroup = go.GetComponentsInChildren<LODGroup>(true).Single(g => g.name == "Body");
                    var heights = bodyGroup.GetLODs().Select(l => l.screenRelativeTransitionHeight).ToArray();
                    var rigRoot = sourceSkins[0].transform.parent;
                    // Remove automatic nested FBX LODGroups in THIS variant; only one owner per renderer.
                    foreach (var nested in bodyGroup.GetComponentsInChildren<LODGroup>(true)) if (nested != bodyGroup) Object.DestroyImmediate(nested);
                    var renderers = new List<Renderer>(); var levels = new List<Renderer>[3];
                    for (int level = 0; level < 3; level++)
                    {
                        levels[level] = new List<Renderer>();
                        var imported = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Body_LOD" + level);
                        CopySkin(imported, sourceSkins[level], bones, inside, sourceMaterial);
                        levels[level].Add(sourceSkins[level]); renderers.Add(sourceSkins[level]);
                    }
                    var plates = new RobotArmorPiece[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var root = Child(rigRoot, "Plate_" + Names[i]);
                        var plate = root.gameObject.AddComponent<RobotArmorPiece>(); plates[i] = plate;
                        for (int level = 0; level < 3; level++)
                        {
                            var imported = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Armor_" + Names[i] + "_LOD" + level);
                            var skin = Child(root, imported.name).gameObject.AddComponent<SkinnedMeshRenderer>();
                            CopySkin(imported, skin, bones, inside, sourceMaterial); levels[level].Add(skin); renderers.Add(skin);
                            if (level != 0) continue;
                            var weights = imported.sharedMesh.boneWeights; int boneIndex = weights[0].boneIndex0;
                            if (weights.Any(w => w.weight0 < .999f || w.boneIndex0 != boneIndex)) throw new InvalidOperationException("Armor must be genuinely rigid-weighted: " + imported.name);
                            var bone = bones[imported.bones[boneIndex].name]; var matrix = imported.sharedMesh.bindposes[boneIndex];
                            var vertices = imported.sharedMesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
                            var bounds = new Bounds(vertices[0], Vector3.zero); foreach (var v in vertices) bounds.Encapsulate(v);
                            var mesh = Object.Instantiate(imported.sharedMesh); mesh.name = "Debris_" + Names[i];
                            mesh.vertices = vertices.Select(v => v - bounds.center).ToArray(); mesh.bindposes = Array.Empty<Matrix4x4>(); mesh.boneWeights = Array.Empty<BoneWeight>();
                            var capTriangles = Enumerable.Range(1, imported.sharedMesh.subMeshCount - 1).SelectMany(s => imported.sharedMesh.GetTriangles(s)).ToArray();
                            var outsideTriangles = imported.sharedMesh.GetTriangles(0); mesh.subMeshCount = 2;
                            mesh.SetTriangles(outsideTriangles, 0); mesh.SetTriangles(capTriangles, 1);
                            mesh.RecalculateNormals(); mesh.RecalculateBounds();
                            string debrisPath = Art + "/Debris_" + Names[i] + ".asset";
                            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(debrisPath);
                            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; AssetDatabase.SaveAssetIfDirty(mesh); }
                            else AssetDatabase.CreateAsset(mesh, debrisPath);
                            var socket = Child(bone, "ArmorSocket_" + Names[i]); socket.localPosition = bounds.center;
                            plate.Configure(socket, mesh, i < 2 ? 1 : 2, inside);
                        }
                    }
                    bodyGroup.SetLODs(Enumerable.Range(0, 3).Select(i => new LOD(heights[i], levels[i].ToArray())).ToArray()); bodyGroup.fadeMode = LODFadeMode.None;
                    var chest = bones["Chest"];
                    var sparks = Loop(chest, "DamageSparks", true);
                    sparks.transform.position = chest.position + go.transform.forward * .21f + go.transform.up * .04f;
                    var smoke = Loop(chest, "BodyDamageSmoke", false);
                    smoke.transform.position = chest.position + go.transform.forward * .19f + go.transform.right * .08f + go.transform.up * .03f;
                    var progression = go.AddComponent<RobotDamageProgression>();
                    progression.Configure(renderers.ToArray(), materials, inside, plates, sparks, smoke, chest, AssetDatabase.LoadAssetAtPath<GameObject>(Burst));
                    var legacy = go.GetComponent<EnemyController>().DeathVfx;
                    bool damaging = legacy.GetComponentInChildren<DamageArea>(true) || legacy.GetComponentInChildren<CannonExplosion>(true) || legacy.GetComponentInChildren<ProjectileBase>(true);
                    var serialized = new SerializedObject(progression); serialized.FindProperty("preserveLegacyDeathEffect").boolValue = damaging; serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(go, Enemy);
                    File.WriteAllText(Reports + "/authoring.txt", "source=" + T07OptimizationBuilder.Enemy + "\nlegacyDeath=" + AssetDatabase.GetAssetPath(legacy) + "\nlegacyDamaging=" + damaging + "\nbones=" + bones.Count + "\nLOD renderers=" + string.Join(",", levels.Select(l => l.Count)) + "\ntriangles=" + string.Join(",", levels.Select(l => l.Sum(s => ((SkinnedMeshRenderer)s).sharedMesh.triangles.Length / 3))));
                }
                finally { Object.DestroyImmediate(go); }
            });
            T07ScenePromotion.RestoreTransientDirtyFlags();
            if (!File.Exists(Lab)) AssetDatabase.CopyAsset(T07OptimizationBuilder.Lab, Lab);
            T07ScenePromotion.ReplaceScene(Lab, T07OptimizationBuilder.Enemy, Enemy);
            IronRequiemSetupTools.WriteAudit();
        }
        static void CopySkin(SkinnedMeshRenderer imported, SkinnedMeshRenderer target, Dictionary<string, Transform> bones, Material inside, Material outside)
        {
            target.sharedMesh = imported.sharedMesh; target.bones = imported.bones.Select(b => bones[b.name]).ToArray();
            target.rootBone = bones["Root"]; target.updateWhenOffscreen = false;
            target.sharedMaterials = Enumerable.Range(0, imported.sharedMesh.subMeshCount).Select(i => i == 0 ? outside : inside).ToArray();
            var bounds = imported.localBounds; bounds.Expand(bounds.size.magnitude * .7f); target.localBounds = bounds;
        }
        static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static ParticleSystem Loop(Transform parent, string name, bool electric)
        {
            var ps = Child(parent, name).gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = electric ? 12 : 8;
            main.startLifetime = electric ? new ParticleSystem.MinMaxCurve(.08f, .24f) : new ParticleSystem.MinMaxCurve(.45f, .85f);
            main.startSpeed = electric ? new ParticleSystem.MinMaxCurve(.6f, 2) : new ParticleSystem.MinMaxCurve(.1f, .3f);
            main.startSize = electric ? new ParticleSystem.MinMaxCurve(.008f, .022f) : new ParticleSystem.MinMaxCurve(.12f, .2f);
            main.startColor = electric ? new Color(1, .65f, .12f, 1) : new Color(.13f, .15f, .16f, .28f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Local;
            var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .05f;
            var color = ps.colorOverLifetime; color.enabled = true; var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            if (!electric) { var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .35f; }
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(T07ExplosionAuthoring.ExplosionPath);
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = template.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == (electric ? "HotSparks" : "ResidualSmoke")).GetComponent<ParticleSystemRenderer>().sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.maxParticleSize = .035f;
            return ps;
        }
        static void BuildBurst()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(T07ExplosionAuthoring.ExplosionPath));
            try
            {
                go.name = "T07_BodyExplosion";
                foreach (var behavior in go.GetComponentsInChildren<MonoBehaviour>(true)) if (behavior) Object.DestroyImmediate(behavior);
                foreach (var light in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                var ring = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PressureWave"); if (ring) Object.DestroyImmediate(ring.gameObject);
                string[] names = { "InitialFlash", "ExpandingCore", "HotSparks", "VisualFragments", "ResidualSmoke" }; int[] counts = { 1, 4, 12, 4, 8 };
                var layers = new ParticleSystem[5];
                for (int i = 0; i < names.Length; i++)
                {
                    var ps = go.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == names[i]); layers[i] = ps;
                    var main = ps.main; main.maxParticles = counts[i]; main.playOnAwake = false;
                    main.startSizeMultiplier *= .65f; main.startSpeedMultiplier *= .55f;
                    var emission = ps.emission; emission.enabled = true; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)counts[i]) });
                }
                var overload = Loop(go.transform, "BodyOverload", true); var e = overload.emission; e.rateOverTime = 30;
                go.AddComponent<RobotBodyBurst>().Configure(overload, layers); go.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(go, Burst);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
