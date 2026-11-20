using System;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Items;
using LightSouls.Combat.Stats;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using LightSouls.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private const string ArsenalWeapons="Assets/ScriptableObjects/Combat/Weapons/Weapon_Arsenal_";
        private const string ArsenalItems="Assets/ScriptableObjects/Combat/Items/";
        private const string ArsenalMaterials="Assets/ScriptableObjects/Combat/Materials/Material_Arsenal_";
        private const string ArsenalEffects="Assets/ScriptableObjects/Combat/Effects/Arsenal_";
        private const string ItemDatabasePath=ArsenalItems+"ItemDatabase.asset";
        private const string ShopPath="Assets/ScriptableObjects/Lobby/ShopCatalog_Main.asset";

        private sealed class ArsenalWeaponSpec
        {
            public string Key,Name,Shape,Ingredient,Effects;
            public CombatVfxElement Element;
            public ItemRarity Rarity;
            public float Damage,Rate,Range,Arc,CastCd,CastDistance,CastDuration,Blast;
            public int Count,Pierce,Strikes;
            public WeaponCastKind Cast;
            public bool IsRanged=>Shape is "Staff" or "Crossbow" or "Orb" or "Lantern" or "Totem" or "Bow" or "Wand" or "Fan";
        }
        private static ArsenalWeaponSpec W(string key,string label,string shape,CombatVfxElement element,float damage,float rate,float range,string ingredient,
            string effects="",ItemRarity rarity=ItemRarity.Rare,float arc=105,WeaponCastKind cast=WeaponCastKind.None,float cd=8,float distance=3,float duration=3,
            int count=1,int pierce=0,int strikes=1,float blast=0)
            =>new ArsenalWeaponSpec{Key=key,Name=label,Shape=shape,Element=element,Damage=damage,Rate=rate,Range=range,Ingredient=ingredient,Effects=effects,Rarity=rarity,
                Arc=arc,Cast=cast,CastCd=cd,CastDistance=distance,CastDuration=duration,Count=count,Pierce=pierce,Strikes=strikes,Blast=blast};
        private static readonly ArsenalWeaponSpec[] NewWeapons={
            W("bastion_hammer","Búa Thành Lũy","Hammer",CombatVfxElement.Stone,37,1.42f,2.5f,"basalt_glass","push_heavy",ItemRarity.Epic,160),
            W("tempest_mace","Chùy Bão Tố","Mace",CombatVfxElement.Storm,27,1.14f,2.1f,"sky_copper","push chain",ItemRarity.Epic,135),
            W("glacier_greatsword","Đại Kiếm Băng Hà","GreatSword",CombatVfxElement.Frost,35,1.38f,2.8f,"rime_salt","freeze",ItemRarity.Epic,150),
            W("rime_dagger","Dao Sương Trắng","Dagger",CombatVfxElement.Frost,13,.56f,1.55f,"rime_salt","freeze_short",ItemRarity.Rare,75),
            W("mirage_sabre","Kiếm Cong Ảo Ảnh","Saber",CombatVfxElement.Void,22,.92f,2.25f,"moon_silver","",ItemRarity.Epic,110,WeaponCastKind.Blink,7.8f,3.2f),
            W("eclipse_scythe","Lưỡi Hái Nhật Thực","Scythe",CombatVfxElement.Void,29,1.29f,2.9f,"echo_bone","pull leech",ItemRarity.Epic,210),
            W("gravity_spear","Giáo Trọng Lực","Spear",CombatVfxElement.Void,25,1.03f,3.2f,"magnetite","pull",ItemRarity.Rare,62),
            W("obsidian_axe","Rìu Hắc Diệm","Axe",CombatVfxElement.Ember,34,1.32f,2.45f,"basalt_glass","expose burn",ItemRarity.Epic,125),
            W("thorn_sword","Kiếm Gai Linh Mộc","Sword",CombatVfxElement.Life,21,.86f,2.25f,"root_fiber","expose",ItemRarity.Rare,108),
            W("guardian_maul","Búa Hộ Thành","Hammer",CombatVfxElement.Stone,29,1.35f,2.4f,"magnetite","push",ItemRarity.Epic,145,WeaponCastKind.StoneWall,13f,2.35f,4.6f),
            W("frost_halberd","Kích Tường Băng","Halberd",CombatVfxElement.Frost,26,1.26f,3f,"rime_salt","slow",ItemRarity.Epic,132,WeaponCastKind.IceWall,14f,2.7f,4.8f),
            W("warden_flail","Liên Chùy Cai Ngục","Flail",CombatVfxElement.Stone,26,1.12f,2.6f,"sky_copper","push",ItemRarity.Rare,165),
            W("requiem_rapier","Rapier Khúc An Hồn","Rapier",CombatVfxElement.Void,18,.86f,2.35f,"dream_dust","charm",ItemRarity.Epic,72),
            W("twin_shade","Song Đao Bóng Ma","Twin",CombatVfxElement.Void,12,.99f,1.9f,"moon_silver","",ItemRarity.Legendary,105,WeaponCastKind.Blink,8.4f,4.1f,2,strikes:2),
            W("dawn_blade","Kiếm Bình Minh","Sword",CombatVfxElement.Life,24,1.05f,2.4f,"amber_resin","leech",ItemRarity.Rare,105,WeaponCastKind.Ward,10.5f,2.5f,2.6f),
            W("blood_fang","Nanh Huyết Quỷ","Dagger",CombatVfxElement.Blood,17,.72f,1.7f,"echo_bone","leech",ItemRarity.Rare,82),
            W("cinder_cleaver","Trảm Đao Than Hồng","Cleaver",CombatVfxElement.Ember,31,1.16f,2.4f,"amber_resin","burn",ItemRarity.Rare,135),
            W("thunder_halberd","Kích Lôi Đình","Halberd",CombatVfxElement.Storm,29,1.28f,3.1f,"sky_copper","chain",ItemRarity.Epic,120),
            W("frost_scepter","Trượng Hàn Ấn","Staff",CombatVfxElement.Frost,18,1.12f,10.5f,"rime_salt","freeze",ItemRarity.Rare),
            W("prism_crossbow","Nỏ Lăng Kính","Crossbow",CombatVfxElement.Frost,25,1.27f,13f,"moon_silver","slow",ItemRarity.Rare,pierce:1),
            W("gravity_orb","Cầu Hư Trọng","Orb",CombatVfxElement.Void,18,1.2f,10f,"magnetite","pull",ItemRarity.Epic),
            W("dream_scepter","Trượng Mộng Lệnh","Staff",CombatVfxElement.Void,15,1.28f,9.6f,"dream_dust","charm",ItemRarity.Epic),
            W("siren_lantern","Đèn Lồng Mê Hồn","Lantern",CombatVfxElement.Life,14,1.18f,8.7f,"abyss_pearl","charm_deep",ItemRarity.Legendary),
            W("earth_totem","Ấn Trụ Địa Thạch","Totem",CombatVfxElement.Stone,18,1.17f,9.3f,"basalt_glass","expose",ItemRarity.Epic,cast:WeaponCastKind.StoneWall,cd:14,distance:2.8f,duration:5.2f),
            W("ice_totem","Trụ Băng Vĩnh Cửu","Totem",CombatVfxElement.Frost,16,1.22f,9.6f,"abyss_pearl","freeze_short",ItemRarity.Legendary,cast:WeaponCastKind.IceWall,cd:15,distance:3,duration:5.8f),
            W("arcane_bow","Cung Sét Linh Văn","Bow",CombatVfxElement.Storm,23,1.16f,12f,"wind_silk","chain",ItemRarity.Epic,pierce:1),
            W("solar_wand","Đũa Phép Nhật Quang","Wand",CombatVfxElement.Life,17,.9f,9f,"living_moss","",ItemRarity.Rare,cast:WeaponCastKind.Ward,cd:11,duration:3),
            W("ember_orb","Cầu Lửa Lõi Sao","Orb",CombatVfxElement.Ember,30,1.68f,9f,"amber_resin","burn",ItemRarity.Epic,blast:2.35f),
            W("gravestone_crossbow","Nỏ Mộ Thạch","Crossbow",CombatVfxElement.Stone,27,1.32f,12f,"echo_bone","expose",ItemRarity.Rare,pierce:2),
            W("wind_fan","Phiến Vũ Cuồng Phong","Fan",CombatVfxElement.Storm,8,1.06f,8f,"wind_silk","push",ItemRarity.Rare,count:3)
        };

        private sealed class ArsenalMaterialSpec
        {
            public string Key,Name,Note,Region,Shape;
            public CombatVfxElement Element;public ItemRarity Rarity;public int Price;public float DropWeight;
        }
        private static ArsenalMaterialSpec M(string key,string name,string note,string region,string shape,CombatVfxElement element,ItemRarity rarity,int price,float weight)
            =>new ArsenalMaterialSpec{Key=key,Name=name,Note=note,Region=region,Shape=shape,Element=element,Rarity=rarity,Price=price,DropWeight=weight};
        private static readonly ArsenalMaterialSpec[] NewMaterials={
            M("basalt_glass","Hắc Kính","Thạch kính từ các vết nứt dung nham.","dungeon.lava","Shard",CombatVfxElement.Stone,ItemRarity.Rare,27,3.8f),
            M("rime_salt","Muối Hàn Sương","Tinh thể trắng ngậm luồng khí lạnh.","dungeon.maze","Salt",CombatVfxElement.Frost,ItemRarity.Rare,28,4),
            M("wind_silk","Tơ Gió","Sợi vải cổ nhẹ như lông vũ.","dungeon.standard","Silk",CombatVfxElement.Storm,ItemRarity.Common,11,7),
            M("moon_silver","Bạc Nguyệt","Kim loại ánh lam phản chiếu nguyệt quang.","dungeon.standard","Ingot",CombatVfxElement.Void,ItemRarity.Rare,35,3.5f),
            M("amber_resin","Nhựa Hổ Phách","Nhựa cây hóa thạch chứa hỏa linh.","dungeon.lava","Resin",CombatVfxElement.Ember,ItemRarity.Common,13,6),
            M("echo_bone","Xương Vọng Âm","Mảnh xương cộng hưởng cổ thuật.","dungeon.maze","Bone",CombatVfxElement.Blood,ItemRarity.Rare,24,4),
            M("magnetite","Đá Hút Linh","Quặng kỳ dị làm cong linh lực.","dungeon.lava","Ore",CombatVfxElement.Void,ItemRarity.Rare,30,3.5f),
            M("dream_dust","Bụi Mộng","Bụi linh hồn có ánh tím mộng mị.","dungeon.maze","Dust",CombatVfxElement.Void,ItemRarity.Epic,55,1.8f),
            M("root_fiber","Rễ Linh Mộc","Rễ cây bền mang khí sinh mệnh.","dungeon.standard","Root",CombatVfxElement.Life,ItemRarity.Common,10,7),
            M("living_moss","Rêu Hồi Sinh","Những cụm rêu phát quang trong mộ thủy triều.","dungeon.maze","Moss",CombatVfxElement.Life,ItemRarity.Common,11,6),
            M("sky_copper","Đồng Thiên Lôi","Kim loại giữ lại tiếng vang của giông bão.","dungeon.lava","Coil",CombatVfxElement.Storm,ItemRarity.Rare,29,4),
            M("abyss_pearl","Ngọc Vực Sâu","Ngọc sáng dưới đáy hầm ngục ngập nước.","dungeon.maze","Pearl",CombatVfxElement.Frost,ItemRarity.Epic,57,1.9f)
        };

        private sealed class ArsenalGearSpec
        {
            public string Key,Name,Ingredient;public bool Boots;public CombatVfxElement Element;public ItemRarity Rarity;
            public float Reduction,Speed;public StatModifier[] Mods;
        }
        private static ArsenalGearSpec G(string key,string label,string ingredient,bool boots,CombatVfxElement element,ItemRarity rarity,float reduction,float speed,params StatModifier[] mods)
            =>new ArsenalGearSpec{Key=key,Name=label,Ingredient=ingredient,Boots=boots,Element=element,Rarity=rarity,Reduction=reduction,Speed=speed,Mods=mods};
        private static StatModifier SM(StatType stat,float value)=>new StatModifier(stat,value);
        private static readonly ArsenalGearSpec[] NewGear={
            G("basalt_mail","Giáp Hắc Thạch","basalt_glass",false,CombatVfxElement.Stone,ItemRarity.Epic,.28f,.88f,SM(StatType.LavaDamage,.65f)),
            G("frost_cuirass","Giáp Hàn Ngọc","rime_salt",false,CombatVfxElement.Frost,ItemRarity.Rare,.19f,.94f,SM(StatType.WaterSlow,.6f)),
            G("duelist_coat","Áo Đấu Sĩ Nguyệt Bạc","moon_silver",false,CombatVfxElement.Void,ItemRarity.Epic,.10f,1.05f,SM(StatType.AttackCooldown,.87f)),
            G("ember_mantle","Áo Choàng Hỏa Linh","amber_resin",false,CombatVfxElement.Ember,ItemRarity.Rare,.14f,1f,SM(StatType.DamageDealt,1.12f)),
            G("iron_tread","Giày Thiết Bộ","sky_copper",true,CombatVfxElement.Stone,ItemRarity.Rare,.04f,.97f,SM(StatType.SpikeDamage,.45f)),
            G("windstep","Giày Phong Ảnh","wind_silk",true,CombatVfxElement.Storm,ItemRarity.Rare,0,1.14f,SM(StatType.DashCooldown,.82f)),
            G("tidewalk","Ủng Thủy Triều","abyss_pearl",true,CombatVfxElement.Frost,ItemRarity.Rare,0,1.01f,SM(StatType.WaterSlow,.15f),SM(StatType.DashDistance,1.12f)),
            G("gravity_boots","Giày Trọng Giới","magnetite",true,CombatVfxElement.Void,ItemRarity.Epic,.07f,.95f,SM(StatType.DashDistance,1.26f),SM(StatType.DashCooldown,1.12f))
        };

        private sealed class ArsenalPotionSpec
        {public string Key,Name,Ingredient;public CombatVfxElement Element;public ItemRarity Rarity;public float Heal,Duration,Cooldown;public StatModifier[] Mods;public bool Cleanse;}
        private static ArsenalPotionSpec P(string key,string name,string ingredient,CombatVfxElement element,ItemRarity rarity,float cooldown,float heal=0,float duration=0,bool cleanse=false,params StatModifier[] mods)
            =>new ArsenalPotionSpec{Key=key,Name=name,Ingredient=ingredient,Element=element,Rarity=rarity,Cooldown=cooldown,Heal=heal,Duration=duration,Cleanse=cleanse,Mods=mods};
        private static readonly ArsenalPotionSpec[] NewPotions={
            P("mending","Dược Hồi Phục","living_moss",CombatVfxElement.Life,ItemRarity.Common,7,heal:24),
            P("renewal","Tinh Dược Tái Sinh","abyss_pearl",CombatVfxElement.Life,ItemRarity.Rare,13,heal:42),
            P("steadfast","Dược Kiên Cường","basalt_glass",CombatVfxElement.Stone,ItemRarity.Rare,16,duration:8,mods:new[]{SM(StatType.DamageTaken,.68f)}),
            P("swift","Dược Phong Hành","wind_silk",CombatVfxElement.Storm,ItemRarity.Common,14,duration:9,mods:new[]{SM(StatType.MoveSpeed,1.24f),SM(StatType.DashCooldown,.85f)}),
            P("focus","Tinh Dược Tập Trung","moon_silver",CombatVfxElement.Void,ItemRarity.Rare,16,duration:7,mods:new[]{SM(StatType.AttackCooldown,.8f),SM(StatType.DamageDealt,.95f)}),
            P("courage","Dược Dũng Khí","amber_resin",CombatVfxElement.Ember,ItemRarity.Rare,17,duration:10,mods:new[]{SM(StatType.DamageDealt,1.24f),SM(StatType.DamageTaken,1.18f)}),
            P("cooling","Dược Kháng Nhiệt","rime_salt",CombatVfxElement.Frost,ItemRarity.Common,17,duration:12,mods:new[]{SM(StatType.LavaDamage,.35f)}),
            P("waterward","Dược Thủy Vệ","root_fiber",CombatVfxElement.Frost,ItemRarity.Common,15,duration:12,mods:new[]{SM(StatType.WaterSlow,.15f),SM(StatType.SpikeDamage,.75f)}),
            P("marrow","Dược Cốt Giáp","echo_bone",CombatVfxElement.Blood,ItemRarity.Rare,17,duration:10,mods:new[]{SM(StatType.ExplosionDamage,.52f),SM(StatType.DamageTaken,.86f)}),
            P("purity","Tinh Dược Thanh Tẩy","living_moss",CombatVfxElement.Life,ItemRarity.Common,10,cleanse:true)
        };

        [MenuItem("Light Souls/Art/Build Arsenal and Dungeon Journal")]
        public static void BuildArsenalExpansion()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("Arsenal icons need a graphics device.");
            string previous=SceneManager.GetActiveScene().path;
            LoadItemMeshes();LoadItemMaterials();
            var effects=CreateArsenalHitEffects();
            var created=new List<ItemDefinitionSO>();var materials=new List<CraftMaterialSO>();
            foreach(var spec in NewMaterials){var item=CreateArsenalMaterial(spec);materials.Add(item);created.Add(item);}
            foreach(var spec in NewWeapons){var item=CreateArsenalWeapon(spec,effects);created.Add(item);Debug.Log("ARSENAL_WEAPON_AUTHORED "+item.ItemId);}
            foreach(var spec in NewGear)created.Add(CreateArsenalGear(spec));
            foreach(var spec in NewPotions)created.Add(CreateArsenalPotion(spec));
            if(created.Count!=60||NewWeapons.Length!=30||NewMaterials.Length+NewGear.Length+NewPotions.Length!=30)throw new InvalidOperationException("Arsenal content count changed");
            RegisterArsenal(created,materials);
            TuneLegacyBalance();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            BuildItemIcons(false,new HashSet<string>(created.Select(i=>i.name)));
            BuildDungeonJournalIllustrations();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            Debug.Log("LIGHT_SOULS_ARSENAL_COMPLETE weapons=30 materials=12 gear=8 consumables=10");
        }

        private static T ArsenalAsset<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset;
        }
        private static T ExistingArsenalAsset<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset "+path);
        private static void SetItem(SerializedObject so,string id,string name,string description,int price,int stack,ItemRarity rarity)
        {
            so.FindProperty("itemId").stringValue=id;so.FindProperty("displayName").stringValue=name;so.FindProperty("description").stringValue=description;
            so.FindProperty("price").intValue=price;so.FindProperty("maxStack").intValue=stack;so.FindProperty("rarity").enumValueIndex=(int)rarity;
        }
        private static void Refs(SerializedProperty list,IEnumerable<UnityEngine.Object> objects)
        {var values=objects.ToArray();list.arraySize=values.Length;for(int i=0;i<values.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        private static void Modifiers(SerializedProperty list,IReadOnlyList<StatModifier> values)
        {list.arraySize=values.Count;for(int i=0;i<values.Count;i++){var x=list.GetArrayElementAtIndex(i);x.FindPropertyRelative("Stat").enumValueIndex=(int)values[i].Stat;x.FindPropertyRelative("Multiplier").floatValue=values[i].Multiplier;}}
        private static int Price(ItemRarity rarity,float delta=0)=>Mathf.RoundToInt((rarity switch{ItemRarity.Common=>110,ItemRarity.Rare=>210,ItemRarity.Epic=>350,_=>570})+delta);
        private static bool ArsenalId(ItemDefinitionSO item)=>item!=null&&item.ItemId.Contains(".arsenal_");

        private static Dictionary<string,WeaponHitEffectSO> CreateArsenalHitEffects()
        {
            var dict=new Dictionary<string,WeaponHitEffectSO>();
            void T(string id,TacticalHitKind kind,float mag,float dur,float cd,CombatVfxElement element)
            {
                var asset=ArsenalAsset<TacticalHitEffectSO>(ArsenalEffects+id+".asset");var so=new SerializedObject(asset);
                so.FindProperty("kind").enumValueIndex=(int)kind;so.FindProperty("magnitude").floatValue=mag;so.FindProperty("duration").floatValue=dur;so.FindProperty("cooldown").floatValue=cd;
                so.FindProperty("accent").colorValue=ArsenalColor(element);so.ApplyModifiedPropertiesWithoutUndo();dict[id]=asset;
            }
            T("push",TacticalHitKind.Push,2.35f,1,2.6f,CombatVfxElement.Stone); T("push_heavy",TacticalHitKind.Push,3.45f,1,3.8f,CombatVfxElement.Stone);
            T("freeze",TacticalHitKind.Freeze,1,1.5f,5f,CombatVfxElement.Frost);T("freeze_short",TacticalHitKind.Freeze,1,.95f,4.2f,CombatVfxElement.Frost);
            T("pull",TacticalHitKind.Pull,2.65f,1,3.6f,CombatVfxElement.Void);T("charm",TacticalHitKind.Charm,.45f,6f,15f,CombatVfxElement.Life);
            T("charm_deep",TacticalHitKind.Charm,.58f,7.5f,19f,CombatVfxElement.Life);T("chain",TacticalHitKind.Chain,10f,1,3.7f,CombatVfxElement.Storm);
            T("expose",TacticalHitKind.Expose,1.18f,4.2f,6f,CombatVfxElement.Ember);
            var burn=ArsenalAsset<BurnHitEffectSO>(ArsenalEffects+"burn.asset");var br=new SerializedObject(burn);br.FindProperty("damagePerSecond").floatValue=3.4f;br.FindProperty("duration").floatValue=2.6f;br.ApplyModifiedPropertiesWithoutUndo();dict["burn"]=burn;
            var leech=ArsenalAsset<LifestealHitEffectSO>(ArsenalEffects+"leech.asset");var ls=new SerializedObject(leech);ls.FindProperty("percent").floatValue=.08f;ls.ApplyModifiedPropertiesWithoutUndo();dict["leech"]=leech;
            var slow=ArsenalAsset<SlowHitEffectSO>(ArsenalEffects+"slow.asset");var sl=new SerializedObject(slow);sl.FindProperty("speedMultiplier").floatValue=.67f;sl.FindProperty("duration").floatValue=2.1f;sl.ApplyModifiedPropertiesWithoutUndo();dict["slow"]=slow;
            return dict;
        }

        private static WeaponDataSO CreateArsenalWeapon(ArsenalWeaponSpec spec,Dictionary<string,WeaponHitEffectSO> effects)
        {
            var item=ArsenalAsset<WeaponDataSO>(ArsenalWeapons+spec.Key+".asset");var so=new SerializedObject(item);
            bool ranged=spec.IsRanged;Color color=ArsenalColor(spec.Element);
            SetItem(so,"weapon.arsenal_"+spec.Key,spec.Name,(ranged?"Linh khí tầm xa":"Vũ khí cận chiến")+" thuộc kho vũ khí cổ. Mỗi đòn có nhịp hồi rõ ràng; kỹ năng chiến thuật không thể dùng liên tục bằng cách đổi vũ khí.",Price(spec.Rarity,spec.Damage),1,spec.Rarity);
            so.FindProperty("weaponType").enumValueIndex=(int)(ranged?WeaponType.Ranged:WeaponType.Melee);
            so.FindProperty("damage").floatValue=spec.Damage;so.FindProperty("attackRate").floatValue=spec.Rate;so.FindProperty("attackRange").floatValue=spec.Range;so.FindProperty("weaponColor").colorValue=color;
            so.FindProperty("swingArcAngle").floatValue=spec.Arc;so.FindProperty("hitRadius").floatValue=Mathf.Clamp(spec.Range*.68f,.8f,2.0f);so.FindProperty("knockbackForce").floatValue=1.5f;
            so.FindProperty("strikeCount").intValue=spec.Strikes;so.FindProperty("strikeInterval").floatValue=.19f;
            so.FindProperty("projectilePrefab").objectReferenceValue=ExistingArsenalAsset<GameObject>(CombatRoot+"/Weapon_Projectile.prefab");
            so.FindProperty("projectileSpeed").floatValue=spec.Blast>0?11f:spec.Shape=="Crossbow"?22f:16f;
            so.FindProperty("projectileLifetime").floatValue=2f;so.FindProperty("projectileCount").intValue=spec.Count;so.FindProperty("spreadAngle").floatValue=spec.Count>1?38f:0;
            so.FindProperty("pierceCount").intValue=spec.Pierce;so.FindProperty("autoFire").boolValue=false;so.FindProperty("inaccuracy").floatValue=0;
            so.FindProperty("explosionRadius").floatValue=spec.Blast;so.FindProperty("explosionDamagesOwner").boolValue=spec.Blast>0;so.FindProperty("arcHeight").floatValue=2.4f;
            so.FindProperty("tacticalCast").enumValueIndex=(int)spec.Cast;so.FindProperty("tacticalCooldown").floatValue=spec.CastCd;so.FindProperty("tacticalDistance").floatValue=spec.CastDistance;so.FindProperty("tacticalDuration").floatValue=spec.CastDuration;
            Refs(so.FindProperty("hitEffects"),spec.Effects.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(e=>effects[e]));
            so.FindProperty("vfx.element").enumValueIndex=(int)spec.Element;
            so.FindProperty("vfx.slashColor").colorValue=color;so.FindProperty("vfx.hitColor").colorValue=color;
            so.FindProperty("vfx.trailColor").colorValue=color;so.FindProperty("vfx.muzzleColor").colorValue=color;
            Color aura=color;aura.a=spec.Rarity>=ItemRarity.Epic?.7f:0f;so.FindProperty("vfx.auraColor").colorValue=aura;so.FindProperty("vfx.auraRate").floatValue=13f;
            so.FindProperty("vfx.groundShockwave").boolValue=spec.Shape is "Hammer" or "Mace" or "Axe";
            so.FindProperty("weaponPrefab").objectReferenceValue=BuildArsenalWeaponModel(spec,item.name);
            so.ApplyModifiedPropertiesWithoutUndo();return item;
        }

        private static CraftMaterialSO CreateArsenalMaterial(ArsenalMaterialSpec spec)
        {
            var asset=ArsenalAsset<CraftMaterialSO>(ArsenalMaterials+spec.Key+".asset");var so=new SerializedObject(asset);
            SetItem(so,"material.arsenal_"+spec.Key,spec.Name,spec.Note,spec.Price,999,spec.Rarity);
            bool shop=spec.Rarity==ItemRarity.Common;
            so.FindProperty("dropWeight").floatValue=spec.DropWeight;so.FindProperty("minDrop").intValue=1;so.FindProperty("maxDrop").intValue=shop?3:2;
            so.FindProperty("favoredDungeonId").stringValue=spec.Region;so.FindProperty("favoredMultiplier").floatValue=2.7f;so.FindProperty("bossOnly").boolValue=false;so.FindProperty("soldAtShop").boolValue=shop;
            so.FindProperty("sourceHint").stringValue="Rơi khi vượt phòng hoặc mở hòm. Ưu tiên "+(spec.Region=="dungeon.lava"?"Vực Dung Nham":spec.Region=="dungeon.maze"?"Mê Cung Thử Thách":"Ngục Tiêu Chuẩn")+(shop?"; có bán ở cửa hàng.":".");
            so.ApplyModifiedPropertiesWithoutUndo();return asset;
        }

        private static EquipmentDataSO CreateArsenalGear(ArsenalGearSpec spec)
        {
            var asset=ArsenalAsset<EquipmentDataSO>(ArsenalItems+(spec.Boots?"Boots":"Armor")+"_Arsenal_"+spec.Key+".asset");var so=new SerializedObject(asset);
            SetItem(so,(spec.Boots?"boots":"armor")+".arsenal_"+spec.Key,spec.Name,"Trang bị được tinh luyện từ linh vật cổ. Chọn đánh đổi tốc độ, sát thương và khả năng phòng hộ phù hợp từng ngục.",Price(spec.Rarity,25),1,spec.Rarity);
            so.FindProperty("slotType").enumValueIndex=(int)(spec.Boots?EquipmentSlotType.Boots:EquipmentSlotType.Armor);so.FindProperty("damageReduction").floatValue=spec.Reduction;
            so.FindProperty("speedMultiplier").floatValue=spec.Speed;so.FindProperty("bonusMaxHealth").floatValue=0f;Modifiers(so.FindProperty("extraModifiers"),spec.Mods);
            Color aura=ArsenalColor(spec.Element);aura.a=.62f;so.FindProperty("auraColor").colorValue=aura;so.ApplyModifiedPropertiesWithoutUndo();return asset;
        }

        private static ConsumableDataSO CreateArsenalPotion(ArsenalPotionSpec spec)
        {
            var item=ArsenalAsset<ConsumableDataSO>(ArsenalItems+"Consumable_Arsenal_"+spec.Key+".asset");var so=new SerializedObject(item);
            SetItem(so,"consumable.arsenal_"+spec.Key,spec.Name,"Dược phẩm của xưởng giả kim. Có thời gian hồi chung với mọi vật phẩm tiêu hao; dùng đúng thời điểm.",spec.Rarity==ItemRarity.Common?48:90,spec.Rarity==ItemRarity.Common?5:3,spec.Rarity);
            ConsumableEffectSO effect;
            string path=ArsenalEffects+"Potion_"+spec.Key+".asset";
            if(spec.Cleanse)effect=ArsenalAsset<CleanseEffectSO>(path);
            else if(spec.Heal>0)
            {
                var heal=ArsenalAsset<HealEffectSO>(path);var h=new SerializedObject(heal);h.FindProperty("flatAmount").floatValue=spec.Heal;h.FindProperty("percentOfMax").floatValue=0;h.FindProperty("color").colorValue=ArsenalColor(spec.Element);h.ApplyModifiedPropertiesWithoutUndo();effect=heal;
            }
            else
            {
                var buff=ArsenalAsset<StatBuffEffectSO>(path);var b=new SerializedObject(buff);Modifiers(b.FindProperty("modifiers"),spec.Mods);b.FindProperty("duration").floatValue=spec.Duration;b.FindProperty("auraColor").colorValue=ArsenalColor(spec.Element);b.ApplyModifiedPropertiesWithoutUndo();effect=buff;
            }
            Refs(so.FindProperty("effects"),new[]{effect});so.FindProperty("triggersOnDeath").boolValue=false;so.FindProperty("cooldown").floatValue=spec.Cooldown;
            so.ApplyModifiedPropertiesWithoutUndo();return item;
        }

        private static void RegisterArsenal(List<ItemDefinitionSO> created,List<CraftMaterialSO> materials)
        {
            var database=ExistingArsenalAsset<ItemDatabaseSO>(ItemDatabasePath);
            var all=database.AllItems.Where(i=>i!=null&&!ArsenalId(i)).Concat(created).ToList();
            var db=new SerializedObject(database);Refs(db.FindProperty("allItems"),all);db.ApplyModifiedPropertiesWithoutUndo();
            var map=all.ToDictionary(i=>i.ItemId);ItemDefinitionSO I(string id)=>map[id];
            var craft=ExistingArsenalAsset<CraftingCatalogSO>(CraftCatalogAsset);
            var materialList=craft.Materials.Where(m=>m!=null&&!ArsenalId(m)).Concat(materials).ToList();
            var recipes=craft.Recipes.Where(r=>r!=null&&!r.Id.StartsWith("arsenal.",StringComparison.Ordinal)).ToList();
            void AddRecipe(string key,CraftRecipeKind kind,string output,string note,params CraftIngredient[] cost)
            {
                var r=new CraftRecipe{Id="arsenal."+key,Kind=kind,Output=I(output),OutputCount=1,Note=note,Ingredients=cost.ToList()};
                if(!r.IsValid)throw new InvalidOperationException("Invalid arsenal recipe "+r.Id);recipes.Add(r);
            }
            CraftIngredient Cost(string id,int count)=>new CraftIngredient(I(id),count);
            CraftIngredient NewMat(string key,int count)=>Cost("material.arsenal_"+key,count);
            foreach(var spec in NewWeapons)
            {
                var costs=new List<CraftIngredient>{Cost(spec.IsRanged?"material.wood":"material.iron",spec.Rarity>=ItemRarity.Epic?4:3),NewMat(spec.Ingredient,spec.Rarity>=ItemRarity.Epic?3:2)};
                if(spec.Rarity>=ItemRarity.Epic)costs.Add(Cost("material.spirit_dust",2));
                if(spec.Rarity==ItemRarity.Legendary)costs.Add(Cost("material.boss_soul",1));
                AddRecipe(spec.Key,CraftRecipeKind.Forge,"weapon.arsenal_"+spec.Key,"Rèn linh khí với vật liệu riêng. Vũ khí huyền thoại cần bảo chứng boss.",costs.ToArray());
            }
            foreach(var spec in NewGear)AddRecipe(spec.Key,CraftRecipeKind.Forge,(spec.Boots?"boots":"armor")+".arsenal_"+spec.Key,"Tinh luyện trang bị phòng hộ với đánh đổi chỉ số.",Cost("material.leather",3),Cost("material.cloth",2),NewMat(spec.Ingredient,2));
            foreach(var spec in NewPotions)AddRecipe(spec.Key,CraftRecipeKind.Alchemy,"consumable.arsenal_"+spec.Key,"Giả kim dược phẩm hỗ trợ hành trình.",Cost("material.herb",2),Cost("material.glass",1),NewMat(spec.Ingredient,1));
            var cs=new SerializedObject(craft);Refs(cs.FindProperty("materials"),materialList);
            var list=cs.FindProperty("recipes");list.arraySize=recipes.Count;
            for(int i=0;i<recipes.Count;i++)
            {
                var source=recipes[i];var p=list.GetArrayElementAtIndex(i);p.FindPropertyRelative("Id").stringValue=source.Id;p.FindPropertyRelative("Kind").enumValueIndex=(int)source.Kind;
                p.FindPropertyRelative("Output").objectReferenceValue=source.Output;p.FindPropertyRelative("OutputCount").intValue=source.OutputCount;p.FindPropertyRelative("Note").stringValue=source.Note;
                var ingredients=p.FindPropertyRelative("Ingredients");ingredients.arraySize=source.Ingredients.Count;
                for(int j=0;j<source.Ingredients.Count;j++){var entry=ingredients.GetArrayElementAtIndex(j);entry.FindPropertyRelative("Item").objectReferenceValue=source.Ingredients[j].Item;entry.FindPropertyRelative("Count").intValue=source.Ingredients[j].Count;}
            }
            cs.ApplyModifiedPropertiesWithoutUndo();

            var shop=ExistingArsenalAsset<ShopCatalogSO>(ShopPath);var shopSo=new SerializedObject(shop);
            var sale=created.Where(i=>i is CraftMaterialSO m?m.SoldAtShop:i is ConsumableDataSO||i is EquipmentDataSO?i.Rarity<=ItemRarity.Rare:i.Rarity==ItemRarity.Rare);
            Refs(shopSo.FindProperty("items"),shop.Items.Where(i=>i!=null&&!ArsenalId(i)).Concat(sale));shopSo.ApplyModifiedPropertiesWithoutUndo();
            AppendArsenalLoot(ArsenalItems+"LootTable.asset",created,false);AppendArsenalLoot(ArsenalItems+"LootTable_SecretTreasury.asset",created,true);
        }

        private static void AppendArsenalLoot(string path,List<ItemDefinitionSO> created,bool secret)
        {
            var table=ExistingArsenalAsset<LootTableSO>(path);
            var entries=table.DropEntries.Where(e=>e.Item!=null&&!ArsenalId(e.Item)).ToList();
            foreach(var item in created)
            {
                if(item.Rarity==ItemRarity.Legendary||secret&&item.Rarity<ItemRarity.Rare)continue;
                var mat=item as CraftMaterialSO;
                float weight=secret?(item.Rarity==ItemRarity.Rare?2.9f:.85f):mat!=null?mat.DropWeight*.40f:item is ConsumableDataSO?(item.Rarity==ItemRarity.Common?3.7f:2.0f):item.Rarity==ItemRarity.Rare?.9f:.32f;
                entries.Add(new LootDropEntry{Item=item,MinCount=1,MaxCount=mat!=null?mat.MaxDrop:1,Weight=weight,FavoredDungeonId=mat!=null?mat.FavoredDungeonId:string.Empty,FavoredMultiplier=mat!=null?mat.FavoredMultiplier:1});
            }
            var so=new SerializedObject(table);var array=so.FindProperty("dropEntries");array.arraySize=entries.Count;
            for(int i=0;i<entries.Count;i++)
            {
                var e=entries[i];var v=array.GetArrayElementAtIndex(i);v.FindPropertyRelative("Item").objectReferenceValue=e.Item;v.FindPropertyRelative("MinCount").intValue=e.MinCount;
                v.FindPropertyRelative("MaxCount").intValue=e.MaxCount;v.FindPropertyRelative("Weight").floatValue=e.Weight;v.FindPropertyRelative("FavoredDungeonId").stringValue=e.FavoredDungeonId??string.Empty;v.FindPropertyRelative("FavoredMultiplier").floatValue=e.FavoredMultiplier;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TuneLegacyBalance()
        {
            var db=ExistingArsenalAsset<ItemDatabaseSO>(ItemDatabasePath);
            void Tune(string id,float damage,float rate,int price=0)
            {
                var item=db.GetItem("weapon."+id) as WeaponDataSO;var so=new SerializedObject(item);so.FindProperty("damage").floatValue=damage;so.FindProperty("attackRate").floatValue=rate;
                if(price>0)so.FindProperty("price").intValue=price;so.ApplyModifiedPropertiesWithoutUndo();
            }
            Tune("iron_sword",23,.69f);Tune("dagger",12,.46f);Tune("spear",25,.94f);Tune("greatsword",39,1.27f);
            Tune("fire_sword",26,.98f,380);Tune("pistol",16,.68f);Tune("shotgun",7,1.13f);Tune("crossbow",27,1.12f);
            Tune("smg",5,.23f);Tune("scythe",27,1.15f);Tune("sledgehammer",39,1.54f);Tune("twin_daggers",10,.87f);
            Tune("battle_axe",32,1.10f);Tune("grenade_launcher",37,1.82f,410);Tune("ice_staff",17,1.04f);
            void Potion(string id,float cooldown,float healing=0,string description=null)
            {var so=new SerializedObject(db.GetItem("consumable."+id));so.FindProperty("cooldown").floatValue=cooldown;if(healing>0)so.FindProperty("healAmount").floatValue=healing;if(description!=null)so.FindProperty("description").stringValue=description;so.ApplyModifiedPropertiesWithoutUndo();}
            Potion("health_potion",8,28,"Hồi 28 máu. Dược phẩm có thời gian hồi dùng chung.");Potion("large_potion",14,0,"Hồi 45% máu tối đa.");Potion("rage_potion",15,0,"Tăng 25% sát thương trong 10 giây.");Potion("hand_bomb",12);Potion("teleport_scroll",16);
            void Effect(string name,Action<SerializedObject> action){var asset=ExistingArsenalAsset<ScriptableObject>("Assets/ScriptableObjects/Combat/Effects/"+name+".asset");var so=new SerializedObject(asset);action(so);so.ApplyModifiedPropertiesWithoutUndo();}
            Effect("Effect_Heal_60Percent",so=>so.FindProperty("percentOfMax").floatValue=.45f);
            Effect("Effect_RageBuff",so=>{so.FindProperty("duration").floatValue=10;Modifiers(so.FindProperty("modifiers"),new[]{SM(StatType.DamageDealt,1.25f)});});
            Effect("Effect_ThrowBomb",so=>so.FindProperty("damage").floatValue=45);
            Effect("HitEffect_Burn",so=>{so.FindProperty("damagePerSecond").floatValue=4;so.FindProperty("duration").floatValue=2.7f;});
            Effect("HitEffect_Lifesteal",so=>so.FindProperty("percent").floatValue=.08f);Effect("HitEffect_Slow",so=>{so.FindProperty("speedMultiplier").floatValue=.68f;so.FindProperty("duration").floatValue=2;});
            Effect("HitEffect_Stun",so=>so.FindProperty("duration").floatValue=.9f);
            var leatherSo=new SerializedObject(db.GetItem("armor.leather_vest"));leatherSo.FindProperty("damageReduction").floatValue=.13f;leatherSo.ApplyModifiedPropertiesWithoutUndo();
            var bootsSo=new SerializedObject(db.GetItem("boots.swift_boots"));bootsSo.FindProperty("speedMultiplier").floatValue=1.18f;bootsSo.ApplyModifiedPropertiesWithoutUndo();
            EditPrefab(CombatRoot+"/Player_Character.prefab",root=>{
                var movement=new SerializedObject(root.GetComponent<PlayerMovement>());movement.FindProperty("dashCooldown").floatValue=1.85f;movement.FindProperty("moveSpeed").floatValue=5.0f;movement.ApplyModifiedPropertiesWithoutUndo();
                var health=new SerializedObject(root.GetComponent<HealthComponent>());health.FindProperty("invincibleAfterHit").boolValue=true;health.FindProperty("invincibilityDuration").floatValue=.25f;health.ApplyModifiedPropertiesWithoutUndo();
            });
        }
    }
}
