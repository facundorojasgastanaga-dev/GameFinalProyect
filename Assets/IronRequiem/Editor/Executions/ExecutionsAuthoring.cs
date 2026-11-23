using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Executions v02 (F2) prefab authoring. Run once from the Editor (menu or `ExecutionsAuthoring.Apply()` through the
    // request queue / execute-method). Idempotent: it only adds what is missing, re-assigns the same clips and sets the same
    // flag. Scenes are never opened or saved. A copy of every touched prefab goes to Tools/IronRequiem/Backups/<date>/Executions.
    //  - T07_Current_Repaired, T07_Mech, SawBot_Current, BigBot_Current: reaction layers + ExecutionRobotAudio with its clips.
    //  - BigBot_Current: EnemyExecutionSource.executionEnabled = true (approved in F1). Map01's BigBot_Map01 / T07_Mech_Map01 are
    //    prefab variants and inherit everything (they are not touched).
    // NOTE: Editor/HangarIntro/CombatV4Authoring.cs (protected zone) sets executionEnabled=false on the Big Bot again if re-run.
    public static class ExecutionsAuthoring
    {
        const string CampaignFolder = "Assets/IronRequiem/Prefabs/Campaign/";
        static readonly string[] Prefabs = { "T07_Current_Repaired.prefab", "T07_Mech.prefab", "SawBot_Current.prefab", "BigBot_Current.prefab" };

        // (serialized field of ExecutionRobotAudio, asset path)
        static readonly (string field, string path)[] Clips =
        {
            ("fistImpact", "Assets/IronRequiem/Audio/SFX/V4/IR_Fist_HeavyImpact.wav"),
            ("clamp", "Assets/IronRequiem/Audio/SFX/V4/IR_Mount_Clamp.wav"),
            ("mountLock", "Assets/IronRequiem/Audio/SFX/V4/IR_Mount_Lock.wav"),
            ("deflect", "Assets/IronRequiem/Audio/SFX/V4/IR_Slide_Deflect.wav"),
            ("blast", "Assets/IronRequiem/Audio/SFX/V4/IR_BigBot_Blast.wav"),
            ("tear", "Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_Tear.wav"),
            ("spark", "Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_Spark.wav"),
            ("footMetal", "Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_FootMetal.wav"),
            ("hydraulic", "Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_Hydraulic.wav"),
            ("cannonArm", "Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_CannonArm.wav"),
            ("creak", "Assets/IronRequiem/Audio/SFX/HangarIntro_v2/IR2_Capsule_Creak.wav"),
            ("subBoom", "Assets/IronRequiem/Audio/SFX/HangarIntro_v2/IR2_Impact_SubBoom.wav"),
            ("motorLoop", "Assets/IronRequiem/Audio/SFX/SawBot/SawMotor_Attack_Loop.wav"),
            ("jump", "Assets/FPS/Audio/SFX/Player/Jump.wav"),
            ("land", "Assets/FPS/Audio/SFX/Player/Land.wav"),
        };

        [MenuItem("Tools/Iron Requiem/Executions/Apply Robot Reaction Prefabs")]
        static void Menu() => Debug.Log(Apply());

        public static string Apply()
        {
            var log = new StringBuilder("ExecutionsAuthoring.Apply\n");
            string backup = "Tools/IronRequiem/Backups/" + System.DateTime.Now.ToString("yyyy-MM-dd") + "/Executions";
            Directory.CreateDirectory(backup);
            var loaded = new AudioClip[Clips.Length];
            for (int i = 0; i < Clips.Length; i++)
            {
                loaded[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(Clips[i].path);
                if (!loaded[i]) log.AppendLine("  MISSING clip " + Clips[i].path);
            }
            foreach (var file in Prefabs)
            {
                string path = CampaignFolder + file;
                if (!File.Exists(path)) { log.AppendLine("  SKIP (not found) " + path); continue; }
                string copy = Path.Combine(backup, file);
                if (!File.Exists(copy)) File.Copy(path, copy);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var source = root.GetComponent<EnemyExecutionSource>();
                    if (!source) { log.AppendLine("  SKIP (no EnemyExecutionSource on root) " + file); continue; }
                    var added = new StringBuilder();
                    // PullIn first: ExecutionReaction reads it in Awake.
                    Ensure<ExecutionPullIn>(root, added); Ensure<ExecutionReaction>(root, added); Ensure<ExecutionWeaponDetach>(root, added);
                    Ensure<ExecutionLocalFlash>(root, added); Ensure<ExecutionHealthBarHider>(root, added); Ensure<ExecutionDebrisGuard>(root, added);
                    var audio = Ensure<ExecutionRobotAudio>(root, added);
                    var so = new SerializedObject(audio);
                    for (int i = 0; i < Clips.Length; i++)
                    {
                        var property = so.FindProperty(Clips[i].field);
                        if (property == null) { log.AppendLine("  field not found: " + Clips[i].field); continue; }
                        property.objectReferenceValue = loaded[i];
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    if (file == "BigBot_Current.prefab")
                    {
                        var sourceSo = new SerializedObject(source);
                        var flag = sourceSo.FindProperty("executionEnabled");
                        if (flag != null) { flag.boolValue = true; sourceSo.ApplyModifiedPropertiesWithoutUndo(); added.Append(" executionEnabled=true"); }
                        else log.AppendLine("  executionEnabled field not found");
                        // Round 2: visible-body proxy (bone-space boxes of the LOD0 parts), rebuilt every run from the meshes.
                        var proxy = Ensure<ExecutionBodyProxy>(root, added);
                        proxy.SetParts(BakeParts(root, log));
                        EditorUtility.SetDirty(proxy);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.AppendLine("  OK " + file + (added.Length > 0 ? " added:" + added : " (already configured)") + " | " + root.GetComponentsInChildren<Transform>(true).Length + " transforms");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            log.AppendLine("Backup: " + backup);
            return log.ToString();
        }

        // One box per rigid LOD0 skinned piece ("Torso_LOD0" -> bone "Torso"), in that bone's space, from the mesh vertices placed
        // through the bind pose (BakeMesh returns degenerate vertices for these skins).
        // Pieces listed in BigBotDestruction.CannonViews are weapon parts.
        static ExecutionBodyProxy.Part[] BakeParts(GameObject root, StringBuilder log)
        {
            var result = new System.Collections.Generic.List<ExecutionBodyProxy.Part>();
            var destruction = root.GetComponent<BigBotDestruction>();
            var cannon = destruction && destruction.CannonViews != null ? new System.Collections.Generic.HashSet<Renderer>(destruction.CannonViews) : new System.Collections.Generic.HashSet<Renderer>();
            var vertices = new System.Collections.Generic.List<Vector3>();
            {
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    int lod = skin.name.IndexOf("_LOD0", System.StringComparison.Ordinal);
                    if (lod <= 0 || !skin.sharedMesh) continue;
                    string boneName = skin.name.Substring(0, lod);
                    Transform bone = null; int boneIndex = -1; var bones = skin.bones;
                    for (int b = 0; b < bones.Length; b++) if (bones[b] && bones[b].name == boneName) { bone = bones[b]; boneIndex = b; break; }
                    var bindposes = skin.sharedMesh.bindposes;
                    if (!bone || boneIndex >= bindposes.Length) { log.AppendLine("  proxy: no bone " + boneName); continue; }
                    // mesh space -> bone space through the bind pose: independent of the pose the prefab happens to be saved in
                    skin.sharedMesh.GetVertices(vertices);
                    if (vertices.Count == 0) { log.AppendLine("  proxy: no vertices " + skin.name); continue; }
                    var toBone = bindposes[boneIndex];
                    Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                    foreach (var v in vertices) { var l = toBone.MultiplyPoint3x4(v); min = Vector3.Min(min, l); max = Vector3.Max(max, l); }
                    bool weapon = false; foreach (var r in cannon) if (r && r.name.StartsWith(boneName + "_LOD", System.StringComparison.Ordinal)) { weapon = true; break; }
                    Vector3[] samples = null;
                    if (weapon)
                    {
                        int step = Mathf.Max(1, vertices.Count / 256); var list = new System.Collections.Generic.List<Vector3>();
                        for (int v = 0; v < vertices.Count; v += step) list.Add(toBone.MultiplyPoint3x4(vertices[v]));
                        samples = list.ToArray();
                    }
                    result.Add(new ExecutionBodyProxy.Part { bone = bone, center = (min + max) * .5f, halfSize = (max - min) * .5f, weapon = weapon, points = samples });
                    var s = bone.TransformVector(max - min);
                    log.AppendLine($"  proxy {boneName}{(weapon ? " (weapon)" : "")}: {vertices.Count} verts, world size ~({Mathf.Abs(s.x):F2}, {Mathf.Abs(s.y):F2}, {Mathf.Abs(s.z):F2})");
                }
            }
            return result.ToArray();
        }

        static T Ensure<T>(GameObject root, StringBuilder added) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component) return component;
            added.Append(" ").Append(typeof(T).Name);
            return root.AddComponent<T>();
        }
    }
}
