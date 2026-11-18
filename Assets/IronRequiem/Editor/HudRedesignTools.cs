using System;
using System.IO;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 16/11/2026 HUD redesign (two columns + avatar + radial fist meter): coordinator tools.
    /// - Creates the Resources link to the shared UI theme through the AssetDatabase (no YAML edits, no scenes saved).
    /// - Logs the runtime state of the new HUD pieces for the capture sequences driven by the local bridge.
    public static class HudRedesignTools
    {
        const string Menu = "Tools/Iron Requiem/HUD Redesign/";
        const string LinkPath = "Assets/IronRequiem/Resources/IronRequiem/HudThemeLink.asset";
        const string ThemePath = "Assets/IronRequiem/Settings/UI/IR_UiTheme.asset";
        const string Dir = "Tools/IronRequiem/Reports/HudRedesign";
        static void Log(string line) { Directory.CreateDirectory(Dir); File.AppendAllText(Dir + "/log.txt", DateTime.Now.ToString("HH:mm:ss.fff ") + line + "\n"); Debug.Log("[HudRedesign] " + line); }

        [MenuItem(Menu + "Create theme link")]
        static void CreateThemeLink()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Log("theme link: exit Play Mode first"); return; }
            var theme = AssetDatabase.LoadAssetAtPath<IronRequiemUiTheme>(ThemePath);
            if (!theme) { Log("theme link: theme not found at " + ThemePath); return; }
            var link = AssetDatabase.LoadAssetAtPath<IronRequiemUiThemeLink>(LinkPath);
            if (!link) { link = ScriptableObject.CreateInstance<IronRequiemUiThemeLink>(); AssetDatabase.CreateAsset(link, LinkPath); }
            var so = new SerializedObject(link); so.FindProperty("theme").objectReferenceValue = theme; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(link); AssetDatabase.SaveAssetIfDirty(link);
            Log($"theme link: {LinkPath} -> {AssetDatabase.GetAssetPath(link.Theme)} (guid {AssetDatabase.AssetPathToGUID(LinkPath)})");
        }

        [MenuItem(Menu + "Log HUD state (play)")]
        static void LogState()
        {
            if (!EditorApplication.isPlaying) { Log("state: Play Mode required"); return; }
            var radial = Object.FindAnyObjectByType<FistChargeRadialGraphic>();
            var melee = Object.FindAnyObjectByType<ProtagonistMelee>();
            var weapons = Object.FindAnyObjectByType<PlayerWeaponsManager>();
            var bar = Object.FindAnyObjectByType<RetroStatusBar>();
            var active = weapons ? weapons.GetActiveWeapon() : null;
            Log($"state: bar={(bool)bar} themeLink={(bool)IronRequiemUiThemeLink.Load()} melee charging={(melee && melee.IsCharging)} level={(melee ? melee.ChargeLevel : -1):0.00} full={(melee && melee.FullyCharged)} " +
                $"radial shown={(radial ? radial.ShownLevel : -1):0.00} split={(radial && radial.IsSplit)} burst={(radial ? radial.Burst01 : -1):0.00} visible={(radial ? radial.VisibleSegments : -1)} driven={(radial && radial.Driven)} " +
                $"weapon={(active ? active.name : "none")} health={(Object.FindAnyObjectByType<PlayerCharacterController>() is var p && p ? p.GetComponent<Unity.FPS.Game.Health>().CurrentHealth : -1):0}");
        }
    }
}
