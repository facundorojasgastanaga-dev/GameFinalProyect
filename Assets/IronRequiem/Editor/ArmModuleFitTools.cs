using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    // Fixed, local operations for fitting Weapon_IR_ArmModule_v07 to the protagonist's forearm.
    // Inspect is read-only. Apply edits only the v07 module prefab, its coupling mesh and its two
    // module/receiver materials through Editor APIs. No scenes are opened or saved.
    public static class ArmModuleFitTools
    {
        const string Weapon = "Assets/IronRequiem/Prefabs/Weapons/Visuals/Weapon_IR_ArmModule_v07.prefab", Player = ModularArmAuthoring.Player;
        const string Art = "Assets/IronRequiem/Art/Weapons/ArmModuleV07";
        const string Report = "Tools/IronRequiem/Reports/ModularArm/fit-inspect.txt";

        [MenuItem("Tools/Iron Requiem/Weapons/Arm Module v07/Inspect Fit")]
        public static void Inspect()
        {
            var log = new StringBuilder();
            var weapon = PrefabUtility.LoadPrefabContents(Weapon);
            try
            {
                var controller = weapon.GetComponent<WeaponController>();
                var muzzle = controller.WeaponMuzzle;
                var attachment = weapon.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Attachment");
                log.AppendLine("muzzle local(root)=" + weapon.transform.InverseTransformPoint(muzzle.position).ToString("F4") + " rot=" + (Quaternion.Inverse(weapon.transform.rotation) * muzzle.rotation).eulerAngles.ToString("F2"));
                if (attachment) log.AppendLine("attachment in muzzle space=" + muzzle.InverseTransformPoint(attachment.position).ToString("F4") + " parent=" + attachment.parent.name + " fwd·muzzle=" + Vector3.Dot(attachment.forward, muzzle.forward).ToString("F4"));
                var renderers = controller.WeaponRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                Bounds? all = null;
                foreach (var r in renderers)
                {
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    var b = new Bounds();bool first = true;
                    foreach (var v in mesh.vertices)
                    {
                        var p = muzzle.InverseTransformPoint(r.transform.TransformPoint(v));
                        if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                    }
                    log.AppendLine("  renderer " + r.name + " active=" + r.gameObject.activeInHierarchy + " mats=" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")) + " boundsMuzzle c=" + b.center.ToString("F4") + " s=" + b.size.ToString("F4"));
                    if (!r.gameObject.activeInHierarchy) continue;
                    if (all == null) all = b; else { var a = all.Value; a.Encapsulate(b); all = a; }
                }
                if (all != null) log.AppendLine("module bounds (muzzle space) c=" + all.Value.center.ToString("F4") + " s=" + all.Value.size.ToString("F4") + " min=" + all.Value.min.ToString("F4"));
            }
            finally { PrefabUtility.UnloadPrefabContents(weapon); }

            var player = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                foreach (var t in player.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Forearm_R") || t.name.StartsWith("RightArm_") || t.name.StartsWith("Hand_R")))
                {
                    var r = t.GetComponent<Renderer>();
                    log.AppendLine("player " + t.name + " parent=" + (t.parent ? t.parent.name : "-") + (r ? " mats=" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "(" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F3") : "") + " tex=" + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none") + ")" : "null")) + " bounds=" + r.bounds.size.ToString("F4") : ""));
                    var filter = t.GetComponent<MeshFilter>(); if (filter && filter.sharedMesh) log.AppendLine("   mesh " + filter.sharedMesh.name + " bounds=" + filter.sharedMesh.bounds.ToString("F4") + " localScale=" + t.localScale.ToString("F3"));
                    var skin = r as SkinnedMeshRenderer;
                    if (skin && skin.sharedMesh && skin.rootBone)
                    {
                        // Radius of the forearm skin around its bone axis near the elbow cut and near the wrist.
                        var mesh = skin.sharedMesh; var verts = mesh.vertices; var bw = mesh.boneWeights; var bind = mesh.bindposes;
                        log.AppendLine("   skin mesh=" + mesh.name + " verts=" + verts.Length + " bones=" + skin.bones.Length + " root=" + skin.rootBone.name);
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            foreach (var path in Directory.GetFiles(Art, "*.mat"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\', '/'));
                log.AppendLine("material " + m.name + " shader=" + m.shader.name + " base=" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F3") : "-") + " tex=" + (m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none") + " metal=" + m.GetFloat("_Metallic").ToString("F2") + " smooth=" + m.GetFloat("_Smoothness").ToString("F2") + " users-guid=" + AssetDatabase.AssetPathToGUID(path));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, log.ToString());
            Debug.Log("[ArmModuleFit] " + Report);
        }

        const string Protagonist = "Assets/IronRequiem/Art/Player/ProtagonistV01";
        const string ApplyReport = "Tools/IronRequiem/Reports/ModularArm/fit-apply.txt";

        // Mean gamma colour of the texture's bright (ivory) and dark (graphite) texels.
        static void Palette(Texture texture, out Color light, out Color dark)
        {
            var rt = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active; Graphics.Blit(texture, rt); RenderTexture.active = rt;
            var copy = new Texture2D(256, 256, TextureFormat.RGBA32, false, false); copy.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); copy.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            Vector3 l = Vector3.zero, d = Vector3.zero; int ln = 0, dn = 0;
            foreach (var p in copy.GetPixels())
            {
                float luma = p.r * .299f + p.g * .587f + p.b * .114f;
                if (luma > .62f) { l += new Vector3(p.r, p.g, p.b); ln++; }
                else if (luma < .16f && luma > .015f) { d += new Vector3(p.r, p.g, p.b); dn++; }
            }
            Object.DestroyImmediate(copy);
            l = ln > 0 ? l / ln : new Vector3(.86f, .83f, .78f); d = dn > 0 ? d / dn : new Vector3(.07f, .07f, .075f);
            light = new Color(l.x, l.y, l.z, 1); dark = new Color(d.x, d.y, d.z, 1);
        }

        static Mesh Coupling(Mesh target)
        {
            // Tapered sleeve: covers the forearm cut at the rear (r .050), flares to the module's rear face
            // (r .059), then a short graphite band (r .0615). Inner radius .034 hides the cut. Submesh 0 ivory, 1 graphite.
            const int sides = 32; const float inner = .034f;
            var vertices = new System.Collections.Generic.List<Vector3>(); var normals = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var shell = new System.Collections.Generic.List<int>(); var band = new System.Collections.Generic.List<int>();
            int Ring(float radius, float z, bool radial, float capSign, float v)
            {
                int start = vertices.Count;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides; var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    vertices.Add(dir * radius + Vector3.forward * z); normals.Add(radial ? dir : Vector3.forward * capSign); uv.Add(new Vector2((float)i / sides, v));
                }
                return start;
            }
            void Strip(System.Collections.Generic.List<int> list, int a, int b)
            {
                for (int i = 0; i < sides; i++) { int j = (i + 1) % sides; list.AddRange(new[] { a + i, a + j, b + j, a + i, b + j, b + i }); }
            }
            // Outer shell, rear to front.
            Strip(shell, Ring(.050f, -.075f, true, 0, 0), Ring(.059f, -.012f, true, 0, .6f));
            // Rear cap (faces -Z): outer then inner, same order as the original port ring.
            { int o = Ring(.050f, -.075f, false, -1, 0), n = Ring(inner, -.075f, false, -1, .1f); for (int i = 0; i < sides; i++) { int j = (i + 1) % sides; shell.AddRange(new[] { o + i, n + i, n + j, o + i, n + j, o + j }); } }
            // Step up to the band (faces -Z).
            { int o = Ring(.059f, -.012f, false, -1, .6f), n = Ring(.0615f, -.012f, false, -1, .62f); for (int i = 0; i < sides; i++) { int j = (i + 1) % sides; band.AddRange(new[] { o + i, j + o, n + j, o + i, n + j, n + i }); } }
            // Graphite band and its front face.
            Strip(band, Ring(.0615f, -.012f, true, 0, .62f), Ring(.0615f, .014f, true, 0, 1));
            { int o = Ring(.0615f, .014f, false, 1, 1), n = Ring(inner, .014f, false, 1, .9f); for (int i = 0; i < sides; i++) { int j = (i + 1) % sides; band.AddRange(new[] { o + i, o + j, n + j, o + i, n + j, n + i }); } }
            target.Clear(); target.subMeshCount = 2; target.SetVertices(vertices); target.SetNormals(normals); target.SetUVs(0, uv);
            target.SetTriangles(shell, 0); target.SetTriangles(band, 1); target.RecalculateBounds(); target.RecalculateTangents();
            return target;
        }

        [MenuItem("Tools/Iron Requiem/Weapons/Arm Module v07/Apply Fit")]
        public static void Apply()
        {
            var log = new StringBuilder();
            var skinMaterial = AssetDatabase.LoadAssetAtPath<Material>(Directory.GetFiles(Protagonist, "IR_Protagonist_URP.mat", SearchOption.AllDirectories).First().Replace('\\', '/'));
            Palette(skinMaterial.GetTexture("_BaseMap"), out var ivory, out var graphite);
            float skinSmooth = skinMaterial.GetFloat("_Smoothness"), skinMetal = skinMaterial.GetFloat("_Metallic");
            log.AppendLine("protagonist ivory=" + ivory.ToString("F3") + " graphite=" + graphite.ToString("F3") + " smooth=" + skinSmooth.ToString("F2") + " metal=" + skinMetal.ToString("F2"));

            // 1. Materials: same palette and a matte finish close to the skin.
            var armor = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Module_PlayerArmor.mat");
            Palette(armor.GetTexture("_BaseMap"), out var moduleLight, out _);
            var tint = new Vector3(ivory.r / Mathf.Max(.05f, moduleLight.r), ivory.g / Mathf.Max(.05f, moduleLight.g), ivory.b / Mathf.Max(.05f, moduleLight.b));
            tint /= Mathf.Max(tint.x, Mathf.Max(tint.y, tint.z));
            Undo.RecordObject(armor, "Arm module fit");
            armor.SetColor("_BaseColor", new Color(tint.x, tint.y, tint.z, 1)); armor.SetFloat("_Smoothness", Mathf.Clamp(skinSmooth + .12f, .3f, .55f)); armor.SetFloat("_Metallic", Mathf.Min(armor.GetFloat("_Metallic"), .2f));
            var front = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Module_PlayerFront.mat");
            Undo.RecordObject(front, "Arm module fit"); front.SetFloat("_Smoothness", Mathf.Clamp(skinSmooth + .08f, .28f, .5f));
            var metal = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Receiver_Graphite.mat");
            Undo.RecordObject(metal, "Arm module fit");
            metal.SetColor("_BaseColor", graphite); metal.SetFloat("_Metallic", .45f); metal.SetFloat("_Smoothness", .32f);
            string ivoryPath = Art + "/Coupling_Ivory.mat";
            var shellMaterial = AssetDatabase.LoadAssetAtPath<Material>(ivoryPath);
            if (!shellMaterial) { shellMaterial = new Material(metal) { name = "Coupling_Ivory" }; AssetDatabase.CreateAsset(shellMaterial, ivoryPath); }
            shellMaterial.SetColor("_BaseColor", ivory); shellMaterial.SetFloat("_Metallic", .1f); shellMaterial.SetFloat("_Smoothness", Mathf.Clamp(skinSmooth + .1f, .3f, .5f));
            foreach (var m in new[] { armor, front, metal, shellMaterial }) EditorUtility.SetDirty(m);
            log.AppendLine("armor tint=" + armor.GetColor("_BaseColor").ToString("F3") + " (module light " + moduleLight.ToString("F3") + ") smooth=" + armor.GetFloat("_Smoothness").ToString("F2"));

            // 2. Attachment on the module's rear cross-section centre, so the forearm axis runs through the body.
            var weapon = PrefabUtility.LoadPrefabContents(Weapon);
            try
            {
                var controller = weapon.GetComponent<WeaponController>(); var muzzle = controller.WeaponMuzzle;
                var attachment = weapon.GetComponentsInChildren<Transform>(true).First(t => t.name == "Attachment");
                var points = controller.WeaponRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>())
                    .SelectMany(r => r.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => muzzle.InverseTransformPoint(r.transform.TransformPoint(v)))).ToArray();
                float minZ = points.Min(p => p.z);
                var rear = points.Where(p => p.z < minZ + .06f).ToArray();
                var centre = new Vector3((rear.Min(p => p.x) + rear.Max(p => p.x)) / 2, (rear.Min(p => p.y) + rear.Max(p => p.y)) / 2, minZ + .002f);
                // Keep the arm where it was (the FPS framing hides the forearm cut) and keep the muzzle:
                // move only the visual body so its rear section is centred on the muzzle/forearm axis.
                // Lowest common ancestor of the visible module meshes (the body container), never above WeaponRoot.
                var meshes = controller.WeaponRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>()).Select(r => r.transform).ToArray();
                var visual = meshes[0].parent;
                while (visual != controller.WeaponRoot.transform && !meshes.All(t => t.IsChildOf(visual))) visual = visual.parent;
                log.AppendLine("body container=" + visual.name);
                var attachmentWorld = attachment.position;
                var muzzleWorld = muzzle.position; var muzzleRotation = muzzle.rotation;
                var before = muzzle.InverseTransformPoint(attachment.position);
                visual.position += muzzle.TransformVector(new Vector3(-centre.x, -centre.y, 0));
                if (muzzle.IsChildOf(visual)) muzzle.SetPositionAndRotation(muzzleWorld, muzzleRotation);
                attachment.position = attachment.IsChildOf(visual) ? muzzle.TransformPoint(new Vector3(0, 0, centre.z)) : attachmentWorld;
                log.AppendLine("visual shifted by " + new Vector2(-centre.x, -centre.y).ToString("F4") + " (muzzle space); attachment " + before.ToString("F4") + " -> " + muzzle.InverseTransformPoint(attachment.position).ToString("F4") + " rear slice size=" + new Vector2(rear.Max(p => p.x) - rear.Min(p => p.x), rear.Max(p => p.y) - rear.Min(p => p.y)).ToString("F4"));
                PrefabUtility.SaveAsPrefabAsset(weapon, Weapon);
            }
            finally { PrefabUtility.UnloadPrefabContents(weapon); }

            // 3. Coupling sleeve geometry (same mesh asset, GUID kept) and its two materials.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/UniversalForearm_Coupling.asset");
            Coupling(mesh); EditorUtility.SetDirty(mesh);
            var player = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                var ring = player.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "RightArm_UniversalCoupling");
                ring.sharedMaterials = new[] { shellMaterial, metal };
                PrefabUtility.SaveAsPrefabAsset(player, Player);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            File.WriteAllText(ApplyReport, log.ToString());
            Debug.Log("[ArmModuleFit] applied\n" + log);
        }
    }
}
