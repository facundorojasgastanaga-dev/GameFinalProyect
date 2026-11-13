using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.Editor
{
    // Read-only inspection used to plan the visual overhaul. Opens scenes without saving them.
    public static class VisualOverhaulProbe
    {
        static string P(Transform t, Transform root) { var s = t.name; while (t.parent && t.parent != root) { t = t.parent; s = t.name + "/" + s; } return s; }
        static string Mats(Renderer r) => string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "(" + (m.shader ? m.shader.name : "?") + ")" : "null"));

        public static string Scene(string path)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("SCENE " + path + " lighting=" + (Lightmapping.lightingDataAsset ? AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset) : "none") + " fog=" + RenderSettings.fog + " ambient=" + RenderSettings.ambientMode + " " + RenderSettings.ambientLight + " sky=" + (RenderSettings.skybox ? RenderSettings.skybox.name : "none"));
            foreach (var root in scene.GetRootGameObjects())
            {
                var rs = root.GetComponentsInChildren<Renderer>(true); var b = rs.Length > 0 ? rs[0].bounds : new Bounds(root.transform.position, Vector3.zero); foreach (var r in rs) b.Encapsulate(r.bounds);
                sb.AppendLine("ROOT " + root.name + " active=" + root.activeSelf + " prefab=" + (PrefabUtility.IsPartOfPrefabInstance(root) ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)) : "-") + " renderers=" + rs.Length + " bounds=" + b.center.ToString("F1") + "/" + b.size.ToString("F1")
                    + " comps=" + string.Join(",", root.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing")));
            }
            var matCount = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials).Where(m => m).GroupBy(m => m).OrderByDescending(g => g.Count());
            foreach (var g in matCount) sb.AppendLine("MAT " + g.Key.name + " x" + g.Count() + " shader=" + g.Key.shader.name + " path=" + AssetDatabase.GetAssetPath(g.Key));
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine("LIGHT " + P(l.transform, null) + " " + l.type + " " + l.lightmapBakeType + " int=" + l.intensity + " range=" + l.range + " col=" + l.color + " shadows=" + l.shadows + " pos=" + l.transform.position.ToString("F1"));
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine("CAMERA " + P(c.transform, null) + " clear=" + c.clearFlags + " far=" + c.farClipPlane);
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine("VOLUME " + v.name + " global=" + v.isGlobal + " profile=" + (v.sharedProfile ? AssetDatabase.GetAssetPath(v.sharedProfile) + " [" + string.Join(",", v.sharedProfile.components.Select(x => x.GetType().Name + (x.active ? "" : "-off"))) + "]" : "none"));
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine("ENEMY " + P(e.transform, null) + " src=" + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(e.gameObject)) + " pos=" + e.transform.position.ToString("F1"));
            foreach (var w in Object.FindObjectsByType<PlayerWeaponsManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine("PLAYER " + P(w.transform, null) + " weapons=" + string.Join(",", w.StartingWeapons.Select(x => x ? AssetDatabase.GetAssetPath(x) : "null")) + " delay=" + w.WeaponSwitchDelay + " smooth=" + w.SmoothWeaponSwitch + " pos=" + w.transform.position.ToString("F1"));
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(m => m && m.GetType().Namespace == "IronRequiem").GroupBy(m => m.GetType().Name)) sb.AppendLine("IR " + mb.Key + " x" + mb.Count());
            var nav = UnityEngine.AI.NavMesh.CalculateTriangulation(); sb.AppendLine("NAVMESH verts=" + nav.vertices.Length);
            var navB = new Bounds(); if (nav.vertices.Length > 0) { navB = new Bounds(nav.vertices[0], Vector3.zero); foreach (var v in nav.vertices) navB.Encapsulate(v); }
            sb.AppendLine("NAVBOUNDS " + navB.center.ToString("F1") + "/" + navB.size.ToString("F1"));
            foreach (var s in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine("NAVSURFACE " + P(s.transform, null) + " data=" + (s.navMeshData ? AssetDatabase.GetAssetPath(s.navMeshData) : "none") + " collect=" + s.collectObjects + " geom=" + s.useGeometry + " agent=" + s.agentTypeID);
            Directory.CreateDirectory(VisualOverhaulDriver.Reports);
            File.WriteAllText(VisualOverhaulDriver.Reports + "/probe-" + Path.GetFileNameWithoutExtension(path) + ".txt", sb.ToString());
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            return "lines=" + sb.ToString().Split('\n').Length;
        }

        public static string Prefab(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); var sb = new StringBuilder();
            sb.AppendLine("PREFAB " + path + " comps=" + string.Join(",", go.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing")));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>(); var mf = t.GetComponent<MeshFilter>(); var smr = r as SkinnedMeshRenderer;
                var mesh = mf ? mf.sharedMesh : smr ? smr.sharedMesh : null;
                var comps = t.GetComponents<Component>().Where(c => c && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter)).Select(c => c.GetType().Name);
                sb.AppendLine("  " + P(t, go.transform) + " act=" + t.gameObject.activeSelf + " lp=" + t.localPosition.ToString("F3") + " le=" + t.localEulerAngles.ToString("F0") + " ls=" + t.localScale.ToString("F3")
                    + (r ? " R[" + Mats(r) + "] en=" + r.enabled + " wb=" + r.bounds.center.ToString("F3") + "/" + r.bounds.size.ToString("F3") : "") + (mesh ? " mesh=" + mesh.name + " v=" + mesh.vertexCount : "") + " " + string.Join(",", comps));
            }
            var w = go.GetComponent<WeaponController>();
            if (w) sb.AppendLine("WEAPON root=" + (w.WeaponRoot ? w.WeaponRoot.name : "null") + " muzzle=" + (w.WeaponMuzzle ? P(w.WeaponMuzzle, go.transform) : "null") + " flash=" + (w.MuzzleFlashPrefab ? AssetDatabase.GetAssetPath(w.MuzzleFlashPrefab) : "null") + " proj=" + (w.ProjectilePrefab ? AssetDatabase.GetAssetPath(w.ProjectilePrefab) : "null") + " delay=" + w.DelayBetweenShots + " heat=" + w.UsesHeatInsteadOfAmmo + " maxAmmo=" + w.MaxAmmo + " auto=" + w.AutomaticReload + " aimOffset=" + w.AimOffset);
            foreach (var cw in go.GetComponentsInChildren<WeaponController>(true).Where(x => x != w)) sb.AppendLine("CHILDWEAPON " + P(cw.transform, go.transform) + " flash=" + (cw.MuzzleFlashPrefab ? AssetDatabase.GetAssetPath(cw.MuzzleFlashPrefab) : "null") + " proj=" + (cw.ProjectilePrefab ? AssetDatabase.GetAssetPath(cw.ProjectilePrefab) : "null") + " delay=" + cw.DelayBetweenShots);
            var cells = go.GetComponent<WeaponFuelCellHandler>(); if (cells) sb.AppendLine("CELLS " + string.Join(",", cells.FuelCells.Select(c => c ? P(c.transform, go.transform) : "null")) + " used=" + cells.FuelCellUsedPosition + " unused=" + cells.FuelCellUnusedPosition);
            var ind = go.GetComponent<EnemyPowerIndicators>(); if (ind) sb.AppendLine("INDICATORS " + string.Join(",", ind.Indicators.Select(r => r ? P(r.transform, go.transform) + ":" + Mats(r) : "null")));
            var dmg = go.GetComponent<RobotDamageProgression>(); if (dmg) { var so = new SerializedObject(dmg); var arr = so.FindProperty("damagedMaterials"); var body = so.FindProperty("bodyRenderers"); sb.AppendLine("DAMAGE mats=" + string.Join(",", Enumerable.Range(0, arr.arraySize).Select(i => arr.GetArrayElementAtIndex(i).objectReferenceValue ? arr.GetArrayElementAtIndex(i).objectReferenceValue.name : "null")) + " body=" + body.arraySize + " interior=" + (so.FindProperty("interiorMaterial").objectReferenceValue ? so.FindProperty("interiorMaterial").objectReferenceValue.name : "null") + " burst=" + (so.FindProperty("deathBurstPrefab").objectReferenceValue ? AssetDatabase.GetAssetPath(so.FindProperty("deathBurstPrefab").objectReferenceValue) : "null")); }
            var ec = go.GetComponent<EnemyController>(); if (ec) { var so = new SerializedObject(ec); sb.AppendLine("ENEMYCTRL bodyMat=" + (so.FindProperty("BodyMaterial")?.objectReferenceValue ? so.FindProperty("BodyMaterial").objectReferenceValue.name : "null") + " charge=" + ec.EnemyChargeDuration + " deathVfx=" + (so.FindProperty("DeathVfx")?.objectReferenceValue ? AssetDatabase.GetAssetPath(so.FindProperty("DeathVfx").objectReferenceValue) : "null")); }
            var hit = go.GetComponent<RobotHitFeedback>(); if (hit) { var so = new SerializedObject(hit); sb.AppendLine("HITFEEDBACK sparks=" + (so.FindProperty("sparks").objectReferenceValue ? so.FindProperty("sparks").objectReferenceValue.name : "null") + " count=" + so.FindProperty("sparkCount").intValue + " body=" + so.FindProperty("body").arraySize); }
            Directory.CreateDirectory(VisualOverhaulDriver.Reports);
            File.WriteAllText(VisualOverhaulDriver.Reports + "/probe-" + Path.GetFileNameWithoutExtension(path) + ".txt", sb.ToString());
            return "lines=" + sb.ToString().Split('\n').Length;
        }

        public static string ShaderReport(string name)
        {
            var sh = Shader.Find(string.IsNullOrEmpty(name) ? VisualOverhaulAssets.ShaderName : name); if (!sh) return "shader not found";
            var sb = new StringBuilder(); sb.AppendLine(sh.name + " hasError=" + ShaderUtil.ShaderHasError(sh) + " passes=" + sh.passCount + " supported=" + sh.isSupported);
            foreach (var m in ShaderUtil.GetShaderMessages(sh)) sb.AppendLine(m.severity + " " + m.message + " line " + m.line + " " + m.messageDetails);
            var mi = typeof(ShaderUtil).GetMethod("GetVariantCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            foreach (var m2 in typeof(ShaderUtil).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Where(x => x.Name.Contains("VariantCount"))) { var ps = m2.GetParameters(); try { var args = ps.Select(pp => pp.ParameterType == typeof(Shader) ? (object)sh : pp.ParameterType == typeof(bool) ? (object)false : null).ToArray(); sb.AppendLine(m2.Name + "(" + string.Join(",", ps.Select(pp => pp.ParameterType.Name)) + ")=" + m2.Invoke(null, args)); } catch (System.Exception e) { sb.AppendLine(m2.Name + " err " + e.GetType().Name); } }
            var kw = sh.keywordSpace.keywords; sb.AppendLine("keywords=" + string.Join(",", kw.Select(k => k.name)));
            File.WriteAllText(VisualOverhaulDriver.Reports + "/shader-" + sh.name.Replace("/", "_") + ".txt", sb.ToString());
            return sb.ToString();
        }

        // Missing scripts and broken object references (a reference whose target no longer exists) in every Build
        // Settings scene and every prefab under Assets. Opens scenes without saving them.
        public static string MissingReferences()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup(); var sb = new StringBuilder(); int scenes = 0, prefabs = 0, broken = 0, missingScripts = 0;
            void Check(GameObject root, string where)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int ms = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject); if (ms > 0) { missingScripts += ms; sb.AppendLine("MISSING SCRIPT " + where + " :: " + P(t, null)); }
                    foreach (var c in t.GetComponents<Component>())
                    {
                        if (!c) continue; var so = new SerializedObject(c); var it = so.GetIterator();
                        while (it.Next(true))
                            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && !it.objectReferenceEntityIdValue.Equals(default(EntityId)))
                            { broken++; sb.AppendLine("BROKEN " + where + " :: " + P(t, null) + " " + c.GetType().Name + "." + it.propertyPath); }
                    }
                }
            }
            foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
            { var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single); scenes++; foreach (var r in scene.GetRootGameObjects()) Check(r, s.path); }
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            { var path = AssetDatabase.GUIDToAssetPath(guid); var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!go) continue; prefabs++; Check(go, path); }
            var head = "scenes " + scenes + ", prefabs " + prefabs + ", missing scripts " + missingScripts + ", broken references " + broken;
            File.WriteAllText(VisualOverhaulDriver.Reports + "/missing-references.txt", head + "\n" + sb);
            return head;
        }

        // ";"-separated list of scenes and prefabs.
        public static string Many(string list)
        {
            var sb = new StringBuilder();
            foreach (var p in list.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                try { sb.Append(p + ": " + (p.EndsWith(".unity") ? Scene(p) : Prefab(p)) + "\n"); }
                catch (System.Exception e) { sb.Append(p + ": ERROR " + e.Message + "\n"); }
            }
            return sb.ToString();
        }
    }
}
