using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class ModularArmAuthoring
    {
        public const string Weapon = "Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_ArmModule_v07.prefab";
        public const string Player = "Assets/IronRequiem/Prefabs/Player/Player_ModularArm_v07.prefab";
        public const string BigWeapon = "Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_Temporary.prefab";
        public const string BigPickup = "Assets/IronRequiem/Prefabs/Weapons/BigBot_CollectibleCannon.prefab";
        public const string Lab = "Assets/IronRequiem/Scenes/ModularArmLab.unity";
        public const string Reports = "Tools/IronRequiem/Reports/ModularArm";
        const string Art = "Assets/IronRequiem/Art/Weapons/ArmModuleV07";
        const string Queue = "Tools/IronRequiem/Requests/modular-arm.txt", Key = "IRON.ModularArm";
        static readonly DateTime LoadedAt = DateTime.UtcNow;
        static TestRunnerApi api;
        static bool Dirty() => Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
        static ModularArmAuthoring() { EditorApplication.update += Tick; if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register; }
        static void Register() { if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); } }
        static void Tick()
        {
            // This fixed request changes only the Editor's Play state. It never activates a desktop window.
            if (File.Exists(Queue) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.ReadAllText(Queue).Trim() == "stop-and-build")
            {
                File.WriteAllText(Queue, "build");
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Key + ".restore", false))
            {
                SessionState.EraseBool(Key + ".restore"); if (!Dirty()) EditorSceneManager.OpenScene(Lab); IronRequiemSetupTools.WriteAudit();
            }
            if (!File.Exists(Queue) || SessionState.GetBool(Key, false)) return;
            foreach (string path in new[] {"Assets/IronRequiem/Editor/ModularArmAuthoring.cs", "Assets/IronRequiem/Tests/PlayMode/ModularArmTests.cs"})
                if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > LoadedAt) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Reports);
            try
            {
                if (Dirty()) throw new InvalidOperationException("Preserving dirty user scene");
                if (op == "build") Build();
                else if (op == "test" || op == "test-t07-view")
                {
                    Register(); SessionState.SetBool(Key, true); SessionState.SetString(Key + ".label", op == "test" ? "" : "-t07-view");
                    api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = op == "test" ? new[] {"IronRequiem.Tests.ModularArmTests"} : new[] {"IronRequiem.Tests.ModularArmTests.T07PickupRespectsWallsRangeAndSeparateMeleeKey"} }));
                }
                else throw new InvalidOperationException("Unknown fixed modular-arm action");
            }
            catch (Exception e) { File.WriteAllText(Reports + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static Transform Named(GameObject go, string name) => go.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        static void Variant(string source, string dest)
        {
            if (File.Exists(dest)) return;
            var preview = EditorSceneManager.NewPreviewScene();
            try { var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), preview); go.name = Path.GetFileNameWithoutExtension(dest); PrefabUtility.SaveAsPrefabAsset(go, dest); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name; string path = Art + "/" + name + ".asset";
            if (File.Exists(path)) { var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path); EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); AssetDatabase.SaveAssetIfDirty(saved); return saved; }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        struct Vertex
        {
            public Vector3 p, n; public Vector2 uv;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t)};
        }
        // Clip only the pistol grip below the native housing. Keep the housing's native UVs and split normals.
        static Mesh ForearmHousing(MeshFilter filter)
        {
            var mesh = filter.sharedMesh; var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            var output = new List<Vertex>(); var triangles = new List<int>(); var rim = new List<Vector3>();
            var indices = mesh.triangles;
            float Height(Vertex v) => filter.transform.TransformPoint(v.p).y - .07f;
            void Emit(Vertex a, Vertex b, Vertex c) { int n = output.Count; output.Add(a); output.Add(b); output.Add(c); triangles.AddRange(new[] {n, n + 1, n + 2}); }
            for (int i = 0; i < indices.Length; i += 3)
            {
                var polygon = new List<Vertex>();
                for (int j = 0; j < 3; j++) { int k = indices[i + j]; polygon.Add(new Vertex {p = vertices[k], n = normals[k], uv = uv[k]}); }
                var clipped = new List<Vertex>();
                for (int j = 0; j < polygon.Count; j++)
                {
                    var a = polygon[j]; var b = polygon[(j + 1) % polygon.Count]; float da = Height(a), db = Height(b);
                    if (da >= 0) clipped.Add(a);
                    if ((da >= 0) != (db >= 0)) { var cut = Vertex.Lerp(a, b, da / (da - db)); clipped.Add(cut); rim.Add(filter.transform.TransformPoint(cut.p)); }
                }
                for (int j = 1; j + 1 < clipped.Count; j++) Emit(clipped[0], clipped[j], clipped[j + 1]);
            }
            // The grip has a convex mounting section. Cap it with graphite from the native palette atlas.
            var points = rim.Select(p => new Vector2(p.x, p.z)).Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            var hull = new List<Vector2>();
            foreach (var p in points) { while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= .00000001f) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--) { var p = points[i]; while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= .00000001f) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            if (hull.Count > 2)
            {
                hull.RemoveAt(hull.Count - 1);
                Vertex Cap(Vector2 p) => new Vertex {p = filter.transform.InverseTransformPoint(new Vector3(p.x, .07f, p.y)), n = filter.transform.InverseTransformDirection(Vector3.down), uv = new Vector2(.73f, .3f)};
                for (int i = 1; i + 1 < hull.Count; i++) Emit(Cap(hull[0]), Cap(hull[i]), Cap(hull[i + 1]));
            }
            var result = new Mesh {indexFormat = output.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16};
            result.vertices = output.Select(v => v.p).ToArray(); result.normals = output.Select(v => v.n).ToArray(); result.uv = output.Select(v => v.uv).ToArray(); result.triangles = triangles.ToArray(); result.RecalculateTangents(); result.RecalculateBounds(); return result;
        }
        static Material CopyMaterial(string source, string name)
        {
            string path = Art + "/" + name + ".mat"; if (!File.Exists(path)) AssetDatabase.CopyAsset(source, path); return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        static void BuildPrimary()
        {
            Variant(NativeWeaponVisualAuthoring.Weapon, Weapon);
            var go = PrefabUtility.LoadPrefabContents(Weapon); var original = PrefabUtility.LoadPrefabContents(NativeWeaponRepairTools.Source);
            try
            {
                var weapon = go.GetComponent<WeaponController>(); var source = original.GetComponent<WeaponController>();
                var armor = CopyMaterial(NativeWeaponRepairTools.Art + "/Blaster_PlayerArmor.mat", "Module_PlayerArmor");
                var front = CopyMaterial(NativeWeaponRepairTools.Art + "/Blaster_PlayerFront.mat", "Module_PlayerFront");
                var energy = CopyMaterial(NativeWeaponRepairTools.Art + "/Blaster_PlayerEnergy.mat", "Module_PlayerEnergy");
                energy.SetColor("_BaseColor", new Color(.06f, .09f, .10f, .55f)); energy.SetColor("_EmissionColor", new Color(.07f, .65f, .85f)); EditorUtility.SetDirty(energy); AssetDatabase.SaveAssetIfDirty(energy);
                foreach (var filter in original.GetComponentsInChildren<MeshFilter>(true))
                {
                    var target = Named(go, filter.name); target.GetComponent<MeshFilter>().sharedMesh = filter.name == "Primary_Weapon" ? SaveMesh(ForearmHousing(filter), "Module_Housing_NoGrip") : filter.sharedMesh;
                    target.localPosition = filter.transform.localPosition; target.localRotation = filter.transform.localRotation; target.localScale = filter.transform.localScale;
                    target.GetComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials.Select((m, i) => i == 1 ? energy : filter.name.StartsWith("Primary_Weapon_Front") ? front : armor).ToArray();
                }
                var model = Named(go, "WeaponMesh_Pistol"); const float scale = .62f;
                model.localScale = Named(original, "WeaponMesh_Pistol").localScale * scale;
                model.localRotation = Named(original, "WeaponMesh_Pistol").localRotation;
                model.localPosition = weapon.WeaponMuzzle.position - source.WeaponMuzzle.position * scale;
                var body = Named(go, "Primary_Weapon").GetComponent<Renderer>();
                var attachment = Named(go, "Attachment"); attachment.SetPositionAndRotation(new Vector3(0, weapon.WeaponMuzzle.position.y, body.bounds.min.z + .002f), weapon.WeaponMuzzle.rotation);
                Named(go, "Muzzle").SetPositionAndRotation(weapon.WeaponMuzzle.position, weapon.WeaponMuzzle.rotation);
                var heat = go.GetComponent<OverheatBehavior>(); heat.OverheatingMaterial = energy;
                if (!go.GetComponent<PrimaryModuleHeatPresentation>()) go.AddComponent<PrimaryModuleHeatPresentation>();
                heat.OverheatGradient = new Gradient {colorKeys = new[] {
                    new GradientColorKey(new Color(.07f, .65f, .85f), 0), new GradientColorKey(new Color(.15f, .7f, .8f), .2f),
                    new GradientColorKey(new Color(1.8f, .35f, .015f), .5f), new GradientColorKey(new Color(3f, .035f, .004f), .8f),
                    new GradientColorKey(new Color(4f, .015f, .002f), 1)}, alphaKeys = new[] {new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1)}};
                heat.SteamVfxEmissionRateMax = 5;
                var smoke = heat.SteamVfx; smoke.gameObject.SetActive(true); smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                smoke.transform.SetPositionAndRotation(weapon.WeaponMuzzle.position - weapon.WeaponMuzzle.forward * .13f + Vector3.up * .055f, Quaternion.Euler(-90, 0, 0)); smoke.transform.localScale = Vector3.one;
                var main = smoke.main; main.playOnAwake = true; main.loop = true; main.maxParticles = 20; main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .5f); main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .16f); main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f); main.startColor = new Color(.35f, .38f, .4f, .25f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Shape;
                var emission = smoke.emission; emission.enabled = true; emission.rateOverTime = 0;
                PrefabUtility.SaveAsPrefabAsset(go, Weapon);
                File.WriteAllText(Reports + "/primary.txt", "Native housing clipped below y=.07 m to remove grip; native UVs/normals retained. Whole model 62% of original dimensions. Native capsule motion code retained; heat cyan/orange/red with 20-particle steam budget. Same weapon gameplay and projectile. Rear port=" + attachment.position.ToString("F5") + "; body size=" + body.bounds.size.ToString("F5"));
            }
            finally { PrefabUtility.UnloadPrefabContents(go); PrefabUtility.UnloadPrefabContents(original); }
        }
        static Mesh PortRing()
        {
            const int sides = 24; var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (int ring = 0; ring < 4; ring++) for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides, radius = ring % 2 == 0 ? .052f : .034f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, ring < 2 ? -.075f : .014f)); uv.Add(new Vector2((float)i / sides, ring / 3f));
            }
            void Quad(int a, int b, int c, int d) { triangles.AddRange(new[] {a, b, c, a, c, d}); }
            for (int i = 0; i < sides; i++) { int j = (i + 1) % sides; Quad(i, j, j + 48, i + 48); Quad(i + 24, i + 72, j + 72, j + 24); Quad(i, i + 24, j + 24, j); Quad(i + 48, j + 48, j + 72, i + 72); }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static void BuildPlayer()
        {
            Variant(NativeWeaponVisualAuthoring.Player, Player); var go = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                var manager = go.GetComponent<PlayerWeaponsManager>(); var weapon = AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>();
                for (int i = 0; i < manager.StartingWeapons.Count; i++) if (manager.StartingWeapons[i].name == Path.GetFileNameWithoutExtension(NativeWeaponVisualAuthoring.Weapon)) manager.StartingWeapons[i] = weapon;
                var hands = go.GetComponent<ProtagonistHandsPresentation>(); var data = new SerializedObject(hands); data.FindProperty("pistolPrefab").objectReferenceValue = weapon; data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var previous in hands.FpsRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Forearm_R_Receiver" || t.name == "RightArm_UniversalCoupling").ToArray()) Object.DestroyImmediate(previous.gameObject);
                var full = hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Forearm_R_FPS");
                var bone = Named(hands.FpsRoot.gameObject, "Forearm_R"); var hand = Named(hands.FpsRoot.gameObject, "Hand_R");
                int boneIndex = Array.IndexOf(full.bones, bone); if (boneIndex < 0) throw new InvalidOperationException("Forearm binding missing");
                var axis = bone.InverseTransformPoint(hand.position).normalized; var mesh = Object.Instantiate(full.sharedMesh);
                var positions = mesh.vertices.Select(v => Vector3.Dot(mesh.bindposes[boneIndex].MultiplyPoint3x4(v), axis) * bone.TransformVector(axis).magnitude).ToArray();
                const float retained = .14f; int[] sourceIndices = mesh.triangles;
                mesh.triangles = Enumerable.Range(0, sourceIndices.Length / 3).Where(i => Enumerable.Range(0, 3).All(j => positions[sourceIndices[i * 3 + j]] <= retained + .004f)).SelectMany(i => new[] {sourceIndices[i * 3], sourceIndices[i * 3 + 1], sourceIndices[i * 3 + 2]}).ToArray();
                if (mesh.triangles.Length < 12 || mesh.triangles.Length == sourceIndices.Length) throw new InvalidOperationException("Invalid forearm partition: " + mesh.triangles.Length + "/" + sourceIndices.Length + "; range=" + positions.Min() + ":" + positions.Max());
                // Extend the hidden proximal end behind the FPS camera; preserve the joint, weights and normal-arm source.
                var receiverVertices = mesh.vertices; var bind = mesh.bindposes[boneIndex]; var inverseBind = bind.inverse; float worldScale = bone.TransformVector(axis).magnitude;
                for (int i = 0; i < receiverVertices.Length; i++)
                {
                    float extension = .18f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .075f, positions[i])));
                    receiverVertices[i] = inverseBind.MultiplyPoint3x4(bind.MultiplyPoint3x4(receiverVertices[i]) - axis * (extension / worldScale));
                }
                mesh.vertices = receiverVertices; mesh.RecalculateBounds();
                var receiver = new GameObject("Forearm_R_Receiver"); receiver.layer = full.gameObject.layer; receiver.transform.SetParent(full.transform.parent, false);
                receiver.transform.localPosition = full.transform.localPosition; receiver.transform.localRotation = full.transform.localRotation; receiver.transform.localScale = full.transform.localScale;
                int retainedTriangles = mesh.triangles.Length / 3;
                var skin = receiver.AddComponent<SkinnedMeshRenderer>(); EditorUtility.CopySerialized(full, skin); skin.sharedMesh = SaveMesh(mesh, "Protagonist_RightForearm_Receiver"); skin.enabled = false;
                skin.updateWhenOffscreen = true;
                var metal = CopyMaterial(Art + "/Module_PlayerFront.mat", "Receiver_Graphite");
                foreach (string property in new[] {"_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap"}) metal.SetTexture(property, null);
                foreach (string keyword in new[] {"_EMISSION", "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP"}) metal.DisableKeyword(keyword);
                metal.SetColor("_BaseColor", new Color(.07f, .075f, .08f)); metal.SetColor("_EmissionColor", Color.black); metal.SetFloat("_Metallic", .7f); metal.SetFloat("_Smoothness", .35f);
                EditorUtility.SetDirty(metal); AssetDatabase.SaveAssetIfDirty(metal);
                var ring = new GameObject("RightArm_UniversalCoupling"); ring.layer = full.gameObject.layer; ring.transform.SetParent(hands.RightModuleSocket, false);
                ring.AddComponent<MeshFilter>().sharedMesh = SaveMesh(PortRing(), "UniversalForearm_Coupling"); var view = ring.AddComponent<MeshRenderer>(); view.sharedMaterial = metal; view.shadowCastingMode = ShadowCastingMode.Off; view.enabled = false;
                hands.ConfigureModularArm(new Renderer[] {skin, view}, retained);
                // The full-body model already has separate forearm/hand meshes. Preserve them and expose the matching animation socket.
                var bodyRoot = (Transform)new SerializedObject(go.GetComponent<PlayerBodyPresentation>()).FindProperty("bodyRoot").objectReferenceValue;
                var bodyForearm = Named(bodyRoot.gameObject, "Forearm_R"); var bodyHand = Named(bodyRoot.gameObject, "Hand_R");
                var bodyPort = bodyForearm.Find("RightArm_ModuleSocket"); if (!bodyPort) { bodyPort = new GameObject("RightArm_ModuleSocket").transform; bodyPort.SetParent(bodyForearm, false); }
                var bodyAxis = (bodyHand.position - bodyForearm.position).normalized;
                bodyPort.SetPositionAndRotation(bodyForearm.position + bodyAxis * retained, Quaternion.LookRotation(bodyAxis, bodyRoot.up));
                PrefabUtility.SaveAsPrefabAsset(go, Player);
                File.WriteAllText(Reports + "/arm.txt", "Right forearm: original full forearm/hand retained for normal mode. Proximal receiver keeps " + retainedTriangles + "/" + sourceIndices.Length / 3 + " triangles with original rig, weights and materials. Retained length .14 m. Shared universal socket follows module rear, used for primary and stolen weapons. Left fist unchanged.");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        static void BuildBigBotPickup()
        {
            Variant(T07OptimizationBuilder.Weapon, BigWeapon); Variant("Assets/IronRequiem/Prefabs/Enemies/BigBot_FallenCannon.prefab", BigPickup);
            var bot = PrefabUtility.LoadPrefabContents(BigBotDestructionAuthoring.Prefab);
            var source = PrefabUtility.LoadPrefabContents("Assets/IronRequiem/Prefabs/Enemies/BigBot_FallenCannon.prefab");
            var held = PrefabUtility.LoadPrefabContents(BigWeapon);
            try
            {
                var pivot = Named(bot, "Forearm_R");
                var cannons = new SerializedObject(bot.GetComponent<BigBotCombat>()).FindProperty("cannons");
                var enemyWeapon = (WeaponController)cannons.GetArrayElementAtIndex(0).objectReferenceValue;
                Vector3 mouth = pivot.InverseTransformPoint(enemyWeapon.WeaponMuzzle.position);
                var rotation = Quaternion.Inverse(Quaternion.LookRotation(pivot.InverseTransformDirection(enemyWeapon.WeaponMuzzle.forward), pivot.InverseTransformDirection(Vector3.up)));
                var filters = source.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.EndsWith("LOD0")).ToArray();
                var all = filters.SelectMany(f => f.sharedMesh.vertices.Select(v => rotation * (source.transform.InverseTransformPoint(f.transform.TransformPoint(v)) - mouth))).ToArray();
                var bounds = new Bounds(all[0], Vector3.zero); foreach (var p in all) bounds.Encapsulate(p);
                float scale = Mathf.Min(.44f / bounds.size.z, .25f / bounds.size.x, .25f / bounds.size.y);
                var weapon = held.GetComponent<WeaponController>(); Named(held, "StolenCannonVisual").GetComponent<Renderer>().enabled = false;
                var previous = held.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BigBot_EquippedCannon"); if (previous) Object.DestroyImmediate(previous.gameObject);
                var visual = new GameObject("BigBot_EquippedCannon"); visual.transform.SetParent(weapon.WeaponRoot.transform, false); visual.transform.SetPositionAndRotation(weapon.WeaponMuzzle.position, weapon.WeaponMuzzle.rotation);
                foreach (var filter in filters)
                {
                    var mesh = Object.Instantiate(filter.sharedMesh); mesh.vertices = mesh.vertices.Select(v => rotation * (source.transform.InverseTransformPoint(filter.transform.TransformPoint(v)) - mouth) * scale).ToArray();
                    mesh.normals = mesh.normals.Select(v => (rotation * source.transform.InverseTransformDirection(filter.transform.TransformDirection(v))).normalized).ToArray(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                    var piece = new GameObject(filter.name); piece.transform.SetParent(visual.transform, false); piece.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "HeldBigBot_" + filter.name); piece.AddComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<Renderer>().sharedMaterials;
                }
                var attach = weapon.WeaponRoot.transform.Find("Attachment");
                if (!attach) { attach = new GameObject("Attachment").transform; attach.SetParent(weapon.WeaponRoot.transform, false); }
                attach.SetPositionAndRotation(visual.transform.TransformPoint(Vector3.forward * (bounds.min.z * scale + .005f)), weapon.WeaponMuzzle.rotation);
                foreach (var t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = Named(held, "StolenCannonVisual").gameObject.layer;
                PrefabUtility.SaveAsPrefabAsset(held, BigWeapon);
            }
            finally { PrefabUtility.UnloadPrefabContents(held); PrefabUtility.UnloadPrefabContents(source); PrefabUtility.UnloadPrefabContents(bot); }

            var pickup = PrefabUtility.LoadPrefabContents(BigPickup); var t07 = PrefabUtility.LoadPrefabContents(HitWeaponAuthoring.T07);
            try
            {
                var reference = t07.GetComponentInChildren<StolenCannon>(true);
                var module = pickup.GetComponent<StolenCannon>(); if (!module) module = pickup.AddComponent<StolenCannon>();
                module.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(BigWeapon).GetComponent<WeaponController>());
                var area = pickup.GetComponent<DamageArea>(); area.AreaOfEffectDistance = reference.GetComponent<DamageArea>().AreaOfEffectDistance; area.DamageRatioOverDistance = reference.GetComponent<DamageArea>().DamageRatioOverDistance;
                pickup.GetComponent<CannonExplosion>().Configure(reference.GetComponent<CannonExplosion>().PresentationPrefab);
                var overdrive = pickup.GetComponent<CannonOverdrive>();
                var previousSmoke = pickup.transform.Find("CriticalSmoke"); if (previousSmoke) Object.DestroyImmediate(previousSmoke.gameObject);
                var smoke = Object.Instantiate(reference.GetComponent<CannonOverdrive>().Smoke.gameObject, pickup.transform).GetComponent<ParticleSystem>(); smoke.name = "CriticalSmoke"; smoke.transform.localPosition = Vector3.zero; smoke.transform.localScale = Vector3.one / Mathf.Abs(pickup.transform.lossyScale.x); overdrive.ConfigureSmoke(smoke);
                var audio = pickup.GetComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                PrefabUtility.SaveAsPrefabAsset(pickup, BigPickup);
            }
            finally { PrefabUtility.UnloadPrefabContents(pickup); PrefabUtility.UnloadPrefabContents(t07); }
            bot = PrefabUtility.LoadPrefabContents(BigBotDestructionAuthoring.Prefab);
            try { bot.GetComponent<BigBotDestruction>().ConfigureCollectibleCannon(AssetDatabase.LoadAssetAtPath<GameObject>(BigPickup).GetComponent<StolenCannon>()); PrefabUtility.SaveAsPrefabAsset(bot, BigBotDestructionAuthoring.Prefab); }
            finally { PrefabUtility.UnloadPrefabContents(bot); }
            File.WriteAllText(Reports + "/pickup.txt", "Root cause: BigBot_FallenCannon was only decorative debris, with no StolenCannon/energy/equipped weapon. New collectible variant reuses the same real world meshes and three LODs, CannonAmmo, CannonChargeShot, Overdrive, explosion and PlayerWeaponsManager. Actual Big Bot cannon mesh used while equipped. Unheld drop lifetime 120 s; held modules never expire/recycle. At most eight independent world/held drops; oldest unheld removed first. E collect, F melee, G throw unchanged.");
        }
        static void Build()
        {
            Directory.CreateDirectory(Art); AssetDatabase.Refresh(); BuildPrimary(); BuildPlayer(); AddT07Socket(); BuildBigBotPickup();
            if (!File.Exists(Lab))
            {
                AssetDatabase.CopyAsset(NativeWeaponVisualAuthoring.Lab, Lab); var scene = EditorSceneManager.OpenScene(Lab);
                var old = Object.FindAnyObjectByType<PlayerCharacterController>().gameObject;
                var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player), scene); player.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation); player.transform.localScale = old.transform.localScale; Object.DestroyImmediate(old);
                EditorSceneManager.SaveScene(scene);
            }
            IronRequiemSetupTools.WriteAudit(); File.WriteAllText(Reports + "/build.txt", "Primary module, removable forearm receiver, collectible Big Bot cannon and separate ModularArmLab built. Original scenes and v06 prefabs preserved.");
        }
        static void AddT07Socket()
        {
            var go = PrefabUtility.LoadPrefabContents(T07OptimizationBuilder.Weapon);
            try
            {
                var weapon = go.GetComponent<WeaponController>(); var visual = Named(go, "StolenCannonVisual"); var mesh = visual.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices.Select(visual.TransformPoint).ToArray();
                float rear = vertices.Min(p => Vector3.Dot(p - weapon.WeaponMuzzle.position, weapon.WeaponMuzzle.forward));
                var rim = vertices.Where(p => Vector3.Dot(p - weapon.WeaponMuzzle.position, weapon.WeaponMuzzle.forward) < rear + .025f).ToArray();
                Vector3 center = rim.Aggregate(Vector3.zero, (sum, p) => sum + p) / rim.Length;
                var socket = weapon.WeaponRoot.transform.Find("Attachment"); if (!socket) { socket = new GameObject("Attachment").transform; socket.SetParent(weapon.WeaponRoot.transform, false); }
                socket.SetPositionAndRotation(center, weapon.WeaponMuzzle.rotation);
                PrefabUtility.SaveAsPrefabAsset(go, T07OptimizationBuilder.Weapon);
                File.WriteAllText(Reports + "/t07-socket.txt", "Shared right-arm interface added at actual rear mesh section=" + center.ToString("F5") + "; model, muzzle, charge effect, stats and animations unchanged.");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t) { } public void TestStarted(ITestAdaptor t) { } public void TestFinished(ITestResultAdaptor t) { }
            public void OnError(string message) { SessionState.EraseBool(Key); File.WriteAllText(Reports + "/runner-error.txt", message); }
            public void RunFinished(ITestResultAdaptor result) { if (!SessionState.GetBool(Key, false)) return; SessionState.EraseBool(Key); string label = SessionState.GetString(Key + ".label", ""); TestRunnerApi.SaveResultToFile(result, Reports + "/play" + label + ".xml"); File.WriteAllText(Reports + "/status" + label + ".txt", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount); SessionState.SetBool(Key + ".restore", true); }
        }
    }
}
