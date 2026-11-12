using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Menu tạm dừng trong Scene Game. Bật/tắt bằng action được gán (mặc định UI/Cancel, phím Esc).
    /// Khi tạm dừng: Time.timeScale = 0 và con trỏ chuột được mở khóa.
    /// Hệ thống khác muốn chặn thao tác của người chơi khi tạm dừng thì nghe PausedChanged.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private InputActionReference toggleAction;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;

        private float timeScaleBeforePause = 1f;
        private CursorState cursorBeforePause;

        public bool IsPaused { get; private set; }

        /// <summary>Phát true khi vừa tạm dừng, false khi vừa tiếp tục.</summary>
        public event Action<bool> PausedChanged;

        private void Awake()
        {
            panel.SetActive(false);
            resumeButton.onClick.AddListener(Resume);
            mainMenuButton.onClick.AddListener(SceneLoader.LoadMainMenu);
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

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
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

            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;
            cursorBeforePause.Restore();
            panel.SetActive(false);
            PausedChanged?.Invoke(false);
        }
    }
}
