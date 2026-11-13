using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class DesignV02Upgrade
    {
        public const string PistolPath = "Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol.prefab";
        public const string PistolProjectilePath = "Assets/IronRequiem/Prefabs/Weapons/Projectile_IR_Pistol.prefab";
        public const string ChargedProjectilePath = "Assets/IronRequiem/Prefabs/Weapons/Projectile_T07_Charged.prefab";
        const string Vfx = "Assets/IronRequiem/VFX/Mechanics";

        [MenuItem("Tools/Iron Requiem/Mechanics/Apply Design Update v02 %#F6")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("User scene changes will not be saved or overwritten.");
            Directory.CreateDirectory(Vfx); AssetDatabase.Refresh();
            PrepareProjectile(PistolProjectilePath, false); PrepareProjectile(ChargedProjectilePath, true);
            PreparePistol();
            EditPrefab(MechanicsLabUpgrade.CannonPrefab, go =>
            {
                var weapon = go.GetComponent<WeaponController>();
                weapon.ShootType = WeaponShootType.Manual; weapon.MaxAmmo = 100;
                weapon.AutomaticReload = false; weapon.HasPhysicalBullets = false;
                weapon.BulletsPerShot = 1; weapon.BulletSpreadAngle = 0;
                weapon.DelayBetweenShots = .5f; weapon.ProjectilePrefab = AssetDatabase.LoadAssetAtPath<ProjectileBase>(ChargedProjectilePath);
                Ensure<CannonChargeShot>(go);
                DisableTemplateSteam(go);
                WeaponFeedbackUpgrade.ConfigureCannon(go);
            });
            EditPrefab(CharacterPrototypeBuilder.PlayerPath, ConfigurePlayer);
            EditPrefab(CharacterPrototypeBuilder.EnemyPath, go =>
            {
                var module = go.GetComponentInChildren<StolenCannon>(true);
                var energy = new SerializedObject(module.GetComponent<CannonAmmo>());
                energy.FindProperty("capacity").floatValue = 100; energy.ApplyModifiedPropertiesWithoutUndo();
                var explosion = new SerializedObject(module.Explosion ? module.Explosion : module.GetComponent<CannonExplosion>());
                explosion.FindProperty("backupFuse").floatValue = 6; explosion.ApplyModifiedPropertiesWithoutUndo();
                module.GetComponent<CannonOverdrive>().ConfigureSmoke(Particles(module.transform, "CriticalSmoke", true));
                var audio = module.GetComponent<AudioSource>(); audio.volume = .3f;
            });
            var scene = SceneManager.GetActiveScene();
            if (scene.path != CharacterPrototypeBuilder.ScenePath) scene = EditorSceneManager.OpenScene(CharacterPrototypeBuilder.ScenePath);
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacterController>(true)).Single();
            ConfigurePlayer(player.gameObject);
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MechanicHud>(true)).Single();
            var status = hud.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "MechanicStatus");
            Undo.RecordObject(status, "Resize energy HUD"); status.fontSize = 21;
            var rect = status.rectTransform; Undo.RecordObject(rect, "Resize energy HUD"); rect.sizeDelta = new Vector2(455, 155);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); CharacterPrototypeBuilder.Validate(); IronRequiemSetupTools.WriteAudit();
            File.WriteAllText("Tools/IronRequiem/Reports/design-v02-upgrade.txt", "Applied design update via Editor APIs at " + DateTime.UtcNow.ToString("O"));
            Debug.Log("IRON REQUIEM: pistol, charged energy cannon, automatic critical overdrive and contact particles configured.");
        }

        static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
        static void EditPrefab(string path, Action<GameObject> edit)
        {
            var go = PrefabUtility.LoadPrefabContents(path);
            try { edit(go); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        static void EnsureVariant(string path, string source)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            try { go.name = Path.GetFileNameWithoutExtension(path); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { Object.DestroyImmediate(go); }
        }
        static void PrepareProjectile(string path, bool charged)
        {
            EnsureVariant(path, "Assets/FPS/Prefabs/Projectiles/Projectile_Blaster.prefab");
            EditPrefab(path, go =>
            {
                var projectile = go.GetComponent<ProjectileStandard>(); projectile.Damage = charged ? 25 : 22;
                projectile.AreaOfDamage = null;
                if (charged) WeaponFeedbackUpgrade.ConfigureProjectile(go);
            });
        }
        static void PreparePistol()
        {
            EnsureVariant(PistolPath, "Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab");
            EditPrefab(PistolPath, go =>
            {
                var weapon = go.GetComponent<WeaponController>(); weapon.WeaponName = "PISTOLA IR";
                weapon.ShootType = WeaponShootType.Manual; weapon.BulletsPerShot = 1; weapon.BulletSpreadAngle = 0;
                weapon.DelayBetweenShots = .4f; weapon.RecoilForce = .12f;
                weapon.ProjectilePrefab = AssetDatabase.LoadAssetAtPath<ProjectileBase>(PistolProjectilePath);
                DisableTemplateSteam(go);
                WeaponFeedbackUpgrade.ConfigurePistol(go);
            });
        }

        static void DisableTemplateSteam(GameObject go)
        {
            // Steam inherited from the blaster must not imply danger on the safe pistol,
            // nor appear on the stolen cannon before its actual critical state.
            foreach(var overheat in go.GetComponentsInChildren<OverheatBehavior>(true))
            {
                overheat.enabled=false;
                if(!overheat.SteamVfx)continue;
                var steam=overheat.SteamVfx;
                steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=steam.main;main.playOnAwake=false;
                var emission=steam.emission;emission.enabled=false;
                steam.gameObject.SetActive(false);
            }
        }
        static void ConfigurePlayer(GameObject go)
        {
            var manager = go.GetComponent<PlayerWeaponsManager>(); Undo.RecordObject(manager, "Start with pistol only");
            manager.StartingWeapons.Clear(); manager.StartingWeapons.Add(AssetDatabase.LoadAssetAtPath<WeaponController>(PistolPath));
            var effects = Ensure<SlideContactEffects>(go); effects.Configure(Particles(go.transform, "SlideContactParticles", false));
            if(PrefabUtility.IsPartOfPrefabInstance(manager))PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        }

        static ParticleSystem Particles(Transform parent, string name, bool smoke)
        {
            var child = parent.Find(name);
            var go = child ? child.gameObject : new GameObject(name);
            if(!child){go.transform.SetParent(parent, false);Undo.RegisterCreatedObjectUndo(go, "Add mechanic particles");}
            var system = Ensure<ParticleSystem>(go); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.loop = true; main.useUnscaledTime = false;
            // FBX modules have parent scale 100: ignore that scale for emitter size and radius.
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = smoke ? 48 : 36;
            main.startLifetime = smoke ? new ParticleSystem.MinMaxCurve(.65f, 1.05f) : new ParticleSystem.MinMaxCurve(.12f, .3f);
            main.startSize = smoke ? new ParticleSystem.MinMaxCurve(.12f, .24f) : new ParticleSystem.MinMaxCurve(.025f, .07f);
            main.startSpeed = smoke ? new ParticleSystem.MinMaxCurve(.15f, .35f) : new ParticleSystem.MinMaxCurve(.3f, .9f);
            main.gravityModifier = smoke ? -.08f : .15f;
            main.startColor = smoke ? new Color(.55f, .58f, .6f, .85f) : new Color(1, .58f, .16f, .8f);
            var emission = system.emission; emission.rateOverTime = smoke ? 22 : 45;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = smoke ? 18 : 55; shape.radius = smoke ? .025f : .2f;
            shape.rotation = new Vector3(-90, 0, 0);
            var size = system.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, smoke ? .5f : 1, 1, smoke ? 2 : .15f));
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = ParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.maxParticleSize = .1f;
            return system;
        }
        static Material ParticleMaterial()
        {
            string path = Vfx + "/MechanicParticles.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.name = "MechanicParticles"; material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            var texturePath = Vfx + "/ParticleSoftCircle.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!texture)
            {
                texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "ParticleSoftCircle", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                    pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 1.5f));
                }
                texture.SetPixels(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture, texturePath);
            }
            material.SetTexture("_BaseMap", texture); AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
