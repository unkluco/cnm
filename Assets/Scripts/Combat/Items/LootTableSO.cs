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
        [Tooltip("Ngục ưu tiên (ví dụ dungeon.lava). Để trống nếu rơi như nhau ở mọi ngục.")]
        public string FavoredDungeonId;
        [Tooltip("Hệ số nhân trọng số khi đang ở ngục ưu tiên (ví dụ 3 = dễ rơi gấp 3).")]
        [Min(0f)] public float FavoredMultiplier;

        /// <summary>Trọng số thực tế ở ngục đang chơi.</summary>
        public float EffectiveWeight(string dungeonId)
        {
            if (!string.IsNullOrEmpty(FavoredDungeonId) && FavoredMultiplier > 0f && FavoredDungeonId == dungeonId)
            {
                return Weight * FavoredMultiplier;
            }

            return Weight;
        }
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

            string dungeonId = RunState.SelectedDungeonId;
            float totalWeight = 0f;
            foreach (var entry in dropEntries)
            {
                if (entry.Item != null && entry.Weight > 0f)
                {
                    totalWeight += entry.EffectiveWeight(dungeonId);
                }
            }

            if (totalWeight <= 0f) return false;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in dropEntries)
            {
                if (entry.Item == null || entry.Weight <= 0f) continue;

                cumulative += entry.EffectiveWeight(dungeonId);
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
