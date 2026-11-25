using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    // 24/11/2026, user request: the mount station model showed two arms too many. The two back arms (SA_BR_Arm, SA_BL_Arm,
    // parked, they never move) are removed from MountStation_C04.prefab; the scenes only hold prefab instances and inherit it.
    // Queue entry for the VisualOverhaulDriver request file. Idempotent. MountStationArtBuilder no longer builds them.
    public static class VisualOverhaulStation
    {
        const string Prefab = "Assets/IronRequiem/Prefabs/Levels/MountStation_C04.prefab";

        public static string RemoveParkedArms()
        {
            var log = new StringBuilder("VisualOverhaulStation.RemoveParkedArms\n");
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                int removed = 0;
                foreach (var name in new[] { "SA_BR_Arm", "SA_BL_Arm" })
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t && t.name == name) { log.AppendLine("  removed " + Path(t)); Object.DestroyImmediate(t.gameObject); removed++; break; }
                if (removed > 0) PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                log.AppendLine(removed > 0 ? $"  saved ({removed} removed)" : "  nothing to remove");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return log.ToString();
        }

        static string Path(Transform t) { var p = t.name; while (t.parent) { t = t.parent; p = t.name + "/" + p; } return p; }
    }
}
