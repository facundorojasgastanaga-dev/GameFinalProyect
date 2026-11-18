using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace IronRequiem.Editor
{
    /// Read-only guard for the protected menu zone (IntroMenu + the conveyor area of Room00_Hangar).
    /// "IronCampaignProtectedZone.Snapshot <label>" writes every protected file and every asset the two scenes depend on
    /// (recursive AssetDatabase dependencies) with its SHA-256 to Tools/IronRequiem/Reports/ProtectedZone/<label>.txt.
    /// "IronCampaignProtectedZone.Compare <labelA> <labelB>" lists the differences. Nothing in the project is modified.
    public static class IronCampaignProtectedZone
    {
        public const string Folder = "Tools/IronRequiem/Reports/ProtectedZone";
        static readonly string[] Scenes = { "Assets/FPS/Scenes/IntroMenu.unity", "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity" };
        static readonly string[] ProtectedRoots =
        {
            "Assets/FPS/Scenes/IntroMenu.unity",
            "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity",
            "Assets/IronRequiem/Scripts/Menu",
            "Assets/IronRequiem/Scripts/HangarIntro",
            "Assets/IronRequiem/Editor/Menu/HangarMenuAuthoring.cs",
            "Assets/IronRequiem/Editor/HangarIntro",
            "Assets/IronRequiem/Prefabs/Menu",
        };

        public static string Snapshot(string label)
        {
            label = string.IsNullOrWhiteSpace(label) ? "snapshot" : label.Trim();
            Directory.CreateDirectory(Folder);
            var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string root in ProtectedRoots)
            {
                if (File.Exists(root)) Add(rows, root, "protected");
                else if (Directory.Exists(root))
                    foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) Add(rows, f.Replace('\\', '/'), "protected");
            }
            var usedBy = new Dictionary<string, List<string>>();
            foreach (string scene in Scenes)
                foreach (string dep in AssetDatabase.GetDependencies(scene, true))
                {
                    if (!usedBy.TryGetValue(dep, out var list)) usedBy[dep] = list = new List<string>();
                    list.Add(Path.GetFileNameWithoutExtension(scene));
                }
            foreach (var kv in usedBy)
            {
                string tag = "shared:" + string.Join("+", kv.Value);
                Add(rows, kv.Key, tag);
            }
            var sb = new StringBuilder();
            sb.AppendLine("# path | sha256(file) | sha256(meta) | guid | type | tag");
            foreach (var kv in rows) sb.AppendLine(kv.Value);
            File.WriteAllText(Path.Combine(Folder, label + ".txt"), sb.ToString());
            return "rows=" + rows.Count + " shared=" + usedBy.Count + " -> " + Folder + "/" + label + ".txt";
        }

        static void Add(SortedDictionary<string, string> rows, string path, string tag)
        {
            if (path.EndsWith(".meta", StringComparison.Ordinal)) return;
            if (rows.TryGetValue(path, out string existing)) { rows[path] = existing + "," + tag; return; }
            string guid = AssetDatabase.AssetPathToGUID(path);
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            rows[path] = path + " | " + Hash(path) + " | " + Hash(path + ".meta") + " | " + guid + " | " + (type != null ? type.Name : "?") + " | " + tag;
        }

        static string Hash(string path)
        {
            if (!File.Exists(path)) return "-";
            using (var sha = SHA256.Create()) using (var s = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").Substring(0, 16);
        }

        public static string Compare(string args)
        {
            var parts = (args ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return "usage: Compare <labelA> <labelB>";
            var a = Load(parts[0]); var b = Load(parts[1]);
            var sb = new StringBuilder();
            int diff = 0;
            foreach (string k in a.Keys.Union(b.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                a.TryGetValue(k, out string va); b.TryGetValue(k, out string vb);
                if (va == vb) continue;
                diff++; sb.AppendLine("DIFF " + k + "\n  A: " + (va ?? "(absent)") + "\n  B: " + (vb ?? "(absent)"));
            }
            string text = "compare " + parts[0] + " vs " + parts[1] + ": differences=" + diff + "\n" + sb;
            File.WriteAllText(Path.Combine(Folder, "compare-" + parts[0] + "-" + parts[1] + ".txt"), text);
            return text;
        }

        static Dictionary<string, string> Load(string label)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(Path.Combine(Folder, label + ".txt")))
            {
                if (line.StartsWith("#")) continue;
                int i = line.IndexOf(" | ", StringComparison.Ordinal);
                if (i > 0) d[line.Substring(0, i)] = line;
            }
            return d;
        }
    }
}
