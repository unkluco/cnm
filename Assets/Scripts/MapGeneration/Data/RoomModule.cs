using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Root component placed on every room prefab.
    /// Exposes doorway sockets, local collision footprint, and gameplay integration anchors.
    /// </summary>
    public class RoomModule : MonoBehaviour
    {
        [Header("Room Identity & Footprint")]
        [SerializeField] private RoomType roomType = RoomType.Normal;
        [SerializeField] private Bounds localBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(16f, 4f, 16f));

        [Header("Integration Anchors")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private List<Transform> enemySpawnPoints = new List<Transform>();
        [SerializeField] private List<Transform> lootSpawnPoints = new List<Transform>();
        [SerializeField] private WeaponVendingMachine vendingMachine;
        [SerializeField] private BossRoomRewardController bossRewardController;
        [SerializeField] private RoomLifecycleController lifecycleController;

        [Header("Door Sockets")]
        [SerializeField] private List<DoorSocket> doorSockets = new List<DoorSocket>();

        public RoomType RoomType => roomType;
        public Bounds LocalBounds => localBounds;
        public Transform PlayerSpawnPoint => playerSpawnPoint;
        public Transform ExitPoint => exitPoint;
        public IReadOnlyList<Transform> EnemySpawnPoints => enemySpawnPoints;
        public IReadOnlyList<Transform> LootSpawnPoints => lootSpawnPoints;
        public IReadOnlyList<DoorSocket> DoorSockets => doorSockets;
        public WeaponVendingMachine VendingMachine => vendingMachine;
        public BossRoomRewardController BossRewardController => bossRewardController;
        public RoomLifecycleController LifecycleController =>
            lifecycleController != null ? lifecycleController : (lifecycleController = GetComponent<RoomLifecycleController>());

        public int RoomId { get; internal set; } = -1;
        public int GraphDepth { get; internal set; }
        public bool IsOnMainPath { get; internal set; }
        public string TemplateId { get; internal set; }

        private void Reset()
        {
            RefreshSocketsFromChildren();
        }

        private void OnValidate()
        {
            if (doorSockets == null || doorSockets.Count == 0)
            {
                RefreshSocketsFromChildren();
            }
        }

        [ContextMenu("Refresh Sockets From Children")]
        public void RefreshSocketsFromChildren()
        {
            doorSockets = new List<DoorSocket>(GetComponentsInChildren<DoorSocket>(true));
        }

        /// <summary>
        /// Configures room metadata and anchors programmatically (used by prefab authoring tools).
        /// </summary>
        public void Configure(
            RoomType type,
            Bounds bounds,
            Transform playerSpawn,
            Transform exit,
            List<Transform> enemySpawns,
            List<Transform> lootSpawns,
            WeaponVendingMachine kiosk = null,
            BossRoomRewardController bossRewards = null,
            RoomLifecycleController lifecycle = null)
        {
            roomType = type;
            localBounds = bounds;
            playerSpawnPoint = playerSpawn;
            exitPoint = exit;
            enemySpawnPoints = enemySpawns ?? new List<Transform>();
            lootSpawnPoints = lootSpawns ?? new List<Transform>();
            vendingMachine = kiosk != null ? kiosk : GetComponentInChildren<WeaponVendingMachine>(true);
            bossRewardController = bossRewards != null ? bossRewards : GetComponentInChildren<BossRoomRewardController>(true);
            lifecycleController = lifecycle != null ? lifecycle : GetComponent<RoomLifecycleController>();
            RefreshSocketsFromChildren();
        }

        /// <summary>
        /// Sets every doorway on this room to its sealed-wall state before connection wiring runs.
        /// </summary>
        public void ResetAllSocketsToSealed()
        {
            if (doorSockets == null)
            {
                return;
            }

            foreach (DoorSocket socket in doorSockets)
            {
                if (socket != null)
                {
                    socket.SetConnectionState(false);
                }
            }
        }

        public bool TryGetSocket(string socketId, out DoorSocket socket)
        {
            if (doorSockets != null)
            {
                foreach (DoorSocket candidate in doorSockets)
                {
                    if (candidate != null && candidate.SocketId == socketId)
                    {
                        socket = candidate;
                        return true;
                    }
                }
            }

            socket = null;
            return false;
        }

        /// <summary>
        /// Computes exact axis-aligned world bounds for a given world position and 90-degree Y-step rotation.
        /// </summary>
        public static Bounds CalculateWorldBounds(Bounds local, Vector3 worldPosition, int yawSteps)
        {
            Quaternion rot = Quaternion.Euler(0f, DoorDirectionUtility.StepsToYawDegrees(yawSteps), 0f);
            Vector3 ext = local.extents;
            Vector3 center = local.center;
            Vector3 worldCenter = worldPosition + rot * center;

            // Exact 8-corner encapsulation guarantees numerical precision across all 90-degree steps
            Bounds b = new Bounds(worldCenter, Vector3.zero);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 localCorner = center + new Vector3(ext.x * sx, ext.y * sy, ext.z * sz);
                        b.Encapsulate(worldPosition + rot * localCorner);
                    }
                }
            }

            return b;
        }

        public Bounds GetCurrentWorldBounds()
        {
            int yawSteps = Mathf.RoundToInt(transform.eulerAngles.y / 90f) & 3;
            return CalculateWorldBounds(localBounds, transform.position, yawSteps);
        }

        private void OnDrawGizmos()
        {
            Color roomColor = roomType switch
            {
                RoomType.Start => new Color(0.2f, 0.9f, 0.35f, 0.7f),
                RoomType.Exit => new Color(0.95f, 0.2f, 0.65f, 0.8f),
                RoomType.Treasure => new Color(1f, 0.85f, 0.15f, 0.8f),
                RoomType.Corridor => new Color(0.55f, 0.75f, 1f, 0.6f),
                RoomType.Combat => new Color(1f, 0.5f, 0.15f, 0.75f),
                _ => new Color(0.35f, 0.8f, 0.95f, 0.6f)
            };

            Gizmos.color = roomColor;
            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
            Gizmos.matrix = prevMatrix;

            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(playerSpawnPoint.position + Vector3.up * 0.5f, 0.6f);
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(exitPoint.position + Vector3.up * 0.75f, new Vector3(1.2f, 1.5f, 1.2f));
            }

            if (enemySpawnPoints != null)
            {
                Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
                foreach (Transform enemyPoint in enemySpawnPoints)
                {
                    if (enemyPoint != null)
                    {
                        Gizmos.DrawSphere(enemyPoint.position + Vector3.up * 0.35f, 0.35f);
                    }
                }
            }
        }
    }
}
