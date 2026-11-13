using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // F8 build: the Tripo export has 99 skinned parts, 99 materials and auto (rubbery) weights.
    // This bakes it into one hard-skinned body mesh (each UV island rides one bone, like real mech
    // plates) plus a separate rigid cannon mesh on the right forearm, sharing one atlas material.
    public static partial class VisualPassMech
    {
        public const string OutDir = Dir + "/Built";
        public const string VisualPrefab = "Assets/IronRequiem/Prefabs/VisualPass/Mech/T07Mech_Visual.prefab";
        public const string CannonBone = "Right_LowerArm";

        public static string Collapse(string n)
        {
            foreach (var side in new[] { "Left_", "Right_" })
                if (n.StartsWith(side) && (n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Pinky"))) return side + "Hand";
            if (n.Contains("Eye") || n.StartsWith("Neck_Twist")) return "Head";
            if (n.EndsWith("ToesEnd")) return n.Replace("ToesEnd", "Toes");
            return n;
        }
        static bool IsCannon(string bone) => bone == "Right_LowerArm" || bone == "Right_Hand";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        class Builder
        {
            public readonly List<Vector3> v = new(); public readonly List<Vector3> n = new(); public readonly List<Vector2> uv = new();
            public readonly List<int> tri = new(); public readonly List<int> bone = new();
            readonly Dictionary<(int part, int vtx), int> map = new();
            public int Add(int part, int vtx, Vector3 p, Vector3 nn, Vector2 t, int b)
            {
                if (map.TryGetValue((part, vtx), out var i)) return i;
                i = v.Count; map[(part, vtx)] = i; v.Add(p); n.Add(nn); uv.Add(t); bone.Add(b); return i;
            }
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F8 Mech 2 build visual")]
        static void BuildVisual()
        {
            EnsureFolder(OutDir); EnsureFolder(System.IO.Path.GetDirectoryName(VisualPrefab).Replace('\\', '/'));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var root = Object.Instantiate(model); root.name = "Model";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            var wrapper = new GameObject("T07Mech_Visual");
            var log = new StringBuilder("[F8 build]\n");
            try
            {
                var byName = new Dictionary<string, Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName[t.name] = t;
                var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                // ---- atlas
                var texList = new List<Texture2D>(); var texIndex = new Dictionary<Texture2D, int>();
                // the FBX materials lose their textures on import; part N always uses ..._part_N_basecolor
                Texture2D TexFor(SkinnedMeshRenderer s)
                {
                    var t = s.sharedMaterial ? s.sharedMaterial.mainTexture as Texture2D : null; if (t) return t;
                    var id = s.gameObject.name.Substring(s.gameObject.name.LastIndexOf('_') + 1);
                    return AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/T07Mech_Rigged.fbm/mech_robot_3d_model_tripo_part_{id}_basecolor.JPEG");
                }
                foreach (var s in smrs)
                {
                    var t = TexFor(s);
                    if (t && !texIndex.ContainsKey(t)) { texIndex[t] = texList.Count; texList.Add(t); }
                }
                var atlas = new Texture2D(4096, 4096, TextureFormat.RGBA32, false);
                var rects = atlas.PackTextures(texList.ToArray(), 4, 4096, false);
                var atlasPath = OutDir + "/T07Mech_Atlas.png";
                System.IO.File.WriteAllBytes(atlasPath, atlas.EncodeToPNG()); Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(atlasPath);
                var ati = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
                ati.maxTextureSize = 4096; ati.textureCompression = TextureImporterCompression.CompressedHQ; ati.mipmapEnabled = true; ati.sRGBTexture = true; ati.SaveAndReimport();
                log.AppendLine($"atlas textures={texList.Count} missingTex={smrs.Count(s => !TexFor(s))} atlasRect0={rects.FirstOrDefault()}");

                // ---- bake
                var body = new Builder(); var cannon = new Builder();
                var bodyBones = new List<Transform>(); var boneSlot = new Dictionary<Transform, int>();
                var cannonT = byName[CannonBone];
                Matrix4x4 toCannon = cannonT.worldToLocalMatrix;
                int islands = 0, weakIslands = 0, part = 0;
                foreach (var s in smrs)
                {
                    var m = s.sharedMesh; var bones = s.bones; var bind = m.bindposes;
                    var verts = m.vertices; var norms = m.normals; var uvs = m.uv;
                    var bpv = m.GetBonesPerVertex(); var all = m.GetAllBoneWeights();
                    var skin = new Matrix4x4[bones.Length]; var collapsed = new string[bones.Length];
                    for (int i = 0; i < bones.Length; i++) { skin[i] = bones[i] ? bones[i].localToWorldMatrix * bind[i] : s.transform.localToWorldMatrix; collapsed[i] = bones[i] ? Collapse(bones[i].name) : "Hips"; }
                    var tex = TexFor(s);
                    var rect = tex ? rects[texIndex[tex]] : new Rect(0, 0, 0, 0);
                    int nv = verts.Length; var P = new Vector3[nv]; var N = new Vector3[nv];
                    var vw = new Dictionary<string, float>[nv]; int k = 0;
                    for (int vi = 0; vi < nv; vi++)
                    {
                        int c = bpv.Length > 0 ? bpv[vi] : 0; var d = new Dictionary<string, float>();
                        if (c == 0) { P[vi] = s.transform.localToWorldMatrix.MultiplyPoint3x4(verts[vi]); N[vi] = s.transform.localToWorldMatrix.MultiplyVector(norms[vi]).normalized; d["Hips"] = 1; }
                        else
                        {
                            Vector3 p = default, nn = default;
                            for (int j = 0; j < c; j++)
                            {
                                var w = all[k++]; p += skin[w.boneIndex].MultiplyPoint3x4(verts[vi]) * w.weight; nn += skin[w.boneIndex].MultiplyVector(norms[vi]) * w.weight;
                                d.TryGetValue(collapsed[w.boneIndex], out var acc); d[collapsed[w.boneIndex]] = acc + w.weight;
                            }
                            P[vi] = p; N[vi] = nn.normalized;
                        }
                        vw[vi] = d;
                    }
                    var tris = m.triangles;
                    // islands by shared vertex index (union-find)
                    var uf = Enumerable.Range(0, nv).ToArray();
                    int Find(int x) { while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; } return x; }
                    for (int t = 0; t < tris.Length; t += 3) { int a = Find(tris[t]); uf[Find(tris[t + 1])] = a; uf[Find(tris[t + 2])] = a; }
                    var seen = new Dictionary<Vector3Int, int>();
                    for (int vi = 0; vi < nv; vi++)
                    {
                        var key = Vector3Int.RoundToInt(P[vi] * 20000f);
                        if (seen.TryGetValue(key, out var other)) uf[Find(vi)] = Find(other); else seen[key] = vi;
                    }
                    var islandBone = new Dictionary<int, string>(); var sums = new Dictionary<int, Dictionary<string, float>>();
                    for (int vi = 0; vi < nv; vi++)
                    {
                        int r = Find(vi); if (!sums.TryGetValue(r, out var acc)) sums[r] = acc = new Dictionary<string, float>();
                        foreach (var kv in vw[vi]) { acc.TryGetValue(kv.Key, out var x); acc[kv.Key] = x + kv.Value; }
                    }
                    foreach (var kv in sums)
                    {
                        var best = kv.Value.OrderByDescending(x => x.Value).First(); float tot = kv.Value.Values.Sum();
                        islandBone[kv.Key] = best.Value / tot >= .6f ? best.Key : null; islands++; if (best.Value / tot < .6f) weakIslands++;
                    }
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        // rigid island, or (for an island spanning a joint) the triangle's dominant vertex bone
                        var boneName = islandBone[Find(tris[t])];
                        if (boneName == null)
                        {
                            var acc = new Dictionary<string, float>();
                            for (int j = 0; j < 3; j++) foreach (var kv in vw[tris[t + j]]) { acc.TryGetValue(kv.Key, out var x); acc[kv.Key] = x + kv.Value; }
                            boneName = acc.OrderByDescending(x => x.Value).First().Key;
                        }
                        bool isCannon = IsCannon(boneName);
                        var target = isCannon ? cannon : body; int slot = 0;
                        if (!isCannon)
                        {
                            var bt = byName.TryGetValue(boneName, out var x) ? x : byName["Hips"];
                            if (!boneSlot.TryGetValue(bt, out slot)) { slot = bodyBones.Count; boneSlot[bt] = slot; bodyBones.Add(bt); }
                        }
                        for (int j = 0; j < 3; j++)
                        {
                            int vi = tris[t + j];
                            var p = isCannon ? toCannon.MultiplyPoint3x4(P[vi]) : P[vi];
                            var nn = isCannon ? toCannon.MultiplyVector(N[vi]).normalized : N[vi];
                            var u = uvs.Length > vi ? uvs[vi] : Vector2.zero;
                            u = new Vector2(rect.x + u.x * rect.width, rect.y + u.y * rect.height);
                            target.tri.Add(target.Add(part, vi, p, nn, u, slot));
                        }
                    }
                    part++;
                }
                log.AppendLine($"islands={islands} weak(<60%)={weakIslands} body v={body.v.Count} t={body.tri.Count / 3} bones={bodyBones.Count} cannon v={cannon.v.Count} t={cannon.tri.Count / 3}");
                log.AppendLine("body bones: " + string.Join(",", bodyBones.Select(b => b.name)));

                // ---- assets
                var mat = AssetDatabase.LoadAssetAtPath<Material>(OutDir + "/T07Mech_Atlas.mat");
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, OutDir + "/T07Mech_Atlas.mat"); }
                var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
                mat.SetTexture("_BaseMap", atlasTex); mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Metallic", .55f); mat.SetFloat("_Smoothness", .42f);
                mat.enableInstancing = true; EditorUtility.SetDirty(mat);

                var bodyMesh = Save(new Mesh { name = "T07Mech_Body" }, OutDir + "/T07Mech_Body.asset");
                bodyMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                bodyMesh.SetVertices(body.v); bodyMesh.SetNormals(body.n); bodyMesh.SetUVs(0, body.uv); bodyMesh.SetTriangles(body.tri, 0);
                bodyMesh.boneWeights = body.bone.Select(b => new BoneWeight { boneIndex0 = b, weight0 = 1 }).ToArray();
                bodyMesh.bindposes = bodyBones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                bodyMesh.RecalculateTangents(); bodyMesh.RecalculateBounds(); EditorUtility.SetDirty(bodyMesh);

                var cannonMesh = Save(new Mesh { name = "T07Mech_Cannon" }, OutDir + "/T07Mech_Cannon.asset");
                cannonMesh.SetVertices(cannon.v); cannonMesh.SetNormals(cannon.n); cannonMesh.SetUVs(0, cannon.uv); cannonMesh.SetTriangles(cannon.tri, 0);
                cannonMesh.RecalculateTangents(); cannonMesh.RecalculateBounds(); EditorUtility.SetDirty(cannonMesh);

                // ---- rebuild the hierarchy: drop the 99 parts, keep the skeleton + Animator
                foreach (var s in smrs) Object.DestroyImmediate(s.gameObject);
                var bodyGo = new GameObject("T07Mech_Body"); bodyGo.transform.SetParent(root.transform, false);
                var smr = bodyGo.AddComponent<SkinnedMeshRenderer>();
                smr.bones = bodyBones.ToArray(); smr.rootBone = byName["Hips"]; smr.sharedMesh = bodyMesh; smr.sharedMaterial = mat;
                smr.quality = SkinQuality.Bone1; smr.updateWhenOffscreen = false;
                var cannonGo = new GameObject("T07Mech_Cannon"); cannonGo.transform.SetParent(cannonT, false);
                cannonGo.AddComponent<MeshFilter>().sharedMesh = cannonMesh; cannonGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
                BuildPieces(root.transform, byName, body, bodyBones, cannon, cannonT, mat, log);
                var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                animator.avatar = model.GetComponent<Animator>()?.avatar; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // the Tripo export faces ~45 deg off; the wrapper turns it so the mech walks along +Z
                var rig = new Rig(root);
                root.transform.SetParent(wrapper.transform, false);
                root.transform.localRotation = Quaternion.FromToRotation(rig.fwd, Vector3.forward);
                log.AppendLine($"facing fix yaw={root.transform.localEulerAngles.y:0.0}");
                var b0 = smr.bounds; b0.Encapsulate(cannonGo.GetComponent<MeshRenderer>().bounds);
                log.AppendLine($"visual bounds c={b0.center.ToString("F3")} s={b0.size.ToString("F3")}");
                PrefabUtility.SaveAsPrefabAsset(wrapper, VisualPrefab);
                AssetDatabase.SaveAssets();
                log.AppendLine("saved " + VisualPrefab);
            }
            finally { Object.DestroyImmediate(wrapper); if (root) Object.DestroyImmediate(root); Debug.Log(log.ToString()); }
        }

        // Death explosion chunks: the body split by bone group, each mesh centred on its own pivot, parked
        // inactive under its anchor bone so it starts exactly where that part of the mech is.
        static readonly (string name, string anchor, string[] bones)[] PieceGroups =
        {
            ("Head", "Head", new[] { "Head", "Neck" }), ("Torso", "Chest", new[] { "Chest", "UpperChest" }), ("Pelvis", "Hips", new[] { "Hips", "Spine" }),
            ("ShoulderL", "Left_Shoulder", new[] { "Left_Shoulder" }), ("ShoulderR", "Right_Shoulder", new[] { "Right_Shoulder" }),
            ("ArmL", "Left_UpperArm", new[] { "Left_UpperArm" }), ("ForearmL", "Left_LowerArm", new[] { "Left_LowerArm", "Left_Hand" }), ("ArmR", "Right_UpperArm", new[] { "Right_UpperArm" }),
            ("ThighL", "Left_UpperLeg", new[] { "Left_UpperLeg" }), ("ShinL", "Left_LowerLeg", new[] { "Left_LowerLeg" }), ("FootL", "Left_Foot", new[] { "Left_Foot", "Left_Toes" }),
            ("ThighR", "Right_UpperLeg", new[] { "Right_UpperLeg" }), ("ShinR", "Right_LowerLeg", new[] { "Right_LowerLeg" }), ("FootR", "Right_Foot", new[] { "Right_Foot", "Right_Toes" }),
        };

        static void BuildPieces(Transform model, Dictionary<string, Transform> byName, Builder body, List<Transform> bodyBones, Builder cannon, Transform cannonT, Material mat, StringBuilder log)
        {
            EnsureFolder(OutDir + "/Pieces");
            var groupOf = new Dictionary<string, int>();
            for (int g = 0; g < PieceGroups.Length; g++) foreach (var b in PieceGroups[g].bones) groupOf[b] = g;
            var tris = new List<int>[PieceGroups.Length]; for (int g = 0; g < tris.Length; g++) tris[g] = new List<int>();
            for (int t = 0; t < body.tri.Count; t += 3)
            {
                var bone = bodyBones[body.bone[body.tri[t]]].name;
                tris[groupOf.TryGetValue(bone, out var g) ? g : 1].AddRange(new[] { body.tri[t], body.tri[t + 1], body.tri[t + 2] });
            }
            int made = 0;
            for (int g = 0; g < PieceGroups.Length; g++)
            {
                if (tris[g].Count == 0) continue;
                var anchor = byName[PieceGroups[g].anchor];
                MakePiece("Piece_" + PieceGroups[g].name, anchor, tris[g], body.v, body.n, body.uv, anchor.worldToLocalMatrix, mat); made++;
            }
            // the cannon chunk (separate from the detachable module art, which is hidden when it flies)
            MakePiece("Piece_Cannon", cannonT, cannon.tri, cannon.v, cannon.n, cannon.uv, Matrix4x4.identity, mat); made++;
            log.AppendLine($"debris pieces={made}");
        }

        static void MakePiece(string name, Transform anchor, List<int> tri, List<Vector3> v, List<Vector3> n, List<Vector2> uv, Matrix4x4 toLocal, Material mat)
        {
            var map = new Dictionary<int, int>(); var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pu = new List<Vector2>(); var pt = new List<int>();
            foreach (var i in tri)
            {
                if (!map.TryGetValue(i, out var j)) { j = pv.Count; map[i] = j; pv.Add(toLocal.MultiplyPoint3x4(v[i])); pn.Add(toLocal.MultiplyVector(n[i]).normalized); pu.Add(uv[i]); }
                pt.Add(j);
            }
            var c = Vector3.zero; foreach (var p in pv) c += p; c /= Mathf.Max(1, pv.Count);
            for (int i = 0; i < pv.Count; i++) pv[i] -= c;
            var mesh = Save(new Mesh { name = "T07Mech_" + name }, $"{OutDir}/Pieces/T07Mech_{name}.asset");
            if (pv.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(pv); mesh.SetNormals(pn); mesh.SetUVs(0, pu); mesh.SetTriangles(pt, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var go = new GameObject(name); go.transform.SetParent(anchor, false); go.transform.localPosition = c;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            go.AddComponent<IronRequiem.MechDebrisPiece>(); go.SetActive(false);
        }

        static Mesh Save(Mesh fresh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { existing.Clear(); return existing; }
            AssetDatabase.CreateAsset(fresh, path); return fresh;
        }
    }
}
