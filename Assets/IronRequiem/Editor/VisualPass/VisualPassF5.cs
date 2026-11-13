using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// F5 enemies + viewmodel.
    public static class VisualPassF5
    {
        const string Menu = "Tools/Iron Requiem/Visual Pass/F5 ";
        const string Report = "Tools/IronRequiem/Reports/VisualPass/F5";
        static readonly string[] Weapons = {
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab",
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab" };

        [MenuItem(Menu + "Audit viewmodel materials (read only)")]
        static void Audit()
        {
            var sb = new StringBuilder();
            foreach (var path in Weapons)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!go) { sb.AppendLine("missing " + path); continue; }
                sb.AppendLine("== " + path);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) Describe(sb, r);
            }
            var player = Object.FindObjectsByType<Unity.FPS.Gameplay.PlayerCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player)
            {
                sb.AppendLine("== player " + player.name);
                foreach (var r in player.GetComponentsInChildren<Renderer>(true)) Describe(sb, r);
                foreach (var c in player.GetComponentsInChildren<Camera>(true)) sb.AppendLine($"cam {c.name} near={c.nearClipPlane} fov={c.fieldOfView} mask={c.cullingMask}");
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-a0.txt", sb.ToString()); Debug.Log("[F5]\n" + sb);
        }


        [MenuItem(Menu + "Viewmodel arm material (less mirror)")]
        static void ArmMaterial()
        {
            const string src = "Assets/IronRequiem/Art/Player/ProtagonistV01/Materials/IR_Protagonist_URP.mat";
            const string dst = "Assets/IronRequiem/Art/VisualPass/IR_Protagonist_VP_FPS.mat";
            var sb = new StringBuilder();
            var original = AssetDatabase.LoadAssetAtPath<Material>(src); if (!original) { Debug.LogError("[F5] missing " + src); return; }
            Directory.CreateDirectory("Assets/IronRequiem/Art/VisualPass");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (!mat) { mat = new Material(original); AssetDatabase.CreateAsset(mat, dst); }
            else mat.CopyPropertiesFromMaterial(original);
            bool metalMap = mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap");
            sb.AppendLine($"src metalMap={metalMap} base={mat.GetTexture("_BaseMap")} normal={(mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null)}");
            // Fully metallic + smoothness 1 mirrors the bright sector lights: armour reads as blown-out white.
            mat.SetFloat("_Smoothness", metalMap ? .55f : .42f);
            if (!metalMap) mat.SetFloat("_Metallic", .6f);
            mat.SetColor("_BaseColor", new Color(.8f, .8f, .82f, 1)); if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(.8f, .8f, .82f, 1));
            if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 1);
            EditorUtility.SetDirty(mat);
            foreach (var player in Object.FindObjectsByType<Unity.FPS.Gameplay.PlayerCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.layer != LayerMask.NameToLayer("FirstPersonWeapon")) continue;
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++) if (mats[i] == original) { mats[i] = mat; changed = true; }
                if (!changed) continue;
                Undo.RecordObject(r, "F5"); r.sharedMaterials = mats; PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                sb.AppendLine("FPS -> " + Path(r.transform));
            }
            foreach (var g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g.GetComponentInParent<Unity.FPS.Gameplay.PlayerCharacterController>()) sb.AppendLine("LODGroup " + Path(g.transform) + " lods=" + g.lodCount);
            foreach (var player in Object.FindObjectsByType<Unity.FPS.Gameplay.PlayerCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var r in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.gameObject.layer == 0) sb.AppendLine($"body {Path(r.transform)} shadows={r.shadowCastingMode}");
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty(); UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-arm.txt", sb.ToString()); Debug.Log("[F5 arm]\n" + sb);
        }


        [MenuItem(Menu + "Weapon deploy + straight jab")]
        static void DeployAndJab()
        {
            var sb = new StringBuilder();
            const string backup = "Tools/IronRequiem/Backups/20261109-visualpass-f5";
            Directory.CreateDirectory(backup);
            foreach (var path in Weapons)
            {
                File.Copy(path, System.IO.Path.Combine(backup, System.IO.Path.GetFileName(path) + ".bak"), true);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (!root.GetComponent<WeaponDeployMotion>()) root.AddComponent<WeaponDeployMotion>();
                PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
                sb.AppendLine("WeaponDeployMotion -> " + path);
            }
            foreach (var melee in Object.FindObjectsByType<ProtagonistMelee>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(melee);
                sb.AppendLine($"melee {melee.name} before windUp={so.FindProperty("windUpKick").vector3Value} impact={so.FindProperty("impactKick").vector3Value} whiff={so.FindProperty("whiffKick").vector3Value}");
                // Straight punch: no upward pitch on wind-up, a short forward nod on contact.
                so.FindProperty("windUpKick").vector3Value = new Vector3(-.3f, -.5f, .5f);
                so.FindProperty("impactKick").vector3Value = new Vector3(1.3f, .25f, -.7f);
                so.FindProperty("whiffKick").vector3Value = new Vector3(.5f, .15f, -.3f);
                so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(melee);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty(); UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-deploy.txt", sb.ToString()); Debug.Log("[F5 deploy]\n" + sb);
        }


        [MenuItem(Menu + "Diag slow motion toggle (play)")]
        static void Slow() { Time.timeScale = Time.timeScale < .5f ? 1f : .08f; Debug.Log("[F5] timeScale " + Time.timeScale); }


        [MenuItem(Menu + "Diag audio (play)")]
        static void DiagAudio()
        {
            var sb = new StringBuilder($"listenerPause={AudioListener.pause} listenerVolume={AudioListener.volume} timeScale={Time.timeScale}\n");
            foreach (var lab in Object.FindObjectsByType<CombatLabMenu>(FindObjectsSortMode.None)) sb.AppendLine($"lab {lab.name} playing={lab.IsPlaying} main={lab.IsMainMenu} fromMenu={CombatLabMenu.LaunchedFromMainMenu}");
            foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!src.clip || !src.loop || !src.isActiveAndEnabled) continue;
                sb.AppendLine($"{src.gameObject.scene.name}:{Path(src.transform)} clip={src.clip.name} playing={src.isPlaying} vol={src.volume:F2} en={src.isActiveAndEnabled} ignorePause={src.ignoreListenerPause} group={(src.outputAudioMixerGroup ? src.outputAudioMixerGroup.name : "-")} load={src.clip.loadState}");
                if (src.outputAudioMixerGroup)
                    foreach (var n in new[] { "MasterVolume" })
                        if (src.outputAudioMixerGroup.audioMixer.GetFloat(n, out var v)) sb.AppendLine($"   mixer {n}={v}");
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-audio.txt", sb.ToString()); Debug.Log("[F5 audio]\n" + sb);
        }

        [MenuItem(Menu + "Diag stolen weapon prefabs (read only)")]
        static void DiagStolen()
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs/Campaign" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!c) continue; var so = new SerializedObject(c); var it = so.GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is Component wc && wc.GetType().Name == "WeaponController")
                        {
                            var wpath = AssetDatabase.GetAssetPath(wc);
                            sb.AppendLine($"{path} :: {c.GetType().Name}.{it.propertyPath} -> {wpath}");
                            foreach (var r in wc.GetComponentsInChildren<Renderer>(true))
                                if (!(r is ParticleSystemRenderer)) sb.AppendLine($"     {Path(r.transform)} en={r.enabled} act={r.gameObject.activeSelf} layer={LayerMask.LayerToName(r.gameObject.layer)} mats={string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "/" + m.shader.name : "null"))} scale={r.transform.lossyScale}");
                        }
                }
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-stolen.txt", sb.ToString()); Debug.Log("[F5 stolen]\n" + sb);
        }


        static int giveIndex;
        [MenuItem(Menu + "Diag give next stolen weapon (play)")]
        static void Give()
        {
            var list = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs/Campaign", "Assets/IronRequiem/Prefabs/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .SelectMany(go => go.GetComponentsInChildren<StolenCannon>(true)).Where(c => c.WeaponPrefab)
                .Select(c => c.WeaponPrefab).Distinct().ToList();
            var sb = new StringBuilder(); foreach (var w in list) sb.AppendLine(AssetDatabase.GetAssetPath(w));
            var mgr = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerWeaponsManager>();
            if (Application.isPlaying && mgr && list.Count > 0)
            {
                var prefab = list[giveIndex++ % list.Count];
                var have = mgr.HasWeapon(prefab); if (!have && mgr.AddWeapon(prefab)) have = mgr.HasWeapon(prefab);
                for (int i = 0; i < 9; i++) if (mgr.GetWeaponAtSlotIndex(i) == have) { mgr.SwitchToWeaponIndex(i, true); break; }
                sb.AppendLine("gave " + prefab.name);
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-give.txt", sb.ToString()); Debug.Log("[F5 give]\n" + sb);
        }


        [MenuItem(Menu + "Campaign music (explore + combat)")]
        static void CampaignMusic()
        {
            var sb = new StringBuilder();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "RequiemCampaign") { Debug.LogError("[F5] open RequiemCampaign first (active=" + scene.name + ")"); return; }
            var explore = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IronRequiem/Audio/Music/IR_Combat01_Explore.wav");
            var combat = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IronRequiem/Audio/Music/IR_Combat01_Loop.wav");
            var menuMusic = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuMusic>(true)).FirstOrDefault();
            var group = menuMusic ? new SerializedObject(menuMusic).FindProperty("m_OutputGroup").objectReferenceValue : null;
            var lab = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatLabMenu>(true)).FirstOrDefault();
            var music = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatMusic>(true)).FirstOrDefault();
            if (!music) { var go = new GameObject("CampaignMusic"); Undo.RegisterCreatedObjectUndo(go, "F5"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); music = go.AddComponent<CombatMusic>(); sb.AppendLine("created CampaignMusic"); }
            var so = new SerializedObject(music);
            so.FindProperty("m_ExploreClip").objectReferenceValue = explore;
            so.FindProperty("m_CombatClip").objectReferenceValue = combat;
            so.FindProperty("m_OutputGroup").objectReferenceValue = group;
            so.FindProperty("m_LabMenu").objectReferenceValue = lab;
            so.ApplyModifiedProperties();
            sb.AppendLine($"explore={(explore ? explore.name : "MISSING")} combat={(combat ? combat.name : "MISSING")} group={(group ? group.name : "none")} lab={(lab ? lab.name : "none")}");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-music.txt", sb.ToString()); Debug.Log("[F5 music]\n" + sb);
        }


        static int stealIndex;
        static readonly string[] StealOrder = { "T07", "SawBot", "BigBot" };
        [MenuItem(Menu + "Diag steal from enemy (play)")]
        static void Steal() => StealType(null);
        [MenuItem(Menu + "Diag steal SawBot (play)")] static void StealSaw() => StealType("SawBot");
        [MenuItem(Menu + "Diag steal BigBot (play)")] static void StealBig() => StealType("BigBot");
        static void StealType(string forced)
        {
            var sb = new StringBuilder();
            var pc = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();
            var carrier = pc ? pc.GetComponent<StolenWeaponCarrier>() : null;
            if (!Application.isPlaying || !pc || !carrier) { Debug.LogWarning("[F5 steal] play mode + player needed"); return; }
            var ph = pc.GetComponent<Unity.FPS.Game.Health>(); if (ph) ph.Invincible = true;
            var sources = Object.FindObjectsByType<EnemyExecutionSource>(FindObjectsSortMode.None).Where(e => e.isActiveAndEnabled && e.ModulePrefab).ToList();
            if (sources.Count == 0)
            {
                foreach (var lab in Object.FindObjectsByType<CombatTwoRoomsLab>(FindObjectsSortMode.None))
                {
                    var f = typeof(CombatTwoRoomsLab).GetField("firstRoom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null && f.GetValue(lab) is Bounds b) { Teleport(pc, b.center); sb.AppendLine("no enemies: moved player into first room " + b.center); break; }
                }
            }
            else
            {
                if (carrier.Held) { sb.AppendLine("threw held -> " + carrier.ThrowHeld()); Directory.CreateDirectory(Report); File.AppendAllText(Report + "/f5-steal.txt", sb.ToString()); Debug.Log("[F5 steal]\n" + sb); return; }
                string typeFile = "Tools/IronRequiem/Bridge/steal_type.txt";
                string type = forced ?? (File.Exists(typeFile) ? File.ReadAllText(typeFile).Trim() : StealOrder[stealIndex++ % StealOrder.Length]);
                var target = sources.FirstOrDefault(e => e.name.Contains(type)) ?? sources[0];
                var h = target.GetComponent<Unity.FPS.Game.Health>();
                if (h && h.CurrentHealth >= h.MaxHealth * target.EffectiveThreshold) h.TakeDamage(h.CurrentHealth - h.MaxHealth * target.EffectiveThreshold * .8f, pc.gameObject);
                var cam = pc.PlayerCamera.transform; var fwd = cam.forward; fwd.y = 0; fwd.Normalize();
                cam.localRotation = Quaternion.Euler(8, 0, 0); // look slightly down at the robot's chest
                var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>(); var at = pc.transform.position + fwd * 1.25f;
                if (agent && agent.isOnNavMesh) agent.Warp(at); else target.transform.position = at;
                target.transform.rotation = Quaternion.LookRotation(-fwd);
                Physics.SyncTransforms();
                sb.AppendLine($"target {target.name} eligible={target.Eligible} status={carrier.GetExecutionStatus(target)}");
                sb.AppendLine("execute -> " + carrier.TryExecute(target));
            }
            Directory.CreateDirectory(Report); File.AppendAllText(Report + "/f5-steal.txt", sb.ToString()); Debug.Log("[F5 steal]\n" + sb);
        }
        static void Teleport(Unity.FPS.Gameplay.PlayerCharacterController pc, Vector3 p)
        {
            var cc = pc.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            pc.transform.position = p + Vector3.up * .2f; if (cc) cc.enabled = true; Physics.SyncTransforms();
        }


        [MenuItem(Menu + "Audit stolen weapon materials (read only)")]
        static void AuditStolenMats()
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs/Campaign" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var src in go.GetComponentsInChildren<EnemyExecutionSource>(true))
                {
                    var w = src.ModulePrefab ? src.ModulePrefab.WeaponPrefab : null;
                    sb.AppendLine($"== {go.name} -> module {(src.ModulePrefab ? AssetDatabase.GetAssetPath(src.ModulePrefab) : "none")} weapon {(w ? AssetDatabase.GetAssetPath(w) : "none")}");
                    if (w) foreach (var r in w.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer) && r.enabled) Describe(sb, r);
                }
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-stolen-mats.txt", sb.ToString()); Debug.Log("[F5 stolen mats] written");
        }


        [MenuItem(Menu + "Stolen weapons readable materials")]
        static void StolenMaterials()
        {
            var sb = new StringBuilder();
            const string backup = "Tools/IronRequiem/Backups/20261109-visualpass-f5";
            const string outDir = "Assets/IronRequiem/Art/VisualPass/StolenFPS";
            Directory.CreateDirectory(backup); Directory.CreateDirectory(outDir);
            var copies = new System.Collections.Generic.Dictionary<Material, Material>();
            var weapons = new System.Collections.Generic.HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs/Campaign" }))
                foreach (var src in AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponentsInChildren<EnemyExecutionSource>(true))
                    if (src.ModulePrefab && src.ModulePrefab.WeaponPrefab) weapons.Add(AssetDatabase.GetAssetPath(src.ModulePrefab.WeaponPrefab));
            foreach (var path in weapons)
            {
                File.Copy(path, System.IO.Path.Combine(backup, System.IO.Path.GetFileName(path) + ".bak"), true);
                var root = PrefabUtility.LoadPrefabContents(path); int swaps = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                    var mats = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; if (!m || !m.HasProperty("_Metallic") || m.shader.name != "Universal Render Pipeline/Lit") continue;
                        bool mirror = m.GetFloat("_Smoothness") > .8f && (m.GetFloat("_Metallic") > .6f || (m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap")));
                        if (!mirror) continue;
                        if (!copies.TryGetValue(m, out var c))
                        {
                            string cp = outDir + "/" + m.name + "_FPS.mat";
                            c = AssetDatabase.LoadAssetAtPath<Material>(cp);
                            if (!c) { c = new Material(m); AssetDatabase.CreateAsset(c, cp); } else c.CopyPropertiesFromMaterial(m);
                            // Painted/worn metal instead of a mirror: keeps albedo + normal, drops the metal map.
                            c.SetTexture("_MetallicGlossMap", null); c.DisableKeyword("_METALLICSPECGLOSSMAP");
                            c.SetFloat("_Metallic", .35f); c.SetFloat("_Smoothness", .42f);
                            c.SetColor("_BaseColor", new Color(1.15f, 1.12f, 1.1f, 1));
                            EditorUtility.SetDirty(c); copies[m] = c; sb.AppendLine("copy " + cp);
                        }
                        mats[i] = c; changed = true; swaps++;
                    }
                    if (changed) r.sharedMaterials = mats;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
                sb.AppendLine($"{path}: {swaps} material slots");
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-stolen-fix.txt", sb.ToString()); Debug.Log("[F5 stolen fix]\n" + sb);
        }


        [MenuItem(Menu + "Diag held weapon renderers (play)")]
        static void DiagHeld()
        {
            var sb = new StringBuilder();
            var mgr = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerWeaponsManager>();
            var cam = Camera.main;
            var active = mgr ? mgr.GetActiveWeapon() : null;
            sb.AppendLine("active weapon " + (active ? active.name : "none"));
            if (active)
                foreach (var r in active.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    var b = r.bounds; var sp = cam ? cam.WorldToViewportPoint(b.center) : Vector3.zero;
                    sb.Append($"  {Path(r.transform)} en={r.enabled} act={r.gameObject.activeInHierarchy} layer={LayerMask.LayerToName(r.gameObject.layer)} vp={sp} size={b.size} shadows={r.shadowCastingMode} probes={r.reflectionProbeUsage} light={r.lightProbeUsage}");
                    foreach (var m in r.sharedMaterials) sb.Append(" | " + (m ? m.name + "/" + m.shader.name + (m.HasProperty("_BaseColor") ? " c=" + m.GetColor("_BaseColor") : "") + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? " tex=" + m.GetTexture("_BaseMap").name : " tex=none") : "null"));
                    if (r.HasPropertyBlock()) { var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb); sb.Append(" MPB c=" + mpb.GetColor("_BaseColor")); }
                    sb.AppendLine();
                }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-held.txt", sb.ToString()); Debug.Log("[F5 held] written");
        }


        [MenuItem(Menu + "Stolen weapons torn mechanism glow")]
        static void TornMechanism()
        {
            var sb = new StringBuilder();
            const string outDir = "Assets/IronRequiem/Art/VisualPass/StolenFPS";
            Directory.CreateDirectory(outDir);
            var weapons = new System.Collections.Generic.HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/IronRequiem/Prefabs/Campaign" }))
                foreach (var src in AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponentsInChildren<EnemyExecutionSource>(true))
                    if (src.ModulePrefab && src.ModulePrefab.WeaponPrefab) weapons.Add(AssetDatabase.GetAssetPath(src.ModulePrefab.WeaponPrefab));
            Material copy = null;
            foreach (var path in weapons)
            {
                var root = PrefabUtility.LoadPrefabContents(path); int swaps = 0;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; if (!m || !m.name.StartsWith("ExposedMechanism") || m.name.EndsWith("_FPS")) continue;
                        if (!copy)
                        {
                            string cp = outDir + "/ExposedMechanism_FPS.mat";
                            copy = AssetDatabase.LoadAssetAtPath<Material>(cp);
                            if (!copy) { copy = new Material(m); AssetDatabase.CreateAsset(copy, cp); } else copy.CopyPropertiesFromMaterial(m);
                            // Torn socket seen from the player's side: readable worn metal with hot orange wiring glow.
                            copy.SetColor("_BaseColor", new Color(.34f, .33f, .32f, 1)); copy.SetFloat("_Metallic", .55f); copy.SetFloat("_Smoothness", .38f);
                            copy.EnableKeyword("_EMISSION"); copy.SetColor("_EmissionColor", new Color(.9f, .26f, .04f) * 1.2f);
                            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                            EditorUtility.SetDirty(copy); sb.AppendLine("copy " + cp);
                        }
                        mats[i] = copy; changed = true; swaps++;
                    }
                    if (changed) r.sharedMaterials = mats;
                }
                if (swaps > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                sb.AppendLine($"{path}: {swaps}");
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-torn.txt", sb.ToString()); Debug.Log("[F5 torn]\n" + sb);
        }


        [MenuItem(Menu + "Stolen cannons use VO shell (robot tint)")]
        static void StolenVOShell()
        {
            var sb = new StringBuilder();
            const string backup = "Tools/IronRequiem/Backups/20261109-visualpass-f5";
            const string outDir = "Assets/IronRequiem/Art/VisualPass/StolenFPS";
            Directory.CreateDirectory(backup); Directory.CreateDirectory(outDir);
            var voPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons[0]);
            var voWeapon = voPrefab.GetComponent<Unity.FPS.Game.WeaponController>();
            var voRoot = voWeapon.WeaponRoot ? voWeapon.WeaponRoot.transform : voPrefab.transform;
            var voAttach = voRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Attachment");
            var targets = new[] {
                ("Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab", "T07", new Color(.78f, .76f, .82f), new Color(1.6f, .25f, 2.2f)),
                ("Assets/IronRequiem/Prefabs/CombatFollowup/BigBot_PlayerWeapon.prefab", "BigBot", new Color(.86f, .62f, .4f), new Color(2.2f, .7f, .12f)) };
            foreach (var (path, tag, tint, glow) in targets)
            {
                if (!File.Exists(path)) { sb.AppendLine("missing " + path); continue; }
                File.Copy(path, System.IO.Path.Combine(backup, System.IO.Path.GetFileName(path) + ".shell.bak"), true);
                var root = PrefabUtility.LoadPrefabContents(path);
                var weapon = root.GetComponent<Unity.FPS.Game.WeaponController>();
                var wRoot = weapon.WeaponRoot ? weapon.WeaponRoot.transform : root.transform;
                var old = wRoot.Find("VP_StolenShell"); if (old) Object.DestroyImmediate(old.gameObject);
                // Hide the enemy-arm meshes (keep objects: scripts may reference them).
                int hidden = 0;
                foreach (var r in wRoot.GetComponentsInChildren<Renderer>(true))
                    if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.enabled && r.sharedMaterials.Any(m => m && m.shader.name == "Universal Render Pipeline/Lit")) { r.enabled = false; hidden++; }
                // Mesh-only copy of the VO cannon visual.
                var shell = Object.Instantiate(voRoot.gameObject); shell.name = "VP_StolenShell";
                foreach (var mb in shell.GetComponentsInChildren<MonoBehaviour>(true)) if (mb) Object.DestroyImmediate(mb);
                for (int pass = 0; pass < 3; pass++)
                    foreach (var c in shell.GetComponentsInChildren<Component>(true))
                        if (c && !(c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer)) { try { Object.DestroyImmediate(c); } catch { } }
                foreach (var t in shell.GetComponentsInChildren<Transform>(true).Where(t => t && t != shell.transform && t.GetComponentsInChildren<Renderer>(true).Length == 0).ToList()) if (t) Object.DestroyImmediate(t.gameObject);
                shell.transform.SetParent(wRoot, false);
                // Align: VO attachment (or muzzle) onto the stolen weapon's attachment (or muzzle).
                var dstAttach = wRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Attachment" && !t.IsChildOf(shell.transform));
                Transform srcRef = voAttach ? voAttach : voWeapon.WeaponMuzzle, dstRef = dstAttach && voAttach ? dstAttach : weapon.WeaponMuzzle;
                Matrix4x4 srcLocal = voRoot.worldToLocalMatrix * srcRef.localToWorldMatrix;            // ref pose inside VO root
                Matrix4x4 want = dstRef.localToWorldMatrix * srcLocal.inverse;                         // where the shell root must be
                shell.transform.SetPositionAndRotation(want.GetColumn(3), want.rotation);
                shell.transform.localScale = voRoot.localScale;
                // Robot tint materials.
                var cache = new System.Collections.Generic.Dictionary<Material, Material>();
                foreach (var r in shell.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; if (!m) continue;
                        if (!cache.TryGetValue(m, out var c))
                        {
                            string cp = $"{outDir}/{m.name}_{tag}Stolen.mat";
                            c = AssetDatabase.LoadAssetAtPath<Material>(cp);
                            if (!c) { c = new Material(m); AssetDatabase.CreateAsset(c, cp); } else c.CopyPropertiesFromMaterial(m);
                            foreach (var prop in new[] { "_BaseColor", "_Color", "_Tint" }) if (c.HasProperty(prop)) c.SetColor(prop, c.GetColor(prop) * tint);
                            if (c.HasProperty("_EmissionColor") && c.GetColor("_EmissionColor").maxColorComponent > .05f) c.SetColor("_EmissionColor", glow);
                            EditorUtility.SetDirty(c); cache[m] = c;
                        }
                        mats[i] = c;
                    }
                    r.sharedMaterials = mats;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
                sb.AppendLine($"{tag}: hid {hidden} enemy renderers, shell aligned on {(dstRef ? dstRef.name : "?")}, {cache.Count} tinted materials -> {path}");
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-shell.txt", sb.ToString()); Debug.Log("[F5 shell]\n" + sb);
        }


        [MenuItem(Menu + "Stolen shells fit forearm socket")]
        static void ShellFit()
        {
            var sb = new StringBuilder();
            var voPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons[0]);
            var voWeapon = voPrefab.GetComponent<Unity.FPS.Game.WeaponController>();
            var voRoot = voWeapon.WeaponRoot ? voWeapon.WeaponRoot.transform : voPrefab.transform;
            var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>(FindObjectsInactive.Include);
            Vector3 offset = new Vector3(0, -.03f, -.42f);
            if (hands) offset = new SerializedObject(hands).FindProperty("mountedWristOffset").vector3Value;
            // VO cannon: forearm socket = muzzle + muzzleRot * mountedWristOffset, rotated like the muzzle (ProtagonistHandsPresentation).
            var m = voRoot.worldToLocalMatrix * voWeapon.WeaponMuzzle.localToWorldMatrix;
            Quaternion R = m.rotation; Vector3 P = (Vector3)m.GetColumn(3) + R * offset;
            Quaternion inv = Quaternion.Inverse(R); Vector3 pos = inv * -P;
            sb.AppendLine($"offset={offset} socket(VOroot)={P} rot={R.eulerAngles} -> shell pos={pos} rot={inv.eulerAngles}");
            foreach (var path in new[] { "Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab", "Assets/IronRequiem/Prefabs/CombatFollowup/BigBot_PlayerWeapon.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var shell = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "VP_StolenShell");
                if (shell)
                {
                    var fit = shell.GetComponent<StolenShellFit>(); if (!fit) fit = shell.gameObject.AddComponent<StolenShellFit>();
                    fit.Configure(pos, inv);
                    var sparkSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_HitSparks.prefab");
                    var spark = sparkSrc ? sparkSrc.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(x => x.sharedMaterial).FirstOrDefault(x => x) : null;
                    fit.ConfigureSparks(spark);
                    PrefabUtility.SaveAsPrefabAsset(root, path); sb.AppendLine("fit -> " + path);
                }
                else sb.AppendLine("no shell in " + path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-fit.txt", sb.ToString()); Debug.Log("[F5 fit]\n" + sb);
        }


        [MenuItem(Menu + "Diag switch weapon (play)")]
        static void DiagSwitch()
        {
            var mgr = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerWeaponsManager>();
            if (!Application.isPlaying || !mgr) return;
            int next = mgr.ActiveWeaponIndex == 0 ? 1 : 0;
            mgr.SwitchToWeaponIndex(next);
            Debug.Log("[F5 switch] -> slot " + next);
        }


        [MenuItem(Menu + "Diag renderers near camera (play)")]
        static void DiagNear()
        {
            var cam = Camera.main; var sb = new StringBuilder();
            var mgr = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerWeaponsManager>();
            sb.AppendLine("activeIndex=" + (mgr ? mgr.ActiveWeaponIndex : -1) + " t=" + Time.time);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !cam) continue;
                var vp = cam.WorldToViewportPoint(r.bounds.center);
                if (vp.z < 0 || vp.z > 1.6f || vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1) continue;
                sb.AppendLine($"{FullPath(r.transform)} [{r.GetType().Name}] vp={vp} mat={(r.sharedMaterial ? r.sharedMaterial.name : "-")}");
            }
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f5-near.txt", sb.ToString()); Debug.Log("[F5 near] written");
        }
        static string FullPath(Transform t) { var s = t.name; while ((t = t.parent)) s = t.name + "/" + s; return s; }

        static void Describe(StringBuilder sb, Renderer r)
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) return;
            sb.Append($"  {Path(r.transform)} [{r.GetType().Name}] layer={LayerMask.LayerToName(r.gameObject.layer)} en={r.enabled} active={r.gameObject.activeInHierarchy}");
            foreach (var m in r.sharedMaterials)
            {
                if (!m) { sb.Append(" | null"); continue; }
                sb.Append($" | {m.name} ({m.shader.name}) {AssetDatabase.GetAssetPath(m)}");
                foreach (var p in new[] { "_BaseColor", "_Color", "_Tint" }) if (m.HasProperty(p)) sb.Append($" {p}={m.GetColor(p)}");
                foreach (var p in new[] { "_Smoothness", "_Metallic", "_EdgeWear", "_Dirt", "_OcclusionStrength", "_BumpScale" }) if (m.HasProperty(p)) sb.Append($" {p}={m.GetFloat(p):F2}");
                if (m.HasProperty("_EmissionColor")) sb.Append($" emis={m.GetColor("_EmissionColor")}");
                if (m.HasProperty("_MetallicGlossMap")) sb.Append($" metalMap={(m.GetTexture("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap").name : "none")}");
                if (m.HasProperty("_BaseMap")) sb.Append($" baseMap={(m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")}");
            }
            sb.AppendLine();
        }
        static string Path(Transform t) { var s = t.name; int n = 0; while ((t = t.parent) && n++ < 4) s = t.name + "/" + s; return s; }
    }
}
