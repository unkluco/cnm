using LightSouls.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LightSouls.UI
{
    /// <summary>
    /// Chuyển Scene từ giao diện. Luôn trả Time.timeScale về 1 trước khi chuyển, vì menu tạm dừng
    /// và màn kết quả đặt timeScale = 0.
    /// </summary>
    public static class SceneLoader
    {
        public static void LoadMainMenu() => Load(SceneNames.MainMenu);

        public static void LoadGame() => Load(SceneNames.Game);

        /// <summary>Về sảnh, giữ nguyên lượt chơi (xu, túi đồ). Dùng sau khi qua màn.</summary>
        public static void LoadLobby() => Load(SceneNames.Lobby);

        /// <summary>
        /// Bắt đầu lượt chơi mới từ đầu: xóa xu, túi đồ và ngục đã chọn trong RunState rồi vào sảnh.
        /// Dùng cho nút Chơi ở menu chính và nút Chơi lại ở màn Thua.
        /// </summary>
        public static void StartNewRun()
        {
            RunState.Reset();
            LoadLobby();
        }

        public static void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }
}
