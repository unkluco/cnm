using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Direct component-level contract exposed by DungeonGenerator for teammate integration.
    /// </summary>
    public interface IDungeonProvider
    {
        bool IsGenerated { get; }
        DungeonLayoutData CurrentLayout { get; }
        Transform PlayerSpawnPoint { get; }
        Transform LevelExitPoint { get; }
        IReadOnlyList<Transform> EnemySpawnPoints { get; }
        IReadOnlyList<Transform> LootSpawnPoints { get; }
        IReadOnlyList<RoomModule> SpawnedRooms { get; }

        event Action<DungeonRuntimeContext> OnDungeonGenerated;
    }
}
