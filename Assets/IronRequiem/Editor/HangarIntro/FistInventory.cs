using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// Paso 0 del puño: inventario de solo lectura de los huesos de las dos manos del rig FPS (escena abierta) y del
    /// CapsuleRobotRig (asset). Por hueso: nombre, posición y rotación local, escala, longitud al hijo; y por malla:
    /// qué huesos de dedo tienen peso, cuántos vértices dominan y si el skin es rígido (un hueso por vértice).
    /// No cambia nada. Informe: Tools/IronRequiem/Reports/HangarIntro/fist_inventory.txt
    public static class FistInventory
    {
        const string Report = "Tools/IronRequiem/Reports/HangarIntro/fist_inventory.txt";
        static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "little", "pinky" };

        [MenuItem("Tools/Iron Requiem/Fist Pose/0. Inventario de manos (solo lectura)")]
        static void Run()
        {
            var sb = new StringBuilder(); sb.AppendLine("Fist inventory " + System.DateTime.Now);
            var hands = Object.FindObjectsByType<ProtagonistHandsPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (hands)
            {
                sb.AppendLine("== FPS rig (escena " + hands.gameObject.scene.name + ", objeto " + hands.name + ")");
                Hand(sb, "LEFT", hands.LeftHandBone); Hand(sb, "RIGHT", hands.RightHand);
                Skins(sb, hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                foreach (var s in hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.name.StartsWith("Hand_")) Bind(sb, s);
                var rigid = hands.FpsRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r => IsFinger(r.transform.parent)).Select(r => r.name + "→" + r.transform.parent.name).ToArray();
                sb.AppendLine("MeshRenderers colgados de huesos de dedo: " + (rigid.Length == 0 ? "ninguno" : string.Join(", ", rigid)));
            }
            else sb.AppendLine("== FPS rig: no hay ProtagonistHandsPresentation en la escena abierta");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/IronRequiem/Art/Protagonist/CapsuleRobotRig" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!go) continue;
                sb.AppendLine("== CapsuleRobotRig asset " + path);
                foreach (var h in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.ToLowerInvariant().Contains("hand") && !IsFinger(t) && t.GetComponentsInChildren<Transform>().Any(IsFinger)))
                    Hand(sb, h.name, h);
                Skins(sb, go.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, sb.ToString()); Debug.Log("[FistInventory] " + Report);
        }
        static bool IsFinger(Transform t) { if (!t) return false; var n = t.name.ToLowerInvariant(); return Fingers.Any(n.Contains); }
        static void Hand(StringBuilder sb, string label, Transform hand)
        {
            if (!hand) { sb.AppendLine(label + ": sin hueso de mano"); return; }
            sb.AppendLine($"-- {label} hand '{hand.name}' path={BonePath(hand)} lossyScale={V(hand.lossyScale)} localRot={V(hand.localEulerAngles)}");
            foreach (var t in hand.GetComponentsInChildren<Transform>(true))
            {
                if (t == hand) continue; int depth = 0; for (var p = t.parent; p && p != hand; p = p.parent) depth++;
                Transform child = null; for (int c = 0; c < t.childCount; c++) if (IsFinger(t.GetChild(c)) || t.GetChild(c).name.EndsWith("_end")) { child = t.GetChild(c); break; }
                float len = child ? Vector3.Distance(t.position, child.position) : 0;
                string kind = t.GetComponent<Renderer>() ? " [renderer " + t.GetComponent<Renderer>().GetType().Name + "]" : IsFinger(t) ? "" : " [no-dedo]";
                sb.AppendLine($"{new string(' ', 2 + depth * 2)}{t.name}{kind} pos={V(t.localPosition, 4)} rot={V(t.localEulerAngles)} q={Q(t.localRotation)} scale={V(t.localScale)} len={len * 1000:0.0}mm");
            }
        }
        static void Skins(StringBuilder sb, IEnumerable<SkinnedMeshRenderer> skins)
        {
            foreach (var s in skins)
            {
                var mesh = s.sharedMesh; if (!mesh) continue;
                var weights = mesh.GetAllBoneWeights(); var perVertex = mesh.GetBonesPerVertex(); var bones = s.bones;
                var dominant = new Dictionary<int, int>(); int multi = 0, idx = 0;
                for (int v = 0; v < perVertex.Length; v++)
                {
                    int n = perVertex[v]; float best = -1; int bestBone = -1; int significant = 0;
                    for (int k = 0; k < n; k++) { var w = weights[idx + k]; if (w.weight > .01f) significant++; if (w.weight > best) { best = w.weight; bestBone = w.boneIndex; } }
                    idx += n; if (significant > 1) multi++; if (bestBone >= 0) dominant[bestBone] = dominant.TryGetValue(bestBone, out var c) ? c + 1 : 1;
                }
                var fingerBones = dominant.Where(kv => kv.Key < bones.Length && IsFinger(bones[kv.Key])).OrderBy(kv => bones[kv.Key].name).Select(kv => bones[kv.Key].name + ":" + kv.Value).ToArray();
                sb.AppendLine($"skin '{s.name}' mesh='{mesh.name}' verts={mesh.vertexCount} bones={bones.Length} verticesConVariosHuesos={multi} ({(perVertex.Length > 0 ? 100f * multi / perVertex.Length : 0):0.0}%) rigido={(multi == 0 ? "sí" : "no")}");
                sb.AppendLine("   vértices dominados por huesos de dedo: " + (fingerBones.Length == 0 ? "ninguno" : string.Join(", ", fingerBones)));
            }
        }
        // True bind pose from the mesh bindposes: local rotation of each finger bone relative to its parent, and how far
        // the scene pose is from it (degrees). Bindpose = bone.worldToLocal * mesh.localToWorld at bind time.
        static void Bind(StringBuilder sb, SkinnedMeshRenderer s)
        {
            var bones = s.bones; var bp = s.sharedMesh.bindposes; var index = new Dictionary<Transform, int>(); for (int i = 0; i < bones.Length; i++) if (bones[i]) index[bones[i]] = i;
            sb.AppendLine($"bind pose de '{s.name}' (rotación local en bind vs escena):");
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i]; if (!b || !IsFinger(b) || !b.parent || !index.TryGetValue(b.parent, out int pi)) continue;
                var bindWorld = bp[i].inverse; var parentWorld = bp[pi].inverse; // bone→mesh space
                var local = Quaternion.Inverse(parentWorld.rotation) * bindWorld.rotation;
                var localPos = parentWorld.inverse.MultiplyPoint3x4(bindWorld.GetColumn(3));
                sb.AppendLine($"   {b.name} bindRot={V(local.eulerAngles)} bindPos={V(localPos, 4)} escena-vs-bind={Quaternion.Angle(local, b.localRotation):0.0}°");
            }
        }
        static string BonePath(Transform t) { var s = t.name; for (var p = t.parent; p; p = p.parent) s = p.name + "/" + s; return s; }
        static string V(Vector3 v, int d = 1) => "(" + v.x.ToString("F" + d) + "," + v.y.ToString("F" + d) + "," + v.z.ToString("F" + d) + ")";
        static string Q(Quaternion q) => $"({q.x:F3},{q.y:F3},{q.z:F3},{q.w:F3})";
    }
}
