using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 12/11/2026 v3 — the protagonist's auto-rig (Tripo) spreads the hand bone's weights up the forearm, so rolling the
    /// wrist twisted the whole arm like rubber. A robot arm is a mechanism: this tool builds a copy of each skinned mesh in
    /// which every arm-chain vertex follows exactly one bone (rigid segments, like the T-07).
    /// 12/11/2026 v30 — the neck stretched when the head turned to look at the hand, and the hand stretched the forearm.
    /// The head, the neck and each hand are now CUT out as separate rigid pieces: every triangle is assigned to one piece
    /// (majority of its vertices), the vertices along the cut are duplicated and the piece's copies are weighted 100 % to
    /// Head, Neck or Hand (fingers keep their own bone so they can still curl). The rest of the body loses any influence
    /// of those bones, so nothing is pulled. Apply() also adds mechanical collars that hide the cuts: head ring (on Neck),
    /// neck ring (on Spine2) and wrist rings (on each ForeArm). Source FBX untouched; the derived meshes keep their asset
    /// path and GUID between builds.
    public static class RobotRigidSkin
    {
        const string Dir = HangarMenuAuthoring.RigDir + "/Rigid";
        const string Version = "v30f-cut-head-neck-hands-per-vertex";
        const string CollarPrefix = "IR_Collar_";
        static readonly string[] ArmBones = { "Arm", "ForeArm", "Hand" };

        enum Piece { Body, Arm, Head, Neck, HandL, HandR }

        static string Short(string boneName) { int c = boneName.LastIndexOf(':'); return c >= 0 ? boneName.Substring(c + 1) : boneName; }

        static Piece PieceOf(string boneName)
        {
            var n = Short(boneName);
            if (n == "Neck") return Piece.Neck;
            if (n.StartsWith("Head")) return Piece.Head;                  // Head, HeadTop_End
            // v30f: hands stay rigid per vertex (like the arm segments): cutting them by triangle exposed the jagged
            // inside of the forearm cuff when the wrist rolled. Head and neck stay cut.
            if (n.StartsWith("LeftHand") || n.StartsWith("RightHand")) return Piece.Arm;
            if (n.StartsWith("Left") || n.StartsWith("Right"))
            {
                var s = n.StartsWith("Left") ? n.Substring(4) : n.Substring(5);
                if (ArmBones.Contains(s)) return Piece.Arm;
            }
            return Piece.Body;
        }

        static bool IsCut(Piece p) => p == Piece.Head || p == Piece.Neck || p == Piece.HandL || p == Piece.HandR;
        static int Distal(Piece p) => p == Piece.Head ? 5 : p == Piece.HandL || p == Piece.HandR ? 4 : p == Piece.Neck ? 3 : p == Piece.Arm ? 2 : 1;

        static bool force;

        [MenuItem("Tools/Iron Requiem/Hangar Intro/Rebuild rigid arm skin (next build)")]
        static void Rebuild() { force = true; Debug.Log("[RigidSkin] the next Room00 / menu build regenerates the rigid robot meshes"); }

        /// Builds (or reuses) the rigid copies from the rig model asset. Call before any scene that holds an instance
        /// of the model is built: toggling Read/Write reimports the model, which would reset instance overrides.
        public static void Prepare(StringBuilder log)
        {
            Directory.CreateDirectory(Dir);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HangarMenuAuthoring.RigModel); if (!model) { log?.AppendLine("rigid skin: rig model missing"); return; }
            var versionFile = Dir + "/version.txt";
            bool stale = !File.Exists(versionFile) || File.ReadAllText(versionFile).Trim() != Version;
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh).ToArray();
            var missing = smrs.Where(r => force || stale || !AssetDatabase.LoadAssetAtPath<Mesh>(PathFor(r.sharedMesh))).ToArray();
            if (missing.Length == 0) { log?.AppendLine("rigid skin: cached copies reused (" + Version + ")"); return; }
            var importer = AssetImporter.GetAtPath(HangarMenuAuthoring.RigModel) as ModelImporter;
            bool toggle = importer && !importer.isReadable;
            if (toggle) { BackupMeta(importer.assetPath); importer.isReadable = true; importer.SaveAndReimport(); }
            try
            {
                model = AssetDatabase.LoadAssetAtPath<GameObject>(HangarMenuAuthoring.RigModel);
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh) Build(r.sharedMesh, r.bones, log);
                File.WriteAllText(versionFile, Version);
            }
            finally { if (toggle) { importer.isReadable = false; importer.SaveAndReimport(); } force = false; }
        }

        static string PathFor(Mesh source) => Dir + "/" + source.name + "_RigidArms.asset";

        /// Swaps the shared meshes of every SkinnedMeshRenderer under root for the prepared rigid copies (no import) and
        /// adds the collars that hide the cuts (idempotent: old collars are removed first).
        /// v30b: collars are off by default (the user preferred the bare cuts: no dark rings at the neck and wrists).
        public static void Apply(Transform root, StringBuilder log, Material collarMaterial = null, bool withCollars = false)
        {
            if (!root) return;
            int swapped = 0;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!r.sharedMesh || AssetDatabase.GetAssetPath(r.sharedMesh).StartsWith(Dir)) continue;
                var rigid = AssetDatabase.LoadAssetAtPath<Mesh>(PathFor(r.sharedMesh));
                if (rigid) { r.sharedMesh = rigid; swapped++; } else log?.AppendLine("WARN rigid skin missing for " + r.sharedMesh.name + " (run Prepare first)");
            }
            log?.AppendLine("rigid skin applied to " + swapped + " skinned renderer(s)");
            AddCollars(root, log, collarMaterial, withCollars);
        }

        // ------------------------------------------------------------------ collars
        static void AddCollars(Transform root, StringBuilder log, Material mat, bool add)
        {
            foreach (var old in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(CollarPrefix)).ToArray()) Object.DestroyImmediate(old.gameObject);
            if (!add) { log?.AppendLine("collars: off"); return; }
            var bones = root.GetComponentsInChildren<Transform>(true);
            Transform B(string n) => bones.FirstOrDefault(b => Short(b.name) == n);
            var main = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh && r.gameObject.name != "GlowOverlay").OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
            if (!main) { log?.AppendLine("collars: no skinned mesh"); return; }
            var verts = SkinnedWorld(main);
            if (verts.Length == 0) { log?.AppendLine("collars: mesh not readable"); return; }
            // body scale: the rig is ~1.85 units from head to foot at scale 1
            float unit = B("Head") && B("LeftFoot") ? Mathf.Max(.3f, Vector3.Distance(B("Head").position, B("LeftFoot").position) / 1.85f) : 1f;
            int n = 0;
            n += Collar("Head", B("Head"), B("Neck"), B("Neck"), verts, .07f * unit, .045f * unit, mat, log) ? 1 : 0;
            n += Collar("Neck", B("Neck"), B("Spine2"), B("Spine2"), verts, .06f * unit, .05f * unit, mat, log) ? 1 : 0;
            n += Collar("WristL", B("LeftHand"), B("LeftForeArm"), B("LeftForeArm"), verts, .05f * unit, .035f * unit, mat, log) ? 1 : 0;
            n += Collar("WristR", B("RightHand"), B("RightForeArm"), B("RightForeArm"), verts, .05f * unit, .035f * unit, mat, log) ? 1 : 0;
            log?.AppendLine("collars added: " + n);
        }

        /// World positions of a skinned mesh's vertices in the current pose, skinned on the CPU from the mesh data
        /// (bindposes + bone matrices). Used by the authoring tools; BakeMesh returned NaN for these meshes in edit mode.
        internal static Vector3[] SkinnedWorld(SkinnedMeshRenderer r)
        {
            var mesh = r ? r.sharedMesh : null; if (!mesh || !mesh.isReadable) return new Vector3[0];
            var v = mesh.vertices; var w = mesh.boneWeights; var bp = mesh.bindposes; var bones = r.bones;
            if (w.Length != v.Length || bones.Length == 0) return new Vector3[0];
            var mats = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) mats[i] = bones[i] && i < bp.Length ? bones[i].localToWorldMatrix * bp[i] : r.transform.localToWorldMatrix;
            var o = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var b = w[i]; var p = Vector3.zero; float sum = 0;
                if (b.weight0 > 0) { p += mats[b.boneIndex0].MultiplyPoint3x4(v[i]) * b.weight0; sum += b.weight0; }
                if (b.weight1 > 0) { p += mats[b.boneIndex1].MultiplyPoint3x4(v[i]) * b.weight1; sum += b.weight1; }
                if (b.weight2 > 0) { p += mats[b.boneIndex2].MultiplyPoint3x4(v[i]) * b.weight2; sum += b.weight2; }
                if (b.weight3 > 0) { p += mats[b.boneIndex3].MultiplyPoint3x4(v[i]) * b.weight3; sum += b.weight3; }
                o[i] = sum > 1e-5f ? p / sum : r.transform.TransformPoint(v[i]);
            }
            return o;
        }

        /// Ring at the joint of `joint` (axis from `from` to `joint`), parented to `parent`, sized from the mesh around it.
        static bool Collar(string name, Transform joint, Transform from, Transform parent, Vector3[] verts, float length, float slab, Material mat, StringBuilder log)
        {
            if (!joint || !from || !parent) { log?.AppendLine("collar " + name + ": bones missing"); return false; }
            var j = joint.position; var axis = (j - from.position).normalized;
            var radii = new List<float>();
            foreach (var v in verts)
            {
                var d = v - j; float along = Vector3.Dot(d, axis); if (Mathf.Abs(along) > slab) continue;
                float radial = (d - axis * along).magnitude; if (radial < slab * 6f) radii.Add(radial);
            }
            if (radii.Count < 8) { log?.AppendLine("collar " + name + ": not enough vertices near the joint"); return false; }
            radii.Sort(); float r = radii[Mathf.Clamp((int)(radii.Count * .8f), 0, radii.Count - 1)] * 1.06f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = CollarPrefix + name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(j - axis * (length * .15f), Quaternion.FromToRotation(Vector3.up, axis));
            var ls = parent.lossyScale; var want = new Vector3(2 * r, length * .5f, 2 * r);
            go.transform.localScale = new Vector3(want.x / Mathf.Max(1e-5f, Mathf.Abs(ls.x)), want.y / Mathf.Max(1e-5f, Mathf.Abs(ls.y)), want.z / Mathf.Max(1e-5f, Mathf.Abs(ls.z)));
            var mr = go.GetComponent<MeshRenderer>(); if (mat) mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.On;
            log?.AppendLine($"collar {name}: radius {r:0.000} m length {length:0.000} m on {parent.name}");
            return true;
        }

        static void BackupMeta(string assetPath)
        {
            var dir = "Tools/IronRequiem/Backups/" + DateTime.Now.ToString("yyyyMMdd") + "-rigid-skin"; Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, Path.GetFileName(assetPath) + ".meta"); if (!File.Exists(target)) File.Copy(assetPath + ".meta", target);
        }

        // ------------------------------------------------------------------ mesh
        static Mesh Build(Mesh src, Transform[] bones, StringBuilder log)
        {
            if (!src.isReadable) { log?.AppendLine("WARN rigid skin: mesh not readable " + src.name); return null; }
            var weights = src.boneWeights; if (weights.Length == 0) return null;
            var piece = bones.Select(b => b ? PieceOf(b.name) : Piece.Body).ToArray();
            int Index(string n) => Array.FindIndex(bones, b => b && Short(b.name) == n);
            int headI = Index("Head"), neckI = Index("Neck"), spine2I = Index("Spine2"), lHandI = Index("LeftHand"), rHandI = Index("RightHand"), lForeI = Index("LeftForeArm"), rForeI = Index("RightForeArm");
            Piece P(int bone) => bone >= 0 && bone < piece.Length ? piece[bone] : Piece.Body;
            // joint positions in mesh (bind) space, from the bind poses
            var bpArr = src.bindposes; var jointPos = new Vector3[bpArr.Length];
            for (int i = 0; i < bpArr.Length; i++) jointPos[i] = bpArr[i].inverse.MultiplyPoint3x4(Vector3.zero);
            float wristBand = lHandI >= 0 && lForeI >= 0 ? Vector3.Distance(jointPos[lHandI], jointPos[lForeI]) * .18f : .03f;

            var pos = src.vertices; var nrm = src.normals; var tan = src.tangents; var col = src.colors;
            var uvs = new List<Vector2>[4]; for (int c = 0; c < 4; c++) { if (!src.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + c))) continue; uvs[c] = new List<Vector2>(); src.GetUVs(c, uvs[c]); }
            var oPos = new List<Vector3>(); var oNrm = new List<Vector3>(); var oTan = new List<Vector4>(); var oCol = new List<Color>(); var oUv = new List<Vector2>[4];
            for (int c = 0; c < 4; c++) if (uvs[c] != null) oUv[c] = new List<Vector2>();
            var oW = new List<BoneWeight>(); var map = new Dictionary<long, int>();

            // dominant bone per vertex
            var dom = new int[weights.Length];
            for (int v = 0; v < weights.Length; v++)
            {
                var w = weights[v]; int best = w.boneIndex0; float bw = w.weight0;
                if (w.weight1 > bw) { best = w.boneIndex1; bw = w.weight1; }
                if (w.weight2 > bw) { best = w.boneIndex2; bw = w.weight2; }
                if (w.weight3 > bw) { best = w.boneIndex3; bw = w.weight3; }
                dom[v] = best;
            }

            BoneWeight Hard(int bone) => new BoneWeight { boneIndex0 = Mathf.Max(0, bone), weight0 = 1 };
            BoneWeight Rest(int v)
            {
                // arms: rigid per segment (v3); body: original weights without any influence of the cut pieces
                if (P(dom[v]) == Piece.Arm) return Hard(dom[v]);
                // hand vertices left in the arm's triangles follow the forearm (renormalising them sent some to the upper
                // arm or the spine and threw spikes around the cuff)
                if (P(dom[v]) == Piece.HandL) return Hard(lForeI);
                if (P(dom[v]) == Piece.HandR) return Hard(rForeI);
                var w = weights[v];
                int[] bi = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 }; float[] bw = { w.weight0, w.weight1, w.weight2, w.weight3 };
                float sum = 0; for (int i = 0; i < 4; i++) { if (IsCut(P(bi[i]))) bw[i] = 0; sum += bw[i]; }
                if (sum <= 1e-4f)
                {
                    var p = P(dom[v]);
                    return Hard(p == Piece.HandL ? lForeI : p == Piece.HandR ? rForeI : spine2I);
                }
                // Unity wants the influences sorted by weight (the first two must not be zero): sort descending
                for (int a = 0; a < 3; a++) for (int c = a + 1; c < 4; c++) if (bw[c] > bw[a]) { (bw[a], bw[c]) = (bw[c], bw[a]); (bi[a], bi[c]) = (bi[c], bi[a]); }
                for (int i = 0; i < 4; i++) if (bw[i] <= 0) { bw[i] = 0; bi[i] = bi[0]; }
                return new BoneWeight { boneIndex0 = bi[0], weight0 = bw[0] / sum, boneIndex1 = bi[1], weight1 = bw[1] / sum, boneIndex2 = bi[2], weight2 = bw[2] / sum, boneIndex3 = bi[3], weight3 = bw[3] / sum };
            }
            BoneWeight Cut(int v, Piece p)
            {
                // a border vertex that belongs to the neighbouring piece keeps following it: the one band of border
                // triangles hinges instead of throwing spikes when the hand rolls or the head turns
                // ...except right at the wrist: those border vertices close the hand piece (no long triangles left behind)
                if (P(dom[v]) != p)
                {
                    int joint = p == Piece.HandL ? lHandI : p == Piece.HandR ? rHandI : -1;
                    if (joint < 0 || Vector3.Distance(pos[v], jointPos[joint]) > wristBand) return Rest(v);
                }
                switch (p)
                {
                    case Piece.Head: return Hard(headI);
                    case Piece.Neck: return Hard(neckI);
                    case Piece.HandL: return Hard(P(dom[v]) == Piece.HandL ? dom[v] : lHandI);   // fingers keep their bone
                    default: return Hard(P(dom[v]) == Piece.HandR ? dom[v] : rHandI);
                }
            }
            int Vert(int v, Piece slot)
            {
                long key = ((long)v << 8) | (uint)slot;
                if (map.TryGetValue(key, out var i)) return i;
                i = oPos.Count; map[key] = i;
                oPos.Add(pos[v]); if (nrm.Length > 0) oNrm.Add(nrm[v]); if (tan.Length > 0) oTan.Add(tan[v]); if (col.Length > 0) oCol.Add(col[v]);
                for (int c = 0; c < 4; c++) if (uvs[c] != null) oUv[c].Add(uvs[c][v]);
                oW.Add(IsCut(slot) ? Cut(v, slot) : Rest(v));
                return i;
            }

            var subs = new List<int[]>(); int cutTris = 0, tris = 0;
            var counts = new Dictionary<Piece, int>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var idx = src.GetTriangles(s); var outIdx = new int[idx.Length];
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    Piece a = P(dom[idx[t]]), b = P(dom[idx[t + 1]]), c = P(dom[idx[t + 2]]);
                    Piece g = a == b || a == c ? a : b == c ? b : (Distal(a) >= Distal(b) && Distal(a) >= Distal(c) ? a : Distal(b) >= Distal(c) ? b : c);
                    var slot = IsCut(g) ? g : Piece.Body;
                    for (int k = 0; k < 3; k++) outIdx[t + k] = Vert(idx[t + k], slot);
                    tris++; if (IsCut(g)) { cutTris++; counts[g] = counts.TryGetValue(g, out var n) ? n + 1 : 1; }
                }
                subs.Add(outIdx);
            }
            var mesh = new Mesh { name = src.name + "_RigidArms", indexFormat = oPos.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(oPos); if (oNrm.Count > 0) mesh.SetNormals(oNrm); if (oTan.Count > 0) mesh.SetTangents(oTan); if (oCol.Count > 0) mesh.SetColors(oCol);
            for (int c = 0; c < 4; c++) if (oUv[c] != null) mesh.SetUVs(c, oUv[c]);
            mesh.subMeshCount = subs.Count; for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.boneWeights = oW.ToArray(); mesh.bindposes = src.bindposes; mesh.bounds = src.bounds;
            var path = PathFor(src);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            log?.AppendLine($"rigid skin {src.name}: {cutTris}/{tris} triangles cut into rigid pieces ({string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value))}); {oPos.Count - pos.Length} seam vertices duplicated ({path})");
            return mesh;
        }
    }
}
