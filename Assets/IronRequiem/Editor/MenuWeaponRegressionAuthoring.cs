using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class MenuWeaponRegressionAuthoring
    {
        public const string Report = "Tools/IronRequiem/Reports/MenuWeaponRegression";
        const string Queue = "Tools/IronRequiem/Requests/menu-weapon-regression.txt";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static MenuWeaponRegressionAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue) || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/MenuWeaponRegressionAuthoring.cs") > Loaded) return;
            var op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { if (op == "inspect") Inspect(); else if(op=="apply") Apply(); else throw new Exception(op); }
            catch (Exception exception) { File.WriteAllText(Report + "/error.txt", exception.ToString()); Debug.LogException(exception); }
        }
        static void Apply()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new Exception("Preserving dirty user scene");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IronRequiem/Audio/Music/IR_Menu01_Loop.wav");
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/FPS/Audio/MainAudioMixer.mixer"); var group = mixer.FindMatchingGroups("Music").FirstOrDefault(g => g.name == "Music");
            if(!clip||!group)throw new Exception("Existing menu clip or Music mixer group missing");
            foreach (string name in new[] {"SawBotLab","SawBotSustainLab","SawBotWeaponDeathLab","SawBotSawPolishLab","CombatTwoRoomsLab","BunkerLab","CombatVarietyLab"})
            {
                string path="Assets/IronRequiem/Scenes/"+name+".unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var roots=scene.GetRootGameObjects();var lab=roots.SelectMany(r=>r.GetComponentsInChildren<CombatLabMenu>(true)).Single();
                    var music=roots.SelectMany(r=>r.GetComponentsInChildren<MenuMusic>(true)).FirstOrDefault();
                    if(!music){var go=new GameObject("LabMenuMusic");SceneManager.MoveGameObjectToScene(go,scene);music=go.AddComponent<MenuMusic>();}
                    music.ConfigureLab(lab);var settings=new SerializedObject(music);settings.FindProperty("m_Clip").objectReferenceValue=clip;settings.FindProperty("m_OutputGroup").objectReferenceValue=group;settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(music);
                    foreach(var combat in roots.SelectMany(r=>r.GetComponentsInChildren<CombatMusic>(true))){combat.ConfigureLab(lab);EditorUtility.SetDirty(combat);if(PrefabUtility.IsPartOfPrefabInstance(combat))PrefabUtility.RecordPrefabInstancePropertyModifications(combat);}
                    foreach(var pause in roots.SelectMany(r=>r.GetComponentsInChildren<InGameMenuManager>(true)).Where(p=>p.UseIndustrialMenu))
                    {
                        var canvas=pause.MenuRoot.GetComponent<Canvas>();var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                        var data=new SerializedObject(scaler);data.FindProperty("m_UiScaleMode").enumValueIndex=(int)UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;data.FindProperty("m_ReferenceResolution").vector2Value=new Vector2(1920,1080);data.FindProperty("m_MatchWidthOrHeight").floatValue=.5f;data.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(scaler);if(PrefabUtility.IsPartOfPrefabInstance(scaler))PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            // Runtime restart/return uses SceneManager.LoadScene. Editor-only unlisted labs cannot reload.
            var buildScenes=EditorBuildSettings.scenes.ToList();foreach(string name in new[]{"SawBotSawPolishLab","CombatVarietyLab"}){string path="Assets/IronRequiem/Scenes/"+name+".unity";if(!buildScenes.Any(s=>s.path==path))buildScenes.Add(new EditorBuildSettingsScene(path,true));}EditorBuildSettings.scenes=buildScenes.ToArray();
            AssetDatabase.SaveAssets();File.WriteAllText(Report+"/apply.txt","Existing menu theme connected to Music mixer in7labs; smooth transition to level music. Pause CanvasScaler reference1920x1080 saved through SerializedObject/prefab overrides. Register only two previously unlisted test labs so runtime reload works. MainScene and audio assets unchanged.");IronRequiemSetupTools.WriteAudit();
        }
        static void Inspect()
        {
            var text = new StringBuilder(); var scene = SceneManager.GetActiveScene(); text.AppendLine(scene.path);
            foreach (var music in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuMusic>(true))) text.AppendLine("MenuMusic " + music.name + " clip=" + music.Clip);
            foreach (var pause in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InGameMenuManager>(true)))
            {
                text.AppendLine("Pause " + pause.name + " industrial=" + pause.UseIndustrialMenu);
                for (var t = pause.MenuRoot.transform; t; t = t.parent)
                {
                    var rect = t as RectTransform; var canvas = t.GetComponent<Canvas>();
                    text.AppendLine(" " + t.name + " rect=" + (rect ? rect.rect.ToString() : "none") + " scale=" + t.localScale + " anchors=" + (rect ? rect.anchorMin + "/" + rect.anchorMax : "none") + " canvas=" + (canvas ? canvas.renderMode + " root=" + canvas.isRootCanvas : "none") + " mask=" + t.GetComponent<UnityEngine.UI.RectMask2D>());
                }
            }
            foreach (string path in new[] { "Assets/IronRequiem/Prefabs/SecondaryShotgun/BigBot_Burst_v10_NavThrottle.prefab", "Assets/IronRequiem/Prefabs/Enemies/BigBot_Burst_v10.prefab" })
            {
                var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(path); var source = enemy.GetComponent<EnemyExecutionSource>();
                if (!source || !source.ModulePrefab) { text.AppendLine(path + " has no execution module (legacy enemy variant)"); continue; }
                text.AppendLine(path + " rootScale=" + enemy.transform.localScale + " module=" + AssetDatabase.GetAssetPath(source.ModulePrefab));
                var module = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source.ModulePrefab));
                try
                {
                    text.AppendLine(" world root=" + module.transform.localScale + " components=" + string.Join(",", module.GetComponents<Component>().Select(c => c.GetType().Name)));
                    foreach (var lod in module.GetComponentsInChildren<LODGroup>(true)) text.AppendLine(" LOD " + lod.name + " enabled=" + lod.enabled + " reference=" + lod.localReferencePoint + " size=" + lod.size + " lossyscale=" + lod.transform.lossyScale + " levels=" + string.Join(",", lod.GetLODs().Select(l => l.screenRelativeTransitionHeight)));
                    foreach (var renderer in module.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer))) text.AppendLine(" " + renderer.name + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled + " forceOff=" + renderer.forceRenderingOff + " layer=" + renderer.gameObject.layer + " bounds=" + renderer.bounds + " shadow=" + renderer.shadowCastingMode);
                }
                finally { PrefabUtility.UnloadPrefabContents(module); }
            }
            File.WriteAllText(Report + "/inspection.txt", text.ToString());
        }
    }
}
