using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    // 17/11/2026 (MAP_FX_AUDIT_20261117 E1): campaign death explosion. VisualOverhaulEnemies.BurstVariant compounded its
    // x1.35 size / x1.15 speed on every rebuild and chained _VO_VO_VO variants; the campaign enemies ended on
    // RobotBodyBurst_CombatPolish_VO_VO_VO (~x36.6 size vs the original: 220 m flare, 110 m mesh ring, 7 m opaque
    // untextured debris quads at 107 m/s = the orange screen and the black squares).
    // Apply builds <original>_IR.prefab: a variant of the ORIGINAL burst with x1.35 size and x1.15 speed applied once
    // (absolute, from the original values), the same VO_Flash light + BurstFlashLight as the _VO tool, and size caps on
    // the systems that draw solid quads/meshes. Then it retargets only Prefabs/Campaign/* fields that point to a _VO chain.
    // Existing _VO* assets are neither modified nor deleted. Idempotent.
    // Queue: "IronCampaignMapFxDeathFx.Analyze" / "IronCampaignMapFxDeathFx.Apply [nullMechDeathVfx] [unifyPool]".
    public static class IronCampaignMapFxDeathFx
    {
        public const string CampaignFolder = "Assets/IronRequiem/Prefabs/Campaign";
        public const string FxFolder = "Assets/IronRequiem/Prefabs/VisualOverhaul/VFX";
        public const string OriginalBurst = "Assets/IronRequiem/Prefabs/VFX/RobotBodyBurst_CombatPolish.prefab";
        public const string FixedBurst = FxFolder + "/RobotBodyBurst_CombatPolish_IR.prefab";
        const float SizeScale = 1.35f, SpeedScale = 1.15f;
        // Safety caps (metres, largest start size) for systems that draw solid shapes. With the single x1.35 they do not
        // bite (debris .27, cube fragments .039, mesh ring 4.05); they only stop a future compounding from filling the view.
        const float OpaqueQuadCap = .3f, CubeMeshCap = .08f, OtherMeshCap = 4.5f;

        public static readonly string[] Enemies =
        {
            CampaignFolder + "/BigBot_Current.prefab", CampaignFolder + "/Carronero.prefab", CampaignFolder + "/HoverBot_Current.prefab",
            CampaignFolder + "/SawBot_Current.prefab", CampaignFolder + "/T07_Current_Repaired.prefab", CampaignFolder + "/T07_Mech.prefab",
            CampaignFolder + "/Turret_Current.prefab",
        };
        // Every serialized field that hands a burst prefab to RobotDestructionPool (first Prepare in a scene wins the pool).
        static readonly (Type type, string field)[] BurstFields =
        {
            (typeof(PooledEnemyDeathBurst), "effectPrefab"), (typeof(RobotDamageProgression), "deathBurstPrefab"),
            (typeof(BigBotDestruction), "sharedPoolTemplate"), (typeof(SawBotCombat), "deathEffect"),
        };

        // ---------- Analyze (read only) ----------
        public static string Analyze()
        {
            var sb = new StringBuilder("IronCampaignMapFxDeathFx.Analyze\n");
            var bursts = new List<GameObject>();
            foreach (var path in Enemies)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) { sb.AppendLine("== " + path + " MISSING"); continue; }
                var baseGo = PrefabUtility.GetCorrespondingObjectFromSource(go);
                sb.AppendLine("== " + path + (baseGo ? " (variant of " + AssetDatabase.GetAssetPath(baseGo) + ")" : ""));
                var presentation = go.GetComponent<Unity.FPS.Game.IEnemyDeathPresentation>();
                sb.AppendLine("  first IEnemyDeathPresentation: " + (presentation != null ? presentation.GetType().Name : "none (template DeathVfx spawns)"));
                var ec = go.GetComponent<EnemyController>();
                if (ec) sb.AppendLine("  EnemyController.DeathVfx -> " + Describe(ec.DeathVfx) + (presentation != null ? "  [not spawned: presentation returns true]" : ""));
                if (go.GetComponent<MechDeathExplosion>()) sb.AppendLine("  MechDeathExplosion (own debris pieces + sparks, no prefab)");
                foreach (var (type, field) in BurstFields)
                    foreach (var c in go.GetComponentsInChildren(type, true))
                    {
                        var so = new SerializedObject(c); var p = so.FindProperty(field); if (p == null) continue;
                        var fx = p.objectReferenceValue as GameObject;
                        string extra = type == typeof(PooledEnemyDeathBurst) ? " size=" + so.FindProperty("size").floatValue
                            : type == typeof(BigBotDestruction) ? " enabled=" + so.FindProperty("deathBurstEnabled").boolValue + " scale=" + so.FindProperty("deathBurstScale").floatValue
                            : type == typeof(RobotDamageProgression) ? " preserveLegacyDeathEffect=" + so.FindProperty("preserveLegacyDeathEffect").boolValue : "";
                        sb.AppendLine($"  {type.Name}.{field} -> {Describe(fx)}{extra}");
                        if (fx && !bursts.Contains(fx)) bursts.Add(fx);
                    }
            }
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalBurst);
            if (original && !bursts.Contains(original)) bursts.Insert(0, original);
            foreach (var chain in AssetDatabase.FindAssets("RobotBodyBurst_CombatPolish t:Prefab", new[] { FxFolder }).Select(id => AssetDatabase.GUIDToAssetPath(id)).OrderBy(s => s.Length))
            { var g = AssetDatabase.LoadAssetAtPath<GameObject>(chain); if (g && !bursts.Contains(g)) bursts.Add(g); }
            foreach (var fx in bursts) DescribeBurst(sb, fx);
            Write("deathfx-analyze.txt", sb.ToString());
            return sb.ToString();
        }

        static string Describe(GameObject g) => g ? AssetDatabase.GetAssetPath(g) : "null";

        static void DescribeBurst(StringBuilder sb, GameObject fx)
        {
            var original = Original(fx);
            sb.AppendLine($"-- {AssetDatabase.GetAssetPath(fx)} (original {AssetDatabase.GetAssetPath(original)}) flash={(fx.GetComponent<BurstFlashLight>() ? "yes" : "no")}");
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main; var r = ps.GetComponent<ParticleSystemRenderer>();
                var o = Match(original, fx, ps);
                string ratio = o && o.main.startSizeMultiplier > 0 ? " x" + (main.startSizeMultiplier / o.main.startSizeMultiplier).ToString("0.###") : "";
                var mat = r ? r.sharedMaterial : null;
                sb.AppendLine($"   {Rel(ps.transform, fx.transform),-28} size {Curve(main.startSize)}{ratio} speed {Curve(main.startSpeed)} max={main.maxParticles} scaling={main.scalingMode}"
                    + $" | {(r ? r.renderMode.ToString() : "-")} mesh={(r && r.renderMode == ParticleSystemRenderMode.Mesh && r.mesh ? r.mesh.name : "-")} mat={(mat ? mat.name + (Opaque(mat) ? "(opaque)" : "") : "null")} maxScreen={(r ? r.maxParticleSize : 0)}");
            }
        }

        // ---------- Apply ----------
        // options (space separated, optional): "nullMechDeathVfx" sets T07_Mech EnemyController.DeathVfx to null (see risk note
        // in the report: EnemyController.OnDie instantiates it when the presentation returns false). "unifyPool" also retargets
        // campaign fields that point to the untouched original burst, so every campaign enemy feeds the scene pool the same prefab.
        public static string Apply(string options = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "refused: Play Mode";
            var opts = (options ?? "").Split(new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            bool nullMech = opts.Any(o => o.Equals("nullMechDeathVfx", StringComparison.OrdinalIgnoreCase));
            bool unify = opts.Any(o => o.Equals("unifyPool", StringComparison.OrdinalIgnoreCase));
            var sb = new StringBuilder("IronCampaignMapFxDeathFx.Apply" + (opts.Length > 0 ? " " + string.Join(" ", opts) : "") + "\n");
            var fixedFor = new Dictionary<GameObject, GameObject>();

            foreach (var path in Enemies)
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) { sb.AppendLine("== " + path + " MISSING (skipped)"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path); bool changed = false;
                try
                {
                    foreach (var (type, field) in BurstFields)
                        foreach (var c in root.GetComponentsInChildren(type, true))
                        {
                            var so = new SerializedObject(c); var p = so.FindProperty(field); if (p == null) continue;
                            var fx = p.objectReferenceValue as GameObject; if (!fx) continue;
                            bool chain = IsVoChain(fx), originalRef = unify && AssetDatabase.GetAssetPath(fx) == OriginalBurst;
                            if (!chain && !originalRef) continue;
                            var original = Original(fx);
                            if (!fixedFor.TryGetValue(original, out var target)) fixedFor[original] = target = BuildFixed(original, sb);
                            if (!target || p.objectReferenceValue == target) continue;
                            sb.AppendLine($"  {path} : {type.Name}.{field} : {Describe(fx)} -> {Describe(target)}");
                            p.objectReferenceValue = target; so.ApplyModifiedPropertiesWithoutUndo(); changed = true;
                        }
                    if (nullMech && Path.GetFileNameWithoutExtension(path) == "T07_Mech")
                    {
                        var ec = root.GetComponent<EnemyController>(); var dmg = root.GetComponent<RobotDamageProgression>();
                        var dso = dmg ? new SerializedObject(dmg) : null;
                        bool otherBurst = dso != null && dso.FindProperty("deathBurstPrefab").objectReferenceValue && !dso.FindProperty("preserveLegacyDeathEffect").boolValue && dmg.enabled;
                        if (ec && ec.DeathVfx && otherBurst)
                        {
                            sb.AppendLine($"  {path} : EnemyController.DeathVfx : {Describe(ec.DeathVfx)} -> null (asset kept)");
                            var eso = new SerializedObject(ec); eso.FindProperty("DeathVfx").objectReferenceValue = null; eso.ApplyModifiedPropertiesWithoutUndo(); changed = true;
                        }
                        else if (ec && ec.DeathVfx) sb.AppendLine($"  {path} : EnemyController.DeathVfx kept (no other enabled death burst)");
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                    else sb.AppendLine("  " + path + " unchanged");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Write("deathfx-apply.txt", sb.ToString());
            return sb.ToString();
        }

        // A burst produced by VisualOverhaulEnemies (any _VO depth) living in the VisualOverhaul VFX folder.
        static bool IsVoChain(GameObject fx) => fx.name.EndsWith("_VO", StringComparison.Ordinal) && AssetDatabase.GetAssetPath(fx).StartsWith(FxFolder + "/", StringComparison.Ordinal);

        // Walks _VO (and _IR) variants back to the burst they were made from (RobotBodyBurst_CombatPolish, T07_BodyExplosion...).
        static GameObject Original(GameObject fx)
        {
            while (fx && (fx.name.EndsWith("_VO", StringComparison.Ordinal) || fx.name.EndsWith("_IR", StringComparison.Ordinal)) && PrefabUtility.GetCorrespondingObjectFromSource(fx) is GameObject b) fx = b;
            return fx;
        }

        static GameObject BuildFixed(GameObject original, StringBuilder sb)
        {
            string src = AssetDatabase.GetAssetPath(original), dest = FxFolder + "/" + original.name + "_IR.prefab";
            if (!original.GetComponent<RobotBodyBurst>()) { sb.AppendLine("  " + src + " has no RobotBodyBurst; not retargeted"); return null; }
            bool created = !File.Exists(dest);
            if (created)
            {
                var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                try
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(original, preview);
                    go.name = Path.GetFileNameWithoutExtension(dest);
                    PrefabUtility.SaveAsPrefabAsset(go, dest);
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            }
            sb.AppendLine((created ? "  created " : "  exists (values reapplied) ") + dest + " = variant of " + src + $" size x{SizeScale} speed x{SpeedScale} once");
            var root = PrefabUtility.LoadPrefabContents(dest);
            try
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var o = PrefabUtility.GetCorrespondingObjectFromSource(ps); if (!o) continue;
                    var main = ps.main; float before = main.startSizeMultiplier;
                    main.startSizeMultiplier = o.main.startSizeMultiplier * SizeScale; main.startSpeedMultiplier = o.main.startSpeedMultiplier * SpeedScale;
                    var r = ps.GetComponent<ParticleSystemRenderer>(); float cap = Cap(r);
                    string capped = "";
                    if (main.startSizeMultiplier > cap) { capped = $" capped {main.startSizeMultiplier:0.###}->{cap}"; main.startSizeMultiplier = cap; }
                    if (main.startSize.mode == ParticleSystemCurveMode.TwoConstants && main.startSize.constantMin > cap)
                    { var c = main.startSize; c.constantMin = cap; main.startSize = c; capped += " min capped"; }
                    sb.AppendLine($"    {Rel(ps.transform, root.transform),-28} size {Curve(o.main.startSize)} -> {Curve(main.startSize)} (was {before:0.###}) speed -> {Curve(main.startSpeed)}{(cap < float.MaxValue ? " cap " + cap : "")}{capped}");
                }
                var flash = root.transform.Find("VO_Flash")?.GetComponent<Light>();
                if (!flash) { var go = new GameObject("VO_Flash"); go.transform.SetParent(root.transform, false); flash = go.AddComponent<Light>(); }
                flash.type = LightType.Point; flash.range = 9; flash.color = new Color(1f, .62f, .3f); flash.intensity = 0; flash.shadows = LightShadows.None; flash.lightmapBakeType = LightmapBakeType.Realtime; flash.enabled = false;
                var bf = root.GetComponent<BurstFlashLight>(); if (!bf) bf = root.AddComponent<BurstFlashLight>(); bf.Configure(flash);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; }
                PrefabUtility.SaveAsPrefabAsset(root, dest);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
        }

        // Solid shapes only: opaque untextured billboards (TrailDeath debris = the black squares) and mesh particles.
        static float Cap(ParticleSystemRenderer r)
        {
            if (!r) return float.MaxValue;
            if (r.renderMode == ParticleSystemRenderMode.Mesh) return r.mesh && r.mesh.name == "Cube" ? CubeMeshCap : OtherMeshCap;
            return Opaque(r.sharedMaterial) ? OpaqueQuadCap : float.MaxValue;
        }

        static bool Opaque(Material m) => m && m.HasProperty("_Surface") && m.GetFloat("_Surface") < .5f;

        static ParticleSystem Match(GameObject original, GameObject fx, ParticleSystem ps)
        {
            if (!original) return null;
            string rel = Rel(ps.transform, fx.transform);
            var t = rel == "." ? original.transform : original.transform.Find(rel);
            return t ? t.GetComponent<ParticleSystem>() : null;
        }

        static string Rel(Transform t, Transform root) => t == root ? "." : AnimationUtility.CalculateTransformPath(t, root);

        static string Curve(ParticleSystem.MinMaxCurve c)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant: return c.constant.ToString("0.###");
                case ParticleSystemCurveMode.TwoConstants: return c.constantMin.ToString("0.###") + ".." + c.constantMax.ToString("0.###");
                default: return c.mode + " x" + c.curveMultiplier.ToString("0.###");
            }
        }

        static void Write(string file, string text)
        {
            try { Directory.CreateDirectory(VisualOverhaulDriver.Reports); File.WriteAllText(VisualOverhaulDriver.Reports + "/" + file, text); }
            catch (Exception e) { Debug.LogWarning("[MapFx E1] report not written: " + e.Message); }
        }
    }
}
