using System.Linq;
using IronRequiem.Editor;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IronRequiem.Tests
{
    public sealed class T07AnimationAssetTests
    {
        [Test] public void AllThreeSkinsShareRealBonesWeightsUvsAndOriginalTriangleCounts()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(T07AnimationBuilder.Draft);Assert.That(prefab,Is.Not.Null);
            var group=prefab.GetComponentsInChildren<LODGroup>(true).Single(g=>g.name=="Body");
            var skins=group.GetLODs().Select(l=>l.renderers.Single() as SkinnedMeshRenderer).ToArray();
            Assert.That(skins,Has.Length.EqualTo(3));long[] expected={10225,5111,2128};
            for(int i=0;i<3;i++)
            {
                Assert.That(skins[i],Is.Not.Null);var mesh=skins[i].sharedMesh;
                Assert.That(mesh.bindposeCount,Is.EqualTo(17));Assert.That(skins[i].bones,Is.EqualTo(skins[0].bones));
                Assert.That(mesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f),Is.True);
                Assert.That(mesh.uv,Has.Length.EqualTo(mesh.vertexCount));Assert.That(mesh.triangles.LongLength/3,Is.EqualTo(expected[i]));
                Assert.That(skins[i].sharedMaterial,Is.SameAs(skins[0].sharedMaterial));
            }
            var module=prefab.GetComponentInChildren<StolenCannon>(true);
            Assert.That(module.GetComponentsInChildren<SkinnedMeshRenderer>(true),Is.Empty);
            Assert.That(module.GetComponent<LODGroup>().GetLODs(),Has.Length.EqualTo(3));
            Assert.That(module.GetComponentsInChildren<Transform>(true).Intersect(skins[0].bones),Is.Empty);
        }
        [Test] public void AnimatorKeepsNativeContractInPlaceClipsAndSeparateDeathState()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(T07AnimationBuilder.Draft);var animator=prefab.GetComponent<EnemyMobile>().Animator;
            Assert.That(animator.applyRootMotion,Is.False);Assert.That(prefab.GetComponent<T07AnimationDriver>(),Is.Not.Null);
            var controller=(AnimatorController)animator.runtimeAnimatorController;
            foreach(string name in new[]{"MoveSpeed","Attack","Alerted","OnDamaged","Disarmed","Dead","Cycle","Turn"})Assert.That(controller.parameters.Any(p=>p.name==name),Is.True,name);
            foreach(string name in new[]{"Idle","CombatIdle","Walk","Turn","CannonAttack","HitReaction","DisarmedWalk","Death"})
                Assert.That(controller.animationClips.Any(c=>c.name==name),Is.True,name);
            foreach(var clip in controller.animationClips)
                Assert.That(AnimationUtility.GetCurveBindings(clip).All(b=>!string.IsNullOrEmpty(b.path)),Is.True,"No animation curve may move the gameplay root");
            Assert.That(prefab.GetComponent<EnemyController>().DeathDuration,Is.GreaterThan(1.35f));
        }
        [Test] public void ExplosionVariantHasBoundedPresentationAndNoDamageOrPhysicsFragments()
        {
            var effect=AssetDatabase.LoadAssetAtPath<GameObject>(T07AnimationBuilder.ExplosionPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(effect),Is.EqualTo(PrefabAssetType.Variant));
            var presentation=effect.GetComponent<CannonExplosionPresentation>();Assert.That(presentation,Is.Not.Null);
            Assert.That(effect.GetComponentsInChildren<DamageArea>(true),Is.Empty);Assert.That(effect.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(effect.GetComponentsInChildren<Rigidbody>(true),Is.Empty);Assert.That(effect.GetComponentsInChildren<Light>(true),Has.Length.EqualTo(1));
            Assert.That(presentation.Duration,Is.InRange(2.7f,3.5f));Assert.That(presentation.Layers.Sum(p=>p.main.maxParticles),Is.LessThanOrEqualTo(70));
            foreach(var ps in presentation.Layers)
            {
                Assert.That(ps.main.loop,Is.False);Assert.That(ps.main.playOnAwake,Is.False);Assert.That(ps.collision.enabled,Is.False);
                Assert.That(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name,Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));
            }
            Assert.That(effect.GetComponent<AudioSource>().clip,Is.Not.Null);Assert.That(effect.GetComponent<AudioSource>().spatialBlend,Is.EqualTo(1));
        }
    }
}
