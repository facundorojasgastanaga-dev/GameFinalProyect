using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Adds OptionsSwitchVisual to the fullscreen Toggle of every options console (IntroMenu, pause, labs) when a scene
    /// loads. RequiemMenuView lives in the fps.UI assembly (asmref) and cannot see this assembly, so the hook is here.
    /// The toggle is recognised by its "FullscreenLabel" child; scenes and prefabs are not edited.
    static class OptionsSwitchInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded;
            Install();
        }
        static void OnLoaded(Scene scene, LoadSceneMode mode) => Install();
        static void Install()
        {
            // menu frame (IndustrialShell/ConsoleFrame): steel sheet with rivets and hazard tape behind the tape-label buttons
            foreach (var panel in Object.FindObjectsByType<IndustrialPanelGraphic>(FindObjectsInactive.Include))
                if (panel.name == "ConsoleFrame" && panel.Style != IndustrialPanelGraphic.PanelStyle.Sheet)
                {
                    panel.SetStyle(IndustrialPanelGraphic.PanelStyle.Sheet);
                    panel.Configure(new Color(.125f, .14f, .155f, .97f), new Color(.035f, .04f, .045f, 1), 14);
                }
            foreach (var toggle in Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include))
                if (toggle.transform.Find("FullscreenLabel") && !toggle.GetComponent<OptionsSwitchVisual>()) toggle.gameObject.AddComponent<OptionsSwitchVisual>();
        }
    }
}
