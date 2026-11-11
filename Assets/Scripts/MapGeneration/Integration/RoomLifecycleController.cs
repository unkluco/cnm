using System;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    public enum RoomLifecycleState
    {
        Unvisited = 0,
        Combat = 1,
        Cleared = 2
    }

    /// <summary>
    /// Room Lifecycle State Machine (Unvisited -> Combat -> Cleared).
    /// Handles recessed 3D doorway entry triggers (inset 2.2m from walls so the player never clips into closing doors),
    /// physical doorway locking during enemy waves, and permanent unlocking + reward spawning upon clear.
    /// </summary>
    public class RoomLifecycleController : MonoBehaviour
    {
        public static readonly Color LandmarkColorNormal = new Color(0.85f, 0.60f, 0.22f);
        public static readonly Color LandmarkColorBoss = new Color(0.95f, 0.18f, 0.58f);
        public static readonly Color LandmarkColorTreasure = new Color(1.00f, 0.85f, 0.12f);
        public static readonly Color LandmarkColorCombatLocked = new Color(1.00f, 0.20f, 0.15f);
        public static readonly Color LandmarkColorCleared = new Color(0.22f, 0.92f, 0.48f);

        [SerializeField] private RoomModule roomModule;
        [SerializeField] private BoxCollider interiorCombatTrigger;

        public RoomLifecycleState State { get; private set; } = RoomLifecycleState.Unvisited;
        public RoomModule Room => roomModule;
        public BoxCollider InteriorCombatTrigger => interiorCombatTrigger;

        public event Action<RoomModule> OnRoomEnteredCombat;
        public event Action<RoomModule> OnRoomCleared;

        public bool RequiresCombatLock =>
            roomModule != null &&
            (roomModule.RoomType == RoomType.Normal ||
             roomModule.RoomType == RoomType.Combat ||
             roomModule.RoomType == RoomType.Exit);

        public void Configure(RoomModule module, BoxCollider triggerCollider)
        {
            roomModule = module;
            interiorCombatTrigger = triggerCollider;
        }

        private void Awake()
        {
            if (roomModule == null)
            {
                roomModule = GetComponent<RoomModule>();
            }
        }

        /// <summary>
        /// Resets the room to its initial state after dungeon generation.
        /// Safe rooms (Start, Treasure, Corridor) begin Cleared; combat/boss rooms begin Unvisited.
        /// </summary>
        public void InitializeLifecycle()
        {
            if (roomModule == null)
            {
                roomModule = GetComponent<RoomModule>();
            }

            if (!RequiresCombatLock)
            {
                State = RoomLifecycleState.Cleared;
                SetConnectedDoorsLocked(false);
            }
            else
            {
                State = RoomLifecycleState.Unvisited;
                SetConnectedDoorsLocked(false);
            }
        }

        /// <summary>
        /// Transitions an Unvisited combat/boss room into Combat state:
        /// slams all connected doors shut (enabling physical BoxColliders) so the player cannot flee or cheese from corridors.
        /// </summary>
        public bool EnterCombat()
        {
            if (!RequiresCombatLock || State != RoomLifecycleState.Unvisited)
            {
                return false;
            }

            State = RoomLifecycleState.Combat;
            SetConnectedDoorsLocked(true);
            SetConnectedDoorFramesColor(LandmarkColorCombatLocked);
            OnRoomEnteredCombat?.Invoke(roomModule);
            return true;
        }

        /// <summary>
        /// Transitions a room into Cleared state:
        /// permanently opens all connected doors, updates 3D wayfinding frames to Cleared Green,
        /// and spawns Boss bonus chests if this is the Boss room.
        /// </summary>
        public bool ClearRoom()
        {
            if (State == RoomLifecycleState.Cleared)
            {
                return false;
            }

            State = RoomLifecycleState.Cleared;
            SetConnectedDoorsLocked(false);
            SetConnectedDoorFramesColor(LandmarkColorCleared);

            if (roomModule != null && roomModule.BossRewardController != null)
            {
                roomModule.BossRewardController.TriggerBossRewards();
            }

            OnRoomCleared?.Invoke(roomModule);
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (State != RoomLifecycleState.Unvisited || !RequiresCombatLock || other == null)
            {
                return;
            }

            if (other.CompareTag("Player"))
            {
                // Push player slightly inward toward room center to guarantee zero door-collider clipping
                Vector3 toCenter = transform.position - other.transform.position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.01f)
                {
                    other.transform.position += toCenter.normalized * 0.6f;
                }

                EnterCombat();
            }
        }

        private void SetConnectedDoorsLocked(bool locked)
        {
            if (roomModule == null || roomModule.DoorSockets == null)
            {
                return;
            }

            foreach (DoorSocket socket in roomModule.DoorSockets)
            {
                if (socket == null || !socket.IsConnected)
                {
                    continue;
                }

                socket.SetLocked(locked);
                if (socket.ConnectedSocket != null)
                {
                    socket.ConnectedSocket.SetLocked(locked);
                }
            }
        }

        private void SetConnectedDoorFramesColor(Color color)
        {
            if (roomModule == null || roomModule.DoorSockets == null)
            {
                return;
            }

            foreach (DoorSocket socket in roomModule.DoorSockets)
            {
                if (socket == null || !socket.IsConnected)
                {
                    continue;
                }

                socket.SetDoorFrameColor(color);
                if (socket.ConnectedSocket != null)
                {
                    socket.ConnectedSocket.SetDoorFrameColor(color);
                }
            }
        }
    }
}
