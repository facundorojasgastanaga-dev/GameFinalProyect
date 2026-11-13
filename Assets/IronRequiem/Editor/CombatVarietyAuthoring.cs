using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using Unity.FPS.AI;
using Unity.FPS.Game;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class CombatVarietyAuthoring
    {
        public const string Report = "Tools/IronRequiem/Reports/CombatVariety";
        const string Queue = "Tools/IronRequiem/Requests/combat-variety.txt";
        public static readonly string[] MenuScenes = { "SawBotLab", "SawBotSustainLab", "SawBotWeaponDeathLab", "SawBotSawPolishLab" };
        static readonly DateTime Loaded = DateTime.UtcNow;
        static CombatVarietyAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue)) return;
            if (File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/CombatVarietyAuthoring.cs") > Loaded || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { if (op == "menu") BuildMenus(); else if (op == "inspect") Inspect(); else if (op == "spawn") BuildVariety(); else throw new Exception(op); IronRequiemSetupTools.WriteAudit(); }
            catch (Exception exception) { File.WriteAllText(Report + "/error.txt", exception.ToString()); Debug.LogException(exception); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        static void Visit(string name, Action<Scene> action, bool save)
        {
            string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { action(scene); if (save) { EnsureNavigation(scene); EditorSceneManager.SaveScene(scene); } }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        static void EnsureNavigation(Scene scene)
        {
            // NavMeshSurface.OnValidate clears data shared by simultaneously open scene copies.
            // Give each known laboratory its own copy of the existing bake, rather than rebaking.
            string folder = "Assets/IronRequiem/Art/Levels/CombatVariety"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            foreach (var surface in All<Unity.AI.Navigation.NavMeshSurface>(scene))
            {
                string target = folder + "/" + scene.name + "_NavMesh.asset";
                if (!AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(target))
                {
                    string source = surface.navMeshData ? AssetDatabase.GetAssetPath(surface.navMeshData) : AssetDatabase.GUIDToAssetPath(scene.name == "CombatTwoRoomsLab" ? "2f6155501e333cf4190750fa9fe3eb7b" : "c1206c16a9a9d624d8871040537eb9f7");
                    if (!AssetDatabase.CopyAsset(source, target)) throw new Exception("Cannot preserve baked navigation: " + scene.name);
                }
                surface.RemoveData(); surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(target); surface.AddData(); EditorUtility.SetDirty(surface);
            }
        }
        static void Inspect()
        {
            var text = new StringBuilder();
            foreach (string name in new[] { "SawBotLab", "CombatTwoRoomsLab", "BunkerLab" }) Visit(name, scene =>
            {
                text.AppendLine(name);
                foreach (var lab in All<CombatTwoRoomsLab>(scene))
                {
                    var data = new SerializedObject(lab); var waves = data.FindProperty("waves");
                    for (int i = 0; i < waves.arraySize; i++)
                    {
                        var groups = waves.GetArrayElementAtIndex(i).FindPropertyRelative("Groups");
                        for (int j = 0; j < groups.arraySize; j++) { var group = groups.GetArrayElementAtIndex(j); text.AppendLine(" wave=" + i + " prefab=" + AssetDatabase.GetAssetPath(group.FindPropertyRelative("Prefab").objectReferenceValue) + " count=" + group.FindPropertyRelative("Count").intValue + " fixed=" + group.FindPropertyRelative("FixedPoint").objectReferenceValue); }
                    }
                    foreach (string field in new[] { "firstRoomSpawns", "secondRoomSpawns" }) { var points = data.FindProperty(field); for (int i = 0; i < points.arraySize; i++) { var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform; text.AppendLine(" " + field + " " + point.name + " " + point.position); } }
                }
            }, false);
            File.WriteAllText(Report + "/inspection.txt", text.ToString());
        }
        static void BuildMenus()
        {
            foreach (string name in MenuScenes) Visit(name, scene =>
            {
                ThermalMenuAuthoring.UseFont(All<TMP_Text>(scene).First(t => t.font).font);
                var lab = All<CombatLabMenu>(scene).Single(); var player = All<PlayerCharacterController>(scene).Single(); var canvas = lab.GetComponent<Canvas>(); var serialized = new SerializedObject(lab);
                var oldMain = serialized.FindProperty("mainPanel").objectReferenceValue as GameObject; var death = serialized.FindProperty("deathPanel").objectReferenceValue as GameObject;
                if (!canvas.transform.Find("IndustrialShell"))
                {
                    var play = oldMain.GetComponentInChildren<UnityEngine.UI.Button>(true); var loader = play.GetComponent<LoadSceneButton>(); if (!loader) loader = play.gameObject.AddComponent<LoadSceneButton>();
                    ThermalMenuAuthoring.BuildMenu(canvas, null, loader, scene.name); lab.Configure(player, canvas.transform.Find("IndustrialShell").gameObject, death); lab.GetComponent<RequiemMenuView>().ConfigureLab(lab);
                    var navigation = lab.GetComponent<MenuNavigation>(); if (!navigation) navigation = lab.gameObject.AddComponent<MenuNavigation>(); navigation.ManageCursorAtStart = false; navigation.DefaultSelection = play;
                    var background = death.GetComponent<UnityEngine.UI.Image>(); if (background) background.color = new Color(.008f, .02f, .028f, .96f);
                    foreach (var button in death.GetComponentsInChildren<UnityEngine.UI.Button>(true)) ThermalMenuAuthoring.StyleButton(button);
                    foreach (var label in death.GetComponentsInChildren<TMP_Text>(true)) label.color = new Color(.88f, .95f, .95f);
                }
                var managers = All<InGameMenuManager>(scene); var pause = managers.FirstOrDefault(p => p.gameObject.activeInHierarchy); if (!pause) pause = managers.First();
                foreach (var other in managers) if (other != pause) { other.enabled = false; if (other.MenuRoot) other.MenuRoot.SetActive(false); }
                pause.UseIndustrialMenu = true; pause.enabled = false; ThermalMenuAuthoring.BuildMenu(pause.MenuRoot.GetComponent<Canvas>(), pause, null, scene.name);
                pause.GetComponent<RequiemMenuView>().ConfigureLab(lab); lab.ConfigurePause(pause);
                foreach (var label in All<TMP_Text>(scene).Where(t => t.transform.parent && t.transform.parent.name == "TABImage")) label.text = "Esc";
                EditorUtility.SetDirty(lab); EditorUtility.SetDirty(pause);
                if (!lab.GetComponent<RequiemMenuView>() || !canvas.transform.Find("IndustrialShell") || !pause.UseIndustrialMenu) throw new Exception("Incomplete menu: " + name);
            }, true);
            File.WriteAllText(Report + "/menu.txt", "Existing industrial main/options/pause reused in four Saw Bot labs; existing death/restart flow and inventories retained."); Inspect();
        }
        public const string Lab = "Assets/IronRequiem/Scenes/CombatVarietyLab.unity";
        static void BuildVariety()
        {
            var bunkerPool = new Dictionary<int, GameObject>();
            Visit("BunkerLab", scene =>
            {
                var data = new SerializedObject(All<CombatTwoRoomsLab>(scene).Single()); var waves = data.FindProperty("waves");
                for (int w = 0; w < waves.arraySize; w++) { var groups = waves.GetArrayElementAtIndex(w).FindPropertyRelative("Groups"); for (int i = 0; i < groups.arraySize; i++) { var prefab = groups.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue as GameObject; bunkerPool[Kind(prefab)] = prefab; } }
            }, false);
            var saw = AssetDatabase.LoadAssetAtPath<GameObject>(SawBotSawPolishAuthoring.Enemy); bunkerPool[1] = saw;
            foreach (string name in new[] { "SawBotLab", "BunkerLab", "CombatTwoRoomsLab" }) Visit(name, scene => ConfigureVariety(scene, bunkerPool), true);
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab))
            {
                if (!AssetDatabase.CopyAsset("Assets/IronRequiem/Scenes/SawBotLab.unity", Lab)) throw new Exception("Cannot copy variety lab");
            }
            Visit("CombatVarietyLab", scene => { }, true);
            AssetDatabase.SaveAssets(); File.WriteAllText(Report + "/spawn.txt", "Three bounded composition profiles per wave in SawBotLab, BunkerLab and CombatTwoRoomsLab. Preserve total counts and fixed turret; shared existing queue, stagger0.2-0.4s, current player distance/nav/path/physical occupancy rechecked on activation. Separate CombatVarietyLab.");
        }
        static int Kind(GameObject prefab)
        {
            if (!prefab) throw new Exception("Missing enemy prefab");
            if (prefab.GetComponent<SawBotCombat>()) return 1;
            if (prefab.GetComponent<BigBotCombat>()) return 3;
            if (prefab.GetComponent<EnemyTurret>()) return 4;
            if (prefab.GetComponent<RobotDamageProgression>()) return 0;
            if (prefab.GetComponent<EnemyMobile>()) return 2;
            throw new Exception("Unidentified enemy: " + prefab.name);
        }
        static void ConfigureVariety(Scene scene, Dictionary<int, GameObject> fallback)
        {
            var lab = All<CombatTwoRoomsLab>(scene).Single(); var data = new SerializedObject(lab); var waves = data.FindProperty("waves"); var pool = new Dictionary<int, GameObject>(fallback);
            for (int w = 0; w < waves.arraySize; w++) { var groups = waves.GetArrayElementAtIndex(w).FindPropertyRelative("Groups"); for (int i = 0; i < groups.arraySize; i++) { var prefab = groups.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue as GameObject; pool[Kind(prefab)] = prefab; } }
            foreach (var prefab in pool.Values) if (!prefab.GetComponent<Health>() || !prefab.GetComponent<EnemyController>() || prefab.GetComponentsInChildren<Component>(true).Any(c => !c)) throw new Exception("Invalid enemy: " + prefab.name);
            int[][][] recipes = {
                new[] { new[]{0,1,0,2,1,0,2}, new[]{1,0,2,1,0,2,0}, new[]{2,1,0,2,0,1,0} },
                new[] { new[]{3,0,1,2,0,1,2}, new[]{3,1,2,0,1,0,2}, new[]{0,2,3,1,0,1,2} },
                new[] { new[]{3,2,1,3,0,2,1}, new[]{3,0,2,1,3,0,2}, new[]{1,3,2,0,3,1,2} },
                new[] { new[]{3,1,3,0,2,1,0}, new[]{3,2,0,3,1,2,0}, new[]{1,3,2,0,3,0,1} }
            };
            for (int w = 0; w < waves.arraySize; w++)
            {
                var wave = waves.GetArrayElementAtIndex(w); var groups = wave.FindPropertyRelative("Groups"); int mobileCount = 0; var fixedGroups = new List<CombatTwoRoomsLab.Group>();
                for (int i = 0; i < groups.arraySize; i++) { var group = groups.GetArrayElementAtIndex(i); var point = group.FindPropertyRelative("FixedPoint").objectReferenceValue as Transform; int count = group.FindPropertyRelative("Count").intValue; if (point) fixedGroups.Add(new CombatTwoRoomsLab.Group { Prefab = group.FindPropertyRelative("Prefab").objectReferenceValue as GameObject, Count = count, FixedPoint = point }); else mobileCount += count; }
                var variants = wave.FindPropertyRelative("Variations"); variants.arraySize = 3;
                for (int v = 0; v < 3; v++)
                {
                    var selected = new Dictionary<GameObject, int>(); var recipe = recipes[Math.Min(w, 3)][v];
                    for (int i = 0; i < mobileCount; i++) { var prefab = pool[recipe[i % recipe.Length]]; selected[prefab] = selected.TryGetValue(prefab, out int count) ? count + 1 : 1; }
                    var composition = variants.GetArrayElementAtIndex(v); composition.FindPropertyRelative("Name").stringValue = new[] { "Presión mixta", "Flancos móviles", "Apoyo y asalto" }[v]; var list = composition.FindPropertyRelative("Groups"); list.arraySize = selected.Count + fixedGroups.Count; int index = 0;
                    void Add(GameObject prefab, int count, Transform point) { var item = list.GetArrayElementAtIndex(index++); item.FindPropertyRelative("Prefab").objectReferenceValue = prefab; item.FindPropertyRelative("Count").intValue = count; item.FindPropertyRelative("FixedPoint").objectReferenceValue = point; }
                    foreach (var item in selected) Add(item.Key, item.Value, null); foreach (var group in fixedGroups) Add(group.Prefab, group.Count, group.FixedPoint);
                }
            }
            data.FindProperty("variedSpawns").boolValue = true; data.FindProperty("scenarioSeed").intValue = 0; data.FindProperty("minimumSpawnInterval").floatValue = .2f; data.FindProperty("maximumSpawnInterval").floatValue = .4f;
            var queue = data.FindProperty("spawnQueue").objectReferenceValue as EnemySpawnQueue; if (!queue) queue = lab.gameObject.AddComponent<EnemySpawnQueue>(); queue.Configure(1, 1); data.FindProperty("spawnQueue").objectReferenceValue = queue;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(lab); EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
