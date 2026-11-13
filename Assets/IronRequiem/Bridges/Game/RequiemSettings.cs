using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    public static class RequiemSettings
    {
        public const string VolumeKey = "IronRequiem.MasterVolume", SensitivityKey = "IronRequiem.LookSensitivity", FullscreenKey = "IronRequiem.Fullscreen";
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
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); Screen.fullScreen = value;
        }
        public static void Apply()
        {
            ApplyVolume();
            if (PlayerPrefs.HasKey(FullscreenKey)) Screen.fullScreen = Fullscreen;
        }
    }
}
