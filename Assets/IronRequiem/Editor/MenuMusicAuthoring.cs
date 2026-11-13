using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    /// <summary>
    /// Sets music import settings and adds a MenuMusic object to IntroMenu.
    /// Runs from Tools > Iron Requiem > Audio, or from the request file below once compiled.
    /// Opens IntroMenu additively so the user's active scene stays loaded, and refuses to run with dirty scenes.
    /// </summary>
    [InitializeOnLoad]
    public static class MenuMusicAuthoring
    {
        const string Queue = "Tools/IronRequiem/Requests/menu-music.txt";
        const string Reports = "Tools/IronRequiem/Reports/MenuMusic";
        const string MusicFolder = "Assets/IronRequiem/Audio/Music";
        const string MenuScene = "Assets/FPS/Scenes/IntroMenu.unity";
        const string MenuClip = MusicFolder + "/IR_Menu01_Loop.wav";
        static readonly DateTime LoadedAt = DateTime.UtcNow;

        static MenuMusicAuthoring() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue))
                return;
            if (File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/MenuMusicAuthoring.cs") > LoadedAt)
                return;
            File.Delete(Queue);
            Directory.CreateDirectory(Reports);
            try { File.WriteAllText(Reports + "/menu-music.txt", Connect()); }
            catch (Exception e) { File.WriteAllText(Reports + "/menu-music-error.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/Iron Requiem/Audio/Connect Menu Music")]
        static void ConnectFromMenu() { Debug.Log(Connect()); }

        static string Connect()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserving dirty scene: save or discard open scene changes first.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureMusicImport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuClip);
            if (clip == null)
                throw new FileNotFoundException("Menu clip not imported", MenuClip);

            var scene = SceneManager.GetSceneByPath(MenuScene);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
            try
            {
                var music = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuMusic>(true)).FirstOrDefault();
                bool created = music == null;
                if (created)
                {
                    var go = new GameObject("MenuMusic", typeof(AudioSource), typeof(MenuMusic));
                    SceneManager.MoveGameObjectToScene(go, scene);
                    music = go.GetComponent<MenuMusic>();
                }

                var serialized = new SerializedObject(music);
                serialized.FindProperty("m_Clip").objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("IntroMenu could not be saved");

                return $"{(created ? "Created" : "Updated")} MenuMusic in {MenuScene}\n" +
                       $"Clip: {MenuClip} ({clip.length:0.0} s, {clip.loadType}, loop, 2D, fade-in)\n" +
                       "Music import: Streaming + Vorbis q0.7 for every clip in " + MusicFolder + "\n" +
                       "No mixer group assigned (MainAudioMixer has no Music group); set Output Group on the component if one is added.";
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ConfigureMusicImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var settings = importer.defaultSampleSettings;
                if (settings.loadType == AudioClipLoadType.Streaming && settings.compressionFormat == AudioCompressionFormat.Vorbis)
                    continue;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
        }
    }
}
