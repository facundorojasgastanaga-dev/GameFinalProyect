using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026): prefab VARIANTS of the enemies (originals untouched). Visual-only changes:
    // HardSurface materials (wear, emissive panels, soft rim), damage-stage materials with the shader's damage
    // parameter, EnemyStateLights on the eye/status renderers, larger muzzle flash, projectile variants with an
    // emissive trail, denser hit sparks and a death burst variant with one brief pooled flash light.
    // Health, damage, AI, navigation, weapons stats and animation are not touched.
    public static class VisualOverhaulEnemies
    {
        public const string Folder = "Assets/IronRequiem/Prefabs/VisualOverhaul";
        static string MatFolder => VisualOverhaulAssets.Mats + "/Enemies";
        static string FxFolder => Folder + "/VFX";

        static bool KeepOriginal(Material m)
        {
            if (!m || !m.shader) return true;
            string s = m.shader.name, n = m.name.ToLowerInvariant();
            if (!s.StartsWith("Universal Render Pipeline/Lit") && !s.StartsWith("Universal Render Pipeline/Simple Lit")) return true; // particles, UI, sprites
            if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > .5f) return true;                       // glass / transparent
            return n.Contains("eye") || n.Contains("status") || n.Contains("glass") || n.Contains("indicator");
        }
        static bool IsLightRenderer(Renderer r) => r.sharedMaterials.Any(m => m && (m.name.ToLowerInvariant().Contains("eye") || m.name.ToLowerInvariant().Contains("status")));

        public static Material Convert(Material m, float damage = 0, string suffix = "") => KeepOriginal(m) && damage <= 0 ? m : VisualOverhaulAssets.ConvertLit(m, MatFolder, true, true, damage, suffix);

        static GameObject Variant(string source, string dest)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if (File.Exists(dest)) return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), preview);
                go.name = Path.GetFileNameWithoutExtension(dest);
                return PrefabUtility.SaveAsPrefabAsset(go, dest);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
        }

        // Trail colours per projectile family.
        static Material TrailMaterial(Color c, string name)
        {
            string path = VisualOverhaulAssets.Mats + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { AssetDatabase.CopyAsset("Assets/FPS/Art/Materials/VFX/GunCylinders_Trail.mat", path); m = AssetDatabase.LoadAssetAtPath<Material>(path); }
            m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); return m;
        }

        static GameObject ProjectileVariant(GameObject projectile, Color color, string family)
        {
            if (!projectile) return null;
            string src = AssetDatabase.GetAssetPath(projectile);
            string dest = FxFolder + "/" + projectile.name + "_VO.prefab";
            var v = Variant(src, dest); var root = PrefabUtility.LoadPrefabContents(dest);
            try
            {
                var trail = root.transform.Find("VO_Trail")?.GetComponent<TrailRenderer>();
                if (!trail) { var t = new GameObject("VO_Trail"); t.transform.SetParent(root.transform, false); trail = t.AddComponent<TrailRenderer>(); }
                trail.time = .1f; trail.minVertexDistance = .3f; trail.widthMultiplier = .09f; trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                trail.colorGradient = new Gradient { colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) } };
                trail.sharedMaterial = TrailMaterial(color, "VO_Trail_" + family); trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
                trail.numCapVertices = 0; trail.numCornerVertices = 0; trail.emitting = true; trail.autodestruct = false;
                NoProbes(root);
                PrefabUtility.SaveAsPrefabAsset(root, dest);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
        }

        // Short-lived effects (hundreds of projectiles per minute) must not sample light/reflection probes per object.
        static void NoProbes(GameObject root) { foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; } }

        static GameObject FlashVariant(GameObject flash)
        {
            string src = AssetDatabase.GetAssetPath(flash); string dest = FxFolder + "/" + flash.name + "_VO.prefab";
            Variant(src, dest); var root = PrefabUtility.LoadPrefabContents(dest);
            try { root.transform.localScale = Vector3.one * 1.7f; NoProbes(root); PrefabUtility.SaveAsPrefabAsset(root, dest); } finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
        }

        public static GameObject BurstVariant(GameObject burst)
        {
            if (!burst) return null;
            string dest = FxFolder + "/" + burst.name + "_VO.prefab"; Variant(AssetDatabase.GetAssetPath(burst), dest);
            var root = PrefabUtility.LoadPrefabContents(dest);
            try
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.startSizeMultiplier *= 1.35f; main.startSpeedMultiplier *= 1.15f; }
                var flash = root.transform.Find("VO_Flash")?.GetComponent<Light>();
                if (!flash) { var go = new GameObject("VO_Flash"); go.transform.SetParent(root.transform, false); flash = go.AddComponent<Light>(); }
                flash.type = LightType.Point; flash.range = 9; flash.color = new Color(1f, .62f, .3f); flash.intensity = 0; flash.shadows = LightShadows.None; flash.lightmapBakeType = LightmapBakeType.Realtime; flash.enabled = false;
                var bf = root.GetComponent<BurstFlashLight>() ?? root.AddComponent<BurstFlashLight>(); bf.Configure(flash); NoProbes(root);
                PrefabUtility.SaveAsPrefabAsset(root, dest);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
        }

        public static string MakeEnemy(string source)
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(FxFolder); Directory.CreateDirectory(MatFolder);
            var srcGo = AssetDatabase.LoadAssetAtPath<GameObject>(source); if (!srcGo) throw new Exception("Missing " + source);
            string dest = Folder + "/" + srcGo.name + "_VO.prefab"; Variant(source, dest);
            var log = new StringBuilder(srcGo.name + ": ");
            var root = PrefabUtility.LoadPrefabContents(dest);
            try
            {
                var map = new Dictionary<Material, Material>(); int swapped = 0;
                Material Map(Material m) { if (!m) return m; if (!map.TryGetValue(m, out var r)) map[m] = r = Convert(m); return r; }
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    var mats = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < mats.Length; i++) { var n = Map(mats[i]); if (n != mats[i]) { mats[i] = n; changed = true; } }
                    if (changed) { r.sharedMaterials = mats; swapped++; }
                }
                log.Append($"renderers converted {swapped}, materials {map.Count(kv => kv.Key != kv.Value)}; ");
                var ec = root.GetComponent<EnemyController>();
                if (ec && ec.BodyMaterial && map.TryGetValue(ec.BodyMaterial, out var body)) ec.BodyMaterial = body;
                var dmg = root.GetComponent<RobotDamageProgression>();
                if (dmg)
                {
                    var so = new SerializedObject(dmg); var arr = so.FindProperty("damagedMaterials");
                    for (int i = 0; i < arr.arraySize; i++) { var m = arr.GetArrayElementAtIndex(i).objectReferenceValue as Material; if (m) arr.GetArrayElementAtIndex(i).objectReferenceValue = VisualOverhaulAssets.ConvertLit(m, MatFolder, true, true, .35f + i * .3f, "_D" + (i + 1)); }
                    var inside = so.FindProperty("interiorMaterial"); if (inside.objectReferenceValue is Material im) inside.objectReferenceValue = Map(im);
                    var burst = so.FindProperty("deathBurstPrefab"); if (burst.objectReferenceValue is GameObject bp) burst.objectReferenceValue = BurstVariant(bp);
                    so.ApplyModifiedPropertiesWithoutUndo(); log.Append("damage stages HS (_Damage .35/.65/.95), burst variant; ");
                }
                var bigDamage = root.GetComponent<BigBotDestruction>();
                if (bigDamage)
                {
                    var so = new SerializedObject(bigDamage); var arr = so.FindProperty("stages");
                    for (int i = 0; i < arr.arraySize; i++) { var m = arr.GetArrayElementAtIndex(i).objectReferenceValue as Material; if (m) arr.GetArrayElementAtIndex(i).objectReferenceValue = VisualOverhaulAssets.ConvertLit(m, MatFolder, true, true, .35f + i * .3f, "_D" + (i + 1)); }
                    var inside = so.FindProperty("interior"); if (inside.objectReferenceValue is Material im) inside.objectReferenceValue = Map(im);
                    so.ApplyModifiedPropertiesWithoutUndo(); log.Append("Big Bot damage stages HS; ");
                }
                var pooled = root.GetComponent<PooledEnemyDeathBurst>();
                if (pooled) { var so = new SerializedObject(pooled); var p = so.FindProperty("effectPrefab"); if (p.objectReferenceValue is GameObject bp) { p.objectReferenceValue = BurstVariant(bp); so.ApplyModifiedPropertiesWithoutUndo(); log.Append("pooled burst variant; "); } }
                var hit = root.GetComponent<RobotHitFeedback>();
                if (hit)
                {
                    var so = new SerializedObject(hit); so.FindProperty("sparkCount").intValue = Math.Min(12, so.FindProperty("sparkCount").intValue * 2);
                    if (so.FindProperty("sparks").objectReferenceValue is ParticleSystem sp) { var main = sp.main; main.startSizeMultiplier *= 1.4f; main.startSpeedMultiplier *= 1.2f; main.maxParticles = Math.Max(main.maxParticles, 48); }
                    so.ApplyModifiedPropertiesWithoutUndo(); log.Append("hit sparks x2; ");
                }
                // Weapons: bigger muzzle flash, projectile trails.
                foreach (var w in root.GetComponentsInChildren<WeaponController>(true))
                {
                    if (w.MuzzleFlashPrefab)
                    {
                        if(srcGo.name.ToLowerInvariant().Contains("t07"))
                        {
                            // A deterministic world-size target prevents repeated builds multiplying the flare.
                            if(w.MuzzleFlashPrefab.transform.root==root.transform)w.MuzzleFlashPrefab.transform.localScale=Vector3.one*.22f;
                        }
                        else if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(w.MuzzleFlashPrefab)) || w.MuzzleFlashPrefab.transform.root == root.transform) w.MuzzleFlashPrefab.transform.localScale *= 1.7f; // in-prefab flash object
                        else w.MuzzleFlashPrefab = FlashVariant(w.MuzzleFlashPrefab);
                    }
                    if (w.ProjectilePrefab)
                    {
                        string n = srcGo.name.ToLowerInvariant();
                        var color = n.Contains("t07") ? new Color(3.2f, .25f, 2.6f) : n.Contains("bigbot") ? new Color(4f, 1.4f, .2f) : n.Contains("turret") ? new Color(3.6f, .6f, .15f) : new Color(3.6f, .3f, .2f);
                        var pv = ProjectileVariant(w.ProjectilePrefab.gameObject, color, n.Contains("t07") ? "Magenta" : n.Contains("bigbot") ? "Amber" : "Red");
                        if (pv) w.ProjectilePrefab = pv.GetComponent<ProjectileBase>();
                    }
                }
                // Status lights per AI state.
                var lights = new List<Renderer>();
                var ind = root.GetComponent<EnemyPowerIndicators>(); if (ind) lights.AddRange(ind.Indicators.Where(r => r));
                lights.AddRange(root.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && IsLightRenderer(r) && !lights.Contains(r)));
                if (ec)
                {
                    var sl = root.GetComponent<EnemyStateLights>() ?? root.AddComponent<EnemyStateLights>(); sl.Configure(lights.ToArray());
                    log.Append($"state lights on {lights.Count} renderers; ");
                }
                // Light probes for every renderer (they are dynamic): blend probes, no lightmaps.
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) { r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Simple; }
                PrefabUtility.SaveAsPrefabAsset(root, dest);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        public static string MakeAll(string list)
        {
            var sb = new StringBuilder();
            foreach (var p in list.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0)) sb.AppendLine(MakeEnemy(p));
            File.WriteAllText(VisualOverhaulDriver.Reports + "/enemies.txt", sb.ToString());
            return sb.ToString();
        }

        public static string VariantPath(string source) => Folder + "/" + Path.GetFileNameWithoutExtension(source) + "_VO.prefab";
    }
}
