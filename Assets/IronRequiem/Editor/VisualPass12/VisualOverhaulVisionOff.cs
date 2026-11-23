using UnityEditor;

namespace IronRequiem.Editor
{
    /// Pase visual 12 · turns the enemy body rim off (or back on) in this machine's saved options.
    public static class VisualOverhaulVisionOff
    {
        [MenuItem("Tools/Iron Requiem/Visual Pass 12/Enemy rim/Off")] public static void MenuOff() => Apply();
        [MenuItem("Tools/Iron Requiem/Visual Pass 12/Enemy rim/On")] public static void MenuOn() => Restore();
        public static string Apply() { RequiemSettings.SetVisionIntensity(0); return "rim off: " + RequiemSettings.VisionIntensity; }
        public static string Restore() { RequiemSettings.SetVisionIntensity(1); return "rim on: " + RequiemSettings.VisionIntensity; }
    }
}
