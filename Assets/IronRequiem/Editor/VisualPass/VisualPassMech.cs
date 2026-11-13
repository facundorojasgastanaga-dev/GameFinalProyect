using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Visual pass F8: new rigged mech model for the T-07 (import, optimized visual, clips, prefab).
    public static partial class VisualPassMech
    {
        public const string Dir = "Assets/IronRequiem/Art/Enemies/T07Mech";
        public const string Fbx = Dir + "/T07Mech_Rigged.fbx";

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 1 import settings")]
        static void ImportSettings()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir + "/T07Mech_Rigged.fbm" }))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (ti.isReadable && ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled) continue;
                ti.isReadable = true; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false; ti.SaveAndReimport(); n++;
            }
            var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false; mi.isReadable = true; mi.importBlendShapes = false;
            mi.SaveAndReimport();
            Debug.Log($"[F8 import] textures set={n}");
            Audit();
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech audit")]
        static void Audit()
        {
            var sb = new StringBuilder("[F8 audit]\n");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (model)
            {
                var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var b = new Bounds(); bool first = true;
                foreach (var s in smrs) { if (first) { b = s.bounds; first = false; } else b.Encapsulate(s.bounds); }
                sb.AppendLine($"mech smr={smrs.Length} bounds c={b.center} s={b.size} mats={smrs.Count(s => s.sharedMaterial && s.sharedMaterial.mainTexture)} anim={model.GetComponent<Animator>()?.avatar}");
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains("Hips") || t.name.EndsWith("Foot") || t.name.EndsWith("Toes") || t.name.Contains("Head") || t.name.Contains("LowerArm") || t.name.Contains("UpperLeg") || t.name.Contains("LowerLeg") || t.name == "Right_Hand")
                        sb.AppendLine($"  {t.name} p={t.position.ToString("F3")} r={t.rotation.eulerAngles.ToString("F0")} parent={t.parent?.name}");
                sb.AppendLine($"  root children: {string.Join(",", model.transform.Cast<Transform>().Select(x => x.name))}");
            }
            var old = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab");
            if (old)
            {
                var rs = old.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r is not ParticleSystemRenderer).ToArray();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                sb.AppendLine($"old T07 bounds c={b.center} s={b.size} renderers={rs.Length}");
                var drv = old.GetComponent<IronRequiem.T07AnimationDriver>();
                if (drv) sb.AppendLine($"  stride={drv.StrideDistance} animator={drv.Animator?.name} ctrl={drv.Animator?.runtimeAnimatorController?.name} avatar={drv.Animator?.avatar?.name} animScale={drv.Animator?.transform.lossyScale}");
                foreach (var c in old.GetComponentsInChildren<MonoBehaviour>(true))
                    if (c) sb.AppendLine($"  comp {Path(c.transform, old.transform)} : {c.GetType().Name}");
                foreach (var c in old.GetComponentsInChildren<Collider>(true))
                    sb.AppendLine($"  col {Path(c.transform, old.transform)} : {c.GetType().Name} {c.bounds.size.ToString("F2")}");
                var na = old.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (na) sb.AppendLine($"  agent speed={na.speed} h={na.height} r={na.radius}");
            }
            Debug.Log(sb.ToString());
        }

        public static string Path(Transform t, Transform root)
        {
            if (!t || t == root) return "."; var s = t.name;
            while (t.parent && t.parent != root) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
