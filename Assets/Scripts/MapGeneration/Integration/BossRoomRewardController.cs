using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Controls the dynamic appearance of 1 to 2 random bonus treasure chests
    /// in the Boss Room (Room_Exit) once the boss is defeated.
    /// </summary>
    public class BossRoomRewardController : MonoBehaviour
    {
        [Header("Boss Reward Chests")]
        [SerializeField] private List<GameObject> rewardChests = new List<GameObject>();
        [SerializeField] private bool areRewardsSpawned = false;
        [SerializeField] private int spawnedChestCount = 0;

        public IReadOnlyList<GameObject> RewardChests => rewardChests;
        public bool AreRewardsSpawned => areRewardsSpawned;
        public int SpawnedChestCount => spawnedChestCount;

        public event Action<int> OnBossRewardsSpawned;

        private void Awake()
        {
            // By default before the boss is defeated, reward chests are hidden
            HideAllChests();
        }

        public void ConfigureChests(List<GameObject> chests)
        {
            rewardChests = chests ?? new List<GameObject>();
            HideAllChests();
        }

        public void HideAllChests()
        {
            if (rewardChests == null) return;
            foreach (GameObject chest in rewardChests)
            {
                if (chest != null)
                {
                    chest.SetActive(false);
                }
            }
            areRewardsSpawned = false;
            spawnedChestCount = 0;
        }

        /// <summary>
        /// Called when the boss dies in Room_Exit.
        /// Randomly activates 1 to 2 chests at the prepared spawn locations.
        /// </summary>
        public int TriggerBossRewards()
        {
            if (areRewardsSpawned)
            {
                return spawnedChestCount;
            }

            if (rewardChests == null || rewardChests.Count == 0)
            {
                Debug.LogWarning("[BossRoomRewardController] No reward chest objects assigned.");
                return 0;
            }

            // User requirement: randomly 1 to 2 chests appear
            int targetChests = UnityEngine.Random.Range(1, Math.Min(3, rewardChests.Count + 1));
            spawnedChestCount = 0;

            for (int i = 0; i < rewardChests.Count; i++)
            {
                if (rewardChests[i] != null)
                {
                    bool activate = i < targetChests;
                    rewardChests[i].SetActive(activate);
                    if (activate)
                    {
                        spawnedChestCount++;
                    }
                }
            }

            areRewardsSpawned = true;
            Debug.Log($"[BossRoomRewardController] Boss defeated! Successfully spawned {spawnedChestCount} bonus chest(s) in the Boss Room.");
            OnBossRewardsSpawned?.Invoke(spawnedChestCount);
            return spawnedChestCount;
        }
    }
}
