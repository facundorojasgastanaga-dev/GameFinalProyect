using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Read-only dumps used to plan the campaign scenes (08/11/2026). Opens scenes without saving.
    public static class IronCampaignProbe
    {
        public const string Reports = "Tools/IronRequiem/Reports/Campaign";

        // args: "scenePath|rootName" -> every renderer/collider under the root with world bounds.
        public static string Children(string args)
        {
            var a = args.Split('|'); 
            var scene = EditorSceneManager.OpenScene(a[0], OpenSceneMode.Single);
            var sb = new StringBuilder();
            var root = scene.GetRootGameObjects().First(g => g.name == a[1]);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>(); var c = t.GetComponent<Collider>();
                if (!r && !c && t != root.transform) continue;
                var b = r ? r.bounds : c ? c.bounds : new Bounds(t.position, Vector3.zero);
                sb.AppendLine(Path(t, root.transform) + " | active=" + t.gameObject.activeInHierarchy + " | min=" + b.min.ToString("F2") + " max=" + b.max.ToString("F2")
                    + (r ? " | mat=" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")) : "") + (c ? " | col=" + c.GetType().Name + (c.isTrigger ? "(trigger)" : "") : "")
                    + " | comps=" + string.Join(",", t.GetComponents<Component>().Where(x => x && !(x is Transform) && !(x is Renderer) && !(x is MeshFilter) && !(x is Collider)).Select(x => x.GetType().Name)));
            }
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports + "/children-" + Path(root.transform, null) + "-" + System.IO.Path.GetFileNameWithoutExtension(a[0]) + ".txt", sb.ToString());
            return "lines=" + sb.ToString().Split('\n').Length;
        }

        // args: "scenePath|ComponentType" -> serialized fields of every component of that type (one level).
        public static string Fields(string args)
        {
            var a = args.Split('|'); 
            EditorSceneManager.OpenScene(a[0], OpenSceneMode.Single); var sb = new StringBuilder();
            foreach (var c in Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c && c.GetType().Name == a[1]))
            {
                sb.AppendLine("== " + c.gameObject.name);
                var so = new SerializedObject(c); var it = so.GetIterator(); bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = it.propertyType == SerializedPropertyType.Generic && it.depth < 6;
                    string v = it.propertyType switch
                    {
                        SerializedPropertyType.ObjectReference => it.objectReferenceValue ? it.objectReferenceValue.name + " (" + AssetDatabase.GetAssetPath(it.objectReferenceValue) + ")" : "null",
                        SerializedPropertyType.Float => it.floatValue.ToString(),
                        SerializedPropertyType.Integer => it.intValue.ToString(),
                        SerializedPropertyType.Boolean => it.boolValue.ToString(),
                        SerializedPropertyType.String => it.stringValue,
                        SerializedPropertyType.Vector3 => it.vector3Value.ToString("F2"),
                        SerializedPropertyType.Bounds => it.boundsValue.ToString(),
                        SerializedPropertyType.Enum => it.enumDisplayNames.Length > it.enumValueIndex && it.enumValueIndex >= 0 ? it.enumDisplayNames[it.enumValueIndex] : it.enumValueIndex.ToString(),
                        _ => it.isArray ? "[" + it.arraySize + "]" : ""
                    };
                    sb.AppendLine(new string(' ', it.depth * 2) + it.propertyPath + " = " + v);
                }
            }
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports + "/fields-" + a[1] + "-" + System.IO.Path.GetFileNameWithoutExtension(a[0]) + ".txt", sb.ToString());
            return "lines=" + sb.ToString().Split('\n').Length;
        }

        static string Path(Transform t, Transform root) => t == root || !t.parent ? t.name : Path(t.parent, root) + "/" + t.name;
    }
}
