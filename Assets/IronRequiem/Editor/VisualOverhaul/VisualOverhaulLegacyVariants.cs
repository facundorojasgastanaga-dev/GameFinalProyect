using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    /// 23/11: in the Editor a prefab variant loads every asset its base prefabs reference, even values the variant
    /// overrides. Carronero / SawBot_Current derive from four SawBot generations (v01..v04) and dragged ~67 MB of text
    /// meshes into each load (17 s hangar activation). This tool finds, for every base prefab used ONLY as a variant
    /// base by live content (campaign, Map01, menu), the meshes and materials that no live final prefab uses, and
    /// clears them in the base. GUIDs, fileIDs and hierarchy are kept; every live final prefab must stay identical.
    /// Audit: read only. Apply: backs up the bases, clears, verifies (restores from backup on any difference).
    public static class VisualOverhaulLegacyVariants
    {
        const string Report = "Tools/IronRequiem/Reports/VisualOverhaul/legacy-variants";
        static string Backup => "Tools/IronRequiem/Backups/20261123-legacy-variants";
        static readonly string[] LivePrefixes =
        {
            "Assets/FPS/Scenes/IntroMenu.unity", "Assets/IronRequiem/Scenes/RequiemCampaign.unity", "Assets/IronRequiem/Scenes/Campaign/Room0",
            "Assets/IronRequiem/Scenes/Campaign/Pursuit05.unity", "Assets/IronRequiem/Scenes/Map01_Bunker/"
        };
        static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        sealed class Plan
        {
            public readonly List<string> LiveFiles = new List<string>(), Finals = new List<string>();
            public readonly Dictionary<string, string> BaseOf = new Dictionary<string, string>();
            public readonly Dictionary<string, HashSet<UnityEngine.Object>> Strip = new Dictionary<string, HashSet<UnityEngine.Object>>();
            public readonly StringBuilder Log = new StringBuilder();
        }

        static List<string> LiveScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
            .Where(p => LivePrefixes.Any(p.StartsWith) && !p.Contains("MechTest") && !p.Contains("_v2") && File.Exists(p)).Distinct().ToList();

        static Plan Build()
        {
            var plan = new Plan();
            var scenes = LiveScenes();
            plan.LiveFiles.AddRange(scenes);
            plan.LiveFiles.AddRange(AssetDatabase.GetDependencies(scenes.ToArray(), true).Where(p => p.EndsWith(".prefab")).Distinct());
            foreach (var p in plan.LiveFiles.Where(p => p.EndsWith(".prefab")))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant)
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (src) plan.BaseOf[p] = AssetDatabase.GetAssetPath(src);
                }
            }
            // a prefab is "used directly" when a live file (scene, prefab, ScriptableObject …) references it other than as
            // its own variant base
            var direct = new HashSet<string>();
            var scan = plan.LiveFiles.Concat(AssetDatabase.GetDependencies(scenes.ToArray(), true).Where(p => p.EndsWith(".asset") && File.Exists(p) && new FileInfo(p).Length < 4 * 1048576)).Distinct();
            foreach (var f in scan)
            {
                plan.BaseOf.TryGetValue(f, out var baseOfF);
                foreach (Match m in GuidRx.Matches(File.ReadAllText(f)))
                {
                    var path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                    if (path.EndsWith(".prefab") && path != baseOfF) direct.Add(path);
                }
            }
            foreach (var s in scenes) direct.Add(s);
            var bases = new HashSet<string>(plan.BaseOf.Values);
            // never a base that code can load by name (Resources) or that a live file uses directly
            var strippable = bases.Where(b => b.EndsWith(".prefab") && !direct.Contains(b) && !b.Contains("/Resources/")).ToList(); // model (FBX) bases cannot be rewritten
            plan.Finals.AddRange(plan.LiveFiles.Where(p => p.EndsWith(".prefab") && !strippable.Contains(p)));
            foreach (var b in strippable)
            {
                var needed = new HashSet<UnityEngine.Object>();
                foreach (var d in plan.Finals.Where(d => Chain(plan, d).Contains(b)))
                    foreach (var o in Used(AssetDatabase.LoadAssetAtPath<GameObject>(d))) needed.Add(o);
                var inBase = Used(AssetDatabase.LoadAssetAtPath<GameObject>(b));
                var strip = new HashSet<UnityEngine.Object>(inBase.Where(o => !needed.Contains(o)));
                if (strip.Count > 0) plan.Strip[b] = strip;
            }
            return plan;
        }
        static List<string> Chain(Plan plan, string p) { var c = new List<string>(); while (plan.BaseOf.TryGetValue(p, out var b) && !c.Contains(b)) { c.Add(b); p = b; } return c; }
        static IEnumerable<UnityEngine.Object> Used(GameObject root)
        {
            if (!root) yield break;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) yield return mf.sharedMesh;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer s && s.sharedMesh) yield return s.sharedMesh;
                foreach (var m in r.sharedMaterials) if (m) yield return m;
            }
        }
        static long Size(UnityEngine.Object o)
        {
            var p = AssetDatabase.GetAssetPath(o); if (string.IsNullOrEmpty(p) || !File.Exists(p)) return 0;
            if (o is Material) return new FileInfo(p).Length + AssetDatabase.GetDependencies(p, false).Where(d => d != p && File.Exists(d)).Sum(d => new FileInfo(d).Length);
            return p.EndsWith(".asset") ? new FileInfo(p).Length : 0; // FBX sub-meshes: the FBX is shared, counted elsewhere
        }

        /// Measurement helper: drop every asset the open scenes do not use, so the next Play loads cold.
        public static string UnloadUnused() { EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect(); return "unloaded"; }

        /// Measurement: cold load time of every dependency of a scene (leaves first), to see where a load goes.
        public static string ProfileLoad(string scenePath)
        {
            EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect();
            int Order(string p) { var e = Path.GetExtension(p).ToLowerInvariant(); return e == ".png" || e == ".jpg" || e == ".tga" || e == ".exr" || e == ".psd" || e == ".wav" || e == ".ogg" || e == ".mp3" ? 0 : e == ".fbx" || e == ".obj" || e == ".asset" ? 1 : e == ".shader" || e == ".shadergraph" || e == ".mat" ? 2 : e == ".prefab" ? 4 : 3; }
            var deps = AssetDatabase.GetDependencies(scenePath, true).Where(p => p != scenePath && !p.EndsWith(".cs") && !p.EndsWith(".unity")).OrderBy(Order).ToList();
            var times = new List<(double ms, string path)>(); var total = System.Diagnostics.Stopwatch.StartNew();
            foreach (var p in deps) { var w = System.Diagnostics.Stopwatch.StartNew(); AssetDatabase.LoadAllAssetsAtPath(p); times.Add((w.Elapsed.TotalMilliseconds, p)); }
            var byExt = times.GroupBy(t => Path.GetExtension(t.path).ToLowerInvariant()).Select(g => $"{g.Key} {g.Sum(t => t.ms) / 1000:0.0}s/{g.Count()}").ToArray();
            var sb = new StringBuilder($"{scenePath}: {deps.Count} deps cold {total.Elapsed.TotalSeconds:0.0}s | {string.Join(" ", byExt)}\n");
            foreach (var t in times.OrderByDescending(t => t.ms).Take(25)) sb.AppendLine($"  {t.ms,8:0} ms  {t.path}");
            Directory.CreateDirectory(Report); File.AppendAllText(Report + "/load-profile.txt", sb.ToString());
            EditorUtility.UnloadUnusedAssetsImmediate();
            return sb.ToString();
        }

        public static string Audit()
        {
            var plan = Build(); Directory.CreateDirectory(Report);
            var sb = new StringBuilder($"{DateTime.Now:s} live scenes={LiveScenes().Count} live files={plan.LiveFiles.Count} variants={plan.BaseOf.Count} finals={plan.Finals.Count}\n");
            long total = 0;
            foreach (var kv in plan.Strip.OrderByDescending(k => k.Value.Sum(Size)))
            {
                long s = kv.Value.Sum(Size); total += s;
                sb.AppendLine($"{s / 1048576f:0.0} MB  {kv.Key}  ({kv.Value.Count} refs no live final uses)");
                foreach (var o in kv.Value.OrderByDescending(Size).Take(8)) sb.AppendLine($"     {Size(o) / 1048576f:0.0} MB {o.GetType().Name} {AssetDatabase.GetAssetPath(o)}");
            }
            sb.AppendLine($"TOTAL removable from Editor loads: {total / 1048576f:0.0} MB");
            File.WriteAllText(Report + "/audit.txt", sb.ToString());
            return sb.ToString();
        }

        /// Snapshot of what each live final prefab renders: object path / component / slot → asset path.
        static Dictionary<string, string> Snapshot(IEnumerable<string> finals)
        {
            var d = new Dictionary<string, string>();
            foreach (var f in finals)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(f); if (!root) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string path = f + "|" + AnimationUtility.CalculateTransformPath(r.transform, root.transform) + "|" + r.GetType().Name;
                    if (r is SkinnedMeshRenderer s) d[path + "|mesh"] = AssetDatabase.GetAssetPath(s.sharedMesh) + "#" + (s.sharedMesh ? s.sharedMesh.name : "");
                    var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) d[path + "|mat" + i] = mats[i] ? AssetDatabase.GetAssetPath(mats[i]) + "#" + mats[i].name : "null";
                }
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    d[f + "|" + AnimationUtility.CalculateTransformPath(mf.transform, root.transform) + "|MeshFilter"] = mf.sharedMesh ? AssetDatabase.GetAssetPath(mf.sharedMesh) + "#" + mf.sharedMesh.name : "null";
            }
            return d;
        }

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var plan = Build(); if (plan.Strip.Count == 0) return "nothing to strip";
            var before = Snapshot(plan.Finals);
            Directory.CreateDirectory(Backup);
            foreach (var b in plan.Strip.Keys) { File.Copy(b, Path.Combine(Backup, Path.GetFileName(b)), true); File.Copy(b + ".meta", Path.Combine(Backup, Path.GetFileName(b) + ".meta"), true); }
            var log = new StringBuilder();
            foreach (var kv in plan.Strip)
            {
                var contents = PrefabUtility.LoadPrefabContents(kv.Key); int cleared = 0;
                try
                {
                    foreach (var mf in contents.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh && kv.Value.Contains(mf.sharedMesh)) { mf.sharedMesh = null; cleared++; }
                    foreach (var r in contents.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r is SkinnedMeshRenderer s && s.sharedMesh && kv.Value.Contains(s.sharedMesh)) { s.sharedMesh = null; cleared++; }
                        var mats = r.sharedMaterials; bool changed = false;
                        for (int i = 0; i < mats.Length; i++) if (mats[i] && kv.Value.Contains(mats[i])) { mats[i] = null; changed = true; cleared++; }
                        if (changed) r.sharedMaterials = mats;
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, kv.Key);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                log.AppendLine($"{kv.Key}: cleared {cleared} references");
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            var after = Snapshot(plan.Finals);
            var diffs = before.Where(kv => !after.TryGetValue(kv.Key, out var v) || v != kv.Value).Select(kv => kv.Key + " : " + kv.Value + " -> " + (after.TryGetValue(kv.Key, out var v2) ? v2 : "missing")).ToList();
            if (diffs.Count > 0)
            {
                foreach (var b in plan.Strip.Keys) { File.Copy(Path.Combine(Backup, Path.GetFileName(b)), b, true); }
                AssetDatabase.Refresh();
                File.WriteAllText(Report + "/apply.txt", "RESTORED (live finals changed):\n" + string.Join("\n", diffs.Take(60)));
                return "restored: " + diffs.Count + " differences in live finals (see apply.txt)";
            }
            log.AppendLine($"verified: {before.Count} renderer slots of {plan.Finals.Count} live final prefabs identical");
            File.WriteAllText(Report + "/apply.txt", log.ToString());
            return log.ToString();
        }
    }
}
