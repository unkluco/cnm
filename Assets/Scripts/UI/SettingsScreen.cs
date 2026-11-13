using System;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Màn Cài đặt, mở từ menu chính và menu tạm dừng. Đọc và ghi giá trị qua LightSouls.Common.GameSettings.
    /// Đóng bằng nút Xong hoặc action đóng (mặc định UI/Cancel: phím Esc, nút Back của Android).
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        [SerializeField] private InputActionReference closeAction;
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle highFrameRateToggle;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private Button doneButton;

        private static int closedOnFrame = -1;
        private Selectable selectOnClose;

        /// <summary>True khi màn Cài đặt đang mở.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// True khi đang mở hoặc vừa đóng trong frame này. Menu khác dùng giá trị này để bỏ qua
        /// cùng lần nhấn Esc/Back đã dùng để đóng màn Cài đặt.
        /// </summary>
        public static bool HandlesCancelThisFrame => IsOpen || closedOnFrame == Time.frameCount;

        /// <summary>Phát khi màn Cài đặt vừa đóng.</summary>
        public event Action Closed;

        private void Awake()
        {
            panel.SetActive(false);
            masterVolumeSlider.onValueChanged.AddListener(GameSettings.SetMasterVolume);
            musicVolumeSlider.onValueChanged.AddListener(GameSettings.SetMusicVolume);
            sfxVolumeSlider.onValueChanged.AddListener(GameSettings.SetSfxVolume);
            highFrameRateToggle.onValueChanged.AddListener(OnHighFrameRateChanged);
            vibrationToggle.onValueChanged.AddListener(GameSettings.SetVibrationEnabled);
            doneButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (closeAction != null && closeAction.action != null)
            {
                closeAction.action.performed += OnClosePerformed;
                closeAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (closeAction != null && closeAction.action != null)
            {
                closeAction.action.performed -= OnClosePerformed;
            }
        }

        private void OnDestroy()
        {
            if (panel.activeSelf)
            {
                IsOpen = false;
            }
        }

        /// <summary>Mở màn Cài đặt. Khi đóng, chọn lại selectOnClose (thường là nút đã mở màn này).</summary>
        public void Open(Selectable selectOnClose = null)
        {
            if (IsOpen)
            {
                return;
            }

            this.selectOnClose = selectOnClose;
            masterVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            musicVolumeSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxVolumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            highFrameRateToggle.SetIsOnWithoutNotify(GameSettings.TargetFrameRate >= GameSettings.HighFrameRate);
            vibrationToggle.SetIsOnWithoutNotify(GameSettings.VibrationEnabled);

            IsOpen = true;
            panel.SetActive(true);
            MenuUtility.Select(masterVolumeSlider);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            closedOnFrame = Time.frameCount;
            GameSettings.Save();
            panel.SetActive(false);
            MenuUtility.Select(selectOnClose);
            Closed?.Invoke();
        }

        private void OnClosePerformed(InputAction.CallbackContext context) => Close();

        private static void OnHighFrameRateChanged(bool isOn) =>
            GameSettings.SetTargetFrameRate(isOn ? GameSettings.HighFrameRate : GameSettings.LowFrameRate);
    }
}
