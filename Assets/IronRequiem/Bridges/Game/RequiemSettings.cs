using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    public static class RequiemSettings
    {
        public const string VolumeKey = "IronRequiem.MasterVolume", SensitivityKey = "IronRequiem.LookSensitivity", FullscreenKey = "IronRequiem.Fullscreen";
        public const string SprintToggleGamepadKey = "IronRequiem.SprintToggleGamepad", SprintToggleKeyboardKey = "IronRequiem.SprintToggleKeyboard";
        // Sprint mode: gamepad toggles by default (holding L3 blocks the thumb), keyboard holds.
        public static bool SprintToggleGamepad => PlayerPrefs.GetInt(SprintToggleGamepadKey, 1) != 0;
        public static bool SprintToggleKeyboard => PlayerPrefs.GetInt(SprintToggleKeyboardKey, 0) != 0;
        public static void SetSprintToggle(bool gamepad, bool value)
        {
            PlayerPrefs.SetInt(gamepad ? SprintToggleGamepadKey : SprintToggleKeyboardKey, value ? 1 : 0); PlayerPrefs.Save();
        }
        public const string InvertYKey = "IronRequiem.InvertY";
        public static bool InvertY(bool fallback = false) => PlayerPrefs.GetInt(InvertYKey, fallback ? 1 : 0) != 0;
        public static void SetInvertY(bool value) { PlayerPrefs.SetInt(InvertYKey, value ? 1 : 0); PlayerPrefs.Save(); }
        // Chat 11 · passive enemy readability in the dark (EnemyDarkReadability). Intensity 0 = off, 1 = normal, up to 1.5.
        public const string VisionIntensityKey = "IronRequiem.VisionIntensity", VisionAlwaysKey = "IronRequiem.VisionOutlinesAlways";
        /// Default 0 (pase visual 12): with the rooms lit and the red eyes the body rim is off unless the player turns it on in the options.
        public static float VisionIntensity => Mathf.Clamp(PlayerPrefs.GetFloat(VisionIntensityKey, 0), 0, 1.5f);
        public static void SetVisionIntensity(float value) { PlayerPrefs.SetFloat(VisionIntensityKey, Mathf.Clamp(value, 0, 1.5f)); PlayerPrefs.Save(); VisionVersion++; }
        /// Bumped by every vision setter: readers re-read the PlayerPrefs only when it changes.
        public static int VisionVersion { get; private set; } = 1;
        /// Accessibility: outlines at full strength everywhere, not only in the dark.
        public static bool VisionOutlinesAlways => PlayerPrefs.GetInt(VisionAlwaysKey, 0) != 0;
        public static void SetVisionOutlinesAlways(bool value) { PlayerPrefs.SetInt(VisionAlwaysKey, value ? 1 : 0); PlayerPrefs.Save(); VisionVersion++; }
        public static float Volume => PlayerPrefs.GetFloat(VolumeKey, 1);
        public static float Sensitivity(float fallback = 1) => PlayerPrefs.GetFloat(SensitivityKey, fallback);
        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
        public static void ApplyVolume(float multiplier = 1)
        {
            if (Object.FindAnyObjectByType<AudioManager>()) AudioUtility.SetMasterVolume(Volume * multiplier);
        }
        public static void SetVolume(float value, float multiplier = 1)
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); ApplyVolume(multiplier);
        }
        public static void SetSensitivity(float value)
        {
            PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, .1f, 5)); PlayerPrefs.Save();
        }
        public static void SetFullscreen(bool value)
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyDisplayMode(value);
        }
        public static void Apply()
        {
            ApplyVolume();
            if (PlayerPrefs.HasKey(FullscreenKey)) ApplyDisplayMode(Fullscreen);
        }
        // Full screen renders at the monitor's native size (no 960x540 upscale blur); windowed falls back to the
        // largest 16:9 window that fits. The Editor Game view ignores SetResolution, so it keeps the plain toggle.
        public static void ApplyDisplayMode(bool fullscreen)
        {
            if (Application.isEditor) { Screen.fullScreen = fullscreen; return; }
            Vector2Int native = NativeResolution();
            if (fullscreen)
            {
                if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow && Screen.width == native.x && Screen.height == native.y) return;
                Screen.SetResolution(native.x, native.y, FullScreenMode.FullScreenWindow);
            }
            else
            {
                if (Screen.fullScreenMode == FullScreenMode.Windowed) return;
                Vector2Int window = WindowedResolution(native);
                Screen.SetResolution(window.x, window.y, FullScreenMode.Windowed);
            }
        }
        public static Vector2Int NativeResolution()
        {
            DisplayInfo info = Screen.mainWindowDisplayInfo;
            if (info.width > 0 && info.height > 0) return new Vector2Int(info.width, info.height);
            if (Display.main != null && Display.main.systemWidth > 0 && Display.main.systemHeight > 0) return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
            Resolution current = Screen.currentResolution;
            return new Vector2Int(Mathf.Max(current.width, 960), Mathf.Max(current.height, 540));
        }
        public static Vector2Int WindowedResolution(Vector2Int native)
        {
            // Leave room for the title bar and the taskbar so the window never spills off the monitor.
            Vector2Int[] sizes = { new Vector2Int(1600, 900), new Vector2Int(1280, 720) };
            foreach (Vector2Int size in sizes) if (size.x <= native.x - 64 && size.y <= native.y - 120) return size;
            return new Vector2Int(960, 540);
        }
    }
}
