using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Màn Thua trong Scene Game. GameBootstrap gọi ShowDefeat() khi người chơi chết.
    /// Màn Qua màn cũ vẫn giữ để thử UI; luồng vượt ngục hiện chuyển qua cổng bí ẩn.
    /// Thua: "Chơi lại" bắt đầu lượt mới ở sảnh (mất xu và đồ). Qua màn: "Về sảnh" giữ nguyên lượt chơi. Có thể thử từ menu chuột phải của component trong Inspector.
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
            defeatRetryButton.onClick.AddListener(SceneLoader.StartNewRun);
            // Qua màn: về sảnh, giữ xu và túi đồ để mua thêm rồi vào ngục tiếp.
            victoryRetryButton.onClick.AddListener(SceneLoader.LoadLobby);
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
