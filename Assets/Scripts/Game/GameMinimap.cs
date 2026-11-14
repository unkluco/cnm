using System.Collections.Generic;
using LightSouls.MapGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Game
{
    /// <summary>
    /// Bản đồ nhỏ trên HUD của Scene Game.
    /// Vẽ mỗi phòng bằng một hình chữ nhật theo đúng kích thước thật, thu nhỏ cho vừa khung.
    /// Chỉ hiện phòng người chơi đã vào và các phòng nối liền với chúng. Phòng đích cũng vậy: chỉ hiện khi người chơi đã tới gần.
    /// Màu phòng đổi theo trạng thái (đang đánh, đã đi qua). Mũi tên ở giữa chấm người chơi chỉ hướng đang quay mặt.
    ///
    /// GameBootstrap gọi Show() sau khi bản đồ sinh xong và người chơi đã được đặt vào màn.
    /// Khác với DungeonMinimapUI (công cụ thử của sinh màn): bản này không lộ đường chính và không hiện cả bản đồ ngay từ đầu.
    /// </summary>
    public class GameMinimap : MonoBehaviour
    {
        [Tooltip("Vùng vẽ bản đồ. Gốc tọa độ là tâm vùng này.")]
        [SerializeField] private RectTransform mapArea;

        [Tooltip("Chấm người chơi. Hướng lên của nó là hướng người chơi đang quay mặt.")]
        [SerializeField] private RectTransform playerMarker;

        [Tooltip("Tỉ lệ tối đa, pixel trên mét. Bản đồ nhỏ thì không phóng to quá mức này.")]
        [SerializeField, Min(0.5f)] private float maxPixelsPerMeter = 4f;

        [Tooltip("Khoảng trống giữa mép vùng vẽ và bản đồ, pixel.")]
        [SerializeField, Min(0f)] private float padding = 12f;

        [Tooltip("Thu nhỏ mỗi phòng một chút để thấy ranh giới giữa các phòng, pixel.")]
        [SerializeField, Min(0f)] private float roomGap = 1.5f;

        [Header("Màu")]
        [SerializeField] private Color seenColor = new Color(0.30f, 0.35f, 0.44f, 0.75f);
        [SerializeField] private Color visitedColor = new Color(0.66f, 0.72f, 0.82f, 0.95f);
        [SerializeField] private Color combatColor = new Color(0.92f, 0.24f, 0.18f, 0.95f);
        [SerializeField] private Color treasureColor = new Color(0.95f, 0.75f, 0.15f, 0.95f);
        [SerializeField] private Color exitColor = new Color(0.90f, 0.22f, 0.62f, 0.95f);

        private sealed class RoomEntry
        {
            public RoomModule Room;
            public Bounds WorldBounds;
            public Image Image;
            public bool Visited;
            public readonly List<RoomEntry> Neighbors = new List<RoomEntry>();
        }

        private readonly List<RoomEntry> entries = new List<RoomEntry>();
        private Transform player;
        private Vector2 worldCenter;
        private float pixelsPerMeter = 1f;

        /// <summary>Dựng bản đồ cho màn vừa sinh và bắt đầu theo dõi người chơi.</summary>
        public void Show(DungeonRuntimeContext context, Transform playerTransform)
        {
            Clear();
            player = playerTransform;

            if (context == null || context.SpawnedRooms == null || mapArea == null)
            {
                return;
            }

            gameObject.SetActive(true);
            BuildEntries(context);
            ComputeScale();
            CreateRoomImages();

            if (playerMarker != null)
            {
                playerMarker.SetAsLastSibling();
            }

            LateUpdate();
        }

        /// <summary>Ẩn bản đồ, ví dụ khi hết game.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (player == null || entries.Count == 0)
            {
                return;
            }

            var playerPosition = player.position;
            foreach (var entry in entries)
            {
                if (!entry.Visited && ContainsXZ(entry.WorldBounds, playerPosition))
                {
                    entry.Visited = true;
                }
            }

            foreach (var entry in entries)
            {
                UpdateRoomImage(entry);
            }

            if (playerMarker != null)
            {
                playerMarker.anchoredPosition = WorldToMap(playerPosition);
                playerMarker.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
            }
        }

        private void BuildEntries(DungeonRuntimeContext context)
        {
            var byId = new Dictionary<int, RoomEntry>();
            foreach (var room in context.SpawnedRooms)
            {
                if (room == null)
                {
                    continue;
                }

                var entry = new RoomEntry { Room = room, WorldBounds = room.GetCurrentWorldBounds() };
                entries.Add(entry);
                byId[room.RoomId] = entry;
            }

            var connections = context.Layout != null ? context.Layout.connections : null;
            if (connections == null)
            {
                return;
            }

            foreach (var connection in connections)
            {
                if (byId.TryGetValue(connection.fromRoomId, out var a) && byId.TryGetValue(connection.toRoomId, out var b))
                {
                    a.Neighbors.Add(b);
                    b.Neighbors.Add(a);
                }
            }
        }

        private void ComputeScale()
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var entry in entries)
            {
                var b = entry.WorldBounds;
                min = Vector2.Min(min, new Vector2(b.min.x, b.min.z));
                max = Vector2.Max(max, new Vector2(b.max.x, b.max.z));
            }

            worldCenter = (min + max) * 0.5f;
            var span = Vector2.Max(max - min, Vector2.one);
            var available = mapArea.rect.size - Vector2.one * (padding * 2f);
            pixelsPerMeter = Mathf.Min(maxPixelsPerMeter, available.x / span.x, available.y / span.y);
        }

        private void CreateRoomImages()
        {
            // Hành lang vẽ trước để các phòng lớn nằm đè lên chỗ nối.
            entries.Sort((a, b) => IsCorridor(b).CompareTo(IsCorridor(a)));

            foreach (var entry in entries)
            {
                var go = new GameObject(entry.Room.name, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(mapArea, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

                var b = entry.WorldBounds;
                rect.anchoredPosition = WorldToMap(b.center);
                var size = new Vector2(b.size.x, b.size.z) * pixelsPerMeter - Vector2.one * roomGap;
                rect.sizeDelta = Vector2.Max(size, Vector2.one * 2f);

                entry.Image = go.GetComponent<Image>();
                entry.Image.raycastTarget = false;
                entry.Image.enabled = false;
            }
        }

        private void UpdateRoomImage(RoomEntry entry)
        {
            bool isExit = entry.Room.RoomType == RoomType.Exit;
            // Phòng đích (phòng boss) cũng chịu sương mù: người chơi phải tự tìm ra nó.
            bool seen = entry.Visited || HasVisitedNeighbor(entry);

            if (entry.Image.enabled != seen)
            {
                entry.Image.enabled = seen;
            }

            if (!seen)
            {
                return;
            }

            var color = GetRoomColor(entry, isExit);
            if (entry.Image.color != color)
            {
                entry.Image.color = color;
            }
        }

        private Color GetRoomColor(RoomEntry entry, bool isExit)
        {
            var lifecycle = entry.Room.LifecycleController;
            if (lifecycle != null && lifecycle.State == RoomLifecycleState.Combat)
            {
                return combatColor;
            }

            if (isExit)
            {
                return exitColor;
            }

            if (entry.Room.RoomType == RoomType.Treasure)
            {
                return treasureColor;
            }

            return entry.Visited ? visitedColor : seenColor;
        }

        private static bool HasVisitedNeighbor(RoomEntry entry)
        {
            foreach (var neighbor in entry.Neighbors)
            {
                if (neighbor.Visited)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsCorridor(RoomEntry entry) => entry.Room.RoomType == RoomType.Corridor;

        private static bool ContainsXZ(Bounds bounds, Vector3 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x
                && point.z >= bounds.min.z && point.z <= bounds.max.z;
        }

        private Vector2 WorldToMap(Vector3 world)
        {
            return (new Vector2(world.x, world.z) - worldCenter) * pixelsPerMeter;
        }

        private void Clear()
        {
            foreach (var entry in entries)
            {
                if (entry.Image != null)
                {
                    Destroy(entry.Image.gameObject);
                }
            }
            entries.Clear();
            player = null;
        }
    }
}
