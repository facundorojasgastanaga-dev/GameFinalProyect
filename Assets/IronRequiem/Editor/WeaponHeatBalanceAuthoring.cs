using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class WeaponHeatBalanceAuthoring
    {
        public const string Report = "Tools/IronRequiem/Reports/WeaponHeatBalance";
        public const string Lab = "Assets/IronRequiem/Scenes/WeaponHeatBalanceLab.unity";
        public const string Primary = "Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_ArmModule_v07_ExtendedHeat.prefab";
        public const string Shotgun = "Assets/IronRequiem/Prefabs/Weapons/Weapon_Shotgun_HeatOnly.prefab";
        const string Queue = "Tools/IronRequiem/Requests/weapon-heat-balance.txt";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static WeaponHeatBalanceAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue) || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/WeaponHeatBalanceAuthoring.cs") > Loaded) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { if (op == "prepare") Prepare(); else if (op == "apply") Apply(); IronRequiemSetupTools.WriteAudit(); }
            catch (Exception e) { File.WriteAllText(Report + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static void Variant(string source, string target, float perShot, float delay)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(target)) return;
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), preview); root.name = Path.GetFileNameWithoutExtension(target);
                var weapon = root.GetComponent<WeaponController>(); weapon.UsesHeatInsteadOfAmmo = true; weapon.AutomaticReload = false; weapon.HasPhysicalBullets = false;
                var reload = root.GetComponent<CylinderBankReload>(); if (reload) reload.enabled = false;
                var heat = new SerializedObject(root.GetComponent<WeaponHeat>()); heat.FindProperty("heatPerShot").floatValue = perShot; heat.FindProperty("coolingDelay").floatValue = delay; heat.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, target);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        static void Prepare()
        {
            Variant("Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_ArmModule_v07_Thermal.prefab", Primary, 2.5f, .6f);
            Variant("Assets/IronRequiem/Prefabs/Weapons/Weapon_Shotgun_Secondary_Thermal.prefab", Shotgun, 12.5f, 1f);
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab))
            {
                if (!AssetDatabase.CopyAsset("Assets/IronRequiem/Scenes/SawBotLab.unity", Lab)) throw new Exception("Lab copy failed");
                string nav = "Assets/IronRequiem/Art/Levels/CombatVariety/WeaponHeatBalanceLab_NavMesh.asset";
                if (!AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(nav)) AssetDatabase.CopyAsset("Assets/IronRequiem/Art/Levels/CombatVariety/SawBotLab_NavMesh.asset", nav);
                var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Additive);
                try { foreach (var surface in All<Unity.AI.Navigation.NavMeshSurface>(scene)) { surface.RemoveData(); surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(nav); surface.AddData(); EditorUtility.SetDirty(surface); } Connect(scene); EditorSceneManager.SaveScene(scene); }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Report + "/prepare.txt", "Variants and isolated lab ready. Primary2.5 heat/shot; shotgun12.5 heat/discharge; max100, recovery35, cooling20/s. Original prefabs preserved.");
        }
        static void Connect(Scene scene)
        {
            var inventory = All<PlayerWeaponsManager>(scene).Single(); var replacements = new Dictionary<Object, Object>();
            for (int i = 0; i < inventory.StartingWeapons.Count; i++)
            {
                var old = inventory.StartingWeapons[i]; string path = AssetDatabase.GetAssetPath(old);
                string target = i == 0 ? Primary : path.Contains("Shotgun_Secondary") || path == Shotgun ? Shotgun : null;
                if (target == null) continue; var next = AssetDatabase.LoadAssetAtPath<WeaponController>(target);
                if (!next) throw new Exception(target); if (next == old) continue;
                replacements[old] = next; replacements[old.gameObject] = next.gameObject; inventory.StartingWeapons[i] = next;
            }
            foreach (var component in inventory.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var data = new SerializedObject(component); var p = data.GetIterator(); bool changed = false;
                while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue && replacements.TryGetValue(p.objectReferenceValue, out var next)) { p.objectReferenceValue = next; changed = true; }
                if (changed) { data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(component); if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
            }
            EditorUtility.SetDirty(inventory); PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        static void Apply()
        {
            var text = new StringBuilder();
            foreach (string name in new[] { "SawBotLab", "CombatVarietyLab", "CombatTwoRoomsLab", "BunkerLab", "SawBotSustainLab", "SawBotWeaponDeathLab", "SawBotSawPolishLab" })
            {
                string path = "Assets/IronRequiem/Scenes/" + name + ".unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try { Connect(scene); EditorSceneManager.SaveScene(scene); text.AppendLine(name + ": " + string.Join(", ", All<PlayerWeaponsManager>(scene).Single().StartingWeapons.Select(AssetDatabase.GetAssetPath))); }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Report + "/apply.txt", text.ToString());
        }
    }
}
