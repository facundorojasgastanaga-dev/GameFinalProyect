using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    // Visual pass F3 (09/11/2026): combat VFX. A0 is a read-only audit of every VFX reference reachable from the
    // campaign weapons and enemies (serialized fields whose name mentions vfx/flash/impact/projectile/explosion/trail).
    public static class VisualPassF3
    {
        public static readonly string[] Roots =
        {
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab",
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/HoverBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/Turret_Current.prefab",
        };
        static readonly string[] Keys = { "vfx", "flash", "impact", "projectile", "explosion", "trail", "spark", "smoke", "muzzle", "burst" };

        [MenuItem("Tools/Iron Requiem/Visual Pass/F3 A0 - Audit combat VFX (read only)")]
        public static void Audit()
        {
            var sb = new StringBuilder(); var seen = new System.Collections.Generic.HashSet<string>();
            var queue = new System.Collections.Generic.Queue<(string path, int depth)>(Roots.Select(r => (r, 0)));
            while (queue.Count > 0)
            {
                var (path, depth) = queue.Dequeue(); if (!seen.Add(path) || depth > 2) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!go) continue;
                sb.AppendLine(new string(' ', depth * 2) + "PREFAB " + path);
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (!c) continue;
                    if (c is ParticleSystemRenderer pr) sb.AppendLine(new string(' ', depth * 2 + 2) + "PSR " + c.name + " mat=" + (pr.sharedMaterial ? pr.sharedMaterial.name : "-") + " maxSize=" + pr.maxParticleSize + " mode=" + pr.renderMode);
                    if (c is TrailRenderer tr) sb.AppendLine(new string(' ', depth * 2 + 2) + "TRAIL " + c.name + " time=" + tr.time + " width=" + tr.widthMultiplier);
                    if (c is Light l) sb.AppendLine(new string(' ', depth * 2 + 2) + "LIGHT " + c.name + " " + l.type + " i=" + l.intensity + " enabled=" + l.enabled);
                    if (!(c is MonoBehaviour)) continue;
                    var so = new SerializedObject(c); var it = so.GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || !it.objectReferenceValue) continue;
                        string n = it.name.ToLowerInvariant(); if (!Keys.Any(n.Contains)) continue;
                        var target = it.objectReferenceValue; string tp = AssetDatabase.GetAssetPath(target);
                        sb.AppendLine(new string(' ', depth * 2 + 2) + c.GetType().Name + "." + it.propertyPath + " (" + c.name + ") -> " + target.name + (string.IsNullOrEmpty(tp) ? " [internal]" : " " + tp));
                        if (tp.EndsWith(".prefab")) queue.Enqueue((tp, depth + 1));
                    }
                }
            }
            Directory.CreateDirectory(VisualPassF1.Report);
            File.WriteAllText(VisualPassF1.Report + "/f3-a0-vfx-audit.txt", sb.ToString());
            Debug.Log("IRON REQUIEM VisualPass F3 A0 audit written (" + seen.Count + " prefabs)");
        }

        const string Out = "Assets/IronRequiem/Prefabs/VisualPass/VFX";
        const string MatOut = "Assets/IronRequiem/Art/VisualPass/Materials";
        const string Backup = "Tools/IronRequiem/Backups/20261109-visualpass-f3";
        static readonly Color HotWhite = new Color(4f, 2.6f, 1.2f, 1), Orange = new Color(3.2f, 1.1f, .18f, 1), Ember = new Color(1.6f, .28f, .05f, 1);

        // V1: player shots follow the palette rule (player = orange energy). The VO weapons used the template's green
        // muzzle flash + green laser sparks (primary) and blue sparks (shotgun). Copies are recoloured, their screen
        // size is capped, and only the two campaign VO weapons are re-pointed. Templates stay untouched.
        [MenuItem("Tools/Iron Requiem/Visual Pass/F3 V1 - Player shots orange (muzzle/impact/bolt)")]
        public static void PlayerShots()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Out); Directory.CreateDirectory(Backup); Directory.CreateDirectory(VisualPassF1.Report);
            var log = new StringBuilder();
            string primaryWeapon = Roots[0], shotgunWeapon = Roots[1];
            foreach (var w in new[] { primaryWeapon, shotgunWeapon }) foreach (var f in new[] { w, w + ".meta" }) File.Copy(f, Path.Combine(Backup, Path.GetFileName(f)), true);

            var muzzleP = Recolor("Assets/FPS/Prefabs/VFX/VFX_MuzzleFlashGreen.prefab", Out + "/VP_Muzzle_Primary.prefab", .12f, 1f, log);
            var impactP = Recolor("Assets/FPS/Prefabs/VFX/VFX_LazerSparksGreen.prefab", Out + "/VP_Impact_Primary.prefab", .05f, 1f, log);
            var muzzleS = Recolor("Assets/IronRequiem/Prefabs/SecondaryShotgun/MuzzleFlash_Shotgun_Secondary.prefab", Out + "/VP_Muzzle_Shotgun.prefab", .16f, 1.25f, log);
            var impactS = Recolor("Assets/FPS/Prefabs/VFX/VFX_LazerSparksBlue.prefab", Out + "/VP_Impact_Shotgun.prefab", .045f, .8f, log);
            var boltP = Bolt("Assets/IronRequiem/Prefabs/Weapons/Projectile_IR_Integrated_v05.prefab", Out + "/VP_Bolt_Primary.prefab", impactP, log);
            var boltS = Bolt("Assets/IronRequiem/Prefabs/SecondaryShotgun/Projectile_Shotgun_Secondary.prefab", Out + "/VP_Bolt_Shotgun.prefab", impactS, log);

            void Repoint(string weapon, GameObject muzzle, GameObject bolt)
            {
                var root = PrefabUtility.LoadPrefabContents(weapon);
                try
                {
                    foreach (var wc in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c && c.GetType().Name == "WeaponController"))
                    {
                        var so = new SerializedObject(wc);
                        var m = so.FindProperty("MuzzleFlashPrefab"); var p = so.FindProperty("ProjectilePrefab");
                        log.AppendLine("WEAPON " + weapon + " muzzle " + (m.objectReferenceValue ? m.objectReferenceValue.name : "-") + " -> " + muzzle.name + " | projectile " + (p.objectReferenceValue ? p.objectReferenceValue.name : "-") + " -> " + bolt.name);
                        m.objectReferenceValue = muzzle;
                        p.objectReferenceValue = bolt.GetComponent(p.objectReferenceValue ? p.objectReferenceValue.GetType() : typeof(Transform)) ?? (Object)bolt;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, weapon);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Repoint(primaryWeapon, muzzleP, boltP);
            Repoint(shotgunWeapon, muzzleS, boltS);
            AssetDatabase.SaveAssets();
            File.WriteAllText(VisualPassF1.Report + "/f3-v1-player-shots.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass F3 V1:\n" + log);
        }

        static Material SparkMat()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_HitSparks.prefab");
            return src ? src.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(x => x.sharedMaterial).FirstOrDefault(m => m) : null;
        }

        static Gradient Fire(Gradient source)
        {
            var g = new Gradient();
            var alphas = source != null && source.alphaKeys.Length > 0 ? source.alphaKeys : new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) };
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, .55f, .15f), .35f), new GradientColorKey(new Color(.9f, .2f, .05f), 1) }, alphas);
            return g;
        }

        static GameObject Recolor(string src, string dst, float maxScreen, float sizeScale, StringBuilder log)
        {
            if (!File.Exists(dst)) AssetDatabase.CopyAsset(src, dst);
            var root = PrefabUtility.LoadPrefabContents(dst);
            try
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main; var sc = main.startColor;
                    float a = sc.mode == ParticleSystemGradientMode.Color ? sc.color.a : 1f;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(HotWhite.r, HotWhite.g, HotWhite.b, a), new Color(Orange.r, Orange.g, Orange.b, a));
                    if (sizeScale != 1f) main.startSizeMultiplier *= sizeScale;
                    var col = ps.colorOverLifetime; if (col.enabled) col.color = new ParticleSystem.MinMaxGradient(Fire(col.color.gradient));
                    var r = ps.GetComponent<ParticleSystemRenderer>(); if (r) { r.maxParticleSize = Mathf.Min(r.maxParticleSize, maxScreen); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        if (r.sharedMaterial && (r.sharedMaterial.name.Contains("Lazer") || r.sharedMaterial.name.Contains("Green") || r.sharedMaterial.name.Contains("Blue")) && SparkMat()) { log.AppendLine("  material " + r.sharedMaterial.name + " -> Spark (" + ps.name + ")"); r.sharedMaterial = SparkMat(); } }
                }
                foreach (var l in root.GetComponentsInChildren<Light>(true)) { l.color = new Color(1f, .55f, .2f); }
                PrefabUtility.SaveAsPrefabAsset(root, dst);
                log.AppendLine("VFX " + dst + " <- " + src + " maxScreen<=" + maxScreen + " size x" + sizeScale);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
        }

        static GameObject Bolt(string src, string dst, GameObject impact, StringBuilder log)
        {
            if (!File.Exists(dst)) AssetDatabase.CopyAsset(src, dst);
            var root = PrefabUtility.LoadPrefabContents(dst);
            try
            {
                foreach (var ps in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c && c.GetType().Name == "ProjectileStandard"))
                { var so = new SerializedObject(ps); var p = so.FindProperty("ImpactVfx"); if (p != null) { p.objectReferenceValue = impact; so.ApplyModifiedPropertiesWithoutUndo(); } }
                foreach (var t in root.GetComponentsInChildren<TrailRenderer>(true)) { t.colorGradient = Fire(t.colorGradient); }
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.startColor = new ParticleSystem.MinMaxGradient(HotWhite, Orange); var r = ps.GetComponent<ParticleSystemRenderer>(); if (r) r.maxParticleSize = Mathf.Min(r.maxParticleSize, .06f); }
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = mr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (!mats[i]) continue; string mp = MatOut + "/VP_Bolt_" + mats[i].name.Replace(" ", "_") + ".mat";
                        var m = AssetDatabase.LoadAssetAtPath<Material>(mp); if (!m) { m = new Material(mats[i]); AssetDatabase.CreateAsset(m, mp); }
                        foreach (var prop in new[] { "_BaseColor", "_Color", "_TintColor" }) if (m.HasProperty(prop)) m.SetColor(prop, Orange);
                        if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Orange); m.EnableKeyword("_EMISSION"); }
                        EditorUtility.SetDirty(m); mats[i] = m;
                    }
                    mr.sharedMaterials = mats;
                }
                foreach (var l in root.GetComponentsInChildren<Light>(true)) log.AppendLine("  NOTE light inside bolt " + l.name + " enabled=" + l.enabled);
                PrefabUtility.SaveAsPrefabAsset(root, dst);
                log.AppendLine("BOLT " + dst + " <- " + src + " impact=" + impact.name);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
        }
    }
}
