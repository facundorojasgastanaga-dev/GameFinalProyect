using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem.EditorTools
{
    /// Task 4: Carroñero, the horde saw bot. Source: Tripo export (one skinned mesh, Mixamo rig, saw fused to the
    /// left forearm, no LODs). Built as a hard-skinned body (collapsed rig) + separate spinning saw + 4 LODs,
    /// on top of the existing SawBot gameplay (EnemyController, EnemyMobile, Health, SawBotCombat sustained saw).
    public static class VisualPassCarronero
    {
        public const string Dir = "Assets/IronRequiem/Art/Enemies/Carronero";
        const string Src = Dir + "/Source";
        const string Fbx = Src + "/Carronero.fbx";
        const string Built = Dir + "/Built";
        public const string VisualPrefab = Dir + "/Carronero_Visual.prefab";
        public const string EnemyPrefab = "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab";
        const string Template = "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab";
        const float TargetHeight = 1.15f;

        // saw disc in the Tripo source frame (Y up, 0..1 tall, saw on +X/+Z), measured offline from the FBX
        static readonly Vector3 SawCenterSrc = new Vector3(.36f, .265f, .23f);
        const float SawRadiusSrc = .155f, SawMinXSrc = .27f;

        static readonly string[] Keep = { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "RightShoulder", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase" };

        static string Short(string n) { int i = n.LastIndexOf(':'); return i >= 0 ? n.Substring(i + 1) : n; }
        [MenuItem("Tools/Iron Requiem/Visual Pass/C1 Carronero import settings")]
        public static void ImportSettings()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            mi.animationType = ModelImporterAnimationType.Generic; mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.isReadable = true; mi.importAnimation = false;
            mi.SaveAndReimport();
            void Tex(string p, bool normal, bool linear, int max)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(p); if (!ti) { Debug.LogWarning("[C1] missing " + p); return; }
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = !linear && !normal;
                ti.maxTextureSize = max; ti.mipmapEnabled = true; ti.SaveAndReimport();
            }
            Tex(Src + "/Carronero_Albedo.jpg", false, false, 1024);
            Tex(Src + "/Carronero_Normal.jpg", true, true, 1024);
            Tex(Src + "/Carronero_MetalSmooth.png", false, true, 512);
            Debug.Log("[C1] Carronero import settings applied");
        }

        class Mb
        {
            public readonly List<Vector3> v = new(); public readonly List<Vector3> n = new(); public readonly List<Vector2> uv = new();
            public readonly List<int> bone = new(); public readonly List<int> tri = new();
            public Mesh ToMesh(string name, Matrix4x4[] bind)
            {
                var m = new Mesh { name = name }; m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
                if (bind != null) { m.boneWeights = bone.Select(b => new BoneWeight { boneIndex0 = b, weight0 = 1 }).ToArray(); m.bindposes = bind; }
                m.RecalculateBounds(); m.RecalculateTangents(); return m;
            }
        }

        // Vertex clustering per bone (plates never merge across joints). cell in metres.
        static Mb Decimate(Mb src, float cell)
        {
            var dst = new Mb(); var map = new Dictionary<(int, int, int, int), int>(); var remap = new int[src.v.Count]; var acc = new List<int>();
            for (int i = 0; i < src.v.Count; i++)
            {
                var p = src.v[i]; var k = (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell), src.bone[i]);
                if (!map.TryGetValue(k, out var id)) { id = dst.v.Count; map[k] = id; dst.v.Add(Vector3.zero); dst.n.Add(Vector3.zero); dst.uv.Add(src.uv[i]); dst.bone.Add(src.bone[i]); acc.Add(0); }
                dst.v[id] += p; dst.n[id] += src.n[i]; acc[id]++; remap[i] = id;
            }
            for (int i = 0; i < dst.v.Count; i++) { dst.v[i] /= acc[i]; dst.n[i] = dst.n[i].normalized; }
            var seen = new HashSet<(int, int, int)>();
            for (int t = 0; t < src.tri.Count; t += 3)
            {
                int a = remap[src.tri[t]], b = remap[src.tri[t + 1]], c = remap[src.tri[t + 2]];
                if (a == b || b == c || a == c) continue;
                var key = a < b ? (a < c ? (a, b, c) : (c, a, b)) : (b < c ? (b, c, a) : (c, a, b));
                if (!seen.Add(key)) continue;
                dst.tri.Add(a); dst.tri.Add(b); dst.tri.Add(c);
            }
            return dst;
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/C2 Carronero build visual (saw + 4 LODs)")]
        public static void BuildVisual()
        {
            var log = new StringBuilder("[C2 Carronero visual]\n");
            VisualPassMech.EnsureFolder(Built);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx); if (!src) { Debug.LogError("[C2] missing " + Fbx); return; }
            var inst = Object.Instantiate(src); inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); inst.transform.localScale = Vector3.one;
            try
            {
                var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true); var mesh = smr.sharedMesh;
                var srcBones = smr.bones; var bw = mesh.boneWeights; var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv; var tris = mesh.triangles;
                // vertices in model-root space, bind pose
                var toRoot = inst.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                var P = verts.Select(p => toRoot.MultiplyPoint3x4(p)).ToArray(); var N = norms.Select(q => toRoot.MultiplyVector(q).normalized).ToArray();
                var b = new Bounds(P[0], Vector3.zero); foreach (var p in P) b.Encapsulate(p);
                float k = b.size.y; // source is ~1 unit tall
                log.AppendLine($"source v={verts.Length} tris={tris.Length / 3} bones={srcBones.Length} bounds c={b.center.ToString("F3")} s={b.size.ToString("F3")}");

                // collapsed rig: keep core bones, fingers -> hand
                var byShort = srcBones.Where(t => t).GroupBy(t => Short(t.name)).ToDictionary(g => g.Key, g => g.First());
                var keep = Keep.Where(byShort.ContainsKey).Select(n => byShort[n]).ToList();
                int BoneOf(int srcIndex)
                {
                    for (var c = srcBones[srcIndex]; c; c = c.parent) { int i = keep.IndexOf(c); if (i >= 0) return i; }
                    return 0;
                }
                var boneMap = new int[srcBones.Length]; for (int i = 0; i < srcBones.Length; i++) boneMap[i] = BoneOf(i);
                int Dom(int v) { var w = bw[v]; int i = w.boneIndex0; float m = w.weight0; if (w.weight1 > m) { m = w.weight1; i = w.boneIndex1; } if (w.weight2 > m) { m = w.weight2; i = w.boneIndex2; } if (w.weight3 > m) i = w.boneIndex3; return boneMap[i]; }

                // which side carries the saw: the LeftForeArm side, front = the side the forearm reaches
                int lf = keep.FindIndex(t => Short(t.name) == "LeftForeArm"); int foot = keep.FindIndex(t => Short(t.name) == "LeftFoot"), toe = keep.FindIndex(t => Short(t.name) == "LeftToeBase");
                Vector3 Cent(int bone) { var s = Vector3.zero; int c = 0; for (int i = 0; i < P.Length; i++) if (Dom(i) == bone) { s += P[i]; c++; } return c > 0 ? s / c : Vector3.zero; }
                var lfc = Cent(lf); float sx = Mathf.Sign(lfc.x - b.center.x), sz = Mathf.Sign(Cent(toe).z - Cent(foot).z);
                Vector3 ToSrc(Vector3 p) => new Vector3((p.x - b.center.x) * sx / k, (p.y - b.min.y) / k, (p.z - b.center.z) * sz / k);
                Vector3 FromSrc(Vector3 s) => new Vector3(s.x * sx * k + b.center.x, s.y * k + b.min.y, s.z * sz * k + b.center.z);
                log.AppendLine($"saw side sx={sx} front sz={sz} (flip={(sz < 0)})");

                bool IsSaw(int i)
                {
                    var s = ToSrc(P[i]); if (s.x < SawMinXSrc) return false;
                    var d = new Vector2(s.z - SawCenterSrc.z, s.y - SawCenterSrc.y); float r = d.magnitude; if (r > SawRadiusSrc) return false;
                    float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; // 0 = forward, 90 = up; the forearm enters from up-back
                    return !(r > .05f && ang > 100 && ang < 170);
                }
                var saw = new bool[P.Length]; int sawCount = 0; for (int i = 0; i < P.Length; i++) if (saw[i] = IsSaw(i)) sawCount++;

                // scale to gameplay height, feet on the ground, centred
                float scale = TargetHeight / k; var offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                var face = sz < 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity; // gameplay forward is +Z
                Vector3 Fit(Vector3 p) => face * ((p + offset) * scale);

                // new rig: copies of the kept bones under a fresh root (scaled with the model)
                var model = new GameObject("Carronero_Visual"); var rigRoot = new GameObject("Rig").transform; rigRoot.SetParent(model.transform, false);
                var newBones = new Transform[keep.Count];
                for (int i = 0; i < keep.Count; i++)
                {
                    var t = new GameObject(Short(keep[i].name)).transform; newBones[i] = t;
                }
                for (int i = 0; i < keep.Count; i++)
                {
                    Transform parent = rigRoot; for (var c = keep[i].parent; c; c = c.parent) { int j = keep.IndexOf(c); if (j >= 0) { parent = newBones[j]; break; } }
                    newBones[i].SetParent(parent, false);
                }
                // world placement of new bones = old bone positions fitted, rotations kept
                foreach (var i in Enumerable.Range(0, keep.Count).OrderBy(i => Depth(keep[i])))
                    newBones[i].SetPositionAndRotation(Fit(inst.transform.InverseTransformPoint(keep[i].position)), face * Quaternion.Inverse(inst.transform.rotation) * keep[i].rotation);

                var bind = newBones.Select(t => t.worldToLocalMatrix * model.transform.localToWorldMatrix).ToArray();
                var body = new Mb(); var sawMb = new Mb();
                var sawCenter = Fit(FromSrc(SawCenterSrc));
                var sawGo = new GameObject("SawSpin"); sawGo.transform.position = sawCenter; sawGo.transform.SetParent(newBones[lf], true);
                // the disc spins about the source X axis (its normal), expressed in the saw transform
                var sawAxisWorld = face * new Vector3(sx, 0, 0);
                var bodyMap = new Dictionary<int, int>(); var sawMap = new Dictionary<int, int>();
                int Add(Mb m, Dictionary<int, int> map, int i, bool rigid)
                {
                    if (map.TryGetValue(i, out var id)) return id; id = m.v.Count; map[i] = id;
                    var p = Fit(P[i]);
                    m.v.Add(rigid ? sawGo.transform.InverseTransformPoint(p) : p); var nn = face * N[i]; m.n.Add(rigid ? sawGo.transform.InverseTransformDirection(nn) : nn); m.uv.Add(uvs[i]); m.bone.Add(Dom(i)); return id;
                }
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], c1 = tris[t + 1], c2 = tris[t + 2];
                    bool s = saw[a] && saw[c1] && saw[c2];
                    var m = s ? sawMb : body; var map = s ? sawMap : bodyMap;
                    m.tri.Add(Add(m, map, a, s)); m.tri.Add(Add(m, map, c1, s)); m.tri.Add(Add(m, map, c2, s));
                }
                log.AppendLine($"saw verts={sawCount} saw mesh v={sawMb.v.Count} t={sawMb.tri.Count / 3} body v={body.v.Count} t={body.tri.Count / 3} scale={scale:0.###}");

                // materials
                var mat = MakeMat(Built + "/Carronero_Body.mat", false); var sawMat = MakeMat(Built + "/Carronero_Saw.mat", true);

                // LODs: 0 full, 1/2/3 clustered (cells relative to height)
                var lodMeshes = new Mesh[4]; var cells = new[] { 0f, TargetHeight / 42, TargetHeight / 22, TargetHeight / 10 };
                for (int l = 0; l < 4; l++)
                {
                    var mb = l == 0 ? body : Decimate(body, cells[l]);
                    float pre = MaxEdge(mb.v, mb.tri);
                    var fresh = mb.ToMesh("Carronero_Body_LOD" + l, bind); float built = MaxEdge(fresh.vertices.ToList(), fresh.triangles.ToList());
                    lodMeshes[l] = Save(fresh, $"{Built}/Carronero_Body_LOD{l}.asset");
                    log.AppendLine($"  LOD{l} v={lodMeshes[l].vertexCount} t={lodMeshes[l].triangles.Length / 3} maxEdge pre={pre:0.###} mesh={built:0.###} saved={MaxEdge(lodMeshes[l].vertices.ToList(), lodMeshes[l].triangles.ToList()):0.###} bounds={lodMeshes[l].bounds.size.ToString("F2")}");
                }
                var sawMesh = Save(sawMb.ToMesh("Carronero_Saw", null), Built + "/Carronero_Saw.asset");
                var sawLow = Save(Decimate(sawMb, TargetHeight / 40).ToMesh("Carronero_Saw_LOD2", null), Built + "/Carronero_Saw_LOD2.asset");

                var renderers = new Renderer[4];
                for (int l = 0; l < 4; l++)
                {
                    var go = new GameObject("Body_LOD" + l); go.transform.SetParent(model.transform, false);
                    var r = go.AddComponent<SkinnedMeshRenderer>(); r.sharedMesh = lodMeshes[l]; r.bones = newBones; r.rootBone = newBones[0]; r.sharedMaterial = mat;
                    r.updateWhenOffscreen = false; r.quality = SkinQuality.Bone1; r.localBounds = new Bounds(newBones[0].InverseTransformPoint(model.transform.position + Vector3.up * TargetHeight * .5f), Vector3.one * TargetHeight * 1.3f);
                    r.shadowCastingMode = l >= 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                    renderers[l] = r;
                }
                sawGo.AddComponent<MeshFilter>().sharedMesh = sawMesh; var sawR = sawGo.AddComponent<MeshRenderer>(); sawR.sharedMaterial = sawMat;
                var sawLowGo = new GameObject("SawSpin_Low"); sawLowGo.transform.SetParent(sawGo.transform, false);
                sawLowGo.AddComponent<MeshFilter>().sharedMesh = sawLow; var sawLowR = sawLowGo.AddComponent<MeshRenderer>(); sawLowR.sharedMaterial = sawMat; sawLowR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var lod = model.AddComponent<LODGroup>();
                lod.SetLODs(new[]
                {
                    new LOD(.25f, new Renderer[] { renderers[0], sawR }),
                    new LOD(.10f, new Renderer[] { renderers[1], sawR }),
                    new LOD(.04f, new Renderer[] { renderers[2], sawLowR }),
                    new LOD(.012f, new Renderer[] { renderers[3] }),
                });
                lod.RecalculateBounds();

                // blade capsule endpoints for SawBotCombat contact (front arc of the disc)
                var axisLocal = sawGo.transform.InverseTransformDirection(sawAxisWorld).normalized;
                var bladeA = new GameObject("BladeStart").transform; bladeA.SetParent(sawGo.transform, false);
                var bladeB = new GameObject("BladeEnd").transform; bladeB.SetParent(sawGo.transform, false);
                // blade edge in the model: forward-low and forward-high points of the rim (world, unaffected by spin since parent is SawSpin... keep them on the forearm instead)
                bladeA.SetParent(newBones[lf], true); bladeB.SetParent(newBones[lf], true);
                bladeA.position = sawCenter + Vector3.forward * SawRadiusSrc * k * scale * .8f + Vector3.down * SawRadiusSrc * k * scale * .5f;
                bladeB.position = sawCenter + Vector3.forward * SawRadiusSrc * k * scale * .8f + Vector3.up * SawRadiusSrc * k * scale * .5f;

                var motionInfo = model.AddComponent<CarroneroRigInfo>();
                motionInfo.bones = newBones; motionInfo.saw = sawGo.transform; motionInfo.sawAxisLocal = axisLocal; motionInfo.bladeStart = bladeA; motionInfo.bladeEnd = bladeB;

                PrefabUtility.SaveAsPrefabAsset(model, VisualPrefab);
                Object.DestroyImmediate(model);
                log.AppendLine("saved " + VisualPrefab);
            }
            finally { Object.DestroyImmediate(inst); AssetDatabase.SaveAssets(); Debug.Log(log.ToString()); }
        }


        [MenuItem("Tools/Iron Requiem/Visual Pass/C3 Carronero build enemy prefab")]
        public static void BuildEnemy()
        {
            var log = new StringBuilder("[C3 Carronero enemy]\n");
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab)) { Debug.LogError("[C3] run C2 first"); return; }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab))
            {
                // same GUID kept afterwards: rebuilds edit this prefab in place
                if (!AssetDatabase.CopyAsset(Template, EnemyPrefab)) { Debug.LogError("[C3] copy failed"); return; }
                log.AppendLine("created " + EnemyPrefab + " from " + Template);
            }
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefab);
            try
            {
                foreach (var old in root.GetComponentsInChildren<CarroneroRigInfo>(true).Select(c => c.gameObject).ToArray()) Object.DestroyImmediate(old);
                // old saw-bot art stays as hidden gameplay sockets (wheels, chains, attack origin); its animator culls itself
                int hidden = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r is MeshRenderer || r is SkinnedMeshRenderer) { if (r.enabled) hidden++; r.enabled = false; }
                foreach (var a in root.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.CullCompletely;
                foreach (var g in root.GetComponentsInChildren<LODGroup>(true)) g.enabled = false;

                var vis = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab), root.transform);
                PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                vis.transform.localPosition = Vector3.zero; vis.transform.localRotation = Quaternion.identity;
                vis.transform.localScale = new Vector3(1 / root.transform.lossyScale.x, 1 / root.transform.lossyScale.y, 1 / root.transform.lossyScale.z);
                var info = vis.GetComponent<CarroneroRigInfo>();

                var motion = root.GetComponent<CarroneroMotion>() ?? root.AddComponent<CarroneroMotion>();
                motion.Configure(info.Bone("Hips"), info.Bone("Spine1") ?? info.Bone("Spine"), info.Bone("LeftUpLeg"), info.Bone("LeftLeg"), info.Bone("RightUpLeg"), info.Bone("RightLeg"),
                    info.Bone("LeftArm"), info.Bone("LeftForeArm"), info.Bone("RightArm"), info.Bone("Head"), info.saw, info.sawAxisLocal, TargetHeight * .5f);

                // gameplay: weakest enemy, fast, sustained contact saw on the new blade
                var combat = root.GetComponent<SawBotCombat>();
                var so = new SerializedObject(combat);
                void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else log.AppendLine("  missing field " + n); }
                void O(string n, Object v) { var p = so.FindProperty(n); if (p != null) p.objectReferenceValue = v; else log.AppendLine("  missing field " + n); }
                so.FindProperty("sustainedSaw").boolValue = true;
                O("leftBladeStart", info.bladeStart); O("leftBladeEnd", info.bladeEnd); O("rightBladeStart", null); O("rightBladeEnd", null);
                F("moveSpeed", 7.2f); F("acceleration", 26); F("damagePerSecond", 4.5f); F("damageInterval", .12f); F("contactRadius", .24f); F("reach", 1.05f); F("exitReach", 1.35f); F("preparation", .35f);
                so.ApplyModifiedPropertiesWithoutUndo();

                var health = root.GetComponent<Health>(); health.MaxHealth = 26;
                var agent = root.GetComponent<NavMeshAgent>();
                agent.radius = .32f; agent.height = TargetHeight; agent.speed = 7.2f; agent.acceleration = 26; agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

                // horde cost: fewer path requests, throttled visibility rays, cheap avoidance, low audio priority
                var ec = root.GetComponent<EnemyController>(); ec.DestinationUpdateThreshold = .6f; ec.DestinationRefreshInterval = .3f;
                foreach (var dm in root.GetComponentsInChildren<DetectionModule>(true)) dm.DetectionInterval = .1f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                foreach (var au in root.GetComponentsInChildren<AudioSource>(true)) { au.priority = 200; au.maxDistance = Mathf.Min(au.maxDistance, 30); } // first to go virtual when 20+ play
                // template presentation that only drove the hidden saw-bot art: flash/kick on the old torso, state lights,
                // per-frame body flash property blocks. Removed to save per-bot work in a 20+ horde.
                ec.BodyMaterial = null;
                foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true).ToArray())
                    if (c && (c is RobotHitFeedback || c is EnemyStateLights)) { log.AppendLine("  removed " + c.GetType().Name); Object.DestroyImmediate(c); }
                // the motor keeps its sound; chain mesh swapping and exhaust smoke belonged to the hidden saw-bot art
                foreach (var smp in root.GetComponentsInChildren<SawMotorPresentation>(true))
                {
                    var mso = new SerializedObject(smp);
                    mso.FindProperty("chains").arraySize = 0; mso.FindProperty("smoke").arraySize = 0; mso.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var ch in root.GetComponentsInChildren<SawChainMotion>(true)) ch.enabled = false;
                // not executable, not stealable
                int removed = 0;
                for (int pass = 0; pass < 2; pass++) // dependents (weapon source) go before the execution source they require
                    foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true).ToArray())
                        if (c && (pass == 0 ? (c is SawBotWeaponSource || c is IStolenWeaponBinding) : c is EnemyExecutionSource)) { log.AppendLine("  removed " + c.GetType().Name + " on " + c.name); Object.DestroyImmediate(c); removed++; }
                // the template hit volumes were sized for a 1.5 m bot
                var body = root.GetComponent<CapsuleCollider>();
                float shrink = body && body.height > TargetHeight + .05f ? TargetHeight / body.height : 1; // once only: rebuilds edit this prefab
                foreach (var col in root.GetComponentsInChildren<Collider>(true))
                {
                    if (col is BoxCollider bc && shrink < 1 && !col.name.StartsWith("VPD_")) { bc.size *= shrink; bc.center *= shrink; }
                    else if (col is CapsuleCollider cc && shrink < 1) { cc.height = TargetHeight; cc.radius = Mathf.Min(cc.radius, .4f); cc.center = new Vector3(cc.center.x, TargetHeight * .5f, cc.center.z); }
                }

                // horde enemy: no floating health bar (dies in a few hits; 20+ bars would cover the screen)
                foreach (var hb in root.GetComponentsInChildren<Unity.FPS.UI.WorldspaceHealthBar>(true).ToArray()) { log.AppendLine("  removed health bar on " + hb.name); Object.DestroyImmediate(hb); }
                foreach (var cv in root.GetComponentsInChildren<Canvas>(true)) { cv.gameObject.SetActive(false); log.AppendLine("  canvas off " + cv.name); }
                foreach (var col in root.GetComponentsInChildren<Collider>(true)) log.AppendLine($"  collider {col.GetType().Name} {col.name} trigger={col.isTrigger} size={col.bounds.size.ToString("F2")}");
                foreach (var c in root.GetComponents<MonoBehaviour>()) if (c) log.AppendLine("  comp " + c.GetType().Name);
                root.name = "Carronero";
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefab);
                log.AppendLine($"hidden old renderers={hidden} removed={removed} health={health.MaxHealth} speed={agent.speed}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); Debug.Log(log.ToString()); }
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/C4 Carronero unified death")]
        public static void Death()
        {
            var spark = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab")?.GetComponentInChildren<StolenShellFit>(true);
            var mat = spark ? new SerializedObject(spark).FindProperty("sparkMaterial").objectReferenceValue as Material : null;
            VisualPassDeathFx.Apply(EnemyPrefab, mat); AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/C5 Diag spawn Carronero horde 8 (play)")] static void Horde8() => Horde(8);
        [MenuItem("Tools/Iron Requiem/Visual Pass/C6 Diag horde cost breakdown (play)")] static void Cost() { if (Application.isPlaying) HordeCostProbe.Run("DIAG_Carronero"); }
        [MenuItem("Tools/Iron Requiem/Visual Pass/C5 Diag frame time baseline (play)")] static void Baseline() { if (Application.isPlaying) FrameTimeSampler.Run("baseline", 8); }
        [MenuItem("Tools/Iron Requiem/Visual Pass/C5 Diag spawn Carronero horde 24 (play)")] static void Horde24() => Horde(24);
        static void Horde(int count)
        {
            if (!Application.isPlaying) return;
            var pc = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>(); if (!pc) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);
            var ph = pc.GetComponent<Health>(); if (ph) { ph.Invincible = true; ph.Heal(ph.MaxHealth); } // diag only: watch the horde, don't die
            var cam = Camera.main.transform; var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized; var right = Vector3.Cross(Vector3.up, fwd);
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                int row = i / 6, col = i % 6;
                var pos = pc.transform.position + fwd * (18 + row * 2.2f) + right * ((col - 2.5f) * 1.8f);
                if (!NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas)) continue;
                var go = Object.Instantiate(prefab, hit.position, Quaternion.LookRotation(-fwd)); go.name = "DIAG_Carronero_" + i; made++;
            }
            Debug.Log($"[C5 horde] spawned {made}/{count}");
            FrameTimeSampler.Run($"horde {made}", 8, 2);
        }


        [MenuItem("Tools/Iron Requiem/Visual Pass/C2b Carronero preview sheet")]
        public static void Preview()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab); if (!prefab) return;
            const int W = 400, H = 400; var views = new (float yaw, int lod)[] { (180, 0), (90, 0), (225, 0), (135, 0), (135, 1), (135, 2), (135, 3) };
            var sheet = new Texture2D(W * 4, H * 2, TextureFormat.RGB24, false);
            var rt = new RenderTexture(W, H, 24); var camGo = new GameObject("PreviewCam"); var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .19f, .21f); cam.fieldOfView = 30;
            var lightGo = new GameObject("PreviewLight"); var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);
            var go = (GameObject)Object.Instantiate(prefab, new Vector3(0, -500, 0), Quaternion.identity); var lodg = go.GetComponent<LODGroup>();
            try
            {
                // render LOD meshes as static meshes at bind pose: freshly shown skinned renderers are not skinned yet in edit mode
                lodg.enabled = false; foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var stat = new GameObject[4];
                for (int l = 0; l < 4; l++)
                {
                    var smr = go.transform.Find("Body_LOD" + l).GetComponent<SkinnedMeshRenderer>();
                    stat[l] = new GameObject("Static" + l); stat[l].transform.SetParent(go.transform, false);
                    stat[l].AddComponent<MeshFilter>().sharedMesh = smr.sharedMesh; stat[l].AddComponent<MeshRenderer>().sharedMaterial = smr.sharedMaterial;
                }
                var sawR = go.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "SawSpin");
                for (int i = 0; i < views.Length; i++)
                {
                    for (int l = 0; l < 4; l++) stat[l].SetActive(l == views[i].lod);
                    sawR.enabled = views[i].lod < 2;
                    var center = go.transform.position + Vector3.up * TargetHeight * .5f;
                    camGo.transform.position = center + Quaternion.Euler(12, views[i].yaw, 0) * Vector3.forward * -3.2f; camGo.transform.LookAt(center);
                    cam.Render(); RenderTexture.active = rt; var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                    sheet.SetPixels((i % 4) * W, (1 - i / 4) * H, W, H, tex.GetPixels()); Object.DestroyImmediate(tex);
                }
                RenderTexture.active = null; System.IO.File.WriteAllBytes("Tools/IronRequiem/Bridge/carronero_sheet.png", sheet.EncodeToPNG());
                Debug.Log("[C2b] preview written");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); rt.Release(); }
        }

        static float MaxEdge(List<Vector3> v, List<int> t)
        {
            float m = 0; for (int i = 0; i < t.Count; i += 3) { m = Mathf.Max(m, (v[t[i]] - v[t[i + 1]]).magnitude, (v[t[i + 1]] - v[t[i + 2]]).magnitude); } return m;
        }

        static int Depth(Transform t) { int d = 0; for (; t; t = t.parent) d++; return d; }

        static Mesh Save(Mesh m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old)
            {
                // keep the asset (and every reference to it); replace all of its data
                old.Clear(); old.indexFormat = m.indexFormat;
                old.vertices = m.vertices; old.normals = m.normals; old.uv = m.uv; old.tangents = m.tangents;
                if (m.bindposes.Length > 0) { old.bindposes = m.bindposes; old.boneWeights = m.boneWeights; }
                old.triangles = m.triangles; old.RecalculateBounds(); EditorUtility.SetDirty(old);
                Object.DestroyImmediate(m); return old;
            }
            AssetDatabase.CreateAsset(m, path); return m;
        }

        static Material MakeMat(string path, bool sawHot)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Src + "/Carronero_Albedo.jpg"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Src + "/Carronero_Normal.jpg")); mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Src + "/Carronero_MetalSmooth.png")); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 1.6f); mat.SetFloat("_Metallic", 1);
            // grimy hull with a rust-orange cast; the saw edge glows hot
            mat.SetColor("_BaseColor", sawHot ? new Color(1f, .82f, .7f) : new Color(.9f, .78f, .7f));
            if (sawHot) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(1.2f, .32f, .06f)); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
        }
    }
}
