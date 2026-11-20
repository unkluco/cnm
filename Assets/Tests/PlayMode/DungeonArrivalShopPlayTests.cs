using System;
using System.Collections;
using System.IO;
using System.Linq;
using LightSouls.Art;
using LightSouls.Combat;
using LightSouls.Combat.UI;
using LightSouls.Common;
using LightSouls.Lobby;
using LightSouls.MapGeneration;
using LightSouls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LightSouls.Stability.Tests
{
    public class DungeonArrivalShopPlayTests
    {
        private static PlayerController Player() => Object.FindAnyObjectByType<PlayerController>();
        private static IEnumerator WaitFor(Func<bool> predicate, string message, float seconds = 30f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), message);
            yield return null;
        }
        private static IEnumerator StartRun()
        {
            SceneLoader.StartNewRun();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Lobby && Player() != null, "Lobby not ready.");
        }
        private static IEnumerator EnterDungeon(string id)
        {
            LobbyController.SelectDungeon(id);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Game && Player() != null &&
                Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated == true, "Dungeon not ready: " + id);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            RunState.Reset();
            yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);
        }

        [UnityTest]
        public IEnumerator SanctuaryKeepsAllLightsOnWhenThePlayerMoves()
        {
            yield return StartRun();
            var budget = Object.FindAnyObjectByType<DarkFantasyLightBudget>();
            Assert.NotNull(budget);
            Assert.IsTrue(budget.FixedSceneLighting);
            var lamps = DarkFantasyTorchLight.ActiveLights.Where(t => t != null && t.Source != null && t.gameObject.scene == budget.gameObject.scene).ToArray();
            Assert.GreaterOrEqual(lamps.Length, 5, "Lobby includes its four sconces, portal and forge.");
            float y = Player().transform.position.y;
            foreach (var point in new[] { new Vector3(0, y, 0), new Vector3(-7, y, -7), new Vector3(7, y, -7), new Vector3(7, y, 7), new Vector3(-7, y, 7) })
            {
                Player().Teleport(point);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.IsTrue(lamps.All(l => l.Source.enabled), "Lights changed with player position: " + point);
                Assert.AreEqual(lamps.Length, budget.ActiveLightCount);
                Assert.IsTrue(lamps.All(l => l.Source.shadows == LightShadows.None));
            }
            Player().Teleport(new Vector3(0, y, 0));
            yield return new WaitForSecondsRealtime(.35f);
            yield return Capture("01-lobby-stable-lighting.png");

            yield return EnterDungeon("dungeon.standard");
            budget = Object.FindAnyObjectByType<DarkFantasyLightBudget>();
            Assert.IsFalse(budget.FixedSceneLighting, "Large procedurally generated dungeons keep the mobile light budget.");
            budget.Refresh(Player().transform.position);
            Assert.That(budget.ActiveLightCount, Is.InRange(1, budget.MaximumActiveLights));
        }

        [UnityTest]
        public IEnumerator EachDungeonAnnouncesItsActualEntryWithNonBlockingTitleCard()
        {
            foreach (string id in new[] { "dungeon.standard", "dungeon.lava", "dungeon.maze" })
            {
                yield return StartRun();
                yield return EnterDungeon(id);
                var generator = Object.FindAnyObjectByType<DungeonGenerator>();
                Assert.IsTrue(generator.Catalog.TryGetEntry(id, out var entry));
                var title = Object.FindAnyObjectByType<DungeonArrivalBanner>();
                Assert.NotNull(title, "Every entry must show a title card, not just mystery portals.");
                Assert.AreEqual(id, title.DungeonId);
                Assert.AreEqual(entry.displayName, title.DungeonName);
                Assert.IsFalse(title.FromMysteryPortal);
                Assert.IsTrue(title.IsShowing);
                Assert.IsFalse(title.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.IsFalse(title.GetComponent<CanvasGroup>().interactable);
                Assert.IsNull(title.GetComponent<GraphicRaycaster>());
                Assert.IsFalse(BackButtonStack.HasOpenPanel);
                Assert.IsTrue(Object.FindAnyObjectByType<WeaponVendingMachine>().IsShopReady, id + " missing its supply shop.");
                yield return new WaitForSecondsRealtime(.55f);
                yield return Capture("02-arrival-" + id.Substring("dungeon.".Length) + ".png");
                if (id == "dungeon.standard")
                    yield return WaitFor(() => !title.gameObject.activeSelf, "Title card did not disappear on its own.", DungeonArrivalBanner.Duration + 1f);
            }
        }

        [UnityTest]
        public IEnumerator StartRoomMachineUsesSanctuaryShopAndRealPurchaseTransactions()
        {
            yield return StartRun();
            var catalog = Object.FindAnyObjectByType<LobbyShopUI>(FindObjectsInactive.Include).Catalog;
            RunState.AddCoins(1000);
            yield return EnterDungeon("dungeon.standard");
            var kiosk = Object.FindAnyObjectByType<WeaponVendingMachine>();
            Assert.NotNull(kiosk);
            Assert.IsTrue(kiosk.IsShopReady);
            var body = kiosk.transform.Find("BodyCabinet").GetComponent<BoxCollider>();
            Assert.NotNull(body);
            Assert.IsFalse(body.isTrigger, "Interaction must not remove the cabinet's physical body.");
            var trigger = kiosk.InteractionPoint.GetComponent<SphereCollider>();
            Assert.NotNull(trigger);
            Assert.IsTrue(trigger.isTrigger);
            Assert.AreEqual(LayerMask.NameToLayer("Interactable"), trigger.gameObject.layer);

            var generator = Object.FindAnyObjectByType<DungeonGenerator>();
            Assert.IsTrue(NavMesh.SamplePosition(generator.PlayerSpawnPoint.position, out var startOnNav, 2f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(kiosk.InteractionPoint.position, out var kioskOnNav, 1.5f, NavMesh.AllAreas));
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(startOnNav.position, kioskOnNav.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "The supply machine should be accessible on foot.");

            var player = Player();
            player.Teleport(kiosk.InteractionPoint.position);
            yield return WaitFor(() => ReferenceEquals(player.Interaction.CurrentInteractable, kiosk), "Machine cannot be reached through PlayerInteraction.");
            var arrival = Object.FindAnyObjectByType<DungeonArrivalBanner>();
            if (arrival != null)
                yield return WaitFor(() => !arrival.IsShowing, "Arrival card should clear before the kiosk preview.", DungeonArrivalBanner.Duration + 1f);
            player.InLockedCombat = true;
            Assert.IsFalse(kiosk.CanInteract(player.gameObject), "Do not allow shopping during a locked fight.");
            player.InLockedCombat = false;
            yield return WaitFor(() => ReferenceEquals(player.Interaction.CurrentInteractable, kiosk), "Kiosk should become available again outside combat.");
            yield return Capture("03-dungeon-supply-kiosk.png");
            player.Interaction.PerformInteract();
            yield return null;
            var shop = Object.FindAnyObjectByType<LobbyShopUI>();
            Assert.NotNull(shop);
            Assert.IsTrue(shop.IsOpen);
            Assert.AreSame(catalog, shop.Catalog);
            Assert.IsTrue(BackButtonStack.HasOpenPanel);
            Assert.IsFalse(kiosk.CanInteract(player.gameObject));
            var pause = Object.FindAnyObjectByType<PauseMenuController>();
            pause.Pause();
            Assert.IsFalse(pause.IsPaused, "Pause must not be opened over the supply shop.");
            var backpack = Object.FindAnyObjectByType<BackpackUI>(FindObjectsInactive.Include);
            backpack.Open();
            Assert.IsFalse(backpack.IsOpen);

            var blade = catalog.Items.First(i => i.Category == ItemCategory.Weapon && i.Price > 0);
            var material = catalog.Items.First(i => i.Category == ItemCategory.Material && i.Price > 0);
            var bag = player.Inventory;
            int coins = RunState.Coins;
            int blades = bag.GetCount(blade);
            shop.SelectItem(blade);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return Capture("04-dungeon-shop.png");
            var buy = shop.GetComponentsInChildren<Button>(true).Single(b => b.name == "BuyButton");
            Assert.IsTrue(buy.interactable);
            buy.onClick.Invoke();
            Assert.AreEqual(blades + 1, bag.GetCount(blade));
            Assert.AreEqual(coins - blade.Price, RunState.Coins);

            int space = bag.HotbarSlots.Concat(bag.BackpackSlots).Count(s => s.IsEmpty);
            Assert.IsTrue(bag.TryAdd(blade, space));
            coins = RunState.Coins;
            blades = bag.GetCount(blade);
            shop.BuySelected();
            Assert.AreEqual(coins, RunState.Coins, "Full bag must not spend any money.");
            Assert.AreEqual(blades, bag.GetCount(blade));

            shop.SelectItem(material);
            int materialCount = bag.GetCount(material);
            buy.onClick.Invoke();
            Assert.AreEqual(materialCount + 1, bag.GetCount(material));
            Assert.AreEqual(coins - material.Price, RunState.Coins, "Material pouch remains usable with a full gear bag.");
            Assert.IsTrue(RunState.TrySpendCoins(RunState.Coins));
            shop.BuySelected();
            Assert.AreEqual(materialCount + 1, bag.GetCount(material), "Insufficient coins must not give an item.");

            Assert.IsTrue(BackButtonStack.TryCloseTop());
            Assert.IsFalse(shop.IsOpen);
            yield return WaitFor(() => ReferenceEquals(player.Interaction.CurrentInteractable, kiosk), "Player cannot interact after closing the shop.");
            player.Interaction.PerformInteract();
            yield return null;
            Assert.IsTrue(shop.IsOpen);
            Assert.AreEqual(1, Object.FindObjectsByType<LobbyShopUI>(FindObjectsInactive.Include).Length, "Re-opening should not make duplicate shops.");
            shop.Close();
        }

        internal static IEnumerator Capture(string filename, string captureVariable = "LIGHTSOULS_ARRIVAL_CAPTURE_DIR")
        {
            string directory = Environment.GetEnvironmentVariable(captureVariable);
            if (string.IsNullOrWhiteSpace(directory) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) yield break;
            Directory.CreateDirectory(directory);
            var camera = Camera.main;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            target.Create();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var overlays = Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras = overlays.Select(c => c.worldCamera).ToArray();
            var oldPlanes = overlays.Select(c => c.planeDistance).ToArray();
            camera.targetTexture = target;
            foreach (var canvas in overlays)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .8f;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, filename), image.EncodeToPNG());
                Object.Destroy(image);
                Debug.Log("ARRIVAL_CAPTURE " + Path.Combine(directory, filename));
            }
            finally
            {
                for (int i = 0; i < overlays.Length; i++)
                {
                    overlays[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    overlays[i].worldCamera = oldCameras[i];
                    overlays[i].planeDistance = oldPlanes[i];
                }
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
