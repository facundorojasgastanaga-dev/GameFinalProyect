using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Tests
{
    public sealed class WeaponPivotAssetTests
    {
        const string Variant = "Assets/IronRequiem/Prefabs/Weapons/Visuals/T07_Cannon_PivotFixed_Visual.prefab";
        const float BarrelLength = 0.6644f;

        [Test] public void CannonVisualPivotSitsOnRearAttachmentAndFiresAlongPositiveZ()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Variant);
            Assert.That(prefab, Is.Not.Null, "Run Tools > Iron Requiem > Weapons > Build T07 Cannon Pivot Fixed Visual first.");
            Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Variant));
            var root = prefab.transform;
            Transform Find(string n) => prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == n);
            Assert.That(root.InverseTransformPoint(Find("WeaponAttachment").position).magnitude, Is.LessThan(.001f));
            var muzzle = Find("WeaponMuzzle");
            Assert.That(Vector3.Distance(root.InverseTransformPoint(muzzle.position), new Vector3(0, 0, BarrelLength)), Is.LessThan(.002f), "Muzzle must lie on +Z at the measured mouth.");
            Assert.That(Vector3.Angle(root.InverseTransformDirection(muzzle.forward), Vector3.forward), Is.LessThan(.1f));
            Assert.That(Vector3.Distance(muzzle.lossyScale, Vector3.one), Is.LessThan(1e-4f), "Sockets must not inherit the FBX x100 node scale.");
            Assert.That(Vector3.Distance(muzzle.position, Find("Muzzle").position), Is.LessThan(1e-4f), "Socket matches the Blender empty.");
        }

        [Test] public void CannonVisualKeepsThreeLodsAndSharedUrpMaterials()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Variant);
            Assert.That(prefab, Is.Not.Null);
            var lods = prefab.GetComponent<LODGroup>().GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            var expected = new[] { 1702, 906, 412 };
            for (int i = 0; i < 3; i++)
            {
                var r = (MeshRenderer)lods[i].renderers.Single();
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                long tris = Enumerable.Range(0, mesh.subMeshCount).Sum(s => (long)mesh.GetIndexCount(s)) / 3;
                Assert.That(tris, Is.EqualTo(expected[i]));
                Assert.That(r.sharedMaterials.Length, Is.EqualTo(2));
                Assert.That(r.sharedMaterials.All(m => m && m.shader.name.StartsWith("Universal Render Pipeline")), Is.True);
            }
        }
    }
}
