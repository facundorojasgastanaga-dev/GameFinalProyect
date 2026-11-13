using System.Linq;
using IronRequiem.Editor;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Tests
{
    public sealed class T07DestructionAssetTests
    {
        [Test] public void VariantKeepsRealSkeletonLodsCannonAndHealthWithIndependentArmor()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(T07DestructionBuilder.Enemy); Assert.That(PrefabUtility.GetPrefabAssetType(go), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(go.GetComponent<Health>(), Is.Not.Null); var d = go.GetComponent<RobotDamageProgression>(); Assert.That(d.Armor.Length, Is.EqualTo(4));
            var body = go.GetComponentsInChildren<LODGroup>(true).Single(g => g.name == "Body"); Assert.That(body.GetLODs().Length, Is.EqualTo(3));
            foreach (var lod in body.GetLODs()) Assert.That(lod.renderers.Length, Is.EqualTo(5));
            foreach (var renderer in body.GetLODs().SelectMany(l => l.renderers))
                Assert.That(go.GetComponentsInChildren<LODGroup>(true).Count(g => g.GetLODs().Any(l => l.renderers.Contains(renderer))), Is.EqualTo(1));
            foreach (var p in d.Armor) { Assert.That(p.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(3)); Assert.That(p.DebrisSocket, Is.Not.Null); }
            Assert.That(go.GetComponent<T07AnimationDriver>().Animator.runtimeAnimatorController, Is.Not.Null); Assert.That(go.GetComponentInChildren<DetachableCannon>(true), Is.Not.Null);
            var module = go.GetComponentInChildren<StolenCannon>(true); Assert.That(module.GetComponent<LODGroup>().GetLODs().Length, Is.EqualTo(3));
            Assert.That(AssetDatabase.GetAssetPath(module.WeaponPrefab), Is.EqualTo(T07OptimizationBuilder.Weapon));
        }
        [Test] public void VisualExplosionAndCosmeticDebrisCannotApplyDamageOrCollisions()
        {
            var fx = AssetDatabase.LoadAssetAtPath<GameObject>(T07DestructionBuilder.Burst); Assert.That(fx.GetComponent<RobotBodyBurst>(), Is.Not.Null);
            Assert.That(fx.GetComponent<CannonExplosion>(), Is.Null); Assert.That(fx.GetComponentsInChildren<DamageArea>(true), Is.Empty);
            Assert.That(fx.GetComponentsInChildren<Collider>(true), Is.Empty); Assert.That(fx.GetComponentsInChildren<Rigidbody>(true), Is.Empty); Assert.That(fx.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(fx.GetComponent<RobotBodyBurst>().Layers.Sum(p => p.main.maxParticles), Is.EqualTo(29));
            foreach (var ps in fx.GetComponent<RobotBodyBurst>().Layers) Assert.That(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported, Is.True);
            Assert.That(fx.activeSelf, Is.False); Assert.That(fx.GetComponent<AudioSource>().playOnAwake, Is.False);
        }
        [Test] public void AllArmorMeshesHaveClosedCapsAndRealRigidWeights()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(T07DestructionBuilder.Art + "/T07_ArmorRig.fbx");
            var plates = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("Armor_")).ToArray(); Assert.That(plates.Length, Is.EqualTo(12));
            foreach (var p in plates)
            {
                Assert.That(p.sharedMesh.subMeshCount, Is.GreaterThanOrEqualTo(2)); Assert.That(p.sharedMesh.uv.Length, Is.EqualTo(p.sharedMesh.vertexCount));
                Assert.That(p.sharedMesh.boneWeights.All(w => w.weight0 > .999f), Is.True); Assert.That(p.sharedMesh.bindposes.Length, Is.GreaterThan(0));
            }
        }
    }
}
