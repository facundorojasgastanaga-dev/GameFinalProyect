using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem.EditorTools
{
    /// F3 rest: extraction arc, cannon heat glow/steam, OVERDRIVE screen edge, module explosion debris.
    public static class VisualPassF3b
    {
        const string Menu = "Tools/Iron Requiem/Visual Pass/F3 ";
        const string Report = "Tools/IronRequiem/Reports/VisualPass/F3";
        const string Backup = "Tools/IronRequiem/Backups/20261109-visualpass-f3b";
        const string Out = "Assets/IronRequiem/Prefabs/VisualPass/VFX";
        const string VignetteGuid = "61e9c8b64d003e047ab7cfc7f5d74555";

        [MenuItem(Menu + "V2 Extraction arc + heat steam + overdrive edge")]
        static void Apply()
        {
            var log = new StringBuilder();
            Directory.CreateDirectory(Backup); Directory.CreateDirectory(Out);
            var spark = ParticleMat("Assets/FPS/Prefabs/VFX/VFX_HitSparks.prefab");
            var steam = FindParticleMat("VO_PortSteam") ?? FindParticleMat("VFX_OverheatPrimary") ?? spark;
            var rust = FindAsset<Material>("VO_Env_RustMetal t:Material");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            log.AppendLine($"mats spark={Name(spark)} steam={Name(steam)} rust={Name(rust)}");

            // 1) Extraction arc on the player(s) of the open scene.
            foreach (var carrier in Object.FindObjectsByType<StolenWeaponCarrier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var fx = carrier.GetComponent<ExtractionArcFx>(); if (!fx) fx = Undo.AddComponent<ExtractionArcFx>(carrier.gameObject);
                fx.Configure(spark, spark, rust, cube); EditorUtility.SetDirty(fx);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fx);
                log.AppendLine("ExtractionArcFx -> " + Path(carrier.transform));
            }

            // 2) Heat glow + steam on every IronRequiem weapon prefab with WeaponHeat.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!(path.Contains("/VisualOverhaul/") || path.Contains("/Campaign/"))) continue;
                if (!asset || !asset.GetComponentInChildren<WeaponHeat>(true)) continue;
                File.Copy(path, System.IO.Path.Combine(Backup, System.IO.Path.GetFileName(path) + ".bak"), true);
                var root = PrefabUtility.LoadPrefabContents(path);
                foreach (var h in root.GetComponentsInChildren<WeaponHeat>(true))
                {
                    var v = h.GetComponent<HeatVentFx>(); if (!v) v = h.gameObject.AddComponent<HeatVentFx>();
                    v.Configure(spark, steam);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
                log.AppendLine("HeatVentFx -> " + path);
            }

            // 3) OVERDRIVE edge on every FeedbackFlashCanvas of the open scene.
            var sprite = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(VignetteGuid)).OfType<Sprite>().FirstOrDefault();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.name == "FeedbackFlashCanvas"))
            {
                var t = c.transform.Find("VP_OverdriveEdge") as RectTransform;
                if (!t)
                {
                    var go = new GameObject("VP_OverdriveEdge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    Undo.RegisterCreatedObjectUndo(go, "F3b"); t = go.transform as RectTransform; t.SetParent(c.transform, false);
                }
                t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero;
                var img = t.GetComponent<Image>(); img.sprite = sprite; img.raycastTarget = false; img.color = new Color(1, .42f, .05f, 0); img.enabled = false;
                if (!t.GetComponent<OverdriveScreenFx>()) Undo.AddComponent<OverdriveScreenFx>(t.gameObject);
                log.AppendLine("OverdriveScreenFx -> " + Path(t) + " sprite=" + Name(sprite));
            }

            // 4) Module explosion: VP copy with metal debris, repoint CannonExplosion.impactEffect (IR prefabs only).
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(Out)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var ex = asset ? asset.GetComponentsInChildren<CannonExplosion>(true) : null;
                if (ex == null || ex.Length == 0) continue;
                bool changed = false; GameObject root = null;
                foreach (var e in ex)
                {
                    var so = new SerializedObject(e); var p = so.FindProperty("impactEffect");
                    var fx = p.objectReferenceValue as GameObject; if (!fx || AssetDatabase.GetAssetPath(fx).StartsWith(Out)) continue;
                    var copy = DebrisCopy(fx, rust, cube, log);
                    if (!root) { File.Copy(path, System.IO.Path.Combine(Backup, System.IO.Path.GetFileName(path) + ".bak"), true); root = PrefabUtility.LoadPrefabContents(path); }
                    changed = true;
                }
                if (!changed) continue;
                foreach (var e in root.GetComponentsInChildren<CannonExplosion>(true))
                {
                    var so = new SerializedObject(e); var p = so.FindProperty("impactEffect");
                    var fx = p.objectReferenceValue as GameObject; if (!fx || AssetDatabase.GetAssetPath(fx).StartsWith(Out)) continue;
                    p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Out + "/VP_" + fx.name + ".prefab"); so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
                log.AppendLine("CannonExplosion.impactEffect repointed in " + path);
            }

            EditorSceneManager.MarkAllScenesDirty(); AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f3-v2.txt", log.ToString()); Debug.Log("[F3b]\n" + log);
        }


        [MenuItem(Menu + "V2b Soft heat glow material")]
        static void GlowMaterial()
        {
            const string matPath = "Assets/IronRequiem/Prefabs/VisualPass/VFX/VP_Muzzle_Primary.prefab";
            var mat = ParticleMat(matPath);
            if (!mat) { Debug.LogError("[F3b] muzzle material not found"); return; }
            var log = new StringBuilder("glow mat " + matPath + "\n");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs/VisualOverhaul" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset || !asset.GetComponentInChildren<HeatVentFx>(true)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                foreach (var v in root.GetComponentsInChildren<HeatVentFx>(true))
                {
                    var so = new SerializedObject(v); so.FindProperty("glowMaterial").objectReferenceValue = mat;
                    so.FindProperty("glowSize").floatValue = .045f; so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
                log.AppendLine("glow -> " + path);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report + "/f3-v2b.txt", log.ToString()); Debug.Log("[F3b] " + log);
        }


        [MenuItem(Menu + "Diag heat fx (play)")]
        static void DiagHeat()
        {
            var sb = new StringBuilder(); var cam = Camera.main;
            foreach (var v in Object.FindObjectsByType<HeatVentFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"{Path(v.transform)} active={v.isActiveAndEnabled} heat={v.GetComponent<WeaponHeat>().NormalizedHeat:F2} layer={LayerMask.LayerToName(v.gameObject.layer)}");
                foreach (var ps in v.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    sb.AppendLine($"  {ps.name} playing={ps.isPlaying} count={ps.particleCount} pos={ps.transform.position} scr={(cam ? cam.WorldToScreenPoint(ps.transform.position).ToString() : "-")} lossy={ps.transform.lossyScale} layer={LayerMask.LayerToName(ps.gameObject.layer)} mat={(r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "null")} enabled={r.enabled}");
                }
            }
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) sb.AppendLine($"cam {c.name} mask={c.cullingMask} depth={c.depth}");
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f3-diag-heat.txt", sb.ToString()); Debug.Log("[F3b diag]\n" + sb);
        }

        static GameObject DebrisCopy(GameObject src, Material rust, Mesh cube, StringBuilder log)
        {
            string dst = Out + "/VP_" + src.name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(dst)) return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(src), dst);
            var root = PrefabUtility.LoadPrefabContents(dst);
            if (!root.transform.Find("VP_Debris"))
            {
                var go = new GameObject("VP_Debris"); go.transform.SetParent(root.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                var m = ps.main; m.playOnAwake = true; m.loop = false; m.duration = .2f; m.maxParticles = 14;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f); m.startSpeed = new ParticleSystem.MinMaxCurve(4, 9);
                m.startSize = new ParticleSystem.MinMaxCurve(.06f, .16f); m.gravityModifier = 1.8f; m.startColor = new Color(.3f, .27f, .25f);
                m.startRotation3D = true; m.startRotationX = m.startRotationY = m.startRotationZ = new ParticleSystem.MinMaxCurve(0, 6.28f);
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.scalingMode = ParticleSystemScalingMode.Local;
                var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = .3f;
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true; rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-8, 8);
                var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.quality = ParticleSystemCollisionQuality.Low; col.bounce = .3f; col.dampen = .4f; col.lifetimeLoss = .2f;
                var r = go.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = cube; r.sharedMaterial = rust;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root, dst); PrefabUtility.UnloadPrefabContents(root);
            log.AppendLine("VP debris copy: " + dst);
            return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
        }

        static Material ParticleMat(string path)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return src ? src.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(x => x.sharedMaterial).FirstOrDefault(m => m) : null;
        }
        static Material FindParticleMat(string name)
        {
            var g = AssetDatabase.FindAssets(name + " t:Prefab").FirstOrDefault();
            return g == null ? null : ParticleMat(AssetDatabase.GUIDToAssetPath(g));
        }
        static T FindAsset<T>(string filter) where T : Object
        {
            var g = AssetDatabase.FindAssets(filter).FirstOrDefault();
            return g == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
        }
        static string Name(Object o) => o ? o.name : "null";
        static string Path(Transform t) { var s = t.name; while ((t = t.parent)) s = t.name + "/" + s; return s; }
    }
}
