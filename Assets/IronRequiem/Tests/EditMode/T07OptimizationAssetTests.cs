using System.Linq;
using IronRequiem.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Tests
{
    public sealed class T07OptimizationAssetTests
    {
        [Test]
        public void LodsKeepSeparateBodyAndWeaponWithValidGeometryAndNoInventedRig()
        {
            long previous = long.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                var meshes = AssetDatabase.LoadAllAssetsAtPath(T07OptimizationBuilder.Art + "/T07_LOD" + i + ".fbx").OfType<Mesh>().ToArray();
                Assert.That(meshes.Length, Is.EqualTo(2));
                Assert.That(meshes.All(m => m.vertexCount > 0 && m.subMeshCount == 1 && m.bindposeCount == 0), Is.True);
                long triangles = meshes.Sum(T07OptimizationBuilder.Triangles);
                Assert.That(triangles, Is.LessThan(previous)); previous = triangles;
            }
            Assert.That(previous, Is.InRange(2400, 2600));
        }

        [Test]
        public void VariantKeepsIndependentCannonLodsAndOptimizedHeldWeapon()
        {
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(T07OptimizationBuilder.Enemy);
            Assert.That(PrefabUtility.GetPrefabAssetType(enemy), Is.EqualTo(PrefabAssetType.Variant));
            var body = enemy.GetComponentsInChildren<LODGroup>(true).Single(g => g.name == "Body");
            var module = enemy.GetComponentInChildren<StolenCannon>(true);
            var cannon = module.GetComponent<LODGroup>();
            Assert.That(cannon, Is.Not.Null); Assert.That(cannon.transform.IsChildOf(body.transform), Is.False);
            foreach (var group in new[] { body, cannon })
            {
                Assert.That(group.GetLODs(), Has.Length.EqualTo(3));
                Assert.That(group.GetLODs().All(l => l.renderers.Length == 1 && l.renderers[0]), Is.True);
            }
            Assert.That(AssetDatabase.GetAssetPath(module.WeaponPrefab), Is.EqualTo(T07OptimizationBuilder.Weapon));
            var held = module.WeaponPrefab.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "StolenCannonVisual");
            Assert.That(held.sharedMesh, Is.EqualTo(cannon.GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh));
        }
    }
}
