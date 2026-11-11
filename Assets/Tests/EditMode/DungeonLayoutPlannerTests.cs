using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class DungeonLayoutPlannerTests
    {
        private DungeonPlanRequest CreateSampleRequest(int targetRooms = 10, int minMainPath = 4)
        {
            var start = new RoomTemplateData
            {
                templateId = "Room_Start",
                roomType = RoomType.Start,
                selectionWeight = 1f,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(16f, 4f, 16f)),
                hasPlayerSpawn = true,
                playerSpawnLocalOffset = Vector3.zero,
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_North", localPosition = new Vector3(0f, 0f, 8f), localDirection = DoorDirection.North },
                    new SocketDefinition { socketName = "Socket_East", localPosition = new Vector3(8f, 0f, 0f), localDirection = DoorDirection.East },
                    new SocketDefinition { socketName = "Socket_West", localPosition = new Vector3(-8f, 0f, 0f), localDirection = DoorDirection.West }
                }
            };

            var cross = new RoomTemplateData
            {
                templateId = "Room_Cross",
                roomType = RoomType.Normal,
                selectionWeight = 1.2f,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(16f, 4f, 16f)),
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_North", localPosition = new Vector3(0f, 0f, 8f), localDirection = DoorDirection.North },
                    new SocketDefinition { socketName = "Socket_East", localPosition = new Vector3(8f, 0f, 0f), localDirection = DoorDirection.East },
                    new SocketDefinition { socketName = "Socket_South", localPosition = new Vector3(0f, 0f, -8f), localDirection = DoorDirection.South },
                    new SocketDefinition { socketName = "Socket_West", localPosition = new Vector3(-8f, 0f, 0f), localDirection = DoorDirection.West }
                },
                enemySpawnLocalOffsets = new List<Vector3> { new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, -3f) }
            };

            var corridor = new RoomTemplateData
            {
                templateId = "Room_Corridor",
                roomType = RoomType.Corridor,
                selectionWeight = 1.0f,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(8f, 4f, 16f)),
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_North", localPosition = new Vector3(0f, 0f, 8f), localDirection = DoorDirection.North },
                    new SocketDefinition { socketName = "Socket_South", localPosition = new Vector3(0f, 0f, -8f), localDirection = DoorDirection.South }
                }
            };

            var corner = new RoomTemplateData
            {
                templateId = "Room_Corner",
                roomType = RoomType.Normal,
                selectionWeight = 1.0f,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(16f, 4f, 16f)),
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_North", localPosition = new Vector3(0f, 0f, 8f), localDirection = DoorDirection.North },
                    new SocketDefinition { socketName = "Socket_East", localPosition = new Vector3(8f, 0f, 0f), localDirection = DoorDirection.East }
                }
            };

            var arena = new RoomTemplateData
            {
                templateId = "Room_Arena",
                roomType = RoomType.Combat,
                selectionWeight = 0.8f,
                maxCount = 2,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(24f, 4f, 24f)),
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_North", localPosition = new Vector3(0f, 0f, 12f), localDirection = DoorDirection.North },
                    new SocketDefinition { socketName = "Socket_East", localPosition = new Vector3(12f, 0f, 0f), localDirection = DoorDirection.East },
                    new SocketDefinition { socketName = "Socket_South", localPosition = new Vector3(0f, 0f, -12f), localDirection = DoorDirection.South },
                    new SocketDefinition { socketName = "Socket_West", localPosition = new Vector3(-12f, 0f, 0f), localDirection = DoorDirection.West }
                },
                enemySpawnLocalOffsets = new List<Vector3>
                {
                    new Vector3(-5f, 0f, 5f),
                    new Vector3(5f, 0f, 5f),
                    new Vector3(0f, 0f, -5f)
                }
            };

            var corridorCorner = new RoomTemplateData
            {
                templateId = "Room_Corridor_Corner_L",
                roomType = RoomType.Corridor,
                selectionWeight = 1.0f,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(10f, 4f, 10f)),
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_North", localPosition = new Vector3(0f, 0f, 5f), localDirection = DoorDirection.North },
                    new SocketDefinition { socketName = "Socket_East", localPosition = new Vector3(5f, 0f, 0f), localDirection = DoorDirection.East }
                }
            };

            var treasure = new RoomTemplateData
            {
                templateId = "Room_Treasure",
                roomType = RoomType.Treasure,
                selectionWeight = 1f,
                minCount = 1,
                maxCount = 1,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(12f, 4f, 12f)),
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_South", localPosition = new Vector3(0f, 0f, -6f), localDirection = DoorDirection.South }
                }
            };

            var exit = new RoomTemplateData
            {
                templateId = "Room_Exit",
                roomType = RoomType.Exit,
                selectionWeight = 1f,
                localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(16f, 4f, 16f)),
                hasExitPoint = true,
                exitLocalOffset = new Vector3(0f, 0f, 2f),
                sockets = new List<SocketDefinition>
                {
                    new SocketDefinition { socketName = "Socket_South", localPosition = new Vector3(0f, 0f, -8f), localDirection = DoorDirection.South }
                }
            };

            return new DungeonPlanRequest
            {
                targetRoomCount = targetRooms,
                minMainPathLength = minMainPath,
                branchProbability = 0.55f,
                loopConnectionChance = 0.25f,
                boundsOverlapMargin = 0.05f,
                maxGenerationAttempts = 25,
                maxBacktrackSteps = 450,
                startTemplates = new List<RoomTemplateData> { start },
                corridorTemplates = new List<RoomTemplateData> { corridor, corridorCorner },
                normalTemplates = new List<RoomTemplateData> { cross, corner, arena },
                specialBranchTemplates = new List<RoomTemplateData> { treasure },
                exitTemplates = new List<RoomTemplateData> { exit }
            };
        }

        [Test]
        public void TryPlan_GeneratesValidSolvableNonOverlappingLayout_AcrossFiftySeeds()
        {
            DungeonPlanRequest request = CreateSampleRequest(targetRooms: 10, minMainPath: 4);
            var templateMap = new Dictionary<string, RoomTemplateData>();
            foreach (RoomTemplateData t in request.startTemplates) templateMap[t.templateId] = t;
            foreach (RoomTemplateData t in request.corridorTemplates) templateMap[t.templateId] = t;
            foreach (RoomTemplateData t in request.normalTemplates) templateMap[t.templateId] = t;
            foreach (RoomTemplateData t in request.specialBranchTemplates) templateMap[t.templateId] = t;
            foreach (RoomTemplateData t in request.exitTemplates) templateMap[t.templateId] = t;
            foreach (RoomTemplateData t in request.normalTemplates) templateMap[t.templateId] = t;
            foreach (RoomTemplateData t in request.specialBranchTemplates) templateMap[t.templateId] = t;
            foreach (RoomTemplateData t in request.exitTemplates) templateMap[t.templateId] = t;

            for (int seed = 1000; seed < 1050; seed++)
            {
                bool ok = DungeonLayoutPlanner.TryPlan(request, seed, out DungeonLayoutData layout, out string err);
                Assert.IsTrue(ok, $"Seed {seed} failed: {err}");
                Assert.IsNotNull(layout);
                Assert.IsTrue(layout.isReachableFromStartToExit, $"Seed {seed} not reachable from Start to Exit.");
                Assert.IsFalse(layout.hasAnyOverlap, $"Seed {seed} reported room overlap.");
                Assert.GreaterOrEqual(layout.mainPathRoomIds.Count, request.minMainPathLength, $"Seed {seed} main path too short.");
                Assert.AreEqual(layout.startRoomId, layout.mainPathRoomIds[0]);
                Assert.AreEqual(layout.exitRoomId, layout.mainPathRoomIds[layout.mainPathRoomIds.Count - 1]);

                // Verify layout graph topological constraints via Validation Gate
                Assert.IsTrue(DungeonLayoutPlanner.IsGraphValid(layout, out string valErr), $"Seed {seed} failed Validation Gate: {valErr}");
                Assert.AreEqual(1, DungeonLayoutPlanner.GetRoomDegree(layout, layout.startRoomId), $"Seed {seed}: Start room must have degree == 1.");
                Assert.AreEqual(1, DungeonLayoutPlanner.GetRoomDegree(layout, layout.exitRoomId), $"Seed {seed}: Boss/Exit room must have degree == 1.");
                Assert.GreaterOrEqual(DungeonLayoutPlanner.CountMajorRoomsOnMainPath(layout), 4, $"Seed {seed}: Main path must have >= 4 major rooms (Start -> >=2 Combat -> Boss).");

                foreach (PlacedRoomData r in layout.rooms)
                {
                    int deg = DungeonLayoutPlanner.GetRoomDegree(layout, r.roomId);
                    if (r.roomType == RoomType.Corridor)
                    {
                        Assert.AreEqual(2, deg, $"Seed {seed}: Corridor {r.roomId} must have degree == 2 (no dead-end corridors).");
                    }
                    else if (r.roomId != layout.startRoomId && r.roomId != layout.exitRoomId)
                    {
                        if (deg == 1)
                        {
                            Assert.AreEqual(RoomType.Treasure, r.roomType, $"Seed {seed}: Leaf room {r.roomId} (deg=1) must be Treasure, found {r.roomType}.");
                        }
                        else
                        {
                            Assert.IsTrue(r.roomType == RoomType.Normal || r.roomType == RoomType.Combat, $"Seed {seed}: Internal room {r.roomId} (deg={deg}) must be Combat/Normal.");
                        }
                    }
                }

                // Verify every connection aligns exact socket positions and opposite outward directions
                foreach (RoomConnectionData conn in layout.connections)
                {
                    PlacedRoomData fromRoom = layout.GetRoomById(conn.fromRoomId);
                    PlacedRoomData toRoom = layout.GetRoomById(conn.toRoomId);
                    Assert.IsNotNull(fromRoom);
                    Assert.IsNotNull(toRoom);

                    SocketDefinition fromSock = FindSocket(templateMap[fromRoom.templateId], conn.fromSocketName);
                    SocketDefinition toSock = FindSocket(templateMap[toRoom.templateId], conn.toSocketName);

                    Vector3 fromWorldPos = fromRoom.worldPosition + Quaternion.Euler(fromRoom.worldRotationEuler) * fromSock.localPosition;
                    Vector3 toWorldPos = toRoom.worldPosition + Quaternion.Euler(toRoom.worldRotationEuler) * toSock.localPosition;

                    Assert.LessOrEqual(Vector3.Distance(fromWorldPos, toWorldPos), 0.005f,
                        $"Seed {seed}: socket gap between room {fromRoom.roomId} ({conn.fromSocketName}) and room {toRoom.roomId} ({conn.toSocketName}).");

                    DoorDirection fromWorldDir = fromSock.localDirection.RotateClockwiseSteps(fromRoom.yawSteps);
                    DoorDirection toWorldDir = toSock.localDirection.RotateClockwiseSteps(toRoom.yawSteps);
                    Assert.AreEqual(fromWorldDir.Opposite(), toWorldDir,
                        $"Seed {seed}: sockets not facing each other.");

                    // User requirement verification: No two non-corridor rooms directly connected
                    Assert.IsTrue(
                        fromRoom.roomType == RoomType.Corridor || toRoom.roomType == RoomType.Corridor,
                        $"Seed {seed}: direct connection between {fromRoom.roomType} and {toRoom.roomType} without corridor!");
                }

                // User requirement verification: Treasure room must not connect directly to Start
                foreach (PlacedRoomData room in layout.rooms)
                {
                    if (room.roomType == RoomType.Treasure)
                    {
                        Assert.GreaterOrEqual(room.graphDepth, 2, $"Seed {seed}: Treasure room {room.roomId} is too close to Start!");
                        foreach (RoomConnectionData conn in layout.connections)
                        {
                            bool touchesStart = (conn.fromRoomId == room.roomId && conn.toRoomId == layout.startRoomId) ||
                                                (conn.toRoomId == room.roomId && conn.fromRoomId == layout.startRoomId);
                            Assert.IsFalse(touchesStart, $"Seed {seed}: Treasure room {room.roomId} connects directly to Start!");
                        }
                    }
                }
            }
        }

        [Test]
        public void TryPlan_SameSeed_ProducesIdenticalDeterministicLayout()
        {
            DungeonPlanRequest request = CreateSampleRequest(targetRooms: 9, minMainPath: 4);

            Assert.IsTrue(DungeonLayoutPlanner.TryPlan(request, 77777, out DungeonLayoutData layoutA, out _));
            Assert.IsTrue(DungeonLayoutPlanner.TryPlan(request, 77777, out DungeonLayoutData layoutB, out _));

            Assert.AreEqual(layoutA.ToJson(false), layoutB.ToJson(false));
        }

        [Test]
        public void TryPlan_FailsCleanly_WhenRequiredTemplatesMissing()
        {
            var invalidRequest = new DungeonPlanRequest
            {
                targetRoomCount = 5,
                minMainPathLength = 3
            };

            bool ok = DungeonLayoutPlanner.TryPlan(invalidRequest, 1, out DungeonLayoutData layout, out string err);
            Assert.IsFalse(ok);
            Assert.IsNull(layout);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void CalculateWorldBounds_HandlesNonSquareRotationAccurately()
        {
            var local = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 16f));

            Bounds rot0 = RoomModule.CalculateWorldBounds(local, Vector3.zero, 0);
            Assert.AreEqual(8f, rot0.size.x, 0.001f);
            Assert.AreEqual(16f, rot0.size.z, 0.001f);

            Bounds rot90 = RoomModule.CalculateWorldBounds(local, new Vector3(10f, 0f, -5f), 1);
            Assert.AreEqual(16f, rot90.size.x, 0.001f);
            Assert.AreEqual(8f, rot90.size.z, 0.001f);
            Assert.AreEqual(10f, rot90.center.x, 0.001f);
            Assert.AreEqual(-5f, rot90.center.z, 0.001f);
        }

        private static SocketDefinition FindSocket(RoomTemplateData template, string socketName)
        {
            foreach (SocketDefinition s in template.sockets)
            {
                if (s.socketName == socketName)
                {
                    return s;
                }
            }

            Assert.Fail($"Socket '{socketName}' not found on template '{template.templateId}'.");
            return default;
        }
    }
}
