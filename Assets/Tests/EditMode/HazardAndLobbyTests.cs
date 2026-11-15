using System;
using System.Collections.Generic;
using LightSouls.Common;
using LightSouls.Lobby;
using LightSouls.Lobby.Testing;
using LightSouls.MapGeneration.Hazards;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class HazardAndLobbyTests
    {
        private class MockDamageable : MonoBehaviour, IDamageable
        {
            public float Health = 100f;
            public bool IsDead => Health <= 0f;
            public int DamageCallCount { get; private set; }
            public float LastDamageAmount { get; private set; }
            public event Action<IDamageable> Died;

            public void TakeDamage(DamageInfo info)
            {
                DamageCallCount++;
                LastDamageAmount = info.Amount;
                Health -= info.Amount;
                if (Health <= 0f)
                {
                    Died?.Invoke(this);
                }
            }
        }

        private class MockSpeedModifiable : MonoBehaviour, ISpeedModifiable
        {
            public float Multiplier = 1f;
            public bool HasMultiplierApplied;

            public void SetSpeedMultiplier(object source, float multiplier)
            {
                Multiplier = multiplier;
                HasMultiplierApplied = true;
            }

            public void RemoveSpeedMultiplier(object source)
            {
                Multiplier = 1f;
                HasMultiplierApplied = false;
            }
        }

        [SetUp]
        public void Setup()
        {
            RunState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            RunState.Reset();
        }

        [Test]
        public void LavaHazard_InflictsDamageOnDamageableEntity()
        {
            var hazardGo = new GameObject("TestLava");
            hazardGo.layer = HazardZoneBase.HazardLayer;
            var box = hazardGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var lava = hazardGo.AddComponent<LavaHazardZone>();
            lava.DamagePerTick = 8f;

            var entityGo = new GameObject("Victim");
            var col = entityGo.AddComponent<BoxCollider>();
            var damageable = entityGo.AddComponent<MockDamageable>();

            // Mô phỏng OnTriggerEnter
            var onTriggerEnter = typeof(HazardZoneBase).GetMethod("OnTriggerEnter",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(onTriggerEnter);
            onTriggerEnter.Invoke(lava, new object[] { col });

            Assert.AreEqual(1, damageable.DamageCallCount, "Lava phải gây sát thương ngay khi đối tượng bước vào vùng.");
            Assert.AreEqual(8f, damageable.LastDamageAmount);
            Assert.AreEqual(92f, damageable.Health);

            UnityEngine.Object.DestroyImmediate(hazardGo);
            UnityEngine.Object.DestroyImmediate(entityGo);
        }

        [Test]
        public void WaterHazard_AppliesAndRemovesSlowMultiplier()
        {
            var hazardGo = new GameObject("TestWater");
            hazardGo.layer = HazardZoneBase.HazardLayer;
            var box = hazardGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var water = hazardGo.AddComponent<WaterHazardZone>();
            water.SpeedMultiplier = 0.5f;

            var entityGo = new GameObject("Swimmer");
            var col = entityGo.AddComponent<BoxCollider>();
            var modifiable = entityGo.AddComponent<MockSpeedModifiable>();

            var onTriggerEnter = typeof(HazardZoneBase).GetMethod("OnTriggerEnter",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var onTriggerExit = typeof(HazardZoneBase).GetMethod("OnTriggerExit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Bước vào nước
            onTriggerEnter.Invoke(water, new object[] { col });
            Assert.IsTrue(modifiable.HasMultiplierApplied, "Nước phải áp dụng hệ số làm chậm khi bước vào.");
            Assert.AreEqual(0.5f, modifiable.Multiplier);

            // Rời khỏi nước
            onTriggerExit.Invoke(water, new object[] { col });
            Assert.IsFalse(modifiable.HasMultiplierApplied, "Hệ số làm chậm phải được gỡ bỏ khi rời khỏi vùng nước.");
            Assert.AreEqual(1.0f, modifiable.Multiplier);

            UnityEngine.Object.DestroyImmediate(hazardGo);
            UnityEngine.Object.DestroyImmediate(entityGo);
        }

        [Test]
        public void DungeonCatalog_ResolvesCatalogAndSelectedDungeonId()
        {
            var catalog = ScriptableObject.CreateInstance<DungeonCatalogSO>();
            var configA = ScriptableObject.CreateInstance<DungeonGenerationConfigSO>();
            var configB = ScriptableObject.CreateInstance<DungeonGenerationConfigSO>();

            var entriesField = typeof(DungeonCatalogSO).GetField("entries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            entriesField.SetValue(catalog, new List<DungeonEntry>
            {
                new DungeonEntry { dungeonId = "dungeon.standard", displayName = "Chuẩn", config = configA },
                new DungeonEntry { dungeonId = "dungeon.lava", displayName = "Dung Nham", config = configB }
            });

            Assert.IsTrue(catalog.TryGetConfig("dungeon.standard", out DungeonGenerationConfigSO resolvedA));
            Assert.AreSame(configA, resolvedA);

            Assert.IsTrue(catalog.TryGetConfig("dungeon.lava", out DungeonGenerationConfigSO resolvedB));
            Assert.AreSame(configB, resolvedB);

            // Kiểm tra tích hợp vào DungeonGenerator
            var genGo = new GameObject("Generator");
            var generator = genGo.AddComponent<DungeonGenerator>();
            generator.Config = configA;
            generator.Catalog = catalog;

            // Chưa chọn: trả về config mặc định
            RunState.SelectedDungeonId = string.Empty;
            Assert.AreSame(configA, generator.ResolveActiveConfig());

            // Đã chọn ngục lava: trả về config lava
            RunState.SelectedDungeonId = "dungeon.lava";
            Assert.AreSame(configB, generator.ResolveActiveConfig());

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(configA);
            UnityEngine.Object.DestroyImmediate(configB);
            UnityEngine.Object.DestroyImmediate(genGo);
        }

        [Test]
        public void LobbyController_SelectDungeon_UpdatesRunStateAndEvent()
        {
            string chosenDungeon = null;
            Action<string> listener = id => chosenDungeon = id;
            LobbyController.DungeonChosen += listener;

            try
            {
                LobbyController.SelectDungeon("dungeon.maze");
                Assert.AreEqual("dungeon.maze", RunState.SelectedDungeonId);
                Assert.AreEqual("dungeon.maze", chosenDungeon);
            }
            finally
            {
                LobbyController.DungeonChosen -= listener;
            }
        }

        [Test]
        public void BackButtonStack_ManagesStackAndClosesTop()
        {
            var ownerA = new GameObject("PanelA");
            var ownerB = new GameObject("PanelB");

            bool closedA = false;
            bool closedB = false;

            BackButtonStack.Push(ownerA, () => closedA = true);
            BackButtonStack.Push(ownerB, () => closedB = true);

            Assert.IsTrue(BackButtonStack.HasOpenPanel);

            // Đóng bảng trên cùng (B)
            Assert.IsTrue(BackButtonStack.TryCloseTop());
            Assert.IsTrue(closedB);
            Assert.IsFalse(closedA);

            // Đóng bảng tiếp theo (A)
            Assert.IsTrue(BackButtonStack.TryCloseTop());
            Assert.IsTrue(closedA);

            // Không còn bảng nào
            Assert.IsFalse(BackButtonStack.TryCloseTop());
            Assert.IsFalse(BackButtonStack.HasOpenPanel);

            UnityEngine.Object.DestroyImmediate(ownerA);
            UnityEngine.Object.DestroyImmediate(ownerB);
        }

        [Test]
        public void LobbyShop_BuyItem_FlowValidation()
        {
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            var inventory = playerGo.AddComponent<TestInventory>();

            var item = ScriptableObject.CreateInstance<TestItemDefinitionSO>();
            var idField = typeof(ItemDefinitionSO).GetField("itemId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var priceField = typeof(ItemDefinitionSO).GetField("price",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxStackField = typeof(ItemDefinitionSO).GetField("maxStack",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            idField.SetValue(item, "item.sword_test");
            priceField.SetValue(item, 50);
            maxStackField.SetValue(item, 1);

            // 1. Không đủ xu
            Assert.AreEqual(0, RunState.Coins);
            Assert.IsFalse(RunState.TrySpendCoins(item.Price));

            // 2. Cộng đủ xu và mua thành công
            RunState.AddCoins(100);
            Assert.AreEqual(100, RunState.Coins);

            Assert.IsTrue(inventory.HasSpaceFor(item, 1));
            Assert.IsTrue(RunState.TrySpendCoins(item.Price));
            Assert.IsTrue(inventory.TryAdd(item, 1));

            Assert.AreEqual(50, RunState.Coins, "Số xu phải bị trừ 50.");
            Assert.AreEqual(1, inventory.GetCount(item), "Túi đồ phải có 1 món.");

            UnityEngine.Object.DestroyImmediate(playerGo);
            UnityEngine.Object.DestroyImmediate(item);
        }

        [Test]
        public void NewHazardRoomPrefabs_ExistAndHaveCorrectConfiguration()
        {
            string[] prefabPaths =
            {
                "Assets/Prefabs/MapGeneration/Rooms/Room_Lava_Arena.prefab",
                "Assets/Prefabs/MapGeneration/Rooms/Room_Water_Pond.prefab",
                "Assets/Prefabs/MapGeneration/Rooms/Room_Spike_Traps.prefab",
                "Assets/Prefabs/MapGeneration/Rooms/Room_Maze_Large.prefab"
            };

            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Prefab tại đường dẫn '{path}' không tồn tại!");

                RoomModule module = prefab.GetComponent<RoomModule>();
                Assert.IsNotNull(module, $"Prefab '{prefab.name}' thiếu component RoomModule!");
                Assert.AreEqual(RoomType.Combat, module.RoomType, $"Prefab '{prefab.name}' phải là RoomType.Combat.");

                // Kiểm tra có DoorSocket
                DoorSocket[] sockets = prefab.GetComponentsInChildren<DoorSocket>(true);
                Assert.GreaterOrEqual(sockets.Length, 2, $"Prefab '{prefab.name}' phải có ít nhất 2 door socket.");

                // Kiểm tra có điểm spawn quái
                Assert.GreaterOrEqual(module.EnemySpawnPoints.Count, 1, $"Prefab '{prefab.name}' phải có ít nhất 1 điểm sinh quái.");
            }
        }
    }
}
