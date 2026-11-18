using System.Collections.Generic;
using System.IO;
using LightSouls.Common;
using LightSouls.Lobby;
using LightSouls.Lobby.Testing;
using LightSouls.MapGeneration.Hazards;
using LightSouls.MapGeneration.Testing;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LightSouls.MapGeneration.Editor
{
    /// <summary>
    /// Editor builder tạo mới các asset địa hình đặc biệt, 4 prefab phòng mới,
    /// hệ thống Sảnh (Lobby), catalog ngục, và 2 Scene kiểm thử: HazardRoomsTest và LobbyTest.
    /// Tuân thủ quy ước: không ghi đè prefab cũ của đợt trước.
    /// </summary>
    public static class HazardRoomsAndLobbyBuilder
    {
        private const string MaterialsRoot = "Assets/Prefabs/MapGeneration/Materials";
        private const string HazardsPrefabRoot = "Assets/Prefabs/MapGeneration/Hazards";
        private const string RoomsRoot = "Assets/Prefabs/MapGeneration/Rooms";
        private const string LobbyPrefabRoot = "Assets/Prefabs/Lobby";
        private const string TemplatesRoot = "Assets/ScriptableObjects/MapGeneration/Templates";
        private const string ConfigRoot = "Assets/ScriptableObjects/MapGeneration/Config";
        private const string LobbySoRoot = "Assets/ScriptableObjects/Lobby";
        private const string LobbyTestingSoRoot = "Assets/ScriptableObjects/Lobby/Testing";
        private const string PlayerPrefabPath = "Assets/Prefabs/Combat/Player_Character.prefab";
        private const string TouchControlsPrefabPath = "Assets/Prefabs/Combat/Combat_TouchControls.prefab";
        private const string BackpackPrefabPath = "Assets/Prefabs/Combat/Combat_Backpack.prefab";

        private const float WallHeight = 3.5f;
        private const float WallThickness = 0.5f;
        private const float FloorThickness = 0.5f;
        private const float DoorWidth = 4.0f;
        private const float DoorHeight = 3.0f;

        [MenuItem("LightSouls/Map Generation/Build Hazard Rooms & Lobby (Task 2026-11-14)")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[HazardRoomsAndLobbyBuilder] Dừng Play Mode trước khi tạo asset.");
                return;
            }

            EditorSettings.serializationMode = SerializationMode.ForceText;

            EnsureFolder(MaterialsRoot);
            EnsureFolder(HazardsPrefabRoot);
            EnsureFolder(RoomsRoot);
            EnsureFolder(LobbyPrefabRoot);
            EnsureFolder(TemplatesRoot);
            EnsureFolder(ConfigRoot);
            EnsureFolder(LobbySoRoot);
            EnsureFolder(LobbyTestingSoRoot);
            EnsureFolder("Assets/Scenes");

            // 1. Materials
            Material matLava = CreateUrpMaterial("Mat_Hazard_Lava", new Color(1.0f, 0.40f, 0.05f), isEmissive: true, emissionColor: new Color(1.8f, 0.6f, 0.1f));
            Material matWater = CreateUrpMaterial("Mat_Hazard_Water", new Color(0.12f, 0.60f, 0.92f, 0.75f));
            Material matSpike = CreateUrpMaterial("Mat_Hazard_Spike", new Color(0.42f, 0.44f, 0.48f));
            Material matWarning = CreateUrpMaterial("Mat_Hazard_Warning", new Color(0.92f, 0.22f, 0.12f));
            Material matMazeWall = CreateUrpMaterial("Mat_Maze_Wall", new Color(0.38f, 0.42f, 0.48f));
            Material matBarrel = CreateUrpMaterial("Mat_Hazard_Barrel", new Color(0.85f, 0.22f, 0.12f));

            Material matWall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Wall.mat");
            Material matDoorFrame = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_DoorFrame.mat");
            Material matDoorGateLocked = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_DoorGate_Locked.mat");
            Material matFloorArena = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Floor_Arena.mat");
            Material matFloorNormal = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Floor_Normal.mat");
            Material matEnemy = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Marker_Enemy.mat");
            Material matLoot = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Marker_Loot.mat");

            // 2. Hazard Prefabs
            GameObject prefabHazardLava = BuildHazardLavaPrefab(matLava);
            GameObject prefabHazardWater = BuildHazardWaterPrefab(matWater);
            GameObject prefabHazardSpike = BuildHazardSpikePrefab(matSpike, matWarning);
            GameObject prefabHazardBarrel = BuildHazardBarrelPrefab(matBarrel);

            // 3. New Combat Room Prefabs (Procedural Tactical Encounter Generation)
            RoomModule roomLava = BuildLavaArenaRoom(matFloorArena, matWall, matDoorFrame, matEnemy, matLoot, prefabHazardLava, prefabHazardWater, prefabHazardSpike, prefabHazardBarrel);
            RoomModule roomWater = BuildWaterPondRoom(matFloorNormal, matWall, matDoorFrame, matEnemy, matLoot, prefabHazardLava, prefabHazardWater, prefabHazardSpike, prefabHazardBarrel);
            RoomModule roomSpikes = BuildSpikeTrapsRoom(matFloorNormal, matWall, matDoorFrame, matEnemy, matLoot, prefabHazardLava, prefabHazardWater, prefabHazardSpike, prefabHazardBarrel);
            RoomModule roomMaze = BuildMazeLargeRoom(matFloorNormal, matWall, matDoorFrame, matEnemy, matLoot, prefabHazardLava, prefabHazardWater, prefabHazardSpike, prefabHazardBarrel);

            // 4. Room Templates
            RoomTemplateSO tLava = CreateOrLoadAsset<RoomTemplateSO>($"{TemplatesRoot}/Template_Room_Lava_Arena.asset");
            tLava.Configure("Template_Room_Lava_Arena", roomLava, weight: 0.9f, min: 0, max: 2, rotate: true);
            EditorUtility.SetDirty(tLava);

            RoomTemplateSO tWater = CreateOrLoadAsset<RoomTemplateSO>($"{TemplatesRoot}/Template_Room_Water_Pond.asset");
            tWater.Configure("Template_Room_Water_Pond", roomWater, weight: 1.0f, min: 0, max: 2, rotate: true);
            EditorUtility.SetDirty(tWater);

            RoomTemplateSO tSpikes = CreateOrLoadAsset<RoomTemplateSO>($"{TemplatesRoot}/Template_Room_Spike_Traps.asset");
            tSpikes.Configure("Template_Room_Spike_Traps", roomSpikes, weight: 1.0f, min: 0, max: 2, rotate: true);
            EditorUtility.SetDirty(tSpikes);

            RoomTemplateSO tMaze = CreateOrLoadAsset<RoomTemplateSO>($"{TemplatesRoot}/Template_Room_Maze_Large.asset");
            tMaze.Configure("Template_Room_Maze_Large", roomMaze, weight: 0.8f, min: 0, max: 2, rotate: true);
            EditorUtility.SetDirty(tMaze);

            // 5. Update DefaultDungeonConfig & Create Specialized Dungeon Configs
            DungeonGenerationConfigSO defaultConfig = AssetDatabase.LoadAssetAtPath<DungeonGenerationConfigSO>($"{ConfigRoot}/DefaultDungeonConfig.asset");
            if (defaultConfig != null)
            {
                var currentNormals = new List<RoomTemplateSO>(defaultConfig.NormalRooms);
                AddIfMissing(currentNormals, tLava);
                AddIfMissing(currentNormals, tWater);
                AddIfMissing(currentNormals, tSpikes);
                AddIfMissing(currentNormals, tMaze);

                defaultConfig.ConfigurePools(
                    new List<RoomTemplateSO>(defaultConfig.StartRooms),
                    new List<RoomTemplateSO>(defaultConfig.CorridorRooms),
                    currentNormals,
                    new List<RoomTemplateSO>(defaultConfig.SpecialBranchRooms),
                    new List<RoomTemplateSO>(defaultConfig.ExitRooms)
                );
                EditorUtility.SetDirty(defaultConfig);
            }

            // Config Ngục Dung Nham
            DungeonGenerationConfigSO lavaConfig = CreateOrLoadAsset<DungeonGenerationConfigSO>($"{ConfigRoot}/Config_LavaDungeon.asset");
            lavaConfig.ConfigurePools(
                new List<RoomTemplateSO>(defaultConfig.StartRooms),
                new List<RoomTemplateSO>(defaultConfig.CorridorRooms),
                new List<RoomTemplateSO> { tLava, tSpikes },
                new List<RoomTemplateSO>(defaultConfig.SpecialBranchRooms),
                new List<RoomTemplateSO>(defaultConfig.ExitRooms)
            );
            lavaConfig.TargetRoomCount = 11;
            lavaConfig.MinMainPathLength = 4;
            EditorUtility.SetDirty(lavaConfig);

            // Config Ngục Mê Cung
            DungeonGenerationConfigSO mazeConfig = CreateOrLoadAsset<DungeonGenerationConfigSO>($"{ConfigRoot}/Config_MazeDungeon.asset");
            mazeConfig.ConfigurePools(
                new List<RoomTemplateSO>(defaultConfig.StartRooms),
                new List<RoomTemplateSO>(defaultConfig.CorridorRooms),
                new List<RoomTemplateSO> { tMaze, tWater, tSpikes },
                new List<RoomTemplateSO>(defaultConfig.SpecialBranchRooms),
                new List<RoomTemplateSO>(defaultConfig.ExitRooms)
            );
            mazeConfig.TargetRoomCount = 12;
            mazeConfig.MinMainPathLength = 5;
            EditorUtility.SetDirty(mazeConfig);

            // 6. Dungeon Catalog SO
            DungeonCatalogSO dungeonCatalog = CreateOrLoadAsset<DungeonCatalogSO>($"{ConfigRoot}/DungeonCatalog.asset");
            var entriesField = typeof(DungeonCatalogSO).GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (entriesField != null)
            {
                var entriesList = new List<DungeonEntry>
                {
                    new DungeonEntry
                    {
                        dungeonId = "dungeon.standard",
                        displayName = "Ngục Tiêu Chuẩn",
                        description = "Ngục ngẫu nhiên cân bằng đầy đủ các loại phòng chiến đấu và địa hình.",
                        config = defaultConfig
                    },
                    new DungeonEntry
                    {
                        dungeonId = "dungeon.lava",
                        displayName = "Vực Dung Nham",
                        description = "Vùng biển dung nham rực cháy, yêu cầu né tránh cẩn thận và chiến đấu liên tục.",
                        config = lavaConfig
                    },
                    new DungeonEntry
                    {
                        dungeonId = "dungeon.maze",
                        displayName = "Mê Cung Thử Thách",
                        description = "Mê cung cổ đại với các vách đá quanh co, hào nước làm chậm và bẫy rải rác.",
                        config = mazeConfig
                    }
                };
                entriesField.SetValue(dungeonCatalog, entriesList);
                EditorUtility.SetDirty(dungeonCatalog);
            }

            // 7. Lobby Test Items & Shop Catalog
            TestItemDefinitionSO itemSword = CreateTestItem("item.weapon.iron_sword", "Kiếm Sắt Thử Nghiệm", "Vũ khí cận chiến sắc bén.", 50, ItemCategory.Weapon);
            TestItemDefinitionSO itemStaff = CreateTestItem("item.weapon.fire_staff", "Gậy Phép Lửa", "Vũ khí tầm xa bắn cầu lửa.", 120, ItemCategory.Weapon);
            TestItemDefinitionSO itemArmor = CreateTestItem("item.armor.leather_vest", "Áo Giáp Da", "Trang bị phòng thủ tăng chống chịu.", 80, ItemCategory.Equipment);
            TestItemDefinitionSO itemPotion = CreateTestItem("item.potion.health", "Bình Máu", "Hồi phục 50 sinh lực khi sử dụng.", 25, ItemCategory.Consumable, maxStack: 5);

            ShopCatalogSO shopCatalog = CreateOrLoadAsset<ShopCatalogSO>($"{LobbySoRoot}/ShopCatalog.asset");
            shopCatalog.SetItems(new List<ItemDefinitionSO> { itemSword, itemStaff, itemArmor, itemPotion });
            EditorUtility.SetDirty(shopCatalog);

            // 8. Lobby Prefabs
            BuildLobbyInteractablePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 9. Build Test Scenes
            BuildHazardRoomsTestScene(roomLava.gameObject, roomWater.gameObject, roomSpikes.gameObject, roomMaze.gameObject);
            // Sảnh thử dùng nhân vật thật, nên bán item thật (ShopCatalog_Main) nếu đã có; item thử chỉ là dự phòng.
            ShopCatalogSO mainShopCatalog = AssetDatabase.LoadAssetAtPath<ShopCatalogSO>($"{LobbySoRoot}/ShopCatalog_Main.asset");
            BuildLobbyTestScene(mainShopCatalog != null ? mainShopCatalog : shopCatalog, dungeonCatalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[HazardRoomsAndLobbyBuilder] Hoàn thành xây dựng toàn bộ Phòng địa hình nguy hiểm, Hệ thống Sảnh và 2 Scene kiểm thử!");
        }

        private static GameObject BuildHazardLavaPrefab(Material matLava)
        {
            var root = new GameObject("Hazard_Lava");
            root.layer = HazardZoneBase.HazardLayer;

            // Trigger detection collider (Layer 11)
            var boxCol = root.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 0.4f, 0f);
            boxCol.size = new Vector3(4f, 0.8f, 4f);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            root.AddComponent<LavaHazardZone>();

            // NavMesh Area: Lava (cost 20)
            var navModifier = root.AddComponent<NavMeshModifier>();
            navModifier.overrideArea = true;
            navModifier.area = 4; // Area 4 = Lava

            // Floor collider ở Layer 8 (Environment) để đứng được
            int envLayer = LayerMask.NameToLayer("Environment");
            GameObject floorColObj = new GameObject("FloorCollider");
            floorColObj.transform.SetParent(root.transform, false);
            if (envLayer >= 0) floorColObj.layer = envLayer;
            var floorCol = floorColObj.AddComponent<BoxCollider>();
            floorCol.center = new Vector3(0f, -0.25f, 0f);
            floorCol.size = new Vector3(4f, 0.5f, 4f);

            // Visual bề mặt lava
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "LavaVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            visual.transform.localScale = new Vector3(4f, 0.1f, 4f);
            if (visual.GetComponent<Collider>() != null) Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = matLava;

            string path = $"{HazardsPrefabRoot}/Hazard_Lava.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildHazardWaterPrefab(Material matWater)
        {
            var root = new GameObject("Hazard_Water");
            root.layer = HazardZoneBase.HazardLayer;

            var boxCol = root.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 0.4f, 0f);
            boxCol.size = new Vector3(4f, 0.8f, 4f);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            root.AddComponent<WaterHazardZone>();

            // NavMesh Area: Water (cost 3)
            var navModifier = root.AddComponent<NavMeshModifier>();
            navModifier.overrideArea = true;
            navModifier.area = 3; // Area 3 = Water

            int envLayer = LayerMask.NameToLayer("Environment");
            GameObject floorColObj = new GameObject("FloorCollider");
            floorColObj.transform.SetParent(root.transform, false);
            if (envLayer >= 0) floorColObj.layer = envLayer;
            var floorCol = floorColObj.AddComponent<BoxCollider>();
            floorCol.center = new Vector3(0f, -0.25f, 0f);
            floorCol.size = new Vector3(4f, 0.5f, 4f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "WaterVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            visual.transform.localScale = new Vector3(4f, 0.08f, 4f);
            if (visual.GetComponent<Collider>() != null) Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = matWater;

            string path = $"{HazardsPrefabRoot}/Hazard_Water.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildHazardSpikePrefab(Material matSpike, Material matWarning)
        {
            var root = new GameObject("Hazard_Spikes");
            root.layer = HazardZoneBase.HazardLayer;

            var boxCol = root.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 0.4f, 0f);
            boxCol.size = new Vector3(3f, 0.8f, 3f);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var hazard = root.AddComponent<SpikeTrapHazard>();

            // Tấm cảnh báo (Warning Plate)
            int envLayer = LayerMask.NameToLayer("Environment");
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "WarningPlate";
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            plate.transform.localScale = new Vector3(3f, 0.06f, 3f);
            if (envLayer >= 0) plate.layer = envLayer;
            plate.GetComponent<Renderer>().sharedMaterial = matWarning;

            // Cụm gai nhọn (Spikes)
            var spikesRoot = new GameObject("Spikes");
            spikesRoot.transform.SetParent(root.transform, false);
            spikesRoot.transform.localPosition = new Vector3(0f, -0.4f, 0f);

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    spike.name = $"Spike_{x}_{z}";
                    spike.transform.SetParent(spikesRoot.transform, false);
                    spike.transform.localPosition = new Vector3(x * 0.9f, 0.35f, z * 0.9f);
                    spike.transform.localScale = new Vector3(0.2f, 0.7f, 0.2f);
                    if (spike.GetComponent<Collider>() != null) Object.DestroyImmediate(spike.GetComponent<Collider>());
                    spike.GetComponent<Renderer>().sharedMaterial = matSpike;
                }
            }

            var spikesField = typeof(SpikeTrapHazard).GetField("spikesTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (spikesField != null) spikesField.SetValue(hazard, spikesRoot.transform);

            var plateField = typeof(SpikeTrapHazard).GetField("warningPlateRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (plateField != null) plateField.SetValue(hazard, plate.GetComponent<Renderer>());

            string path = $"{HazardsPrefabRoot}/Hazard_Spikes.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildHazardBarrelPrefab(Material matBarrel)
        {
            var root = new GameObject("Hazard_ExplosiveBarrel");
            int envLayer = LayerMask.NameToLayer("Environment");
            if (envLayer >= 0) root.layer = envLayer;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.55f, 0f);
            col.radius = 0.4f;
            col.height = 1.1f;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var hazard = root.AddComponent<ExplosiveBarrelHazard>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "BarrelVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            visual.transform.localScale = new Vector3(0.8f, 0.55f, 0.8f);
            if (visual.GetComponent<Collider>() != null) Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = matBarrel;

            var rendField = typeof(ExplosiveBarrelHazard).GetField("barrelRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rendField != null) rendField.SetValue(hazard, visual.GetComponent<Renderer>());

            string path = $"{HazardsPrefabRoot}/Hazard_ExplosiveBarrel.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static RoomModule BuildLavaArenaRoom(Material floorMat, Material wallMat, Material doorFrameMat, Material enemyMat, Material lootMat, GameObject prefabLava, GameObject prefabWater, GameObject prefabSpikes, GameObject prefabBarrel)
        {
            float width = 24f;
            float length = 24f;
            var sockets = new[] { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

            var root = new GameObject("Room_Lava_Arena");
            Transform wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root.transform, false);
            Transform socketsRoot = new GameObject("Sockets").transform;
            socketsRoot.SetParent(root.transform, false);

            // Sàn chính
            GameObject floor = CreatePrimitiveChild("Floor", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -FloorThickness * 0.5f, 0f), new Vector3(width, FloorThickness, length), floorMat, true);

            // Tường bao quanh và 4 cửa
            var socketSet = new HashSet<DoorDirection>(sockets);
            foreach (DoorDirection d in sockets)
            {
                BuildWallSide(d, width, length, socketSet.Contains(d), wallsRoot, socketsRoot, wallMat, doorFrameMat);
            }

            // RoomModule & Lifecycle
            var roomModule = root.AddComponent<RoomModule>();
            Bounds localBounds = new Bounds(new Vector3(0f, (WallHeight - FloorThickness) * 0.5f, 0f), new Vector3(width, WallHeight + FloorThickness, length));

            var interiorTrigger = root.AddComponent<BoxCollider>();
            interiorTrigger.isTrigger = true;
            interiorTrigger.center = new Vector3(0f, 1.25f, 0f);
            interiorTrigger.size = new Vector3(width - 4.4f, 2.5f, length - 4.4f);

            var lifecycleCtrl = root.AddComponent<RoomLifecycleController>();
            lifecycleCtrl.Configure(roomModule, interiorTrigger);

            roomModule.Configure(RoomType.Combat, localBounds, null, null, null, null, null, null, lifecycleCtrl);

            // Gắn Procedural Layout Generator & Visual Debugger
            var applicator = root.AddComponent<ProceduralRoomApplicator>();
            applicator.Theme = TerrainTheme.Lava;
            applicator.CustomSeed = 101;
            applicator.ConfigurePrefabs(prefabLava, prefabWater, prefabSpikes, prefabBarrel, wallMat, enemyMat, lootMat);
            applicator.GenerateAndApply(101);

            var debugger = root.AddComponent<ProceduralRoomDebugger>();
            debugger.UpdateStatusFromApplicator();

            ApplyEnvironmentLayer(root);

            string path = $"{RoomsRoot}/Room_Lava_Arena.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<RoomModule>();
        }

        private static RoomModule BuildWaterPondRoom(Material floorMat, Material wallMat, Material doorFrameMat, Material enemyMat, Material lootMat, GameObject prefabLava, GameObject prefabWater, GameObject prefabSpikes, GameObject prefabBarrel)
        {
            float width = 16f;
            float length = 16f;
            var sockets = new[] { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

            var root = new GameObject("Room_Water_Pond");
            Transform wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root.transform, false);
            Transform socketsRoot = new GameObject("Sockets").transform;
            socketsRoot.SetParent(root.transform, false);

            GameObject floor = CreatePrimitiveChild("Floor", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -FloorThickness * 0.5f, 0f), new Vector3(width, FloorThickness, length), floorMat, true);

            var socketSet = new HashSet<DoorDirection>(sockets);
            foreach (DoorDirection d in sockets)
            {
                BuildWallSide(d, width, length, socketSet.Contains(d), wallsRoot, socketsRoot, wallMat, doorFrameMat);
            }

            var roomModule = root.AddComponent<RoomModule>();
            Bounds localBounds = new Bounds(new Vector3(0f, (WallHeight - FloorThickness) * 0.5f, 0f), new Vector3(width, WallHeight + FloorThickness, length));

            var interiorTrigger = root.AddComponent<BoxCollider>();
            interiorTrigger.isTrigger = true;
            interiorTrigger.center = new Vector3(0f, 1.25f, 0f);
            interiorTrigger.size = new Vector3(width - 4.4f, 2.5f, length - 4.4f);

            var lifecycleCtrl = root.AddComponent<RoomLifecycleController>();
            lifecycleCtrl.Configure(roomModule, interiorTrigger);

            roomModule.Configure(RoomType.Combat, localBounds, null, null, null, null, null, null, lifecycleCtrl);

            var applicator = root.AddComponent<ProceduralRoomApplicator>();
            applicator.Theme = TerrainTheme.Water;
            applicator.CustomSeed = 102;
            applicator.ConfigurePrefabs(prefabLava, prefabWater, prefabSpikes, prefabBarrel, wallMat, enemyMat, lootMat);
            applicator.GenerateAndApply(102);

            var debugger = root.AddComponent<ProceduralRoomDebugger>();
            debugger.UpdateStatusFromApplicator();

            ApplyEnvironmentLayer(root);

            string path = $"{RoomsRoot}/Room_Water_Pond.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<RoomModule>();
        }

        private static RoomModule BuildSpikeTrapsRoom(Material floorMat, Material wallMat, Material doorFrameMat, Material enemyMat, Material lootMat, GameObject prefabLava, GameObject prefabWater, GameObject prefabSpikes, GameObject prefabBarrel)
        {
            float width = 16f;
            float length = 16f;
            var sockets = new[] { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

            var root = new GameObject("Room_Spike_Traps");
            Transform wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root.transform, false);
            Transform socketsRoot = new GameObject("Sockets").transform;
            socketsRoot.SetParent(root.transform, false);

            GameObject floor = CreatePrimitiveChild("Floor", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -FloorThickness * 0.5f, 0f), new Vector3(width, FloorThickness, length), floorMat, true);

            var socketSet = new HashSet<DoorDirection>(sockets);
            foreach (DoorDirection d in sockets)
            {
                BuildWallSide(d, width, length, socketSet.Contains(d), wallsRoot, socketsRoot, wallMat, doorFrameMat);
            }

            var roomModule = root.AddComponent<RoomModule>();
            Bounds localBounds = new Bounds(new Vector3(0f, (WallHeight - FloorThickness) * 0.5f, 0f), new Vector3(width, WallHeight + FloorThickness, length));

            var interiorTrigger = root.AddComponent<BoxCollider>();
            interiorTrigger.isTrigger = true;
            interiorTrigger.center = new Vector3(0f, 1.25f, 0f);
            interiorTrigger.size = new Vector3(width - 4.4f, 2.5f, length - 4.4f);

            var lifecycleCtrl = root.AddComponent<RoomLifecycleController>();
            lifecycleCtrl.Configure(roomModule, interiorTrigger);

            roomModule.Configure(RoomType.Combat, localBounds, null, null, null, null, null, null, lifecycleCtrl);

            var applicator = root.AddComponent<ProceduralRoomApplicator>();
            applicator.Theme = TerrainTheme.Spike;
            applicator.CustomSeed = 103;
            applicator.ConfigurePrefabs(prefabLava, prefabWater, prefabSpikes, prefabBarrel, wallMat, enemyMat, lootMat);
            applicator.GenerateAndApply(103);

            var debugger = root.AddComponent<ProceduralRoomDebugger>();
            debugger.UpdateStatusFromApplicator();

            ApplyEnvironmentLayer(root);

            string path = $"{RoomsRoot}/Room_Spike_Traps.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<RoomModule>();
        }

        private static RoomModule BuildMazeLargeRoom(Material floorMat, Material wallMat, Material doorFrameMat, Material enemyMat, Material lootMat, GameObject prefabLava, GameObject prefabWater, GameObject prefabSpikes, GameObject prefabBarrel)
        {
            float width = 24f;
            float length = 24f;
            var sockets = new[] { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

            var root = new GameObject("Room_Maze_Large");
            Transform wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root.transform, false);
            Transform socketsRoot = new GameObject("Sockets").transform;
            socketsRoot.SetParent(root.transform, false);

            GameObject floor = CreatePrimitiveChild("Floor", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -FloorThickness * 0.5f, 0f), new Vector3(width, FloorThickness, length), floorMat, true);

            var socketSet = new HashSet<DoorDirection>(sockets);
            foreach (DoorDirection d in sockets)
            {
                BuildWallSide(d, width, length, socketSet.Contains(d), wallsRoot, socketsRoot, wallMat, doorFrameMat);
            }

            var roomModule = root.AddComponent<RoomModule>();
            Bounds localBounds = new Bounds(new Vector3(0f, (WallHeight - FloorThickness) * 0.5f, 0f), new Vector3(width, WallHeight + FloorThickness, length));

            var interiorTrigger = root.AddComponent<BoxCollider>();
            interiorTrigger.isTrigger = true;
            interiorTrigger.center = new Vector3(0f, 1.25f, 0f);
            interiorTrigger.size = new Vector3(width - 4.4f, 2.5f, length - 4.4f);

            var lifecycleCtrl = root.AddComponent<RoomLifecycleController>();
            lifecycleCtrl.Configure(roomModule, interiorTrigger);

            roomModule.Configure(RoomType.Combat, localBounds, null, null, null, null, null, null, lifecycleCtrl);

            var applicator = root.AddComponent<ProceduralRoomApplicator>();
            applicator.Theme = TerrainTheme.LowCover;
            applicator.CustomSeed = 104;
            applicator.ConfigurePrefabs(prefabLava, prefabWater, prefabSpikes, prefabBarrel, wallMat, enemyMat, lootMat);
            applicator.GenerateAndApply(104);

            var debugger = root.AddComponent<ProceduralRoomDebugger>();
            debugger.UpdateStatusFromApplicator();

            ApplyEnvironmentLayer(root);

            string path = $"{RoomsRoot}/Room_Maze_Large.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<RoomModule>();
        }

        private static void BuildWallSide(DoorDirection dir, float roomWidth, float roomLength, bool hasDoor, Transform wallsRoot, Transform socketsRoot, Material wallMat, Material doorFrameMat)
        {
            float halfW = roomWidth * 0.5f;
            float halfL = roomLength * 0.5f;
            bool isNorthSouth = dir == DoorDirection.North || dir == DoorDirection.South;
            float wallSpan = isNorthSouth ? roomWidth : (roomLength - WallThickness * 2f);
            float sideSign = (dir == DoorDirection.North || dir == DoorDirection.East) ? 1f : -1f;

            Vector3 wallCenter = isNorthSouth
                ? new Vector3(0f, WallHeight * 0.5f, sideSign * (halfL - WallThickness * 0.5f))
                : new Vector3(sideSign * (halfW - WallThickness * 0.5f), WallHeight * 0.5f, 0f);

            Vector3 socketPos = isNorthSouth
                ? new Vector3(0f, 0f, sideSign * halfL)
                : new Vector3(sideSign * halfW, 0f, 0f);

            Quaternion wallRotation = isNorthSouth ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            if (!hasDoor)
            {
                GameObject wall = CreatePrimitiveChild($"Wall_{dir}", PrimitiveType.Cube, wallsRoot,
                    wallCenter, new Vector3(wallSpan, WallHeight, WallThickness), wallMat, true);
                wall.transform.localRotation = wallRotation;
                return;
            }

            float sideSegLength = (wallSpan - DoorWidth) * 0.5f;
            Vector3 alongAxis = isNorthSouth ? Vector3.right : Vector3.forward;

            if (sideSegLength > 0.05f)
            {
                float segOffset = DoorWidth * 0.5f + sideSegLength * 0.5f;
                GameObject leftSeg = CreatePrimitiveChild($"Wall_{dir}_Left", PrimitiveType.Cube, wallsRoot,
                    wallCenter - alongAxis * segOffset, new Vector3(sideSegLength, WallHeight, WallThickness), wallMat, true);
                leftSeg.transform.localRotation = wallRotation;

                GameObject rightSeg = CreatePrimitiveChild($"Wall_{dir}_Right", PrimitiveType.Cube, wallsRoot,
                    wallCenter + alongAxis * segOffset, new Vector3(sideSegLength, WallHeight, WallThickness), wallMat, true);
                rightSeg.transform.localRotation = wallRotation;
            }

            float lintelHeight = WallHeight - DoorHeight;
            if (lintelHeight > 0.05f)
            {
                Vector3 lintelPos = new Vector3(wallCenter.x, DoorHeight + lintelHeight * 0.5f, wallCenter.z);
                GameObject lintel = CreatePrimitiveChild($"Wall_{dir}_Lintel", PrimitiveType.Cube, wallsRoot,
                    lintelPos, new Vector3(DoorWidth, lintelHeight, WallThickness), wallMat, true);
                lintel.transform.localRotation = wallRotation;
            }

            var socketObj = new GameObject($"Socket_{dir}");
            socketObj.transform.SetParent(socketsRoot, false);
            socketObj.transform.localPosition = socketPos;
            socketObj.transform.localRotation = Quaternion.LookRotation(dir.ToVector3(), Vector3.up);

            var openVisual = new GameObject("OpenVisual");
            openVisual.transform.SetParent(socketObj.transform, false);

            GameObject archLeft = CreatePrimitiveChild("FrameLeft", PrimitiveType.Cube, openVisual.transform,
                new Vector3(-DoorWidth * 0.5f + 0.15f, DoorHeight * 0.5f, 0f), new Vector3(0.3f, DoorHeight, 0.4f), doorFrameMat, false);
            GameObject archRight = CreatePrimitiveChild("FrameRight", PrimitiveType.Cube, openVisual.transform,
                new Vector3(DoorWidth * 0.5f - 0.15f, DoorHeight * 0.5f, 0f), new Vector3(0.3f, DoorHeight, 0.4f), doorFrameMat, false);
            GameObject archTop = CreatePrimitiveChild("FrameTop", PrimitiveType.Cube, openVisual.transform,
                new Vector3(0f, DoorHeight - 0.15f, 0f), new Vector3(DoorWidth, 0.3f, 0.4f), doorFrameMat, false);

            GameObject closedVisual = CreatePrimitiveChild("ClosedWallVisual", PrimitiveType.Cube, socketObj.transform,
                new Vector3(0f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth, DoorHeight, WallThickness * 1.1f), wallMat, true);
            closedVisual.SetActive(false);

            Material matDoorGateLocked = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_DoorGate_Locked.mat");
            GameObject lockedGateVisual = CreatePrimitiveChild("LockedGateVisual", PrimitiveType.Cube, socketObj.transform,
                new Vector3(0f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth * 0.98f, DoorHeight * 0.95f, 0.25f), matDoorGateLocked, true);
            lockedGateVisual.SetActive(false);

            var doorSocket = socketObj.AddComponent<DoorSocket>();
            doorSocket.Configure($"Socket_{dir}", dir, new Vector2(DoorWidth, DoorHeight), openVisual, closedVisual, lockedGateVisual);

            var safeZone = socketObj.AddComponent<DoorwaySafeZone>();
            safeZone.Configure(dir, 2.5f, DoorWidth);
        }

        private static void BuildLobbyInteractablePrefabs()
        {
            Material matFrame = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_DoorFrame.mat");
            Material matScreen = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Vending_Screen.mat");
            Material matBody = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Vending_Body.mat");

            // 1. Quầy hàng Sảnh (Lobby_ShopCounter)
            var counterGo = new GameObject("Lobby_ShopCounter");
            counterGo.layer = LobbyShopCounter.InteractableLayer;

            var counterCol = counterGo.AddComponent<BoxCollider>();
            counterCol.isTrigger = true;
            counterCol.center = new Vector3(0f, 1.0f, 0f);
            counterCol.size = new Vector3(3.2f, 2.0f, 2.0f);

            var shopCounter = counterGo.AddComponent<LobbyShopCounter>();

            // Visual quầy hàng
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "CounterTable";
            table.transform.SetParent(counterGo.transform, false);
            table.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            table.transform.localScale = new Vector3(2.8f, 1.0f, 1.2f);
            table.GetComponent<Renderer>().sharedMaterial = matFrame;

            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "ShopSign";
            sign.transform.SetParent(counterGo.transform, false);
            sign.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            sign.transform.localScale = new Vector3(2.0f, 0.6f, 0.2f);
            sign.GetComponent<Renderer>().sharedMaterial = matScreen;

            PrefabUtility.SaveAsPrefabAsset(counterGo, $"{LobbyPrefabRoot}/Lobby_ShopCounter.prefab");
            Object.DestroyImmediate(counterGo);

            // 2. Cổng chọn ngục (Lobby_DungeonPortal)
            var portalGo = new GameObject("Lobby_DungeonPortal");
            portalGo.layer = DungeonSelectPortal.InteractableLayer;

            var portalCol = portalGo.AddComponent<BoxCollider>();
            portalCol.isTrigger = true;
            portalCol.center = new Vector3(0f, 1.5f, 0f);
            portalCol.size = new Vector3(3.5f, 3.0f, 2.5f);

            portalGo.AddComponent<DungeonSelectPortal>();

            GameObject archL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archL.name = "PillarLeft";
            archL.transform.SetParent(portalGo.transform, false);
            archL.transform.localPosition = new Vector3(-1.6f, 1.75f, 0f);
            archL.transform.localScale = new Vector3(0.6f, 3.5f, 0.6f);
            archL.GetComponent<Renderer>().sharedMaterial = matBody;

            GameObject archR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archR.name = "PillarRight";
            archR.transform.SetParent(portalGo.transform, false);
            archR.transform.localPosition = new Vector3(1.6f, 1.75f, 0f);
            archR.transform.localScale = new Vector3(0.6f, 3.5f, 0.6f);
            archR.GetComponent<Renderer>().sharedMaterial = matBody;

            GameObject archTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archTop.name = "ArchTop";
            archTop.transform.SetParent(portalGo.transform, false);
            archTop.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            archTop.transform.localScale = new Vector3(3.8f, 0.6f, 0.8f);
            archTop.GetComponent<Renderer>().sharedMaterial = matBody;

            GameObject portalScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portalScreen.name = "PortalEnergy";
            portalScreen.transform.SetParent(portalGo.transform, false);
            portalScreen.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            portalScreen.transform.localScale = new Vector3(2.6f, 2.9f, 1f);
            portalScreen.GetComponent<Renderer>().sharedMaterial = matScreen;

            PrefabUtility.SaveAsPrefabAsset(portalGo, $"{LobbyPrefabRoot}/Lobby_DungeonPortal.prefab");
            Object.DestroyImmediate(portalGo);
        }

        private static void BuildHazardRoomsTestScene(GameObject roomLava, GameObject roomWater, GameObject roomSpikes, GameObject roomMaze)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "HazardRoomsTest";

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
            camGo.transform.position = new Vector3(0f, 35f, -25f);
            camGo.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Đặt 4 phòng hazard liền kề để thử nghiệm trực quan
            var roomsContainer = new GameObject("HazardRooms");
            GameObject instLava = (GameObject)PrefabUtility.InstantiatePrefab(roomLava, roomsContainer.transform);
            instLava.transform.position = new Vector3(-15f, 0f, 15f);

            GameObject instWater = (GameObject)PrefabUtility.InstantiatePrefab(roomWater, roomsContainer.transform);
            instWater.transform.position = new Vector3(15f, 0f, 15f);

            GameObject instSpikes = (GameObject)PrefabUtility.InstantiatePrefab(roomSpikes, roomsContainer.transform);
            instSpikes.transform.position = new Vector3(-15f, 0f, -15f);

            GameObject instMaze = (GameObject)PrefabUtility.InstantiatePrefab(roomMaze, roomsContainer.transform);
            instMaze.transform.position = new Vector3(15f, 0f, -15f);

            // Nhân vật người chơi test
            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player_Character";
            playerGo.tag = "Player";
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) playerGo.layer = playerLayer;
            playerGo.transform.position = new Vector3(0f, 1f, 0f);
            playerGo.AddComponent<TestSpeedModifiableEntity>();

            // UI Test Guide
            var canvasGo = new GameObject("Canvas_TestHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            var safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeArea.transform.SetParent(canvasGo.transform, false);

            var guideGo = new GameObject("GuideText", typeof(RectTransform), typeof(Text));
            guideGo.transform.SetParent(safeArea.transform, false);
            var guideText = guideGo.GetComponent<Text>();
            guideText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            guideText.fontSize = 24;
            guideText.color = Color.white;
            guideText.alignment = TextAnchor.UpperLeft;
            guideText.text = "<b>SCENE THỬ NGHIỆM ĐỊA HÌNH NGUY HIỂM (HazardRoomsTest)</b>\n" +
                             "• Góc trên trái: Phòng Lava (trừ 8 máu/0.5s, quái né lava)\n" +
                             "• Góc trên phải: Phòng Nước (làm chậm tốc độ 0.5x)\n" +
                             "• Góc dưới trái: Phòng Bẫy Gai (báo trước 0.6s đổi màu đỏ rồi đâm)\n" +
                             "• Góc dưới phải: Phòng Mê Cung (vách đá chia lối, NavMesh thông)";
            var guideRt = (RectTransform)guideGo.transform;
            guideRt.anchorMin = new Vector2(0.02f, 0.75f);
            guideRt.anchorMax = new Vector2(0.6f, 0.98f);
            guideRt.offsetMin = Vector2.zero;
            guideRt.offsetMax = Vector2.zero;

            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/HazardRoomsTest.unity");
        }

        private static void BuildLobbyTestScene(ShopCatalogSO shopCatalog, DungeonCatalogSO dungeonCatalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LobbyTest";

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camGo.transform.position = new Vector3(0f, 12f, -12f);
            camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Sàn sảnh (20x20m)
            Material matFloor = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Floor_Start.mat");
            Material matWall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_Wall.mat");
            int envLayer = LayerMask.NameToLayer("Environment");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "LobbyFloor";
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
            floor.GetComponent<Renderer>().sharedMaterial = matFloor;
            if (envLayer >= 0) floor.layer = envLayer;

            // Tường bao quanh sảnh
            var wallsRoot = new GameObject("LobbyWalls");
            CreatePrimitiveChild("Wall_N", PrimitiveType.Cube, wallsRoot.transform, new Vector3(0f, 1.75f, 10f), new Vector3(20f, 3.5f, 0.5f), matWall, true);
            CreatePrimitiveChild("Wall_S", PrimitiveType.Cube, wallsRoot.transform, new Vector3(0f, 1.75f, -10f), new Vector3(20f, 3.5f, 0.5f), matWall, true);
            CreatePrimitiveChild("Wall_E", PrimitiveType.Cube, wallsRoot.transform, new Vector3(10f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 20f), matWall, true);
            CreatePrimitiveChild("Wall_W", PrimitiveType.Cube, wallsRoot.transform, new Vector3(-10f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 20f), matWall, true);
            if (envLayer >= 0) SetLayerRecursively(wallsRoot, envLayer);

            // Đặt Quầy hàng và Cổng chọn ngục
            GameObject shopCounterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LobbyPrefabRoot}/Lobby_ShopCounter.prefab");
            GameObject shopCounterInst = (GameObject)PrefabUtility.InstantiatePrefab(shopCounterPrefab);
            shopCounterInst.transform.position = new Vector3(-5f, 0f, 4f);

            GameObject portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LobbyPrefabRoot}/Lobby_DungeonPortal.prefab");
            GameObject portalInst = (GameObject)PrefabUtility.InstantiatePrefab(portalPrefab);
            portalInst.transform.position = new Vector3(5f, 0f, 4f);

            // Nhân vật người chơi thật (di chuyển, túi đồ, tương tác của Khải) cùng điều khiển cảm ứng và balo.
            // Chưa có prefab thật thì dùng khối trụ thử với TestInventory/TestInteractor như trước.
            GameObject realPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (realPlayerPrefab != null)
            {
                var playerInst = (GameObject)PrefabUtility.InstantiatePrefab(realPlayerPrefab);
                playerInst.transform.position = new Vector3(0f, 0.05f, -2f);

                foreach (string uiPrefabPath in new[] { TouchControlsPrefabPath, BackpackPrefabPath })
                {
                    GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(uiPrefabPath);
                    if (uiPrefab != null)
                    {
                        PrefabUtility.InstantiatePrefab(uiPrefab);
                    }
                }
            }
            else
            {
                var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerGo.name = "Player_Character";
                playerGo.tag = "Player";
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0) playerGo.layer = playerLayer;
                playerGo.transform.position = new Vector3(0f, 1f, -2f);

                playerGo.AddComponent<TestInventory>();
                playerGo.AddComponent<TestInteractor>();
            }

            // Công cụ cộng xu nhanh (phím C) để thử mua đồ.
            new GameObject("LobbyTestTools").AddComponent<CoinTester>();

            // UI Canvas cho Sảnh (Sort order 8 theo quy ước)
            var canvasGo = new GameObject("Canvas_LobbyUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8; // Sort Order 8: bảng mở đè lên màn chơi (quy ước 4.9)
            var lobbyScaler = canvasGo.GetComponent<CanvasScaler>();
            lobbyScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            lobbyScaler.referenceResolution = new Vector2(1920f, 1080f);
            lobbyScaler.matchWidthOrHeight = 0.5f;

            var safeAreaGo = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            var safeAreaRt = (RectTransform)safeAreaGo.transform;
            safeAreaRt.anchorMin = Vector2.zero;
            safeAreaRt.anchorMax = Vector2.one;
            safeAreaRt.offsetMin = Vector2.zero;
            safeAreaRt.offsetMax = Vector2.zero;

            // Bảng Cửa hàng (Shop UI Panel)
            var shopPanelGo = BuildShopPanel(safeAreaGo.transform, shopCatalog);
            shopPanelGo.SetActive(false);
            var shopUI = shopPanelGo.GetComponent<LobbyShopUI>();

            // Bảng Chọn ngục (Dungeon Select Panel)
            var dungeonPanelGo = BuildDungeonSelectPanel(safeAreaGo.transform, dungeonCatalog);
            dungeonPanelGo.SetActive(false);
            var dungeonSelectUI = dungeonPanelGo.GetComponent<DungeonSelectUI>();

            // Controller
            var lobbyCtrlGo = new GameObject("LobbyController");
            var lobbyCtrl = lobbyCtrlGo.AddComponent<LobbyController>();

            var shopField = typeof(LobbyController).GetField("shopUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (shopField != null) shopField.SetValue(lobbyCtrl, shopUI);

            var dungeonField = typeof(LobbyController).GetField("dungeonSelectUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dungeonField != null) dungeonField.SetValue(lobbyCtrl, dungeonSelectUI);

            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/LobbyTest.unity");
        }

        private static GameObject BuildShopPanel(Transform parent, ShopCatalogSO catalog)
        {
            var panel = new GameObject("Panel_Shop", typeof(RectTransform), typeof(Image), typeof(LobbyShopUI));
            panel.transform.SetParent(parent, false);

            var panelImg = panel.GetComponent<Image>();
            panelImg.color = new Color(0.1f, 0.12f, 0.16f, 0.95f);

            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.15f, 0.1f);
            rt.anchorMax = new Vector2(0.85f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Title
            var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panel.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "CỬA HÀNG VŨ KHÍ & TRANG BỊ";
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.05f, 0.88f);
            titleRt.anchorMax = new Vector2(0.7f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            // Coins text
            var coinsGo = new GameObject("CoinsText", typeof(RectTransform), typeof(Text));
            coinsGo.transform.SetParent(panel.transform, false);
            var coinsText = coinsGo.GetComponent<Text>();
            coinsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            coinsText.fontSize = 26;
            coinsText.fontStyle = FontStyle.Bold;
            coinsText.color = new Color(1f, 0.85f, 0.2f);
            coinsText.alignment = TextAnchor.MiddleRight;
            coinsText.text = "Xu: 0";
            var coinsRt = (RectTransform)coinsGo.transform;
            coinsRt.anchorMin = new Vector2(0.7f, 0.88f);
            coinsRt.anchorMax = new Vector2(0.88f, 0.98f);
            coinsRt.offsetMin = Vector2.zero;
            coinsRt.offsetMax = Vector2.zero;

            // Close button (cao tối thiểu 96 px)
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            var closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(0.9f, 0.88f);
            closeRt.anchorMax = new Vector2(0.98f, 0.98f);
            closeRt.offsetMin = Vector2.zero;
            closeRt.offsetMax = Vector2.zero;

            var closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            var closeText = closeTextGo.GetComponent<Text>();
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeText.fontSize = 28;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = Color.white;
            closeText.text = "X";
            var closeTextRt = (RectTransform)closeTextGo.transform;
            closeTextRt.anchorMin = Vector2.zero;
            closeTextRt.anchorMax = Vector2.one;
            closeTextRt.offsetMin = Vector2.zero;
            closeTextRt.offsetMax = Vector2.zero;

            // Status notification text
            var notifGo = new GameObject("NotificationText", typeof(RectTransform), typeof(Text));
            notifGo.transform.SetParent(panel.transform, false);
            var notifText = notifGo.GetComponent<Text>();
            notifText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            notifText.fontSize = 22;
            notifText.color = Color.yellow;
            notifText.alignment = TextAnchor.MiddleCenter;
            notifText.text = string.Empty;
            var notifRt = (RectTransform)notifGo.transform;
            notifRt.anchorMin = new Vector2(0.05f, 0.02f);
            notifRt.anchorMax = new Vector2(0.95f, 0.1f);
            notifRt.offsetMin = Vector2.zero;
            notifRt.offsetMax = Vector2.zero;

            // Vùng nội dung: LobbyShopUI tự dựng lưới cuộn được và bảng chi tiết bên trong khi chạy.
            var containerGo = new GameObject("ItemsContainer", typeof(RectTransform));
            containerGo.transform.SetParent(panel.transform, false);
            var cRt = (RectTransform)containerGo.transform;
            cRt.anchorMin = new Vector2(0.05f, 0.12f);
            cRt.anchorMax = new Vector2(0.95f, 0.86f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            var shopUI = panel.GetComponent<LobbyShopUI>();
            SetPrivateField(shopUI, "catalog", catalog);
            SetPrivateField(shopUI, "panelRoot", panel);
            SetPrivateField(shopUI, "coinsText", coinsText);
            SetPrivateField(shopUI, "statusNotificationText", notifText);
            SetPrivateField(shopUI, "itemsContainer", containerGo.transform);
            SetPrivateField(shopUI, "closeButton", closeGo.GetComponent<Button>());

            return panel;
        }

        private static GameObject BuildDungeonSelectPanel(Transform parent, DungeonCatalogSO catalog)
        {
            var panel = new GameObject("Panel_DungeonSelect", typeof(RectTransform), typeof(Image), typeof(DungeonSelectUI));
            panel.transform.SetParent(parent, false);

            var panelImg = panel.GetComponent<Image>();
            panelImg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);

            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.15f, 0.1f);
            rt.anchorMax = new Vector2(0.85f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panel.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "CỔNG DỊCH CHUYỂN — CHỌN NGỤC";
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.05f, 0.88f);
            titleRt.anchorMax = new Vector2(0.85f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            var closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(0.9f, 0.88f);
            closeRt.anchorMax = new Vector2(0.98f, 0.98f);
            closeRt.offsetMin = Vector2.zero;
            closeRt.offsetMax = Vector2.zero;

            var closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            var closeText = closeTextGo.GetComponent<Text>();
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeText.fontSize = 28;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = Color.white;
            closeText.text = "X";
            var closeTextRt = (RectTransform)closeTextGo.transform;
            closeTextRt.anchorMin = Vector2.zero;
            closeTextRt.anchorMax = Vector2.one;
            closeTextRt.offsetMin = Vector2.zero;
            closeTextRt.offsetMax = Vector2.zero;

            var notifGo = new GameObject("NotificationText", typeof(RectTransform), typeof(Text));
            notifGo.transform.SetParent(panel.transform, false);
            var notifText = notifGo.GetComponent<Text>();
            notifText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            notifText.fontSize = 22;
            notifText.color = Color.cyan;
            notifText.alignment = TextAnchor.MiddleCenter;
            notifText.text = string.Empty;
            var notifRt = (RectTransform)notifGo.transform;
            notifRt.anchorMin = new Vector2(0.05f, 0.02f);
            notifRt.anchorMax = new Vector2(0.95f, 0.1f);
            notifRt.offsetMin = Vector2.zero;
            notifRt.offsetMax = Vector2.zero;

            var containerGo = new GameObject("DungeonsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            containerGo.transform.SetParent(panel.transform, false);
            var vlg = containerGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var cRt = (RectTransform)containerGo.transform;
            cRt.anchorMin = new Vector2(0.05f, 0.12f);
            cRt.anchorMax = new Vector2(0.95f, 0.86f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            var selectUI = panel.GetComponent<DungeonSelectUI>();
            SetPrivateField(selectUI, "catalog", catalog);
            SetPrivateField(selectUI, "panelRoot", panel);
            SetPrivateField(selectUI, "statusNotificationText", notifText);
            SetPrivateField(selectUI, "dungeonListContainer", containerGo.transform);
            SetPrivateField(selectUI, "closeButton", closeGo.GetComponent<Button>());

            return panel;
        }

        private static TestItemDefinitionSO CreateTestItem(string id, string displayName, string desc, int price, ItemCategory category, int maxStack = 1)
        {
            string path = $"{LobbyTestingSoRoot}/Item_{id.Replace(".", "_")}.asset";
            TestItemDefinitionSO item = CreateOrLoadAsset<TestItemDefinitionSO>(path);

            SetPrivateField(item, "itemId", id);
            SetPrivateField(item, "displayName", displayName);
            SetPrivateField(item, "description", desc);
            SetPrivateField(item, "price", price);
            SetPrivateField(item, "maxStack", maxStack);
            item.Configure(id, displayName, desc, price, category);

            EditorUtility.SetDirty(item);
            return item;
        }

        private static Material CreateUrpMaterial(string name, Color baseColor, bool isEmissive = false, Color emissionColor = default)
        {
            string path = $"{MaterialsRoot}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = baseColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (isEmissive)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreatePrimitiveChild(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            if (mat != null && go.GetComponent<Renderer>() != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }

            if (!keepCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            return go;
        }

        private static void ApplyEnvironmentLayer(GameObject root)
        {
            int envLayer = LayerMask.NameToLayer("Environment");
            if (envLayer < 0) return;

            Transform floor = root.transform.Find("Floor");
            if (floor != null) SetLayerRecursively(floor.gameObject, envLayer);

            Transform walls = root.transform.Find("Walls");
            if (walls != null) SetLayerRecursively(walls.gameObject, envLayer);

            Transform sockets = root.transform.Find("Sockets");
            if (sockets != null) SetLayerRecursively(sockets.gameObject, envLayer);

            Transform mazeWalls = root.transform.Find("MazeWalls");
            if (mazeWalls != null) SetLayerRecursively(mazeWalls.gameObject, envLayer);
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            // Không ghi đè layer Hazard của các vùng trigger
            if (obj.layer != HazardZoneBase.HazardLayer && obj.layer != LobbyShopCounter.InteractableLayer)
            {
                obj.layer = layer;
            }
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void AddIfMissing(List<RoomTemplateSO> list, RoomTemplateSO item)
        {
            if (list != null && item != null && !list.Contains(item))
            {
                list.Add(item);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            System.Reflection.FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                type = type.BaseType;
            }
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
