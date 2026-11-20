using System;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat.Items;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.MapGeneration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private const string TemplateRoot="Assets/ScriptableObjects/MapGeneration/Templates";
        private const string ConfigRoot="Assets/ScriptableObjects/MapGeneration/Config";
        private static Material terrainMoss, terrainMossShade, terrainEmber, terrainPaleStone, terrainWaterSheen;
        private static Material[] terrainLavaFacets, terrainWaterFacets;
        private static Mesh terrainRamp, terrainFacet;

        private enum TerrainLayout { Existing, Cloister, Library, StoneBridge, Trial, Islands, EmberBridge, Forge, DrownedCourt, Labyrinth, Sluice, TrapVault, Treasury, Start, Boss }
        private sealed class TerrainVariant
        {
            public string Name, Shell, Label, Note;
            public DungeonTerrainRegion Region;
            public TerrainLayout Layout;
            public TerrainVariant(string name,string shell,DungeonTerrainRegion region,TerrainLayout layout,string label,string note)
            {Name=name;Shell=shell;Region=region;Layout=layout;Label=label;Note=note;}
        }

        [MenuItem("Light Souls/Art/Build Expanded Dungeon Terrain")]
        public static void BuildExpandedDungeonTerrain()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            string previous=SceneManager.GetActiveScene().path;
            LoadTerrainKit();
            var ruins=DungeonTerrainRegion.AncientRuins;var molten=DungeonTerrainRegion.VolcanicDepths;var drowned=DungeonTerrainRegion.DrownedLabyrinth;
            var variants=new[]
            {
                new TerrainVariant("Room_AncientCloister","Room_Large_Arena",ruins,TerrainLayout.Cloister,"Hành Lang Tu Viện","Cột đá đổ tạo nhiều hướng tiếp cận và che chắn."),
                new TerrainVariant("Room_RuinedLibrary","Room_Large_Arena",ruins,TerrainLayout.Library,"Thư Viện Hoang Phế","Kệ sách cổ thấp, lối đi giữa kệ và bốn vùng giao tranh."),
                new TerrainVariant("Room_StoneBridge","Room_Large_Arena",ruins,TerrainLayout.StoneBridge,"Cầu Đá Cổ","Cầu nâng và đường dốc qua các rãnh nước sâu."),
                new TerrainVariant("Room_TrialHall","Room_Cross_4Way",ruins,TerrainLayout.Trial,"Điện Thử Lửa","Bẫy cọc nhịp lệch; đường giao cắt luôn an toàn."),
                new TerrainVariant("Room_RoyalReliquary","Room_Treasure",ruins,TerrainLayout.Treasury,"Kho Di Vật Hoàng Gia","Nhánh kho báu ẩn chứa vật phẩm quý."),

                new TerrainVariant("Room_Start_EmberGate","Room_Start",molten,TerrainLayout.Start,"Cổng Tro Tàn","Trạm tiếp tế trên nền đá núi lửa."),
                new TerrainVariant("Room_BasaltIslands","Room_Lava_Arena",molten,TerrainLayout.Islands,"Đảo Hắc Thạch","Bốn hồ dung nham, đường đá nối các đảo và cột basalt."),
                new TerrainVariant("Room_EmberBridge","Room_Lava_Arena",molten,TerrainLayout.EmberBridge,"Cầu Hỏa Ngục","Cầu đá liên kết nhiều ngả qua dung nham; có dốc thật."),
                new TerrainVariant("Room_AshForge","Room_Lava_Arena",molten,TerrainLayout.Forge,"Xưởng Rèn Tro Tàn","Hai máng dung nham và các bệ rèn tạo vật cản."),
                new TerrainVariant("Room_EmberVault","Room_Treasure",molten,TerrainLayout.Treasury,"Kho Tinh Thạch Lửa","Kho báu đặc biệt trong hốc đá núi lửa."),
                new TerrainVariant("Room_Exit_MoltenSanctum","Room_Exit",molten,TerrainLayout.Boss,"Thánh Điện Hỏa Ngục","Bốn đài basalt, trung tâm rộng cho thủ vệ."),

                new TerrainVariant("Room_Start_DrownedGate","Room_Start",drowned,TerrainLayout.Start,"Cổng Mộ Thủy Triều","Trạm tiếp tế giữa thành đá rêu phong."),
                new TerrainVariant("Room_DrownedCourt","Room_Maze_Large",drowned,TerrainLayout.DrownedCourt,"Sân Điện Ngập Nước","Hồ nước làm chậm quanh đường đá khô."),
                new TerrainVariant("Room_MossLabyrinth","Room_Maze_Large",drowned,TerrainLayout.Labyrinth,"Mê Cung Rêu Phong","Tường thấp bố trí lệch nhau, có lối vòng."),
                new TerrainVariant("Room_SluiceCrossing","Room_Maze_Large",drowned,TerrainLayout.Sluice,"Ngã Tư Thủy Môn","Rãnh nước và cầu đá nối các ngả."),
                new TerrainVariant("Room_TrapVault","Room_Water_Pond",drowned,TerrainLayout.TrapVault,"Mật Điện Bẫy Cổ","Cọc đá theo nhịp, vách đổ và đường tránh."),
                new TerrainVariant("Room_DrownedCache","Room_Treasure",drowned,TerrainLayout.Treasury,"Kho Báu Thủy Mộ","Hòm hiếm sau nhánh lối rẽ bị che trên minimap."),
                new TerrainVariant("Room_Exit_DeepCrypt","Room_Exit",drowned,TerrainLayout.Boss,"Mộ Điện Thủy Triều","Đài tinh thạch xanh, sân giao chiến thông thoáng."),
            };
            var templates=new Dictionary<string,RoomTemplateSO>();
            // Clone only the structural shell; explicit authored interiors replace the previous layout.
            // This preserves door sockets, the Start machine and the boss reward / exit anchors.
            foreach(var v in variants)
            {
                string source=RoomRoot+"/"+v.Shell+".prefab",path=RoomRoot+"/"+v.Name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(source);
                try
                {
                    root.name=v.Name;
                    var room=root.GetComponent<RoomModule>();
                    if(v.Layout!=TerrainLayout.Start&&v.Layout!=TerrainLayout.Boss)ClearOriginalInterior(room);
                    DressTerrainRoom(room,v,true);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RoomModule>();
                var template=LoadOrCreateTerrainTemplate(v.Name);
                int min=prefab.RoomType==RoomType.Start||prefab.RoomType==RoomType.Exit||prefab.RoomType==RoomType.Treasure?1:0;
                template.Configure("terrain."+v.Name.Substring("Room_".Length).ToLowerInvariant(),prefab,1.8f,min,1,true);
                EditorUtility.SetDirty(template);templates[v.Name]=template;
                Debug.Log("TERRAIN_ROOM_AUTHORED "+v.Name);
            }

            var originals=new[]
            {
                ("Room_Start",ruins,"Tiền Sảnh Thành Cổ",TerrainLayout.Start),
                ("Room_Exit",ruins,"Điện Thờ Thủ Vệ",TerrainLayout.Boss),
                ("Room_Treasure",ruins,"Kho Cổ",TerrainLayout.Existing),
                ("Room_Large_Arena",ruins,"Đấu Trường Thành Cổ",TerrainLayout.Existing),
                ("Room_Corner_L",ruins,"Phế Tích Lối Rẽ",TerrainLayout.Existing),
                ("Room_Cross_4Way",ruins,"Giao Lộ Tàn Tích",TerrainLayout.Existing),
                ("Room_Corridor_Straight",ruins,"Hành Lang Đá",TerrainLayout.Existing),
                ("Room_Corridor_Corner_L",ruins,"Hành Lang Gấp Khúc",TerrainLayout.Existing),
                ("Room_Lava_Arena",molten,"Đấu Trường Dung Nham",TerrainLayout.Existing),
                ("Room_Maze_Large",drowned,"Mê Cung Đá Cổ",TerrainLayout.Existing),
                ("Room_Water_Pond",drowned,"Đầm Nước Cổ",TerrainLayout.Existing),
                ("Room_Spike_Traps",ruins,"Hành Lang Cọc Đá",TerrainLayout.Existing)
            };
            foreach(var entry in originals)
            {
                EditPrefab(RoomRoot+"/"+entry.Item1+".prefab",root=>DressTerrainRoom(root.GetComponent<RoomModule>(),
                    new TerrainVariant(entry.Item1,entry.Item1,entry.Item2,entry.Item4,entry.Item3,"Địa hình nguyên bản được nâng cấp ánh sáng, vật liệu và mốc dẫn đường."),false));
            }
            ConfigureTerrainPools(templates);
            BuildTerrainMapIcons();
            var loot=BuildSecretTreasuryLoot();
            var game=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);
            var camera=Object.FindAnyObjectByType<GameCameraFollow>();
            if(camera==null)throw new InvalidOperationException("Missing gameplay camera");
            if(camera.GetComponent<DungeonWallOcclusion>()==null)camera.gameObject.AddComponent<DungeonWallOcclusion>();
            var gameplay=Object.FindAnyObjectByType<GameBootstrap>();
            if(gameplay==null)throw new InvalidOperationException("Missing gameplay bootstrap");
            var so=new SerializedObject(gameplay);so.FindProperty("hiddenTreasuryLootTable").objectReferenceValue=loot;so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(game);EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(!string.IsNullOrEmpty(previous)&&previous!=game.path)EditorSceneManager.OpenScene(previous);
            Debug.Log("LIGHT_SOULS_EXPANDED_TERRAIN_COMPLETE rooms="+(variants.Length+originals.Length));
        }

        private static void LoadTerrainKit()
        {
            LoadItemMeshes();LoadItemMaterials();
            Material Existing(string name)=>AssetDatabase.LoadAssetAtPath<Material>(ArtRoot+"/Materials/DF_"+name+".mat")??throw new InvalidOperationException("Missing terrain material "+name);
            mortar=Existing("Mortar");floor0=Existing("FlagstoneAsh");floor1=Existing("FlagstoneSlate");floor2=Existing("FlagstoneBlue");floor3=Existing("FlagstoneWeathered");
            wall0=Existing("WallBasalt");wall1=Existing("MasonryCold");wall2=Existing("MasonryAsh");wall3=Existing("MasonryDark");trim=Existing("ArchitectureTrim");
            floorStones=new[]{floor0,floor1,floor2,floor3};wallStones=new[]{wall1,wall2,wall3};
            volcanicStones=new[]{Existing("VolcanicAsh"),Existing("VolcanicBasalt"),Existing("VolcanicSlate"),Existing("VolcanicWeathered")};
            drownedStones=new[]{Existing("DrownedStone"),Existing("DrownedSlate"),Existing("DrownedMoss"),Existing("DrownedAsh")};
            cryptStones=new[]{Existing("CryptStone"),Existing("CryptSlate"),Existing("CryptAsh"),Existing("CryptWeathered")};
            treasuryStones=new[]{Existing("TreasuryStone"),Existing("TreasurySlate"),Existing("TreasuryAsh"),Existing("TreasuryWeathered")};
            runeGold=Existing("EngravedGold");runeViolet=Existing("EngravedViolet");runeTeal=Existing("EngravedTeal");landmark=Existing("LandmarkInlay");
            water=Existing("DeepWater");lava=Existing("LavaFlow");
            terrainMoss=Mat("TerrainMoss","58774f",.15f);terrainMossShade=Mat("TerrainMossShade","304e41",.12f);
            terrainEmber=Mat("TerrainEmberSeam","b25930",.1f,0,"f17c30",.5f);
            terrainPaleStone=Mat("TerrainPaleLimestone","85817a",.19f);
            terrainLavaFacets=new[]{Mat("TerrainLavaCrust","652925",.26f,0,"b34023",.26f),Mat("TerrainLavaDeep","b54023",.30f,0,"df4e24",.58f),
                Mat("TerrainLavaFlow","e66b2e",.34f,0,"ff8b32",.75f),Mat("TerrainLavaHot","f0a34a",.39f,0,"ffad54",.85f)};
            terrainWaterFacets=new[]{Mat("TerrainWaterDeep","254856",.58f,.04f),Mat("TerrainWaterCurrent","315c68",.55f,.06f),
                Mat("TerrainWaterShallow","417a7c",.58f,.04f),Mat("TerrainWaterShadow","213d50",.55f,.03f)};
            terrainWaterSheen=Mat("TerrainWaterSheen","5d9092",.66f,.03f,"47989a",.12f);
            plainCube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");if(plainCube==null){var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);plainCube=cube.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(cube);}
            torchPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot+"/Props/WallSconce.prefab");
            if(torchPrefab==null)throw new InvalidOperationException("Missing wall sconce prefab");
            terrainRamp=BuildTerrainRampMesh();
            terrainFacet=BuildTerrainFacetMesh();
        }

        private static RoomTemplateSO LoadOrCreateTerrainTemplate(string name)
        {
            string path=TemplateRoot+"/Template_"+name+".asset";var template=AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(path);
            if(template==null){template=ScriptableObject.CreateInstance<RoomTemplateSO>();AssetDatabase.CreateAsset(template,path);}return template;
        }

        private static void ConfigureTerrainPools(Dictionary<string,RoomTemplateSO> added)
        {
            RoomTemplateSO T(string key)=>added.TryGetValue(key,out var value)?value:AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(TemplateRoot+"/Template_Room_"+key+".asset");
            List<RoomTemplateSO> Pool(params string[] keys)=>keys.Select(T).Select(t=>t??throw new InvalidOperationException("Missing terrain template in pool")).ToList();
            var oldTreasure=T("Treasure");oldTreasure.Configure(oldTreasure.TemplateId,oldTreasure.Prefab,.7f,0,1,true);EditorUtility.SetDirty(oldTreasure);
            void Config(string name,string start,string exit,string treasure,params string[] normal)
            {
                var config=AssetDatabase.LoadAssetAtPath<DungeonGenerationConfigSO>(ConfigRoot+"/"+name+".asset");
                config.ConfigurePools(Pool(start),Pool("Corridor","Corridor_Corner"),Pool(normal),Pool(treasure,"Treasure"),Pool(exit));EditorUtility.SetDirty(config);
            }
            Config("DefaultDungeonConfig","Start","Exit","Room_RoyalReliquary","Cross","Corner","Arena","Spike_Traps","Room_AncientCloister","Room_RuinedLibrary","Room_StoneBridge","Room_TrialHall");
            Config("Config_LavaDungeon","Room_Start_EmberGate","Room_Exit_MoltenSanctum","Room_EmberVault","Lava_Arena","Room_BasaltIslands","Room_EmberBridge","Room_AshForge");
            Config("Config_MazeDungeon","Room_Start_DrownedGate","Room_Exit_DeepCrypt","Room_DrownedCache","Maze_Large","Water_Pond","Spike_Traps","Room_DrownedCourt","Room_MossLabyrinth","Room_SluiceCrossing","Room_TrapVault");
        }

        private static LootTableSO BuildSecretTreasuryLoot()
        {
            string path="Assets/ScriptableObjects/Combat/Items/LootTable_SecretTreasury.asset";
            var table=AssetDatabase.LoadAssetAtPath<LootTableSO>(path);
            if(table==null){table=ScriptableObject.CreateInstance<LootTableSO>();AssetDatabase.CreateAsset(table,path);}
            var db=AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/ScriptableObjects/Combat/Items/ItemDatabase.asset");
            var items=db.AllItems.Where(i=>i!=null&&i.Rarity>=ItemRarity.Rare&&!(i is CraftMaterialSO m&&m.BossOnly)&&!(ArsenalId(i)&&i.Rarity==ItemRarity.Legendary)).ToArray();
            var so=new SerializedObject(table);var list=so.FindProperty("dropEntries");list.arraySize=items.Length;
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];var entry=list.GetArrayElementAtIndex(i);entry.FindPropertyRelative("Item").objectReferenceValue=item;
                entry.FindPropertyRelative("MinCount").intValue=1;entry.FindPropertyRelative("MaxCount").intValue=item.Category==ItemCategory.Material?2:1;
                entry.FindPropertyRelative("Weight").floatValue=item.Rarity==ItemRarity.Rare?10f:item.Rarity==ItemRarity.Epic?3.4f:.65f;
                entry.FindPropertyRelative("FavoredDungeonId").stringValue=string.Empty;entry.FindPropertyRelative("FavoredMultiplier").floatValue=1f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();return table;
        }

        private static void BuildTerrainMapIcons()
        {
            // Readable silhouettes at 12–26px: merchant awning, chest and crown.
            const int size=64;
            bool RectAt(int x,int y,int left,int bottom,int right,int top)=>x>=left&&x<=right&&y>=bottom&&y<=top;
            Color ink=C("18212d"),iv=C("fff2cc"),warm=C("edbd70"),cyan=C("91d6dc"),rose=C("e3a4ed");
            SpriteAsset("MapShop",size,size,(x,y)=>
            {
                if(x<6||x>57||y<8||y>55)return Color.clear;
                if(y>=36&&y<=51&&x>=8+(y-36)/3&&x<=55-(y-36)/3)return ((x-8)/9)%2==0?iv:warm;
                if(RectAt(x,y,8,8,55,13))return iv;
                if(RectAt(x,y,27,13,37,30))return ink;
                if(RectAt(x,y,14,15,49,39))return cyan;
                if(RectAt(x,y,9,9,54,40))return ink;
                return Color.clear;
            });
            SpriteAsset("MapTreasure",size,size,(x,y)=>
            {
                if(!RectAt(x,y,8,12,55,51)||((x==8||x==55)&&(y>47||y<15)))return Color.clear;
                if(x<12||x>51||y<16||y>47)return ink;
                if(y>=35)return warm;
                if(y>=31&&y<=34)return ink;
                if(x>=27&&x<=37&&y>=24&&y<=40)return iv;
                return warm;
            });
            SpriteAsset("MapBoss",size,size,(x,y)=>
            {
                if(RectAt(x,y,11,10,52,20))return ink;
                if(RectAt(x,y,14,14,49,19))return rose;
                float ax=Mathf.Abs(x-31.5f);float upper=ax<8?56-ax*.8f:46-Mathf.Abs(ax-20)*1.2f;
                if(x>=9&&x<=54&&y>=22&&y<=upper)return y==22||x<12||x>51?ink:rose;
                return Color.clear;
            });
        }
    }
}
