using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>
    /// Một ô trong túi đồ, ở dạng lưu được qua Scene: chỉ có chuỗi và số, không có tham chiếu asset.
    /// SlotKey do túi đồ tự đặt và tự đọc (ví dụ "hotbar/0", "backpack/12", "equip/armor"); hệ thống khác không đọc nó.
    /// </summary>
    [Serializable]
    public struct ItemSlotRecord
    {
        public string SlotKey;
        public string ItemId;
        public int Count;

        public ItemSlotRecord(string slotKey, string itemId, int count)
        {
            SlotKey = slotKey;
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// Dữ liệu của một lượt chơi, giữ nguyên khi chuyển Scene (sảnh → ngục → sảnh).
    /// Chỉ sống trong bộ nhớ: tắt game là mất. Lưu xuống máy sẽ làm ở đợt sau.
    ///
    /// Ai ghi, ai đọc:
    ///   - Xu: sảnh trừ khi mua đồ, Scene Game cộng khi dọn phòng (GameBootstrap). Ai cũng đọc được.
    ///   - Túi đồ: túi đồ của người chơi ghi vào đây mỗi khi thay đổi và đọc lại khi được sinh ra trong Scene mới.
    ///   - Ngục đã chọn: sảnh ghi, bộ sinh màn trong Scene Game đọc. Chuỗi rỗng nghĩa là dùng ngục mặc định.
    /// </summary>
    public static class RunState
    {
        private static readonly List<ItemSlotRecord> inventorySlots = new List<ItemSlotRecord>();

        public static int Coins { get; private set; }

        /// <summary>Mã ngục người chơi chọn ở sảnh. Rỗng khi vào thẳng Scene Game (ví dụ lúc thử trong Editor).</summary>
        public static string SelectedDungeonId { get; set; } = string.Empty;

        /// <summary>Phát số xu mới mỗi khi số xu thay đổi.</summary>
        public static event Action<int> CoinsChanged;

        /// <summary>Ảnh chụp túi đồ gần nhất do túi đồ của người chơi ghi lại.</summary>
        public static IReadOnlyList<ItemSlotRecord> InventorySlots => inventorySlots;

        public static void AddCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Coins += amount;
            CoinsChanged?.Invoke(Coins);
        }

        /// <summary>Trừ đúng amount xu, hoặc không trừ gì và trả về false khi không đủ.</summary>
        public static bool TrySpendCoins(int amount)
        {
            if (amount < 0 || amount > Coins)
            {
                return false;
            }

            if (amount > 0)
            {
                Coins -= amount;
                CoinsChanged?.Invoke(Coins);
            }

            return true;
        }

        /// <summary>Thay toàn bộ ảnh chụp túi đồ. Chỉ túi đồ của người chơi gọi hàm này.</summary>
        public static void SaveInventory(IEnumerable<ItemSlotRecord> slots)
        {
            inventorySlots.Clear();
            if (slots != null)
            {
                inventorySlots.AddRange(slots);
            }
        }

        /// <summary>Bắt đầu lượt chơi mới: xóa xu, túi đồ và ngục đã chọn.</summary>
        public static void Reset()
        {
            inventorySlots.Clear();
            SelectedDungeonId = string.Empty;
            var hadCoins = Coins != 0;
            Coins = 0;
            if (hadCoins)
            {
                CoinsChanged?.Invoke(Coins);
            }
        }

        // Xóa dữ liệu cũ mỗi lần vào Play Mode, kể cả khi Editor tắt domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            CoinsChanged = null;
            Reset();
        }
    }
}
