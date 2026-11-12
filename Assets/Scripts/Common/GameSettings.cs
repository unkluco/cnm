using System;
using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>
    /// Cài đặt của người chơi, lưu trong PlayerPrefs và được áp dụng ngay khi game khởi động.
    /// Màn Cài đặt (LightSouls.UI) đổi giá trị; các hệ thống khác chỉ đọc.
    /// MusicVolume và SfxVolume hiện chỉ được lưu lại; hệ thống âm thanh sau này đọc chúng để chỉnh từng kênh.
    /// </summary>
    public static class GameSettings
    {
        public const int LowFrameRate = 30;
        public const int HighFrameRate = 60;

        private const string MasterVolumeKey = "settings.masterVolume";
        private const string MusicVolumeKey = "settings.musicVolume";
        private const string SfxVolumeKey = "settings.sfxVolume";
        private const string FrameRateKey = "settings.targetFrameRate";
        private const string VibrationKey = "settings.vibration";

        /// <summary>Âm lượng tổng, 0..1. Áp dụng qua AudioListener.volume.</summary>
        public static float MasterVolume { get; private set; } = 1f;

        /// <summary>Âm lượng nhạc nền, 0..1.</summary>
        public static float MusicVolume { get; private set; } = 1f;

        /// <summary>Âm lượng hiệu ứng, 0..1.</summary>
        public static float SfxVolume { get; private set; } = 1f;

        /// <summary>FPS mục tiêu: LowFrameRate hoặc HighFrameRate.</summary>
        public static int TargetFrameRate { get; private set; } = HighFrameRate;

        /// <summary>Hệ thống khác chỉ rung máy khi giá trị này là true.</summary>
        public static bool VibrationEnabled { get; private set; } = true;

        /// <summary>Phát mỗi khi có một cài đặt thay đổi.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            TargetFrameRate = NormalizeFrameRate(PlayerPrefs.GetInt(FrameRateKey, HighFrameRate));
            VibrationEnabled = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            Apply();
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
            Apply();
            Changed?.Invoke();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            Changed?.Invoke();
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            Changed?.Invoke();
        }

        public static void SetTargetFrameRate(int value)
        {
            TargetFrameRate = NormalizeFrameRate(value);
            PlayerPrefs.SetInt(FrameRateKey, TargetFrameRate);
            Apply();
            Changed?.Invoke();
        }

        public static void SetVibrationEnabled(bool value)
        {
            VibrationEnabled = value;
            PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
            Changed?.Invoke();
        }

        /// <summary>Ghi các cài đặt xuống bộ nhớ máy. Gọi khi đóng màn Cài đặt.</summary>
        public static void Save() => PlayerPrefs.Save();

        private static void Apply()
        {
            AudioListener.volume = MasterVolume;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }

        private static int NormalizeFrameRate(int value) => value <= LowFrameRate ? LowFrameRate : HighFrameRate;
    }
}
