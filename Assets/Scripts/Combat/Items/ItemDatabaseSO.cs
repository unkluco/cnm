using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Items
{
    /// <summary>
    /// Danh mục tổng hợp mọi vật phẩm trong game để tra cứu nhanh bằng ItemId.
    /// Dùng cho việc load túi đồ từ RunState.InventorySlots sau khi đổi Scene.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "LightSouls/Combat/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        [Tooltip("Danh sách toàn bộ vật phẩm trong game.")]
        [SerializeField] private List<ItemDefinitionSO> allItems = new List<ItemDefinitionSO>();

        private Dictionary<string, ItemDefinitionSO> _lookup;

        public IReadOnlyList<ItemDefinitionSO> AllItems => allItems;

        private void InitializeLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, ItemDefinitionSO>();

            foreach (var item in allItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.ItemId))
                {
                    if (!_lookup.ContainsKey(item.ItemId))
                    {
                        _lookup.Add(item.ItemId, item);
                    }
                    else
                    {
                        Debug.LogWarning($"[ItemDatabaseSO] Trùng lặp ItemId '{item.ItemId}' trên asset '{item.name}'.");
                    }
                }
            }
        }

        public ItemDefinitionSO GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            InitializeLookup();
            _lookup.TryGetValue(itemId, out var item);
            return item;
        }

        public bool TryGetItem(string itemId, out ItemDefinitionSO item)
        {
            item = null;
            if (string.IsNullOrEmpty(itemId)) return false;
            InitializeLookup();
            return _lookup.TryGetValue(itemId, out item);
        }

        private void OnValidate()
        {
            HashSet<string> seenIds = new HashSet<string>();
            foreach (var item in allItems)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.ItemId))
                {
                    Debug.LogWarning($"[ItemDatabaseSO] Vật phẩm '{item.name}' chưa có ItemId!", this);
                }
                else if (seenIds.Contains(item.ItemId))
                {
                    Debug.LogWarning($"[ItemDatabaseSO] Phát hiện ItemId trùng lặp: '{item.ItemId}' trên '{item.name}'!", this);
                }
                else
                {
                    seenIds.Add(item.ItemId);
                }
            }
            _lookup = null; // Reset để cache lại khi chạy
        }
    }
}
