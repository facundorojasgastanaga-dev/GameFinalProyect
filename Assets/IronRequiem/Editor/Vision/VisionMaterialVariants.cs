using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools.Vision
{
    /// Chat 11 · F2 (passive dark readability). The fresnel rim of IronRequiem/HardSurface is a material keyword (_RIM),
    /// so a MaterialPropertyBlock can only drive its colour on materials that already have it. This tool:
    ///  - Audit (read only): lists, per enemy prefab, every renderer material, its shader and whether _RIM is on.
    ///  - Build variants: for body materials WITHOUT rim, creates a NEW material copy "<name>_Vision" with the rim keyword
    ///    on and a neutral rim (the runtime sets its colour) under Art/Vision/Materials, and records the pairs in
    ///    Art/Vision/VisionMaterialMap.asset. Source materials and prefabs are never modified; the runtime swaps materials
    ///    on live enemy instances only.
    public static class VisionMaterialVariants
    {
        const string Menu = "Tools/Iron Requiem/Vision/";
        const string Report = "Tools/IronRequiem/Reports/Vision/F2";
        const string VariantFolder = "Assets/IronRequiem/Art/Vision/Materials";
        public const string MapPath = "Assets/IronRequiem/Art/Vision/Resources/Vision/VisionMaterialMap.asset";

        public static IEnumerable<string> EnemyPrefabs()
        {
            var paths = new List<string>();
            foreach (var folder in new[] { "Assets/IronRequiem/Prefabs/Map01/Enemies", "Assets/IronRequiem/Prefabs/Campaign" })
                if (AssetDatabase.IsValidFolder(folder))
                    paths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath));
            return paths.Distinct().Where(p => { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); return go && go.GetComponentInChildren<Unity.FPS.AI.EnemyController>(true); }).OrderBy(p => p);
        }

        [MenuItem(Menu + "F2 Audit enemy materials (read only)")]
        public static void Audit()
        {
            var sb = new StringBuilder();
            foreach (var path in EnemyPrefabs())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); sb.AppendLine("## " + path);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!VisionMaterialRules.IsBodyRenderer(r)) continue;
                    foreach (var m in r.sharedMaterials.Where(m => m))
                        sb.AppendLine($"  {r.name} [{(r.gameObject.activeSelf ? "on" : "off")}] -> {m.name} | {m.shader.name} | rim={m.IsKeywordEnabled("_RIM")} | emissive={IsEmissive(m)} | eligible={VisionMaterialRules.IsBodyMaterial(m)}");
                }
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/enemy-materials.txt", sb.ToString());
            Debug.Log("[Vision] enemy material audit: " + Report + "/enemy-materials.txt");
        }
        static bool IsEmissive(Material m) => m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > .4f;

        [MenuItem(Menu + "F2 Build rim variants for enemy materials")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            var hs = Shader.Find("IronRequiem/HardSurface");
            Directory.CreateDirectory(VariantFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(MapPath)); var map = AssetDatabase.LoadAssetAtPath<VisionMaterialMap>(MapPath);
            if (!map) { map = ScriptableObject.CreateInstance<VisionMaterialMap>(); AssetDatabase.CreateAsset(map, MapPath); }
            var pairs = new List<VisionMaterialMap.Pair>(map.Pairs ?? new VisionMaterialMap.Pair[0]);
            var log = new StringBuilder();
            foreach (var path in EnemyPrefabs())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!VisionMaterialRules.IsBodyRenderer(r)) continue;
                    foreach (var src in r.sharedMaterials.Where(m => m))
                    {
                        if (!VisionMaterialRules.IsBodyMaterial(src) || src.IsKeywordEnabled("_RIM") || pairs.Any(p => p.Source == src)) continue;
                        string variantPath = VariantFolder + "/" + src.name + "_Vision.mat";
                        var variant = AssetDatabase.LoadAssetAtPath<Material>(variantPath);
                        if (!variant)
                        {
                            if (src.shader == hs) variant = new Material(src);
                            else
                            {
                                variant = new Material(hs);
                                CopyTexture(src, variant, "_BaseMap"); CopyColor(src, variant, "_BaseColor"); CopyFloat(src, variant, "_Metallic"); CopyFloat(src, variant, "_Smoothness");
                                if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap")) { variant.SetTexture("_BumpMap", src.GetTexture("_BumpMap")); variant.EnableKeyword("_NORMALMAP"); variant.SetFloat("_UseNormalMap", 1); }
                                if (src.HasProperty("_MetallicGlossMap") && src.GetTexture("_MetallicGlossMap")) variant.SetTexture("_MetallicGlossMap", src.GetTexture("_MetallicGlossMap"));
                            }
                            variant.EnableKeyword("_RIM"); variant.SetFloat("_UseRim", 1);
                            variant.SetColor("_RimColor", Color.black); variant.SetFloat("_RimPower", VisionMaterialRules.RimPower); // black = looks like the source until the runtime drives it
                            AssetDatabase.CreateAsset(variant, variantPath);
                        }
                        pairs.Add(new VisionMaterialMap.Pair { Source = src, Variant = variant });
                        log.AppendLine($"{path}: {src.name} ({src.shader.name}) -> {variantPath}");
                    }
                }
            }
            map.Pairs = pairs.ToArray(); EditorUtility.SetDirty(map); AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/variants.txt", log.Length > 0 ? log.ToString() : "no new variants\n");
            Debug.Log("[Vision] rim variants: " + pairs.Count + " pairs");
        }
        static void CopyTexture(Material a, Material b, string p) { if (a.HasProperty(p)) { b.SetTexture(p, a.GetTexture(p)); b.SetTextureScale(p, a.GetTextureScale(p)); b.SetTextureOffset(p, a.GetTextureOffset(p)); } }
        static void CopyColor(Material a, Material b, string p) { if (a.HasProperty(p)) b.SetColor(p, a.GetColor(p)); }
        static void CopyFloat(Material a, Material b, string p) { if (a.HasProperty(p)) b.SetFloat(p, a.GetFloat(p)); }
    }
}
