using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Mục thông tin của một ngục trong danh mục, dùng cho bảng chọn ngục ở Sảnh.
    /// </summary>
    [Serializable]
    public class DungeonEntry
    {
        [Tooltip("Mã ngục duy nhất, ví dụ: 'dungeon.normal', 'dungeon.lava', 'dungeon.maze'.")]
        public string dungeonId;

        public string displayName;

        [TextArea]
        public string description;

        public Sprite icon;

        [Tooltip("Cấu hình sinh màn tương ứng cho ngục này.")]
        public DungeonGenerationConfigSO config;
    }

    /// <summary>
    /// Danh mục các ngục có thể chọn tại Sảnh.
    /// DungeonGenerator tra cứu catalog này theo RunState.SelectedDungeonId để chọn config sinh màn phù hợp.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonCatalog", menuName = "LightSouls/Map Generation/Dungeon Catalog")]
    public class DungeonCatalogSO : ScriptableObject
    {
        [SerializeField] private List<DungeonEntry> entries = new List<DungeonEntry>();

        public IReadOnlyList<DungeonEntry> Entries => entries;

        public bool TryGetEntry(string dungeonId, out DungeonEntry entry)
        {
            if (!string.IsNullOrEmpty(dungeonId))
            {
                foreach (DungeonEntry item in entries)
                {
                    if (item != null && string.Equals(item.dungeonId, dungeonId, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = item;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        public bool TryGetConfig(string dungeonId, out DungeonGenerationConfigSO config)
        {
            if (TryGetEntry(dungeonId, out DungeonEntry entry) && entry.config != null)
            {
                config = entry.config;
                return true;
            }

            config = null;
            return false;
        }
    }
}
