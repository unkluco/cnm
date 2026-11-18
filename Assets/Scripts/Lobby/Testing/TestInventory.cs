using System;
using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Lobby.Testing
{
    /// <summary>
    /// Túi đồ thử nghiệm cài đặt IInventory từ LightSouls.Common.
    /// Dùng trong Scene thử nghiệm LobbyTest để kiểm thử chức năng mua hàng khi túi đồ thật của Khải chưa có.
    /// </summary>
    public class TestInventory : MonoBehaviour, IInventory
    {
        [Header("Inventory Capacity")]
        [SerializeField, Min(1)] private int maxSlots = 10;

        [Header("Starting Items (For Testing)")]
        [SerializeField] private List<ItemDefinitionSO> initialItems = new List<ItemDefinitionSO>();

        private readonly Dictionary<string, int> itemCounts = new Dictionary<string, int>();

        public int MaxSlots => maxSlots;
        public int UsedSlots => itemCounts.Count;
        public event Action Changed;

        private void Awake()
        {
            foreach (ItemDefinitionSO item in initialItems)
            {
                if (item != null)
                {
                    TryAdd(item, 1);
                }
            }
        }

        public bool HasSpaceFor(ItemDefinitionSO item, int count = 1)
        {
            if (item == null || count <= 0)
            {
                return false;
            }

            if (itemCounts.ContainsKey(item.ItemId))
            {
                // Đã có trong túi đồ: kiểm tra max stack
                int current = itemCounts[item.ItemId];
                return (current + count) <= (item.MaxStack * 5); // Cho phép xếp chồng nhiều ô trong test
            }

            // Món mới: kiểm tra còn ô trống không
            return itemCounts.Count < maxSlots;
        }

        public bool TryAdd(ItemDefinitionSO item, int count = 1)
        {
            if (!HasSpaceFor(item, count))
            {
                return false;
            }

            if (itemCounts.TryGetValue(item.ItemId, out int current))
            {
                itemCounts[item.ItemId] = current + count;
            }
            else
            {
                itemCounts[item.ItemId] = count;
            }

            SyncRunState();
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(ItemDefinitionSO item, int count = 1)
        {
            if (item == null || count <= 0)
            {
                return false;
            }

            if (!itemCounts.TryGetValue(item.ItemId, out int current) || current < count)
            {
                return false;
            }

            if (current == count)
            {
                itemCounts.Remove(item.ItemId);
            }
            else
            {
                itemCounts[item.ItemId] = current - count;
            }

            SyncRunState();
            Changed?.Invoke();
            return true;
        }

        public int GetCount(ItemDefinitionSO item)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId))
            {
                return 0;
            }

            return itemCounts.TryGetValue(item.ItemId, out int count) ? count : 0;
        }

        private void SyncRunState()
        {
            var records = new List<ItemSlotRecord>(itemCounts.Count);
            int slot = 0;
            foreach (var kvp in itemCounts)
            {
                records.Add(new ItemSlotRecord($"test_slot/{slot++}", kvp.Key, kvp.Value));
            }

            RunState.SaveInventory(records);
        }
    }
}
