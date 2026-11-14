using System;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Menu tạm dừng trong Scene Game. Mở bằng nút tạm dừng trên màn hình hoặc action được gán
    /// (mặc định UI/Cancel: phím Esc, nút Back của Android); action đó cũng đóng menu.
    /// Khi tạm dừng: Time.timeScale = 0 và con trỏ chuột được mở khóa.
    /// Hệ thống khác muốn chặn thao tác của người chơi khi tạm dừng thì nghe PausedChanged.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private InputActionReference toggleAction;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;

        [Tooltip("Để trống thì tự tìm SettingsScreen trong Scene.")]
        [SerializeField] private SettingsScreen settingsScreen;

        private float timeScaleBeforePause = 1f;
        private CursorState cursorBeforePause;

        public bool IsPaused { get; private set; }

        /// <summary>Phát true khi vừa tạm dừng, false khi vừa tiếp tục.</summary>
        public event Action<bool> PausedChanged;

        private void Awake()
        {
            if (settingsScreen == null)
            {
                settingsScreen = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            }

            panel.SetActive(false);
            pauseButton.onClick.AddListener(Pause);
            resumeButton.onClick.AddListener(Resume);
            mainMenuButton.onClick.AddListener(SceneLoader.LoadMainMenu);
            settingsButton.onClick.AddListener(OpenSettings);
            settingsButton.gameObject.SetActive(settingsScreen != null);
        }

        private void OnEnable()
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                toggleAction.action.performed += OnTogglePerformed;
                toggleAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                toggleAction.action.performed -= OnTogglePerformed;
            }
        }

        private void Update()
        {
            // Nút tạm dừng trên màn hình chỉ hiện khi đang chơi.
            var showPauseButton = !IsPaused && !GameResultScreen.IsOpen;
            if (pauseButton.gameObject.activeSelf != showPauseButton)
            {
                pauseButton.gameObject.SetActive(showPauseButton);
            }
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            // Lần nhấn này đã dùng để đóng màn Cài đặt.
            if (SettingsScreen.HandlesCancelThisFrame)
            {
                return;
            }

            // Đang mở balo, cửa hàng…: nút Back đóng bảng đó trước.
            if (!IsPaused && BackButtonStack.TryCloseTop())
            {
                return;
            }

            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void Pause()
        {
            if (IsPaused || GameResultScreen.IsOpen)
            {
                return;
            }

            IsPaused = true;
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            cursorBeforePause = CursorState.CaptureAndRelease();
            pauseButton.gameObject.SetActive(false);
            panel.SetActive(true);
            MenuUtility.Select(resumeButton);
            PausedChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            if (settingsScreen != null)
            {
                settingsScreen.Close();
            }

            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;
            cursorBeforePause.Restore();
            panel.SetActive(false);
            PausedChanged?.Invoke(false);
        }

        private void OpenSettings() => settingsScreen.Open(settingsButton);
    }
}
