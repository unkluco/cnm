using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Màn Thua và màn Qua màn trong Scene Game. Không tự bật: GameBootstrap gọi ShowDefeat()
    /// khi người chơi chết và ShowVictory() khi tới đích. Có thể thử từ menu chuột phải của component trong Inspector.
    /// </summary>
    public class GameResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Button defeatRetryButton;
        [SerializeField] private Button defeatMainMenuButton;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Button victoryRetryButton;
        [SerializeField] private Button victoryMainMenuButton;

        private float timeScaleBeforeShow = 1f;
        private CursorState cursorBeforeShow;

        /// <summary>True khi một màn kết quả đang hiện. Menu tạm dừng không mở được lúc này.</summary>
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            defeatPanel.SetActive(false);
            victoryPanel.SetActive(false);
            defeatRetryButton.onClick.AddListener(SceneLoader.LoadGame);
            victoryRetryButton.onClick.AddListener(SceneLoader.LoadGame);
            defeatMainMenuButton.onClick.AddListener(SceneLoader.LoadMainMenu);
            victoryMainMenuButton.onClick.AddListener(SceneLoader.LoadMainMenu);
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                IsOpen = false;
                Time.timeScale = 1f;
            }
        }

        [ContextMenu("Show Defeat")]
        public void ShowDefeat() => Show(defeatPanel, defeatRetryButton);

        [ContextMenu("Show Victory")]
        public void ShowVictory() => Show(victoryPanel, victoryRetryButton);

        [ContextMenu("Hide")]
        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            defeatPanel.SetActive(false);
            victoryPanel.SetActive(false);
            Time.timeScale = timeScaleBeforeShow;
            cursorBeforeShow.Restore();
        }

        private void Show(GameObject panel, Button firstSelected)
        {
            Hide();
            IsOpen = true;
            timeScaleBeforeShow = Time.timeScale;
            Time.timeScale = 0f;
            cursorBeforeShow = CursorState.CaptureAndRelease();
            panel.SetActive(true);
            MenuUtility.Select(firstSelected);
        }
    }
}
