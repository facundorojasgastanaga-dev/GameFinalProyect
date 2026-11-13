using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 12/11/2026 — scrap set for Room00_Hangar from the three supplied wreck models (rusted robot wreckage, robotic
    /// machine wreck, military mech). Imports them (no imported materials or clips), gives them IronRequiem/HardSurface
    /// materials with world dirt and edge wear, builds one normalised prefab per model (largest side = 1 m, pivot on the
    /// ground, LODGroup with distance culling, no collider) and assembles varied piles with a fixed seed.
    internal static class HangarScrapAuthoring
    {
        const string ArtDir = "Assets/IronRequiem/Art/Environment/Scrap";
        const string PrefabDir = "Assets/IronRequiem/Prefabs/Scrap";
        static readonly string[] Names = { "Scrap_RustedWreckage", "Scrap_MachineWreck", "Scrap_MilitaryMech" };
        const float CullScreenHeight = .015f;

        /// Imports the models and (re)builds the normalised prefabs. Prefab GUIDs are kept on rebuild.
        internal static GameObject[] Prefabs(StringBuilder log)
        {
            Directory.CreateDirectory(PrefabDir);
            var shader = Shader.Find("IronRequiem/HardSurface") ?? Shader.Find("Universal Render Pipeline/Lit");
            var result = new System.Collections.Generic.List<GameObject>();
            foreach (var name in Names)
            {
                string dir = ArtDir + "/" + name, fbx = dir + "/" + name + ".fbx", tex = dir + "/" + name + "_BaseColor.jpg";
                var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (!mi) { log.AppendLine("scrap model missing " + fbx); continue; }
                if (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation || mi.animationType != ModelImporterAnimationType.None || mi.importCameras || mi.importLights)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
                    mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false; mi.meshCompression = ModelImporterMeshCompression.Low;
                    mi.SaveAndReimport();
                }
                TryMeshLods(mi, log);
                var ti = AssetImporter.GetAtPath(tex) as TextureImporter;
                if (ti && ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; ti.sRGBTexture = true; ti.SaveAndReimport(); }
                var matPath = dir + "/" + name + "_HS.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = shader;
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex)); mat.SetColor("_BaseColor", new Color(.78f, .74f, .7f));
                mat.SetFloat("_Metallic", .45f); mat.SetFloat("_Smoothness", .32f);
                if (mat.HasProperty("_UseDirt")) { mat.SetFloat("_UseDirt", 1); mat.EnableKeyword("_DIRT"); mat.SetFloat("_DirtAmount", .5f); mat.SetFloat("_DirtFloorY", 0); }
                if (mat.HasProperty("_UseEdgeWear")) { mat.SetFloat("_UseEdgeWear", 1); mat.EnableKeyword("_EDGEWEAR"); mat.SetFloat("_WearAmount", .45f); }
                mat.enableInstancing = true; EditorUtility.SetDirty(mat);

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var root = new GameObject(name);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model); inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                var rs = inst.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rs) { r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray(); r.shadowCastingMode = ShadowCastingMode.On; }
                var raw = Bounds(rs);
                // Tripo exports are sometimes Z-up: stand them on their widest base
                var upFix = raw.size.z > raw.size.y * 1.35f && raw.size.z >= raw.size.x ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
                inst.transform.localRotation = upFix;
                var b = Bounds(rs);
                float scale = 1f / Mathf.Max(.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                inst.transform.localScale = Vector3.one * scale;
                b = Bounds(rs);
                inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(CullScreenHeight, rs) }); lod.RecalculateBounds();
                int tris = rs.OfType<MeshRenderer>().Select(r => r.GetComponent<MeshFilter>()).Where(f => f && f.sharedMesh).Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
                var prefabPath = PrefabDir + "/" + name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                log.AppendLine($"scrap {name} raw={raw.size} upFix={(upFix == Quaternion.identity ? "none" : "Z-up")} tris={tris}");
                result.Add(prefab);
            }
            return result.ToArray();
        }

        static Bounds Bounds(Renderer[] rs) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false); go.transform.localPosition = localPos; go.transform.localRotation = localRot; go.transform.localScale = Vector3.one * size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// v3 (12/11/2026): a big, tall, varied pile in three layers (base ring, middle, top) built from the three wreck
        /// models with random rotation/scale, optionally leaning against a wall or column (lean = local direction of the
        /// support, zero for a free-standing heap), plus loose parts around the foot. Colliders are added by the caller.
        internal static int BuildPile(Transform pile, float radius, float height, Vector3 lean, System.Random rnd, GameObject[] prefabs, int index)
        {
            if (prefabs.Length == 0) return 0;
            float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
            GameObject Pick() => prefabs[rnd.Next(prefabs.Length)];
            var leanDir = lean.sqrMagnitude > 1e-4f ? new Vector3(lean.x, 0, lean.z).normalized : Vector3.zero;
            var tiltAxis = leanDir == Vector3.zero ? Vector3.right : Vector3.Cross(Vector3.up, leanDir);
            double upright = leanDir == Vector3.zero ? .35 : .6;   // wall heaps: more slabs stood up against the support
            Quaternion Rot(float tilt) => Quaternion.AngleAxis(tilt, tiltAxis) * Quaternion.Euler(R(-35, 35) + (rnd.NextDouble() < upright ? 90 : 0), R(0, 360), R(-40, 40));
            int count = 0;
            // base: a ring of the largest wrecks, half sunk in the floor
            // v3b: fewer, much bigger pieces (the models are ~0.55 m tall per metre of length: big scale = tall heap)
            int baseCount = 3 + rnd.Next(2);
            for (int k = 0; k < baseCount; k++)
            {
                float size = radius * R(1.8f, 2.4f);
                var dir = Quaternion.Euler(0, k * (360f / baseCount) + R(-30, 30), 0) * Vector3.forward;
                var pos = dir * radius * R(.05f, .3f) + leanDir * radius * .15f + Vector3.down * size * R(.04f, .12f);
                Place(Pick(), pile, pos, Rot(leanDir == Vector3.zero ? 0 : R(8, 22)), size).name = "Base" + k; count++;
            }
            // middle and top: smaller wrecks stacked on the base, pushed towards the support when leaning
            int midCount = 2 + rnd.Next(2);
            for (int k = 0; k < midCount; k++)
            {
                float size = radius * R(1.3f, 1.7f);
                var dir = Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward;
                var pos = dir * radius * R(0, .45f) + leanDir * radius * .35f + Vector3.up * (height * R(.3f, .42f) - size * .12f);
                NoShadow(Place(Pick(), pile, pos, Rot(leanDir == Vector3.zero ? 0 : R(18, 32)), size)).name = "Mid" + k; count++;
            }
            int topCount = 1 + rnd.Next(2);
            for (int k = 0; k < topCount; k++)
            {
                float size = radius * R(.9f, 1.3f);
                var dir = Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward;
                var pos = dir * radius * R(0, .3f) + leanDir * radius * .55f + Vector3.up * (height * R(.66f, .8f) - size * .08f);
                NoShadow(Place(Pick(), pile, pos, Rot(leanDir == Vector3.zero ? 0 : R(25, 40)), size)).name = "Top" + k; count++;
            }
            // loose parts spilled around the foot (no colliders, no shadows: they never block a route)
            int loose = 3 + rnd.Next(3);
            for (int k = 0; k < loose; k++)
            {
                var dir = Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward;
                if (leanDir != Vector3.zero && Vector3.Dot(dir, leanDir) > .3f) dir = -dir;   // not inside the wall
                var pos = dir * radius * R(1.35f, 2.0f) + Vector3.down * R(.02f, .08f);
                var go = Place(Pick(), pile, pos, Quaternion.Euler(R(-70, 70), R(0, 360), R(-70, 70)), R(.35f, .75f)); go.name = "Loose" + k; count++;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return count;
        }

        // only the base of a heap casts shadows (the stacked pieces sit inside its shadow anyway): fewer shadow draws
        static GameObject NoShadow(GameObject go) { foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off; return go; }

        /// Single debris pieces scattered on the floor between the piles (no colliders, no shadows).
        internal static GameObject Debris(Transform parent, GameObject[] prefabs, System.Random rnd, Vector3 localPos, int i)
        {
            float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
            var go = Place(prefabs[rnd.Next(prefabs.Length)], parent, localPos + Vector3.down * R(.02f, .07f), Quaternion.Euler(R(-80, 80), R(0, 360), R(-80, 80)), R(.2f, .5f));
            go.name = "Debris" + i;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// Unity 6 mesh LODs, when the importer exposes them (looked up by name, so older versions just skip it).
        static void TryMeshLods(ModelImporter mi, StringBuilder log)
        {
            var props = typeof(ModelImporter).GetProperties().Where(p => p.Name.IndexOf("MeshLod", System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            var gen = props.FirstOrDefault(p => p.PropertyType == typeof(bool) && p.CanWrite && p.Name.StartsWith("generate", System.StringComparison.OrdinalIgnoreCase));
            if (gen == null) { log.AppendLine("mesh LOD import option not found (" + string.Join(",", props.Select(p => p.Name)) + ")"); return; }
            if (!(bool)gen.GetValue(mi)) { gen.SetValue(mi, true); mi.SaveAndReimport(); }
            log.AppendLine("mesh LODs on for " + System.IO.Path.GetFileName(mi.assetPath) + " (" + gen.Name + ")");
        }

        /// A throwable piece for the scavengers: a small wreck with a box collider and a rigidbody (inactive).
        internal static Rigidbody ThrowPiece(Transform parent, GameObject[] prefabs, int i)
        {
            var root = new GameObject("ScrapPiece" + i); root.transform.SetParent(parent, false);
            if (prefabs.Length > 0)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i % prefabs.Length]);
                go.transform.SetParent(root.transform, false); go.transform.localScale = Vector3.one * (.38f + .05f * (i % 3));
                go.transform.localRotation = Quaternion.Euler(i * 37 % 360, i * 71 % 360, 0);
                var rs = go.GetComponentsInChildren<Renderer>(true);
                var b = Bounds(rs);
                var box = root.AddComponent<BoxCollider>(); box.center = root.transform.InverseTransformPoint(b.center); box.size = b.size;
            }
            else root.AddComponent<BoxCollider>().size = new Vector3(.42f, .07f, .3f);
            var rb = root.AddComponent<Rigidbody>(); rb.mass = 3; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; rb.isKinematic = true;
            root.SetActive(false);
            return rb;
        }
    }
}
