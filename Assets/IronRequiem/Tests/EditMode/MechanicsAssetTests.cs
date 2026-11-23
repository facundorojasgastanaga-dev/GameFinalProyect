using System.Linq;
using IronRequiem.Editor;
using NUnit.Framework;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem.Tests
{
    public sealed class MechanicsAssetTests
    {
        [Test] public void NewActionsAreUniqueAndDoNotReuseExistingKeyboardBindings()
        {
            var asset=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/FPS/InputSystem_Actions.inputactions");var map=asset.FindActionMap("Player",true);
            string[] names={"Dash","CollectWeapon","ThrowWeapon","Overdrive"};
            foreach(string name in names)
            {
                // v8 (controls Ágil): bindings are audited by ControlsSchemeTests; here the actions only have to exist as buttons
                var action=map.FindAction(name,true);Assert.That(action.type,Is.EqualTo(InputActionType.Button));
            }
            Assert.That(map.FindAction("NextWeapon"),Is.Null,"NextWeapon was split into WeaponNext/WeaponPrevious");
            Assert.That(map.FindAction("Overdrive").bindings.Count,Is.Zero,"Overdrive is automatic and stays unbound");
        }
        [Test] public void PlayerHasOnlyOneMovementAuthorityAndCannonConfigurationIsPersistent()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.PlayerPath);
            Assert.That(player.GetComponent<IPlayerMotionExtension>(),Is.SameAs(player.GetComponent<AdvancedPlayerMovement>()));
            Assert.That(player.GetComponents<CharacterController>(),Has.Length.EqualTo(1));Assert.That(player.GetComponent<Rigidbody>(),Is.Null);
            var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.EnemyPath);var module=enemy.GetComponentInChildren<StolenCannon>(true);
            Assert.That(module,Is.Not.Null);Assert.That(module.GetComponent<CannonAmmo>().Capacity,Is.EqualTo(100));Assert.That(module.WeaponPrefab,Is.Not.Null);
            var weapon=AssetDatabase.LoadAssetAtPath<WeaponController>(MechanicsLabUpgrade.CannonPrefab);
            Assert.That(weapon.AutomaticReload,Is.False);Assert.That(weapon.HasPhysicalBullets,Is.False);Assert.That(weapon.MaxAmmo,Is.EqualTo(100));
            Assert.That(weapon.ProjectilePrefab,Is.Not.Null);Assert.That(weapon.WeaponMuzzle,Is.Not.Null);
            Assert.That(weapon.GetComponent<CannonChargeShot>(),Is.Not.Null);
        }

        [Test] public void PistolIsASingleShotVariantAndShotgunRemainsAnIndependentAsset()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.PlayerPath);
            var pistol=AssetDatabase.LoadAssetAtPath<WeaponController>(DesignV02Upgrade.PistolPath);
            Assert.That(player.GetComponent<PlayerWeaponsManager>().StartingWeapons,Is.EqualTo(new[]{pistol}));
            Assert.That(PrefabUtility.GetPrefabAssetType(pistol),Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(pistol.ShootType,Is.EqualTo(WeaponShootType.Manual));Assert.That(pistol.BulletsPerShot,Is.EqualTo(1));
            Assert.That(pistol.BulletSpreadAngle,Is.Zero);Assert.That(pistol.DelayBetweenShots,Is.EqualTo(.4f));
            var visual=pistol.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="PistolModule_1000_Equipped");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponFeedbackUpgrade.PistolVisualPath)));
            Assert.That(pistol.WeaponMuzzle.IsChildOf(visual),Is.True);Assert.That(pistol.WeaponMuzzle.name,Is.EqualTo("Muzzle"));
            Assert.That(Vector3.Dot(pistol.WeaponMuzzle.forward,pistol.transform.forward),Is.GreaterThan(.999f));
            Assert.That(visual.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount),Is.EqualTo(1000));
            Assert.That(pistol.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).All(r=>r.transform.IsChildOf(visual)),Is.True);
            Assert.That(pistol.WeaponAnimator,Is.Null);
            var shotgun=AssetDatabase.LoadAssetAtPath<WeaponController>("Assets/FPS/Prefabs/Weapons/Weapon_Shotgun.prefab");
            Assert.That(shotgun,Is.Not.Null);Assert.That(shotgun.BulletsPerShot,Is.GreaterThan(1));
            Assert.That(shotgun,Is.Not.EqualTo(pistol));
            Assert.That(pistol.GetComponentInChildren<CannonOverdrive>(true),Is.Null);
            Assert.That(pistol.GetComponentInChildren<CannonExplosion>(true),Is.Null);
            var cannon=AssetDatabase.LoadAssetAtPath<WeaponController>(MechanicsLabUpgrade.CannonPrefab);
            foreach(var variant in new[]{pistol,cannon})
            {
                var steam=variant.GetComponent<OverheatBehavior>();Assert.That(steam.enabled,Is.False);
                Assert.That(steam.SteamVfx.gameObject.activeSelf,Is.False);Assert.That(steam.SteamVfx.emission.enabled,Is.False);
            }
            var original=AssetDatabase.LoadAssetAtPath<WeaponController>("Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab");
            Assert.That(original.GetComponent<OverheatBehavior>().enabled,Is.True,"The source blaster keeps its original effect");
        }

        [Test] public void CriticalAndSlideParticlesAreBoundedAndUseSupportedUrpMaterial()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.PlayerPath);
            var slide=player.GetComponent<SlideContactEffects>().ContactParticles;
            var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.EnemyPath);
            var overdrive=enemy.GetComponentInChildren<CannonOverdrive>(true);
            Assert.That(overdrive.CriticalThreshold,Is.EqualTo(.2f));Assert.That(overdrive.CriticalGraceDuration,Is.EqualTo(5));
            foreach(var system in new[]{slide,overdrive.Smoke})
            {
                Assert.That(system,Is.Not.Null);Assert.That(system.main.playOnAwake,Is.False);
                Assert.That(system.main.maxParticles,Is.LessThanOrEqualTo(48));Assert.That(system.main.useUnscaledTime,Is.False);
                Assert.That(system.main.scalingMode,Is.EqualTo(ParticleSystemScalingMode.Local),"Imported FBX parent scale must not inflate the effect");
                var material=system.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.That(material.shader.isSupported,Is.True);Assert.That(material.GetTexture("_BaseMap"),Is.Not.Null);
            }
            var damage=AssetDatabase.LoadAssetAtPath<GameObject>(DesignV02Upgrade.ChargedProjectilePath).GetComponent<ChargedProjectileDamage>();
            Assert.That(damage.MinimumDamage,Is.EqualTo(25));Assert.That(damage.MaximumDamage,Is.EqualTo(65));
            Assert.That(damage.EnergyVisual,Is.Not.Null);Assert.That(damage.MinimumRadius,Is.EqualTo(.04f));Assert.That(damage.MaximumRadius,Is.EqualTo(.14f));
            var cannon=AssetDatabase.LoadAssetAtPath<GameObject>(MechanicsLabUpgrade.CannonPrefab);
            var charge=cannon.GetComponent<CannonChargePresentation>();Assert.That(charge,Is.Not.Null);
            Assert.That(charge.EnergyParticles.main.maxParticles,Is.LessThanOrEqualTo(32));
            Assert.That(charge.EnergyParticles.main.playOnAwake,Is.False);Assert.That(charge.IsVisible,Is.False);
            Assert.That(charge.EnergyParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported,Is.True);
        }
    }
}
