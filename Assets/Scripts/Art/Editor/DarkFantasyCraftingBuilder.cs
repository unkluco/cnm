using System;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat.Inventory;
using LightSouls.Combat.Items;
using LightSouls.Common;
using LightSouls.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private const string CraftMaterialFolder="Assets/ScriptableObjects/Combat/Materials";
        private const string CraftCatalogAsset="Assets/Resources/Combat/Crafting/CraftingCatalog.asset";
        private const string CraftForgePrefab="Assets/Prefabs/Lobby/Lobby_CraftingForge.prefab";

        [MenuItem("Light Souls/Art/Build Crafting Forge")]
        public static void BuildCraftingContent()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            string previous=SceneManager.GetActiveScene().path;
            var catalogue=AuthorCraftingCatalogue();
            RegisterCraftingItems(catalogue.Materials);
            AddStartingMaterials(catalogue);
            AssetDatabase.SaveAssets();
            LoadItemMeshes();LoadItemMaterials();
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("The forge's material icons need a graphics device.");
            BuildItemIcons(true);
            BuildCraftingForge(catalogue);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(!string.IsNullOrEmpty(previous)&&previous!="Assets/Scenes/Lobby.unity")EditorSceneManager.OpenScene(previous);
            Debug.Log($"LIGHT_SOULS_CRAFTING_COMPLETE materials={catalogue.Materials.Count} recipes={catalogue.Recipes.Count}");
        }

        private static CraftingCatalogSO AuthorCraftingCatalogue()
        {
            EnsureFolder(CraftMaterialFolder);EnsureFolder("Assets/Resources/Combat/Crafting");
            var materials=new List<CraftMaterialSO>();
            const string standard="dungeon.standard",lavaDungeon="dungeon.lava",maze="dungeon.maze";
            void M(string key,string id,string label,string description,ItemRarity rarity,int price,float weight,int lo,int hi,string favored=null,bool shop=false,bool bossOnly=false,string bonus=null)
            {
                string path=CraftMaterialFolder+"/Material_"+key+".asset";
                var asset=AssetDatabase.LoadAssetAtPath<CraftMaterialSO>(path);
                if(asset==null){asset=ScriptableObject.CreateInstance<CraftMaterialSO>();AssetDatabase.CreateAsset(asset,path);}
                var so=new SerializedObject(asset);
                so.FindProperty("itemId").stringValue="material."+id;so.FindProperty("displayName").stringValue=label;
                so.FindProperty("description").stringValue=description;so.FindProperty("price").intValue=price;
                so.FindProperty("maxStack").intValue=999;so.FindProperty("rarity").enumValueIndex=(int)rarity;
                so.FindProperty("dropWeight").floatValue=weight;so.FindProperty("minDrop").intValue=lo;so.FindProperty("maxDrop").intValue=hi;
                so.FindProperty("favoredDungeonId").stringValue=favored??string.Empty;so.FindProperty("favoredMultiplier").floatValue=3f;
                so.FindProperty("soldAtShop").boolValue=shop;so.FindProperty("bossOnly").boolValue=bossOnly;
                string source=bossOnly?"Hạ boss để nhận bảo chứng linh hồn.":"Thưởng vượt phòng và hòm trong ngục.";
                if(favored==standard)source+=" Dễ gặp ở Ngục Tiêu Chuẩn.";
                if(favored==lavaDungeon)source+=" Dễ gặp ở Vực Dung Nham.";
                if(favored==maze)source+=" Dễ gặp ở Mê Cung Thử Thách.";
                if(shop)source+=" Có bán tại cửa hàng sảnh.";
                if(!string.IsNullOrEmpty(bonus))source+=" "+bonus;
                so.FindProperty("sourceHint").stringValue=source;so.ApplyModifiedPropertiesWithoutUndo();materials.Add(asset);
            }
            var common=ItemRarity.Common;var rare=ItemRarity.Rare;var epic=ItemRarity.Epic;
            M("Wood","wood","Gỗ cổ","Những thớ gỗ còn giữ dấu tích của thành cổ.",common,6,14,2,4,standard,true);
            M("Iron","iron","Quặng sắt","Mảnh kim loại đen thuộc thời đại đã mất.",common,9,14,2,4,null,true);
            M("Stone","stone","Đá đen","Đá núi có kết cấu rắn và sắc cạnh.",common,5,11,2,4,null,true);
            M("Coal","coal","Than hồng","Than giữ trong mình chút hơi ấm cuối cùng.",common,7,9,2,3,lavaDungeon,true);
            M("Copper","copper","Đồng cổ","Kim loại màu hổ phách từ kho báu bỏ quên.",common,10,8,1,3,null,true);
            M("Leather","leather","Da thuộc","Mảnh da bền dùng cho đồ phòng hộ.",common,9,11,2,3,null,true);
            M("Cloth","cloth","Vải linh văn","Vải nhuốm màu đêm, lưu giữ linh văn cổ.",common,7,10,2,3,maze,true);
            M("Herb","herb","Thảo dược","Lá thuốc mọc quanh những vết nứt đá.",common,6,12,2,4,standard,true);
            M("Glass","glass","Mảnh thủy tinh","Thủy tinh để lưu giữ tinh chất phép thuật.",common,7,9,1,3,null,true);
            M("Bone","bone","Xương cổ","Mảnh di cốt chứa tiếng vọng linh hồn.",common,9,8,1,3,maze);
            M("Steel","steel","Thép tinh luyện","Kim loại hiếm thuộc giáp của các thủ vệ.",rare,25,4,1,2,null,false,false,"Orgrim để lại thép; cũng có thể tinh luyện từ quặng.");
            M("FireStone","fire_stone","Đá lửa","Tinh thạch đang cháy với ngọn lửa bất diệt.",rare,35,3,1,2,lavaDungeon,false,false,"Varkhan luôn để lại đá lửa.");
            M("FrostCrystal","frost_crystal","Tinh thạch băng","Mảnh băng không tan, tỏa ánh sáng lam.",rare,35,3,1,2,standard,false,false,"Nhalis luôn để lại tinh thạch băng.");
            M("StormShard","storm_shard","Mảnh lôi điện","Tinh thể ánh kim lưu giữ sấm chớp.",rare,38,3,1,2,maze,false,false,"Aurex luôn để lại mảnh lôi điện.");
            M("VoidShard","void_shard","Tinh thể hư không","Mảnh vỡ màu tím từ không gian bên kia.",epic,55,1.7f,1,2,maze,false,false,"Noctyr luôn để lại tinh thể hư không.");
            M("WaterPearl","water_pearl","Ngọc thủy triều","Ngọc xanh trong chứa hơi thở nước sâu.",rare,30,4,1,2,standard);
            M("VenomPetal","venom_petal","Cánh hoa độc","Thực vật kỳ dị mang năng lượng phép độc.",rare,28,4,1,2,maze);
            M("SpiritDust","spirit_dust","Bụi linh hồn","Hạt sáng lơ lửng từ linh hồn lạc mất.",rare,32,4,1,2,maze,false,false,"Có thể tinh luyện từ xương cổ và thủy tinh.");
            M("SunEssence","sun_essence","Tinh hoa mặt trời","Tàn tích ánh sáng của vương quốc cổ.",epic,65,1.4f,1,1);
            M("BossSoul","boss_soul","Linh hồn thủ vệ","Bảo chứng huyền thoại của người đã vượt qua thủ vệ khổng lồ.",ItemRarity.Legendary,150,0,1,1,null,false,true);

            var items=AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/ScriptableObjects/Combat"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i=>i!=null&&!string.IsNullOrEmpty(i.ItemId)).ToDictionary(i=>i.ItemId);
            var recipes=new List<CraftRecipe>();
            ItemDefinitionSO Item(string id)=>items.TryGetValue(id,out var item)?item:throw new InvalidOperationException("Missing crafting item: "+id);
            void R(string id,CraftRecipeKind kind,string output,int count,string note,params (string id,int count)[] ingredients)
            {
                var recipe=new CraftRecipe{Id=id,Kind=kind,Output=Item(output),OutputCount=count,Note=note};
                foreach(var entry in ingredients)recipe.Ingredients.Add(new CraftIngredient(Item(entry.id.Contains('.')?entry.id:"material."+entry.id),entry.count));
                if(!recipe.IsValid)throw new InvalidOperationException("Invalid crafting recipe: "+id);
                recipes.Add(recipe);
            }
            var forge=CraftRecipeKind.Forge;var upgrade=CraftRecipeKind.Upgrade;var alchemy=CraftRecipeKind.Alchemy;var refine=CraftRecipeKind.Refine;
            // These are game-economy costs, not simulation of real manufacturing.
            R("forge.iron_sword",forge,"weapon.iron_sword",1,"Thanh kiếm cơ bản, cân bằng giữa tốc độ và sát thương.",("wood",2),("iron",3));
            R("upgrade.fire_sword",upgrade,"weapon.fire_sword",1,"Thức tỉnh ngọn lửa trên Kiếm Sắt. Thanh kiếm cũ trở thành Kiếm Lửa.",("weapon.iron_sword",1),("fire_stone",1));
            R("forge.dagger",forge,"weapon.dagger",1,"Nhẹ, linh hoạt và có nhịp đánh nhanh.",("wood",1),("iron",2),("leather",1));
            R("forge.spear",forge,"weapon.spear",1,"Vũ khí cổ có tầm đánh xa.",("wood",3),("iron",2));
            R("upgrade.greatsword",upgrade,"weapon.greatsword",1,"Biến Kiếm Sắt thành đại kiếm nặng của kỵ sĩ.",("weapon.iron_sword",1),("steel",3),("copper",1));
            R("forge.battle_axe",forge,"weapon.battle_axe",1,"Lưỡi rìu của chiến binh phá giáp.",("wood",2),("steel",2),("stone",1));
            R("forge.sledgehammer",forge,"weapon.sledgehammer",1,"Búa chiến có nhịp đánh chậm và uy lực lớn.",("wood",2),("iron",3),("stone",2));
            R("upgrade.twin_daggers",upgrade,"weapon.twin_daggers",1,"Hợp nhất hai dao găm thành cặp vũ khí linh hoạt.",("weapon.dagger",2),("storm_shard",1),("steel",1));
            R("forge.crossbow",forge,"weapon.crossbow",1,"Nỏ cổ có thể đánh từ khoảng cách an toàn.",("wood",3),("cloth",2),("steel",2));
            R("forge.ice_staff",forge,"weapon.ice_staff",1,"Quyền trượng băng của pháp sư, mang sức mạnh giá lạnh.",("wood",2),("frost_crystal",2),("spirit_dust",1));
            R("upgrade.scythe",upgrade,"weapon.scythe",1,"Linh hồn hư không biến ngọn giáo thành lưỡi hái.",("weapon.spear",1),("void_shard",2),("bone",2));
            R("forge.pistol",forge,"weapon.pistol",1,"Vật phẩm tầm xa cổ được triệu hồi bằng linh văn sấm sét.",("spirit_dust",2),("storm_shard",1),("sun_essence",1));
            R("upgrade.shotgun",upgrade,"weapon.shotgun",1,"Biến đổi trang bị tầm xa bằng phép lôi điện.",("weapon.pistol",1),("storm_shard",2),("spirit_dust",2));
            R("upgrade.smg",upgrade,"weapon.smg",1,"Nâng cấp trang bị tầm xa bằng tinh thể hư không.",("weapon.pistol",1),("void_shard",2),("storm_shard",1));
            R("upgrade.grenade_launcher",upgrade,"weapon.grenade_launcher",1,"Dùng linh hồn thủ vệ để mở khóa trang bị huyền thoại.",("weapon.pistol",1),("boss_soul",1),("sun_essence",1));
            R("forge.leather_vest",forge,"armor.leather_vest",1,"Giáp da phòng hộ cơ bản, dùng cho hành trình đầu tiên.",("leather",3),("cloth",2));
            R("upgrade.chainmail",upgrade,"armor.chainmail",1,"Nâng giáp da thành một tầng bảo hộ vững chắc hơn.",("armor.leather_vest",1),("steel",3),("copper",1));
            R("upgrade.heat_armor",upgrade,"armor.heat_armor",1,"Giáp chống nóng mang dấu ấn của thủ vệ dung nham.",("armor.chainmail",1),("fire_stone",2),("boss_soul",1));
            R("forge.shadow_cloak",forge,"armor.shadow_cloak",1,"Áo choàng bóng tối cho những bước đi trong màn đêm.",("cloth",4),("void_shard",2),("spirit_dust",2));
            R("forge.wading_boots",forge,"boots.wading",1,"Đôi giày giúp vượt qua địa hình ngập nước dễ hơn.",("leather",2),("water_pearl",1),("cloth",1));
            R("forge.swift_boots",forge,"boots.swift_boots",1,"Đôi giày mang dấu ấn của gió và lôi điện.",("leather",2),("storm_shard",1),("cloth",1));
            R("upgrade.spring_boots",upgrade,"boots.spring",1,"Nâng cấp giày nhanh thành đôi giày linh hoạt vượt địa hình.",("boots.swift_boots",1),("copper",2),("spirit_dust",2));
            R("forge.spiked_boots",forge,"boots.spiked",1,"Đôi giày có chức năng bảo hộ trên địa hình nguy hiểm.",("leather",2),("iron",2),("stone",1));
            R("alchemy.health_potion",alchemy,"consumable.health_potion",1,"Bình thuốc phục hồi sinh lực cơ bản.",("herb",2),("glass",1));
            R("alchemy.large_potion",alchemy,"consumable.large_potion",1,"Tinh chế hai bình nhỏ thành một bình phục hồi lớn.",("consumable.health_potion",2),("sun_essence",1));
            R("alchemy.rage_potion",alchemy,"consumable.rage_potion",1,"Dược phẩm phép thuật tăng sức mạnh tạm thời.",("herb",1),("fire_stone",1),("venom_petal",1),("glass",1));
            R("alchemy.teleport_scroll",alchemy,"consumable.teleport_scroll",1,"Cuộn giấy mở lối trở về sảnh.",("cloth",1),("spirit_dust",2),("void_shard",1));
            R("alchemy.phoenix_feather",alchemy,"consumable.phoenix_feather",1,"Lông vũ huyền thoại mang phước lành phục sinh.",("boss_soul",1),("sun_essence",2),("spirit_dust",2));
            R("alchemy.hand_bomb",alchemy,"consumable.hand_bomb",1,"Vật phẩm phép thuật phóng thích ánh sáng linh hồn.",("spirit_dust",1),("storm_shard",1),("sun_essence",1));
            R("refine.steel",refine,"material.steel",2,"Đổi nguyên liệu thô lấy hai thép tinh luyện.",("iron",3),("coal",1));
            R("refine.spirit_dust",refine,"material.spirit_dust",1,"Thu hồi bụi linh hồn từ dấu tích xưa.",("bone",2),("glass",1));

            var catalog=AssetDatabase.LoadAssetAtPath<CraftingCatalogSO>(CraftCatalogAsset);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<CraftingCatalogSO>();AssetDatabase.CreateAsset(catalog,CraftCatalogAsset);}
            var serialized=new SerializedObject(catalog);var ml=serialized.FindProperty("materials");ml.arraySize=materials.Count;
            for(int i=0;i<materials.Count;i++)ml.GetArrayElementAtIndex(i).objectReferenceValue=materials[i];
            var rl=serialized.FindProperty("recipes");rl.arraySize=recipes.Count;
            for(int i=0;i<recipes.Count;i++)
            {
                var source=recipes[i];var entry=rl.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Id").stringValue=source.Id;entry.FindPropertyRelative("Kind").enumValueIndex=(int)source.Kind;
                entry.FindPropertyRelative("Output").objectReferenceValue=source.Output;entry.FindPropertyRelative("OutputCount").intValue=source.OutputCount;entry.FindPropertyRelative("Note").stringValue=source.Note;
                var il=entry.FindPropertyRelative("Ingredients");il.arraySize=source.Ingredients.Count;
                for(int j=0;j<il.arraySize;j++)
                {var ingredient=il.GetArrayElementAtIndex(j);ingredient.FindPropertyRelative("Item").objectReferenceValue=source.Ingredients[j].Item;ingredient.FindPropertyRelative("Count").intValue=source.Ingredients[j].Count;}
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();return catalog;
        }

        private static void AppendCraftingReferences(SerializedProperty list,IEnumerable<CraftMaterialSO> items)
        {
            foreach(var item in items)
            {
                bool present=false;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==item){present=true;break;}
                if(!present)list.GetArrayElementAtIndex(list.arraySize++).objectReferenceValue=item;
            }
        }

        private static void RegisterCraftingItems(IReadOnlyList<CraftMaterialSO> materials)
        {
            var db=new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/ScriptableObjects/Combat/Items/ItemDatabase.asset"));
            AppendCraftingReferences(db.FindProperty("allItems"),materials);db.ApplyModifiedPropertiesWithoutUndo();
            var loot=new SerializedObject(AssetDatabase.LoadAssetAtPath<LootTableSO>("Assets/ScriptableObjects/Combat/Items/LootTable.asset"));
            var entries=loot.FindProperty("dropEntries");
            foreach(var item in materials.Where(m=>!m.BossOnly))
            {
                int index=-1;for(int i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue==item){index=i;break;}
                if(index<0){index=entries.arraySize;entries.arraySize++;}
                var entry=entries.GetArrayElementAtIndex(index);entry.FindPropertyRelative("Item").objectReferenceValue=item;
                entry.FindPropertyRelative("MinCount").intValue=item.MinDrop;entry.FindPropertyRelative("MaxCount").intValue=item.MaxDrop;
                entry.FindPropertyRelative("Weight").floatValue=item.DropWeight*.4f;entry.FindPropertyRelative("FavoredDungeonId").stringValue=item.FavoredDungeonId;entry.FindPropertyRelative("FavoredMultiplier").floatValue=item.FavoredMultiplier;
            }
            loot.ApplyModifiedPropertiesWithoutUndo();
            var shop=new SerializedObject(AssetDatabase.LoadAssetAtPath<ShopCatalogSO>("Assets/ScriptableObjects/Lobby/ShopCatalog_Main.asset"));
            AppendCraftingReferences(shop.FindProperty("items"),materials.Where(m=>m.SoldAtShop));shop.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddStartingMaterials(CraftingCatalogSO catalog)
        {
            EditPrefab(CombatRoot+"/Player_Character.prefab",root=>
            {
                var so=new SerializedObject(root.GetComponent<PlayerInventory>());var list=so.FindProperty("defaultStartingItems");
                foreach(var bundle in new[]{(id:"wood",count:2),(id:"iron",count:3),(id:"herb",count:2),(id:"glass",count:1)})
                {
                    var item=catalog.FindMaterial("material."+bundle.id);string key=PlayerInventory.MaterialSlotKey(item);int index=-1;
                    for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).FindPropertyRelative("SlotKey").stringValue==key){index=i;break;}
                    if(index<0){index=list.arraySize;list.arraySize++;}
                    var entry=list.GetArrayElementAtIndex(index);entry.FindPropertyRelative("SlotKey").stringValue=key;entry.FindPropertyRelative("Item").objectReferenceValue=item;entry.FindPropertyRelative("Count").intValue=bundle.count;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        // Each slot has its own faceted silhouette, recognizable even at phone-sized HUD scale.
        private static void BuildMaterialIconModel(Transform t,string key)
        {
            void P(string name,Mesh mesh,Material material,Vector3 pos,Vector3 scale,Quaternion rot=default)=>Part(t,name,mesh,material,pos,scale,rot);
            switch(key)
            {
                case "Wood":
                    for(int i=0;i<3;i++)
                    {float x=(i-1)*.25f;P("Log",prism,wood,V(x,.39f,i*.02f),V(.28f,.93f,.29f),Q(12,0,-23));P("End grain",prism,ivory,V(x+.16f,.8f,i*.02f+.08f),V(.26f,.035f,.27f),Q(12,0,-23));}
                    P("Binding",box,leather,V(0,.4f,-.15f),V(.77f,.13f,.08f),Q(0,0,-10));break;
                case "Iron":case "Coal":
                    Material rock=key=="Iron"?steel:darkIron;
                    P("Main chunk",gem,rock,V(-.05f,.44f,0),V(.76f,.69f,.66f),Q(19,12,24));
                    P("Smaller fragment",gem,rock,V(.37f,.21f,-.05f),V(.46f,.43f,.43f),Q(5,40,-39));
                    P("Mineral veins",gem,key=="Iron"?silver:amber,V(-.21f,.48f,-.31f),V(.31f,.16f,.095f),Q(0,0,35));
                    if(key=="Coal")P("Ember",gem,flame,V(.13f,.35f,-.28f),V(.15f,.29f,.12f),Q(0,0,-37));break;
                case "Stone":
                    P("Basalt pebble",plateMesh,ForgeMat("MasonryCold"),V(-.21f,.35f,0),V(.68f,.61f,.63f),Q(12,24,18));
                    P("Angular cobble",plateMesh,ForgeMat("WallBasalt"),V(.28f,.29f,-.09f),V(.58f,.53f,.49f),Q(0,25,-19));
                    P("Stone chip",gem,ForgeMat("MasonryAsh"),V(.02f,.18f,-.37f),V(.31f,.31f,.35f),Q(12,32,19));break;
                case "Copper":case "Steel":
                    Material metal=key=="Copper"?bronze:steel;
                    for(int i=0;i<3;i++)P("Ingot",box,metal,V(i==2?0:(i-.5f)*.43f,i==2?.53f:.23f,0),V(.49f,.27f,.78f),Q(0,24,-9));
                    P("Stamp",gem,key=="Copper"?gold:silver,V(0,.56f,-.38f),V(.17f,.15f,.09f),Q(0,0,45));break;
                case "Leather":
                    P("Rolled hide",prism,leather,V(-.18f,.42f,0),V(.50f,.83f,.50f),Q(0,0,27));
                    P("Rolled edge",prism,wood,V(-.36f,.78f,.02f),V(.38f,.055f,.38f),Q(0,0,27));
                    P("Fold",box,leather,V(.27f,.19f,-.05f),V(.71f,.33f,.48f),Q(3,0,-23));
                    P("Strap",box,bronze,V(-.18f,.42f,-.265f),V(.58f,.13f,.08f),Q(0,0,27));break;
                case "Cloth":
                    for(int i=0;i<3;i++)P("Folded linen",box,i%2==0?cloak:cloth,V((i-1)*.05f,.15f+i*.19f,0),V(.9f,.18f,.66f),Q(0,i*10,0));
                    P("Embroidery",box,bronze,V(.17f,.33f,-.32f),V(.085f,.65f,.09f));P("Seal",gem,gold,V(.17f,.44f,-.38f),V(.17f,.24f,.10f));break;
                case "Herb":case "VenomPetal":
                    bool venom=key=="VenomPetal";Material leaves=venom?poison:ForgeMat("HunterCloth");
                    for(int i=0;i<5;i++)
                    {float x=(i-2)*.12f;P("Stem",prism,wood,V(x*.6f,.37f,0),V(.055f,.63f,.055f),Q(0,0,(2-i)*12));P("Leaf",blade,leaves,V(x,.47f+Mathf.Abs(i-2)*.04f,-.04f),V(.27f,.66f,.10f),Q(8,i*13,(2-i)*27));}
                    P("Blossom",gem,venom?purple:soul,V(0,.78f,-.09f),V(.24f,.23f,.21f));P("Binding",prism,leather,V(0,.18f,0),V(.35f,.12f,.25f));break;
                case "Glass":
                    for(int i=0;i<3;i++)P("Glass fragment",gem,i==1?silver:frost,V((i-1)*.26f,.35f+i*.10f,i*.04f),V(.32f,.79f-i*.08f,.13f),Q(16,i*17,28-i*35));break;
                case "Bone":
                    for(int i=0;i<2;i++){var b=Group(t,"Bone",V((i-.5f)*.32f,.41f,i*.14f),Q(0,0,i==0?-34:36));Part(b,"Shaft",prism,ivory,Vector3.zero,V(.15f,.71f,.15f));foreach(var y in new[]{-.34f,.34f})Part(b,"Joint",plateMesh,ivory,V(0,y,0),V(.33f,.26f,.28f));}break;
                case "FireStone":
                    P("Volcanic host",gem,ForgeMat("VolcanicBasalt"),V(0,.31f,0),V(.91f,.59f,.65f),Q(10,24,16));
                    P("Ember core",gem,fireOuter,V(-.03f,.53f,-.22f),V(.52f,.71f,.42f),Q(0,0,-14));
                    P("Molten vein",blade,flame,V(-.09f,.37f,-.43f),V(.13f,.50f,.085f),Q(0,0,-26));
                    P("Hot splinter",gem,red,V(.38f,.30f,-.16f),V(.30f,.42f,.33f),Q(0,0,-43));
                    P("Living ember",fireMesh,fireOuter,V(.03f,.84f,0),V(.30f,.35f,.27f),Q(2,15,-10));break;
                case "FrostCrystal":
                    P("Tall ice",gem,frost,V(0,.60f,0),V(.36f,1.18f,.36f),Q(0,0,-5));
                    P("Left ice",gem,soul,V(-.25f,.35f,.045f),V(.27f,.67f,.24f),Q(0,15,29));
                    P("Right ice",gem,frost,V(.25f,.33f,-.05f),V(.28f,.62f,.25f),Q(0,25,-37));
                    P("Frozen base",gem,steel,V(0,.10f,0),V(.72f,.27f,.58f),Q(0,15,10));break;
                case "StormShard":
                    P("Stormstone",gem,cloak,V(.04f,.45f,.1f),V(.60f,.81f,.39f),Q(20,0,19));
                    P("Lightning upper",blade,gold,V(.083f,.67f,-.19f),V(.32f,.67f,.18f),Q(0,0,-35));
                    P("Lightning lower",blade,amber,V(-.12f,.29f,-.20f),V(.31f,.70f,.16f),Q(0,0,143));
                    P("Bolt junction",gem,ivory,V(0,.47f,-.26f),V(.31f,.21f,.09f));
                    P("Spark",gem,gold,V(.45f,.52f,-.10f),V(.12f,.31f,.12f),Q(0,0,-27));break;
                case "VoidShard":
                    P("Dark heart",gem,cloak,V(0,.49f,0),V(.66f,.99f,.55f),Q(22,0,13));
                    P("Void facet",gem,purple,V(0,.49f,-.26f),V(.43f,.72f,.18f),Q(0,0,12));
                    P("Warp orbit",ring,ForgeMat("EngravedViolet"),V(0,.48f,.01f),V(1.16f,1,1.16f),Q(73,0,-17));
                    P("Levitation fragment",gem,purple,V(-.43f,.69f,.075f),V(.22f,.37f,.23f),Q(0,35,32));
                    P("Levitation fragment",gem,purple,V(.39f,.23f,-.03f),V(.22f,.31f,.21f),Q(0,35,-33));break;
                case "WaterPearl":
                    P("Pearl",plateMesh,frost,V(0,.44f,0),V(.81f,.81f,.80f));P("Tide ring",broadRing,runeTeal,V(0,.42f,0),V(1,1,1),Q(19,0,23));
                    P("Luminescent core",gem,soul,V(-.14f,.52f,-.37f),V(.31f,.23f,.08f),Q(0,0,28));break;
                case "SpiritDust":
                    P("Vial base",prism,darkIron,V(0,.1f,0),V(.56f,.15f,.56f));P("Spirit vessel",plateMesh,frost,V(0,.42f,0),V(.57f,.64f,.51f));
                    P("Inner light",gem,soul,V(0,.40f,-.24f),V(.24f,.39f,.12f));P("Stopper",prism,bronze,V(0,.77f,0),V(.35f,.18f,.35f));
                    for(int i=0;i<3;i++)P("Floating mote",gem,ivory,V(-.37f+i*.31f,.88f+i*.08f,0),V(.10f,.20f,.10f),Q(0,0,30));break;
                case "SunEssence":
                    P("Sun heart",gem,flame,V(0,.49f,0),V(.57f,.65f,.54f),Q(10,0,14));
                    P("Solar halo",broadRing,gold,V(0,.49f,.04f),V(1.12f,1,1.12f),Q(83,0,0));
                    for(int i=0;i<8;i++){float a=i*45*Mathf.Deg2Rad;P("Ray",blade,gold,V(Mathf.Sin(a)*.49f,.49f+Mathf.Cos(a)*.49f,-.02f),V(.17f,.27f,.075f),Q(0,0,-i*45));}break;
                case "BossSoul":
                    P("Soul",gem,purple,V(0,.49f,0),V(.65f,.98f,.59f),Q(0,0,9));P("Soul light",gem,flame,V(0,.5f,-.29f),V(.22f,.59f,.12f));
                    P("Crown",prism,gold,V(0,.83f,0),V(.78f,.15f,.70f));for(int i=0;i<5;i++){float a=i*72*Mathf.Deg2Rad;P("Crown point",cone,bronze,V(Mathf.Sin(a)*.31f,1.01f,Mathf.Cos(a)*.31f),V(.16f,.37f,.16f));}
                    P("Sigil",broadRing,gold,V(0,.25f,0),V(.88f,1,.88f),Q(18,0,0));break;
                default:throw new ArgumentException("Unknown material icon "+key);
            }
        }

        private static Material ForgeMat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(ArtRoot+"/Materials/DF_"+name+".mat");

        private static void BuildCraftingForge(CraftingCatalogSO catalog)
        {
            var root=new GameObject("Lobby_CraftingForge");root.layer=12;
            try
            {
                Transform t=root.transform;
                var trigger=root.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=V(0,1,-1.65f);trigger.size=V(4.65f,2.2f,1.65f);
                var station=root.AddComponent<CraftingStation>();
                var collision=Group(t,"Solid workstations");collision.gameObject.layer=8;
                var solid=collision.gameObject.AddComponent<BoxCollider>();solid.center=V(0,.9f,.17f);solid.size=V(4.54f,1.8f,1.95f);
                Material masonry=ForgeMat("MasonryDark"),cold=ForgeMat("MasonryCold"),ash=ForgeMat("MasonryAsh"),edge=ForgeMat("ArchitectureTrim"),dark=ForgeMat("WallBasalt");
                var batch=new MasonryBatch();
                void A(Mesh mesh,Material m,Vector3 p,Vector3 s,Quaternion q=default)=>batch.Add(mesh,m,p,s,q);
                // Cut-stone podium, metal-banded hearth, chimney and two recessed workstations.
                A(block,edge,V(0,.065f,.1f),V(4.8f,.13f,2.38f));
                A(block,masonry,V(0,.19f,.15f),V(4.55f,.15f,2.15f));
                A(block,cold,V(-1.12f,.40f,.18f),V(2.12f,.38f,1.76f));
                A(block,dark,V(-1.12f,1.06f,.83f),V(1.94f,1.45f,.28f));
                A(box,darkIron,V(-1.12f,.62f,.09f),V(1.48f,.18f,1.34f));
                A(block,edge,V(-1.12f,.57f,-.63f),V(2.15f,.19f,.38f));
                for(int side=-1;side<=1;side+=2)
                {
                    for(int j=0;j<3;j++)A(block,j%2==0?cold:ash,V(-1.12f+side*.78f,.77f+j*.30f,.09f),V(.48f,.28f,1.48f));
                    A(box,bronze,V(-1.12f+side*1.02f,1.09f,-.58f),V(.09f,1.02f,.12f));
                    A(block,edge,V(-1.12f+side*.69f,1.69f,-.47f),V(.52f,.33f,.55f),Q(0,0,side*37));
                    A(block,ash,V(-1.12f+side*.38f,1.84f,-.47f),V(.48f,.33f,.55f),Q(0,0,side*20));
                }
                A(block,edge,V(-1.12f,1.96f,.10f),V(2.20f,.21f,1.5f));
                A(block,cold,V(-1.12f,2.16f,.54f),V(1.3f,.29f,.85f));
                A(block,masonry,V(-1.12f,2.48f,.54f),V(.86f,.39f,.81f));
                A(block,edge,V(-1.12f,2.70f,.54f),V(1.02f,.18f,.95f));
                A(box,bronze,V(-1.12f,1.58f,-.80f),V(1.61f,.08f,.08f));
                A(gem,gold,V(-1.12f,1.98f,-.69f),V(.25f,.29f,.09f));
                for(int i=0;i<4;i++)A(gem,i%2==0?amber:darkIron,V(-1.60f+i*.32f,.72f,-.32f),V(.39f,.26f,.42f),Q(6,i*33,0));
                // Adjacent carved workbench and low-poly anvil, hammer and sorted arcane shards.
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                    A(block,wood,V(1.12f+x*.75f,.62f,.16f+z*.60f),V(.27f,.76f,.27f));
                A(block,wood,V(1.12f,.96f,.16f),V(2.02f,.24f,1.68f));
                A(box,bronze,V(1.12f,1.01f,-.69f),V(2.08f,.09f,.08f));
                A(box,darkIron,V(1.12f,.50f,.15f),V(1.83f,.15f,1.54f));
                A(box,darkIron,V(.87f,1.12f,.04f),V(.87f,.12f,.63f));
                A(box,steel,V(.87f,1.32f,.04f),V(.43f,.23f,.50f));
                A(box,steel,V(.87f,1.51f,.04f),V(.91f,.18f,.60f));
                A(cape,silver,V(.28f,1.48f,.04f),V(.41f,.28f,.57f),Q(0,90,90));
                A(prism,wood,V(1.94f,1.15f,-.24f),V(.12f,.67f,.12f),Q(83,0,-23));
                A(box,steel,V(1.85f,1.19f,.05f),V(.35f,.20f,.22f),Q(0,-23,0));
                A(box,darkIron,V(1.93f,1.14f,.52f),V(.53f,.14f,.55f));
                A(gem,purple,V(1.78f,1.34f,.49f),V(.22f,.4f,.20f),Q(0,0,15));
                A(gem,frost,V(2.02f,1.30f,.52f),V(.24f,.32f,.22f),Q(0,20,-19));
                for(int i=0;i<3;i++)A(box,i==2?bronze:steel,V(.46f+i*.40f,.64f,.05f),V(.32f,.13f,.52f),Q(0,i*10,0));
                // Rack and two blade silhouettes (decorative props only); sign crest and a floor sigil.
                A(block,wood,V(1.12f,1.65f,.90f),V(1.92f,1.19f,.15f));
                A(box,bronze,V(1.12f,2.29f,.81f),V(2.03f,.10f,.16f));
                for(int i=0;i<2;i++)
                {
                    float x=.63f+i*.78f;float r=i==0?-24f:23f;
                    A(blade,i==0?silver:fireOuter,V(x,1.91f,.77f),V(.15f,.63f,.06f),Q(0,0,r));
                    A(box,bronze,V(x+(i==0?-.15f:.14f),1.61f,.73f),V(.30f,.07f,.07f),Q(0,0,r));
                    A(prism,leather,V(x+(i==0?-.20f:.20f),1.49f,.73f),V(.09f,.23f,.08f),Q(0,0,r));
                }
                A(gem,gold,V(1.12f,2.20f,.77f),V(.21f,.25f,.13f));
                A(ring,ForgeMat("EngravedGold"),V(0,.018f,-1.75f),V(2.02f,1,2.02f));
                A(ring,bronze,V(0,.014f,-1.75f),V(1.79f,1,1.79f));
                for(int i=0;i<8;i++){float a=i*45*Mathf.Deg2Rad;A(box,amber,V(Mathf.Sin(a)*.76f,.023f,-1.75f+Mathf.Cos(a)*.76f),V(.055f,.012f,.14f),Q(0,i*45,0));}
                A(gem,bronze,V(0,.025f,-1.75f),V(.23f,.035f,.45f),Q(0,0,0));
                batch.Finish(t,"Art_ForgeMasonry","Lobby_CraftingForge",true);

                var fire=Group(t,"Living flame",V(-1.12f,.73f,-.24f));
                for(int i=0;i<3;i++)
                {
                    float x=(i-1)*.38f;float h=i==1?.93f:.66f;
                    Part(fire,"Outer flame",fireMesh,fireOuter,V(x,h*.42f,0),V(.50f,h,.55f),Q(0,i*24,7-i*7),false);
                    Part(fire,"Hot core",fireMesh,flame,V(x,.22f,-.20f),V(.26f,h*.65f,.25f),Q(0,i*21,0),false);
                }
                var sparks=Group(t,"Rising cinders",V(-1.12f,1.2f,-.62f)).gameObject.AddComponent<ParticleSystem>();
                sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=sparks.main;
                main.playOnAwake=false;main.loop=true;main.maxParticles=56;main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,1.15f);main.startSpeed=new ParticleSystem.MinMaxCurve(.28f,.98f);
                main.startSize=new ParticleSystem.MinMaxCurve(.035f,.10f);main.startColor=new ParticleSystem.MinMaxGradient(C("ff9551"),C("ffe5a3"));main.gravityModifier=-.18f;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.31f;
                var emission=sparks.emission;emission.rateOverTime=10;
                var fade=sparks.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(C("ff9965"),1)},new[]{new GradientAlphaKey(.95f,0),new GradientAlphaKey(.7f,.55f),new GradientAlphaKey(0f,1)});fade.color=gradient;
                var sr=sparks.GetComponent<ParticleSystemRenderer>();sr.sharedMaterial=Resources.Load<Material>("VFX/Mat_VFX_Additive");sr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;sr.receiveShadows=false;
                var lamp=Group(t,"Hearth glow",V(-1.12f,1.12f,-.92f)).gameObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.color=C("ffc17e");lamp.range=7.2f;lamp.intensity=5.2f;lamp.shadows=LightShadows.None;
                lamp.gameObject.AddComponent<DarkFantasyTorchLight>().Configure(lamp,5.2f,.12f,fire,sparks);
                AddForgeSign(t);
                var serialized=new SerializedObject(station);serialized.FindProperty("catalog").objectReferenceValue=catalog;serialized.FindProperty("craftSparks").objectReferenceValue=sparks;serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,CraftForgePrefab);
            }
            finally{Object.DestroyImmediate(root);}

            var lobby=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Single);
            var instance=Object.FindAnyObjectByType<CraftingStation>(FindObjectsInactive.Include);
            if(instance==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CraftForgePrefab);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,lobby);
                go.transform.position=V(0,0,6.70f);go.transform.rotation=Quaternion.identity;
                EditorSceneManager.MarkSceneDirty(lobby);EditorSceneManager.SaveScene(lobby);
            }
        }

        private static void AddForgeSign(Transform parent)
        {
            var go=new GameObject("Forge nameplate",typeof(RectTransform),typeof(Canvas));var rt=(RectTransform)go.transform;
            rt.SetParent(parent,false);rt.localPosition=V(.25f,2.84f,-.12f);rt.localRotation=Q(57,0,0);rt.localScale=Vector3.one*.011f;rt.sizeDelta=new Vector2(300,58);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=1;
            var frame=new GameObject("Bronze frame",typeof(RectTransform),typeof(Image));frame.transform.SetParent(rt,false);
            var frameRt=(RectTransform)frame.transform;frameRt.anchorMin=Vector2.zero;frameRt.anchorMax=Vector2.one;frameRt.offsetMin=frameRt.offsetMax=Vector2.zero;
            var image=frame.GetComponent<Image>();DarkFantasyTheme.FramePanel(image,Color.white);image.raycastTarget=false;
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(rt,false);var labelRt=(RectTransform)label.transform;labelRt.anchorMin=Vector2.zero;labelRt.anchorMax=Vector2.one;labelRt.offsetMin=new Vector2(6,4);labelRt.offsetMax=new Vector2(-6,-4);
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontStyle=FontStyle.Bold;text.fontSize=27;text.color=DarkFantasyTheme.Gold;text.alignment=TextAnchor.MiddleCenter;text.text="LÒ RÈN ÁNH TÀN";text.raycastTarget=false;
        }
    }
}
