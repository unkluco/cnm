using System.Collections.Generic;
using System.IO;
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
    /// One-click editor builder that generates all prototype materials, modular room prefabs,
    /// ScriptableObject templates/channels/config, and the MapGenTest scene.
    /// </summary>
    public static class MapGenPrototypeBuilder
    {
        private const string MaterialsRoot = "Assets/Prefabs/MapGeneration/Materials";
        private const string RoomsRoot = "Assets/Prefabs/MapGeneration/Rooms";
        private const string TemplatesRoot = "Assets/ScriptableObjects/MapGeneration/Templates";
        private const string ConfigRoot = "Assets/ScriptableObjects/MapGeneration/Config";
        private const string ChannelsRoot = "Assets/ScriptableObjects/MapGeneration/Channels";
        private const string TestScenePath = "Assets/Scenes/MapGenTest.unity";

        private const float WallHeight = 3.5f;
        private const float WallThickness = 0.5f;
        private const float FloorThickness = 0.5f;
        private const float DoorWidth = 4.0f;
        private const float DoorHeight = 3.0f;

        [MenuItem("LightSouls/Map Generation/Rebuild Prototype Assets & MapGenTest Scene")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[MapGenPrototypeBuilder] Exit Play Mode before rebuilding prototype assets and scene.");
                return;
            }

            EditorSettings.serializationMode = SerializationMode.ForceText;

            EnsureFolder(MaterialsRoot);
            EnsureFolder(RoomsRoot);
            EnsureFolder(TemplatesRoot);
            EnsureFolder(ConfigRoot);
            EnsureFolder(ChannelsRoot);
            EnsureFolder("Assets/Scenes");

            // 1. Materials
            Material matFloorNormal = CreateUrpMaterial("Mat_Floor_Normal", new Color(0.24f, 0.27f, 0.33f));
            Material matFloorStart = CreateUrpMaterial("Mat_Floor_Start", new Color(0.18f, 0.45f, 0.24f));
            Material matFloorExit = CreateUrpMaterial("Mat_Floor_Exit", new Color(0.52f, 0.14f, 0.34f));
            Material matFloorTreasure = CreateUrpMaterial("Mat_Floor_Treasure", new Color(0.65f, 0.50f, 0.15f));
            Material matFloorArena = CreateUrpMaterial("Mat_Floor_Arena", new Color(0.32f, 0.22f, 0.19f));
            Material matFloorCorridor = CreateUrpMaterial("Mat_Floor_Corridor", new Color(0.27f, 0.35f, 0.40f));
            Material matWall = CreateUrpMaterial("Mat_Wall", new Color(0.56f, 0.60f, 0.68f));
            Material matDoorFrame = CreateUrpMaterial("Mat_DoorFrame", new Color(0.85f, 0.60f, 0.22f));
            Material matDoorGateLocked = CreateUrpMaterial("Mat_DoorGate_Locked", new Color(0.95f, 0.18f, 0.15f));
            Material matSpawn = CreateUrpMaterial("Mat_Marker_Spawn", new Color(0.1f, 0.92f, 0.42f));
            Material matEnemy = CreateUrpMaterial("Mat_Marker_Enemy", new Color(0.95f, 0.34f, 0.12f));
            Material matExit = CreateUrpMaterial("Mat_Marker_Exit", new Color(0.90f, 0.25f, 0.95f));
            Material matLoot = CreateUrpMaterial("Mat_Marker_Loot", new Color(1.0f, 0.85f, 0.15f));
            Material matPathLine = CreatePathLineMaterial("Mat_PathLine", new Color(1.0f, 0.85f, 0.15f));

            // Vending Machine Materials
            Material matVendingBody = CreateUrpMaterial("Mat_Vending_Body", new Color(0.12f, 0.15f, 0.22f));
            Material matVendingScreen = CreateUrpMaterial("Mat_Vending_Screen", new Color(0.1f, 0.95f, 0.95f));
            Material matVendingTray = CreateUrpMaterial("Mat_Vending_Tray", new Color(0.95f, 0.65f, 0.15f));
            Material matVendingSign = CreateUrpMaterial("Mat_Vending_Sign", new Color(1.0f, 0.85f, 0.15f));

            // 2. Room Prefabs (Every standard room has 4 cardinal sockets N/E/S/W; unused directions seal with SolidWall)
            DoorDirection[] allFourCardinalSockets =
            {
                DoorDirection.North,
                DoorDirection.East,
                DoorDirection.South,
                DoorDirection.West
            };

            RoomModule roomStart = BuildRoomPrefab(
                "Room_Start", RoomType.Start, 16f, 16f,
                allFourCardinalSockets,
                matFloorStart, matWall, matDoorFrame,
                hasPlayerSpawn: true, hasExit: false,
                enemyOffsets: null,
                lootOffsets: null,
                hasArenaPillars: false,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            RoomModule roomCross = BuildRoomPrefab(
                "Room_Cross_4Way", RoomType.Normal, 16f, 16f,
                allFourCardinalSockets,
                matFloorNormal, matWall, matDoorFrame,
                hasPlayerSpawn: false, hasExit: false,
                enemyOffsets: new[]
                {
                    new Vector3(-3.8f, 0f, 3.8f),
                    new Vector3(3.8f, 0f, -3.8f),
                    new Vector3(3.8f, 0f, 3.8f)
                },
                lootOffsets: null,
                hasArenaPillars: false,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            // Compact Straight Gateway Corridor (6m wide x 8m long, No Monsters — prevents 3D dead-time walking)
            RoomModule roomCorridor = BuildRoomPrefab(
                "Room_Corridor_Straight", RoomType.Corridor, 6f, 8f,
                new[] { DoorDirection.North, DoorDirection.South },
                matFloorCorridor, matWall, matDoorFrame,
                hasPlayerSpawn: false, hasExit: false,
                enemyOffsets: null,
                lootOffsets: null,
                hasArenaPillars: false,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            // Corner Corridor L (Kept as asset for legacy compatibility, not used in straight 2D grid config)
            RoomModule roomCorridorCorner = BuildRoomPrefab(
                "Room_Corridor_Corner_L", RoomType.Corridor, 10f, 10f,
                new[] { DoorDirection.North, DoorDirection.East },
                matFloorCorridor, matWall, matDoorFrame,
                hasPlayerSpawn: false, hasExit: false,
                enemyOffsets: null,
                lootOffsets: null,
                hasArenaPillars: false,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            RoomModule roomCorner = BuildRoomPrefab(
                "Room_Corner_L", RoomType.Normal, 16f, 16f,
                allFourCardinalSockets,
                matFloorNormal, matWall, matDoorFrame,
                hasPlayerSpawn: false, hasExit: false,
                enemyOffsets: new[]
                {
                    new Vector3(-3.8f, 0f, -3.8f),
                    new Vector3(3.8f, 0f, 3.8f)
                },
                lootOffsets: null,
                hasArenaPillars: false,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            // Elite / Champion Combat Arena (20x20 with waist-high 3D tactical cover and 4 enemy spawns outside Doorway Safe Zone)
            RoomModule roomArena = BuildRoomPrefab(
                "Room_Large_Arena", RoomType.Combat, 20f, 20f,
                allFourCardinalSockets,
                matFloorArena, matWall, matDoorFrame,
                hasPlayerSpawn: false, hasExit: false,
                enemyOffsets: new[]
                {
                    new Vector3(-5.2f, 0f, 5.2f),
                    new Vector3(5.2f, 0f, 5.2f),
                    new Vector3(-5.2f, 0f, -5.2f),
                    new Vector3(5.2f, 0f, -5.2f)
                },
                lootOffsets: null,
                hasArenaPillars: true,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            RoomModule roomTreasure = BuildRoomPrefab(
                "Room_Treasure", RoomType.Treasure, 12f, 12f,
                allFourCardinalSockets,
                matFloorTreasure, matWall, matDoorFrame,
                hasPlayerSpawn: false, hasExit: false,
                enemyOffsets: null,
                lootOffsets: new[] { new Vector3(0f, 0f, 0f) },
                hasArenaPillars: false,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            // Macro 2x2 Slot Boss Exit Room (28m x 28m — nearly 3x area of normal rooms for 3D Boss AoE/Dodge space)
            RoomModule roomExit = BuildRoomPrefab(
                "Room_Exit", RoomType.Exit, 28f, 28f,
                allFourCardinalSockets,
                matFloorExit, matWall, matDoorFrame,
                hasPlayerSpawn: false, hasExit: true,
                enemyOffsets: new[] { new Vector3(-5.0f, 0f, -1.5f), new Vector3(5.0f, 0f, -1.5f) },
                lootOffsets: new[] { new Vector3(-5.5f, 0f, 6.5f), new Vector3(5.5f, 0f, 6.5f) },
                hasArenaPillars: false,
                matSpawn, matEnemy, matExit, matLoot,
                matVendingBody, matVendingScreen, matVendingTray, matVendingSign);

            // 3. Room Templates SO
            RoomTemplateSO tStart = CreateOrUpdateTemplate("Template_Room_Start", roomStart, 1f, 1, 1, allowRotation: false);
            RoomTemplateSO tCross = CreateOrUpdateTemplate("Template_Room_Cross", roomCross, 1.2f, 0, 0, allowRotation: true);
            RoomTemplateSO tCorridor = CreateOrUpdateTemplate("Template_Room_Corridor", roomCorridor, 1.2f, 0, 0, allowRotation: true);
            RoomTemplateSO tCorridorCorner = CreateOrUpdateTemplate("Template_Room_Corridor_Corner", roomCorridorCorner, 1.0f, 0, 0, allowRotation: true);
            RoomTemplateSO tCorner = CreateOrUpdateTemplate("Template_Room_Corner", roomCorner, 1.0f, 0, 0, allowRotation: true);
            RoomTemplateSO tArena = CreateOrUpdateTemplate("Template_Room_Arena", roomArena, 0.85f, 0, 2, allowRotation: true);
            RoomTemplateSO tTreasure = CreateOrUpdateTemplate("Template_Room_Treasure", roomTreasure, 1.0f, 1, 2, allowRotation: false);
            RoomTemplateSO tExit = CreateOrUpdateTemplate("Template_Room_Exit", roomExit, 1f, 1, 1, allowRotation: false);

            // 4. Integration Channels & Anchors
            DungeonGeneratedEventChannelSO eventChannel = CreateOrLoadAsset<DungeonGeneratedEventChannelSO>($"{ChannelsRoot}/OnDungeonGenerated.asset");
            TransformAnchorSO playerAnchor = CreateOrLoadAsset<TransformAnchorSO>($"{ChannelsRoot}/PlayerSpawnAnchor.asset");
            TransformAnchorSO exitAnchor = CreateOrLoadAsset<TransformAnchorSO>($"{ChannelsRoot}/LevelExitAnchor.asset");

            // 5. Dungeon Generation Config
            DungeonGenerationConfigSO config = CreateOrLoadAsset<DungeonGenerationConfigSO>($"{ConfigRoot}/DefaultDungeonConfig.asset");
            config.ConfigurePools(
                starts: new List<RoomTemplateSO> { tStart },
                corridors: new List<RoomTemplateSO> { tCorridor },
                normals: new List<RoomTemplateSO> { tCross, tCorner, tArena },
                specials: new List<RoomTemplateSO> { tTreasure },
                exits: new List<RoomTemplateSO> { tExit }
            );
            config.TargetRoomCount = 13;
            config.MinMainPathLength = 5;
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6. Build MapGenTest Scene (loads ScriptableObjects after NewScene so UnloadUnusedAssets cannot invalidate them)
            BuildTestScene();

            Debug.Log("[MapGenPrototypeBuilder] Successfully built all prototype rooms, ScriptableObjects, and MapGenTest scene.");
        }

        private static RoomModule BuildRoomPrefab(
            string prefabName,
            RoomType roomType,
            float width,
            float length,
            DoorDirection[] sockets,
            Material floorMat,
            Material wallMat,
            Material doorFrameMat,
            bool hasPlayerSpawn,
            bool hasExit,
            Vector3[] enemyOffsets,
            Vector3[] lootOffsets,
            bool hasArenaPillars,
            Material spawnMat,
            Material enemyMat,
            Material exitMat,
            Material lootMat,
            Material matVendingBody,
            Material matVendingScreen,
            Material matVendingTray,
            Material matVendingSign)
        {
            var root = new GameObject(prefabName);

            // Floor: top surface strictly at Y = 0.0
            GameObject floor = CreatePrimitiveChild("Floor", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -FloorThickness * 0.5f, 0f),
                new Vector3(width, FloorThickness, length),
                floorMat, keepCollider: true);

            // Perimeter walls
            var socketSet = new HashSet<DoorDirection>(sockets);
            Transform wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root.transform, false);

            Transform socketsRoot = new GameObject("Sockets").transform;
            socketsRoot.SetParent(root.transform, false);

            BuildWallSide(DoorDirection.North, width, length, socketSet.Contains(DoorDirection.North), wallsRoot, socketsRoot, wallMat, doorFrameMat);
            BuildWallSide(DoorDirection.South, width, length, socketSet.Contains(DoorDirection.South), wallsRoot, socketsRoot, wallMat, doorFrameMat);
            BuildWallSide(DoorDirection.East, width, length, socketSet.Contains(DoorDirection.East), wallsRoot, socketsRoot, wallMat, doorFrameMat);
            BuildWallSide(DoorDirection.West, width, length, socketSet.Contains(DoorDirection.West), wallsRoot, socketsRoot, wallMat, doorFrameMat);

            if (hasArenaPillars)
            {
                var pillarsRoot = new GameObject("ArenaPillars").transform;
                pillarsRoot.SetParent(root.transform, false);
                // Waist-high tactical cover (1.4m height) to prevent 3D camera occlusion while blocking projectiles
                Vector3[] pillarPositions =
                {
                    new Vector3(-4.4f, 0.7f, 4.4f),
                    new Vector3(4.4f, 0.7f, 4.4f),
                    new Vector3(-4.4f, 0.7f, -4.4f),
                    new Vector3(4.4f, 0.7f, -4.4f)
                };
                for (int i = 0; i < pillarPositions.Length; i++)
                {
                    CreatePrimitiveChild($"Pillar_{i + 1}", PrimitiveType.Cube, pillarsRoot,
                        pillarPositions[i], new Vector3(1.6f, 1.4f, 1.6f), wallMat, keepCollider: true);
                }
            }

            // Integration markers (no colliders so NavMesh & movement aren't blocked)
            Transform playerSpawn = null;
            WeaponVendingMachine vendingKiosk = null;
            if (hasPlayerSpawn)
            {
                GameObject spawnPad = CreatePrimitiveChild("PlayerSpawnPoint", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, 0.05f, 0f), new Vector3(2.2f, 0.05f, 2.2f), spawnMat, keepCollider: false);
                playerSpawn = spawnPad.transform;

                // User requirement: Vending Machine for weapon purchases placed in the corner of Start room
                vendingKiosk = BuildWeaponVendingMachine(
                    root.transform,
                    new Vector3(-5.8f, 0f, -6.0f),
                    Quaternion.Euler(0f, 25f, 0f),
                    matVendingBody, matVendingScreen, matVendingTray, matVendingSign);
            }

            Transform exitPoint = null;
            if (hasExit)
            {
                GameObject exitObj = new GameObject("ExitPoint");
                exitObj.transform.SetParent(root.transform, false);
                exitObj.transform.localPosition = new Vector3(0f, 0f, 1.5f);
                CreatePrimitiveChild("ExitBeaconBase", PrimitiveType.Cylinder, exitObj.transform,
                    new Vector3(0f, 0.08f, 0f), new Vector3(3.0f, 0.08f, 3.0f), exitMat, keepCollider: false);
                CreatePrimitiveChild("ExitBeaconPillar", PrimitiveType.Cylinder, exitObj.transform,
                    new Vector3(0f, 1.4f, 0f), new Vector3(0.75f, 1.4f, 0.75f), exitMat, keepCollider: false);
                exitPoint = exitObj.transform;
            }

            var enemyTransforms = new List<Transform>();
            if (enemyOffsets != null)
            {
                for (int i = 0; i < enemyOffsets.Length; i++)
                {
                    GameObject enemyMarker = CreatePrimitiveChild($"EnemySpawn_{i + 1}", PrimitiveType.Cylinder, root.transform,
                        enemyOffsets[i] + new Vector3(0f, 0.06f, 0f),
                        new Vector3(1.1f, 0.06f, 1.1f), enemyMat, keepCollider: false);
                    enemyTransforms.Add(enemyMarker.transform);
                }
            }

            var lootTransforms = new List<Transform>();
            if (lootOffsets != null)
            {
                for (int i = 0; i < lootOffsets.Length; i++)
                {
                    GameObject lootMarker = CreatePrimitiveChild($"LootSpawn_{i + 1}", PrimitiveType.Cube, root.transform,
                        lootOffsets[i] + new Vector3(0f, 0.45f, 0f),
                        new Vector3(1.4f, 0.9f, 1.0f), lootMat, keepCollider: true);
                    lootTransforms.Add(lootMarker.transform);
                }
            }

            BossRoomRewardController bossRewardCtrl = null;
            if (hasExit && lootTransforms.Count > 0)
            {
                var chestObjs = new List<GameObject>();
                foreach (Transform t in lootTransforms)
                {
                    chestObjs.Add(t.gameObject);
                }
                bossRewardCtrl = root.AddComponent<BossRoomRewardController>();
                bossRewardCtrl.ConfigureChests(chestObjs);
            }

            var roomModule = root.AddComponent<RoomModule>();
            Bounds localBounds = new Bounds(
                new Vector3(0f, (WallHeight - FloorThickness) * 0.5f, 0f),
                new Vector3(width, WallHeight + FloorThickness, length));

            // Recessed Interior Combat Trigger (inset 2.2m from walls so player never clips into closing doors)
            BoxCollider interiorTrigger = null;
            if (roomType == RoomType.Normal || roomType == RoomType.Combat || roomType == RoomType.Exit)
            {
                interiorTrigger = root.AddComponent<BoxCollider>();
                interiorTrigger.isTrigger = true;
                interiorTrigger.center = new Vector3(0f, 1.25f, 0f);
                interiorTrigger.size = new Vector3(Mathf.Max(2f, width - 4.4f), 2.5f, Mathf.Max(2f, length - 4.4f));
            }

            var lifecycleCtrl = root.AddComponent<RoomLifecycleController>();
            lifecycleCtrl.Configure(roomModule, interiorTrigger);

            roomModule.Configure(
                roomType,
                localBounds,
                playerSpawn,
                exitPoint,
                enemyTransforms,
                lootTransforms,
                vendingKiosk,
                bossRewardCtrl,
                lifecycleCtrl);

            int envLayer = LayerMask.NameToLayer("Environment");
            if (envLayer >= 0)
            {
                SetLayerRecursively(floor, envLayer);
                SetLayerRecursively(wallsRoot.gameObject, envLayer);
                SetLayerRecursively(socketsRoot.gameObject, envLayer);
                if (hasArenaPillars)
                {
                    Transform pillars = root.transform.Find("ArenaPillars");
                    if (pillars != null)
                    {
                        SetLayerRecursively(pillars.gameObject, envLayer);
                    }
                }
            }

            string prefabPath = $"{RoomsRoot}/{prefabName}.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return savedPrefab.GetComponent<RoomModule>();
        }

        private static WeaponVendingMachine BuildWeaponVendingMachine(
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material matBody,
            Material matScreen,
            Material matTray,
            Material matSign)
        {
            var kioskGo = new GameObject("WeaponVendingMachine");
            kioskGo.transform.SetParent(parent, false);
            kioskGo.transform.localPosition = localPos;
            kioskGo.transform.localRotation = localRot;

            CreatePrimitiveChild("BodyCabinet", PrimitiveType.Cube, kioskGo.transform,
                new Vector3(0f, 1.1f, 0f), new Vector3(1.4f, 2.2f, 0.85f), matBody, keepCollider: true);

            CreatePrimitiveChild("DisplayScreen", PrimitiveType.Cube, kioskGo.transform,
                new Vector3(0f, 1.45f, 0.44f), new Vector3(1.1f, 0.95f, 0.08f), matScreen, keepCollider: false);

            CreatePrimitiveChild("DispenseTray", PrimitiveType.Cube, kioskGo.transform,
                new Vector3(0f, 0.35f, 0.44f), new Vector3(1.0f, 0.35f, 0.2f), matTray, keepCollider: false);

            CreatePrimitiveChild("MarqueeSign", PrimitiveType.Cube, kioskGo.transform,
                new Vector3(0f, 2.15f, 0.44f), new Vector3(1.3f, 0.3f, 0.12f), matSign, keepCollider: false);

            var interactGo = new GameObject("InteractionPoint");
            interactGo.transform.SetParent(kioskGo.transform, false);
            interactGo.transform.localPosition = new Vector3(0f, 0f, 1.2f);

            var machine = kioskGo.AddComponent<WeaponVendingMachine>();
            machine.Configure("StartRoom_ArmoryKiosk", "Armory Weapon Dispenser", interactGo.transform);

            return machine;
        }

        private static void BuildWallSide(
            DoorDirection dir,
            float roomWidth,
            float roomLength,
            bool hasDoor,
            Transform wallsRoot,
            Transform socketsRoot,
            Material wallMat,
            Material doorFrameMat)
        {
            float halfW = roomWidth * 0.5f;
            float halfL = roomLength * 0.5f;
            bool isNorthSouth = dir == DoorDirection.North || dir == DoorDirection.South;

            // North/South walls span full roomWidth; East/West walls sit cleanly between them to avoid corner Z-fighting
            float wallSpan = isNorthSouth ? roomWidth : (roomLength - WallThickness * 2f);
            float sideSign = (dir == DoorDirection.North || dir == DoorDirection.East) ? 1f : -1f;

            Vector3 wallCenter;
            Vector3 socketPos;
            Quaternion wallRotation = isNorthSouth ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            if (isNorthSouth)
            {
                wallCenter = new Vector3(0f, WallHeight * 0.5f, sideSign * (halfL - WallThickness * 0.5f));
                socketPos = new Vector3(0f, 0f, sideSign * halfL);
            }
            else
            {
                wallCenter = new Vector3(sideSign * (halfW - WallThickness * 0.5f), WallHeight * 0.5f, 0f);
                socketPos = new Vector3(sideSign * halfW, 0f, 0f);
            }

            if (!hasDoor)
            {
                GameObject wall = CreatePrimitiveChild($"Wall_{dir}", PrimitiveType.Cube, wallsRoot,
                    wallCenter, new Vector3(wallSpan, WallHeight, WallThickness), wallMat, keepCollider: true);
                wall.transform.localRotation = wallRotation;
                return;
            }

            // Wall with doorway cutout
            float sideSegLength = (wallSpan - DoorWidth) * 0.5f;
            Vector3 alongAxis = isNorthSouth ? Vector3.right : Vector3.forward;

            if (sideSegLength > 0.05f)
            {
                float segOffset = DoorWidth * 0.5f + sideSegLength * 0.5f;
                GameObject leftSeg = CreatePrimitiveChild($"Wall_{dir}_Left", PrimitiveType.Cube, wallsRoot,
                    wallCenter - alongAxis * segOffset,
                    new Vector3(sideSegLength, WallHeight, WallThickness), wallMat, keepCollider: true);
                leftSeg.transform.localRotation = wallRotation;

                GameObject rightSeg = CreatePrimitiveChild($"Wall_{dir}_Right", PrimitiveType.Cube, wallsRoot,
                    wallCenter + alongAxis * segOffset,
                    new Vector3(sideSegLength, WallHeight, WallThickness), wallMat, keepCollider: true);
                rightSeg.transform.localRotation = wallRotation;
            }

            // Top lintel above doorway
            float lintelHeight = WallHeight - DoorHeight;
            if (lintelHeight > 0.05f)
            {
                Vector3 lintelPos = new Vector3(wallCenter.x, DoorHeight + lintelHeight * 0.5f, wallCenter.z);
                GameObject lintel = CreatePrimitiveChild($"Wall_{dir}_Lintel", PrimitiveType.Cube, wallsRoot,
                    lintelPos, new Vector3(DoorWidth, lintelHeight, WallThickness), wallMat, keepCollider: true);
                lintel.transform.localRotation = wallRotation;
            }

            // DoorSocket child aligned to outer seam
            var socketObj = new GameObject($"Socket_{dir}");
            socketObj.transform.SetParent(socketsRoot, false);
            socketObj.transform.localPosition = socketPos;
            socketObj.transform.localRotation = Quaternion.LookRotation(dir.ToVector3(), Vector3.up);

            // Open doorway arch visual (no collider so NavMeshAgent passes smoothly)
            var openVisual = new GameObject("OpenVisual");
            openVisual.transform.SetParent(socketObj.transform, false);
            openVisual.transform.localPosition = new Vector3(0f, 0f, -WallThickness * 0.5f);

            CreatePrimitiveChild("Frame_Left", PrimitiveType.Cube, openVisual.transform,
                new Vector3(-(DoorWidth * 0.5f - 0.12f), DoorHeight * 0.5f, 0f),
                new Vector3(0.24f, DoorHeight, WallThickness + 0.12f), doorFrameMat, keepCollider: false);
            CreatePrimitiveChild("Frame_Right", PrimitiveType.Cube, openVisual.transform,
                new Vector3(+(DoorWidth * 0.5f - 0.12f), DoorHeight * 0.5f, 0f),
                new Vector3(0.24f, DoorHeight, WallThickness + 0.12f), doorFrameMat, keepCollider: false);
            CreatePrimitiveChild("Frame_Top", PrimitiveType.Cube, openVisual.transform,
                new Vector3(0f, DoorHeight - 0.12f, 0f),
                new Vector3(DoorWidth, 0.24f, WallThickness + 0.12f), doorFrameMat, keepCollider: false);

            // Closed wall blocker (enabled automatically when unconnected)
            GameObject closedVisual = CreatePrimitiveChild("ClosedWallVisual", PrimitiveType.Cube, socketObj.transform,
                new Vector3(0f, DoorHeight * 0.5f, -WallThickness * 0.5f),
                new Vector3(DoorWidth, DoorHeight, WallThickness), wallMat, keepCollider: true);

            // Combat Locked Gate barrier (activated during RoomLifecycleState.Combat to lock the player inside the room)
            Material gateMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_DoorGate_Locked.mat") ?? doorFrameMat;
            var lockedGateVisual = new GameObject("LockedGateVisual");
            lockedGateVisual.transform.SetParent(socketObj.transform, false);
            lockedGateVisual.transform.localPosition = new Vector3(0f, 0f, -WallThickness * 0.5f);

            var gateBlocker = lockedGateVisual.AddComponent<BoxCollider>();
            gateBlocker.center = new Vector3(0f, DoorHeight * 0.5f, 0f);
            gateBlocker.size = new Vector3(DoorWidth, DoorHeight, WallThickness);

            const int barCount = 5;
            for (int b = 0; b < barCount; b++)
            {
                float t = (b + 1f) / (barCount + 1f);
                float bx = Mathf.Lerp(-DoorWidth * 0.5f + 0.3f, DoorWidth * 0.5f - 0.3f, t);
                CreatePrimitiveChild($"GateBar_{b + 1}", PrimitiveType.Cube, lockedGateVisual.transform,
                    new Vector3(bx, DoorHeight * 0.5f, 0f),
                    new Vector3(0.18f, DoorHeight - 0.15f, 0.18f), gateMat, keepCollider: false);
            }
            CreatePrimitiveChild("GateCrossbeam", PrimitiveType.Cube, lockedGateVisual.transform,
                new Vector3(0f, DoorHeight * 0.5f, 0f),
                new Vector3(DoorWidth - 0.2f, 0.22f, 0.22f), gateMat, keepCollider: false);

            openVisual.SetActive(false);
            closedVisual.SetActive(true);
            lockedGateVisual.SetActive(false);

            DoorSocket socketComp = socketObj.AddComponent<DoorSocket>();
            socketComp.Configure($"Socket_{dir}", dir, new Vector2(DoorWidth, DoorHeight), openVisual, closedVisual, lockedGateVisual);
        }

        private static GameObject CreatePrimitiveChild(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            if (!keepCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                {
                    Object.DestroyImmediate(col);
                }
            }

            if (material != null && go.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            return go;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null || layer < 0) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static RoomTemplateSO CreateOrUpdateTemplate(
            string assetName,
            RoomModule prefab,
            float weight,
            int minCount,
            int maxCount,
            bool allowRotation)
        {
            string path = $"{TemplatesRoot}/{assetName}.asset";
            RoomTemplateSO so = CreateOrLoadAsset<RoomTemplateSO>(path);
            so.Configure(prefab.name, prefab, weight, minCount, maxCount, allowRotation);
            EditorUtility.SetDirty(so);
            return so;
        }

        private static void BuildTestScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            DungeonGenerationConfigSO config = AssetDatabase.LoadAssetAtPath<DungeonGenerationConfigSO>($"{ConfigRoot}/DefaultDungeonConfig.asset");
            DungeonGeneratedEventChannelSO eventChannel = AssetDatabase.LoadAssetAtPath<DungeonGeneratedEventChannelSO>($"{ChannelsRoot}/OnDungeonGenerated.asset");
            TransformAnchorSO playerAnchor = AssetDatabase.LoadAssetAtPath<TransformAnchorSO>($"{ChannelsRoot}/PlayerSpawnAnchor.asset");
            TransformAnchorSO exitAnchor = AssetDatabase.LoadAssetAtPath<TransformAnchorSO>($"{ChannelsRoot}/LevelExitAnchor.asset");
            Material pathLineMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsRoot}/Mat_PathLine.mat");

            if (config == null)
            {
                Debug.LogError("[MapGenPrototypeBuilder] DefaultDungeonConfig.asset could not be loaded after creating MapGenTest scene.");
                return;
            }

            // Configure Directional Light
            Light mainLight = Object.FindAnyObjectByType<Light>();
            if (mainLight != null)
            {
                mainLight.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                mainLight.intensity = 1.6f;
            }

            // Optional URP Volume matching SampleScene
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile != null)
            {
                var volumeGo = new GameObject("Global Volume");
                Volume vol = volumeGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            // Dungeon Generator + NavMeshSurface + Path LineRenderer
            var generatorGo = new GameObject("DungeonGenerator");
            NavMeshSurface navSurface = generatorGo.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.Children;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var pathLineGo = new GameObject("MainPathLine");
            pathLineGo.transform.SetParent(generatorGo.transform, false);
            LineRenderer lineRenderer = pathLineGo.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.45f;
            lineRenderer.endWidth = 0.45f;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.sharedMaterial = pathLineMat;
            lineRenderer.startColor = new Color(1f, 0.88f, 0.15f, 1f);
            lineRenderer.endColor = new Color(1f, 0.35f, 0.65f, 1f);

            DungeonGenerator generator = generatorGo.AddComponent<DungeonGenerator>();
            generator.ConfigureReferences(config, navSurface, eventChannel, playerAnchor, exitAnchor, lineRenderer);

            // Build HUD Canvas + EventSystem (InputSystemUIInputModule)
            DungeonMinimapUI minimapUI = BuildTestHud(generator);

            // Note: MapGenTest is saved clean (without pre-spawned room instances / embedded NavMeshData)
            // so that Assets/Scenes/MapGenTest.unity serializes strictly as text YAML (%YAML 1.1).
            // DungeonGenerator has generateOnStart = true, generating the dungeon dynamically on Play Mode.
            AssetDatabase.DeleteAsset(TestScenePath);
            EditorSceneManager.SaveScene(scene, TestScenePath);
        }

        private static DungeonMinimapUI BuildTestHud(DungeonGenerator generator)
        {
            // EventSystem with New Input System module
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("MapGenTestHUD");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Top-left Telemetry Panel
            GameObject statsPanel = CreatePanel("TelemetryPanel", canvasGo.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -20f), new Vector2(920f, 250f),
                new Color(0.08f, 0.10f, 0.14f, 0.84f));

            GameObject textGo = new GameObject("StatsText");
            textGo.transform.SetParent(statsPanel.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 12f);
            textRect.offsetMax = new Vector2(-16f, -12f);
            Text statsText = textGo.AddComponent<Text>();
            statsText.font = uiFont;
            statsText.fontSize = 18;
            statsText.lineSpacing = 1.15f;
            statsText.supportRichText = true;
            statsText.color = Color.white;
            statsText.alignment = TextAnchor.UpperLeft;

            // Top-right Soul Knight 2D Grid Minimap Panel
            GameObject minimapPanel = CreatePanel("MinimapPanel", canvasGo.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -20f), new Vector2(290f, 275f),
                new Color(0.06f, 0.08f, 0.12f, 0.88f));

            var titleGo = new GameObject("MinimapTitle", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(minimapPanel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -6f);
            titleRt.sizeDelta = new Vector2(-16f, 22f);
            var titleTxt = titleGo.GetComponent<Text>();
            titleTxt.font = uiFont;
            titleTxt.fontSize = 13;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(1.0f, 0.88f, 0.25f);
            titleTxt.text = "DUNGEON MINIMAP";

            var viewportGo = new GameObject("MinimapViewport", typeof(RectTransform));
            viewportGo.transform.SetParent(minimapPanel.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = new Vector2(0.5f, 0.5f);
            viewportRt.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRt.pivot = new Vector2(0.5f, 0.5f);
            viewportRt.anchoredPosition = new Vector2(0f, -4f);
            viewportRt.sizeDelta = new Vector2(250f, 205f);

            var legendGo = new GameObject("MinimapLegend", typeof(RectTransform), typeof(Text));
            legendGo.transform.SetParent(minimapPanel.transform, false);
            var legendRt = legendGo.GetComponent<RectTransform>();
            legendRt.anchorMin = new Vector2(0f, 0f);
            legendRt.anchorMax = new Vector2(1f, 0f);
            legendRt.pivot = new Vector2(0.5f, 0f);
            legendRt.anchoredPosition = new Vector2(0f, 6f);
            legendRt.sizeDelta = new Vector2(-12f, 20f);
            var legendTxt = legendGo.GetComponent<Text>();
            legendTxt.font = uiFont;
            legendTxt.fontSize = 11;
            legendTxt.alignment = TextAnchor.MiddleCenter;
            legendTxt.color = new Color(0.8f, 0.85f, 0.92f);
            legendTxt.text = "S:Start  C:Combat  $:Treasure  BOSS:Chamber";

            DungeonMinimapUI minimapUI = minimapPanel.AddComponent<DungeonMinimapUI>();
            minimapUI.Configure(viewportRt, uiFont);

            // Bottom-left Toolbar
            GameObject toolbar = CreatePanel("ActionToolbar", canvasGo.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 20f), new Vector2(1380f, 64f),
                new Color(0.08f, 0.10f, 0.14f, 0.84f));

            HorizontalLayoutGroup hlg = toolbar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.spacing = 10f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            Button regenBtn = CreateHudButton("Btn_Regenerate", toolbar.transform, "Regenerate (R)", uiFont, new Color(0.18f, 0.56f, 0.34f));
            Button replayBtn = CreateHudButton("Btn_ReplaySeed", toolbar.transform, "Replay Seed (C)", uiFont, new Color(0.24f, 0.42f, 0.64f));
            Button lockClearBtn = CreateHudButton("Btn_LockClear", toolbar.transform, "Lock/Clear (L)", uiFont, new Color(0.76f, 0.36f, 0.16f));
            Button bossBtn = CreateHudButton("Btn_KillBoss", toolbar.transform, "Kill Boss (B)", uiFont, new Color(0.68f, 0.22f, 0.48f));
            Button camBtn = CreateHudButton("Btn_ToggleCam", toolbar.transform, "Camera (Tab)", uiFont, new Color(0.32f, 0.36f, 0.45f));
            Button pathBtn = CreateHudButton("Btn_TogglePath", toolbar.transform, "Path (P)", uiFont, new Color(0.55f, 0.45f, 0.18f));
            Button decBtn = CreateHudButton("Btn_DecRooms", toolbar.transform, "- Rooms", uiFont, new Color(0.45f, 0.25f, 0.25f));
            Button incBtn = CreateHudButton("Btn_IncRooms", toolbar.transform, "+ Rooms", uiFont, new Color(0.28f, 0.48f, 0.28f));

            var controllerGo = new GameObject("MapGenTestController");
            MapGenTestController controller = controllerGo.AddComponent<MapGenTestController>();
            controller.Configure(
                generator,
                Camera.main,
                statsText,
                regenBtn,
                replayBtn,
                camBtn,
                pathBtn,
                decBtn,
                incBtn,
                bossBtn,
                lockClearBtn,
                minimapUI);

            return minimapUI;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color bgColor)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            Image img = panel.AddComponent<Image>();
            img.color = bgColor;
            return panel;
        }

        private static Button CreateHudButton(string name, Transform parent, string label, Font font, Color bgColor)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            btnGo.AddComponent<RectTransform>();
            Image img = btnGo.AddComponent<Image>();
            img.color = bgColor;
            Button btn = btnGo.AddComponent<Button>();

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform tr = txtGo.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            Text txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = 16;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 11;
            txt.resizeTextMaxSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        private static Material CreateUrpMaterial(string fileName, Color color)
        {
            string path = $"{MaterialsRoot}/{fileName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreatePathLineMaterial(string fileName, Color color)
        {
            string path = $"{MaterialsRoot}/{fileName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static T CreateOrLoadAsset<T>(string assetPath) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            T instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, assetPath);
            return instance;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string normalized = assetPath.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
