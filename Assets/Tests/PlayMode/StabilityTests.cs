using System;
using System.Collections;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Items;
using LightSouls.Combat.UI;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.MapGeneration;
using LightSouls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LightSouls.Stability.Tests
{
    public class StabilityTests
    {
        private static IEnumerator WaitFor(Func<bool> predicate, string message)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsTrue(predicate(), message);
            yield return null;
        }

        private static PlayerController FindPlayer() => Object.FindAnyObjectByType<PlayerController>();

        private static IEnumerator StartRun()
        {
            SceneLoader.StartNewRun();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Lobby && FindPlayer() != null,
                "Lobby did not load a player.");
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            Time.timeScale = 1f;
            RunState.Reset();
            SceneLoader.LoadMainMenu();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu,
                "MainMenu did not load.");
        }

        [UnityTest]
        public IEnumerator EmptyInventoryRemainsEmptyAcrossSceneAndPanelsAreExclusive()
        {
            yield return StartRun();
            var inventory = FindPlayer().Inventory;
            Assert.IsTrue(inventory.HotbarSlots.Any(slot => !slot.IsEmpty), "New run should have starting items.");
            foreach (var slot in inventory.HotbarSlots.Concat(inventory.BackpackSlots).Concat(inventory.EquipmentSlots.Values).Concat(inventory.MaterialSlots.Values))
            {
                if (!slot.IsEmpty) Assert.IsTrue(inventory.TryRemoveFromSlot(slot.SlotKey, slot.Count));
            }
            Assert.IsTrue(RunState.HasInventorySnapshot);
            Assert.IsEmpty(RunState.InventorySlots);

            SceneLoader.LoadGame();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Game && FindPlayer() != null,
                "Dungeon did not generate a player.");
            Assert.IsEmpty(RunState.InventorySlots, "Starting items were incorrectly restored.");
            Assert.IsTrue(FindPlayer().Inventory.HotbarSlots.All(slot => slot.IsEmpty));
            var defaultArrival = Object.FindAnyObjectByType<DungeonArrivalBanner>();
            Assert.NotNull(defaultArrival);
            Assert.AreEqual("dungeon.standard", defaultArrival.DungeonId, "Direct Game entry should announce the actual fallback dungeon.");

            var pause = Object.FindAnyObjectByType<PauseMenuController>();
            var backpack = Object.FindAnyObjectByType<BackpackUI>(FindObjectsInactive.Include);
            Assert.NotNull(pause);
            Assert.NotNull(backpack);
            pause.Pause();
            Assert.IsTrue(pause.IsPaused);
            backpack.Open();
            Assert.IsFalse(backpack.IsOpen, "Backpack must not open over pause.");
            pause.Resume();
            backpack.Open();
            Assert.IsTrue(backpack.IsOpen);
            pause.Pause();
            Assert.IsFalse(pause.IsPaused, "Pause must not open over backpack.");
            Assert.IsTrue(BackButtonStack.TryCloseTop());
            Assert.IsFalse(backpack.IsOpen);
            Assert.IsFalse(BackButtonStack.HasOpenPanel);

            var generator = Object.FindAnyObjectByType<DungeonGenerator>();
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.ReturnToShopChance = 1f;
            Assert.IsTrue(generator.IsGenerated);
            Assert.IsTrue(generator.IsNavMeshPathReachable);
            var exit = generator.SpawnedRooms.First(room => room.RoomType == RoomType.Exit);
            Assert.IsFalse(bootstrap.IsExitPortalOpen);
            Assert.IsFalse(generator.LevelExitPoint.gameObject.activeSelf, "Portal should be hidden before boss is cleared.");
            int before = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None).Length;
            Assert.IsTrue(exit.LifecycleController.EnterCombat());
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Assert.Greater(spawner.AliveEnemyCount, 0);
            FindPlayer().Teleport(generator.LevelExitPoint.position);
            foreach (var enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            {
                enemy.TakeDamage(new DamageInfo(100000f, FindPlayer().gameObject, enemy.transform.position));
            }
            yield return null;
            Assert.AreEqual(RoomLifecycleState.Cleared, exit.LifecycleController.State);
            int after = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(exit.BossRewardController.SpawnedChestCount, after - before,
                "All activated reward markers should become real treasure chests.");
            Assert.Greater(after, before);
            Assert.IsTrue(bootstrap.IsExitPortalOpen);
            Assert.IsTrue(generator.LevelExitPoint.gameObject.activeSelf);
            Assert.IsTrue(string.IsNullOrEmpty(RunState.PendingPortalArrivalMessage), "Destination must remain unknown before entry.");
            Assert.AreEqual(SceneNames.Game, SceneManager.GetActiveScene().name, "The player must have an opportunity to collect the rewards.");
            Assert.IsFalse(GameResultScreen.IsOpen);

            int coins = RunState.Coins;
            // Rời cổng vừa mở rồi bước lại vào. Đây là nhánh 100% về sảnh để kiểm thử xác định.
            FindPlayer().Teleport(generator.PlayerSpawnPoint.position);
            yield return null;
            FindPlayer().Teleport(generator.LevelExitPoint.position);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Lobby && FindPlayer() != null,
                "Returning to the portal should load the shop lobby.");
            Assert.AreEqual(coins, RunState.Coins);
            Assert.IsEmpty(RunState.InventorySlots);
            Assert.IsFalse(GameResultScreen.IsOpen);
        }

        [UnityTest]
        public IEnumerator MysteryPortalCanLeadToDifferentDungeonWithoutResettingRun()
        {
            yield return StartRun();
            RunState.AddCoins(41);
            RunState.SelectedDungeonId = "dungeon.standard";
            var snapshot = RunState.InventorySlots.Select(slot => slot.SlotKey + ":" + slot.ItemId + ":" + slot.Count).ToArray();
            SceneLoader.LoadGame();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Game && FindPlayer() != null,
                "Dungeon did not load.");
            var oldGenerator = Object.FindAnyObjectByType<DungeonGenerator>();
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.ReturnToShopChance = 0f;
            var exit = oldGenerator.SpawnedRooms.First(room => room.RoomType == RoomType.Exit);
            Assert.IsFalse(bootstrap.IsExitPortalOpen);
            Assert.IsTrue(exit.LifecycleController.EnterCombat());
            FindPlayer().Teleport(oldGenerator.LevelExitPoint.position);
            yield return null;
            Assert.AreEqual("dungeon.standard", RunState.SelectedDungeonId, "Locked portal must not change the destination.");
            foreach (var enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            {
                enemy.TakeDamage(new DamageInfo(100000f, FindPlayer().gameObject, enemy.transform.position));
            }
            yield return null;
            Assert.IsTrue(bootstrap.IsExitPortalOpen);
            Assert.AreEqual("dungeon.standard", RunState.SelectedDungeonId, "Destination must not be chosen until entering the open portal.");
            Assert.IsEmpty(RunState.PendingPortalArrivalMessage);
            int coins = RunState.Coins;

            // Walk away and back into the opened portal. The selected dungeon changes and Game reloads.
            FindPlayer().Teleport(oldGenerator.PlayerSpawnPoint.position);
            yield return null;
            FindPlayer().Teleport(oldGenerator.LevelExitPoint.position);
            yield return WaitFor(() =>
            {
                var current = Object.FindAnyObjectByType<DungeonGenerator>();
                return current != null && current != oldGenerator && current.IsGenerated && FindPlayer() != null;
            }, "Mystery portal did not generate the next dungeon.");
            Assert.AreEqual(SceneNames.Game, SceneManager.GetActiveScene().name);
            Assert.AreNotEqual("dungeon.standard", RunState.SelectedDungeonId);
            Assert.IsTrue(new[] { "dungeon.lava", "dungeon.maze" }.Contains(RunState.SelectedDungeonId));
            Assert.AreEqual(coins, RunState.Coins);
            CollectionAssert.AreEqual(snapshot, RunState.InventorySlots.Select(slot => slot.SlotKey + ":" + slot.ItemId + ":" + slot.Count));
            var arrival = Object.FindAnyObjectByType<DungeonArrivalBanner>();
            Assert.NotNull(arrival, "The destination is announced only after the new dungeon has loaded.");
            Assert.IsTrue(arrival.FromMysteryPortal);
            Assert.AreEqual(RunState.SelectedDungeonId, arrival.DungeonId);
            Assert.IsEmpty(RunState.PendingPortalArrivalMessage);
            Assert.IsTrue(Object.FindAnyObjectByType<WeaponVendingMachine>().IsShopReady);
            Assert.IsFalse(Object.FindAnyObjectByType<GameBootstrap>().IsExitPortalOpen, "The new dungeon starts with its own portal locked.");
        }

        [UnityTest]
        public IEnumerator MissingDungeonConfigReturnsToLobbyWithoutResettingRun()
        {
            yield return StartRun();
            RunState.AddCoins(37);
            var snapshot = RunState.InventorySlots.ToArray();
            bool sawGame = false;

            void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != SceneNames.Game) return;
                sawGame = true;
                var generator = Object.FindAnyObjectByType<DungeonGenerator>();
                generator.Config = null;
                generator.Catalog = null;
            }

            LogAssert.Expect(LogType.Error, "[DungeonGenerator] No active DungeonGenerationConfigSO is available.");
            LogAssert.Expect(LogType.Error, "[GameBootstrap] Không thể tạo ngục: No active DungeonGenerationConfigSO is available.");
            SceneManager.sceneLoaded += OnLoaded;
            try
            {
                SceneLoader.LoadGame();
                yield return null;
                yield return WaitFor(() => sawGame && SceneManager.GetActiveScene().name == SceneNames.Lobby && FindPlayer() != null,
                    "Failed generation did not return to Lobby.");
                Assert.AreEqual(37, RunState.Coins);
                CollectionAssert.AreEqual(snapshot.Select(slot => slot.ItemId), RunState.InventorySlots.Select(slot => slot.ItemId));
                CollectionAssert.AreEqual(snapshot.Select(slot => slot.Count), RunState.InventorySlots.Select(slot => slot.Count));
            }
            finally
            {
                SceneManager.sceneLoaded -= OnLoaded;
            }
        }
    }
}
