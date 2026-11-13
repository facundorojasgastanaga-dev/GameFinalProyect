using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.AI.Navigation;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class SawBotSawPolishAuthoring
    {
        public const string Report = "Tools/IronRequiem/Reports/SawBotSawPolish";
        public const string Queue = "Tools/IronRequiem/Requests/sawbot-saw-polish.txt";
        public const string Source = "Assets/IronRequiem/Prefabs/Enemies/SawBot_Arsenal_v03.prefab";
        public const string Enemy = "Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab";
        public const string Weapon = "Assets/IronRequiem/Prefabs/Weapons/Weapon_SawBot_Animated_v02.prefab";
        public const string Pickup = "Assets/IronRequiem/Prefabs/Weapons/SawBot_AnimatedModule_v02.prefab";
        public const string Lab = "Assets/IronRequiem/Scenes/SawBotSawPolishLab.unity";
        const string Art = "Assets/IronRequiem/Art/Enemies/SawBotSawPolish";
        const string Motor = "Assets/IronRequiem/Audio/SFX/SawBot/SawMotor_Attack_Loop.wav";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static SawBotSawPolishAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue)) return;
            if (File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/SawBotSawPolishAuthoring.cs") > Loaded) return;
            var operation = File.ReadAllText(Queue).Trim();
            if (operation != "inspect" && Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            File.Delete(Queue);
            Directory.CreateDirectory(Report);
            try { if (operation == "inspect") Inspect(); else if (operation == "build") Build(); else if (operation == "fix-nav") RestoreLabNavigation(); else if (operation == "refit") RefitDeathFloor(); else if (operation == "smoke") PolishSmoke(); else throw new InvalidOperationException(operation); }
            catch (Exception exception) { File.WriteAllText(Report + "/error.txt", exception.ToString()); Debug.LogException(exception); }
        }
        static void Inspect()
        {
            var root = PrefabUtility.LoadPrefabContents(Source);
            try
            {
                var text = new StringBuilder();
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var bone in skins[0].bones) text.AppendLine(bone.name + " parent=" + bone.parent.name + " local=" + bone.localPosition.ToString("F5") + " rot=" + bone.localEulerAngles.ToString("F3"));
                foreach (var skin in skins.Where(s => s.name.EndsWith("LOD0")))
                {
                    var mesh = skin.sharedMesh; var weights = mesh.boneWeights; var vertices = mesh.vertices;
                    foreach (string side in new[] { "L", "R" }) foreach (string name in new[] { "GuideBar_" + side, "Chain_" + side })
                    {
                        int index = Array.FindIndex(skin.bones, b => b.name == name);
                        var points = vertices.Where((v, i) => weights[i].boneIndex0 == index).Select(v => mesh.bindposes[index].MultiplyPoint3x4(v)).ToArray();
                        if (points.Length == 0) continue;
                        var center = points.Aggregate(Vector3.zero, (a, b) => a + b) / points.Length;
                        var axis = Vector3.up;
                        for (int i = 0; i < 30; i++) { var sum = Vector3.zero; foreach (var point in points) { var d = point - center; sum += d * Vector3.Dot(d, axis); } axis = sum.normalized; }
                        if (Vector3.Dot(axis, center) < 0) axis = -axis;
                        var bone = skin.bones[index];
                        text.AppendLine(skin.name + " " + name + " count=" + points.Length + " center=" + center.ToString("F5") + " axis=" + axis.ToString("F5") + " wristAngle=" + Vector3.Angle(bone.TransformDirection(axis), bone.position - bone.parent.position));
                    }
                }
                File.WriteAllText(Report + "/inspection.txt", text.ToString());
                Capture(root, "before-front", new Vector3(1.7f, 1.1f, 2.2f));
                Capture(root, "before-right", new Vector3(2, .9f, .15f));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void Capture(GameObject root, string name, Vector3 offset)
        {
            var scene = root.scene;
            var camera = new GameObject("SawPolishCamera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            var light = new GameObject("SawPolishLight").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional; light.intensity = 3; light.transform.rotation = Quaternion.Euler(35, 140, 0);
            camera.transform.position = root.transform.position + offset; camera.transform.LookAt(root.transform.position + Vector3.up * .7f); camera.fieldOfView = 36;
            camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .18f);
            var previous = RenderTexture.active; var rt = new RenderTexture(1000, 800, 24); var image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); image.Apply(); File.WriteAllBytes(Report + "/" + name + ".png", image.EncodeToPNG()); }
            finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject); }
        }

        static void Save(Mesh mesh, string name) { AssetDatabase.CreateAsset(mesh, Art + "/" + name + ".asset"); }
        static Mesh WithoutChain(SkinnedMeshRenderer skin)
        {
            var input = skin.sharedMesh; var weights = input.boneWeights; var vertices = input.vertices; var normals = input.normals; var uv = input.uv; var tangents = input.tangents;
            var keep = new List<int>(); var triangles = new List<int>(); var map = new Dictionary<int, int>();
            var source = input.triangles;
            for (int t = 0; t < source.Length; t += 3)
            {
                bool chain = skin.bones[weights[source[t]].boneIndex0].name.StartsWith("Chain_");
                for (int k = 0; k < 3; k++) if (skin.bones[weights[source[t + k]].boneIndex0].name.StartsWith("Chain_") != chain) throw new Exception("Unexpected mixed rigid triangle");
                if (chain) continue;
                for (int k = 0; k < 3; k++) { int i = source[t + k]; if (!map.TryGetValue(i, out int index)) { index = keep.Count; map.Add(i, index); keep.Add(i); } triangles.Add(index); }
            }
            var mesh = new Mesh { name = skin.name + "_Fixed", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = keep.Select(i => vertices[i]).ToArray(); mesh.normals = keep.Select(i => normals[i]).ToArray(); mesh.uv = keep.Select(i => uv[i]).ToArray(); mesh.tangents = keep.Select(i => tangents[i]).ToArray();
            mesh.boneWeights = keep.Select(i => weights[i]).ToArray(); mesh.bindposes = input.bindposes; mesh.triangles = triangles.ToArray(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh ChainMesh(int level, int phase, Vector2 palette)
        {
            int steps = new[] { 80, 60, 40 }[level], links = new[] { 28, 24, 18 }[level]; const float length = .175f, radius = .035f;
            float perimeter = 2 * length + 2 * Mathf.PI * radius;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            Vector4 Path(float distance)
            {
                float q = Mathf.Repeat(distance, perimeter);
                if (q < length) return new Vector4(q, radius, 0, 1); q -= length;
                if (q < Mathf.PI * radius) { float a = Mathf.PI / 2 - q / radius; return new Vector4(length + radius * Mathf.Cos(a), radius * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a)); } q -= Mathf.PI * radius;
                if (q < length) return new Vector4(length - q, -radius, 0, -1); q -= length;
                float angle = -Mathf.PI / 2 - q / radius; return new Vector4(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), Mathf.Cos(angle), Mathf.Sin(angle));
            }
            void Face(params Vector3[] points)
            {
                int first = vertices.Count; var normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized; var u = (points[1] - points[0]).normalized; var v = Vector3.Cross(normal, u);
                foreach (var point in points) { vertices.Add(point); var d = point - points[0]; uv.Add(palette + new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v)) * .015f); }
                for (int i = 1; i < points.Length - 1; i++) { indices.Add(first); indices.Add(first + i); indices.Add(first + i + 1); }
            }
            Vector3 Ring(float distance, float radial, float depth) { var p = Path(distance); return new Vector3(p.y + p.w * radial, depth, p.x + p.z * radial); }
            void Band(params Vector3[] points) { Face(points.Reverse().ToArray()); }
            for (int i = 0; i < steps; i++)
            {
                float a = i * perimeter / steps, b = (i + 1) * perimeter / steps;
                Band(Ring(a, -.002f, -.012f), Ring(b, -.002f, -.012f), Ring(b, .002f, -.012f), Ring(a, .002f, -.012f));
                Band(Ring(a, .002f, .012f), Ring(b, .002f, .012f), Ring(b, -.002f, .012f), Ring(a, -.002f, .012f));
                Band(Ring(a, -.002f, .012f), Ring(b, -.002f, .012f), Ring(b, -.002f, -.012f), Ring(a, -.002f, -.012f));
                Band(Ring(a, .002f, -.012f), Ring(b, .002f, -.012f), Ring(b, .002f, .012f), Ring(a, .002f, .012f));
            }
            void Prism(Vector2[] points, float lo, float hi)
            {
                Face(points.Reverse().Select(p => new Vector3(p.y, lo, p.x)).ToArray()); Face(points.Select(p => new Vector3(p.y, hi, p.x)).ToArray());
                for (int i = 0; i < points.Length; i++) { var a = points[i]; var b = points[(i + 1) % points.Length]; Face(new Vector3(a.y, lo, a.x), new Vector3(b.y, lo, b.x), new Vector3(b.y, hi, b.x), new Vector3(a.y, hi, a.x)); }
            }
            for (int link = 0; link < links; link++)
            {
                var p = Path((link + .5f + phase / 8f * 2) * perimeter / links); var center = new Vector2(p.x, p.y); var tangent = new Vector2(p.w, -p.z); var normal = new Vector2(p.z, p.w);
                Vector2[] Polygon(Vector2[] points) => points.Select(v => center + tangent * v.x + normal * v.y).ToArray();
                float side = link % 2 == 0 ? -1 : 1;
                Prism(Polygon(new[] { new Vector2(-.006f, -.001f), new Vector2(.006f, -.001f), new Vector2(.006f, .008f), new Vector2(.001f, .009f) }), side * .007f - .0025f, side * .007f + .0025f);
                var shape = Polygon(new[] { new Vector2(-.006f, -.0025f), new Vector2(.006f, -.0025f), new Vector2(.006f, .0025f), new Vector2(-.006f, .0025f) }); Prism(shape, -.014f, -.012f); Prism(shape, .012f, .014f);
            }
            var mesh = new Mesh { name = "Chain_LOD" + level + "_Phase" + phase }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
        static SawChainMotion AddChain(Transform parent, Vector3 position, Quaternion rotation, Mesh[] frames, Material material, int layer)
        {
            var root = new GameObject("CirculatingChain"); root.layer = layer; root.transform.SetParent(parent, false); root.transform.localPosition = position; root.transform.localRotation = rotation;
            var filters = new MeshFilter[3];
            for (int lod = 0; lod < 3; lod++) { var view = new GameObject("Chain_LOD" + lod); view.layer = layer; view.transform.SetParent(root.transform, false); filters[lod] = view.AddComponent<MeshFilter>(); filters[lod].sharedMesh = frames[lod * 8]; var renderer = view.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; }
            var motion = root.AddComponent<SawChainMotion>(); motion.Configure(filters, frames); return motion;
        }
        static ParticleSystem Smoke(Transform parent, Vector3 local, int layer)
        {
            var go = new GameObject("SawMotorSmoke"); go.layer = layer; go.transform.SetParent(parent, false); go.transform.localPosition = local; var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.duration = 1; main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .65f); main.startSpeed = .04f; main.startSize = new ParticleSystem.MinMaxCurve(.045f, .085f); main.startColor = new Color(.35f, .37f, .39f, .22f); main.maxParticles = 12; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 7; var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .018f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .28f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .4f, 1, 1.8f));
            var fade = ps.colorOverLifetime; fade.enabled = true; var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(0, 1) }); fade.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FPS/Art/Materials/VFX/SmokeOverheat.mat"); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return ps;
        }
        static void AddLods(LODGroup group, SawChainMotion[] chains)
        {
            var lods = group.GetLODs(); for (int i = 0; i < lods.Length; i++) { var views = lods[i].renderers.ToList(); foreach (var chain in chains) views.Add(chain.Views[i]); lods[i].renderers = views.ToArray(); } group.SetLODs(lods); group.RecalculateBounds();
        }
        static Mesh ModuleMesh(SkinnedMeshRenderer skin, Transform forearm, Quaternion orientation)
        {
            var input = skin.sharedMesh; var vertices = input.vertices; var normals = input.normals; var tangents = input.tangents; var weights = input.boneWeights; var bind = input.bindposes;
            var result = Object.Instantiate(input); result.name = "FixedModule_" + skin.name; result.boneWeights = Array.Empty<BoneWeight>(); result.bindposes = Array.Empty<Matrix4x4>();
            var inverse = Quaternion.Inverse(orientation);
            for (int i = 0; i < vertices.Length; i++) { int index = weights[i].boneIndex0; var matrix = skin.bones[index].localToWorldMatrix * bind[index]; vertices[i] = inverse * (matrix.MultiplyPoint3x4(vertices[i]) - forearm.position); normals[i] = inverse * matrix.MultiplyVector(normals[i]).normalized; var t = inverse * matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized; tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w); }
            result.vertices = vertices; result.normals = normals; result.tangents = tangents; result.RecalculateBounds(); return result;
        }
        static void Record(GameObject root) { foreach (var component in root.GetComponentsInChildren<Component>(true)) if (component && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
        static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Enemy)) throw new Exception("Delivered variant exists; inspect before overwriting it.");
            Directory.CreateDirectory(Art); AssetDatabase.Refresh(); AssetDatabase.ImportAsset(Motor, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(Motor); importer.forceToMono = true; var sample = importer.defaultSampleSettings; sample.preloadAudioData = true; sample.loadType = AudioClipLoadType.DecompressOnLoad; sample.compressionFormat = AudioCompressionFormat.PCM; importer.defaultSampleSettings = sample; importer.SaveAndReimport();
            var preview = EditorSceneManager.NewPreviewScene(); var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), preview);
            try
            {
                root.name = "SawBot_SawPolish_v04"; var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true); var bones = skins[0].bones.ToDictionary(b => b.name); var material = skins[0].sharedMaterial;
                var frames = new Mesh[24]; var input = skins[0].sharedMesh; int chainIndex = Array.FindIndex(skins[0].bones, b => b.name == "Chain_L"); var uv = input.uv; var weights = input.boneWeights; var palette = uv.Where((p, i) => weights[i].boneIndex0 == chainIndex).Aggregate(Vector2.zero, (a, b) => a + b) / weights.Count(w => w.boneIndex0 == chainIndex);
                for (int lod = 0; lod < 3; lod++) for (int phase = 0; phase < 8; phase++) { var mesh = ChainMesh(lod, phase, palette); Save(mesh, mesh.name); frames[lod * 8 + phase] = mesh; }
                var loops = new List<SawChainMotion>(); var exhaust = new List<ParticleSystem>();
                foreach (string side in new[] { "L", "R" })
                {
                    var chain = bones["Chain_" + side]; var guide = bones["GuideBar_" + side]; var axis = new Vector3(side == "L" ? .68f : -.68f, -.733212f, 0).normalized;
                    var correction = Quaternion.FromToRotation(axis, chain.localPosition.normalized); guide.localRotation = correction; chain.localRotation = correction;
                    loops.Add(AddChain(chain, Vector3.zero, Quaternion.LookRotation(axis, Vector3.forward), frames, material, chain.gameObject.layer)); exhaust.Add(Smoke(bones["Forearm_" + side], new Vector3(0, -.02f, .035f), chain.gameObject.layer));
                }
                foreach (var skin in skins) { var mesh = WithoutChain(skin); Save(mesh, mesh.name); skin.sharedMesh = mesh; }
                AddLods(root.GetComponent<LODGroup>(), loops.ToArray());
                var rightViews = skins.Where(s => s.name.StartsWith("RightSaw")).Cast<Renderer>().Concat(loops[1].Views).ToArray(); root.GetComponent<SawBotWeaponSource>().Configure(rightViews, bones["Forearm_R"].GetComponentsInChildren<Collider>(true));
                var feedback = root.GetComponent<RobotHitFeedback>(); var feedbackData = new SerializedObject(feedback); var body = feedbackData.FindProperty("body"); body.arraySize = skins.Length + 6; for (int i = 0; i < skins.Length; i++) body.GetArrayElementAtIndex(i).objectReferenceValue = skins[i]; for (int i = 0; i < 6; i++) body.GetArrayElementAtIndex(skins.Length + i).objectReferenceValue = loops[i / 3].Views[i % 3]; feedbackData.ApplyModifiedPropertiesWithoutUndo();
                var audio = new GameObject("SawAttackMotor").AddComponent<AudioSource>(); audio.transform.SetParent(root.transform, false); audio.transform.localPosition = Vector3.up * .8f; audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Motor); audio.loop = true; audio.playOnAwake = false; audio.volume = .3f; audio.spatialBlend = 1; audio.minDistance = 1.2f; audio.maxDistance = 16; audio.rolloffMode = AudioRolloffMode.Logarithmic; audio.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.EnemyAttack);
                root.AddComponent<SawMotorPresentation>().Configure(root.GetComponent<SawBotCombat>(), root.GetComponent<SawBotWeaponSource>(), null, loops.ToArray(), exhaust.ToArray(), audio);
                var forearm = bones["Forearm_R"]; var rightChain = bones["Chain_R"]; var forward = (rightChain.position - forearm.position).normalized; var orientation = Quaternion.LookRotation(forward, rightChain.TransformDirection(Vector3.forward)); var inverse = Quaternion.Inverse(orientation); var modules = new Mesh[3];
                for (int lod = 0; lod < 3; lod++) { var mesh = ModuleMesh(skins.Single(s => s.name == "RightSaw_LOD" + lod), forearm, orientation); Save(mesh, mesh.name); modules[lod] = mesh; }
                Vector3 origin = inverse * (rightChain.position - forearm.position); Quaternion rotation = inverse * rightChain.rotation * Quaternion.LookRotation(new Vector3(-.68f, -.733212f, 0).normalized, Vector3.forward);
                BuildModule("Assets/IronRequiem/Prefabs/Weapons/Weapon_SawBot_Stolen.prefab", Weapon, true, preview, modules, frames, material, origin, rotation);
                BuildModule("Assets/IronRequiem/Prefabs/Weapons/SawBot_StolenModule.prefab", Pickup, false, preview, modules, frames, material, origin, rotation);
                root.GetComponent<EnemyExecutionSource>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(Pickup).GetComponent<StolenCannon>());
                Record(root); PrefabUtility.SaveAsPrefabAsset(root, Enemy); Capture(root, "corrected-front", new Vector3(1.7f, 1.1f, 2.2f));
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            foreach (string path in new[] { "Assets/IronRequiem/Scenes/SawBotLab.unity", "Assets/IronRequiem/Scenes/SawBotWeaponDeathLab.unity", "Assets/IronRequiem/Scenes/SawBotSustainLab.unity", "Assets/IronRequiem/Scenes/BunkerLab.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var component in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Component>(true)).Where(c => c))
                    { var data = new SerializedObject(component); var property = data.GetIterator(); bool changed = false; while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == AssetDatabase.LoadAssetAtPath<GameObject>(Source)) { property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy); changed = true; } if (changed) data.ApplyModifiedPropertiesWithoutUndo(); }
                    EditorSceneManager.SaveScene(scene); if (path.EndsWith("SawBotWeaponDeathLab.unity")) EditorSceneManager.SaveScene(scene, Lab, true);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            RestoreLabNavigation(); RefitDeathFloor(); PolishSmoke(); AssetDatabase.SaveAssets(); File.WriteAllText(Report + "/build.txt", "Complete. Wrist axis corrected 16.7 degrees. Three LODs share 24 chain phase assets. Attack-only smoke and original synthesized motor loop. Existing health, attacks, extraction and energy unchanged."); IronRequiemSetupTools.WriteAudit();
        }
        static void PolishSmoke()
        {
            foreach (string path in new[] { Enemy, Weapon })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var ps in root.GetComponent<SawMotorPresentation>().Smoke)
                    {
                        if (path == Enemy)
                        {
                            var parent = ps.transform.parent; var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name.EndsWith("LOD0") && s.bones.Contains(parent) && s.sharedMesh.boneWeights.Any(w => s.bones[w.boneIndex0] == parent));
                            var mesh = skin.sharedMesh; int bone = Array.IndexOf(skin.bones, parent); var weights = mesh.boneWeights;
                            var points = mesh.vertices.Where((v, i) => weights[i].boneIndex0 == bone).Select(v => mesh.bindposes[bone].MultiplyPoint3x4(v)).ToArray();
                            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
                            ps.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + .018f);
                        }
                        else
                        {
                            var mesh = ps.transform.parent.parent.GetComponentsInChildren<MeshFilter>().First(f => f.name == "Saw_LOD0").sharedMesh;
                            ps.transform.localPosition = new Vector3(0, mesh.bounds.max.y + .018f, .04f);
                        }
                        var main = ps.main; main.startColor = new Color(.55f, .58f, .6f, .45f); main.startSize = new ParticleSystem.MinMaxCurve(.065f, .11f); main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .7f);
                        var emission = ps.emission; emission.rateOverTime = 10; ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/VFX/CannonExplosion/ResidualSmoke.mat");
                    }
                    Record(root); PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets(); File.WriteAllText(Report + "/smoke.txt", "Exhaust anchored outside measured motor housing; shared URP smoke lobe, max12particles per saw,10/s,0.45-0.7s, no lights. Existing critical smoke unchanged.");
        }
        static void RefitDeathFloor()
        {
            var root = PrefabUtility.LoadPrefabContents(Enemy);
            try
            {
                var animator = root.GetComponentInChildren<Animator>(); var controllerPath = Art + "/SawBot_SawPolish.controller";
                if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), controllerPath);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath); var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true); var chains = root.GetComponentsInChildren<SawChainMotion>(true);
                var transforms = animator.GetComponentsInChildren<Transform>(true); var positions = transforms.Select(t => t.localPosition).ToArray(); var rotations = transforms.Select(t => t.localRotation).ToArray(); var rig = animator.transform.Find("Rig");
                foreach (string name in new[] { "Death", "DeathContact", "DeathDisarmed" })
                {
                    var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == name).state; string path = Art + "/" + name + "_FloorFixed.anim";
                    if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(path)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(state.motion), path);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); var heights = new AnimationCurve();
                    var localPoints = new List<Vector3>(); var targets = new List<Transform>();
                    foreach (var skin in skins)
                    {
                        if (name == "DeathDisarmed" && skin.name.StartsWith("RightSaw")) continue;
                        var mesh = skin.sharedMesh; var weights = mesh.boneWeights; var vertices = mesh.vertices; var binds = mesh.bindposes;
                        for (int i = 0; i < vertices.Length; i++) { int bone = weights[i].boneIndex0; localPoints.Add(binds[bone].MultiplyPoint3x4(vertices[i])); targets.Add(skin.bones[bone]); }
                    }
                    foreach (var chain in chains)
                    {
                        if (name == "DeathDisarmed" && chain.transform.parent.name == "Chain_R") continue;
                        // Every geometry phase fits this union; use corners so no phase escapes the fit.
                        var bounds = new Bounds(); bool first = true;
                        foreach (var view in chain.Views) { var b = view.GetComponent<MeshFilter>().sharedMesh.bounds; if (first) { bounds = b; first = false; } else bounds.Encapsulate(b); }
                        for (int i = 0; i < 8; i++) { localPoints.Add(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))); targets.Add(chain.transform); }
                    }
                    for (int frame = 0; frame <= 126; frame++)
                    {
                        float time = frame / 60f; clip.SampleAnimation(animator.gameObject, time); float minimum = float.PositiveInfinity;
                        var matrices = targets.Distinct().ToDictionary(t => t, t => root.transform.worldToLocalMatrix * t.localToWorldMatrix);
                        for (int i = 0; i < localPoints.Count; i++) minimum = Mathf.Min(minimum, matrices[targets[i]].MultiplyPoint3x4(localPoints[i]).y);
                        heights.AddKey(time, rig.localPosition.y + (.02f - minimum) / animator.transform.lossyScale.y);
                    }
                    for (int i = 0; i < heights.length; i++) { AnimationUtility.SetKeyLeftTangentMode(heights, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(heights, i, AnimationUtility.TangentMode.Linear); }
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(rig, animator.transform), typeof(Transform), "m_LocalPosition.y"), heights); state.motion = clip;
                    for (int i = 0; i < transforms.Length; i++) { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; }
                }
                animator.runtimeAnimatorController = controller; Record(root); PrefabUtility.SaveAsPrefabAsset(root, Enemy); AssetDatabase.SaveAssets();
                File.WriteAllText(Report + "/death-fit.txt", "Three copied death clips refitted for corrected blade orientation, shared chain bounds and all body LODs.127samples,20mm target clearance; gameplay root unchanged.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void RestoreLabNavigation()
        {
            // SaveScene(..., saveAsCopy:true) can clear a surface's data reference in the copy
            // and its source. Restore the existing baked data explicitly; never rebake gameplay.
            var original = AssetDatabase.GUIDToAssetPath("c1206c16a9a9d624d8871040537eb9f7");
            var copy = Art + "/SawPolishNavMesh.asset";
            if (!AssetDatabase.LoadAssetAtPath<NavMeshData>(copy) && !AssetDatabase.CopyAsset(original, copy)) throw new Exception("Cannot copy existing lab NavMeshData");
            foreach (var path in new[] { "Assets/IronRequiem/Scenes/SawBotWeaponDeathLab.unity", Lab })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try { foreach (var surface in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NavMeshSurface>(true))) { surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path == Lab ? copy : original); EditorUtility.SetDirty(surface); } EditorSceneManager.SaveScene(scene); }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Report + "/navigation.txt", "Existing source NavMeshData restored; separate copied NavMeshData assigned to new lab. No bake and no changes to MainScene.");
        }
        static void BuildModule(string source, string path, bool equipped, Scene scene, Mesh[] meshes, Mesh[] frames, Material material, Vector3 origin, Quaternion rotation)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), scene);
            try
            {
                root.name = equipped ? "Weapon_SawBot_Animated_v02" : "SawBot_AnimatedModule_v02"; var visual = root.transform.Find(equipped ? "Content/SawMotorVisual" : "WorldSaw");
                var filters = visual.GetComponentsInChildren<MeshFilter>(true).OrderBy(f => f.name).ToArray(); for (int i = 0; i < 3; i++) filters[i].sharedMesh = meshes[i];
                var mechanics = new GameObject("SawMechanics").transform; mechanics.SetParent(visual, false); mechanics.localScale = filters[0].transform.localScale; var scaleParent = mechanics;
                var loop = AddChain(scaleParent, origin, rotation, frames, material, filters[0].gameObject.layer); AddLods(visual.GetComponent<LODGroup>(), new[] { loop });
                if (equipped)
                {
                    var attack = root.GetComponent<StolenSawAttack>(); var sparks = root.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "SawContactSparks"); attack.Configure(visual, sparks, AssetDatabase.LoadAssetAtPath<AudioClip>(Motor));
                    var smoke = Smoke(scaleParent, origin - Vector3.forward * .06f, filters[0].gameObject.layer); root.AddComponent<SawMotorPresentation>().Configure(null, null, attack, new[] { loop }, new[] { smoke }, null);
                    var weapon = root.GetComponent<WeaponController>(); weapon.WeaponMuzzle.localPosition = new Vector3(0, 0, meshes[0].bounds.max.z * .85f);
                }
                else root.GetComponent<StolenCannon>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>());
                Record(root); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
