using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Immutable snapshot of a newly generated dungeon level,
    /// published to gameplay systems (Player Spawner, Enemy Spawner, GameManager).
    /// </summary>
    public sealed class DungeonRuntimeContext
    {
        public DungeonLayoutData Layout { get; }
        public Transform PlayerSpawnPoint { get; }
        public Transform LevelExitPoint { get; }
        public IReadOnlyList<Transform> EnemySpawnPoints { get; }
        public IReadOnlyList<Transform> LootSpawnPoints { get; }
        public IReadOnlyList<RoomModule> SpawnedRooms { get; }
        public WeaponVendingMachine VendingMachine { get; }
        public BossRoomRewardController BossRewardController { get; }
        public bool IsNavMeshBaked { get; }
        public bool IsNavMeshReachable { get; }

        public DungeonRuntimeContext(
            DungeonLayoutData layout,
            Transform playerSpawnPoint,
            Transform levelExitPoint,
            IReadOnlyList<Transform> enemySpawnPoints,
            IReadOnlyList<Transform> lootSpawnPoints,
            IReadOnlyList<RoomModule> spawnedRooms,
            bool isNavMeshBaked,
            bool isNavMeshReachable,
            WeaponVendingMachine vendingMachine = null,
            BossRoomRewardController bossRewardController = null)
        {
            Layout = layout;
            PlayerSpawnPoint = playerSpawnPoint;
            LevelExitPoint = levelExitPoint;
            EnemySpawnPoints = enemySpawnPoints;
            LootSpawnPoints = lootSpawnPoints;
            SpawnedRooms = spawnedRooms;
            IsNavMeshBaked = isNavMeshBaked;
            IsNavMeshReachable = isNavMeshReachable;
            VendingMachine = vendingMachine;
            BossRewardController = bossRewardController;
        }

        /// <summary>
        /// Convenience method for Combat/GameManager to trigger bonus chests in the boss room when the boss dies.
        /// </summary>
        public int TriggerBossRewards()
        {
            return BossRewardController != null ? BossRewardController.TriggerBossRewards() : 0;
        }
    }
}
