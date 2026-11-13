using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// Adds the new enemies to the campaign rounds (CombatTwoRoomsLab waves in each stage scene).
    /// Adds groups next to the existing ones; nothing is replaced. Scenes are backed up first. Idempotent.
    public static class VisualPassCampaignSpawns
    {
        const string MechTest = "Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_Test.prefab";
        public const string MechCampaign = "Assets/IronRequiem/Prefabs/Campaign/T07_Mech.prefab";
        const string CarroneroPrefab = "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab";
        const string BackupDir = "Tools/IronRequiem/Backups/Spawns";

        [MenuItem("Tools/Iron Requiem/Visual Pass/T4 Audit campaign spawns")]
        static void Audit() => Run(false);

        [MenuItem("Tools/Iron Requiem/Visual Pass/T4 Add mech T-07 to campaign rounds")]
        static void Apply() => Run(true);

        // Carroñero hordes, next to the existing groups: room 2 round 2 = 8, room 3 last round = 14, room 4 last round = 20.
        [MenuItem("Tools/Iron Requiem/Visual Pass/T4 Add Carronero hordes to campaign rounds")]
        static void ApplyHordes() => Run(true, true);

        static void Run(bool apply, bool hordes = false)
        {
            var sb = new StringBuilder($"[campaign spawns] apply={apply}\n");
            if (EditorApplication.isPlaying) { Debug.LogWarning("[campaign spawns] stop play mode first"); return; }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning("[campaign spawns] save open scenes first: " + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path); return; }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                GameObject mech = null;
                if (apply)
                {
                    if (!AssetDatabase.LoadAssetAtPath<GameObject>(MechCampaign))
                    {
                        // Campaign copy of the tested mech; the test prefab stays for the test scene.
                        if (!AssetDatabase.CopyAsset(MechTest, MechCampaign)) { sb.AppendLine("  copy failed " + MechTest); return; }
                        sb.AppendLine("  created " + MechCampaign);
                    }
                    mech = AssetDatabase.LoadAssetAtPath<GameObject>(MechCampaign);
                    if (hordes) mech = AssetDatabase.LoadAssetAtPath<GameObject>(CarroneroPrefab);
                    if (!mech) { sb.AppendLine("  missing prefab"); return; }
                }

                var buildScenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
                string[] stages = null;
                foreach (var path in buildScenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var flow = Object.FindAnyObjectByType<CampaignFlow>();
                    if (flow) { stages = flow.StageScenes; sb.AppendLine($"  flow in {path}: {string.Join(", ", stages)}"); break; }
                }
                if (stages == null) { sb.AppendLine("  no CampaignFlow in build scenes"); return; }

                if (apply) Directory.CreateDirectory(BackupDir);
                int stageIndex = -1;
                foreach (var stageName in stages)
                {
                    var path = buildScenes.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == stageName);
                    if (path == null) { sb.AppendLine($"  {stageName}: not in build settings"); continue; }
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var lab = Object.FindAnyObjectByType<CombatTwoRoomsLab>(FindObjectsInactive.Include);
                    if (!lab) { sb.AppendLine($"  {stageName}: no rounds (chase/other)"); continue; }
                    stageIndex++;
                    var so = new SerializedObject(lab); var waves = so.FindProperty("waves");
                    sb.AppendLine($"  {stageName} (stage {stageIndex}) waves={waves.arraySize}");
                    bool changed = false;
                    for (int w = 0; w < waves.arraySize; w++)
                    {
                        var wave = waves.GetArrayElementAtIndex(w);
                        // last round of every sector; second round too from sector 2 on
                        bool wants = w == waves.arraySize - 1 || (stageIndex >= 1 && w == waves.arraySize - 2);
                        int count = 1;
                        if (hordes)
                        {
                            count = stageIndex == 1 ? 8 : stageIndex == 2 ? 14 : stageIndex >= 3 ? 20 : 0;
                            wants = count > 0 && (stageIndex == 1 ? w == 1 : w == waves.arraySize - 1);
                        }
                        changed |= Visit(wave.FindPropertyRelative("Groups"), $"    w{w} base", wants, mech, apply, sb, count);
                        var vars = wave.FindPropertyRelative("Variations");
                        for (int v = 0; v < vars.arraySize; v++)
                        {
                            var comp = vars.GetArrayElementAtIndex(v);
                            changed |= Visit(comp.FindPropertyRelative("Groups"), $"    w{w} var '{comp.FindPropertyRelative("Name").stringValue}'", wants, mech, apply, sb, count);
                        }
                    }
                    if (apply && changed)
                    {
                        File.Copy(path, Path.Combine(BackupDir, Path.GetFileName(path) + ".bak"), true);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                        sb.AppendLine("    saved (backup in " + BackupDir + ")");
                    }
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Debug.Log(sb.ToString());
            }
        }

        static bool Visit(SerializedProperty groups, string label, bool wants, GameObject mech, bool apply, StringBuilder sb, int count = 1)
        {
            if (groups == null) return false;
            var line = new StringBuilder(label + ":");
            bool has = false;
            for (int i = 0; i < groups.arraySize; i++)
            {
                var g = groups.GetArrayElementAtIndex(i);
                var prefab = g.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;
                line.Append($" {(prefab ? prefab.name : "null")}x{g.FindPropertyRelative("Count").intValue}");
                if (mech && prefab == mech) has = true;
                if (prefab && mech && prefab.name == mech.name) has = true;
            }
            bool add = apply && wants && !has && groups.arraySize > 0;
            if (add)
            {
                int i = groups.arraySize; groups.arraySize++;
                var g = groups.GetArrayElementAtIndex(i);
                g.FindPropertyRelative("Prefab").objectReferenceValue = mech;
                g.FindPropertyRelative("Count").intValue = count;
                g.FindPropertyRelative("FixedPoint").objectReferenceValue = null;
                line.Append($"  + {mech.name} x{count}");
            }
            sb.AppendLine(line.ToString());
            return add;
        }
    }
}
