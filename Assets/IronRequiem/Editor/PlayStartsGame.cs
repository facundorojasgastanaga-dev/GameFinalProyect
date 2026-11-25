using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// 24/11: the Editor Play button starts the game from the animated IntroMenu, whatever scene is open.
    /// Applied once per Editor session (tools that clear the start scene for their own runs restore it themselves).
    /// Toggle: Tools > Iron Requiem > Play inicia el juego.
    [InitializeOnLoad]
    public static class PlayStartsGame
    {
        const string Intro = "Assets/FPS/Scenes/IntroMenu.unity";
        const string MenuPath = "Tools/Iron Requiem/Play inicia el juego";
        const string SessionKey = "IR.PlayStartsGame.Applied";
        static string PrefKey => "IR.PlayStartsGame." + Application.dataPath.GetHashCode();

        static bool Enabled { get => EditorPrefs.GetBool(PrefKey, true); set => EditorPrefs.SetBool(PrefKey, value); }

        static PlayStartsGame()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Intro);
            if (scene && EditorSceneManager.playModeStartScene != scene) EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            if (Enabled) Apply();
            else if (EditorSceneManager.playModeStartScene && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == Intro) EditorSceneManager.playModeStartScene = null;
        }
        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }
    }
}
