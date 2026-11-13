using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Visual pass F7: fuel-cell tubes on the arm modules (audit + fit).
    public static class VisualPassTubes
    {
        static readonly string[] Prefabs =
        {
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab",
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab",
        };

        [MenuItem("Tools/Iron Requiem/Visual Pass/F7 Audit arm tubes")]
        static void Audit()
        {
            var sb = new StringBuilder("[F7 tubes]\n");
            foreach (var path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var w = root.GetComponent<WeaponController>(); var h = root.GetComponent<WeaponFuelCellHandler>();
                    var vis = root.GetComponent<ArmModuleVisual>();
                    sb.AppendLine($"== {root.name} weaponRoot={Path(w.WeaponRoot ? w.WeaponRoot.transform : null, root.transform)} module={Path(vis ? vis.ModuleRoot : null, root.transform)}");
                    if (vis && vis.ModuleRoot) sb.AppendLine($"   moduleBounds={Bounds(vis.ModuleRoot, root.transform)}");
                    if (h)
                    {
                        sb.AppendLine($"   used={V(h.FuelCellUsedPosition)} unused={V(h.FuelCellUnusedPosition)} simultaneous={h.SimultaneousFuelCellsUsage}");
                        foreach (var c in h.FuelCells)
                        {
                            if (!c) continue; var t = c.transform; var mf = c.GetComponent<MeshFilter>();
                            sb.AppendLine($"   cell {Path(t, root.transform)} lp={V(t.localPosition)} le={V(t.localEulerAngles)} ls={V(t.localScale)} mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name + " " + V(mf.sharedMesh.bounds.size) : "-")} kids={t.childCount} parentLossy={V(t.parent.lossyScale)}");
                            foreach (Transform k in t) sb.AppendLine($"      child {k.name} lp={V(k.localPosition)} ls={V(k.localScale)}");
                        }
                    }
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        sb.AppendLine($"   r {Path(r.transform, root.transform)} b={V(r.bounds.center - root.transform.position)} s={V(r.bounds.size)}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log(sb.ToString());
        }

        // The shotgun module reuses the template pistol block (4 primary tubes, barrels, orange caps) at the primary's
        // height, so the tubes stood ~7 cm above the slim shotgun body. Lower that block so only the tube caps show
        // flush on top of the body (they still pop up one per shot and press down on the bank reload).
        [MenuItem("Tools/Iron Requiem/Visual Pass/F7 Fit shotgun tubes")]
        static void FitShotgun()
        {
            const string path = "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab";
            var backup = "Tools/IronRequiem/Backups/Weapon_Shotgun_ArmModule_VO_preF7tubes.prefab.bak";
            if (!System.IO.File.Exists(backup)) System.IO.File.Copy(path, backup);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var vis = root.GetComponent<ArmModuleVisual>(); var h = root.GetComponent<WeaponFuelCellHandler>();
                var body = vis.ModuleRoot.Find("VO_Body").GetComponent<Renderer>();
                var block = vis.ModuleRoot.parent.Find("WeaponMesh_Pistol");
                float top = float.MinValue; foreach (var c in h.FuelCells) top = Mathf.Max(top, c.GetComponent<Renderer>().bounds.max.y);
                float drop = top - body.bounds.max.y - .015f;
                Debug.Log($"[F7 fit] tubesTop={top:0.###} bodyTop={body.bounds.max.y:0.###} drop={drop:0.###}");
                if (drop < .005f) { Debug.Log("[F7 fit] already fitted"); return; }
                block.position -= vis.ModuleRoot.parent.up * drop;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[F7 fit] WeaponMesh_Pistol lowered {drop:0.###} m -> lp={V(block.localPosition)}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static string Bounds(Transform t, Transform root)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) return "-";
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return $"c={V(b.center - root.position)} s={V(b.size)}";
        }
        static string Path(Transform t, Transform root)
        {
            if (!t) return "null"; var s = t.name;
            while (t.parent && t.parent != root) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
        static string V(Vector3 v) => $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";
    }
}
