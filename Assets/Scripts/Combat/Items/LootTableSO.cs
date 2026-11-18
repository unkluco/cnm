using System;
using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Items
{
    [Serializable]
    public struct LootDropEntry
    {
        [Tooltip("Vật phẩm rơi.")]
        public ItemDefinitionSO Item;
        [Tooltip("Số lượng tối thiểu.")]
        [Min(1)] public int MinCount;
        [Tooltip("Số lượng tối đa.")]
        [Min(1)] public int MaxCount;
        [Tooltip("Trọng số tỉ lệ xuất hiện (càng cao càng dễ rơi).")]
        [Min(0f)] public float Weight;
    }

    [CreateAssetMenu(fileName = "LootTable_", menuName = "LightSouls/Combat/Loot Table")]
    public class LootTableSO : ScriptableObject
    {
        [Tooltip("Danh sách các mục rơi đồ kèm trọng số.")]
        [SerializeField] private List<LootDropEntry> dropEntries = new List<LootDropEntry>();

        public IReadOnlyList<LootDropEntry> DropEntries => dropEntries;

        /// <summary>
        /// Quay thưởng ngẫu nhiên chọn ra 1 vật phẩm theo trọng số.
        /// Trả về true nếu chọn được vật phẩm kèm số lượng ngẫu nhiên trong khoảng [MinCount, MaxCount].
        /// </summary>
        public bool RollDrop(out ItemDefinitionSO droppedItem, out int dropCount)
        {
            droppedItem = null;
            dropCount = 0;

            if (dropEntries == null || dropEntries.Count == 0) return false;

            float totalWeight = 0f;
            foreach (var entry in dropEntries)
            {
                if (entry.Item != null && entry.Weight > 0f)
                {
                    totalWeight += entry.Weight;
                }
            }

            if (totalWeight <= 0f) return false;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in dropEntries)
            {
                if (entry.Item == null || entry.Weight <= 0f) continue;

                cumulative += entry.Weight;
                if (roll <= cumulative)
                {
                    droppedItem = entry.Item;
                    dropCount = UnityEngine.Random.Range(entry.MinCount, entry.MaxCount + 1);
                    return true;
                }
            }

            return false;
        }
    }
}
