using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Minimal serializable description of a single placed room in a dungeon layout.
    /// </summary>
    [Serializable]
    public class PlacedRoomData
    {
        public int roomId;
        public string templateId;
        public RoomType roomType;
        public Vector3 worldPosition;
        public int yawSteps;
        public Vector3 worldRotationEuler;
        public Bounds worldBounds;
        public int graphDepth;
        public bool isOnMainPath;
        public List<Vector3> enemySpawnPositions = new List<Vector3>();
    }

    /// <summary>
    /// Minimal serializable description of a doorway link connecting two rooms.
    /// </summary>
    [Serializable]
    public class RoomConnectionData
    {
        public int fromRoomId;
        public string fromSocketName;
        public int toRoomId;
        public string toSocketName;
        public Vector3 doorWorldPosition;
    }

    /// <summary>
    /// Complete serializable level layout produced by DungeonLayoutPlanner.
    /// Serves as the shared data contract between Map Generation and other gameplay systems.
    /// </summary>
    [Serializable]
    public class DungeonLayoutData
    {
        public int seed;
        public List<PlacedRoomData> rooms = new List<PlacedRoomData>();
        public List<RoomConnectionData> connections = new List<RoomConnectionData>();
        public int startRoomId = -1;
        public int exitRoomId = -1;
        public List<int> mainPathRoomIds = new List<int>();
        public Vector3 playerSpawnPosition;
        public Quaternion playerSpawnRotation = Quaternion.identity;
        public Vector3 exitPosition;
        public List<Vector3> enemySpawnPositions = new List<Vector3>();
        public bool isReachableFromStartToExit;
        public bool hasAnyOverlap;

        public PlacedRoomData GetRoomById(int roomId)
        {
            foreach (PlacedRoomData room in rooms)
            {
                if (room.roomId == roomId)
                {
                    return room;
                }
            }

            return null;
        }

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }
    }
}
