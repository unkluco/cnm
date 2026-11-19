using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>Resolves a mystery exit on entry. A null result means return to the shop.</summary>
    public static class BossPortalRouter
    {
        public static DungeonEntry ChooseDestination(DungeonCatalogSO catalog, string currentDungeonId,
            DungeonGenerationConfigSO currentConfig, float shopProbability, float routeRoll, float dungeonRoll)
        {
            float probability = Mathf.Clamp01(shopProbability);
            if (probability >= 1f || (probability > 0f && routeRoll < probability) || catalog == null)
                return null;

            var alternatives = new List<DungeonEntry>();
            foreach (var entry in catalog.Entries)
            {
                if (entry == null || entry.config == null || string.IsNullOrEmpty(entry.dungeonId)) continue;
                if (string.Equals(entry.dungeonId, currentDungeonId, StringComparison.OrdinalIgnoreCase) ||
                    entry.config == currentConfig) continue;
                alternatives.Add(entry);
            }

            // No distinct dungeon is available: return safely to the shop instead of reloading the same map type.
            if (alternatives.Count == 0) return null;
            int index = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(dungeonRoll) * alternatives.Count), alternatives.Count - 1);
            return alternatives[index];
        }
    }
}
