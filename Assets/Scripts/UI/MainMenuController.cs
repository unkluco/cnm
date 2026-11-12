using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>Menu chính trong Scene MainMenu: nút Chơi nạp Scene Game, nút Thoát thoát game.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            playButton.onClick.AddListener(SceneLoader.LoadGame);
            quitButton.onClick.AddListener(SceneLoader.QuitGame);
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuUtility.Select(playButton);
        }
    }
}
