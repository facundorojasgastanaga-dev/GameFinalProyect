using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    /// IR-03: the standalone player started at 960x540 windowed and stretched, which read as blur. Apply() makes the
    /// executable start full screen at the monitor's native resolution. Runs from the VisualOverhaulDriver queue as
    /// "IronCampaignMapFxDisplay.Apply" / "IronCampaignMapFxDisplay.Report".
    public static class IronCampaignMapFxDisplay
    {
        // B1 (approved): native resolution only. The launch mode stays as it is unless launchFullScreen is passed
        // ("IronCampaignMapFxDisplay.Apply fullscreen"), which needs the user's separate OK.
        public static string Apply(string args)
        {
            if (EditorApplication.isPlaying) return "refused: leave Play Mode before changing Player Settings.";
            var report = new StringBuilder("IronCampaignMapFxDisplay.Apply\nbefore:\n").Append(Describe());
            bool oldNative = PlayerSettings.defaultIsNativeResolution;
            FullScreenMode oldMode = PlayerSettings.fullScreenMode;
            PlayerSettings.defaultIsNativeResolution = true;
            if (args != null && args.Trim() == "fullscreen") PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            AssetDatabase.SaveAssets();
            report.Append("after:\n").Append(Describe());
            report.Append("changed: defaultIsNativeResolution ").Append(oldNative).Append(" -> ").Append(PlayerSettings.defaultIsNativeResolution).Append('\n');
            report.Append("changed: fullScreenMode ").Append(oldMode).Append(" -> ").Append(PlayerSettings.fullScreenMode).Append('\n');
            if (oldMode != PlayerSettings.fullScreenMode)
                report.Append("BEHAVIOUR CHANGE (confirm): the executable now launches full screen (borderless FullScreenWindow) instead of ")
                      .Append(oldMode).Append(". defaultScreenWidth/Height only apply to windowed launches.\n");
            report.Append("note: Windows players reuse the last resolution stored under HKCU\\Software\\<company>\\<product> (Screenmanager *) until those keys are cleared.");
            return report.ToString();
        }

        public static string Report() => "IronCampaignMapFxDisplay.Report\n" + Describe();

        static string Describe()
        {
            return "  defaultScreenWidth=" + PlayerSettings.defaultScreenWidth + " defaultScreenHeight=" + PlayerSettings.defaultScreenHeight
                + "\n  defaultIsNativeResolution=" + PlayerSettings.defaultIsNativeResolution
                + "\n  fullScreenMode=" + PlayerSettings.fullScreenMode
                + "\n  resizableWindow=" + PlayerSettings.resizableWindow
                + "\n  company=" + PlayerSettings.companyName + " product=" + PlayerSettings.productName + "\n";
        }
    }
}
