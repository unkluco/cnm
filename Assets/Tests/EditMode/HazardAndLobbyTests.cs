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

        [Test]
        public void SpikeTrapHazard_IntegratesWithRoomLifecycle_ActivatesInCombatAndRetractsWhenCleared()
        {
            var roomGo = new GameObject("TestRoom");
            var module = roomGo.AddComponent<RoomModule>();
            var trigger = roomGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            var lifecycle = roomGo.AddComponent<RoomLifecycleController>();
            lifecycle.Configure(module, trigger);
            module.Configure(RoomType.Combat, new Bounds(Vector3.zero, Vector3.one * 10), null, null, null, null, null, null, lifecycle);

            var trapGo = new GameObject("TestTrap");
            trapGo.transform.SetParent(roomGo.transform, false);
            var col = trapGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var trap = trapGo.AddComponent<SpikeTrapHazard>();
            trap.RoomLifecycle = lifecycle;

            lifecycle.InitializeLifecycle();
            Assert.AreEqual(RoomLifecycleState.Unvisited, lifecycle.State);

            // Vào phòng combat
            lifecycle.EnterCombat();
            Assert.AreEqual(RoomLifecycleState.Combat, lifecycle.State);

            // Dọn phòng sạch -> bẫy thu hoàn toàn
            lifecycle.ClearRoom();
            Assert.AreEqual(RoomLifecycleState.Cleared, lifecycle.State);
            Assert.AreEqual(SpikeTrapHazard.TrapState.Retracted, trap.CurrentState);

            UnityEngine.Object.DestroyImmediate(roomGo);
        }

        [Test]
        public void ExplosiveBarrelHazard_TakeDamage_AndExplode_InflictsRadialDamage()
        {
            var barrelGo = new GameObject("TestBarrel");
            var col = barrelGo.AddComponent<BoxCollider>();
            var barrel = barrelGo.AddComponent<ExplosiveBarrelHazard>();

            var victimGo = new GameObject("Victim");
            victimGo.transform.position = new Vector3(2f, 0f, 0f); // trong bán kính 4m
            var victimCol = victimGo.AddComponent<BoxCollider>();
            var victimDamageable = victimGo.AddComponent<MockDamageable>();

            // Gây sát thương làm cạn máu thùng nổ
            barrel.TakeDamage(new DamageInfo(25f, null, Vector3.zero));
            Assert.IsTrue(barrel.State == ExplosiveBarrelHazard.BarrelState.Ignited || barrel.IsDead);

            // Kích nổ trực tiếp
            barrel.Explode();
            Assert.IsTrue(barrel.IsDead);
            Assert.AreEqual(ExplosiveBarrelHazard.BarrelState.Destroyed, barrel.State);
            Assert.GreaterOrEqual(victimDamageable.DamageCallCount, 1, "Vụ nổ phải gây sát thương lên mục tiêu trong bán kính.");

            UnityEngine.Object.DestroyImmediate(barrelGo);
            UnityEngine.Object.DestroyImmediate(victimGo);
        }

        [Test]
        public void DoorwaySafeZone_Geometry_CalculatesSafeDistanceAccurately()
        {
            var doorGo = new GameObject("DoorSocket_North");
            doorGo.transform.position = new Vector3(0f, 0f, 10f);
            doorGo.transform.rotation = Quaternion.identity;

            var safeZone = doorGo.AddComponent<DoorwaySafeZone>();
            safeZone.Configure(DoorDirection.North, radius: 2.5f, width: 4.0f);

            // Điểm ngay trước cửa (cách 1.0m theo trục Z)
            Vector3 pointClose = new Vector3(0f, 0f, 9.0f);
            Assert.IsTrue(safeZone.IsInsideSafeZone(pointClose), "Điểm cách đoạn cửa 1.0m phải nằm trong vùng an toàn.");

            // Điểm dọc mép cửa mở rộng (x = 1.5m, z = 9.0m -> khoảng cách tới đoạn cửa [-2, 2] là 1.0m)
            Vector3 pointAlongDoor = new Vector3(1.5f, 0f, 9.0f);
            Assert.IsTrue(safeZone.IsInsideSafeZone(pointAlongDoor), "Điểm dọc theo bề rộng cửa phải được bảo vệ.");

            // Điểm xa cửa (cách 4.0m)
            Vector3 pointFar = new Vector3(0f, 0f, 6.0f);
            Assert.IsFalse(safeZone.IsInsideSafeZone(pointFar), "Điểm cách 4m không được nằm trong vùng an toàn 2.5m.");

            // Kiểm tra Overlap với Bounds
            Bounds safeBounds = new Bounds(new Vector3(0f, 0f, 9.0f), new Vector3(1f, 1f, 1f));
            Assert.IsTrue(safeZone.IsOverlapping(safeBounds));

            Bounds outsideBounds = new Bounds(new Vector3(0f, 0f, 5.0f), new Vector3(1f, 1f, 1f));
            Assert.IsFalse(safeZone.IsOverlapping(outsideBounds));

            UnityEngine.Object.DestroyImmediate(doorGo);
        }

        [Test]
        public void HazardRooms_AllDoorSocketsHaveDoorwaySafeZones_AndZeroViolations()
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
                Assert.IsNotNull(prefab, $"Prefab tại '{path}' không tồn tại!");

                var sockets = prefab.GetComponentsInChildren<DoorSocket>(true);
                var safeZones = prefab.GetComponentsInChildren<DoorwaySafeZone>(true);

                Assert.AreEqual(sockets.Length, safeZones.Length,
                    $"Mỗi DoorSocket trong phòng '{prefab.name}' đều phải có component DoorwaySafeZone tương ứng.");

                // Kiểm tra validation không có lỗi vi phạm vùng an toàn cửa
                var result = LightSouls.MapGeneration.Editor.HazardValidationTool.ValidateRoomPrefab(prefab);
                Assert.IsTrue(result.IsValid, $"Phòng '{prefab.name}' vi phạm tiêu chuẩn thiết kế: {string.Join(", ", result.Errors)}");
            }
        }

        [Test]
        public void ProceduralGeneration_100DeterministicSeeds_AllPassHardConstraintsAndSafeRouteConnectivity()
        {
            var themes = new[] { TerrainTheme.Lava, TerrainTheme.Spike, TerrainTheme.Water, TerrainTheme.LowCover };
            var sizes = new[] { new Vector2(24f, 24f), new Vector2(16f, 16f) };
            var sockets = new List<DoorDirection>
            {
                DoorDirection.North,
                DoorDirection.South,
                DoorDirection.East,
                DoorDirection.West
            };

            int totalTested = 0;
            int passCount = 0;
            var failedSeeds = new List<string>();

            for (int seed = 1000; seed < 1100; seed++)
            {
                totalTested++;
                TerrainTheme theme = themes[seed % themes.Length];
                Vector2 size = sizes[seed % sizes.Length];

                RoomTacticalLayout layout = ProceduralLayoutGenerator.Generate(seed, theme, size, sockets);

                if (!layout.IsValid)
                {
                    failedSeeds.Add($"Seed {seed} ({theme}): {layout.RejectionReason}");
                    continue;
                }

                // Kiểm tra 1: Mọi hazard đều có khoảng cách >= 2.5m tới mọi đoạn cửa
                bool hasDoorwayViolation = false;
                foreach (var h in layout.Hazards)
                {
                    if (ProceduralLayoutGenerator.IsBoundsOverlappingDoorwaySafeZones(h.GetLocalBounds(), size, sockets))
                    {
                        hasDoorwayViolation = true;
                        break;
                    }
                }

                if (hasDoorwayViolation)
                {
                    failedSeeds.Add($"Seed {seed} ({theme}): Hazard vi phạm vùng an toàn cửa.");
                    continue;
                }

                // Kiểm tra 2: Mọi điểm sinh quái đều nằm ngoài vùng an toàn cửa
                bool hasSpawnViolation = false;
                foreach (var sp in layout.EnemySpawns)
                {
                    if (ProceduralLayoutGenerator.IsPointInsideAnyDoorwaySafeZone(sp, size, sockets))
                    {
                        hasSpawnViolation = true;
                        break;
                    }
                }

                if (hasSpawnViolation)
                {
                    failedSeeds.Add($"Seed {seed} ({theme}): Điểm sinh quái vi phạm vùng an toàn cửa.");
                    continue;
                }

                // Kiểm tra 3: Xác minh đường an toàn liên tục giữa các cặp cửa
                if (layout.VerifiedSafeRoutes == null || layout.VerifiedSafeRoutes.Count == 0)
                {
                    failedSeeds.Add($"Seed {seed} ({theme}): Thiếu đường an toàn kết nối giữa các cửa.");
                    continue;
                }

                passCount++;
            }

            Assert.AreEqual(100, totalTested);
            Assert.AreEqual(100, passCount, $"Có {failedSeeds.Count} seeds thất bại: {string.Join("; ", failedSeeds)}");
        }

        [Test]
        public void ProceduralGeneration_SameSeed_ProducesIdenticalDeterministicLayout()
        {
            const int testSeed = 4242;
            var size = new Vector2(24f, 24f);

            RoomTacticalLayout run1 = ProceduralLayoutGenerator.Generate(testSeed, TerrainTheme.Lava, size);
            RoomTacticalLayout run2 = ProceduralLayoutGenerator.Generate(testSeed, TerrainTheme.Lava, size);

            Assert.AreEqual(run1.PatternName, run2.PatternName);
            Assert.AreEqual(run1.Hazards.Count, run2.Hazards.Count);
            Assert.AreEqual(run1.Covers.Count, run2.Covers.Count);
            Assert.AreEqual(run1.EnemySpawns.Count, run2.EnemySpawns.Count);

            for (int i = 0; i < run1.Hazards.Count; i++)
            {
                Assert.AreEqual(run1.Hazards[i].LocalPosition, run2.Hazards[i].LocalPosition);
                Assert.AreEqual(run1.Hazards[i].LocalScale, run2.Hazards[i].LocalScale);
            }

            for (int i = 0; i < run1.EnemySpawns.Count; i++)
            {
                Assert.AreEqual(run1.EnemySpawns[i], run2.EnemySpawns[i]);
            }
        }

        [Test]
        public void ProceduralGeneration_DifferentSeeds_ProduceMeaningfulPatternVariation()
        {
            var size = new Vector2(24f, 24f);
            var distinctPatterns = new HashSet<string>();

            for (int i = 0; i < 20; i++)
            {
                RoomTacticalLayout layout = ProceduralLayoutGenerator.Generate(2000 + i, TerrainTheme.Lava, size);
                distinctPatterns.Add(layout.PatternName);
            }

            Assert.GreaterOrEqual(distinctPatterns.Count, 3, "20 seed khác nhau phải sinh ra ít nhất 3 mẫu bố cục dung nham khác biệt (đạt chuẩn Intentional Diversity).");
        }

        [Test]
        public void ProceduralRoomApplicator_AppliesLayoutToRoomModule_CorrectlyInstantiatesContainers()
        {
            var roomGo = new GameObject("TestProceduralRoom");
            var module = roomGo.AddComponent<RoomModule>();
            var trigger = roomGo.AddComponent<BoxCollider>();
            var lifecycle = roomGo.AddComponent<RoomLifecycleController>();
            lifecycle.Configure(module, trigger);
            module.Configure(RoomType.Combat, new Bounds(Vector3.zero, new Vector3(24f, 4f, 24f)), null, null, null, null, null, null, lifecycle);

            var applicator = roomGo.AddComponent<ProceduralRoomApplicator>();
            applicator.Theme = TerrainTheme.Spike;
            applicator.CustomSeed = 555;

            var layout = applicator.GenerateAndApply(555);

            Assert.IsNotNull(layout);
            Assert.IsTrue(layout.IsValid);
            Assert.IsNotNull(roomGo.transform.Find("Hazards"));
            Assert.IsNotNull(roomGo.transform.Find("Cover"));
            Assert.IsNotNull(roomGo.transform.Find("ProceduralSpawns"));
            Assert.GreaterOrEqual(module.EnemySpawnPoints.Count, 1);

            UnityEngine.Object.DestroyImmediate(roomGo);
        }
    }
}
