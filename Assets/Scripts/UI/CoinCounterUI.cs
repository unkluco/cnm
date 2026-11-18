using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>
    /// Ô hiện số xu của lượt chơi (RunState.Coins), tự cập nhật khi xu thay đổi.
    /// Dùng ở cả sảnh và Scene Game.
    /// </summary>
    public class CoinCounterUI : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private string format = "Xu: {0}";

        private void OnEnable()
        {
            RunState.CoinsChanged += HandleCoinsChanged;
            HandleCoinsChanged(RunState.Coins);
        }

        private void OnDisable()
        {
            RunState.CoinsChanged -= HandleCoinsChanged;
        }

        private void HandleCoinsChanged(int coins)
        {
            if (label != null)
            {
                label.text = string.Format(format, coins);
            }
        }
    }
}
