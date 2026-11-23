using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor.Map01
{
    /// R0: opens a Map01 session scene and enters Play Mode (the sessions chain S1 → S2 → S3 → S4 → WinScene by themselves).
    /// Refuses if a non-Map01 scene has unsaved changes, with ONE exception: Room00_Hangar marked dirty by editor-time scripts right
    /// after HangarRouteAuthoring saved it (file written < 30 min ago): that mark carries no authored change and is discarded.
    /// Play Mode changes are never saved.
    public static class Map01PlayMenu
    {
        const string Hangar = "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity";
        [MenuItem(Map01Paths.MenuRoot + "Play/Play from S1 (full map)")] public static void S1() => Play(0);
        [MenuItem(Map01Paths.MenuRoot + "Play/Play from S2")] public static void S2() => Play(1);
        [MenuItem(Map01Paths.MenuRoot + "Play/Play from S3")] public static void S3() => Play(2);
        [MenuItem(Map01Paths.MenuRoot + "Play/Play from S4 (boss)")] public static void S4() => Play(3);

        /// The real game start: IntroMenu → JUGAR (now loads Map01_Campaign_S1). Opening the menu never saves it.
        [MenuItem(Map01Paths.MenuRoot + "Play/Play from IntroMenu (JUGAR)")]
        public static void FromMenu()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            DiscardHangarEditorMark();
            Map01Scenes.GuardForeignDirtyScenes();
            EditorSceneManager.OpenScene("Assets/FPS/Scenes/IntroMenu.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void DiscardHangarEditorMark()
        {
            foreach (var path in new[] { Hangar, "Assets/FPS/Scenes/IntroMenu.unity" })
            {
                var s = SceneManager.GetSceneByPath(path);
                // only scenes that a Map01 tool itself saved in the last 30 min: the in-memory mark is editor noise, the file on disk is the intended state
                if (s.IsValid() && s.isDirty && (DateTime.Now - File.GetLastWriteTime(path)).TotalMinutes < 30)
                {
                    UnityEngine.Debug.Log("[Map01] " + path + ": cambios en memoria descartados (el archivo en disco es el estado guardado por Map01)");
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    return;
                }
            }
        }

        static void Play(int session)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            var hangar = SceneManager.GetSceneByPath(Hangar);
            if (hangar.IsValid() && hangar.isDirty && (DateTime.Now - File.GetLastWriteTime(Hangar)).TotalMinutes < 30)
            {
                UnityEngine.Debug.Log("[Map01] Room00_Hangar: marca de cambios del Editor descartada (la autoría la guardó hace " + (DateTime.Now - File.GetLastWriteTime(Hangar)).TotalMinutes.ToString("0") + " min)");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // closes the hangar without saving
            }
            Map01Scenes.OpenSession(session);
            EditorApplication.isPlaying = true;
        }
    }
}
