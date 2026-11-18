using System;
using System.Collections.Generic;
using System.IO;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Items;
using LightSouls.Combat.Stats;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightSouls.Combat.Editor
{
    /// <summary>
    /// Tạo/cập nhật bộ vật phẩm mở rộng (20 vật phẩm), biểu tượng, hiệu ứng, độ hiếm của toàn bộ vật phẩm,
    /// vật liệu hiệu ứng hạt, cơ sở dữ liệu vật phẩm, bảng loot và danh mục cửa hàng.
    /// Chạy lại bao nhiêu lần cũng được: asset có sẵn được cập nhật tại chỗ (giữ GUID), không tạo trùng.
    ///
    /// Thêm vật phẩm mới theo cùng cách: vẽ icon trong ItemIconLibrary, thêm một khối Weapon/Equipment/Consumable
    /// ở BuildItems, rồi thêm vào bảng loot (BuildLootTable). Xem Docs/Items.md.
    /// </summary>
    public static class ItemSetBuilder
    {
        private const string SpritesRoot = "Assets/Sprites/Items";
        private const string WeaponsRoot = "Assets/ScriptableObjects/Combat/Weapons";
        private const string ItemsRoot = "Assets/ScriptableObjects/Combat/Items";
        private const string EffectsRoot = "Assets/ScriptableObjects/Combat/Effects";
        private const string VfxRoot = "Assets/Resources/VFX";
        private const string ShopCatalogPath = "Assets/ScriptableObjects/Lobby/ShopCatalog_Main.asset";
        private const string ProjectileGuid = "1da134f1b388c433b974c52a1c410881";

        private static readonly Color None = new Color(1f, 1f, 1f, 0f);

        [MenuItem("LightSouls/Items/Build Item Set")]
        public static void Build()
        {
            EnsureFolder(SpritesRoot);
            EnsureFolder(WeaponsRoot);
            EnsureFolder(ItemsRoot);
            EnsureFolder(EffectsRoot);
            EnsureFolder(VfxRoot);

            BuildVfxMaterial();

            var icons = new Dictionary<string, Sprite>();
            foreach (KeyValuePair<string, Action<ItemIconPainter>> pair in ItemIconLibrary.All)
            {
                icons[pair.Key] = ItemIconPainter.Save(SpritesRoot, pair.Key, pair.Value);
            }

            var items = new Dictionary<string, ItemDefinitionSO>();
            BuildItems(icons, items);
            UpdateExistingItems(items);
            UpdateDatabase(items);
            BuildLootTable(items);
            UpdateShop(items);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ItemSetBuilder] Đã tạo/cập nhật " + items.Count + " vật phẩm.");
        }

        #region Vật phẩm mới

        private static void BuildItems(Dictionary<string, Sprite> icons, Dictionary<string, ItemDefinitionSO> items)
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ProjectileGuid));

            // Hiệu ứng khi trúng đòn (dùng chung được giữa nhiều vũ khí)
            var burn = Effect<BurnHitEffectSO>("HitEffect_Burn", so => { F(so, "damagePerSecond", 6f); F(so, "duration", 3f); });
            var slow = Effect<SlowHitEffectSO>("HitEffect_Slow", so => { F(so, "speedMultiplier", 0.6f); F(so, "duration", 2f); });
            var stun = Effect<StunHitEffectSO>("HitEffect_Stun", so => F(so, "duration", 1f));
            var lifesteal = Effect<LifestealHitEffectSO>("HitEffect_Lifesteal", so => F(so, "percent", 0.05f));

            // Tác dụng vật phẩm tiêu hao
            var heal60 = Effect<HealEffectSO>("Effect_Heal_60Percent", so =>
            {
                F(so, "flatAmount", 0f); F(so, "percentOfMax", 0.6f);
                C(so, "color", new Color(0.4f, 1f, 0.5f)); B(so, "bigEffect", true);
            });
            var phoenixHeal = Effect<HealEffectSO>("Effect_Heal_Phoenix", so =>
            {
                F(so, "flatAmount", 0f); F(so, "percentOfMax", 0.3f);
                C(so, "color", new Color(1f, 0.6f, 0.1f)); B(so, "bigEffect", true);
            });
            var rage = Effect<StatBuffEffectSO>("Effect_RageBuff", so =>
            {
                Mods(so, "modifiers", new StatModifier(StatType.DamageDealt, 1.3f));
                F(so, "duration", 15f);
                C(so, "auraColor", new Color(1f, 0.2f, 0.15f));
            });
            var bomb = Effect<ThrowBombEffectSO>("Effect_ThrowBomb", so =>
            {
                F(so, "damage", 60f); F(so, "radius", 3f); F(so, "range", 7f); F(so, "flightTime", 1f); F(so, "arcHeight", 3f);
                B(so, "damagesUser", false);
            });
            var teleport = Effect<TeleportEffectSO>("Effect_TeleportHome", so => C(so, "color", new Color(0.55f, 0.4f, 1f)));

            // ---- Vũ khí cận chiến ----
            items["axe"] = Weapon("Weapon_BattleAxe", "weapon.battle_axe", "Rìu Chiến",
                "Rìu nặng chém rộng hình quạt. Chậm nhưng mỗi nhát đều đau.", icons["Icon_BattleAxe"], 220, ItemRarity.Common,
                WeaponType.Melee, 38f, 0.75f, 2.4f, new Color(0.62f, 0.62f, 0.68f), so =>
                {
                    F(so, "swingArcAngle", 150f); F(so, "hitRadius", 1.6f); F(so, "knockbackForce", 4f);
                    Vfx(so, slash: new Color(1f, 0.85f, 0.6f, 0.9f), hit: new Color(1f, 0.8f, 0.5f));
                });

            items["hammer"] = Weapon("Weapon_Sledgehammer", "weapon.sledgehammer", "Búa Tạ",
                "Nện xuống đất tạo sóng chấn động, làm choáng mọi kẻ địch xung quanh trong 1 giây.", icons["Icon_Sledgehammer"], 280, ItemRarity.Epic,
                WeaponType.Melee, 45f, 1.2f, 1.6f, new Color(0.4f, 0.42f, 0.48f), so =>
                {
                    F(so, "swingArcAngle", 360f); F(so, "hitRadius", 2.4f); F(so, "knockbackForce", 6f);
                    Effects(so, stun);
                    Vfx(so, slash: new Color(0.95f, 0.85f, 0.55f, 0.9f), aura: new Color(0.75f, 0.6f, 1f, 0.8f),
                        hit: new Color(1f, 0.95f, 0.5f), shockwave: true);
                });

            items["twin"] = Weapon("Weapon_TwinDaggers", "weapon.twin_daggers", "Song Dao",
                "Mỗi lần bấm chém liền hai nhát. Tầm rất ngắn, phải áp sát.", icons["Icon_TwinDaggers"], 160, ItemRarity.Common,
                WeaponType.Melee, 12f, 0.3f, 1.4f, new Color(0.7f, 0.95f, 0.85f), so =>
                {
                    F(so, "swingArcAngle", 90f); F(so, "hitRadius", 1.1f); F(so, "knockbackForce", 1f);
                    I(so, "strikeCount", 2); F(so, "strikeInterval", 0.1f);
                    Vfx(so, slash: new Color(0.7f, 1f, 0.9f, 0.9f), hit: new Color(0.75f, 1f, 0.9f));
                });

            items["scythe"] = Weapon("Weapon_Scythe", "weapon.scythe", "Lưỡi Hái",
                "Quét nửa vòng tròn trước mặt. Hút 5% sát thương gây ra thành máu.", icons["Icon_Scythe"], 300, ItemRarity.Epic,
                WeaponType.Melee, 30f, 0.7f, 2.8f, new Color(0.45f, 0.12f, 0.25f), so =>
                {
                    F(so, "swingArcAngle", 180f); F(so, "hitRadius", 1.8f); F(so, "knockbackForce", 2f);
                    Effects(so, lifesteal);
                    Vfx(so, slash: new Color(0.75f, 0.1f, 0.3f, 0.95f), aura: new Color(0.6f, 0.1f, 0.35f, 0.85f),
                        hit: new Color(0.9f, 0.15f, 0.3f));
                });

            items["firesword"] = Weapon("Weapon_FireSword", "weapon.fire_sword", "Kiếm Lửa",
                "Lưỡi kiếm rực lửa đốt cháy kẻ địch trong 3 giây. Hay gặp ở Vực Dung Nham.", icons["Icon_FireSword"], 320, ItemRarity.Legendary,
                WeaponType.Melee, 28f, 0.4f, 2.3f, new Color(1f, 0.4f, 0.1f), so =>
                {
                    F(so, "swingArcAngle", 110f); F(so, "hitRadius", 1.5f); F(so, "knockbackForce", 3f);
                    Effects(so, burn);
                    Vfx(so, slash: new Color(1f, 0.5f, 0.1f, 0.95f), aura: new Color(1f, 0.45f, 0.05f, 1f), auraRate: 30f,
                        hit: new Color(1f, 0.6f, 0.1f));
                });

            // ---- Vũ khí tầm xa ----
            items["crossbow"] = Weapon("Weapon_Crossbow", "weapon.crossbow", "Nỏ",
                "Mũi tên xuyên qua tối đa 2 kẻ địch. Bắn chậm nhưng mạnh.", icons["Icon_Crossbow"], 200, ItemRarity.Common,
                WeaponType.Ranged, 30f, 0.8f, 14f, new Color(0.55f, 0.38f, 0.2f), so =>
                {
                    O(so, "projectilePrefab", projectile); F(so, "projectileSpeed", 26f); F(so, "projectileLifetime", 2f);
                    I(so, "projectileCount", 1); F(so, "spreadAngle", 0f); I(so, "pierceCount", 2);
                    Vfx(so, trail: new Color(0.95f, 0.9f, 0.75f, 0.8f), muzzle: new Color(1f, 0.95f, 0.8f));
                });

            items["smg"] = Weapon("Weapon_SMG", "weapon.smg", "Súng Tiểu Liên",
                "Giữ nút tấn công để xả đạn liên tục. Mỗi viên yếu và hơi lệch.", icons["Icon_SMG"], 300, ItemRarity.Epic,
                WeaponType.Ranged, 7f, 0.09f, 11f, new Color(0.3f, 0.3f, 0.34f), so =>
                {
                    O(so, "projectilePrefab", projectile); F(so, "projectileSpeed", 24f); F(so, "projectileLifetime", 1.5f);
                    I(so, "projectileCount", 1); F(so, "spreadAngle", 0f); F(so, "inaccuracy", 6f); B(so, "autoFire", true);
                    Vfx(so, trail: new Color(1f, 0.85f, 0.3f, 0.7f), muzzle: new Color(1f, 0.8f, 0.3f), aura: new Color(0.8f, 0.5f, 1f, 0.5f), auraRate: 8f);
                });

            items["icestaff"] = Weapon("Weapon_IceStaff", "weapon.ice_staff", "Gậy Băng",
                "Bắn cầu băng làm chậm kẻ địch 40% trong 2 giây. Hay gặp ở Mê Cung Thử Thách.", icons["Icon_IceStaff"], 280, ItemRarity.Epic,
                WeaponType.Ranged, 16f, 0.55f, 12f, new Color(0.55f, 0.9f, 1f), so =>
                {
                    O(so, "projectilePrefab", projectile); F(so, "projectileSpeed", 14f); F(so, "projectileLifetime", 2.5f);
                    I(so, "projectileCount", 1); F(so, "spreadAngle", 0f);
                    Effects(so, slow);
                    Vfx(so, trail: new Color(0.5f, 0.9f, 1f, 0.9f), muzzle: new Color(0.6f, 0.95f, 1f),
                        aura: new Color(0.55f, 0.9f, 1f, 0.9f), auraRate: 18f, hit: new Color(0.7f, 0.95f, 1f));
                });

            items["launcher"] = Weapon("Weapon_GrenadeLauncher", "weapon.grenade_launcher", "Súng Phóng Lựu",
                "Bắn đạn nổ bay vòng cung, nổ lan rộng. Cẩn thận: đứng gần cũng bị thương!", icons["Icon_GrenadeLauncher"], 350, ItemRarity.Legendary,
                WeaponType.Ranged, 55f, 1.4f, 10f, new Color(0.36f, 0.44f, 0.24f), so =>
                {
                    O(so, "projectilePrefab", null); F(so, "projectileSpeed", 12f); F(so, "projectileLifetime", 3f);
                    I(so, "projectileCount", 1); F(so, "spreadAngle", 0f);
                    F(so, "explosionRadius", 3f); B(so, "explosionDamagesOwner", true); F(so, "arcHeight", 3f);
                    Vfx(so, muzzle: new Color(1f, 0.6f, 0.2f), aura: new Color(1f, 0.55f, 0.15f, 0.6f), auraRate: 10f,
                        hit: new Color(1f, 0.7f, 0.2f));
                });

            // ---- Giáp ----
            items["chainmail"] = Equipment("Armor_Chainmail", "armor.chainmail", "Áo Giáp Xích",
                "Giảm 20% sát thương nhận vào nhưng hơi nặng (chậm 5%).", icons["Icon_Chainmail"], 220, ItemRarity.Rare,
                EquipmentSlotType.Armor, 0.2f, 0.95f, None);
            items["heatarmor"] = Equipment("Armor_HeatArmor", "armor.heat_armor", "Giáp Chịu Nhiệt",
                "Miễn nhiễm dung nham và giảm một nửa sát thương nổ.", icons["Icon_HeatArmor"], 260, ItemRarity.Epic,
                EquipmentSlotType.Armor, 0.1f, 1f, new Color(1f, 0.45f, 0.1f, 0.9f),
                new StatModifier(StatType.LavaDamage, 0f), new StatModifier(StatType.ExplosionDamage, 0.5f));
            items["cloak"] = Equipment("Armor_ShadowCloak", "armor.shadow_cloak", "Áo Choàng Bóng Đêm",
                "Lướt hồi nhanh hơn 30%, giảm 5% sát thương nhận vào.", icons["Icon_ShadowCloak"], 240, ItemRarity.Epic,
                EquipmentSlotType.Armor, 0.05f, 1f, new Color(0.55f, 0.25f, 0.9f, 0.9f),
                new StatModifier(StatType.DashCooldown, 0.7f));

            // ---- Giày ----
            items["wading"] = Equipment("Boots_Wading", "boots.wading", "Ủng Lội Nước",
                "Lội nước không bị chậm.", icons["Icon_WadingBoots"], 140, ItemRarity.Common,
                EquipmentSlotType.Boots, 0f, 1f, None, new StatModifier(StatType.WaterSlow, 0f));
            items["spiked"] = Equipment("Boots_Spiked", "boots.spiked", "Giày Gai Sắt",
                "Đế sắt dày: bẫy gai không làm bị thương.", icons["Icon_SpikedBoots"], 160, ItemRarity.Rare,
                EquipmentSlotType.Boots, 0f, 1f, None, new StatModifier(StatType.SpikeDamage, 0f));
            items["spring"] = Equipment("Boots_Spring", "boots.spring", "Giày Lò Xo",
                "Lướt xa hơn 50%.", icons["Icon_SpringBoots"], 180, ItemRarity.Rare,
                EquipmentSlotType.Boots, 0f, 1f, new Color(1f, 0.9f, 0.3f, 0.8f), new StatModifier(StatType.DashDistance, 1.5f));

            // ---- Tiêu hao ----
            items["largepotion"] = Consumable("Consumable_LargePotion", "consumable.large_potion", "Bình Máu Lớn",
                "Hồi 60% máu tối đa.", icons["Icon_LargePotion"], 120, ItemRarity.Rare, 5, false, heal60);
            items["rage"] = Consumable("Consumable_RagePotion", "consumable.rage_potion", "Thuốc Cuồng Nộ",
                "Tăng 30% sát thương trong 15 giây.", icons["Icon_RagePotion"], 100, ItemRarity.Rare, 3, false, rage);
            items["bomb"] = Consumable("Consumable_HandBomb", "consumable.hand_bomb", "Bom Tay",
                "Ném về phía kẻ địch gần nhất, nổ sau 1 giây.", icons["Icon_HandBomb"], 80, ItemRarity.Common, 5, false, bomb);
            items["scroll"] = Consumable("Consumable_TeleportScroll", "consumable.teleport_scroll", "Cuộn Dịch Chuyển",
                "Dịch chuyển về phòng xuất phát. Không dùng được khi đang giao tranh.", icons["Icon_TeleportScroll"], 90, ItemRarity.Rare, 3, false, teleport);
            items["phoenix"] = Consumable("Consumable_PhoenixFeather", "consumable.phoenix_feather", "Lông Phượng Hoàng",
                "Khi gục ngã, tự hồi sinh với 30% máu (mất lông). Chỉ tìm thấy trong hòm kho báu.", icons["Icon_PhoenixFeather"], 500,
                ItemRarity.Legendary, 1, true, phoenixHeal);
        }

        private static WeaponDataSO Weapon(string asset, string id, string displayName, string description, Sprite icon, int price,
            ItemRarity rarity, WeaponType type, float damage, float rate, float range, Color color, Action<SerializedObject> extra)
        {
            var weapon = LoadOrCreate<WeaponDataSO>(WeaponsRoot + "/" + asset + ".asset");
            var so = new SerializedObject(weapon);
            Definition(so, id, displayName, description, icon, price, 1, rarity);
            E(so, "weaponType", (int)type);
            F(so, "damage", damage);
            F(so, "attackRate", rate);
            F(so, "attackRange", range);
            C(so, "weaponColor", color);
            extra(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static EquipmentDataSO Equipment(string asset, string id, string displayName, string description, Sprite icon, int price,
            ItemRarity rarity, EquipmentSlotType slot, float damageReduction, float speed, Color aura, params StatModifier[] extras)
        {
            var equipment = LoadOrCreate<EquipmentDataSO>(ItemsRoot + "/" + asset + ".asset");
            var so = new SerializedObject(equipment);
            Definition(so, id, displayName, description, icon, price, 1, rarity);
            E(so, "slotType", (int)slot);
            F(so, "damageReduction", damageReduction);
            F(so, "speedMultiplier", speed);
            F(so, "bonusMaxHealth", 0f);
            Mods(so, "extraModifiers", extras);
            C(so, "auraColor", aura);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(equipment);
            return equipment;
        }

        private static ConsumableDataSO Consumable(string asset, string id, string displayName, string description, Sprite icon, int price,
            ItemRarity rarity, int maxStack, bool onDeath, params ConsumableEffectSO[] effects)
        {
            var consumable = LoadOrCreate<ConsumableDataSO>(ItemsRoot + "/" + asset + ".asset");
            var so = new SerializedObject(consumable);
            Definition(so, id, displayName, description, icon, price, maxStack, rarity);
            ObjList(so, "effects", effects);
            B(so, "triggersOnDeath", onDeath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(consumable);
            return consumable;
        }

        private static void Definition(SerializedObject so, string id, string displayName, string description, Sprite icon,
            int price, int maxStack, ItemRarity rarity)
        {
            S(so, "itemId", id);
            S(so, "displayName", displayName);
            S(so, "description", description);
            O(so, "icon", icon);
            I(so, "price", price);
            I(so, "maxStack", maxStack);
            E(so, "rarity", (int)rarity);
        }

        private static T Effect<T>(string asset, Action<SerializedObject> setup) where T : ScriptableObject
        {
            var effect = LoadOrCreate<T>(EffectsRoot + "/" + asset + ".asset");
            var so = new SerializedObject(effect);
            setup(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static void Vfx(SerializedObject so, Color? slash = null, Color? aura = null, float auraRate = 14f, Color? hit = null,
            Color? trail = null, Color? muzzle = null, bool shockwave = false)
        {
            C(so, "vfx.slashColor", slash ?? new Color(1f, 1f, 1f, 0.9f));
            C(so, "vfx.auraColor", aura ?? None);
            F(so, "vfx.auraRate", auraRate);
            C(so, "vfx.hitColor", hit ?? new Color(1f, 0.95f, 0.8f));
            C(so, "vfx.trailColor", trail ?? None);
            C(so, "vfx.muzzleColor", muzzle ?? None);
            B(so, "vfx.groundShockwave", shockwave);
        }

        private static void Effects(SerializedObject so, params WeaponHitEffectSO[] effects)
        {
            ObjList(so, "hitEffects", effects);
        }

        #endregion

        #region Vật phẩm có sẵn: độ hiếm và hiệu ứng

        private static void UpdateExistingItems(Dictionary<string, ItemDefinitionSO> items)
        {
            Existing(items, "ironsword", WeaponsRoot + "/Weapon_Sword_Iron.asset", ItemRarity.Common,
                so => Vfx(so, slash: new Color(1f, 1f, 1f, 0.9f)));
            Existing(items, "dagger", WeaponsRoot + "/Weapon_Dagger.asset", ItemRarity.Common,
                so => Vfx(so, slash: new Color(0.85f, 1f, 0.9f, 0.9f)));
            Existing(items, "spear", WeaponsRoot + "/Weapon_Spear.asset", ItemRarity.Rare,
                so => Vfx(so, slash: new Color(0.6f, 0.85f, 1f, 0.9f), hit: new Color(0.7f, 0.9f, 1f)));
            Existing(items, "greatsword", WeaponsRoot + "/Weapon_Sword_Greatsword.asset", ItemRarity.Epic,
                so => Vfx(so, slash: new Color(0.85f, 0.7f, 1f, 0.95f), aura: new Color(0.7f, 0.45f, 1f, 0.8f), hit: new Color(0.85f, 0.7f, 1f), shockwave: true));
            Existing(items, "pistol", WeaponsRoot + "/Weapon_Gun_Pistol.asset", ItemRarity.Common,
                so => Vfx(so, trail: new Color(0.4f, 0.8f, 1f, 0.7f), muzzle: new Color(0.6f, 0.9f, 1f)));
            Existing(items, "shotgun", WeaponsRoot + "/Weapon_Gun_Shotgun.asset", ItemRarity.Rare,
                so => Vfx(so, trail: new Color(1f, 0.65f, 0.25f, 0.7f), muzzle: new Color(1f, 0.7f, 0.3f)));
            Existing(items, "leather", ItemsRoot + "/Armor_LeatherVest.asset", ItemRarity.Common, null);
            Existing(items, "swift", ItemsRoot + "/Boots_SwiftBoots.asset", ItemRarity.Rare,
                so => C(so, "auraColor", new Color(0.45f, 0.9f, 1f, 0.8f)));
            Existing(items, "potion", ItemsRoot + "/Consumable_HealthPotion.asset", ItemRarity.Common, null);
        }

        private static void Existing(Dictionary<string, ItemDefinitionSO> items, string key, string path, ItemRarity rarity,
            Action<SerializedObject> extra)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
            if (item == null)
            {
                Debug.LogWarning("[ItemSetBuilder] Không thấy " + path);
                return;
            }

            var so = new SerializedObject(item);
            E(so, "rarity", (int)rarity);
            if (extra != null) extra(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            items[key] = item;
        }

        #endregion

        #region Cơ sở dữ liệu, loot, cửa hàng

        private static void UpdateDatabase(Dictionary<string, ItemDefinitionSO> items)
        {
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(ItemsRoot + "/ItemDatabase.asset");
            if (database == null)
            {
                Debug.LogWarning("[ItemSetBuilder] Không thấy ItemDatabase.asset");
                return;
            }

            AppendUnique(new SerializedObject(database), "allItems", items.Values);
            EditorUtility.SetDirty(database);
        }

        private static void BuildLootTable(Dictionary<string, ItemDefinitionSO> items)
        {
            var table = AssetDatabase.LoadAssetAtPath<LootTableSO>(ItemsRoot + "/LootTable.asset");
            if (table == null)
            {
                Debug.LogWarning("[ItemSetBuilder] Không thấy LootTable.asset");
                return;
            }

            // Trọng số theo độ hiếm; vài món ưu tiên ngục riêng.
            var entries = new List<(string key, int min, int max, float weight, string dungeon, float mult)>
            {
                ("potion", 1, 3, 30f, null, 0f),
                ("largepotion", 1, 2, 10f, null, 0f),
                ("bomb", 1, 3, 12f, null, 0f),
                ("scroll", 1, 2, 8f, null, 0f),
                ("rage", 1, 1, 8f, null, 0f),
                ("phoenix", 1, 1, 1.5f, null, 0f),
                ("ironsword", 1, 1, 8f, null, 0f),
                ("dagger", 1, 1, 8f, null, 0f),
                ("twin", 1, 1, 8f, null, 0f),
                ("axe", 1, 1, 8f, null, 0f),
                ("pistol", 1, 1, 8f, null, 0f),
                ("crossbow", 1, 1, 8f, null, 0f),
                ("spear", 1, 1, 5f, null, 0f),
                ("shotgun", 1, 1, 5f, null, 0f),
                ("greatsword", 1, 1, 3f, null, 0f),
                ("hammer", 1, 1, 3f, null, 0f),
                ("scythe", 1, 1, 3f, null, 0f),
                ("smg", 1, 1, 3f, null, 0f),
                ("icestaff", 1, 1, 2.5f, "dungeon.maze", 4f),
                ("firesword", 1, 1, 1.2f, "dungeon.lava", 5f),
                ("launcher", 1, 1, 1.2f, null, 0f),
                ("leather", 1, 1, 8f, null, 0f),
                ("chainmail", 1, 1, 5f, null, 0f),
                ("heatarmor", 1, 1, 2.5f, "dungeon.lava", 4f),
                ("cloak", 1, 1, 2.5f, null, 0f),
                ("wading", 1, 1, 8f, "dungeon.standard", 2f),
                ("spiked", 1, 1, 5f, null, 0f),
                ("spring", 1, 1, 5f, null, 0f),
                ("swift", 1, 1, 5f, null, 0f),
            };

            var so = new SerializedObject(table);
            SerializedProperty list = so.FindProperty("dropEntries");
            list.ClearArray();
            foreach (var entry in entries)
            {
                ItemDefinitionSO item;
                if (!items.TryGetValue(entry.key, out item) || item == null) continue;
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                SerializedProperty element = list.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("Item").objectReferenceValue = item;
                element.FindPropertyRelative("MinCount").intValue = entry.min;
                element.FindPropertyRelative("MaxCount").intValue = entry.max;
                element.FindPropertyRelative("Weight").floatValue = entry.weight;
                element.FindPropertyRelative("FavoredDungeonId").stringValue = entry.dungeon ?? string.Empty;
                element.FindPropertyRelative("FavoredMultiplier").floatValue = entry.mult;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
        }

        private static void UpdateShop(Dictionary<string, ItemDefinitionSO> items)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ShopCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[ItemSetBuilder] Không thấy " + ShopCatalogPath);
                return;
            }

            // Lông Phượng Hoàng chỉ có trong hòm kho báu, không bán.
            var forSale = new List<ItemDefinitionSO>();
            string[] order = { "axe", "twin", "crossbow", "hammer", "scythe", "firesword", "smg", "icestaff", "launcher",
                "chainmail", "heatarmor", "cloak", "wading", "spiked", "spring", "largepotion", "rage", "bomb", "scroll" };
            foreach (string key in order)
            {
                ItemDefinitionSO item;
                if (items.TryGetValue(key, out item) && item != null) forSale.Add(item);
            }

            var so = new SerializedObject(catalog);
            AppendUnique(so, "items", forSale);
            RemoveFromList(so, "items", items.ContainsKey("phoenix") ? items["phoenix"] : null);
            EditorUtility.SetDirty(catalog);
        }

        #endregion

        #region Vật liệu hiệu ứng hạt

        private static void BuildVfxMaterial()
        {
            string texPath = VfxRoot + "/Tex_VFX_SoftDot.png";
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string matPath = VfxRoot + "/Mat_VFX_Additive.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[ItemSetBuilder] Không tìm thấy shader URP Particles/Unlit, dùng Sprites/Default.");
                shader = Shader.Find("Sprites/Default");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, matPath);
            }
            else
            {
                material.shader = shader;
            }

            // Trong suốt, pha cộng sáng (Additive), không ghi chiều sâu.
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
        }

        #endregion

        #region Tiện ích SerializedObject

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static SerializedProperty P(SerializedObject so, string name)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) throw new InvalidOperationException("Không có thuộc tính '" + name + "' trên " + so.targetObject.GetType().Name);
            return p;
        }

        private static void F(SerializedObject so, string name, float value) => P(so, name).floatValue = value;
        private static void I(SerializedObject so, string name, int value) => P(so, name).intValue = value;
        private static void E(SerializedObject so, string name, int value) => P(so, name).intValue = value;
        private static void B(SerializedObject so, string name, bool value) => P(so, name).boolValue = value;
        private static void S(SerializedObject so, string name, string value) => P(so, name).stringValue = value;
        private static void C(SerializedObject so, string name, Color value) => P(so, name).colorValue = value;
        private static void O(SerializedObject so, string name, Object value) => P(so, name).objectReferenceValue = value;

        private static void ObjList(SerializedObject so, string name, IList<Object> values)
        {
            SerializedProperty list = P(so, name);
            list.ClearArray();
            for (int i = 0; i < values.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void ObjList(SerializedObject so, string name, params ConsumableEffectSO[] values) => ObjList(so, name, (IList<Object>)values);
        private static void ObjList(SerializedObject so, string name, params WeaponHitEffectSO[] values) => ObjList(so, name, (IList<Object>)values);

        private static void Mods(SerializedObject so, string name, params StatModifier[] modifiers)
        {
            SerializedProperty list = P(so, name);
            list.ClearArray();
            for (int i = 0; i < modifiers.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Stat").intValue = (int)modifiers[i].Stat;
                element.FindPropertyRelative("Multiplier").floatValue = modifiers[i].Multiplier;
            }
        }

        private static void AppendUnique(SerializedObject so, string name, IEnumerable<ItemDefinitionSO> values)
        {
            SerializedProperty list = P(so, name);
            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++) existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (ItemDefinitionSO item in values)
            {
                if (item == null || existing.Contains(item)) continue;
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = item;
                existing.Add(item);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveFromList(SerializedObject so, string name, Object value)
        {
            if (value == null) return;
            SerializedProperty list = P(so, name);
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        #endregion
    }
}
