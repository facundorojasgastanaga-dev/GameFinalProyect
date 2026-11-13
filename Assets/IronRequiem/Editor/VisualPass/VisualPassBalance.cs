using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// Read-only audit for enemy balance (campaign prefabs) and the primary weapon's fuel cells.
    public static class VisualPassBalance
    {
        const string Menu = "Tools/Iron Requiem/Visual Pass/F7 ";
        const string Report = "Tools/IronRequiem/Reports/VisualPass/F7";
        static readonly string[] Enemies = {
            "Assets/IronRequiem/Prefabs/Campaign/HoverBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab" };

        [MenuItem(Menu + "Audit enemy balance + fuel cells (read only)")]
        static void Audit()
        {
            var sb = new StringBuilder();
            foreach (var path in Enemies)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!go) { sb.AppendLine("missing " + path); continue; }
                sb.AppendLine("== " + path);
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (!c) continue;
                    string type = c.GetType().Name;
                    if (c is UnityEngine.AI.NavMeshAgent a) { sb.AppendLine($"  [{Rel(go, c)}] NavMeshAgent speed={a.speed} accel={a.acceleration} angular={a.angularSpeed} stop={a.stoppingDistance}"); continue; }
                    if (!(c is MonoBehaviour)) continue;
                    if (!(type.Contains("Enemy") || type.Contains("Detection") || type.Contains("Weapon") || type.Contains("Attack") || type.Contains("Saw") || type.Contains("Cannon") || type.Contains("Burst") || type.Contains("Combat") || type.Contains("Charge") || type.Contains("Health") || type.Contains("Patrol") || type.Contains("Range") || type.Contains("Tuning"))) continue;
                    sb.AppendLine($"  [{Rel(go, c)}] {type}: {Fields(c)}");
                    if (type == "WeaponController")
                    {
                        var proj = new SerializedObject(c).FindProperty("ProjectilePrefab")?.objectReferenceValue as Component;
                        if (proj) foreach (var pc in proj.GetComponents<MonoBehaviour>()) sb.AppendLine($"      projectile {AssetDatabase.GetAssetPath(proj)} {pc.GetType().Name}: {Fields(pc)}");
                    }
                }
            }
            // Primary weapon fuel cells.
            var vo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab");
            if (vo)
            {
                sb.AppendLine("== VO primary");
                var wc = vo.GetComponent<Unity.FPS.Game.WeaponController>();
                sb.AppendLine("  WeaponRoot=" + (wc && wc.WeaponRoot ? Rel(vo, wc.WeaponRoot.transform) : "none"));
                foreach (var c in vo.GetComponentsInChildren<MonoBehaviour>(true))
                    if (c && (c.GetType().Name.Contains("Fuel") || c.GetType().Name.Contains("Heat") || c.GetType().Name.Contains("Capsule") || c.GetType().Name.Contains("Deploy")))
                        sb.AppendLine($"  [{Rel(vo, c)}] {c.GetType().Name}: {Fields(c)}");
                foreach (var t in vo.GetComponentsInChildren<Transform>(true))
                    if (t.name.ToLower().Contains("cell") || t.name.ToLower().Contains("tube") || t.name.ToLower().Contains("capsule"))
                        sb.AppendLine($"  T {Rel(vo, t)} lp={t.localPosition} ls={t.localScale} underRoot={(wc && wc.WeaponRoot && t.IsChildOf(wc.WeaponRoot.transform))}");
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f7-audit.txt", sb.ToString()); Debug.Log("[F7] audit written");
        }


        [MenuItem(Menu + "Apply enemy balance (drones, T07 sniper, SawBot)")]
        static void Apply()
        {
            var sb = new StringBuilder();
            const string backup = "Tools/IronRequiem/Backups/20261110-balance";
            const string outDir = "Assets/IronRequiem/Prefabs/VisualPass/Balance";
            Directory.CreateDirectory(backup); Directory.CreateDirectory(outDir);
            foreach (var p in Enemies) if (File.Exists(p)) File.Copy(p, System.IO.Path.Combine(backup, System.IO.Path.GetFileName(p) + ".bak"), true);

            GameObject CopyProjectile(GameObject src, string name, float damage, float speed, float? minDmg = null, float? maxDmg = null)
            {
                string dst = outDir + "/" + name + ".prefab";
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(dst)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(src), dst);
                var root = PrefabUtility.LoadPrefabContents(dst);
                foreach (var c in root.GetComponents<MonoBehaviour>())
                {
                    var so = new SerializedObject(c);
                    Set(so, "Damage", damage); Set(so, "Speed", speed);
                    if (minDmg.HasValue) Set(so, "minimumDamage", minDmg.Value);
                    if (maxDmg.HasValue) Set(so, "maximumDamage", maxDmg.Value);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, dst); PrefabUtility.UnloadPrefabContents(root);
                sb.AppendLine($"  projectile copy {dst}: damage={damage} speed={speed}");
                return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
            }

            // Drones: half damage, slower fire, slower bolt.
            Edit(Enemies[0], root =>
            {
                foreach (var w in root.GetComponentsInChildren<Unity.FPS.Game.WeaponController>(true))
                {
                    var so = new SerializedObject(w);
                    var proj = so.FindProperty("ProjectilePrefab");
                    var src = proj.objectReferenceValue as Component;
                    if (src) proj.objectReferenceValue = CopyProjectile(src.gameObject, "EnemyProjectile_Drone", 4, 70).GetComponent(src.GetType());
                    Set(so, "DelayBetweenShots", 1.5f); so.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine($"  drone weapon {w.name}: delay 0.8->1.5");
                }
            }, sb);

            // T-07: sniper. More damage, long range, fast bolt; keeps its charge telegraph.
            Edit(Enemies[1], root =>
            {
                foreach (var w in root.GetComponentsInChildren<Unity.FPS.Game.WeaponController>(true))
                {
                    var so = new SerializedObject(w); var proj = so.FindProperty("ProjectilePrefab");
                    var src = proj.objectReferenceValue as Component;
                    if (src) proj.objectReferenceValue = CopyProjectile(src.gameObject, "T07_SniperProjectile", 22, 90, 18, 26).GetComponent(src.GetType());
                    so.ApplyModifiedPropertiesWithoutUndo(); sb.AppendLine($"  T07 weapon {w.name}: sniper projectile");
                }
                foreach (var d in root.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m && m.GetType().Name == "DetectionModule"))
                {
                    var so = new SerializedObject(d); Set(so, "DetectionRange", 45); Set(so, "AttackRange", 38); Set(so, "KnownTargetTimeout", 6); so.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine("  T07 detection 16->45, attack 12.5->38, timeout 2->6");
                }
            }, sb);

            // Saw Bot: faster chase and quicker swings.
            Edit(Enemies[2], root =>
            {
                foreach (var a in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) { a.speed = 9.5f; a.acceleration = 34; }
                foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m && m.GetType().Name == "SawBotCombat"))
                {
                    var so = new SerializedObject(c); Set(so, "moveSpeed", 9.5f); Set(so, "acceleration", 34); Set(so, "preparation", .2f); Set(so, "recovery", .55f); so.ApplyModifiedPropertiesWithoutUndo();
                }
                sb.AppendLine("  SawBot speed 7.2->9.5, accel 24->34, preparation .25->.2, recovery .75->.55");
            }, sb);

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f7-apply.txt", sb.ToString()); Debug.Log("[F7 balance]\n" + sb);
        }

        static void Edit(string path, System.Action<GameObject> change, StringBuilder sb)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { change(root); PrefabUtility.SaveAsPrefabAsset(root, path); sb.AppendLine("saved " + path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void Set(SerializedObject so, string name, float value) { var p = so.FindProperty(name); if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = value; }


        [MenuItem(Menu + "Weapon swap in place (no slide from below)")]
        static void SwapInPlace()
        {
            var sb = new StringBuilder();
            foreach (var mgr in Object.FindObjectsByType<Unity.FPS.Gameplay.PlayerWeaponsManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(mgr);
                var def = so.FindProperty("DefaultWeaponPosition"); var down = so.FindProperty("DownWeaponPosition"); var delay = so.FindProperty("WeaponSwitchDelay");
                sb.AppendLine($"{mgr.name}: down {(down.objectReferenceValue ? down.objectReferenceValue.name : "null")} -> {(def.objectReferenceValue ? def.objectReferenceValue.name : "null")}, delay {delay.floatValue} -> 0.12");
                down.objectReferenceValue = def.objectReferenceValue; delay.floatValue = .12f;
                so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(mgr);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty(); UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f7-swap.txt", sb.ToString()); Debug.Log("[F7 swap]\n" + sb);
        }

        static string Fields(Object c)
        {
            var so = new SerializedObject(c); var it = so.GetIterator(); var sb = new StringBuilder(); bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script") continue;
                switch (it.propertyType)
                {
                    case SerializedPropertyType.Float: sb.Append($"{it.name}={it.floatValue:0.###} "); break;
                    case SerializedPropertyType.Integer: sb.Append($"{it.name}={it.intValue} "); break;
                    case SerializedPropertyType.Boolean: sb.Append($"{it.name}={it.boolValue} "); break;
                    case SerializedPropertyType.Vector2: sb.Append($"{it.name}={it.vector2Value} "); break;
                    case SerializedPropertyType.ObjectReference: if (it.objectReferenceValue) sb.Append($"{it.name}->{it.objectReferenceValue.name} "); break;
                }
            }
            return sb.ToString();
        }
        static string Rel(GameObject root, Component c) => Rel(root, c.transform);
        static string Rel(GameObject root, Transform t) { var s = t.name; while (t.parent && t != root.transform) { t = t.parent; s = t.name + "/" + s; } return s; }
    }
}
