using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Pure C# configuration for deterministic room-layout planning.
    /// </summary>
    [Serializable]
    public class DungeonPlanRequest
    {
        public int targetRoomCount = 13;
        public int minMainPathLength = 5;
        public float branchProbability = 0.65f;
        public float loopConnectionChance = 0f;
        public float boundsOverlapMargin = 0.05f;
        public int maxGenerationAttempts = 30;
        public int maxBacktrackSteps = 450;

        public List<RoomTemplateData> startTemplates = new List<RoomTemplateData>();
        public List<RoomTemplateData> corridorTemplates = new List<RoomTemplateData>();
        public List<RoomTemplateData> normalTemplates = new List<RoomTemplateData>();
        public List<RoomTemplateData> specialBranchTemplates = new List<RoomTemplateData>();
        public List<RoomTemplateData> exitTemplates = new List<RoomTemplateData>();
    }

    /// <summary>
    /// Two-phase graph-first & socket-snapping 3D dungeon layout solver.
    /// Enforces rigorous topological constraints:
    /// 1. 2D Grid Graph Topology (Self-Avoiding Random Walk) -> 3D AABB Socket Placement.
    /// 2. Zero dead-end corridors (every corridor connects two major rooms, degree == 2) & solid walls on unused sockets.
    /// 3. Linear Critical Path (Start -> Combat_1 -> Combat_2 -> [Combat_3] -> Boss) with Degree(Start) == 1 and Degree(Boss) == 1.
    /// 4. Node Degree Role Rule: Every leaf major room (degree == 1, except Start/Boss) is a Treasure/Reward room;
    ///    every internal major room (degree >= 2) is a Combat/Normal room.
    /// 5. Validation Gate (IsGraphValid) before accepting any layout.
    /// </summary>
    public static class DungeonLayoutPlanner
    {
        private sealed class OpenSocketState
        {
            public int roomId;
            public string socketName;
            public Vector3 worldPosition;
            public DoorDirection worldDirection;
            public bool isUsed;
        }

        private sealed class PlacedRoomState
        {
            public PlacedRoomData data;
            public RoomTemplateData template;
            public Vector2Int gridCoord;
            public List<OpenSocketState> sockets = new List<OpenSocketState>();
        }

        /// <summary>
        /// Plans a valid dungeon layout from the supplied config and seed.
        /// Retries with derived seeds if an individual backtrack search hits the step budget.
        /// </summary>
        public static bool TryPlan(
            DungeonPlanRequest request,
            int initialSeed,
            out DungeonLayoutData layout,
            out string errorMessage)
        {
            layout = null;
            errorMessage = null;

            if (!ValidateRequest(request, out errorMessage))
            {
                return false;
            }

            int attempts = Mathf.Max(1, request.maxGenerationAttempts);
            string lastValidationError = null;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int attemptSeed = attempt == 0 ? initialSeed : unchecked(initialSeed * 397 ^ (attempt + 1) * 7919);
                if (TryPlanSingleAttempt(request, attemptSeed, out layout, out lastValidationError))
                {
                    layout.seed = initialSeed;
                    return true;
                }
            }

            errorMessage = $"Failed to find a valid layout after {attempts} attempts. Last validation: {lastValidationError}";
            return false;
        }

        /// <summary>
        /// Hard Rule 5: Validation Gate verifying all structural constraints on the planned graph before 3D instantiation.
        /// </summary>
        public static bool IsGraphValid(DungeonLayoutData layout, out string validationError)
        {
            validationError = null;
            if (layout == null || layout.rooms == null || layout.rooms.Count == 0)
            {
                validationError = "Layout is null or contains no rooms.";
                return false;
            }

            if (!layout.isReachableFromStartToExit)
            {
                validationError = "Start to Exit is not reachable.";
                return false;
            }

            if (layout.hasAnyOverlap)
            {
                validationError = "Layout contains overlapping room bounds.";
                return false;
            }

            var degree = new Dictionary<int, int>(layout.rooms.Count);
            var neighbors = new Dictionary<int, List<int>>(layout.rooms.Count);
            bool hasCorridors = false;

            foreach (PlacedRoomData r in layout.rooms)
            {
                degree[r.roomId] = 0;
                neighbors[r.roomId] = new List<int>();
                if (r.roomType == RoomType.Corridor)
                {
                    hasCorridors = true;
                }
            }

            foreach (RoomConnectionData conn in layout.connections)
            {
                if (!degree.ContainsKey(conn.fromRoomId) || !degree.ContainsKey(conn.toRoomId))
                {
                    validationError = "Connection references invalid room ID.";
                    return false;
                }

                degree[conn.fromRoomId]++;
                degree[conn.toRoomId]++;
                neighbors[conn.fromRoomId].Add(conn.toRoomId);
                neighbors[conn.toRoomId].Add(conn.fromRoomId);

                if (hasCorridors)
                {
                    PlacedRoomData fromRoom = layout.GetRoomById(conn.fromRoomId);
                    PlacedRoomData toRoom = layout.GetRoomById(conn.toRoomId);
                    bool fromIsCorridor = fromRoom.roomType == RoomType.Corridor;
                    bool toIsCorridor = toRoom.roomType == RoomType.Corridor;

                    // Every connection when corridors are enabled must bridge a Major Room and a Corridor
                    if (fromIsCorridor == toIsCorridor)
                    {
                        validationError = $"Invalid direct connection between {fromRoom.roomType} and {toRoom.roomType}.";
                        return false;
                    }
                }
            }

            int startCount = 0;
            int exitCount = 0;
            int treasureCount = 0;

            foreach (PlacedRoomData room in layout.rooms)
            {
                int deg = degree[room.roomId];

                if (room.roomType == RoomType.Start)
                {
                    startCount++;
                    // Rule 5.1: Degree(Start) == 1
                    if (deg != 1)
                    {
                        validationError = $"Start room (id={room.roomId}) has degree {deg}, expected 1.";
                        return false;
                    }
                }
                else if (room.roomType == RoomType.Exit)
                {
                    exitCount++;
                    // Rule 5.2: Degree(Exit/Boss) == 1
                    if (deg != 1)
                    {
                        validationError = $"Boss/Exit room (id={room.roomId}) has degree {deg}, expected 1.";
                        return false;
                    }
                }
                else if (room.roomType == RoomType.Corridor)
                {
                    // Rule 2: Zero dead-end corridors! Every corridor must connect exactly 2 rooms (degree == 2)
                    if (deg != 2)
                    {
                        validationError = $"Corridor room (id={room.roomId}) has degree {deg}, expected 2 (no dead-end corridors allowed).";
                        return false;
                    }
                }
                else if (room.roomType == RoomType.Treasure)
                {
                    treasureCount++;
                    // Rule 4 & 5.4: Every Treasure room must be a leaf node (degree == 1) and not adjacent to Start
                    if (deg != 1)
                    {
                        validationError = $"Treasure room (id={room.roomId}) has degree {deg}, expected 1 (leaf node).";
                        return false;
                    }

                    if (room.graphDepth < 2)
                    {
                        validationError = $"Treasure room (id={room.roomId}) is too close to Start (depth={room.graphDepth}).";
                        return false;
                    }

                    // Ensure Treasure room is not connected to Start even through a single corridor
                    foreach (int nbId in neighbors[room.roomId])
                    {
                        if (nbId == layout.startRoomId)
                        {
                            validationError = "Treasure room connects directly to Start room.";
                            return false;
                        }
                        foreach (int secondHopId in neighbors[nbId])
                        {
                            if (secondHopId == layout.startRoomId)
                            {
                                validationError = "Treasure room branches directly from Start room via corridor.";
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    // Rule 4: Normal / Combat rooms MUST be internal nodes (degree >= 2), never dead-end leaves!
                    if (deg < 2)
                    {
                        validationError = $"Combat/Normal room (id={room.roomId}) is a dead-end leaf (degree={deg}), which violates the Node Degree Role Rule.";
                        return false;
                    }
                }
            }

            if (startCount != 1)
            {
                validationError = $"Expected 1 Start room, found {startCount}.";
                return false;
            }

            if (exitCount != 1)
            {
                validationError = $"Expected 1 Exit/Boss room, found {exitCount}.";
                return false;
            }

            // Rule 5.3: Critical path must traverse at least 2 Combat/Normal rooms between Start and Boss
            // (i.e. >= 4 Major rooms on main path: Start -> Combat_1 -> Combat_2 -> [Combat_3] -> Boss)
            int totalMajorRooms = 0;
            foreach (PlacedRoomData r in layout.rooms)
            {
                if (r.roomType != RoomType.Corridor)
                {
                    totalMajorRooms++;
                }
            }

            int majorRoomsOnMainPath = CountMajorRoomsOnMainPath(layout);
            int minRequiredMajorOnMain = totalMajorRooms >= 4 ? 4 : Mathf.Max(2, totalMajorRooms);
            if (layout.mainPathRoomIds == null || majorRoomsOnMainPath < minRequiredMajorOnMain)
            {
                validationError = $"Main path has only {majorRoomsOnMainPath} major rooms (expected >= {minRequiredMajorOnMain}).";
                return false;
            }

            return true;
        }

        public static int CountMajorRoomsOnMainPath(DungeonLayoutData layout)
        {
            if (layout == null || layout.mainPathRoomIds == null)
            {
                return 0;
            }

            int count = 0;
            foreach (int roomId in layout.mainPathRoomIds)
            {
                PlacedRoomData r = layout.GetRoomById(roomId);
                if (r != null && r.roomType != RoomType.Corridor)
                {
                    count++;
                }
            }
            return count;
        }

        public static int GetRoomDegree(DungeonLayoutData layout, int roomId)
        {
            if (layout == null || layout.connections == null)
            {
                return 0;
            }

            int deg = 0;
            foreach (RoomConnectionData c in layout.connections)
            {
                if (c.fromRoomId == roomId || c.toRoomId == roomId)
                {
                    deg++;
                }
            }
            return deg;
        }

        private static bool ValidateRequest(DungeonPlanRequest request, out string error)
        {
            if (request == null)
            {
                error = "DungeonPlanRequest is null.";
                return false;
            }

            if (request.startTemplates == null || request.startTemplates.Count == 0)
            {
                error = "At least one Start room template is required.";
                return false;
            }

            if (request.exitTemplates == null || request.exitTemplates.Count == 0)
            {
                error = "At least one Exit room template is required.";
                return false;
            }

            if (request.minMainPathLength > 2 &&
                (request.normalTemplates == null || request.normalTemplates.Count == 0))
            {
                error = "Normal room templates are required when minMainPathLength > 2.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryPlanSingleAttempt(
            DungeonPlanRequest request,
            int seed,
            out DungeonLayoutData layout,
            out string validationError)
        {
            layout = null;
            validationError = null;

            var corridorPool = GetCorridorPool(request);
            var majorRoomPool = GetMajorRoomPool(request);
            bool hasCorridors = corridorPool.Count > 0;

            // Determine desired number of intermediate Combat rooms on the Critical Path:
            // Standard critical path: Start -> Combat_1 -> Combat_2 -> Combat_3 -> Boss (3 intermediate combat rooms = 5 major rooms)
            int primaryCombatCount;
            if (request.minMainPathLength <= 2)
            {
                primaryCombatCount = 0;
            }
            else if (request.minMainPathLength >= 5 || request.targetRoomCount >= 9)
            {
                primaryCombatCount = 3;
            }
            else if (request.minMainPathLength >= 4 || request.targetRoomCount >= 6)
            {
                primaryCombatCount = 2;
            }
            else
            {
                primaryCombatCount = Mathf.Max(1, request.minMainPathLength - 2);
            }

            // Try with primaryCombatCount (e.g. 3 combat rooms), and fall back to 2 if space is constrained
            int[] candidateCombatCounts = primaryCombatCount >= 3
                ? new[] { primaryCombatCount, 2 }
                : new[] { primaryCombatCount };

            foreach (int combatCount in candidateCombatCounts)
            {
                var rng = new System.Random(seed);
                var placedRooms = new List<PlacedRoomState>();
                var connections = new List<RoomConnectionData>();
                var templateCounts = new Dictionary<string, int>();
                var occupiedGrid = new HashSet<Vector2Int>();

                // 1. Place Start Room at origin (0, 0) on 2D grid and (0, 0, 0) in 3D space
                RoomTemplateData startTemplate = PickWeightedTemplate(request.startTemplates, templateCounts, rng, null);
                if (startTemplate == null || startTemplate.sockets == null || startTemplate.sockets.Count == 0)
                {
                    validationError = "Start template is missing sockets.";
                    return false;
                }

                int startYaw = startTemplate.allowRotation ? rng.Next(0, 4) : 0;
                PlacedRoomState startState = CreatePlacedRoom(0, startTemplate, Vector3.zero, startYaw, Vector2Int.zero);
                placedRooms.Add(startState);
                occupiedGrid.Add(Vector2Int.zero);
                IncrementCount(templateCounts, startTemplate.templateId);

                int remainingStepsBudget = Mathf.Max(150, request.maxBacktrackSteps);

                if (TryBuildCriticalPathAndBranchesRecursive(
                        request,
                        rng,
                        placedRooms,
                        connections,
                        templateCounts,
                        occupiedGrid,
                        currentMajorRoomId: 0,
                        remainingCombatRooms: combatCount,
                        hasBranchCapableRoomOnPath: false,
                        lastMajorTemplateId: startTemplate.templateId,
                        stepsBudget: ref remainingStepsBudget,
                        corridorPool: corridorPool,
                        majorRoomPool: majorRoomPool,
                        out layout,
                        out validationError))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildCriticalPathAndBranchesRecursive(
            DungeonPlanRequest request,
            System.Random rng,
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            HashSet<Vector2Int> occupiedGrid,
            int currentMajorRoomId,
            int remainingCombatRooms,
            bool hasBranchCapableRoomOnPath,
            string lastMajorTemplateId,
            ref int stepsBudget,
            List<RoomTemplateData> corridorPool,
            List<RoomTemplateData> majorRoomPool,
            out DungeonLayoutData validLayout,
            out string validationError)
        {
            validLayout = null;
            validationError = null;

            if (stepsBudget <= 0)
            {
                validationError = "Backtrack step budget exhausted.";
                return false;
            }

            stepsBudget--;
            PlacedRoomState hostMajor = placedRooms[currentMajorRoomId];

            // Hard Rule 3: Start room (id == 0) is ONLY allowed 1 connection (Degree(Start) == 1)
            if (currentMajorRoomId == 0 && CountUsedSockets(hostMajor) > 0)
            {
                return false;
            }

            List<OpenSocketState> candidateHostSockets = CollectShuffledUnusedSockets(hostMajor, rng);
            bool hasCorridors = corridorPool != null && corridorPool.Count > 0;
            bool requiresSpecialBranch = HasRequiredSpecialTemplates(request);

            // Base Case: All intermediate Combat rooms placed -> attach Boss/Exit Room, then attach Treasure/Special branches & validate!
            if (remainingCombatRooms == 0)
            {
                List<RoomTemplateData> exitPool = ShuffleWeightedOrder(request.exitTemplates, templateCounts, rng, null);
                foreach (OpenSocketState hostSocket in candidateHostSockets)
                {
                    Vector2Int exitGridCoord = hostMajor.gridCoord + DirectionToGridDelta(hostSocket.worldDirection);
                    if (occupiedGrid.Contains(exitGridCoord))
                    {
                        continue;
                    }

                    foreach (RoomTemplateData exitTemplate in exitPool)
                    {
                        if (!TrySnapEdgeAndMajorRoom(
                                hostSocket,
                                exitTemplate,
                                exitGridCoord,
                                placedRooms,
                                connections,
                                templateCounts,
                                occupiedGrid,
                                corridorPool,
                                request.boundsOverlapMargin,
                                rng,
                                out int exitRoomId,
                                out int roomsAddedForExit,
                                out int connsAddedForExit,
                                out OpenSocketState corridorMatchedSocket,
                                out OpenSocketState exitMatchedSocket,
                                out string usedCorridorTemplateId))
                        {
                            continue;
                        }

                        // Now expand purposeful side branches (Treasure / Special leaf rooms) from intermediate Combat rooms
                        int roomsBeforeBranches = placedRooms.Count;
                        int connsBeforeBranches = connections.Count;
                        var countsSnapshot = new Dictionary<string, int>(templateCounts);
                        var gridSnapshot = new HashSet<Vector2Int>(occupiedGrid);
                        var socketUsageSnapshot = CaptureSocketUsage(placedRooms);

                        bool branchesOk = TryExpandDisciplinedBranches(
                            request,
                            rng,
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            exitRoomId,
                            corridorPool,
                            majorRoomPool);

                        if (branchesOk)
                        {
                            DungeonLayoutData candidateLayout = BuildFinalLayout(
                                rng.Next(),
                                placedRooms,
                                connections,
                                startRoomId: 0,
                                exitRoomId: exitRoomId,
                                overlapMargin: request.boundsOverlapMargin);

                            // Ensure deterministic seed is preserved
                            if (IsGraphValid(candidateLayout, out validationError) &&
                                (!requiresSpecialBranch || CountRoomType(candidateLayout, RoomType.Treasure) >= 1) &&
                                candidateLayout.mainPathRoomIds.Count >= request.minMainPathLength)
                            {
                                validLayout = candidateLayout;
                                return true;
                            }
                        }

                        // Rollback branches
                        RestoreSnapshot(
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            roomsBeforeBranches,
                            connsBeforeBranches,
                            countsSnapshot,
                            gridSnapshot,
                            socketUsageSnapshot);

                        // Rollback Exit (+ Corridor)
                        RollbackEdgeAndMajorRoom(
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            exitGridCoord,
                            hostSocket,
                            corridorMatchedSocket,
                            exitMatchedSocket,
                            exitTemplate.templateId,
                            usedCorridorTemplateId,
                            roomsAddedForExit,
                            connsAddedForExit);
                    }
                }

                return false;
            }

            // Recursive Step: Attach an intermediate Combat / Normal room on the Critical Path
            var eligibleMajors = new List<RoomTemplateData>();
            bool mustHaveBranchSocket = requiresSpecialBranch && !hasBranchCapableRoomOnPath && remainingCombatRooms == 1;

            foreach (RoomTemplateData t in majorRoomPool)
            {
                if (t == null || t.sockets == null || t.sockets.Count < 2 || !CanUseTemplate(t, templateCounts))
                {
                    continue;
                }

                if (mustHaveBranchSocket && t.sockets.Count < 3)
                {
                    continue;
                }

                eligibleMajors.Add(t);
            }

            // Fallback if no >=3 socket template exists in pool
            if (eligibleMajors.Count == 0)
            {
                foreach (RoomTemplateData t in majorRoomPool)
                {
                    if (t != null && t.sockets != null && t.sockets.Count >= 2 && CanUseTemplate(t, templateCounts))
                    {
                        eligibleMajors.Add(t);
                    }
                }
            }

            if (eligibleMajors.Count == 0)
            {
                return false;
            }

            List<RoomTemplateData> orderedMajors = ShuffleWeightedOrder(eligibleMajors, templateCounts, rng, lastMajorTemplateId);

            foreach (OpenSocketState hostSocket in candidateHostSockets)
            {
                Vector2Int nextGridCoord = hostMajor.gridCoord + DirectionToGridDelta(hostSocket.worldDirection);
                // Hard Rule 1 & 3: Self-Avoiding Random Walk on 2D Grid
                if (occupiedGrid.Contains(nextGridCoord))
                {
                    continue;
                }

                foreach (RoomTemplateData combatTemplate in orderedMajors)
                {
                    if (!CanUseTemplate(combatTemplate, templateCounts))
                    {
                        continue;
                    }

                    if (!TrySnapEdgeAndMajorRoom(
                            hostSocket,
                            combatTemplate,
                            nextGridCoord,
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            corridorPool,
                            request.boundsOverlapMargin,
                            rng,
                            out int newCombatRoomId,
                            out int roomsAdded,
                            out int connsAdded,
                            out OpenSocketState corridorMatchedSocket,
                            out OpenSocketState majorMatchedSocket,
                            out string usedCorridorTemplateId))
                    {
                        continue;
                    }

                    bool nextHasBranchCapable = hasBranchCapableRoomOnPath || combatTemplate.sockets.Count >= 3;

                    if (TryBuildCriticalPathAndBranchesRecursive(
                            request,
                            rng,
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            currentMajorRoomId: newCombatRoomId,
                            remainingCombatRooms: remainingCombatRooms - 1,
                            hasBranchCapableRoomOnPath: nextHasBranchCapable,
                            lastMajorTemplateId: combatTemplate.templateId,
                            stepsBudget: ref stepsBudget,
                            corridorPool: corridorPool,
                            majorRoomPool: majorRoomPool,
                            out validLayout,
                            out validationError))
                    {
                        return true;
                    }

                    // Backtrack cleanly
                    RollbackEdgeAndMajorRoom(
                        placedRooms,
                        connections,
                        templateCounts,
                        occupiedGrid,
                        nextGridCoord,
                        hostSocket,
                        corridorMatchedSocket,
                        majorMatchedSocket,
                        combatTemplate.templateId,
                        usedCorridorTemplateId,
                        roomsAdded,
                        connsAdded);
                }
            }

            return false;
        }

        /// <summary>
        /// Atomically snaps (Corridor + TargetMajorRoom) when corridors are enabled, or TargetMajorRoom directly when not.
        /// Guarantees a corridor is NEVER placed without a valid destination room on the other end.
        /// </summary>
        private static bool TrySnapEdgeAndMajorRoom(
            OpenSocketState hostSocket,
            RoomTemplateData targetMajorTemplate,
            Vector2Int targetGridCoord,
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            HashSet<Vector2Int> occupiedGrid,
            List<RoomTemplateData> corridorPool,
            float overlapMargin,
            System.Random rng,
            out int placedMajorRoomId,
            out int roomsAddedCount,
            out int connsAddedCount,
            out OpenSocketState corridorMatchedSocket,
            out OpenSocketState majorMatchedSocket,
            out string usedCorridorTemplateId)
        {
            placedMajorRoomId = -1;
            roomsAddedCount = 0;
            connsAddedCount = 0;
            corridorMatchedSocket = null;
            majorMatchedSocket = null;
            usedCorridorTemplateId = null;

            bool hasCorridors = corridorPool != null && corridorPool.Count > 0;
            if (!hasCorridors)
            {
                if (!TrySnapRoom(
                        targetMajorTemplate,
                        hostSocket,
                        placedRooms,
                        overlapMargin,
                        rng,
                        out PlacedRoomState directMajor,
                        out majorMatchedSocket,
                        out RoomConnectionData directConn))
                {
                    return false;
                }

                directMajor.gridCoord = targetGridCoord;
                CommitPlacedRoom(placedRooms, connections, templateCounts, hostSocket, directMajor, majorMatchedSocket, directConn, targetMajorTemplate.templateId);
                occupiedGrid.Add(targetGridCoord);
                placedMajorRoomId = directMajor.data.roomId;
                roomsAddedCount = 1;
                connsAddedCount = 1;
                return true;
            }

            // Order corridors: prefer straight corridors (2 opposite sockets) first
            List<RoomTemplateData> orderedCorridors = OrderCorridorsStraightFirst(corridorPool, templateCounts, rng);
            foreach (RoomTemplateData corridorTemplate in orderedCorridors)
            {
                if (!CanUseTemplate(corridorTemplate, templateCounts))
                {
                    continue;
                }

                if (!TrySnapRoom(
                        corridorTemplate,
                        hostSocket,
                        placedRooms,
                        overlapMargin,
                        rng,
                        out PlacedRoomState placedCorridor,
                        out OpenSocketState matchedCorrInSocket,
                        out RoomConnectionData connToCorridor))
                {
                    continue;
                }

                // Temporarily commit corridor so targetMajorTemplate checks overlap against it as well
                placedCorridor.gridCoord = targetGridCoord;
                CommitPlacedRoom(placedRooms, connections, templateCounts, hostSocket, placedCorridor, matchedCorrInSocket, connToCorridor, corridorTemplate.templateId);

                // Prefer the corridor exit socket that continues straight along hostSocket.worldDirection
                List<OpenSocketState> corridorExitSockets = CollectCorridorExitSocketsOrdered(placedCorridor, hostSocket.worldDirection, rng);
                foreach (OpenSocketState corrOutSocket in corridorExitSockets)
                {
                    if (TrySnapRoom(
                            targetMajorTemplate,
                            corrOutSocket,
                            placedRooms,
                            overlapMargin,
                            rng,
                            out PlacedRoomState placedMajor,
                            out OpenSocketState matchedMajorInSocket,
                            out RoomConnectionData connToMajor))
                    {
                        placedMajor.gridCoord = targetGridCoord;
                        CommitPlacedRoom(placedRooms, connections, templateCounts, corrOutSocket, placedMajor, matchedMajorInSocket, connToMajor, targetMajorTemplate.templateId);
                        occupiedGrid.Add(targetGridCoord);

                        placedMajorRoomId = placedMajor.data.roomId;
                        roomsAddedCount = 2;
                        connsAddedCount = 2;
                        corridorMatchedSocket = matchedCorrInSocket;
                        majorMatchedSocket = matchedMajorInSocket;
                        usedCorridorTemplateId = corridorTemplate.templateId;
                        return true;
                    }
                }

                // Rollback temporary corridor
                DecrementCount(templateCounts, corridorTemplate.templateId);
                connections.RemoveAt(connections.Count - 1);
                placedRooms.RemoveAt(placedRooms.Count - 1);
                matchedCorrInSocket.isUsed = false;
                hostSocket.isUsed = false;
            }

            return false;
        }

        private static void RollbackEdgeAndMajorRoom(
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            HashSet<Vector2Int> occupiedGrid,
            Vector2Int targetGridCoord,
            OpenSocketState hostSocket,
            OpenSocketState corridorMatchedSocket,
            OpenSocketState majorMatchedSocket,
            string majorTemplateId,
            string corridorTemplateId,
            int roomsAdded,
            int connsAdded)
        {
            occupiedGrid.Remove(targetGridCoord);
            DecrementCount(templateCounts, majorTemplateId);
            if (!string.IsNullOrEmpty(corridorTemplateId))
            {
                DecrementCount(templateCounts, corridorTemplateId);
            }

            for (int i = 0; i < connsAdded && connections.Count > 0; i++)
            {
                connections.RemoveAt(connections.Count - 1);
            }

            for (int i = 0; i < roomsAdded && placedRooms.Count > 0; i++)
            {
                placedRooms.RemoveAt(placedRooms.Count - 1);
            }

            if (majorMatchedSocket != null) majorMatchedSocket.isUsed = false;
            if (corridorMatchedSocket != null) corridorMatchedSocket.isUsed = false;
            if (hostSocket != null) hostSocket.isUsed = false;
        }

        /// <summary>
        /// Hard Rules 2, 3, 4:
        /// Sprouts side branches ONLY from intermediate Combat/Normal rooms on the critical path (never Start or Boss).
        /// Every side branch MUST terminate in a Treasure/Special leaf room (degree == 1).
        /// Never creates a dead-end corridor or a dead-end Normal/Combat room.
        /// </summary>
        private static bool TryExpandDisciplinedBranches(
            DungeonPlanRequest request,
            System.Random rng,
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            HashSet<Vector2Int> occupiedGrid,
            int exitRoomId,
            List<RoomTemplateData> corridorPool,
            List<RoomTemplateData> majorRoomPool)
        {
            var specialPool = request.specialBranchTemplates ?? new List<RoomTemplateData>();
            if (specialPool.Count == 0)
            {
                return true;
            }

            // Collect candidate host sockets strictly on intermediate Combat/Normal rooms (exclude Start=0, Exit, and Corridors)
            List<OpenSocketState> branchHostSockets = CollectIntermediateCombatSockets(placedRooms, exitRoomId, rng);
            if (branchHostSockets.Count == 0)
            {
                return !HasRequiredSpecialTemplates(request);
            }

            // Determine how many reward branches to sprout (1 to 2 branches, each ending in a Treasure/Special room)
            int branchesPlaced = 0;
            int maxBranches = Mathf.Clamp((request.targetRoomCount - placedRooms.Count + 1) / 2, 1, 2);

            foreach (RoomTemplateData specialTemplate in specialPool)
            {
                if (specialTemplate == null)
                {
                    continue;
                }

                while (CanUseTemplate(specialTemplate, templateCounts) && branchesPlaced < maxBranches)
                {
                    branchHostSockets = CollectIntermediateCombatSockets(placedRooms, exitRoomId, rng);
                    if (branchHostSockets.Count == 0)
                    {
                        break;
                    }

                    // If room budget allows (+4 modules) and only 1 Treasure is allowed by maxCount, try a 2-step branch:
                    // MainPath_Combat -> Corridor -> Branch_Combat (deg=2) -> Corridor -> Treasure (deg=1)
                    bool tryTwoStepBranch = (placedRooms.Count + 4 <= request.targetRoomCount) &&
                                            (specialTemplate.maxCount == 1) &&
                                            (branchesPlaced == 0);

                    bool placedThisBranch = false;

                    if (tryTwoStepBranch)
                    {
                        placedThisBranch = TryPlaceTwoStepTreasureBranch(
                            branchHostSockets,
                            specialTemplate,
                            majorRoomPool,
                            corridorPool,
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            request.boundsOverlapMargin,
                            rng);
                    }

                    if (!placedThisBranch)
                    {
                        placedThisBranch = TryPlaceOneStepTreasureBranch(
                            branchHostSockets,
                            specialTemplate,
                            corridorPool,
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            request.boundsOverlapMargin,
                            rng);
                    }

                    if (placedThisBranch)
                    {
                        branchesPlaced++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Verify all minCount requirements for special templates are met
            foreach (RoomTemplateData sp in specialPool)
            {
                if (sp != null && sp.minCount > 0 && GetCount(templateCounts, sp.templateId) < sp.minCount)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryPlaceOneStepTreasureBranch(
            List<OpenSocketState> hostSockets,
            RoomTemplateData treasureTemplate,
            List<RoomTemplateData> corridorPool,
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            HashSet<Vector2Int> occupiedGrid,
            float overlapMargin,
            System.Random rng)
        {
            foreach (OpenSocketState hostSocket in hostSockets)
            {
                if (hostSocket.isUsed)
                {
                    continue;
                }

                PlacedRoomState hostRoom = placedRooms[hostSocket.roomId];
                Vector2Int leafGridCoord = hostRoom.gridCoord + DirectionToGridDelta(hostSocket.worldDirection);
                if (occupiedGrid.Contains(leafGridCoord))
                {
                    continue;
                }

                if (TrySnapEdgeAndMajorRoom(
                        hostSocket,
                        treasureTemplate,
                        leafGridCoord,
                        placedRooms,
                        connections,
                        templateCounts,
                        occupiedGrid,
                        corridorPool,
                        overlapMargin,
                        rng,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPlaceTwoStepTreasureBranch(
            List<OpenSocketState> hostSockets,
            RoomTemplateData treasureTemplate,
            List<RoomTemplateData> majorRoomPool,
            List<RoomTemplateData> corridorPool,
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            HashSet<Vector2Int> occupiedGrid,
            float overlapMargin,
            System.Random rng)
        {
            var eligibleBranchCombats = new List<RoomTemplateData>();
            foreach (RoomTemplateData t in majorRoomPool)
            {
                if (t != null && t.sockets != null && t.sockets.Count >= 2 && CanUseTemplate(t, templateCounts))
                {
                    eligibleBranchCombats.Add(t);
                }
            }

            if (eligibleBranchCombats.Count == 0)
            {
                return false;
            }

            foreach (OpenSocketState hostSocket in hostSockets)
            {
                if (hostSocket.isUsed)
                {
                    continue;
                }

                PlacedRoomState hostRoom = placedRooms[hostSocket.roomId];
                Vector2Int midGridCoord = hostRoom.gridCoord + DirectionToGridDelta(hostSocket.worldDirection);
                if (occupiedGrid.Contains(midGridCoord))
                {
                    continue;
                }

                List<RoomTemplateData> orderedCombats = ShuffleWeightedOrder(eligibleBranchCombats, templateCounts, rng, hostRoom.template.templateId);
                foreach (RoomTemplateData branchCombatTemplate in orderedCombats)
                {
                    if (!TrySnapEdgeAndMajorRoom(
                            hostSocket,
                            branchCombatTemplate,
                            midGridCoord,
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            corridorPool,
                            overlapMargin,
                            rng,
                            out int midCombatRoomId,
                            out int roomsAddedStep1,
                            out int connsAddedStep1,
                            out OpenSocketState corr1Socket,
                            out OpenSocketState midMajorSocket,
                            out string corr1TemplateId))
                    {
                        continue;
                    }

                    PlacedRoomState midCombatRoom = placedRooms[midCombatRoomId];
                    List<OpenSocketState> midSockets = CollectShuffledUnusedSockets(midCombatRoom, rng);
                    if (TryPlaceOneStepTreasureBranch(
                            midSockets,
                            treasureTemplate,
                            corridorPool,
                            placedRooms,
                            connections,
                            templateCounts,
                            occupiedGrid,
                            overlapMargin,
                            rng))
                    {
                        return true;
                    }

                    // Rollback step 1 if Treasure could not attach to midCombatRoom (never leave a Combat room as a dead-end leaf!)
                    RollbackEdgeAndMajorRoom(
                        placedRooms,
                        connections,
                        templateCounts,
                        occupiedGrid,
                        midGridCoord,
                        hostSocket,
                        corr1Socket,
                        midMajorSocket,
                        branchCombatTemplate.templateId,
                        corr1TemplateId,
                        roomsAddedStep1,
                        connsAddedStep1);
                }
            }

            return false;
        }

        private static List<OpenSocketState> CollectIntermediateCombatSockets(
            List<PlacedRoomState> placedRooms,
            int exitRoomId,
            System.Random rng)
        {
            var result = new List<OpenSocketState>();
            foreach (PlacedRoomState room in placedRooms)
            {
                int id = room.data.roomId;
                if (id == 0 || id == exitRoomId || IsCorridor(room.template))
                {
                    continue;
                }

                if (room.template.roomType != RoomType.Normal && room.template.roomType != RoomType.Combat)
                {
                    continue;
                }

                foreach (OpenSocketState s in room.sockets)
                {
                    if (!s.isUsed)
                    {
                        result.Add(s);
                    }
                }
            }

            ShuffleList(result, rng);
            return result;
        }

        private static List<RoomTemplateData> OrderCorridorsStraightFirst(
            List<RoomTemplateData> corridorPool,
            Dictionary<string, int> counts,
            System.Random rng)
        {
            var straight = new List<RoomTemplateData>();
            var other = new List<RoomTemplateData>();

            foreach (RoomTemplateData c in corridorPool)
            {
                if (c == null || !CanUseTemplate(c, counts) || c.sockets == null || c.sockets.Count < 2)
                {
                    continue;
                }

                if (c.sockets[0].localDirection.Opposite() == c.sockets[1].localDirection)
                {
                    straight.Add(c);
                }
                else
                {
                    other.Add(c);
                }
            }

            ShuffleList(straight, rng);
            ShuffleList(other, rng);
            straight.AddRange(other);
            return straight;
        }

        private static List<OpenSocketState> CollectCorridorExitSocketsOrdered(
            PlacedRoomState corridor,
            DoorDirection preferredDirection,
            System.Random rng)
        {
            var preferred = new List<OpenSocketState>();
            var fallback = new List<OpenSocketState>();

            foreach (OpenSocketState s in corridor.sockets)
            {
                if (s.isUsed)
                {
                    continue;
                }

                if (s.worldDirection == preferredDirection)
                {
                    preferred.Add(s);
                }
                else
                {
                    fallback.Add(s);
                }
            }

            ShuffleList(fallback, rng);
            preferred.AddRange(fallback);
            return preferred;
        }

        private static Vector2Int DirectionToGridDelta(DoorDirection dir)
        {
            return dir switch
            {
                DoorDirection.North => new Vector2Int(0, 1),
                DoorDirection.East => new Vector2Int(1, 0),
                DoorDirection.South => new Vector2Int(0, -1),
                DoorDirection.West => new Vector2Int(-1, 0),
                _ => new Vector2Int(0, 1)
            };
        }

        private static int CountUsedSockets(PlacedRoomState room)
        {
            int used = 0;
            foreach (OpenSocketState s in room.sockets)
            {
                if (s.isUsed) used++;
            }
            return used;
        }

        private static bool HasRequiredSpecialTemplates(DungeonPlanRequest request)
        {
            if (request.specialBranchTemplates == null)
            {
                return false;
            }

            foreach (RoomTemplateData t in request.specialBranchTemplates)
            {
                if (t != null && t.minCount > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static int CountRoomType(DungeonLayoutData layout, RoomType type)
        {
            int c = 0;
            foreach (PlacedRoomData r in layout.rooms)
            {
                if (r.roomType == type) c++;
            }
            return c;
        }

        private static List<bool[]> CaptureSocketUsage(List<PlacedRoomState> rooms)
        {
            var snapshot = new List<bool[]>(rooms.Count);
            foreach (PlacedRoomState r in rooms)
            {
                var arr = new bool[r.sockets.Count];
                for (int i = 0; i < r.sockets.Count; i++)
                {
                    arr[i] = r.sockets[i].isUsed;
                }
                snapshot.Add(arr);
            }
            return snapshot;
        }

        private static void RestoreSnapshot(
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            HashSet<Vector2Int> occupiedGrid,
            int targetRoomCount,
            int targetConnCount,
            Dictionary<string, int> countsSnapshot,
            HashSet<Vector2Int> gridSnapshot,
            List<bool[]> socketUsageSnapshot)
        {
            while (placedRooms.Count > targetRoomCount)
            {
                placedRooms.RemoveAt(placedRooms.Count - 1);
            }

            while (connections.Count > targetConnCount)
            {
                connections.RemoveAt(connections.Count - 1);
            }

            templateCounts.Clear();
            foreach (KeyValuePair<string, int> kv in countsSnapshot)
            {
                templateCounts[kv.Key] = kv.Value;
            }

            occupiedGrid.Clear();
            foreach (Vector2Int g in gridSnapshot)
            {
                occupiedGrid.Add(g);
            }

            for (int i = 0; i < socketUsageSnapshot.Count && i < placedRooms.Count; i++)
            {
                bool[] arr = socketUsageSnapshot[i];
                for (int j = 0; j < arr.Length && j < placedRooms[i].sockets.Count; j++)
                {
                    placedRooms[i].sockets[j].isUsed = arr[j];
                }
            }
        }

        private static bool IsCorridor(RoomTemplateData template)
        {
            return template != null && template.roomType == RoomType.Corridor;
        }

        private static List<RoomTemplateData> GetCorridorPool(DungeonPlanRequest request)
        {
            var pool = new List<RoomTemplateData>();
            if (request.corridorTemplates != null)
            {
                pool.AddRange(request.corridorTemplates);
            }
            if (request.normalTemplates != null)
            {
                foreach (RoomTemplateData t in request.normalTemplates)
                {
                    if (t != null && t.roomType == RoomType.Corridor && !pool.Contains(t))
                    {
                        pool.Add(t);
                    }
                }
            }
            return pool;
        }

        private static List<RoomTemplateData> GetMajorRoomPool(DungeonPlanRequest request)
        {
            var pool = new List<RoomTemplateData>();
            if (request.normalTemplates != null)
            {
                foreach (RoomTemplateData t in request.normalTemplates)
                {
                    if (t != null && t.roomType != RoomType.Corridor && !pool.Contains(t))
                    {
                        pool.Add(t);
                    }
                }
            }
            return pool;
        }

        private static void CommitPlacedRoom(
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            Dictionary<string, int> templateCounts,
            OpenSocketState hostSocket,
            PlacedRoomState placedRoom,
            OpenSocketState matchedSocket,
            RoomConnectionData connection,
            string templateId)
        {
            int newId = placedRooms.Count;
            placedRoom.data.roomId = newId;
            connection.toRoomId = newId;
            foreach (OpenSocketState s in placedRoom.sockets)
            {
                s.roomId = newId;
            }

            hostSocket.isUsed = true;
            matchedSocket.isUsed = true;
            placedRooms.Add(placedRoom);
            connections.Add(connection);
            IncrementCount(templateCounts, templateId);
        }

        private static bool TrySnapRoom(
            RoomTemplateData candidateTemplate,
            OpenSocketState hostSocket,
            List<PlacedRoomState> placedRooms,
            float overlapMargin,
            System.Random rng,
            out PlacedRoomState placedRoom,
            out OpenSocketState matchedSocket,
            out RoomConnectionData connection)
        {
            placedRoom = null;
            matchedSocket = null;
            connection = null;

            if (candidateTemplate == null || candidateTemplate.sockets == null || candidateTemplate.sockets.Count == 0)
            {
                return false;
            }

            var socketIndices = new List<int>(candidateTemplate.sockets.Count);
            for (int i = 0; i < candidateTemplate.sockets.Count; i++)
            {
                socketIndices.Add(i);
            }
            ShuffleList(socketIndices, rng);

            DoorDirection desiredCandidateWorldDir = hostSocket.worldDirection.Opposite();

            foreach (int socketIdx in socketIndices)
            {
                SocketDefinition candidateSockDef = candidateTemplate.sockets[socketIdx];
                int yawSteps = DoorDirectionUtility.StepsToAlign(candidateSockDef.localDirection, desiredCandidateWorldDir);

                if (!candidateTemplate.allowRotation && yawSteps != 0)
                {
                    continue;
                }

                float yawDegrees = DoorDirectionUtility.StepsToYawDegrees(yawSteps);
                Quaternion roomRot = Quaternion.Euler(0f, yawDegrees, 0f);
                Vector3 candidateWorldPos = hostSocket.worldPosition - (roomRot * candidateSockDef.localPosition);

                Bounds candidateBounds = RoomModule.CalculateWorldBounds(candidateTemplate.localBounds, candidateWorldPos, yawSteps);
                if (HasAnyBoundsOverlap(candidateBounds, placedRooms, overlapMargin))
                {
                    continue;
                }

                placedRoom = CreatePlacedRoom(-1, candidateTemplate, candidateWorldPos, yawSteps, Vector2Int.zero);
                matchedSocket = placedRoom.sockets[socketIdx];
                connection = new RoomConnectionData
                {
                    fromRoomId = hostSocket.roomId,
                    fromSocketName = hostSocket.socketName,
                    toRoomId = -1,
                    toSocketName = candidateSockDef.socketName,
                    doorWorldPosition = hostSocket.worldPosition
                };
                return true;
            }

            return false;
        }

        private static PlacedRoomState CreatePlacedRoom(
            int roomId,
            RoomTemplateData template,
            Vector3 worldPos,
            int yawSteps,
            Vector2Int gridCoord)
        {
            float yawDegrees = DoorDirectionUtility.StepsToYawDegrees(yawSteps);
            Quaternion rot = Quaternion.Euler(0f, yawDegrees, 0f);
            Bounds worldBounds = RoomModule.CalculateWorldBounds(template.localBounds, worldPos, yawSteps);

            var data = new PlacedRoomData
            {
                roomId = roomId,
                templateId = template.templateId,
                roomType = template.roomType,
                worldPosition = worldPos,
                yawSteps = yawSteps,
                worldRotationEuler = new Vector3(0f, yawDegrees, 0f),
                worldBounds = worldBounds,
                graphDepth = 0,
                isOnMainPath = false
            };

            if (template.enemySpawnLocalOffsets != null)
            {
                foreach (Vector3 localSpawn in template.enemySpawnLocalOffsets)
                {
                    data.enemySpawnPositions.Add(worldPos + rot * localSpawn);
                }
            }

            var state = new PlacedRoomState
            {
                data = data,
                template = template,
                gridCoord = gridCoord
            };

            foreach (SocketDefinition s in template.sockets)
            {
                state.sockets.Add(new OpenSocketState
                {
                    roomId = roomId,
                    socketName = s.socketName,
                    worldPosition = worldPos + rot * s.localPosition,
                    worldDirection = s.localDirection.RotateClockwiseSteps(yawSteps),
                    isUsed = false
                });
            }

            return state;
        }

        private static DungeonLayoutData BuildFinalLayout(
            int seed,
            List<PlacedRoomState> placedRooms,
            List<RoomConnectionData> connections,
            int startRoomId,
            int exitRoomId,
            float overlapMargin)
        {
            var layout = new DungeonLayoutData
            {
                seed = seed,
                startRoomId = startRoomId,
                exitRoomId = exitRoomId
            };

            // Build adjacency graph for BFS reachability & shortest critical path
            var adjacency = new Dictionary<int, List<int>>(placedRooms.Count);
            foreach (PlacedRoomState room in placedRooms)
            {
                room.data.graphDepth = 0;
                room.data.isOnMainPath = false;
                adjacency[room.data.roomId] = new List<int>();
                layout.rooms.Add(room.data);
                layout.enemySpawnPositions.AddRange(room.data.enemySpawnPositions);
            }

            foreach (RoomConnectionData conn in connections)
            {
                layout.connections.Add(conn);
                if (adjacency.ContainsKey(conn.fromRoomId) && adjacency.ContainsKey(conn.toRoomId))
                {
                    adjacency[conn.fromRoomId].Add(conn.toRoomId);
                    adjacency[conn.toRoomId].Add(conn.fromRoomId);
                }
            }

            // BFS from Start Room
            var visited = new HashSet<int> { startRoomId };
            var parent = new Dictionary<int, int>();
            var queue = new Queue<int>();
            queue.Enqueue(startRoomId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                PlacedRoomData currentData = layout.GetRoomById(current);

                foreach (int neighbor in adjacency[current])
                {
                    if (!visited.Add(neighbor))
                    {
                        continue;
                    }

                    parent[neighbor] = current;
                    PlacedRoomData neighborData = layout.GetRoomById(neighbor);
                    if (neighborData != null && currentData != null)
                    {
                        neighborData.graphDepth = currentData.graphDepth + 1;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            layout.isReachableFromStartToExit = visited.Contains(exitRoomId);

            if (layout.isReachableFromStartToExit)
            {
                var path = new List<int>();
                int trace = exitRoomId;
                while (true)
                {
                    path.Add(trace);
                    PlacedRoomData roomData = layout.GetRoomById(trace);
                    if (roomData != null)
                    {
                        roomData.isOnMainPath = true;
                    }

                    if (trace == startRoomId || !parent.TryGetValue(trace, out int prev))
                    {
                        break;
                    }
                    trace = prev;
                }

                path.Reverse();
                layout.mainPathRoomIds = path;
            }

            // Player spawn & Exit world positions
            PlacedRoomState startState = placedRooms[startRoomId];
            Quaternion startRot = Quaternion.Euler(startState.data.worldRotationEuler);
            layout.playerSpawnPosition = startState.template.hasPlayerSpawn
                ? startState.data.worldPosition + startRot * startState.template.playerSpawnLocalOffset
                : startState.data.worldPosition;
            layout.playerSpawnRotation = startRot;

            PlacedRoomState exitState = placedRooms[exitRoomId];
            Quaternion exitRot = Quaternion.Euler(exitState.data.worldRotationEuler);
            layout.exitPosition = exitState.template.hasExitPoint
                ? exitState.data.worldPosition + exitRot * exitState.template.exitLocalOffset
                : exitState.data.worldPosition;

            // Final pairwise overlap verification
            layout.hasAnyOverlap = CheckPairwiseOverlap(layout.rooms, overlapMargin);
            return layout;
        }

        public static bool CheckPairwiseOverlap(IReadOnlyList<PlacedRoomData> rooms, float margin)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (BoundsOverlapXZWithMargin(rooms[i].worldBounds, rooms[j].worldBounds, margin))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasAnyBoundsOverlap(Bounds candidate, List<PlacedRoomState> existingRooms, float margin)
        {
            foreach (PlacedRoomState existing in existingRooms)
            {
                if (BoundsOverlapXZWithMargin(candidate, existing.data.worldBounds, margin))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BoundsOverlapXZWithMargin(Bounds a, Bounds b, float margin)
        {
            float eps = Mathf.Max(0.005f, margin);
            bool overlapX = (a.min.x + eps) < (b.max.x - eps) && (a.max.x - eps) > (b.min.x + eps);
            bool overlapY = (a.min.y + eps) < (b.max.y - eps) && (a.max.y - eps) > (b.min.y + eps);
            bool overlapZ = (a.min.z + eps) < (b.max.z - eps) && (a.max.z - eps) > (b.min.z + eps);
            return overlapX && overlapY && overlapZ;
        }

        private static List<OpenSocketState> CollectShuffledUnusedSockets(PlacedRoomState room, System.Random rng)
        {
            var result = new List<OpenSocketState>();
            foreach (OpenSocketState s in room.sockets)
            {
                if (!s.isUsed)
                {
                    result.Add(s);
                }
            }
            ShuffleList(result, rng);
            return result;
        }

        private static bool CanUseTemplate(RoomTemplateData template, Dictionary<string, int> counts)
        {
            if (template == null)
            {
                return false;
            }

            if (template.maxCount <= 0)
            {
                return true;
            }

            return GetCount(counts, template.templateId) < template.maxCount;
        }

        private static RoomTemplateData PickWeightedTemplate(
            List<RoomTemplateData> pool,
            Dictionary<string, int> counts,
            System.Random rng,
            string avoidTemplateId)
        {
            List<RoomTemplateData> ordered = ShuffleWeightedOrder(pool, counts, rng, avoidTemplateId);
            return ordered.Count > 0 ? ordered[0] : null;
        }

        private static List<RoomTemplateData> ShuffleWeightedOrder(
            List<RoomTemplateData> pool,
            Dictionary<string, int> counts,
            System.Random rng,
            string avoidTemplateId)
        {
            var eligible = new List<RoomTemplateData>();
            if (pool != null)
            {
                foreach (RoomTemplateData t in pool)
                {
                    if (t != null && CanUseTemplate(t, counts))
                    {
                        eligible.Add(t);
                    }
                }
            }

            var ordered = new List<RoomTemplateData>(eligible.Count);
            var remaining = new List<RoomTemplateData>(eligible);

            while (remaining.Count > 0)
            {
                double totalWeight = 0.0;
                foreach (RoomTemplateData item in remaining)
                {
                    double w = Math.Max(0.01, item.selectionWeight);
                    if (!string.IsNullOrEmpty(avoidTemplateId) && item.templateId == avoidTemplateId && remaining.Count > 1)
                    {
                        w *= 0.25;
                    }
                    totalWeight += w;
                }

                double roll = rng.NextDouble() * totalWeight;
                double acc = 0.0;
                int chosenIndex = remaining.Count - 1;
                for (int i = 0; i < remaining.Count; i++)
                {
                    double w = Math.Max(0.01, remaining[i].selectionWeight);
                    if (!string.IsNullOrEmpty(avoidTemplateId) && remaining[i].templateId == avoidTemplateId && remaining.Count > 1)
                    {
                        w *= 0.25;
                    }

                    acc += w;
                    if (roll <= acc)
                    {
                        chosenIndex = i;
                        break;
                    }
                }

                ordered.Add(remaining[chosenIndex]);
                remaining.RemoveAt(chosenIndex);
            }

            return ordered;
        }

        private static void ShuffleList<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static int GetCount(Dictionary<string, int> counts, string id)
        {
            return counts.TryGetValue(id, out int val) ? val : 0;
        }

        private static void IncrementCount(Dictionary<string, int> counts, string id)
        {
            counts[id] = GetCount(counts, id) + 1;
        }

        private static void DecrementCount(Dictionary<string, int> counts, string id)
        {
            int c = GetCount(counts, id);
            if (c <= 1)
            {
                counts.Remove(id);
            }
            else
            {
                counts[id] = c - 1;
            }
        }
    }
}
