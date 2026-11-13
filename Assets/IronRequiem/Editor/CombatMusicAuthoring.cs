using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    /// <summary>
    /// Adds a "Music" group under Master in MainAudioMixer, routes MenuMusic (IntroMenu) through it,
    /// and adds adaptive CombatMusic to MainScene.
    /// Runs from Tools > Iron Requiem > Audio, or from the request file below once compiled.
    /// Scenes open additively so the user's active scene stays loaded; refuses to run with dirty scenes.
    /// </summary>
    [InitializeOnLoad]
    public static class CombatMusicAuthoring
    {
        const string Queue = "Tools/IronRequiem/Requests/combat-music.txt";
        const string Reports = "Tools/IronRequiem/Reports/MenuMusic";
        const string Mixer = "Assets/FPS/Audio/MainAudioMixer.mixer";
        const string MenuScene = "Assets/FPS/Scenes/IntroMenu.unity";
        static readonly string[] GameScenes = { "Assets/FPS/Scenes/MainScene.unity", "Assets/IronRequiem/Scenes/FastCombatExecutionLab.unity" };
        const string ExploreClip = "Assets/IronRequiem/Audio/Music/IR_Combat01_Explore.wav";
        const string CombatClip = "Assets/IronRequiem/Audio/Music/IR_Combat01_Loop.wav";
        const string GroupName = "Music";
        static readonly DateTime LoadedAt = DateTime.UtcNow;

        static CombatMusicAuthoring() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue))
                return;
            if (File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/CombatMusicAuthoring.cs") > LoadedAt)
                return;
            File.Delete(Queue);
            Directory.CreateDirectory(Reports);
            try { File.WriteAllText(Reports + "/combat-music.txt", Connect()); }
            catch (Exception e) { File.WriteAllText(Reports + "/combat-music-error.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/Iron Requiem/Audio/Connect Combat Music")]
        static void ConnectFromMenu() { Debug.Log(Connect()); }

        static string Connect()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserving dirty scene: save or discard open scene changes first.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var explore = AssetDatabase.LoadAssetAtPath<AudioClip>(ExploreClip);
            var combat = AssetDatabase.LoadAssetAtPath<AudioClip>(CombatClip);
            if (explore == null || combat == null)
                throw new FileNotFoundException("Combat music clips not imported");
            ConfigureStreaming(ExploreClip);

            var (group, createdGroup) = EnsureMusicGroup();

            EditScene(MenuScene, scene =>
            {
                var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuMusic>(true)).FirstOrDefault();
                if (menu == null)
                    throw new InvalidOperationException("IntroMenu has no MenuMusic; run Connect Menu Music first.");
                SetFields(menu, ("m_OutputGroup", group));
            });

            var sceneLog = "";
            foreach (var path in GameScenes)
                EditScene(path, scene =>
                {
                    var music = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatMusic>(true)).FirstOrDefault();
                    bool created = music == null;
                    if (created)
                    {
                        var go = new GameObject("CombatMusic", typeof(CombatMusic));
                        SceneManager.MoveGameObjectToScene(go, scene);
                        music = go.GetComponent<CombatMusic>();
                    }
                    SetFields(music, ("m_ExploreClip", explore), ("m_CombatClip", combat), ("m_OutputGroup", group));
                    sceneLog += $"{(created ? "Created" : "Updated")} CombatMusic in {path}\n";
                });

            return $"Mixer group '{GroupName}' under Master: {(createdGroup ? "created" : "already present")} in {Mixer}; runtime mixer rebuilt\n" +
                   $"IntroMenu MenuMusic routed to {GroupName}\n" +
                   sceneLog +
                   $"Explore: {ExploreClip} ({explore.length:0.000} s, {explore.loadType})\n" +
                   $"Combat: {CombatClip} ({combat.length:0.000} s, {combat.loadType})\n" +
                   $"Layers aligned: {explore.samples == combat.samples} ({explore.samples} / {combat.samples} samples)";
        }

        static void EditScene(string path, Action<Scene> edit)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                edit(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException(path + " could not be saved");
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void SetFields(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach (var (name, value) in fields)
                serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureStreaming(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming && settings.compressionFormat == AudioCompressionFormat.Vorbis)
                return;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Unity exposes no public API for adding mixer groups; AudioMixerController is internal to UnityEditor.
        static (AudioMixerGroup group, bool created) EnsureMusicGroup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Mixer);
            var existing = mixer.FindMatchingGroups("Master/" + GroupName).FirstOrDefault(g => g.name == GroupName);
            if (existing != null)
            {
                RebuildRuntimeMixer(mixer);
                return (existing, false);
            }

            const BindingFlags flags = Flags;
            var controllerType = mixer.GetType();
            var master = controllerType.GetProperty("masterGroup", flags)?.GetValue(mixer);
            var create = controllerType.GetMethod("CreateNewGroup", flags, null, new[] { typeof(string), typeof(bool) }, null);
            var addChild = controllerType.GetMethods(flags).FirstOrDefault(m => m.Name == "AddChildToParent" && m.GetParameters().Length == 2);
            if (master == null || create == null || addChild == null)
                throw new MissingMethodException($"{controllerType.FullName} no longer exposes masterGroup/CreateNewGroup/AddChildToParent; add the '{GroupName}' group by hand in the Audio Mixer window.");

            var group = create.Invoke(mixer, new object[] { GroupName, false });
            addChild.Invoke(mixer, new[] { group, master });
            controllerType.GetMethod("AddGroupToCurrentView", flags)?.Invoke(mixer, new[] { group });
            RebuildRuntimeMixer(mixer);
            return ((AudioMixerGroup)group, true);
        }

        // Without this the runtime mixer keeps its old group graph and anything routed to a new group is silent
        // (the Audio Mixer window does the same after adding a group).
        static void RebuildRuntimeMixer(AudioMixer mixer)
        {
            var rebuild = mixer.GetType().GetMethod("OnSubAssetChanged", Flags, null, Type.EmptyTypes, null);
            if (rebuild == null)
                throw new MissingMethodException($"{mixer.GetType().FullName}.OnSubAssetChanged not found; open MainAudioMixer in the Audio Mixer window and adjust the Music fader once to rebuild it.");
            rebuild.Invoke(mixer, null);
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Mixer, ImportAssetOptions.ForceUpdate);
        }
    }
}
