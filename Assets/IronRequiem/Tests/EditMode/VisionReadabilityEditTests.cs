using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Tests
{
    /// Chat 11 · option C (passive readability). Option C adds no input action, so there is no binding to test
    /// (VISION_V01.md §4); these cover colours, debris and the material variants.
    public sealed class VisionReadabilityEditTests
    {
        [Test]
        public void HighlightColourIsNotReservedAndStaysUnderHdrCap()
        {
            Assert.That(VisionMaterialRules.IsReservedColor(VisionMaterialRules.Tint), Is.False, "Vision tint must not read as a door colour");
            // the classifier really catches the door colours (red, amber, green of 04_PUERTAS / 05_ILUMINACION)
            Assert.That(VisionMaterialRules.IsReservedColor(new Color(1f, .1f, .05f)), Is.True, "red");
            Assert.That(VisionMaterialRules.IsReservedColor(new Color(1f, .6f, .15f)), Is.True, "amber");
            Assert.That(VisionMaterialRules.IsReservedColor(new Color(.2f, 1f, .3f)), Is.True, "green");
            foreach (float k in new[] { 0f, .5f, 1f })
                foreach (float s in new[] { .5f, 1f, 1.5f })
                {
                    var rim = VisionMaterialRules.RimFor(k, s);
                    Assert.That(rim.maxColorComponent, Is.LessThanOrEqualTo(4f), "HDR ≤ 4 (05_ILUMINACION §L1.3)");
                    Assert.That(VisionMaterialRules.IsReservedColor(rim), Is.False, $"rim k={k} s={s}");
                }
            Assert.That(VisionMaterialRules.RimFor(1, 1).maxColorComponent, Is.GreaterThan(VisionMaterialRules.RimFor(0, 1).maxColorComponent), "darker → stronger");
            Assert.That(VisionMaterialRules.RimFor(1, 0).maxColorComponent, Is.EqualTo(0f), "intensity 0 = off");
        }

        [Test]
        public void RobotDebrisIsNeverHighlighted()
        {
            var debris = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs/Scrap" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(debris, Is.Not.Empty, "scrap prefabs present");
            var map = Resources.Load<VisionMaterialMap>(VisionMaterialMap.ResourcePath);
            foreach (var path in debris)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                // the runtime only tracks EnemyController instances
                Assert.That(go.GetComponentInChildren<Unity.FPS.AI.EnemyController>(true), Is.Null, path + " is not an enemy");
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials.Where(m => m))
                    {
                        Assert.That(m.IsKeywordEnabled("_RIM"), Is.False, $"{path}: {m.name} has no rim (D1.8)");
                        Assert.That(map.Pairs.Any(p => p.Source == m || p.Variant == m), Is.False, $"{path}: {m.name} is not a vision material");
                    }
            }
        }

        [Test]
        public void VisionVariantsLookLikeTheirSourceUntilDriven()
        {
            var map = Resources.Load<VisionMaterialMap>(VisionMaterialMap.ResourcePath);
            Assert.That(map, Is.Not.Null, "VisionMaterialMap in Resources/Vision");
            Assert.That(map.Pairs, Is.Not.Empty);
            foreach (var p in map.Pairs)
            {
                Assert.That(p.Source && p.Variant, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(p.Variant), Does.StartWith("Assets/IronRequiem/Art/Vision/"), "variants are new assets, sources untouched");
                Assert.That(p.Source.IsKeywordEnabled("_RIM"), Is.False, p.Source.name + " source keeps no rim");
                Assert.That(p.Variant.IsKeywordEnabled("_RIM"), Is.True, p.Variant.name + " has the rim keyword");
                Assert.That(p.Variant.GetColor("_RimColor").maxColorComponent, Is.EqualTo(0f), p.Variant.name + " default rim is black");
                Assert.That(p.Variant.GetTexture("_BaseMap"), Is.EqualTo(p.Source.HasProperty("_BaseMap") ? p.Source.GetTexture("_BaseMap") : null), p.Variant.name + " keeps the albedo");
            }
        }
    }
}
