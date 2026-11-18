using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// <summary>Restores the existing animated entry and archives the obsolete campaign-only main screen.</summary>
    public static class CampaignMenuEntryRepair
    {
        const string Intro="Assets/FPS/Scenes/IntroMenu.unity";
        const string Campaign="Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string Report="Tools/IronRequiem/Reports/HandsHudCampaign";
        static void Guard()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving dirty scene.");
        }
        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Restore animated entry")]
        public static void Restore()
        {
            Guard();Directory.CreateDirectory(Report);
            var scene=SceneManager.GetSceneByPath(Campaign);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(Campaign,OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();var menu=roots.SelectMany(r=>r.GetComponentsInChildren<CombatLabMenu>(true)).Single();
                var legacy=new SerializedObject(menu).FindProperty("mainPanel").objectReferenceValue as GameObject;
                if(legacy)
                {
                    const string temporary="Assets/IronRequiem/Prefabs/LegacyCampaignMenu_Archive.prefab";
                    string workspace=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
                    string archive=Path.GetFullPath(Path.Combine(workspace,"Tools/IronRequiem/Quarantine/20261115-legacy-menu/IndustrialShell.prefab"));
                    string source=Path.GetFullPath(Path.Combine(workspace,temporary));
                    if(!source.StartsWith(workspace+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!archive.StartsWith(workspace+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Archive must remain within the project workspace.");
                    if(!File.Exists(archive))
                    {
                        PrefabUtility.SaveAsPrefabAsset(legacy,temporary);
                        Directory.CreateDirectory(Path.GetDirectoryName(archive));File.Move(source,archive);
                        if(File.Exists(source+".meta"))File.Move(source+".meta",archive+".meta");
                        AssetDatabase.Refresh();
                    }
                    foreach(var view in roots.SelectMany(r=>r.GetComponentsInChildren<RequiemMenuView>(true)))
                    {
                        if(view.transform.IsChildOf(legacy.transform))continue;
                        var data=new SerializedObject(view);var home=data.FindProperty("home").objectReferenceValue as GameObject;
                        if(!data.FindProperty("pauseOwner").objectReferenceValue&&data.FindProperty("labMenuSource").objectReferenceValue==menu&&home&&home.transform.IsChildOf(legacy.transform))
                            Undo.DestroyObjectImmediate(view);
                    }
                    Undo.RecordObject(menu,"Remove obsolete campaign main menu");menu.RemoveLegacyMainPanel();
                    Undo.DestroyObjectImmediate(legacy);
                }
                else menu.RemoveLegacyMainPanel();
                EditorUtility.SetDirty(menu);PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Intro);
            EditorSceneManager.OpenScene(Intro);
            File.WriteAllText(Report+"/animated-menu-entry.txt","Animated IntroMenu is the Editor Play entry. Obsolete campaign IndustrialShell archived outside Assets and removed from RequiemCampaign. Campaign direct entry starts gameplay; return always loads IntroMenu. Build Settings order unchanged. Existing IntroMenu scene/art/animation were not rebuilt.\n");
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Preview animated menu")]
        public static void Preview()
        {
            Guard();EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Intro);
            EditorSceneManager.OpenScene(Intro);EditorApplication.isPlaying=true;
        }
        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Inspect animated menu")]
        public static void Inspect()
        {
            if(!EditorApplication.isPlaying||SceneManager.GetActiveScene().name!="IntroMenu")throw new InvalidOperationException("Animated IntroMenu Play Mode required.");
            var sequence=UnityEngine.Object.FindObjectsByType<HangarLaunchSequence>(FindObjectsSortMode.None);
            var text=new StringBuilder($"time={Time.unscaledTime:F3}; scene={SceneManager.GetActiveScene().name}; sequences={sequence.Length}\n");
            foreach(var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.Contains("Walker")||t.name.Contains("Patrol")||t.name.EndsWith(":Hips")||t.GetComponent<Camera>()).Take(100))
                text.AppendLine($"{transform.name}: position={transform.position:F4}; rotation={transform.eulerAngles:F3}");
            Directory.CreateDirectory(Report);File.AppendAllText(Report+"/animated-menu-motion.txt",text+"\n");
        }
    }
}
