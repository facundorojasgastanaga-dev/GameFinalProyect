using System.Linq;
using System.Text;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Task 3: stolen T-07 cannon charge effect spawns away from the muzzle. Audit + fix tools.
    public static class VisualPassStolenCharge
    {
        [MenuItem("Tools/Iron Requiem/Visual Pass/T3 Audit stolen T-07 charge")]
        static void Audit()
        {
            var sb = new StringBuilder("[T3 audit]\n");
            var t07 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab");
            foreach (var sc in t07.GetComponentsInChildren<StolenCannon>(true))
            {
                var so = new SerializedObject(sc); var it = so.GetIterator();
                while (it.NextVisible(true)) if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is GameObject g)
                        sb.AppendLine($"StolenCannon.{it.name} -> {AssetDatabase.GetAssetPath(g)}");
            }
            var paths = AssetDatabase.FindAssets("t:Prefab Weapon_T07").Select(AssetDatabase.GUIDToAssetPath);
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var pres = root.GetComponentInChildren<CannonChargePresentation>(true); if (!pres) continue;
                    var w = root.GetComponent<WeaponController>(); var fit = root.GetComponentInChildren<StolenShellFit>(true);
                    sb.AppendLine($"== {path} scale={root.transform.localScale}");
                    sb.AppendLine($"  muzzle={P(w.WeaponMuzzle, root)} wp={w.WeaponMuzzle.position.ToString("F3")} lossy={w.WeaponMuzzle.lossyScale.ToString("F3")}");
                    var so = new SerializedObject(pres);
                    foreach (var n in new[] { "effectRoot", "energyCore", "energyParticles", "outerFrame", "referenceVortex" })
                    {
                        var p = so.FindProperty(n); if (p == null) continue;
                        if (p.propertyType == SerializedPropertyType.Boolean) { sb.AppendLine($"  {n}={p.boolValue}"); continue; }
                        var o = p.objectReferenceValue; var tr = o is GameObject g ? g.transform : (o as Component)?.transform;
                        sb.AppendLine($"  {n}={P(tr, root)} wp={tr?.position.ToString("F3")} lossy={tr?.lossyScale.ToString("F3")}");
                    }
                    var ps = pres.EnergyParticles; if (ps) { var m = ps.main; sb.AppendLine($"  particles sim={m.simulationSpace} scaling={m.scalingMode} shape={ps.shape.shapeType}"); }
                    if (fit)
                    {
                        var b = new Bounds(); bool any = false;
                        foreach (var r in fit.GetComponentsInChildren<MeshRenderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                        sb.AppendLine($"  shell={P(fit.transform, root)} wp={fit.transform.position.ToString("F3")} bounds c={b.center.ToString("F3")} s={b.size.ToString("F3")}");
                    }
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r is not ParticleSystemRenderer).Take(12))
                        sb.AppendLine($"  r {P(r.transform, root)} c={r.bounds.center.ToString("F3")}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log(sb.ToString());
        }

[MenuItem("Tools/Iron Requiem/Visual Pass/T3 Fix stolen muzzle + charge anchor")]
        static void Fix()
        {
            var sb = new StringBuilder("[T3 fix]\n");
            System.IO.Directory.CreateDirectory("Tools/IronRequiem/Backups/T3");
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var fitA = asset.GetComponentInChildren<StolenShellFit>(true); var wA = asset.GetComponent<WeaponController>();
                if (!fitA || !wA || !wA.WeaponMuzzle || asset.GetComponent<StolenSawAttack>()) continue;
                var bak = "Tools/IronRequiem/Backups/T3/" + System.IO.Path.GetFileName(path) + ".bak";
                if (!System.IO.File.Exists(bak)) System.IO.File.Copy(path, bak);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var w = root.GetComponent<WeaponController>(); var fit = root.GetComponentInChildren<StolenShellFit>(true);
                    var a = root.GetComponent<StolenChargeAnchor>() ?? root.AddComponent<StolenChargeAnchor>();
                    a.Configure(fit.transform, w.WeaponMuzzle);
                    // charge particles must ride the muzzle, not stay in world space
                    var pres = root.GetComponent<CannonChargePresentation>();
                    if (pres && pres.EnergyParticles) { var m = pres.EnergyParticles.main; m.simulationSpace = ParticleSystemSimulationSpace.Local; }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    sb.AppendLine($"  anchored {path} muzzle={w.WeaponMuzzle.name} charge={(pres ? "yes" : "no")}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T3 Diag hold charge (play)")]
        static void HoldCharge()
        {
            foreach (var c in Object.FindObjectsByType<CannonChargeShot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var w = c.GetComponent<WeaponController>(); if (!w.IsWeaponActive) continue;
                if (!c.GetComponent<ChargeHoldProbe>()) c.gameObject.AddComponent<ChargeHoldProbe>();
                var a = c.GetComponent<StolenChargeAnchor>(); var fit = c.GetComponentInChildren<StolenShellFit>(true);
                Debug.Log($"[T3 hold] {c.name} charging={c.IsCharging} muzzle={w.WeaponMuzzle.position.ToString("F3")} shell={fit?.transform.position.ToString("F3")} anchor={(a ? "yes" : "no")} cam={Camera.main.transform.position.ToString("F3")}");
            }
        }

[MenuItem("Tools/Iron Requiem/Visual Pass/Diag maximize Game view")]
        static void MaxGame()
        {
            var t = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView"); var w = EditorWindow.GetWindow(t);
            w.maximized = true; Debug.Log("[diag] game view maximized " + w.position);
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T3 Diag charge state (play)")]
        static void ChargeState()
        {
            foreach (var c in Object.FindObjectsByType<CannonChargeShot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var w = c.GetComponent<WeaponController>(); var pres = c.GetComponent<CannonChargePresentation>(); var fit = c.GetComponentInChildren<StolenShellFit>(true);
                var cam = Camera.main; var fx = pres ? pres.EffectOrigin : null;
                Vector3 vp = fx ? cam.WorldToViewportPoint(fx.position) : Vector3.zero;
                var b = new Bounds(); bool any = false;
                foreach (var r in fit.GetComponentsInChildren<MeshRenderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                Debug.Log($"[T3 state] {c.name} active={w.IsWeaponActive} charging={c.IsCharging} frac={c.ChargeFraction:0.00} fxVisible={pres && pres.IsVisible} fx={fx?.position.ToString("F3")} muzzle={w.WeaponMuzzle.position.ToString("F3")} fxViewport={vp.ToString("F2")} shellBounds c={b.center.ToString("F3")} s={b.size.ToString("F3")} shellFwd={fit.transform.forward.ToString("F2")}");
            }
        }

        static string P(Transform t, GameObject root)
        {
            if (!t) return "null"; var s = t.name;
            while (t.parent && t.parent != root.transform) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
