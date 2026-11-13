using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class ThermalHudRepairAuthoring
    {
        public const string Report = "Tools/IronRequiem/Reports/ThermalHudRepair";
        const string Queue = "Tools/IronRequiem/Requests/thermal-hud-repair.txt";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static readonly string[] Names = { "SawBotLab", "CombatVarietyLab", "CombatTwoRoomsLab", "BunkerLab", "SawBotSustainLab", "SawBotWeaponDeathLab", "SawBotSawPolishLab" };
        static ThermalHudRepairAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue) || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/ThermalHudRepairAuthoring.cs") > Loaded) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            var op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { Inspect(); if (op == "apply") Apply(); IronRequiemSetupTools.WriteAudit(); }
            catch (Exception e) { File.WriteAllText(Report + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static void Inspect()
        {
            var text = new StringBuilder();
            foreach (string name in Names)
            {
                string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    text.AppendLine(path + " HUDs=" + All<WeaponHeatHud>(scene).Length);
                    foreach (var inventory in All<PlayerWeaponsManager>(scene))
                        foreach (var w in inventory.StartingWeapons) text.AppendLine(" weapon=" + AssetDatabase.GetAssetPath(w) + " heat=" + (w && w.GetComponent<WeaponHeat>()) + " rate=" + w.DelayBetweenShots);
                    foreach (var h in All<WeaponHeatHud>(scene))
                    {
                        var data = new SerializedObject(h); text.AppendLine(" hud=" + h.name + " active=" + h.gameObject.activeInHierarchy + " enabled=" + h.enabled + " inventory=" + data.FindProperty("inventory").objectReferenceValue);
                    }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Report + "/inspection.txt", text.ToString());
        }
        static void Apply()
        {
            var result = new StringBuilder();
            foreach (string name in Names)
            {
                string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var inventory = All<PlayerWeaponsManager>(scene).Single(); var primary = inventory.StartingWeapons[0];
                    bool missingHud = All<WeaponHeatHud>(scene).Length == 0;
                    if (primary.GetComponent<WeaponHeat>() && !missingHud) { result.AppendLine(name + " already connected"); continue; }
                    var thermal = ThermalMenuAuthoring.HeatVariant(primary);
                    if (thermal != primary)
                    {
                        inventory.StartingWeapons[0] = thermal;
                        // The arm presentation compares source prefab identity. Retarget together with the inventory.
                        foreach (var component in inventory.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            var data = new SerializedObject(component); var p = data.GetIterator(); bool changed = false;
                            while (p.Next(true))
                            {
                                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                                if (p.objectReferenceValue == primary) { p.objectReferenceValue = thermal; changed = true; }
                                else if (p.objectReferenceValue == primary.gameObject) { p.objectReferenceValue = thermal.gameObject; changed = true; }
                            }
                            if (changed) { data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(component); if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
                        }
                        EditorUtility.SetDirty(inventory); PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
                    }
                    ThermalMenuAuthoring.UseFont(All<TMPro.TMP_Text>(scene).First(t => t.font).font);
                    ThermalMenuAuthoring.AddHeatHud(scene, inventory);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    result.AppendLine(name + " primary=" + AssetDatabase.GetAssetPath(thermal) + " HUD restored; secondary unchanged");
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Report + "/apply.txt", result.ToString());
        }
    }
}
