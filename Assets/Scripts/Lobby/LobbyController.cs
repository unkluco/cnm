using System;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Lobby
{
    /// <summary>
    /// Nhạc trưởng điều phối sảnh (Lobby).
    /// Phát sự kiện DungeonChosen khi người chơi chọn ngục để nhóm trưởng nghe và nạp Scene Game.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        public static LobbyController Instance { get; private set; }

        /// <summary>
        /// Sự kiện phát ra khi người chơi chọn một ngục tại cổng sảnh.
        /// Tham số string là dungeonId tương ứng trong DungeonCatalogSO.
        /// Nhóm trưởng sẽ nghe sự kiện này để chuyển sang Scene Game.
        /// </summary>
        public static event Action<string> DungeonChosen;

        [Header("References")]
        [SerializeField] private LobbyShopUI shopUI;
        [SerializeField] private DungeonSelectUI dungeonSelectUI;

        public LobbyShopUI ShopUI => shopUI;
        public DungeonSelectUI DungeonSelectUI => dungeonSelectUI;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void SelectDungeon(string dungeonId)
        {
            RunState.SelectedDungeonId = dungeonId ?? string.Empty;
            Debug.Log($"[LobbyController] Người chơi đã chọn ngục: '{RunState.SelectedDungeonId}'");
            DungeonChosen?.Invoke(RunState.SelectedDungeonId);
        }

        public void OpenShop(GameObject interactor)
        {
            if (shopUI != null)
            {
                shopUI.Open(interactor);
            }
        }

        public void OpenDungeonSelect(GameObject interactor)
        {
            if (dungeonSelectUI != null)
            {
                dungeonSelectUI.Open(interactor);
            }
        }
    }
}
