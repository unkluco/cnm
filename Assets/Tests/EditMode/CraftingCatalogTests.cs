using System.Collections.Generic;
using System.Linq;
using LightSouls.Art;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Items;
using LightSouls.Common;
using LightSouls.Lobby;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class CraftingCatalogTests
    {
        [Test]
        public void EveryMaterialHasAnIconSourceRecipeAndPersistentDatabaseEntry()
        {
            var catalog=CraftingCatalogSO.Load();Assert.NotNull(catalog);
            var database=AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/ScriptableObjects/Combat/Items/ItemDatabase.asset");
            var loot=AssetDatabase.LoadAssetAtPath<LootTableSO>("Assets/ScriptableObjects/Combat/Items/LootTable.asset");
            var shop=AssetDatabase.LoadAssetAtPath<ShopCatalogSO>("Assets/ScriptableObjects/Lobby/ShopCatalog_Main.asset");
            Assert.AreEqual(32,catalog.Materials.Count);
            Assert.AreEqual(32,catalog.Materials.Select(m=>m.ItemId).Distinct().Count());
            Assert.AreEqual(32,catalog.Materials.Select(m=>m.Icon).Distinct().Count());
            Assert.AreEqual(79,catalog.Recipes.Count);
            Assert.AreEqual(79,catalog.Recipes.Select(r=>r.Id).Distinct().Count());
            foreach(var m in catalog.Materials)
            {
                Assert.NotNull(m.Icon,m.name);Assert.AreEqual(ItemCategory.Material,m.Category);Assert.AreEqual(999,m.MaxStack);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.SourceHint),m.name);Assert.AreSame(m,database.GetItem(m.ItemId));
                Assert.IsTrue(catalog.Recipes.Any(r=>r.Ingredients.Any(i=>i.Item==m)),m.name+" is unused");
                Assert.AreEqual(m.SoldAtShop,shop.Items.Contains(m),m.name+" shop source mismatch");
                Assert.AreEqual(!m.BossOnly,loot.DropEntries.Any(d=>d.Item==m&&d.Weight>0),m.name+" chest source mismatch");
                if(!m.BossOnly){Assert.Greater(m.DropWeight,0,m.name);Assert.GreaterOrEqual(m.MaxDrop,m.MinDrop);}
                if(!string.IsNullOrEmpty(m.FavoredDungeonId))Assert.Greater(m.WeightFor(m.FavoredDungeonId),m.WeightFor("other"));
            }
            foreach(var r in catalog.Recipes)
            {
                Assert.IsTrue(r.IsValid,r.Id);Assert.AreSame(r.Output,database.GetItem(r.Output.ItemId));Assert.NotNull(r.Output.Icon);
                Assert.That(r.Ingredients.Count,Is.InRange(2,4));
                foreach(var ingredient in r.Ingredients)Assert.AreSame(ingredient.Item,database.GetItem(ingredient.Item.ItemId),r.Id);
            }
            foreach(var item in database.AllItems.Where(i=>i.Category!=ItemCategory.Material))Assert.IsTrue(catalog.Recipes.Any(r=>r.Output==item),item.ItemId);
        }

        [Test]
        public void SwordRecipesAndRefinementHaveTheIntendedGameCosts()
        {
            var catalog=CraftingCatalogSO.Load();Assert.NotNull(catalog);
            var sword=catalog.FindRecipe("forge.iron_sword");var fire=catalog.FindRecipe("upgrade.fire_sword");var steel=catalog.FindRecipe("refine.steel");
            Assert.AreEqual("weapon.iron_sword",sword.Output.ItemId);Assert.AreEqual(CraftRecipeKind.Forge,sword.Kind);
            CollectionAssert.AreEquivalent(new[]{"material.wood:2","material.iron:3"},sword.Ingredients.Select(i=>i.Item.ItemId+":"+i.Count));
            Assert.AreEqual("weapon.fire_sword",fire.Output.ItemId);Assert.AreEqual(CraftRecipeKind.Upgrade,fire.Kind);
            CollectionAssert.AreEquivalent(new[]{"weapon.iron_sword:1","material.fire_stone:1"},fire.Ingredients.Select(i=>i.Item.ItemId+":"+i.Count));
            Assert.AreEqual("material.steel",steel.Output.ItemId);Assert.AreEqual(2,steel.OutputCount);Assert.AreEqual(CraftRecipeKind.Refine,steel.Kind);
        }

        [Test]
        public void NormalRoomRewardsExcludeBossSoulAndEachGiantHasGuaranteedElementalLoot()
        {
            var catalog=CraftingCatalogSO.Load();Assert.NotNull(catalog);
            var previous=Random.state;
            try
            {
                Random.InitState(913);
                foreach(var dungeon in new[]{"dungeon.standard","dungeon.lava","dungeon.maze"})for(int i=0;i<80;i++)
                {
                    var drops=CraftingLootDrops.RollRoom(catalog,dungeon,null);
                    Assert.That(drops.Count,Is.InRange(1,2));Assert.IsTrue(drops.Any(d=>d.Item.Rarity==ItemRarity.Common));
                    Assert.IsTrue(drops.All(d=>d.Count>0&&d.Item is CraftMaterialSO m&&!m.BossOnly));
                }
                var expected=new Dictionary<BossKind,string>{{BossKind.Varkhan,"fire_stone"},{BossKind.Nhalis,"frost_crystal"},{BossKind.Orgrim,"steel"},{BossKind.Noctyr,"void_shard"},{BossKind.Aurex,"storm_shard"}};
                foreach(var pair in expected)
                {
                    var drops=CraftingLootDrops.RollRoom(catalog,"dungeon.standard",pair.Key);
                    Assert.AreEqual(1,drops.Single(d=>d.Item.ItemId=="material.boss_soul").Count);
                    Assert.That(drops.Single(d=>d.Item.ItemId=="material."+pair.Value).Count,Is.InRange(1,2));
                    Assert.IsTrue(drops.Any(d=>d.Item.Rarity==ItemRarity.Common));
                }
            }
            finally{Random.state=previous;}
        }

        [Test]
        public void ForgePrefabUsesInteractionAndSharedMobileLightBudget()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/Lobby_CraftingForge.prefab");Assert.NotNull(prefab);
            var station=prefab.GetComponent<CraftingStation>();Assert.NotNull(station);Assert.AreSame(CraftingCatalogSO.Load(),station.Catalog);
            Assert.AreEqual(LayerMask.NameToLayer("Interactable"),prefab.layer);Assert.IsTrue(prefab.GetComponent<BoxCollider>().isTrigger);
            Assert.AreEqual(1,prefab.GetComponentsInChildren<DarkFantasyTorchLight>().Length);
            Assert.IsTrue(prefab.GetComponentsInChildren<BoxCollider>().Any(c=>!c.isTrigger&&c.gameObject.layer==LayerMask.NameToLayer("Environment")));
            Assert.Greater(prefab.GetComponentsInChildren<MeshRenderer>().Length,12);
        }
    }
}
