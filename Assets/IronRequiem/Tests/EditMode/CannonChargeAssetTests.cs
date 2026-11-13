using System.Linq;
using IronRequiem.Editor;
using NUnit.Framework;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Tests
{
    public sealed class CannonChargeAssetTests
    {
        [Test] public void EquippedVariantUsesGeometrySocketAndPreservesNativeGameplay()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponController>(CannonChargeUpgrade.WeaponPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(weapon), Is.EqualTo(PrefabAssetType.Variant));
            var mesh = weapon.GetComponentsInChildren<Transform>(true).Single(t => t.name == "StolenCannonVisual");
            Assert.That(mesh.localScale.x, Is.EqualTo(100));
            Assert.That(Vector3.Distance(mesh.TransformPoint(CannonChargeUpgrade.MouthPoint), weapon.WeaponMuzzle.position), Is.LessThan(.0001f));
            Assert.That(weapon.WeaponMuzzle.lossyScale, Is.EqualTo(Vector3.one));
            Assert.That(weapon.GetComponent<CannonChargePresentation>().EffectOrigin.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(weapon.DelayBetweenShots, Is.EqualTo(.5f));
            var projectile = weapon.ProjectilePrefab.GetComponent<ProjectileStandard>();
            var source = AssetDatabase.LoadAssetAtPath<ProjectileStandard>(DesignV02Upgrade.ChargedProjectilePath);
            Assert.That(projectile.Speed, Is.EqualTo(source.Speed)); Assert.That(projectile.TrajectoryCorrectionDistance, Is.EqualTo(source.TrajectoryCorrectionDistance));
            Assert.That(projectile.InheritWeaponVelocity, Is.EqualTo(source.InheritWeaponVelocity)); Assert.That(projectile.GetComponent<ProjectileMuzzleOrigin>(), Is.Not.Null);
            Assert.That(projectile.GetComponent<ChargedProjectileDamage>().MaximumDamage, Is.EqualTo(65));
            Assert.That(projectile.GetComponent<ChargedProjectileDamage>().MaximumRadius, Is.EqualTo(.14f));
        }
        [Test] public void ChargePrefabHasBoundedLocalParticlesSharedUrpMaterialsAndNoGameplay()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(CannonChargeUpgrade.ChargePrefab);
            Assert.That(go.activeSelf, Is.False); Assert.That(go.GetComponentsInChildren<Collider>(true), Is.Empty); Assert.That(go.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(go.GetComponentsInChildren<Rigidbody>(true), Is.Empty); Assert.That(go.GetComponentsInChildren<DamageArea>(true), Is.Empty);
            Assert.That(go.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(6));
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True); Assert.That(renderer.sharedMaterial.shader.name, Does.StartWith("Universal Render Pipeline/"));
                Assert.That(renderer.sharedMaterial.renderQueue, Is.EqualTo(3000));
            }
            var ps = go.GetComponentInChildren<ParticleSystem>(true);
            Assert.That(ps.main.maxParticles, Is.EqualTo(32)); Assert.That(ps.main.playOnAwake, Is.False);
            Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.Local)); Assert.That(ps.main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Local));
            Assert.That(go.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh.triangles.Length / 3), Is.EqualTo(30));
        }
    }
}
