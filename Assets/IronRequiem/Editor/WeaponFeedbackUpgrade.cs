using System;
using System.IO;
using System.Linq;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Specific prefab upgrade; no scene writes, package changes or general command endpoint.
    public static class WeaponFeedbackUpgrade
    {
        public const string PistolVisualPath = "Assets/IronRequiem/Prefabs/Weapons/Visuals/PistolModule_1000_Visual.prefab";
        const string Vfx = "Assets/IronRequiem/VFX/Mechanics";

        [MenuItem("Tools/Iron Requiem/Weapons/Apply Pistol Module and Charge Feedback %#F11")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("User scene changes will not be overwritten.");
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PistolVisualPath)) throw new InvalidOperationException("PistolModule_1000 visual is missing.");
            Edit(DesignV02Upgrade.PistolPath, ConfigurePistol);
            Edit(MechanicsLabUpgrade.CannonPrefab, ConfigureCannon);
            Edit(DesignV02Upgrade.ChargedProjectilePath, ConfigureProjectile);
            IronRequiemSetupTools.WriteAudit();
            File.WriteAllText("Tools/IronRequiem/Reports/weapon-feedback-upgrade.txt", "Applied prefab-only upgrade at " + DateTime.UtcNow.ToString("O"));
        }

        static void Edit(string path, Action<GameObject> configure)
        {
            var go = PrefabUtility.LoadPrefabContents(path);
            try { configure(go); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        public static void ConfigurePistol(GameObject go)
        {
            var weapon = go.GetComponent<WeaponController>();
            weapon.DelayBetweenShots = .4f;
            // Cylinder energy loops belong to the template blaster, not the permanent pistol.
            foreach (var particles in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main; main.playOnAwake = false;
                var emission = particles.emission; emission.enabled = false;
                particles.gameObject.SetActive(false);
            }
            weapon.WeaponAnimator = null;
            var legacyMuzzle = go.GetComponentsInChildren<Transform>(true).Single(t => t.name == "GunMuzzle");
            foreach (var animator in go.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) renderer.enabled = false;
            var visual = weapon.WeaponRoot.transform.Find("PistolModule_1000_Equipped");
            if (!visual)
            {
                visual = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PistolVisualPath), weapon.WeaponRoot.transform)).transform;
                visual.name = "PistolModule_1000_Equipped";
            }
            visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
            var muzzle = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Muzzle");
            // Preserve the existing camera/aim line and use the actual model's barrel socket.
            visual.position += legacyMuzzle.position - muzzle.position;
            weapon.WeaponMuzzle = muzzle;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
        }

        public static void ConfigureCannon(GameObject go)
        {
            var weapon = go.GetComponent<WeaponController>();
            var root = Child(weapon.WeaponMuzzle, "ChargeEnergy"); root.localPosition = new Vector3(0, 0, .04f);
            root.localRotation = Quaternion.identity; root.localScale = Vector3.one; root.gameObject.SetActive(true);
            var core = Sphere(root, "EnergyCore", CoreMaterial()); core.localScale = Vector3.one * .035f;
            var particleTransform = Child(root, "EnergyOrbit");
            var system = particleTransform.GetComponent<ParticleSystem>();
            if (!system) system = particleTransform.gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.loop = true; main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, .28f); main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .035f); main.startColor = new Color(.15f, 1, 1, .9f);
            main.gravityModifier = 0; main.useUnscaledTime = false;
            var emission = system.emission; emission.enabled = true; emission.rateOverTime = 18;
            var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .13f; shape.radiusThickness = .15f;
            var velocity = system.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalZ = 5; velocity.radial = -.5f;
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.cyan, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = EnergyMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.maxParticleSize = .08f;
            var presentation = go.GetComponent<CannonChargePresentation>();
            if (!presentation) presentation = go.AddComponent<CannonChargePresentation>();
            presentation.Configure(root.gameObject, core, system);
        }

        public static void ConfigureProjectile(GameObject go)
        {
            var projectile = go.GetComponent<ProjectileStandard>();
            // Remove the inherited ten-metre visual offset from this variant's collision sweep.
            go.transform.localScale = Vector3.one; projectile.Root = go.transform; projectile.Tip = go.transform;
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
            var visual = Sphere(go.transform, "ChargedEnergyBolt", CoreMaterial()); visual.localPosition = Vector3.zero;
            var trail = go.GetComponent<TrailRenderer>(); if (!trail) trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = EnergyMaterial(); trail.time = .045f; trail.minVertexDistance = .2f;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0); trail.startColor = Color.cyan; trail.endColor = new Color(0, 1, 1, 0);
            var payload = go.GetComponent<ChargedProjectileDamage>(); if (!payload) payload = go.AddComponent<ChargedProjectileDamage>();
            payload.ConfigureVisual(visual, trail); payload.ApplyCharge(0);
        }

        static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            return child;
        }

        static Transform Sphere(Transform parent, string name, Material material)
        {
            var child = parent.Find(name);
            if (!child)
            {
                child = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; child.name = name; child.SetParent(parent, false);
                Object.DestroyImmediate(child.GetComponent<Collider>());
            }
            var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return child;
        }

        static Material CoreMaterial()
        {
            const string path = Vfx + "/ChargeEnergyCore.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ChargeEnergyCore" };
            material.SetColor("_BaseColor", new Color(.2f, 2, 2.8f, 1)); AssetDatabase.CreateAsset(material, path); return material;
        }
        static Material EnergyMaterial()
        {
            const string path = Vfx + "/ChargeEnergyParticles.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "ChargeEnergyParticles" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Vfx + "/ParticleSoftCircle.asset"));
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 2); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One); material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
