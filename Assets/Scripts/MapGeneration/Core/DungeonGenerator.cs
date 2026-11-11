using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Scene orchestrator responsible for planning a valid dungeon layout,
    /// instantiating room prefabs, sealing unconnected doorways, baking runtime NavMesh,
    /// and publishing runtime spawn/exit anchors to gameplay systems.
    /// </summary>
    public class DungeonGenerator : MonoBehaviour, IDungeonProvider
    {
        [Header("Configuration")]
        [SerializeField] private DungeonGenerationConfigSO config;
        [SerializeField] private bool generateOnStart = true;

        [Header("Scene Containers & Navigation")]
        [SerializeField] private Transform roomsContainer;
        [SerializeField] private NavMeshSurface navMeshSurface;
        [SerializeField] private bool bakeNavMeshOnGenerate = true;

        [Header("Async Instantiation (Unity 6 Awaitable)")]
        [Tooltip("When enabled in Play Mode, yields one frame per room instantiation to avoid frame hitches on large dungeons.")]
        [SerializeField] private bool useAsyncInstantiation = false;

        [Header("Integration Channels & Anchors (Open Project #1 Pattern)")]
        [SerializeField] private DungeonGeneratedEventChannelSO onDungeonGeneratedChannel;
        [SerializeField] private TransformAnchorSO playerSpawnAnchor;
        [SerializeField] private TransformAnchorSO levelExitAnchor;

        [Header("Debug Visualization")]
        [SerializeField] private LineRenderer mainPathLineRenderer;
        [SerializeField] private bool showMainPathLine = true;
        [SerializeField] private bool drawSceneGizmos = true;

        private readonly List<RoomModule> spawnedRooms = new List<RoomModule>();
        private readonly List<Transform> enemySpawnPoints = new List<Transform>();
        private readonly List<Transform> lootSpawnPoints = new List<Transform>();

        public bool IsGenerated { get; private set; }
        public DungeonLayoutData CurrentLayout { get; private set; }
        public Transform PlayerSpawnPoint { get; private set; }
        public Transform LevelExitPoint { get; private set; }
        public IReadOnlyList<Transform> EnemySpawnPoints => enemySpawnPoints;
        public IReadOnlyList<Transform> LootSpawnPoints => lootSpawnPoints;
        public IReadOnlyList<RoomModule> SpawnedRooms => spawnedRooms;
        public bool IsNavMeshBaked { get; private set; }
        public bool IsNavMeshPathReachable { get; private set; }
        public string LastError { get; private set; }

        public DungeonGenerationConfigSO Config
        {
            get => config;
            set => config = value;
        }

        public bool ShowMainPathLine
        {
            get => showMainPathLine;
            set
            {
                showMainPathLine = value;
                if (mainPathLineRenderer != null)
                {
                    mainPathLineRenderer.enabled = value && IsGenerated;
                }
            }
        }

        public event Action<DungeonRuntimeContext> OnDungeonGenerated;

        private async void Start()
        {
            if (!generateOnStart)
            {
                return;
            }

            if (useAsyncInstantiation)
            {
                await GenerateAsync();
            }
            else
            {
                Generate();
            }
        }

        public void ConfigureReferences(
            DungeonGenerationConfigSO generationConfig,
            NavMeshSurface surface,
            DungeonGeneratedEventChannelSO eventChannel,
            TransformAnchorSO spawnAnchor,
            TransformAnchorSO exitAnchor,
            LineRenderer pathLine)
        {
            config = generationConfig;
            navMeshSurface = surface;
            onDungeonGeneratedChannel = eventChannel;
            playerSpawnAnchor = spawnAnchor;
            levelExitAnchor = exitAnchor;
            mainPathLineRenderer = pathLine;
        }

        [ContextMenu("Generate New Dungeon")]
        public void Generate()
        {
            if (config == null)
            {
                LastError = "DungeonGenerationConfigSO is not assigned.";
                Debug.LogError($"[DungeonGenerator] {LastError}", this);
                return;
            }

            int seed = config.UseRandomSeed
                ? UnityEngine.Random.Range(int.MinValue + 1, int.MaxValue)
                : config.FixedSeed;

            GenerateWithSeed(seed);
        }

        public bool GenerateWithSeed(int seed)
        {
            if (config == null)
            {
                LastError = "DungeonGenerationConfigSO is not assigned.";
                Debug.LogError($"[DungeonGenerator] {LastError}", this);
                return false;
            }

            ClearDungeon();
            EnsureRoomsContainer();

            DungeonPlanRequest planRequest = config.ToPlanRequest(out Dictionary<string, RoomTemplateSO> templateLookup);
            if (!DungeonLayoutPlanner.TryPlan(planRequest, seed, out DungeonLayoutData layout, out string error))
            {
                LastError = error;
                Debug.LogError($"[DungeonGenerator] {LastError}", this);
                return false;
            }

            CurrentLayout = layout;
            LastError = null;

            var roomInstancesById = new Dictionary<int, RoomModule>(layout.rooms.Count);
            foreach (PlacedRoomData placed in layout.rooms)
            {
                if (!templateLookup.TryGetValue(placed.templateId, out RoomTemplateSO templateSo) || templateSo.Prefab == null)
                {
                    continue;
                }

                RoomModule instance = InstantiateRoomModule(templateSo.Prefab, placed);
                roomInstancesById[placed.roomId] = instance;
                spawnedRooms.Add(instance);
            }

            FinalizeGeneratedDungeon(roomInstancesById);
            return true;
        }

        public async Awaitable<bool> GenerateAsync(int? customSeed = null)
        {
            if (config == null)
            {
                LastError = "DungeonGenerationConfigSO is not assigned.";
                Debug.LogError($"[DungeonGenerator] {LastError}", this);
                return false;
            }

            ClearDungeon();
            EnsureRoomsContainer();

            int seed = customSeed ?? (config.UseRandomSeed
                ? UnityEngine.Random.Range(int.MinValue + 1, int.MaxValue)
                : config.FixedSeed);

            DungeonPlanRequest planRequest = config.ToPlanRequest(out Dictionary<string, RoomTemplateSO> templateLookup);
            if (!DungeonLayoutPlanner.TryPlan(planRequest, seed, out DungeonLayoutData layout, out string error))
            {
                LastError = error;
                Debug.LogError($"[DungeonGenerator] {LastError}", this);
                return false;
            }

            CurrentLayout = layout;
            LastError = null;

            var roomInstancesById = new Dictionary<int, RoomModule>(layout.rooms.Count);
            foreach (PlacedRoomData placed in layout.rooms)
            {
                if (!templateLookup.TryGetValue(placed.templateId, out RoomTemplateSO templateSo) || templateSo.Prefab == null)
                {
                    continue;
                }

                RoomModule instance = InstantiateRoomModule(templateSo.Prefab, placed);
                roomInstancesById[placed.roomId] = instance;
                spawnedRooms.Add(instance);

                if (Application.isPlaying)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
            }

            FinalizeGeneratedDungeon(roomInstancesById);
            return true;
        }

        [ContextMenu("Clear Dungeon")]
        public void ClearDungeon()
        {
            IsGenerated = false;
            IsNavMeshBaked = false;
            IsNavMeshPathReachable = false;
            CurrentLayout = null;
            PlayerSpawnPoint = null;
            LevelExitPoint = null;
            enemySpawnPoints.Clear();
            lootSpawnPoints.Clear();
            spawnedRooms.Clear();

            if (playerSpawnAnchor != null)
            {
                playerSpawnAnchor.Clear();
            }

            if (levelExitAnchor != null)
            {
                levelExitAnchor.Clear();
            }

            if (mainPathLineRenderer != null)
            {
                mainPathLineRenderer.positionCount = 0;
                mainPathLineRenderer.enabled = false;
            }

            if (navMeshSurface != null)
            {
                navMeshSurface.RemoveData();
            }

            if (roomsContainer != null)
            {
                for (int i = roomsContainer.childCount - 1; i >= 0; i--)
                {
                    GameObject child = roomsContainer.GetChild(i).gameObject;
                    if (Application.isPlaying)
                    {
                        // Detach and deactivate immediately so same-frame Physics.SyncTransforms and
                        // NavMeshSurface.BuildNavMesh (CollectObjects.Children) ignore dying rooms.
                        child.SetActive(false);
                        child.transform.SetParent(null, false);
                        Destroy(child);
                    }
                    else
                    {
                        DestroyImmediate(child);
                    }
                }
            }
        }

        public bool VerifyNavMeshPath(out NavMeshPath path)
        {
            path = new NavMeshPath();
            if (!IsNavMeshBaked || PlayerSpawnPoint == null || LevelExitPoint == null)
            {
                return false;
            }

            Vector3 start = PlayerSpawnPoint.position;
            Vector3 end = LevelExitPoint.position;

            if (NavMesh.SamplePosition(start, out NavMeshHit startHit, 3f, NavMesh.AllAreas))
            {
                start = startHit.position;
            }

            if (NavMesh.SamplePosition(end, out NavMeshHit endHit, 3f, NavMesh.AllAreas))
            {
                end = endHit.position;
            }

            bool found = NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path);
            return found && path.status == NavMeshPathStatus.PathComplete;
        }

        private RoomModule InstantiateRoomModule(RoomModule prefab, PlacedRoomData placed)
        {
            Quaternion rotation = Quaternion.Euler(placed.worldRotationEuler);
            RoomModule instance = Instantiate(prefab, placed.worldPosition, rotation, roomsContainer);
            instance.name = $"Room_{placed.roomId:00}_{placed.roomType}_{placed.templateId}";
            instance.RoomId = placed.roomId;
            instance.GraphDepth = placed.graphDepth;
            instance.IsOnMainPath = placed.isOnMainPath;
            instance.TemplateId = placed.templateId;
            instance.RefreshSocketsFromChildren();
            instance.ResetAllSocketsToSealed();

            if (placed.roomType == RoomType.Start && instance.PlayerSpawnPoint != null)
            {
                PlayerSpawnPoint = instance.PlayerSpawnPoint;
            }

            if (placed.roomType == RoomType.Exit && instance.ExitPoint != null)
            {
                LevelExitPoint = instance.ExitPoint;
            }

            if (instance.EnemySpawnPoints != null)
            {
                foreach (Transform spawn in instance.EnemySpawnPoints)
                {
                    if (spawn != null)
                    {
                        enemySpawnPoints.Add(spawn);
                    }
                }
            }

            if (instance.LootSpawnPoints != null)
            {
                foreach (Transform loot in instance.LootSpawnPoints)
                {
                    if (loot != null)
                    {
                        lootSpawnPoints.Add(loot);
                    }
                }
            }

            return instance;
        }

        private void FinalizeGeneratedDungeon(Dictionary<int, RoomModule> roomInstancesById)
        {
            // Wire every doorway connection between instantiated rooms
            foreach (RoomConnectionData conn in CurrentLayout.connections)
            {
                if (!roomInstancesById.TryGetValue(conn.fromRoomId, out RoomModule fromRoom) ||
                    !roomInstancesById.TryGetValue(conn.toRoomId, out RoomModule toRoom))
                {
                    continue;
                }

                if (fromRoom.TryGetSocket(conn.fromSocketName, out DoorSocket fromSocket) &&
                    toRoom.TryGetSocket(conn.toSocketName, out DoorSocket toSocket))
                {
                    fromSocket.SetConnectionState(true, toSocket, toRoom.RoomId);
                    toSocket.SetConnectionState(true, fromSocket, fromRoom.RoomId);
                }
            }

            // Fallback spawn anchors if prefab omitted explicit child transform
            if (PlayerSpawnPoint == null && roomInstancesById.TryGetValue(CurrentLayout.startRoomId, out RoomModule startRoom))
            {
                PlayerSpawnPoint = startRoom.transform;
            }

            if (LevelExitPoint == null && roomInstancesById.TryGetValue(CurrentLayout.exitRoomId, out RoomModule exitRoom))
            {
                LevelExitPoint = exitRoom.transform;
            }

            // Runtime NavMesh bake (requires Physics/Collider sync first when instantiating at runtime)
            if (bakeNavMeshOnGenerate && navMeshSurface != null)
            {
                Physics.SyncTransforms();
                navMeshSurface.BuildNavMesh();
                IsNavMeshBaked = true;
                IsNavMeshPathReachable = VerifyNavMeshPath(out _);
            }

            InitializeRoomLifecyclesAndWayfindingLandmarks(roomInstancesById);

            IsGenerated = true;
            UpdateMainPathLineRenderer(roomInstancesById);

            if (playerSpawnAnchor != null && PlayerSpawnPoint != null)
            {
                playerSpawnAnchor.Provide(PlayerSpawnPoint);
            }

            if (levelExitAnchor != null && LevelExitPoint != null)
            {
                levelExitAnchor.Provide(LevelExitPoint);
            }

            WeaponVendingMachine vendingMachine = null;
            BossRoomRewardController bossRewardController = null;

            if (roomInstancesById.TryGetValue(CurrentLayout.startRoomId, out RoomModule startRoomInstance))
            {
                vendingMachine = startRoomInstance.VendingMachine ?? startRoomInstance.GetComponentInChildren<WeaponVendingMachine>(true);
            }

            if (roomInstancesById.TryGetValue(CurrentLayout.exitRoomId, out RoomModule exitRoomInstance))
            {
                bossRewardController = exitRoomInstance.BossRewardController ?? exitRoomInstance.GetComponentInChildren<BossRoomRewardController>(true);
            }

            var context = new DungeonRuntimeContext(
                CurrentLayout,
                PlayerSpawnPoint,
                LevelExitPoint,
                enemySpawnPoints,
                lootSpawnPoints,
                spawnedRooms,
                IsNavMeshBaked,
                IsNavMeshPathReachable,
                vendingMachine,
                bossRewardController);

            OnDungeonGenerated?.Invoke(context);
            if (onDungeonGeneratedChannel != null)
            {
                onDungeonGeneratedChannel.RaiseEvent(context);
            }
        }

        /// <summary>
        /// Triggers 1 to 2 bonus chests to appear in the Boss Room when the boss is killed,
        /// and marks the Boss Room lifecycle state as Cleared.
        /// </summary>
        public int TriggerBossRewards()
        {
            if (spawnedRooms != null)
            {
                RoomModule exitRoom = spawnedRooms.Find(r => r != null && r.RoomType == RoomType.Exit);
                if (exitRoom != null)
                {
                    if (exitRoom.LifecycleController != null)
                    {
                        exitRoom.LifecycleController.ClearRoom();
                    }

                    BossRoomRewardController ctrl = exitRoom.BossRewardController ?? exitRoom.GetComponentInChildren<BossRoomRewardController>(true);
                    if (ctrl != null)
                    {
                        return ctrl.TriggerBossRewards();
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// Cycles combat room states (Unvisited -> Combat [Doors Locked] -> Cleared [Doors Unlocked & Green])
        /// for quick visual inspection in MapGenTest.
        /// </summary>
        public string CycleCombatLockSimulation()
        {
            if (spawnedRooms == null || spawnedRooms.Count == 0)
            {
                return "No rooms";
            }

            bool anyUnvisited = false;
            bool anyCombat = false;
            foreach (RoomModule r in spawnedRooms)
            {
                if (r == null || r.LifecycleController == null || !r.LifecycleController.RequiresCombatLock)
                {
                    continue;
                }

                if (r.LifecycleController.State == RoomLifecycleState.Unvisited) anyUnvisited = true;
                else if (r.LifecycleController.State == RoomLifecycleState.Combat) anyCombat = true;
            }

            if (anyUnvisited)
            {
                foreach (RoomModule r in spawnedRooms)
                {
                    if (r != null && r.LifecycleController != null && r.LifecycleController.RequiresCombatLock)
                    {
                        r.LifecycleController.EnterCombat();
                    }
                }
                return "COMBAT LOCKED (Doors Closed)";
            }

            if (anyCombat)
            {
                foreach (RoomModule r in spawnedRooms)
                {
                    if (r != null && r.LifecycleController != null && r.LifecycleController.RequiresCombatLock)
                    {
                        r.LifecycleController.ClearRoom();
                    }
                }
                return "ALL CLEARED (Doors Open & Green)";
            }

            // Reset back to Unvisited with 3D Wayfinding Landmarks
            var map = new Dictionary<int, RoomModule>();
            foreach (RoomModule r in spawnedRooms)
            {
                if (r != null) map[r.RoomId] = r;
            }
            InitializeRoomLifecyclesAndWayfindingLandmarks(map);
            return "UNVISITED (Wayfinding Landmarks)";
        }

        private void InitializeRoomLifecyclesAndWayfindingLandmarks(Dictionary<int, RoomModule> roomInstancesById)
        {
            foreach (RoomModule room in roomInstancesById.Values)
            {
                if (room == null) continue;
                if (room.LifecycleController != null)
                {
                    room.LifecycleController.InitializeLifecycle();
                }

                if (room.DoorSockets != null)
                {
                    foreach (DoorSocket s in room.DoorSockets)
                    {
                        if (s != null && s.IsConnected)
                        {
                            s.SetDoorFrameColor(RoomLifecycleController.LandmarkColorNormal);
                        }
                    }
                }
            }

            // Apply 3D Wayfinding Visual Landmarks to doorways leading into Boss (Exit) or Treasure rooms
            foreach (RoomModule room in roomInstancesById.Values)
            {
                if (room == null || room.DoorSockets == null) continue;

                Color? landmark = null;
                if (room.RoomType == RoomType.Exit)
                {
                    landmark = RoomLifecycleController.LandmarkColorBoss;
                }
                else if (room.RoomType == RoomType.Treasure)
                {
                    landmark = RoomLifecycleController.LandmarkColorTreasure;
                }

                if (!landmark.HasValue) continue;

                foreach (DoorSocket socket in room.DoorSockets)
                {
                    if (socket == null || !socket.IsConnected) continue;

                    socket.SetDoorFrameColor(landmark.Value);
                    DoorSocket corrInSocket = socket.ConnectedSocket;
                    if (corrInSocket != null)
                    {
                        corrInSocket.SetDoorFrameColor(landmark.Value);

                        // Also highlight the other end of the corridor so players see the Boss/Treasure landmark from inside the combat room!
                        if (roomInstancesById.TryGetValue(corrInSocket.ConnectedRoomId, out _) &&
                            roomInstancesById.TryGetValue(socket.ConnectedRoomId, out RoomModule corridorRoom) &&
                            corridorRoom.RoomType == RoomType.Corridor &&
                            corridorRoom.DoorSockets != null)
                        {
                            foreach (DoorSocket corrSocket in corridorRoom.DoorSockets)
                            {
                                if (corrSocket != null && corrSocket.IsConnected)
                                {
                                    corrSocket.SetDoorFrameColor(landmark.Value);
                                    if (corrSocket.ConnectedSocket != null)
                                    {
                                        corrSocket.ConnectedSocket.SetDoorFrameColor(landmark.Value);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void UpdateMainPathLineRenderer(Dictionary<int, RoomModule> roomInstancesById)
        {
            if (mainPathLineRenderer == null || CurrentLayout == null || CurrentLayout.mainPathRoomIds.Count == 0)
            {
                return;
            }

            var points = new List<Vector3>();
            const float yOffset = 0.6f;

            if (PlayerSpawnPoint != null)
            {
                points.Add(PlayerSpawnPoint.position + Vector3.up * yOffset);
            }

            List<int> pathIds = CurrentLayout.mainPathRoomIds;
            for (int i = 0; i < pathIds.Count; i++)
            {
                if (roomInstancesById.TryGetValue(pathIds[i], out RoomModule room))
                {
                    points.Add(room.transform.position + Vector3.up * yOffset);
                }

                if (i + 1 < pathIds.Count && TryFindConnectionDoorPosition(pathIds[i], pathIds[i + 1], out Vector3 doorPos))
                {
                    points.Add(doorPos + Vector3.up * yOffset);
                }
            }

            if (LevelExitPoint != null)
            {
                points.Add(LevelExitPoint.position + Vector3.up * yOffset);
            }

            mainPathLineRenderer.positionCount = points.Count;
            mainPathLineRenderer.SetPositions(points.ToArray());
            mainPathLineRenderer.enabled = showMainPathLine;
        }

        private bool TryFindConnectionDoorPosition(int roomA, int roomB, out Vector3 doorPos)
        {
            foreach (RoomConnectionData conn in CurrentLayout.connections)
            {
                bool match = (conn.fromRoomId == roomA && conn.toRoomId == roomB) ||
                             (conn.fromRoomId == roomB && conn.toRoomId == roomA);
                if (match)
                {
                    doorPos = conn.doorWorldPosition;
                    return true;
                }
            }

            doorPos = Vector3.zero;
            return false;
        }

        private void EnsureRoomsContainer()
        {
            if (roomsContainer != null)
            {
                return;
            }

            Transform existing = transform.Find("GeneratedRooms");
            if (existing != null)
            {
                roomsContainer = existing;
                return;
            }

            var container = new GameObject("GeneratedRooms");
            container.transform.SetParent(transform, false);
            roomsContainer = container.transform;
        }

        private void OnDrawGizmos()
        {
            if (!drawSceneGizmos || CurrentLayout == null)
            {
                return;
            }

            // Draw doorway connection links
            Gizmos.color = new Color(0.2f, 0.95f, 1f, 0.85f);
            foreach (RoomConnectionData conn in CurrentLayout.connections)
            {
                PlacedRoomData a = CurrentLayout.GetRoomById(conn.fromRoomId);
                PlacedRoomData b = CurrentLayout.GetRoomById(conn.toRoomId);
                if (a != null && b != null)
                {
                    Vector3 posA = a.worldPosition + Vector3.up * 0.8f;
                    Vector3 posB = b.worldPosition + Vector3.up * 0.8f;
                    Vector3 door = conn.doorWorldPosition + Vector3.up * 0.8f;
                    Gizmos.DrawLine(posA, door);
                    Gizmos.DrawLine(door, posB);
                    Gizmos.DrawWireSphere(door, 0.35f);
                }
            }

            // Draw critical Start -> Exit path in Gold
            if (CurrentLayout.mainPathRoomIds != null && CurrentLayout.mainPathRoomIds.Count > 1)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
                for (int i = 0; i < CurrentLayout.mainPathRoomIds.Count - 1; i++)
                {
                    int idA = CurrentLayout.mainPathRoomIds[i];
                    int idB = CurrentLayout.mainPathRoomIds[i + 1];
                    PlacedRoomData roomA = CurrentLayout.GetRoomById(idA);
                    PlacedRoomData roomB = CurrentLayout.GetRoomById(idB);
                    if (roomA != null && roomB != null)
                    {
                        Vector3 pA = roomA.worldPosition + Vector3.up * 1.4f;
                        Vector3 pB = roomB.worldPosition + Vector3.up * 1.4f;
                        if (TryFindConnectionDoorPosition(idA, idB, out Vector3 door))
                        {
                            Vector3 d = door + Vector3.up * 1.4f;
                            Gizmos.DrawLine(pA, d);
                            Gizmos.DrawLine(d, pB);
                        }
                        else
                        {
                            Gizmos.DrawLine(pA, pB);
                        }
                    }
                }
            }
        }
    }
}
