using UnityEngine;

namespace Project.Core
{
    /// <summary>
    /// PlayerPrefs-backed audio and display settings for the menu shell.
    /// </summary>
    public static class GameSettings
    {
        public const string MasterVolumeKey = "project.settings.master_volume";
        public const string BgmVolumeKey = "project.settings.bgm_volume";
        public const string SfxVolumeKey = "project.settings.sfx_volume";
        public const string FullscreenKey = "project.settings.fullscreen";

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            set
            {
                PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
                ApplyAudio();
            }
        }

        public static float BgmVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 0.7f));
            set => PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                Screen.fullScreen = value;
            }
        }

        public static void Apply()
        {
            ApplyAudio();
            Screen.fullScreen = Fullscreen;
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        private static void ApplyAudio()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
