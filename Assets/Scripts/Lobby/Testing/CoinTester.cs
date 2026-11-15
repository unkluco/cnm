using LightSouls.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LightSouls.Lobby.Testing
{
    /// <summary>
    /// Công cụ hỗ trợ cộng xu nhanh để kiểm thử việc mua đồ ở Sảnh khi chưa có nguồn rơi xu từ quái.
    /// Bấm phím C hoặc bấm nút trên màn hình để thêm +100 xu.
    /// </summary>
    public class CoinTester : MonoBehaviour
    {
        [Header("Coin Settings")]
        [SerializeField] private int coinsToAdd = 100;
        [SerializeField] private bool addCoinsOnStart = true;
        [SerializeField] private int initialCoins = 200;

        private void Start()
        {
            if (addCoinsOnStart && initialCoins > 0)
            {
                RunState.AddCoins(initialCoins);
            }
        }

        private void Update()
        {
            // Phím C để cộng xu nhanh
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                AddCoins();
            }
        }

        public void AddCoins()
        {
            RunState.AddCoins(coinsToAdd);
            Debug.Log($"[CoinTester] Đã cộng +{coinsToAdd} xu. Tổng hiện có: {RunState.Coins} xu.");
        }

        private void OnGUI()
        {
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft
            };

            GUI.Box(new Rect(20, 20, 220, 90), $"Xu hiện có: {RunState.Coins}\nNgục chọn: {(string.IsNullOrEmpty(RunState.SelectedDungeonId) ? "Mặc định" : RunState.SelectedDungeonId)}", boxStyle);

            if (GUI.Button(new Rect(20, 120, 160, 45), $"+{coinsToAdd} Xu [C]"))
            {
                AddCoins();
            }
        }
    }
}
