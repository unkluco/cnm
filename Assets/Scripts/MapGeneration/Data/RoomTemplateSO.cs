using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Serializable description of a room doorway socket in local room space.
    /// </summary>
    [Serializable]
    public struct SocketDefinition
    {
        public string socketName;
        public Vector3 localPosition;
        public DoorDirection localDirection;
    }

    /// <summary>
    /// Pure C# room metadata consumed by DungeonLayoutPlanner during planning
    /// so layout calculation and tests do not require scene GameObject instantiation.
    /// </summary>
    [Serializable]
    public class RoomTemplateData
    {
        public string templateId;
        public RoomType roomType = RoomType.Normal;
        public float selectionWeight = 1f;
        public int minCount = 0;
        public int maxCount = 0; // 0 = unlimited
        public bool allowRotation = true;
        public Bounds localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(16f, 4f, 16f));
        public List<SocketDefinition> sockets = new List<SocketDefinition>();

        public bool hasPlayerSpawn;
        public Vector3 playerSpawnLocalOffset;
        public bool hasExitPoint;
        public Vector3 exitLocalOffset;
        public List<Vector3> enemySpawnLocalOffsets = new List<Vector3>();
    }

    /// <summary>
    /// ScriptableObject template defining a room prefab and its procedural placement rules.
    /// Automatically extracts bounds, doorway sockets, and spawn anchors from the assigned RoomModule prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomTemplate", menuName = "LightSouls/Map Generation/Room Template")]
    public class RoomTemplateSO : ScriptableObject
    {
        [Header("Template Identity")]
        [SerializeField] private string templateId;
        [SerializeField] private RoomType roomType = RoomType.Normal;
        [SerializeField] private RoomModule prefab;

        [Header("Generation Constraints")]
        [Min(0.01f)]
        [SerializeField] private float selectionWeight = 1f;
        [Min(0)]
        [SerializeField] private int minCount = 0;
        [Min(0)]
        [Tooltip("0 means unlimited instances of this room template.")]
        [SerializeField] private int maxCount = 0;
        [SerializeField] private bool allowRotation = true;

        [Header("Cached Layout Metadata (Auto-Synced From Prefab)")]
        [SerializeField] private Bounds localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(16f, 4f, 16f));
        [SerializeField] private List<SocketDefinition> sockets = new List<SocketDefinition>();
        [SerializeField] private bool hasPlayerSpawn;
        [SerializeField] private Vector3 playerSpawnLocalOffset;
        [SerializeField] private bool hasExitPoint;
        [SerializeField] private Vector3 exitLocalOffset;
        [SerializeField] private List<Vector3> enemySpawnLocalOffsets = new List<Vector3>();

        public string TemplateId => string.IsNullOrWhiteSpace(templateId) ? name : templateId;
        public RoomType RoomType => roomType;
        public RoomModule Prefab => prefab;
        public float SelectionWeight => selectionWeight;
        public int MinCount => minCount;
        public int MaxCount => maxCount;
        public bool AllowRotation => allowRotation;
        public Bounds LocalBounds => localBounds;
        public IReadOnlyList<SocketDefinition> Sockets => sockets;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                templateId = name;
            }

            SyncFromPrefab();
        }

        [ContextMenu("Sync Metadata From Prefab")]
        public void SyncFromPrefab()
        {
            if (prefab == null)
            {
                return;
            }

            prefab.RefreshSocketsFromChildren();
            roomType = prefab.RoomType;
            localBounds = prefab.LocalBounds;

            sockets = new List<SocketDefinition>();
            if (prefab.DoorSockets != null)
            {
                foreach (DoorSocket socket in prefab.DoorSockets)
                {
                    if (socket != null)
                    {
                        sockets.Add(socket.ToSocketDefinition(prefab.transform));
                    }
                }
            }

            hasPlayerSpawn = prefab.PlayerSpawnPoint != null;
            playerSpawnLocalOffset = hasPlayerSpawn
                ? prefab.transform.InverseTransformPoint(prefab.PlayerSpawnPoint.position)
                : Vector3.zero;

            hasExitPoint = prefab.ExitPoint != null;
            exitLocalOffset = hasExitPoint
                ? prefab.transform.InverseTransformPoint(prefab.ExitPoint.position)
                : Vector3.zero;

            enemySpawnLocalOffsets = new List<Vector3>();
            if (prefab.EnemySpawnPoints != null)
            {
                foreach (Transform spawn in prefab.EnemySpawnPoints)
                {
                    if (spawn != null)
                    {
                        enemySpawnLocalOffsets.Add(prefab.transform.InverseTransformPoint(spawn.position));
                    }
                }
            }
        }

        /// <summary>
        /// Assigns prefab and configuration programmatically (used by editor builders).
        /// </summary>
        public void Configure(
            string id,
            RoomModule roomPrefab,
            float weight = 1f,
            int min = 0,
            int max = 0,
            bool rotate = true)
        {
            templateId = id;
            prefab = roomPrefab;
            selectionWeight = Mathf.Max(0.01f, weight);
            minCount = Mathf.Max(0, min);
            maxCount = Mathf.Max(0, max);
            allowRotation = rotate;
            SyncFromPrefab();
        }

        public RoomTemplateData ToRuntimeData()
        {
            return new RoomTemplateData
            {
                templateId = TemplateId,
                roomType = roomType,
                selectionWeight = selectionWeight,
                minCount = minCount,
                maxCount = maxCount,
                allowRotation = allowRotation,
                localBounds = localBounds,
                sockets = new List<SocketDefinition>(sockets),
                hasPlayerSpawn = hasPlayerSpawn,
                playerSpawnLocalOffset = playerSpawnLocalOffset,
                hasExitPoint = hasExitPoint,
                exitLocalOffset = exitLocalOffset,
                enemySpawnLocalOffsets = new List<Vector3>(enemySpawnLocalOffsets)
            };
        }
    }
}
