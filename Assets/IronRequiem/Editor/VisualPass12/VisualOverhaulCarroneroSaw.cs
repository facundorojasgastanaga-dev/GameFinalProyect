using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    /// Pase visual 12 · the Carroñero saw cut out of the generated model looks melted. This swaps it, ONLY in the Map01
    /// variant, for a plain toothed steel disc of the same radius on the same spin axis. The base prefab, the hangar and
    /// the menu Carroñeros keep the original mesh. Revert puts the original meshes and material back.
    public static class VisualOverhaulCarroneroSaw
    {
        const string Variant = "Assets/IronRequiem/Prefabs/Map01/Enemies/Carronero_Map01.prefab";
        const string Folder = "Assets/IronRequiem/Art/VisualPass12";
        const string MeshPath = Folder + "/Carronero_Saw_Simple.asset";
        const string MatPath = Folder + "/Carronero_Saw_Simple.mat";
        const string OldMat = "Assets/IronRequiem/Art/Enemies/Carronero/Built/Carronero_Saw.mat";
        const string OldMesh = "Assets/IronRequiem/Art/Enemies/Carronero/Built/Carronero_Saw.asset";
        const string OldMeshLow = "Assets/IronRequiem/Art/Enemies/Carronero/Built/Carronero_Saw_LOD2.asset";
        const int Teeth = 12;
        const string MarkPath = Folder + "/Carronero_Saw_Simple_Mark.mat";

        [MenuItem("Tools/Iron Requiem/Visual Pass 12/Carronero/Simple saw (Map01 variant)")] static void MenuApply() => Debug.Log(Apply());
        [MenuItem("Tools/Iron Requiem/Visual Pass 12/Carronero/Revert simple saw")] static void MenuRevert() => Debug.Log(Revert());

        public static string Apply()
        {
            var log = new StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(Variant);
            try
            {
                var info = root.GetComponentInChildren<CarroneroRigInfo>(true);
                if (!info || !info.saw) throw new InvalidOperationException("Sin CarroneroRigInfo.saw en " + Variant);
                var mainFilter = info.saw.GetComponent<MeshFilter>();
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(OldMesh);
                if (!mainFilter || !source) throw new InvalidOperationException("Sin malla de sierra original");

                // disc from the original: same axis, same radius, centred on the spin axis
                Vector3 axis = info.sawAxisLocal.sqrMagnitude > 1e-6f ? info.sawAxisLocal.normalized : Vector3.right;
                var b = source.bounds;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.forward).normalized, v = Vector3.Cross(axis, u);
                float along = Vector3.Dot(b.center, axis);
                float eu = Mathf.Abs(Vector3.Dot(b.extents, Abs(u))), ev = Mathf.Abs(Vector3.Dot(b.extents, Abs(v))), ea = Mathf.Abs(Vector3.Dot(b.extents, Abs(axis)));
                float radius = Mathf.Max(eu, ev), thick = Mathf.Clamp(radius * .07f, .006f, .02f);
                Vector3 off = b.center - axis * along;
                log.AppendLine($"original: centro {b.center:F3} ext {b.extents:F3} | eje {axis:F2} | radio {radius:F3} grosor original {ea * 2f:F3} | descentre {off.magnitude:F3}");
                Vector3 centre = axis * along + (off.magnitude > radius * .3f ? off : Vector3.zero);

                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                bool fresh = !mesh; if (fresh) mesh = new Mesh { name = "Carronero_Saw_Simple" };
                Build(mesh, centre, axis, u, v, radius, thick);
                if (fresh) AssetDatabase.CreateAsset(mesh, MeshPath); else EditorUtility.SetDirty(mesh);

                var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
                if (!mat)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Carronero_Saw_Simple" };
                    mat.SetColor("_BaseColor", new Color(.34f, .35f, .37f)); mat.SetFloat("_Metallic", .85f); mat.SetFloat("_Smoothness", .5f);
                    AssetDatabase.CreateAsset(mat, MatPath);
                }

                var mark = AssetDatabase.LoadAssetAtPath<Material>(MarkPath);
                if (!mark)
                {
                    mark = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Carronero_Saw_Simple_Mark" };
                    mark.SetColor("_BaseColor", new Color(.03f, .03f, .035f)); mark.SetFloat("_Metallic", 0f); mark.SetFloat("_Smoothness", .15f);
                    AssetDatabase.CreateAsset(mark, MarkPath);
                }

                int n = 0;
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = f.sharedMesh; if (!m) continue;
                    string p = AssetDatabase.GetAssetPath(m);
                    if (p != OldMesh && p != OldMeshLow && p != MeshPath) continue;
                    f.sharedMesh = mesh;
                    var r = f.GetComponent<MeshRenderer>();
                    if (r) r.sharedMaterials = new[] { mat, mark };
                    log.AppendLine("sierra simple en " + f.name); n++;
                }
                if (n == 0) throw new InvalidOperationException("Ningún MeshFilter usa la sierra original");
                PrefabUtility.SaveAsPrefabAsset(root, Variant, out bool saved);
                if (!saved) throw new InvalidOperationException("No se pudo guardar " + Variant);
                log.AppendLine($"GUARDADO {Variant} · {n} piezas · {mesh.triangles.Length / 3} tris");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        public static string Revert()
        {
            var log = new StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(Variant);
            try
            {
                var simple = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                int n = 0;
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!simple || f.sharedMesh != simple) continue;
                    PrefabUtility.RevertObjectOverride(f, InteractionMode.AutomatedAction);
                    var r = f.GetComponent<MeshRenderer>(); if (r) PrefabUtility.RevertObjectOverride(r, InteractionMode.AutomatedAction);
                    n++;
                }
                if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, Variant, out bool saved); if (!saved) throw new InvalidOperationException("No se pudo guardar " + Variant); }
                log.AppendLine(n > 0 ? $"REVERTIDO {n} piezas" : "sin cambios");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return log.ToString();
        }

        static Vector3 Abs(Vector3 a) => new Vector3(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z));

        /// Flat-shaded toothed disc with a thicker hub. Outline alternates tip / root radius, so the teeth read in silhouette.
        static void Build(Mesh mesh, Vector3 c, Vector3 axis, Vector3 u, Vector3 v, float radius, float thick)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int ring = Teeth * 2; float h = thick * .5f, root = radius * .84f;
            Vector3 P(int i, float rad, float side) { float a = i * Mathf.PI * 2f / ring; return c + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rad + axis * side; }
            float R(int i) => (i & 1) == 0 ? radius : root;
            void Tri(Vector3 a, Vector3 b2, Vector3 d)
            {
                Vector3 n = Vector3.Cross(b2 - a, d - a).normalized; int k = verts.Count;
                verts.Add(a); verts.Add(b2); verts.Add(d); norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            void Disc(float rad0, Func<int, float> rad, float side, float sign)
            {
                for (int i = 0; i < ring; i++)
                {
                    int j = (i + 1) % ring; Vector3 centre = c + axis * side, a = P(i, rad(i), side), b2 = P(j, rad(j), side);
                    if (sign > 0) Tri(centre, a, b2); else Tri(centre, b2, a);
                }
            }
            void Wall(Func<int, float> rad, float half)
            {
                for (int i = 0; i < ring; i++)
                {
                    int j = (i + 1) % ring; Vector3 a0 = P(i, rad(i), -half), a1 = P(i, rad(i), half), b0 = P(j, rad(j), -half), b1 = P(j, rad(j), half);
                    Tri(a0, a1, b1); Tri(a0, b1, b0);
                }
            }
            // winding is fixed after the fact so the faces look outwards whatever the handedness of (u, v, axis)
            Disc(0, R, h, 1); Disc(0, R, -h, -1); Wall(R, h);
            float hub = radius * .26f, hubH = h * 2.4f;
            Disc(0, _ => hub, hubH, 1); Disc(0, _ => hub, -hubH, -1); Wall(_ => hub, hubH);
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 mid = (verts[tris[t]] + verts[tris[t + 1]] + verts[tris[t + 2]]) / 3f - c, n = norms[tris[t]];
                Vector3 outward = Mathf.Abs(Vector3.Dot(n, axis)) > .5f ? axis * Mathf.Sign(Vector3.Dot(mid, axis)) : mid - axis * Vector3.Dot(mid, axis);
                if (Vector3.Dot(n, outward) < 0f)
                {
                    (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
                    norms[tris[t]] = norms[tris[t + 1]] = norms[tris[t + 2]] = -n;
                }
            }
            // dark painted wedges (submesh 1), uneven on purpose: a plain disc of equal teeth looks still while it spins
            var marks = new List<int>(); float[] starts = { 0f, 100f, 235f }, widths = { 50f, 30f, 40f }; float lift = h + .0006f, r0 = hub * 1.15f, r1 = root * .93f;
            for (int w = 0; w < starts.Length; w++)
                for (int sIdx = 0; sIdx < 4; sIdx++)
                {
                    float a0 = (starts[w] + widths[w] * sIdx / 4f) * Mathf.Deg2Rad, a1 = (starts[w] + widths[w] * (sIdx + 1) / 4f) * Mathf.Deg2Rad;
                    Vector3 d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 n = axis * side, o = c + n * lift; int k = verts.Count;
                        verts.Add(o + d0 * r0); verts.Add(o + d0 * r1); verts.Add(o + d1 * r1); verts.Add(o + d1 * r0);
                        for (int q = 0; q < 4; q++) { norms.Add(n); uvs.Add(Vector2.zero); }
                        bool flip = Vector3.Dot(Vector3.Cross(verts[k + 1] - verts[k], verts[k + 2] - verts[k]), n) < 0f;
                        if (flip) { marks.Add(k); marks.Add(k + 2); marks.Add(k + 1); marks.Add(k); marks.Add(k + 3); marks.Add(k + 2); }
                        else { marks.Add(k); marks.Add(k + 1); marks.Add(k + 2); marks.Add(k); marks.Add(k + 2); marks.Add(k + 3); }
                    }
                }
            mesh.Clear(); mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.subMeshCount = 2; mesh.SetTriangles(tris, 0); mesh.SetTriangles(marks, 1);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
        }
    }
}
