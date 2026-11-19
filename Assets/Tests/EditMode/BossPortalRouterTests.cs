using LightSouls.MapGeneration;
using NUnit.Framework;
using UnityEditor;

namespace LightSouls.MapGeneration.Tests
{
    public class BossPortalRouterTests
    {
        [Test]
        public void HalfChanceReturnsToShopOrChoosesOnlyDifferentDungeon()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DungeonCatalogSO>("Assets/ScriptableObjects/MapGeneration/Config/DungeonCatalog.asset");
            Assert.NotNull(catalog);
            Assert.IsTrue(catalog.TryGetConfig("dungeon.standard", out var currentConfig));
            Assert.IsNull(BossPortalRouter.ChooseDestination(catalog, "dungeon.standard", currentConfig, 0.5f, 0.49f, 0f));

            var first = BossPortalRouter.ChooseDestination(catalog, "dungeon.standard", currentConfig, 0.5f, 0.5f, 0f);
            var last = BossPortalRouter.ChooseDestination(catalog, "dungeon.standard", currentConfig, 0.5f, 0.99f, 1f);
            Assert.AreEqual("dungeon.lava", first.dungeonId);
            Assert.AreEqual("dungeon.maze", last.dungeonId);
            Assert.AreNotEqual(currentConfig, first.config);
            Assert.AreNotEqual(currentConfig, last.config);

            Assert.IsNull(BossPortalRouter.ChooseDestination(catalog, "dungeon.standard", currentConfig, 1f, 1f, 0f));
            Assert.NotNull(BossPortalRouter.ChooseDestination(catalog, "dungeon.standard", currentConfig, 0f, 0f, 0f));
            Assert.IsNull(BossPortalRouter.ChooseDestination(null, "dungeon.standard", currentConfig, 0f, 0f, 0f));
        }

        [Test]
        public void DefaultDungeonConfigIsExcludedEvenWithoutSelectedId()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DungeonCatalogSO>("Assets/ScriptableObjects/MapGeneration/Config/DungeonCatalog.asset");
            Assert.IsTrue(catalog.TryGetConfig("dungeon.standard", out var currentConfig));
            var destination = BossPortalRouter.ChooseDestination(catalog, string.Empty, currentConfig, 0f, 0f, 0f);
            Assert.NotNull(destination);
            Assert.AreNotEqual("dungeon.standard", destination.dungeonId);
            Assert.AreNotEqual(currentConfig, destination.config);
        }
    }
}
