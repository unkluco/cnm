using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LightSouls.Combat.Testing
{
    /// <summary>
    /// Script kiểm tra nạp lại Scene để xác minh túi đồ giữ nguyên nhờ RunState.
    /// Bấm phím R hoặc bấm nút Reload trên giao diện để nạp lại Scene hiện tại.
    /// </summary>
    public class SceneReloadTester : MonoBehaviour
    {
        [Header("UI Tùy chọn")]
        [SerializeField] private Button reloadButton;

        private void Awake()
        {
            if (reloadButton != null)
            {
                reloadButton.onClick.AddListener(ReloadCurrentScene);
            }
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                ReloadCurrentScene();
            }
        }

        public void ReloadCurrentScene()
        {
            Debug.Log("[SceneReloadTester] Nạp lại Scene để kiểm tra lưu trữ RunState...");
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex >= 0 ? activeScene.buildIndex : 0);
        }

        private void OnGUI()
        {
            if (reloadButton == null)
            {
                // Hiển thị nút test nhỏ ở góc trên phải nếu không có UI
                if (GUI.Button(new Rect(Screen.width - 160, 20, 140, 40), "Reload Scene (R)"))
                {
                    ReloadCurrentScene();
                }
            }
        }
    }
}
