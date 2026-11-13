using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Task 1: unified piece-explosion death for every campaign enemy (reference: mech T-07 MechDeathExplosion).
    public static partial class VisualPassDeathFx
    {
        public static readonly string[] Campaign =
        {
            "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/HoverBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/Turret_Current.prefab",
        };

        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Audit enemy deaths")]
        static void Audit()
        {
            var sb = new StringBuilder("[T1 audit]\n");
            foreach (var path in Campaign)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    sb.AppendLine($"== {path} scale={root.transform.localScale} src={AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(root))}");
                    var ec = root.GetComponent<EnemyController>();
                    if (ec)
                    {
                        var so = new SerializedObject(ec);
                        foreach (var n in new[] { "DeathVfx", "DeathVfxSpawnPoint", "DropRate", "LootPrefab", "SelfDestructYHeight", "DelayAfterDeath" })
                        {
                            var p = so.FindProperty(n); if (p == null) continue;
                            sb.AppendLine($"  EC.{n}={(p.propertyType == SerializedPropertyType.ObjectReference ? (p.objectReferenceValue ? p.objectReferenceValue.name : "null") : p.propertyType == SerializedPropertyType.Float ? p.floatValue.ToString() : "?")}");
                        }
                    }
                    var h = root.GetComponent<Health>(); if (h) sb.AppendLine($"  Health max={h.MaxHealth}");
                    foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (c && c.GetType().Namespace != null && (c.GetType().Namespace.StartsWith("IronRequiem") || c is IEnemyDeathPresentation))
                            sb.AppendLine($"  comp {P(c.transform, root)} : {c.GetType().Name}");
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                        var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                        var lod = r.GetComponentInParent<LODGroup>();
                        sb.AppendLine($"  r {(r is SkinnedMeshRenderer sk ? "SMR bones=" + sk.bones.Length : "MR")} {P(r.transform, root)} on={r.enabled && r.gameObject.activeInHierarchy} v={(mesh ? mesh.vertexCount : 0)} mats={r.sharedMaterials.Length} lossy={r.transform.lossyScale.ToString("F2")} size={r.bounds.size.ToString("F2")} lod={(lod ? lod.name : "-")}");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log(sb.ToString());
        }


        public const string PieceDir = "Assets/IronRequiem/Art/VisualPass/DeathPieces";

        // per enemy: overload delay, size force, centre offset, piece seconds, spark colour, excluded renderer names
        static (float delay, float force, Vector3 offset, float seconds, Color spark, string[] keep) Tuning(string path)
        {
            var hot = new Color(2.4f, 1.1f, .35f);
            if (path.Contains("T07")) return (.28f, 1f, Vector3.zero, 60, new Color(2.2f, .6f, 2.4f), new string[0]);
            if (path.Contains("BigBot")) return (.2f, 1.7f, Vector3.zero, 60, hot, new string[0]);
            if (path.Contains("SawBot")) return (.22f, 1.1f, Vector3.zero, 60, hot, new string[0]);
            if (path.Contains("Carronero")) return (0f, .8f, Vector3.zero, 22, hot, new string[0]); // horde: instant pop, short-lived pieces
            if (path.Contains("HoverBot")) return (0f, .75f, Vector3.zero, 45, new Color(.6f, 1.6f, 2.4f), new string[0]);
            if (path.Contains("Turret")) return (0f, 1.3f, Vector3.up * .3f, 60, hot, new[] { "TurretBottom" }); // base stays bolted down
            return (.15f, 1f, Vector3.zero, 45, hot, new string[0]);
        }

        static bool InWeaponModule(Transform t)
        {
            for (; t; t = t.parent)
                if (t.GetComponent<StolenCannon>() || t.GetComponent<DetachableCannon>() || t.name == "CannonMount") return true;
            return false;
        }

        static bool IsLod0(Renderer r)
        {
            var g = r.GetComponentInParent<LODGroup>(); if (!g) return true;
            var lods = g.GetLODs(); bool listed = false;
            for (int i = 0; i < lods.Length; i++) foreach (var x in lods[i].renderers) if (x == r) { if (i == 0) return true; listed = true; }
            return !listed;
        }

        static Material SparkMaterial()
        {
            var w = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab");
            var fit = w ? w.GetComponentInChildren<StolenShellFit>(true) : null;
            return fit ? new SerializedObject(fit).FindProperty("sparkMaterial").objectReferenceValue as Material : null;
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Apply unified death to campaign enemies")]
        static void ApplyAll() { var spark = SparkMaterial(); foreach (var p in Campaign) Apply(p, spark); AssetDatabase.SaveAssets(); }

        public static void Apply(string path, Material spark)
        {
            var sb = new StringBuilder($"[T1 apply] {path}\n");
            var bakDir = "Tools/IronRequiem/Backups/T1"; System.IO.Directory.CreateDirectory(bakDir);
            var bak = bakDir + "/" + System.IO.Path.GetFileName(path) + ".bak"; if (!System.IO.File.Exists(bak)) System.IO.File.Copy(path, bak);
            var name = System.IO.Path.GetFileNameWithoutExtension(path); var dir = PieceDir + "/" + name; VisualPassMech.EnsureFolder(dir);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var old in root.GetComponentsInChildren<Transform>(true).Where(t => t && t.name.StartsWith("VPD_")).ToArray()) if (old) Object.DestroyImmediate(old.gameObject);
                var tune = Tuning(path);
                var all = root.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                var pieces = new System.Collections.Generic.List<MechDebrisPiece>(); var sources = new System.Collections.Generic.List<Renderer>();
                var hide = new System.Collections.Generic.List<Renderer>();
                int meshIndex = 0;
                foreach (var r in all)
                {
                    if (InWeaponModule(r.transform) || tune.keep.Any(k => r.name.Contains(k))) continue;
                    hide.Add(r);
                    if (!r.enabled || !IsLod0(r) || r.bounds.size.magnitude < .12f) continue;
                    if (r is MeshRenderer mr)
                    {
                        var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                        var go = new GameObject("VPD_" + r.name); go.transform.SetParent(r.transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh; var pr = go.AddComponent<MeshRenderer>(); pr.sharedMaterials = r.sharedMaterials;
                        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        pieces.Add(go.AddComponent<MechDebrisPiece>()); sources.Add(r); go.SetActive(false);
                    }
                    else
                    {
                        var smr = (SkinnedMeshRenderer)r; var mesh = smr.sharedMesh; if (!mesh) continue;
                        var bones = smr.bones; var bind = mesh.bindposes; var bw = mesh.boneWeights;
                        var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv;
                        // dominant bone per triangle; tiny groups fold into the largest
                        var groups = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<(int sub, int a, int b, int c)>>();
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var tri = mesh.GetTriangles(sub);
                            for (int t = 0; t < tri.Length; t += 3)
                            {
                                var acc = new System.Collections.Generic.Dictionary<int, float>();
                                for (int k = 0; k < 3; k++)
                                {
                                    var w = bw.Length > 0 ? bw[tri[t + k]] : new BoneWeight { boneIndex0 = 0, weight0 = 1 };
                                    void Add(int bi, float wt) { if (wt <= 0) return; acc.TryGetValue(bi, out var x); acc[bi] = x + wt; }
                                    Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                                }
                                int best = acc.Count > 0 ? acc.OrderByDescending(x => x.Value).First().Key : 0;
                                if (!groups.TryGetValue(best, out var list)) groups[best] = list = new System.Collections.Generic.List<(int, int, int, int)>();
                                list.Add((sub, tri[t], tri[t + 1], tri[t + 2]));
                            }
                        }
                        int largest = groups.OrderByDescending(g => g.Value.Count).First().Key;
                        foreach (var small in groups.Where(g => g.Key != largest && g.Value.Count < 60).Select(g => g.Key).ToList()) { groups[largest].AddRange(groups[small]); groups.Remove(small); }
                        foreach (var g in groups)
                        {
                            var bone = g.Key < bones.Length ? bones[g.Key] : null; if (!bone) continue;
                            var bp = bind[g.Key];
                            var map = new System.Collections.Generic.Dictionary<int, int>();
                            var pv = new System.Collections.Generic.List<Vector3>(); var pn = new System.Collections.Generic.List<Vector3>(); var pu = new System.Collections.Generic.List<Vector2>();
                            var subs = new System.Collections.Generic.List<int>[mesh.subMeshCount]; for (int k = 0; k < subs.Length; k++) subs[k] = new System.Collections.Generic.List<int>();
                            int V(int i) { if (!map.TryGetValue(i, out var j)) { j = pv.Count; map[i] = j; pv.Add(bp.MultiplyPoint3x4(verts[i])); pn.Add(norms.Length > i ? bp.MultiplyVector(norms[i]).normalized : Vector3.up); pu.Add(uvs.Length > i ? uvs[i] : Vector2.zero); } return j; }
                            foreach (var (sub, a, b2, c) in g.Value) { subs[sub].Add(V(a)); subs[sub].Add(V(b2)); subs[sub].Add(V(c)); }
                            var cen = Vector3.zero; foreach (var p in pv) cen += p; cen /= Mathf.Max(1, pv.Count);
                            for (int k = 0; k < pv.Count; k++) pv[k] -= cen;
                            var used = Enumerable.Range(0, subs.Length).Where(k => subs[k].Count > 0).ToArray();
                            var assetPath = $"{dir}/{name}_{meshIndex++:00}_{bone.name.Replace(':', '_')}.asset";
                            var pm = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath); if (pm) pm.Clear(); else { pm = new Mesh(); AssetDatabase.CreateAsset(pm, assetPath); }
                            pm.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                            if (pv.Count > 65000) pm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                            pm.SetVertices(pv); pm.SetNormals(pn); pm.SetUVs(0, pu); pm.subMeshCount = used.Length;
                            for (int k = 0; k < used.Length; k++) pm.SetTriangles(subs[used[k]], k);
                            pm.RecalculateBounds(); pm.RecalculateTangents(); EditorUtility.SetDirty(pm);
                            var go = new GameObject("VPD_" + r.name + "_" + bone.name.Replace(':', '_')); go.transform.SetParent(bone, false); go.transform.localPosition = cen;
                            go.AddComponent<MeshFilter>().sharedMesh = pm; var pr = go.AddComponent<MeshRenderer>();
                            pr.sharedMaterials = used.Select(k => k < r.sharedMaterials.Length ? r.sharedMaterials[k] : r.sharedMaterials[0]).ToArray();
                            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            pieces.Add(go.AddComponent<MechDebrisPiece>()); sources.Add(r); go.SetActive(false);
                        }
                    }
                }
                var ec = root.GetComponent<EnemyController>();
                Transform center = ec && ec.DeathVfxSpawnPoint ? ec.DeathVfxSpawnPoint : null;
                if (!center) { var probe = new GameObject("VPD_Center").transform; probe.SetParent(root.transform, false); var b = hide[0].bounds; foreach (var h in hide) b.Encapsulate(h.bounds); probe.position = b.center; center = probe; }
                var boom = root.GetComponent<MechDeathExplosion>() ?? root.AddComponent<MechDeathExplosion>();
                boom.ConfigureGeneric(center, pieces.ToArray(), sources.ToArray(), hide.ToArray(), tune.delay, tune.force, tune.offset, tune.seconds, tune.spark, spark);
                if (ec && tune.delay > 0 && ec.DeathDuration < tune.delay + .1f) { sb.AppendLine($"  DeathDuration {ec.DeathDuration} -> {tune.delay + .1f}"); ec.DeathDuration = tune.delay + .1f; }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                sb.AppendLine($"  pieces={pieces.Count} hidden={hide.Count} center={center.name} delay={tune.delay} force={tune.force} lossyRoot={root.transform.lossyScale}");
                foreach (var p in pieces) sb.AppendLine($"    {p.name} lossy={p.transform.lossyScale.ToString("F2")} v={p.GetComponent<MeshFilter>().sharedMesh.vertexCount}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); Debug.Log(sb.ToString()); }
        }

        // Mech T-07: its animated arm hangs while it shoots. Raise the cannon arm toward the target and aim the muzzle,
        // so the charge glows at the cannon mouth and the shot leaves toward the player.
        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Mech T-07 aims cannon")]
        public static void MechAim()
        {
            const string path = "Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_Test.prefab";
            var sb = new StringBuilder($"[mech aim] {path}\n");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var w = root.GetComponentsInChildren<WeaponController>(true).FirstOrDefault();
                var module = root.GetComponentInChildren<StolenCannon>(true)?.transform; var joint = root.GetComponentInChildren<DetachableCannon>(true);
                var socket = root.GetComponent<T07CannonSocket>();
                if (!w || !module || !socket || !socket.Socket) { sb.AppendLine($"  missing weapon={w} module={module} socket={socket}"); return; }
                var anchor = root.GetComponent<EnemyMuzzleAnchor>() ?? root.AddComponent<EnemyMuzzleAnchor>();
                anchor.Configure(w.WeaponMuzzle, module, joint); anchor.SetAim(true, 60); anchor.Measure();
                var upper = socket.Socket.parent;
                socket.ConfigureAim(upper, anchor);
                var charge = root.GetComponent<EnemyCannonChargePresentation>();
                var fx = charge ? new SerializedObject(charge).FindProperty("effect").objectReferenceValue as GameObject : null;
                if (fx && fx.transform.parent != w.WeaponMuzzle) { fx.transform.SetParent(w.WeaponMuzzle, false); fx.transform.localPosition = Vector3.zero; fx.transform.localRotation = Quaternion.identity; sb.AppendLine("  charge effect moved under muzzle"); }
                sb.AppendLine($"  muzzle={P(w.WeaponMuzzle, root)} muzzleParent={P(w.WeaponMuzzle.parent, root)} weapon={P(w.transform, root)} module={P(module, root)} socketBone={socket.Socket.name} aimBone={upper.name} charge={P(fx ? fx.transform : null, root)}");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); Debug.Log(sb.ToString()); }
        }

        // T-07 enemy fired from the template eye-laser origin. Put its muzzle (and the charge effect) on the cannon mouth.
        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 T-07 enemy fires from cannon")]
        public static void T07Muzzle()
        {
            foreach (var path in new[] { "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab" })
            {
                var sb = new StringBuilder($"[T07 muzzle] {path}\n");
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var w = root.GetComponentsInChildren<WeaponController>(true).FirstOrDefault();
                    var module = root.GetComponentInChildren<StolenCannon>(true)?.transform; var joint = root.GetComponentInChildren<DetachableCannon>(true);
                    if (!w || !module) { sb.AppendLine("  missing weapon/module"); continue; }
                    sb.AppendLine($"  weapon={P(w.transform, root)} muzzle={P(w.WeaponMuzzle, root)} wp={w.WeaponMuzzle.position.ToString("F3")} module={P(module, root)}");
                    var anchor = root.GetComponent<EnemyMuzzleAnchor>() ?? root.AddComponent<EnemyMuzzleAnchor>();
                    anchor.Configure(w.WeaponMuzzle, module, joint); anchor.Measure();
                    var charge = root.GetComponent<EnemyCannonChargePresentation>();
                    if (charge)
                    {
                        var fx = new SerializedObject(charge).FindProperty("effect").objectReferenceValue as GameObject;
                        sb.AppendLine($"  charge effect={P(fx ? fx.transform : null, root)} wp={fx?.transform.position.ToString("F3")}");
                        if (fx && fx.transform.parent != w.WeaponMuzzle) { fx.transform.SetParent(w.WeaponMuzzle, false); fx.transform.localPosition = Vector3.zero; fx.transform.localRotation = Quaternion.identity; sb.AppendLine("  charge effect moved under muzzle"); }
                    }
                    // preview where the anchor will put the muzzle in the authored pose
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); Debug.Log(sb.ToString()); }
            }
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Diag spawn campaign T-07 (play)")] static void SpawnT07() => SpawnDiag("T07_Current_Repaired", 14, true);
        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Diag spawn all enemies (play)")]
        static void SpawnAll() { int i = 0; foreach (var n in new[] { "T07_Current_Repaired", "BigBot_Current", "SawBot_Current", "HoverBot_Current", "Turret_Current" }) SpawnDiag(n, 12, false, (i++ - 2) * 5f); }
        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Diag spawn turret far (play)")] static void SpawnTurret() => SpawnDiag("Turret_Current", 18, false);
        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Diag list white renderers near camera (play)")]
        static void NearCam()
        {
            var cam = Camera.main.transform.position; var sb = new StringBuilder("[near cam]\n");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.isVisible && r.enabled && Vector3.Distance(r.bounds.center, cam) < 9 && !r.transform.IsChildOf(Camera.main.transform.root))
                    sb.AppendLine($"  {r.GetType().Name} {r.name} root={r.transform.root.name} d={Vector3.Distance(r.bounds.center, cam):0.0} size={r.bounds.size.ToString("F1")} mat={r.sharedMaterial?.name}");
            Debug.Log(sb.ToString());
        }
        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Diag kill spawned (play)")]
        static void KillSpawned() { foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None)) if (h.name.StartsWith("DIAG_") && h.CurrentHealth > 0) h.Kill(); }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T1 Diag spawn mech T-07 (play)")] static void SpawnMech() => SpawnDiag("Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_Test.prefab", 14, true);

        static void SpawnDiag(string prefab, float dist, bool probe, float side = 0)
        {
            if (!Application.isPlaying) return;
            var pc = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>(); if (!pc) return;
            var ph = pc.GetComponent<Health>(); if (ph) ph.Invincible = true;
            var cam = Camera.main.transform; var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized; var right = Vector3.Cross(Vector3.up, fwd);
            var pos = pc.transform.position + fwd * dist + right * side;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab.StartsWith("Assets/") ? prefab : $"Assets/IronRequiem/Prefabs/Campaign/{prefab}.prefab"), pos, Quaternion.LookRotation(-fwd));
            go.name = "DIAG_" + System.IO.Path.GetFileNameWithoutExtension(prefab); if (probe) go.AddComponent<EnemyShotProbe>();
            Debug.Log($"[T1 spawn] {go.name} at {pos}");
        }

        public static string P(Transform t, GameObject root)
        {
            if (!t || t == root.transform) return "."; var s = t.name;
            while (t.parent && t.parent != root.transform) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
