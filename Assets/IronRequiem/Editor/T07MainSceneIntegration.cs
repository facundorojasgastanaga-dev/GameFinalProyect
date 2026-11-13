using System;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    // Explicit local job for the requested MainScene migration; never writes a dirty user scene.
    [InitializeOnLoad]
    public static class T07MainSceneIntegration
    {
        const string Main = "Assets/FPS/Scenes/MainScene.unity";
        const string Request = "Tools/IronRequiem/Requests/t07-main.json";
        const string Status = "Tools/IronRequiem/Requests/t07-main-status.json";
        const string Backup = "Tools/IronRequiem/Backups/20261030-main-t07-destructible/";
        const string Key = "IronRequiem.MainDestructionCheck";
        static double started;
        static int frame;

        [Serializable] sealed class Receipt
        {
            public string operation, result, detail;
            public int enemies, destructible, missingScripts;
        }

        static T07MainSceneIntegration() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string phase = SessionState.GetString(Key, "");
            if (phase == "running" && EditorApplication.isPlaying)
            {
                if (frame == 0) { frame = Time.frameCount; started = EditorApplication.timeSinceStartup; }
                if (Time.frameCount < frame + 30 || EditorApplication.timeSinceStartup < started + .5) return;
                try { VerifyRuntime(); }
                catch (Exception e) { Write("verify", "FAILED", e.ToString()); Debug.LogException(e); }
                finally { SessionState.SetString(Key, "finish"); EditorApplication.ExitPlaymode(); }
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (phase == "finish")
            {
                SessionState.EraseString(Key); IronRequiemSetupTools.WriteAudit(); return;
            }
            if (!File.Exists(Request)) return;
            string operation = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                if (operation == "integrate") Integrate();
                else if (operation == "verify")
                {
                    T07AnimationBuilder.RequireClean();
                    if (SceneManager.GetActiveScene().path != Main) throw new InvalidOperationException("Open MainScene before checking.");
                    SessionState.SetString(Key, "running"); frame = 0; EditorApplication.EnterPlaymode();
                }
                else throw new InvalidOperationException("Unknown MainScene operation.");
            }
            catch (Exception e) { Write(operation, "FAILED", e.ToString()); Debug.LogException(e); }
        }

        static void Integrate()
        {
            T07AnimationBuilder.RequireClean();
            if (!File.Exists(Backup + Main) || !File.ReadAllBytes(Main).SequenceEqual(File.ReadAllBytes(Backup + Main)))
                throw new InvalidOperationException("MainScene changed after its backup; preserve the newer version first.");
            var scene = EditorSceneManager.OpenScene(Main, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyController>(true)).ToArray();
            var candidates = all.Where(e => e.GetComponent<EnemyMobile>() &&
                (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(e.gameObject) == "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab" ||
                 PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(e.gameObject) == T07OptimizationBuilder.Enemy ||
                 PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(e.gameObject) == CharacterPrototypeBuilder.EnemyPath)).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("No matching common mobile enemies in MainScene.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(T07DestructionBuilder.Enemy);
            if (!prefab || !prefab.GetComponent<RobotDamageProgression>()) throw new InvalidOperationException("Destructible variant missing.");
            var patrols = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PatrolPath>(true)).ToArray();
            foreach (var enemy in candidates)
            {
                var t = enemy.transform;
                var p = t.position; var q = t.rotation; var s = t.localScale; var parent = t.parent;
                int order = t.GetSiblingIndex(); bool active = enemy.gameObject.activeSelf;
                int assignments = patrols.Sum(path => path.EnemiesToAssign.Count(e => e == enemy));
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(enemy.gameObject, prefab,
                    new PrefabReplacingSettings { objectMatchMode = ObjectMatchMode.ByHierarchy,
                        prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides, changeRootNameToAssetName = true },
                    InteractionMode.AutomatedAction);
                if (t.position != p || t.rotation != q || t.localScale != s || t.parent != parent || t.GetSiblingIndex() != order ||
                    enemy.gameObject.activeSelf != active || patrols.Sum(path => path.EnemiesToAssign.Count(e => e == enemy)) != assignments)
                    throw new InvalidOperationException("Migration changed a transform, active state, or patrol reference.");
                if (!enemy.GetComponent<RobotDamageProgression>() || !enemy.GetComponent<T07AnimationDriver>())
                    throw new InvalidOperationException("Migrated instance is incomplete.");
                Debug.Log("MainScene T07 replaced at " + p + "; patrol assignments=" + assignments, enemy);
            }
            if (Missing(scene) != 0) throw new InvalidOperationException("Missing scripts after migration.");
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("MainScene save failed.");
            Selection.activeGameObject = candidates[0].gameObject;
            Write("integrate", "SUCCESS", "Replaced " + candidates.Length + " common enemies. Other enemy types, player, level, navigation data and source models preserved.");
            IronRequiemSetupTools.WriteAudit();
        }

        static int Missing(Scene scene) => scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true)
            .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));

        static void VerifyRuntime()
        {
            var scene = SceneManager.GetActiveScene();
            var robots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RobotDamageProgression>()).ToArray();
            if (scene.path != Main || robots.Length == 0 || Missing(scene) != 0) throw new InvalidOperationException("MainScene robot/reference check failed.");
            foreach (var robot in robots)
            {
                var enemy = robot.GetComponent<EnemyController>(); var nav = robot.GetComponent<NavMeshAgent>();
                if (!enemy || !nav || !nav.enabled || !nav.isOnNavMesh || !robot.GetComponent<Health>() ||
                    !robot.GetComponent<EnemyMobile>().Animator || !robot.GetComponentsInChildren<LODGroup>().Any(g => g.name == "Body"))
                    throw new InvalidOperationException("T07 not ready for combat/navigation in MainScene: " + robot.name);
            }
            Write("verify", "SUCCESS", "Play Mode advanced " + (Time.frameCount - frame) + " frames; all " + robots.Length + " T07 active on existing NavMesh with Health, Animator, damage progression and LOD references.");
        }

        static void Write(string operation, string result, string detail)
        {
            var scene = SceneManager.GetActiveScene();
            var all = scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
            File.WriteAllText(Status, JsonUtility.ToJson(new Receipt { operation = operation, result = result, detail = detail,
                enemies = all.Sum(r => r.GetComponentsInChildren<EnemyController>(true).Length),
                destructible = all.Sum(r => r.GetComponentsInChildren<RobotDamageProgression>(true).Length),
                missingScripts = scene.IsValid() && scene.isLoaded ? Missing(scene) : 0 }, true));
        }
    }
}
