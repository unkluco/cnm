using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Marks a doorway attachment point on a room prefab.
    /// Controls open archway vs. sealed wall visuals, combat lock barriers,
    /// and 3D wayfinding color landmarks depending on runtime state.
    /// </summary>
    public class DoorSocket : MonoBehaviour
    {
        [Header("Socket Identity")]
        [SerializeField] private string socketId;
        [SerializeField] private DoorDirection localDirection = DoorDirection.North;
        [SerializeField] private Vector2 openingSize = new Vector2(4f, 3f);

        [Header("Visuals")]
        [Tooltip("Activated when this socket successfully connects to an adjacent room.")]
        [SerializeField] private GameObject openVisual;
        [Tooltip("Activated when this socket remains unconnected so no holes open into the void.")]
        [SerializeField] private GameObject closedWallVisual;
        [Tooltip("Activated during RoomLifecycleState.Combat to physically lock the doorway until waves are cleared.")]
        [SerializeField] private GameObject lockedGateVisual;

        public string SocketId => string.IsNullOrWhiteSpace(socketId) ? gameObject.name : socketId;
        public DoorDirection LocalDirection => localDirection;
        public Vector2 OpeningSize => openingSize;
        public bool IsConnected { get; private set; }
        public bool IsLocked { get; private set; }
        public DoorSocket ConnectedSocket { get; private set; }
        public int ConnectedRoomId { get; private set; } = -1;

        private void Reset()
        {
            socketId = gameObject.name;
            localDirection = DoorDirectionUtility.FromHorizontalVector(transform.localRotation * Vector3.forward);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(socketId))
            {
                socketId = gameObject.name;
            }
        }

        /// <summary>
        /// Updates connection state and switches doorway vs. sealed wall geometry.
        /// </summary>
        public void SetConnectionState(bool connected, DoorSocket otherSocket = null, int otherRoomId = -1)
        {
            IsConnected = connected;
            ConnectedSocket = connected ? otherSocket : null;
            ConnectedRoomId = connected ? otherRoomId : -1;

            if (!connected)
            {
                IsLocked = false;
            }

            if (openVisual != null)
            {
                openVisual.SetActive(connected);
            }

            if (closedWallVisual != null)
            {
                closedWallVisual.SetActive(!connected);
            }

            if (lockedGateVisual != null)
            {
                lockedGateVisual.SetActive(connected && IsLocked);
            }
        }

        /// <summary>
        /// Locks or unlocks a connected doorway (used by RoomLifecycleController during Combat vs Cleared states).
        /// </summary>
        public void SetLocked(bool locked)
        {
            IsLocked = IsConnected && locked;
            if (lockedGateVisual != null)
            {
                lockedGateVisual.SetActive(IsLocked);
            }
        }

        /// <summary>
        /// Updates the doorway frame color for 3D Wayfinding Landmarks (Boss=Purple/Red, Treasure=Gold, Cleared=Green).
        /// Uses MaterialPropertyBlock to avoid leaking material instances.
        /// </summary>
        public void SetDoorFrameColor(Color landmarkColor)
        {
            if (openVisual == null)
            {
                return;
            }

            Renderer[] renderers = openVisual.GetComponentsInChildren<Renderer>(true);
            var propBlock = new MaterialPropertyBlock();
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", landmarkColor);
                propBlock.SetColor("_Color", landmarkColor);
                r.SetPropertyBlock(propBlock);
            }
        }

        /// <summary>
        /// Assigns visual references programmatically (used by editor builders/tests).
        /// </summary>
        public void Configure(
            string id,
            DoorDirection direction,
            Vector2 size,
            GameObject openObj,
            GameObject closedObj,
            GameObject lockedGateObj = null)
        {
            socketId = id;
            localDirection = direction;
            openingSize = size;
            openVisual = openObj;
            closedWallVisual = closedObj;
            lockedGateVisual = lockedGateObj;
        }

        /// <summary>
        /// Extracts relative position and direction with respect to the owning room root transform.
        /// </summary>
        public SocketDefinition ToSocketDefinition(Transform roomRoot)
        {
            Vector3 relPos = roomRoot != null
                ? roomRoot.InverseTransformPoint(transform.position)
                : transform.localPosition;

            return new SocketDefinition
            {
                socketName = SocketId,
                localPosition = relPos,
                localDirection = localDirection
            };
        }

        public DoorDirection GetWorldDirection(int roomYawSteps)
        {
            return localDirection.RotateClockwiseSteps(roomYawSteps);
        }

        private void OnDrawGizmos()
        {
            Vector3 worldPos = transform.position;
            Vector3 outward = transform.TransformDirection(localDirection.ToVector3()).normalized;

            Gizmos.color = IsConnected
                ? (IsLocked ? new Color(1f, 0.2f, 0.2f, 0.95f) : new Color(0.15f, 0.9f, 0.35f, 0.9f))
                : (Application.isPlaying
                    ? new Color(0.95f, 0.25f, 0.25f, 0.85f)
                    : new Color(1f, 0.85f, 0.15f, 0.9f));

            Gizmos.DrawSphere(worldPos + Vector3.up * 0.25f, 0.22f);
            Gizmos.DrawRay(worldPos + Vector3.up * 0.5f, outward * 1.4f);

            // Draw doorway opening outline perpendicular to outward direction
            Vector3 right = Vector3.Cross(Vector3.up, outward).normalized;
            float halfW = openingSize.x * 0.5f;
            float h = openingSize.y;

            Vector3 bl = worldPos - right * halfW;
            Vector3 br = worldPos + right * halfW;
            Vector3 tl = bl + Vector3.up * h;
            Vector3 tr = br + Vector3.up * h;

            Gizmos.DrawLine(bl, tl);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
        }
    }
}
