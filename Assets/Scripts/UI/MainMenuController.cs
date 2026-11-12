using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Menu chính trong Scene MainMenu: nút Chơi nạp Scene Game, nút Cài đặt mở màn Cài đặt, nút Thoát thoát game.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Tooltip("Để trống thì tự tìm SettingsScreen trong Scene.")]
        [SerializeField] private SettingsScreen settingsScreen;

        private void Awake()
        {
            if (settingsScreen == null)
            {
                settingsScreen = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            }

            playButton.onClick.AddListener(SceneLoader.LoadGame);
            quitButton.onClick.AddListener(SceneLoader.QuitGame);
            settingsButton.onClick.AddListener(OpenSettings);
            settingsButton.gameObject.SetActive(settingsScreen != null);
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuUtility.Select(playButton);
        }

        private void OpenSettings() => settingsScreen.Open(settingsButton);
    }
}
