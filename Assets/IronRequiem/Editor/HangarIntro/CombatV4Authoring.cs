using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// v4 (12/11/2026) — combat pass on the existing systems (no parallel systems):
    ///  · Weapon_IR_Fist: permanent melee slot (WeaponController.PermanentMelee + FistWeaponInput -> ProtagonistMelee charge).
    ///  · BigBot_Current: not executable (EnemyExecutionSource.executionEnabled=false), +50% shell damage, bigger blast
    ///    radius and a heavier explosion (BigBotBlastFx on a copy of its projectile). Its stolen-weapon assets stay.
    ///  · RequiemCampaign: the player starts with the fist only; a montage station in the lobby airlock mounts
    ///    Weapon_IR_ArmCannon_VO after the alarm corridor; slide-guard and fist-charge feedback + HUD.
    /// Idempotent: assets are created once (GUIDs kept) and updated in place; scene objects named V4_* are rebuilt.
    internal static class CombatV4Authoring
    {
        const string Menu = "Tools/Iron Requiem/Combat v4/";
        const string Session = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string AudioDir = "Assets/IronRequiem/Audio/SFX/V4";
        const string FistPath = "Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Fist.prefab";
        const string CannonPath = "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab";
        const string ShotgunPath = "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab";
        const string BigBotPath = "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab";
        const string BigBotBase = "Assets/IronRequiem/Prefabs/VisualOverhaul/BigBot_Burst_v10_NavThrottle_VO.prefab";
        const string BlastProjectilePath = "Assets/IronRequiem/Prefabs/Projectiles/Projectile_BigBot_Blast_v4.prefab";
        const string VfxDir = "Assets/IronRequiem/Prefabs/VFX";
        const string BlastFxPath = VfxDir + "/FX_BigBot_Blast_v4.prefab";
        const string FistFxPath = VfxDir + "/FX_Fist_HeavyImpact_v4.prefab";
        const string DeflectFxPath = VfxDir + "/FX_Slide_Deflect_v4.prefab";
        const string RingSprite = "Assets/IronRequiem/Art/VisualPass/IR_V4_Ring.png";
        const string EdgeSprite = "Assets/IronRequiem/Art/VisualPass/IR_V4_EdgeGlow.png";
        // Big Bot tuning (multipliers on the base VO prefab, so re-running never compounds)
        internal const float BigBotDamageScale = 1.5f, BigBotRadiusScale = 1.6f;

        [MenuItem(Menu + "Apply assets + campaign session (fist, loadout, Big Bot)")]
        static void ApplyMenu()
        {
            var log = new StringBuilder("Combat v4 " + System.DateTime.Now + "\n");
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Unsaved scene open: " + SceneManager.GetSceneAt(i).path);
                var m = HangarMenuAuthoring.BuildMaterials();
                PrepareAssets(log, m);
                var scene = EditorSceneManager.OpenScene(Session, OpenSceneMode.Single);
                var lobby = scene.GetRootGameObjects().Single(g => g.name == "PersistentAirlocks").transform.Find("StartLobby");
                ApplyToSession(scene, lobby, m, log);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[CombatV4] ok\n" + log);
            }
            catch (System.Exception e) { Debug.LogError("[CombatV4] " + e + "\n" + log); }
            Directory.CreateDirectory("Tools/IronRequiem/Reports/HangarIntro");
            File.WriteAllText("Tools/IronRequiem/Reports/HangarIntro/combat_v4.txt", log.ToString());
        }

        // Editor GetComponent can return a fake-null object, so no '??' with components.
        static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c ? c : go.AddComponent<T>(); }
        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + name + ".wav");

        // ------------------------------------------------------------------ assets
        internal static void PrepareAssets(StringBuilder log, HangarMenuAuthoring.Mats m)
        {
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                var ai = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter; if (!ai) continue;
                var s = ai.defaultSampleSettings;
                if (!ai.forceToMono || s.loadType != AudioClipLoadType.DecompressOnLoad)
                { ai.forceToMono = true; s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.ADPCM; ai.defaultSampleSettings = s; ai.SaveAndReimport(); }
            }
            EnsureSprite(RingSprite, Ring);
            EnsureSprite(EdgeSprite, Edge);
            BuildFx(BlastFxPath, root => BlastFx(root, m), log);
            BuildFx(FistFxPath, root => FistFx(root, m), log);
            BuildFx(DeflectFxPath, root => DeflectFx(root, m), log);
            BuildFist(log);
            BuildBigBot(log);
        }

        static void BuildFist(StringBuilder log)
        {
            bool exists = File.Exists(FistPath);
            var root = exists ? PrefabUtility.LoadPrefabContents(FistPath) : new GameObject("Weapon_IR_Fist");
            try
            {
                Transform Child(string name) { var t = root.transform.Find(name); if (!t) { t = new GameObject(name).transform; t.SetParent(root.transform, false); } return t; }
                var weaponRoot = Child("FistRoot"); var muzzle = Child("FistMuzzle"); muzzle.localPosition = new Vector3(-.1f, -.1f, .5f);
                var audio = Get<AudioSource>(root); audio.playOnAwake = false; audio.spatialBlend = 0;
                var weapon = Get<WeaponController>(root);
                weapon.WeaponName = "PUÑO"; weapon.PermanentMelee = true; weapon.WeaponRoot = weaponRoot.gameObject; weapon.WeaponMuzzle = muzzle;
                weapon.ShootType = WeaponShootType.Manual; weapon.ProjectilePrefab = null; weapon.AimZoomRatio = 1f; weapon.AimOffset = Vector3.zero;
                weapon.RecoilForce = 0; weapon.DelayBetweenShots = .3f; weapon.MaxAmmo = 1; weapon.AutomaticReload = true; weapon.UsesHeatInsteadOfAmmo = false;
                weapon.HasPhysicalBullets = false; weapon.UseContinuousShootSound = false; weapon.ShootSfx = null; weapon.MuzzleFlashPrefab = null;
                var cannonGo = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPath); var cannon = cannonGo ? cannonGo.GetComponent<WeaponController>() : null;
                if (cannon) { weapon.CrosshairDataDefault = cannon.CrosshairDataDefault; weapon.CrosshairDataTargetInSight = cannon.CrosshairDataTargetInSight; weapon.ChangeWeaponSfx = cannon.ChangeWeaponSfx; }
                if (!root.GetComponent<FistWeaponInput>()) root.AddComponent<FistWeaponInput>();
                PrefabUtility.SaveAsPrefabAsset(root, FistPath);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
            log.AppendLine("fist weapon: " + FistPath + (exists ? " (updated)" : " (created)"));
        }

        static void BuildBigBot(StringBuilder log)
        {
            var baseCombat = AssetDatabase.LoadAssetAtPath<GameObject>(BigBotBase).GetComponentInChildren<BigBotCombat>(true);
            var baseSo = new SerializedObject(baseCombat);
            float baseDamage = baseSo.FindProperty("damage").floatValue, baseRadius = baseSo.FindProperty("explosionRadius").floatValue;
            var baseCannon = baseSo.FindProperty("cannons").arraySize > 0 ? baseSo.FindProperty("cannons").GetArrayElementAtIndex(0).objectReferenceValue as WeaponController : null;
            var sourceProjectile = baseCannon ? baseCannon.ProjectilePrefab : null;
            if (!sourceProjectile) throw new System.Exception("Big Bot base projectile not found");
            // projectile copy with the heavier blast presentation (the shared source projectile is untouched)
            if (!File.Exists(BlastProjectilePath)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceProjectile), BlastProjectilePath);
            var proj = PrefabUtility.LoadPrefabContents(BlastProjectilePath);
            try
            {
                var fx = Get<BigBotBlastFx>(proj);
                fx.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(BlastFxPath), 18f, .9f, .5f);
                var standard = proj.GetComponent<ProjectileStandard>(); if (standard) standard.ImpactVfxLifetime = Mathf.Max(standard.ImpactVfxLifetime, 2f);
                PrefabUtility.SaveAsPrefabAsset(proj, BlastProjectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(proj); }
            var blastProjectile = AssetDatabase.LoadAssetAtPath<GameObject>(BlastProjectilePath).GetComponent<ProjectileBase>();

            var root = PrefabUtility.LoadPrefabContents(BigBotPath);
            try
            {
                var combat = root.GetComponentInChildren<BigBotCombat>(true);
                var so = new SerializedObject(combat);
                so.FindProperty("damage").floatValue = baseDamage * BigBotDamageScale;
                so.FindProperty("explosionRadius").floatValue = baseRadius * BigBotRadiusScale;
                so.FindProperty("explosiveShots").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                var cannons = so.FindProperty("cannons"); int set = 0;
                for (int i = 0; i < cannons.arraySize; i++) { var w = cannons.GetArrayElementAtIndex(i).objectReferenceValue as WeaponController; if (w) { w.ProjectilePrefab = blastProjectile; EditorUtility.SetDirty(w); set++; } }
                var execution = root.GetComponentInChildren<EnemyExecutionSource>(true);
                if (execution) { var eso = new SerializedObject(execution); eso.FindProperty("executionEnabled").boolValue = false; eso.ApplyModifiedPropertiesWithoutUndo(); }
                PrefabUtility.SaveAsPrefabAsset(root, BigBotPath);
                log.AppendLine($"BigBot_Current: damage {baseDamage:F1} -> {baseDamage * BigBotDamageScale:F1} per shell, blast radius {baseRadius:F2} -> {baseRadius * BigBotRadiusScale:F2} m, cannons on blast projectile {set}, execution disabled={(execution ? "yes" : "no source")}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ fx prefabs
        static void BuildFx(string path, System.Action<GameObject> build, StringBuilder log)
        {
            bool exists = File.Exists(path);
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                foreach (var c in root.GetComponents<Component>()) if (!(c is Transform)) Object.DestroyImmediate(c);
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
            log.AppendLine("fx " + path);
        }

        static ParticleSystem Ps(GameObject root, string name, Material mat, System.Action<ParticleSystem> setup)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = false; main.playOnAwake = true; main.duration = 1; main.simulationSpace = ParticleSystemSimulationSpace.World; main.stopAction = ParticleSystemStopAction.None;
            var em = ps.emission; em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            setup(ps); return ps;
        }
        static void Burst(ParticleSystem ps, int min, int max) { var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0, (short)min, (short)max) }); }

        static void BlastFx(GameObject root, HangarMenuAuthoring.Mats m)
        {
            var audio = root.AddComponent<AudioSource>(); audio.clip = Clip("IR_BigBot_Blast"); audio.playOnAwake = true; audio.spatialBlend = 1; audio.minDistance = 5; audio.maxDistance = 70; audio.rolloffMode = AudioRolloffMode.Linear; audio.volume = 1; audio.dopplerLevel = 0;
            audio.outputAudioMixerGroup = HangarSectorAuthoring.Group("General");
            Ps(root, "Fireball", m.fxAdd, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .55f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f); main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .62f, .2f), new Color(1f, .35f, .08f)); main.maxParticles = 40; Burst(ps, 18, 26);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .6f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .6f, 1, 1.4f));
                var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white);
            });
            Ps(root, "Smoke", m.fxSmoke, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f); main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 2.2f); main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(.12f, .11f, .1f, .8f), new Color(.25f, .23f, .2f, .65f)); main.gravityModifier = -.08f; main.maxParticles = 30; Burst(ps, 14, 20);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .9f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .7f, 1, 1.8f));
                var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white, .9f);
            });
            Ps(root, "Shockwave", m.fxAdd, ps =>
            {
                var main = ps.main; main.startLifetime = .32f; main.startSpeed = 0; main.startSize = 1f; main.startColor = new Color(1f, .8f, .55f, .55f); main.maxParticles = 2; main.simulationSpace = ParticleSystemSimulationSpace.Local; Burst(ps, 1, 1);
                var sh = ps.shape; sh.enabled = false;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(10f, AnimationCurve.EaseInOut(0, .1f, 1, 1f));
                var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white);
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.alignment = ParticleSystemRenderSpace.Local; // flat on the hit surface (prefab z = normal)
            });
            Ps(root, "Sparks", m.fxSpark, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .9f); main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 15f); main.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .85f, .5f), new Color(1f, .55f, .2f)); main.gravityModifier = 1.6f; main.maxParticles = 90; Burst(ps, 60, 80);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .3f;
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .04f; r.lengthScale = 1.2f;
            });
            Ps(root, "Debris", m.fxDebris ? m.fxDebris : m.fxSpark, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f); main.startSize = new ParticleSystem.MinMaxCurve(.06f, .16f);
                main.startColor = new Color(.18f, .17f, .16f); main.gravityModifier = 2.2f; main.maxParticles = 30; Burst(ps, 16, 24);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 45; sh.radius = .3f;
                var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.dampen = .5f; col.bounce = .3f; col.quality = ParticleSystemCollisionQuality.Low;
            });
            Ps(root, "Scorch", m.fxSmoke, ps =>
            {
                var main = ps.main; main.startLifetime = 5.5f; main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(2.6f, 3.4f); main.startColor = new Color(0f, 0f, 0f, .75f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.maxParticles = 1; main.simulationSpace = ParticleSystemSimulationSpace.Local; Burst(ps, 1, 1);
                var sh = ps.shape; sh.enabled = false;
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.85f, .7f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.alignment = ParticleSystemRenderSpace.Local; r.sortingFudge = 10;
                ps.transform.localPosition = new Vector3(0, 0, .02f);
            });
        }

        static void FistFx(GameObject root, HangarMenuAuthoring.Mats m)
        {
            Ps(root, "Flash", m.fxAdd, ps =>
            {
                var main = ps.main; main.startLifetime = .09f; main.startSpeed = 0; main.startSize = 1.1f; main.startColor = new Color(.6f, 1f, 1f, .9f); main.maxParticles = 1; Burst(ps, 1, 1);
                var sh = ps.shape; sh.enabled = false;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white);
            });
            Ps(root, "Sparks", m.fxSpark, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .55f); main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f); main.startSize = new ParticleSystem.MinMaxCurve(.02f, .05f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .9f, .6f), new Color(.5f, .95f, 1f)); main.gravityModifier = 1.3f; main.maxParticles = 70; Burst(ps, 40, 60);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 55; sh.radius = .05f;
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .035f; r.lengthScale = 1.1f;
            });
            Ps(root, "Ring", m.fxAdd, ps =>
            {
                var main = ps.main; main.startLifetime = .18f; main.startSpeed = 0; main.startSize = .4f; main.startColor = new Color(.4f, .95f, 1f, .6f); main.maxParticles = 1; main.simulationSpace = ParticleSystemSimulationSpace.Local; Burst(ps, 1, 1);
                var sh = ps.shape; sh.enabled = false;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(3.2f, AnimationCurve.EaseInOut(0, .2f, 1, 1f));
                var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white);
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.alignment = ParticleSystemRenderSpace.Local;
            });
        }

        static void DeflectFx(GameObject root, HangarMenuAuthoring.Mats m)
        {
            Ps(root, "Sparks", m.fxSpark, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .35f); main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f); main.startSize = new ParticleSystem.MinMaxCurve(.015f, .035f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, 1f, 1f), new Color(1f, 1f, 1f)); main.gravityModifier = .8f; main.maxParticles = 40; Burst(ps, 22, 30);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 60; sh.radius = .04f;
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .03f; r.lengthScale = 1f;
            });
            Ps(root, "Flash", m.fxAdd, ps =>
            {
                var main = ps.main; main.startLifetime = .07f; main.startSpeed = 0; main.startSize = .5f; main.startColor = new Color(.5f, 1f, 1f, .8f); main.maxParticles = 1; Burst(ps, 1, 1);
                var sh = ps.shape; sh.enabled = false;
            });
        }

        // ------------------------------------------------------------------ procedural UI sprites
        static Color Ring(float u, float v) { float d = new Vector2(u - .5f, v - .5f).magnitude * 2f; float a = Mathf.Clamp01(1 - Mathf.Abs(d - .82f) / .1f); return new Color(1, 1, 1, a * a * (3 - 2 * a)); }
        static Color Edge(float u, float v) { float e = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)); float a = Mathf.Clamp01(1 - e / .16f); return new Color(1, 1, 1, a * a); }
        static void EnsureSprite(string path, System.Func<float, float, Color> pixel)
        {
            if (!File.Exists(path))
            {
                const int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) tex.SetPixel(x, y, pixel((x + .5f) / size, (y + .5f) / size));
                tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.textureType != TextureImporterType.Sprite || !ti.alphaIsTransparency || ti.mipmapEnabled)
            { ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        }

        // ------------------------------------------------------------------ campaign session
        internal static void ApplyToSession(Scene scene, Transform lobby, HangarMenuAuthoring.Mats m, StringBuilder log)
        {
            T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
            var weapons = All<PlayerWeaponsManager>().Single();
            var player = weapons.gameObject;
            var fist = AssetDatabase.LoadAssetAtPath<GameObject>(FistPath).GetComponent<WeaponController>();
            var cannon = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPath).GetComponent<WeaponController>();
            string before = string.Join(", ", weapons.StartingWeapons.Where(w => w).Select(w => w.name));
            // v5: no fist slot any more: the right hand strikes with F (and M1 while unarmed); the inventory starts empty
            weapons.StartingWeapons = new System.Collections.Generic.List<WeaponController>();
            EditorUtility.SetDirty(weapons);
            log.AppendLine("starting weapons: [" + before + "] -> [] (right-hand strike on F / M1 unarmed; cannon from the montage station; Weapon_IR_Fist and the shotgun kept as assets)");

            foreach (var t in player.GetComponentsInChildren<Transform>(true).Where(t => t && t.name.StartsWith("V4_")).ToArray()) if (t) Object.DestroyImmediate(t.gameObject);
            var melee = player.GetComponent<ProtagonistMelee>();
            var movement = player.GetComponent<AdvancedPlayerMovement>();
            var health = player.GetComponent<Health>();
            var controller = player.GetComponent<PlayerCharacterController>();
            var hands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ProtagonistHandsPresentation>(true)).FirstOrDefault();
            if (!hands) throw new System.Exception("ProtagonistHandsPresentation not found in " + scene.path);
            var general = HangarSectorAuthoring.Group("General");
            AudioSource Source(string name, bool loop)
            {
                var go = new GameObject(name); go.transform.SetParent(player.transform, false); go.transform.localPosition = new Vector3(0, 1.2f, 0);
                var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.loop = loop; a.spatialBlend = 0; a.outputAudioMixerGroup = general; return a;
            }
            // charged punch tuning + audio/fx
            var meleeAudio = Source("V4_MeleeAudio", false);
            var mso = new SerializedObject(melee);
            mso.FindProperty("sfx").objectReferenceValue = meleeAudio;
            mso.FindProperty("quickImpactClip").objectReferenceValue = Clip("IR_Fist_QuickImpact");
            mso.FindProperty("heavyImpactClip").objectReferenceValue = Clip("IR_Fist_HeavyImpact");
            mso.FindProperty("heavyImpactFx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(FistFxPath);
            mso.ApplyModifiedPropertiesWithoutUndo();
            // slide guard (window is AdvancedPlayerMovement.slideInvulnerability = .42 s; cooldown 1.5 s + release C unchanged)
            var mvso = new SerializedObject(movement); mvso.FindProperty("slideInvulnerability").floatValue = .42f; mvso.ApplyModifiedPropertiesWithoutUndo();
            var guard = Get<SlideGuardFeedback>(player);
            guard.Configure(movement, health, controller.PlayerCamera.transform, AssetDatabase.LoadAssetAtPath<GameObject>(DeflectFxPath), Source("V4_SlideAudio", false), Clip("IR_Slide_Guard"), Clip("IR_Slide_Deflect"));
            EditorUtility.SetDirty(guard);
            // fist charge presentation on the left hand (the punching rig)
            var hso = new SerializedObject(hands);
            var leftHand = hso.FindProperty("rightHand").objectReferenceValue as Transform; // v5: the right hand strikes
            var fxRoot = new GameObject("V4_FistCharge"); fxRoot.transform.SetParent(player.transform, false);
            var energy = Ps(fxRoot, "Energy", m.fxAdd, ps =>
            {
                var main = ps.main; main.loop = true; main.playOnAwake = false; main.duration = 1; main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .35f); main.startSpeed = new ParticleSystem.MinMaxCurve(-.5f, -.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(.012f, .035f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(.3f, .95f, 1f), new Color(.8f, 1f, 1f)); main.maxParticles = 80;
                var em = ps.emission; em.rateOverTime = 8;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .09f;
                var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white);
            });
            var burst = Ps(fxRoot, "FullBurst", m.fxAdd, ps =>
            {
                var main = ps.main; main.playOnAwake = false; main.startLifetime = new ParticleSystem.MinMaxCurve(.15f, .3f); main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(.02f, .05f); main.startColor = new Color(.7f, 1f, 1f); main.maxParticles = 40; Burst(ps, 26, 34);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .04f;
            });
            int fpsLayer = leftHand ? leftHand.gameObject.layer : player.layer;
            energy.gameObject.layer = burst.gameObject.layer = fpsLayer;
            var lightGo = new GameObject("Glow"); lightGo.transform.SetParent(fxRoot.transform, false);
            var glow = lightGo.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(.3f, .9f, 1f); glow.range = 2.2f; glow.intensity = 0; glow.shadows = LightShadows.None; glow.enabled = false;
            var hum = Source("V4_FistHum", true); hum.clip = Clip("IR_Fist_ChargeLoop");
            var charge = Get<FistChargePresentation>(player);
            charge.Configure(melee, leftHand, energy, burst, glow, hum, Clip("IR_Fist_ChargeFull"));
            EditorUtility.SetDirty(charge);

            // HUD: charge ring under the crosshair + cyan screen edge for the slide guard
            // the gameplay HUD canvas: the one holding the heat bar / crosshair (never a fade or menu canvas)
            Canvas canvas = null;
            var heatHud = All<WeaponHeatHud>().FirstOrDefault(); if (heatHud) canvas = heatHud.GetComponentInParent<Canvas>(true);
            if (!canvas) { var mech = All<MechanicHud>().FirstOrDefault(); if (mech) canvas = mech.GetComponentInParent<Canvas>(true); }
            if (!canvas) canvas = All<Canvas>().FirstOrDefault(c => c.renderMode != RenderMode.WorldSpace && c.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b && (b.GetType().Name == "CrosshairManager" || b.GetType().Name == "PlayerHealthBar")));
            if (canvas) canvas = canvas.rootCanvas;
            // remove the v4 HUD left on any other canvas by an earlier run
            foreach (var t in All<RectTransform>().Where(t => t && (t.name == "V4_SlideGuardEdge" || t.name == "V4_FistChargeHud")).ToArray()) if (t) Object.DestroyImmediate(t.gameObject);
            if (canvas)
            {
                foreach (var t in canvas.GetComponentsInChildren<Transform>(true).Where(t => t && t.name.StartsWith("V4_")).ToArray()) if (t) Object.DestroyImmediate(t.gameObject);
                var edgeGo = new GameObject("V4_SlideGuardEdge", typeof(RectTransform), typeof(Image)); edgeGo.transform.SetParent(canvas.transform, false); edgeGo.transform.SetAsFirstSibling();
                var er = (RectTransform)edgeGo.transform; er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one; er.offsetMin = er.offsetMax = Vector2.zero;
                var edge = edgeGo.GetComponent<Image>(); edge.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EdgeSprite); edge.raycastTarget = false; edge.color = new Color(.2f, .95f, 1f, 0); edge.enabled = false;
                var screen = edgeGo.AddComponent<SlideGuardScreenFx>(); screen.Configure(guard, edge);
                var ringGo = new GameObject("V4_FistChargeHud", typeof(RectTransform), typeof(CanvasGroup)); ringGo.transform.SetParent(canvas.transform, false);
                var rr = (RectTransform)ringGo.transform; rr.anchorMin = rr.anchorMax = new Vector2(.5f, .5f); rr.anchoredPosition = new Vector2(0, -64); rr.sizeDelta = new Vector2(56, 56);
                var group = ringGo.GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
                Image Img(string name, float alpha, bool filled)
                {
                    var g = new GameObject(name, typeof(RectTransform), typeof(Image)); g.transform.SetParent(ringGo.transform, false);
                    var r = (RectTransform)g.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                    var img = g.GetComponent<Image>(); img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSprite); img.raycastTarget = false; img.color = new Color(1, 1, 1, alpha);
                    if (filled) { img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Radial360; img.fillOrigin = (int)Image.Origin360.Top; img.fillClockwise = true; img.fillAmount = 0; }
                    return img;
                }
                Img("Track", .18f, false);
                var fill = Img("Fill", .9f, true);
                var full = Img("FullFlash", 0, false);
                var label = new GameObject("Label", typeof(RectTransform)); label.transform.SetParent(ringGo.transform, false);
                var lr = (RectTransform)label.transform; lr.anchorMin = lr.anchorMax = new Vector2(.5f, 0); lr.anchoredPosition = new Vector2(0, -12); lr.sizeDelta = new Vector2(160, 20);
                var tmp = label.AddComponent<TextMeshProUGUI>(); tmp.text = "CARGA"; tmp.fontSize = 13; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(.45f, .95f, 1f, .85f); tmp.raycastTarget = false;
                var chargeHud = ringGo.AddComponent<FistChargeHud>(); chargeHud.Configure(melee, group, fill, full);
                log.AppendLine("HUD: fist charge ring + slide guard edge on " + canvas.name);
            }
            else log.AppendLine("WARN HUD canvas not found (fist ring / slide edge skipped)");

            BuildMountStation(lobby, m, cannon, log);
        }

        const string StationPrefab = "Assets/IronRequiem/Prefabs/Levels/MountStation_C04.prefab";
        /// v5: the montage station is a prefab (gantry, two mechanical arms, cannon rack, wrist locks, steam, sparks,
        /// dock flash, light, audio) driven by WeaponMountStation. Rebuilt in place (GUID kept) and instanced in the lobby.
        static void BuildMountStation(Transform lobby, HangarMenuAuthoring.Mats m, WeaponController cannon, StringBuilder log)
        {
            foreach (var n in new[] { "V4_MountStation", "V5_MountStation" }) { var old = lobby.Find(n); if (old) Object.DestroyImmediate(old.gameObject); }
            var root = new GameObject("MountStation_C04");
            try
            {
                var rt = root.transform;
                GameObject B(string name, Transform parent, Vector3 p, Vector3 s, Material mat, Vector3? euler = null) => HangarMenuAuthoring.Box(name, parent, p, s, mat, euler, false);
                // gantry the player walks through: posts, lintel with status lamp, sign
                foreach (var x in new[] { -2.3f, 2.3f })
                {
                    var post = B("Post", rt, new Vector3(x, 1.7f, 0), new Vector3(.35f, 3.4f, .5f), m.plateDark); post.AddComponent<BoxCollider>();
                    B("PostHazard", rt, new Vector3(x - Mathf.Sign(x) * .19f, 1.0f, 0), new Vector3(.03f, 1.6f, .5f), m.hazard);
                }
                B("Lintel", rt, new Vector3(0, 3.55f, 0), new Vector3(5f, .35f, .55f), m.plateDark);
                B("ArmBeam", rt, new Vector3(0, 3.42f, .32f), new Vector3(4.2f, .22f, .3f), m.trim);
                var lamp = B("StatusLamp", rt, new Vector3(0, 3.3f, -.29f), new Vector3(1.4f, .12f, .05f), m.orangeGlow).GetComponent<Renderer>();
                var sign = new GameObject("Sign"); sign.transform.SetParent(rt, false); sign.transform.localPosition = new Vector3(0, 4.05f, -.3f);
                var tmp = sign.AddComponent<TextMeshPro>(); tmp.font = TMP_Settings.defaultFontAsset; tmp.text = "ESTACIÓN DE MONTAJE · C-04"; tmp.fontSize = 3.2f; tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(.45f, .95f, 1f); tmp.rectTransform.sizeDelta = new Vector2(6, .8f);
                // cannon rack on the left
                B("RackShelf", rt, new Vector3(-1.45f, 1.02f, .35f), new Vector3(.7f, .06f, .5f), m.trim);
                B("RackLeg", rt, new Vector3(-1.45f, .5f, .35f), new Vector3(.08f, 1f, .08f), m.plateDark);
                var rack = new GameObject("CannonRack").transform; rack.SetParent(rt, false); rack.localPosition = new Vector3(-1.45f, 1.16f, .35f); rack.localRotation = Quaternion.Euler(0, -90f, 0);
                // two mechanical arms hanging from the beam
                StationArmRig Arm(string name, float x, Vector3 rest)
                {
                    var a = new GameObject(name).transform; a.SetParent(rt, false); a.localPosition = new Vector3(x, 3.3f, .32f);
                    var yaw = new GameObject("Yaw").transform; yaw.SetParent(a, false);
                    B("Housing", yaw, new Vector3(0, .05f, 0), new Vector3(.32f, .22f, .32f), m.plateDark);
                    var upper = new GameObject("Upper").transform; upper.SetParent(yaw, false); upper.localPosition = new Vector3(0, -.12f, 0);
                    B("UpperBeam", upper, new Vector3(0, 0, .65f), new Vector3(.14f, .16f, 1.3f), m.trim);
                    B("UpperPiston", upper, new Vector3(.09f, .02f, .55f), new Vector3(.04f, .04f, .9f), m.plate);
                    var fore = new GameObject("Fore").transform; fore.SetParent(yaw, false);
                    B("ForeBeam", fore, new Vector3(0, 0, .6f), new Vector3(.11f, .12f, 1.2f), m.trim);
                    B("Elbow", fore, Vector3.zero, new Vector3(.18f, .18f, .18f), m.plateDark);
                    var grip = new GameObject("Gripper").transform; grip.SetParent(a, false); grip.position = rt.TransformPoint(rest);
                    B("Palm", grip, new Vector3(0, 0, -.03f), new Vector3(.12f, .08f, .06f), m.plateDark);
                    var jawA = B("JawA", grip, new Vector3(-.07f, 0, .04f), new Vector3(.02f, .06f, .09f), m.hazard).transform;
                    var jawB = B("JawB", grip, new Vector3(.07f, 0, .04f), new Vector3(.02f, .06f, .09f), m.hazard).transform;
                    var rig = a.gameObject.AddComponent<StationArmRig>(); rig.Configure(yaw, upper, fore, grip, 1.3f, 1.2f, jawA, jawB);
                    return rig;
                }
                var armA = Arm("ArmA_Hand", 1.55f, new Vector3(1.25f, 2.35f, .45f));
                var armB = Arm("ArmB_Cannon", -1.55f, new Vector3(-1.3f, 2.3f, .45f));
                // wrist locks (three clamps on a ring that turn before the hand comes off)
                var locks = new GameObject("WristLocks").transform; locks.SetParent(rt, false);
                for (int i = 0; i < 3; i++)
                {
                    var arm = new GameObject("Lock" + i).transform; arm.SetParent(locks, false); arm.localRotation = Quaternion.Euler(0, 0, i * 120f);
                    B("Clamp", arm, new Vector3(0, .055f, 0), new Vector3(.03f, .025f, .05f), m.hazard);
                }
                ParticleSystem Fx(string name, Material mat, System.Action<ParticleSystem> setup)
                {
                    var go = new GameObject(name); go.transform.SetParent(rt, false);
                    var ps = go.AddComponent<ParticleSystem>(); var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var em = ps.emission; em.rateOverTime = 0;
                    var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    setup(ps); return ps;
                }
                var steam = Fx("LockSteam", m.fxSmoke, ps =>
                {
                    var main = ps.main; main.duration = .6f; main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .8f); main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(.04f, .14f); main.startColor = new Color(.85f, .9f, .95f, .55f); main.maxParticles = 40;
                    var em = ps.emission; em.rateOverTime = 45;
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .04f;
                    var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.2f));
                    var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white);
                });
                var sparks = Fx("LockSparks", m.fxSpark, ps =>
                {
                    var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.15f, .45f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(.008f, .02f); main.gravityModifier = 1.4f; main.maxParticles = 50;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .85f, .5f), new Color(.5f, .95f, 1f));
                    var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0, 24, 32), new ParticleSystem.Burst(.18f, 10, 14) });
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .03f;
                    var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .03f; r.lengthScale = 1f;
                });
                var flash = Fx("DockFlash", m.fxAdd, ps =>
                {
                    var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .3f); main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(.01f, .04f); main.startColor = new Color(.45f, .95f, 1f); main.maxParticles = 60;
                    var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0, 40, 50) });
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .05f;
                });
                var lightGo = new GameObject("DockLight"); lightGo.transform.SetParent(rt, false);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(.35f, .9f, 1f); light.range = 1.6f; light.intensity = 0; light.shadows = LightShadows.None; light.enabled = false;
                var audioGo = new GameObject("MountAudio"); audioGo.transform.SetParent(rt, false); audioGo.transform.localPosition = new Vector3(0, 1.6f, 0);
                var audio = audioGo.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = .6f; audio.minDistance = 3; audio.maxDistance = 25; audio.outputAudioMixerGroup = HangarSectorAuthoring.Group("General");
                var trigger = root.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0, 1.5f, -.2f); trigger.size = new Vector3(4.2f, 3f, 1.6f);
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var station = root.AddComponent<WeaponMountStation>();
                AudioClip Hangar(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IronRequiem/Audio/SFX/HangarIntro/" + n + ".wav");
                station.Configure(cannon, armA, armB, rack, locks, lamp, steam, sparks, flash, light, audio,
                    Hangar("IR_Hangar_Hydraulic"), Hangar("IR_Hangar_Spark"), Hangar("IR_Hangar_Servo"), Clip("IR_Mount_Clamp"), Clip("IR_Mount_Lock"));
                Directory.CreateDirectory(Path.GetDirectoryName(StationPrefab));
                PrefabUtility.SaveAsPrefabAsset(root, StationPrefab);
            }
            finally { Object.DestroyImmediate(root); }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StationPrefab), lobby.gameObject.scene);
            instance.name = "V5_MountStation"; instance.transform.SetParent(lobby, true); instance.transform.SetPositionAndRotation(new Vector3(0, 0, -33.9f), Quaternion.identity);
            log.AppendLine("montage station prefab " + StationPrefab + " in the lobby at z=-33.9 (E to mount " + cannon.name + ", ~" + WeaponMountStation.Duration + " s, hold E/Space to skip)");
        }
    }
}
