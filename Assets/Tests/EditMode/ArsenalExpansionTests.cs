using System.Linq;
using LightSouls.Combat.Items;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using LightSouls.Lobby;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class ArsenalExpansionTests
    {
        private static ItemDatabaseSO Database()=>AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/ScriptableObjects/Combat/Items/ItemDatabase.asset");

        [Test]
        public void ThirtyNewWeaponsAndThirtyOtherItemsAreAuthenticAndRegistered()
        {
            var db=Database();var all=db.AllItems.ToArray();
            Assert.AreEqual(all.Length,all.Select(i=>i.ItemId).Distinct().Count());
            Assert.AreEqual(45,all.OfType<WeaponDataSO>().Count());
            var added=all.Where(i=>i.ItemId.Contains(".arsenal_")).ToArray();
            Assert.AreEqual(60,added.Length);
            var weapons=added.OfType<WeaponDataSO>().ToArray();
            Assert.AreEqual(30,weapons.Length);Assert.AreEqual(18,weapons.Count(w=>w.Type==WeaponType.Melee));Assert.AreEqual(12,weapons.Count(w=>w.Type==WeaponType.Ranged));
            Assert.AreEqual(12,added.OfType<CraftMaterialSO>().Count());Assert.AreEqual(8,added.OfType<EquipmentDataSO>().Count());Assert.AreEqual(10,added.OfType<ConsumableDataSO>().Count());
            Assert.IsTrue(added.All(i=>i.Icon!=null&&AssetDatabase.GetAssetPath(i.Icon).Contains("/Icons/")));
            Assert.AreEqual(60,added.Select(i=>i.Icon).Distinct().Count());Assert.AreEqual(30,weapons.Select(w=>w.WeaponPrefab).Distinct().Count());
            foreach(var w in weapons)
            {
                Assert.NotNull(w.WeaponPrefab,w.ItemId);
                Assert.GreaterOrEqual(w.WeaponPrefab.GetComponentsInChildren<MeshRenderer>().Length,3,w.ItemId);
                Assert.IsEmpty(w.WeaponPrefab.GetComponentsInChildren<Collider>(true),w.ItemId);
                Assert.IsTrue(w.Type==WeaponType.Melee?w.WeaponPrefab.GetComponent<MeleeWeapon>()!=null:w.WeaponPrefab.GetComponent<RangedWeapon>()!=null,w.ItemId);
                if(w.Type==WeaponType.Ranged)Assert.NotNull(w.ProjectilePrefab,w.ItemId);
            }
        }

        [Test]
        public void ArsenalItemsHaveRealEconomySourcesAndLegendaryRequiresGuardianSoul()
        {
            var db=Database();var added=db.AllItems.Where(i=>i.ItemId.Contains(".arsenal_")).ToArray();
            var craft=CraftingCatalogSO.Load();var shop=AssetDatabase.LoadAssetAtPath<ShopCatalogSO>("Assets/ScriptableObjects/Lobby/ShopCatalog_Main.asset");
            var normal=AssetDatabase.LoadAssetAtPath<LootTableSO>("Assets/ScriptableObjects/Combat/Items/LootTable.asset");
            var treasury=AssetDatabase.LoadAssetAtPath<LootTableSO>("Assets/ScriptableObjects/Combat/Items/LootTable_SecretTreasury.asset");
            Assert.AreEqual(48,craft.Recipes.Count(r=>r.Id.StartsWith("arsenal.")));
            foreach(var item in added)
            {
                if(item is CraftMaterialSO material){Assert.IsTrue(craft.Materials.Contains(material));Assert.IsTrue(craft.Recipes.Any(r=>r.Ingredients.Any(i=>i.Item==material)));}
                else Assert.IsTrue(craft.Recipes.Any(r=>r.Output==item),item.ItemId);
                if(item.Rarity==ItemRarity.Legendary)
                {
                    Assert.IsFalse(shop.Items.Contains(item));Assert.IsFalse(normal.DropEntries.Any(e=>e.Item==item));Assert.IsFalse(treasury.DropEntries.Any(e=>e.Item==item));
                    Assert.IsTrue(craft.Recipes.Single(r=>r.Output==item).Ingredients.Any(i=>i.Item.ItemId=="material.boss_soul"));
                }
                else Assert.IsTrue(normal.DropEntries.Any(e=>e.Item==item&&e.Weight>0),item.ItemId);
            }
        }

        [Test]
        public void RecoveryAndDamageBudgetsAndAllSixTacticalFamiliesAreAuthored()
        {
            var weapons=Database().AllItems.OfType<WeaponDataSO>().ToArray();
            foreach(var weapon in weapons)
            {
                int count=weapon.Type==WeaponType.Melee?weapon.StrikeCount:weapon.ProjectileCount;
                Assert.GreaterOrEqual(weapon.AttackRate,.2f,weapon.ItemId);
                Assert.LessOrEqual(weapon.Damage*count/weapon.AttackRate,40f,weapon.ItemId+" nominal maximum sustained DPS");
                if(weapon.TacticalCast!=WeaponCastKind.None)Assert.Greater(weapon.TacticalCooldown,6f,weapon.ItemId);
                foreach(var effect in weapon.HitEffects.OfType<TacticalHitEffectSO>())Assert.GreaterOrEqual(effect.Cooldown,2.5f,weapon.ItemId);
            }
            foreach(WeaponCastKind kind in System.Enum.GetValues(typeof(WeaponCastKind)))
                if(kind!=WeaponCastKind.None)Assert.IsTrue(weapons.Any(w=>w.TacticalCast==kind),kind.ToString());
            foreach(TacticalHitKind kind in System.Enum.GetValues(typeof(TacticalHitKind)))
                Assert.IsTrue(weapons.SelectMany(w=>w.HitEffects).OfType<TacticalHitEffectSO>().Any(e=>e.Kind==kind),kind.ToString());
        }

        [Test]
        public void JournalHasThreeActualRegionIllustrationsAndIncreasingChallenge()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<DungeonCatalogSO>("Assets/ScriptableObjects/MapGeneration/Config/DungeonCatalog.asset");
            Assert.AreEqual(3,catalog.Entries.Count);Assert.AreEqual(3,catalog.Entries.Select(e=>e.icon).Distinct().Count());
            Assert.IsTrue(catalog.Entries.All(e=>e.icon!=null&&AssetDatabase.GetAssetPath(e.icon).Contains("/DungeonJournal/")));
            var a=DungeonChallenge.For("dungeon.standard");var b=DungeonChallenge.For("dungeon.lava");var c=DungeonChallenge.For("dungeon.maze");
            Assert.Less(a.EnemyHealth,b.EnemyHealth);Assert.Less(b.EnemyHealth,c.EnemyHealth);
            Assert.Greater(c.EnemyDamage,a.EnemyDamage);Assert.Greater(a.EnemyHealth,1f);
            Assert.Greater(c.Progression(3,7,12),c.Progression(1,1,0));Assert.LessOrEqual(c.Progression(50,50,50),1.55f);
            RunState.Reset();RunState.CarryHealthToNextDungeon(.42f);Assert.AreEqual(.42f,RunState.ConsumeDungeonHealth(),.001f);Assert.AreEqual(1f,RunState.ConsumeDungeonHealth());
        }
    }
}
