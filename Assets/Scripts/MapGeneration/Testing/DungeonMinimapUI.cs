using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Procedural 2D Grid Minimap UI rendered in the top-right corner.
    /// Reads the 2D topology graph and live RoomLifecycleController states (Unvisited, Combat Locked, Cleared).
    /// </summary>
    public class DungeonMinimapUI : MonoBehaviour
    {
        [SerializeField] private RectTransform mapViewport;
        [SerializeField] private Font labelFont;

        private readonly List<GameObject> spawnedElements = new List<GameObject>();

        public void Configure(RectTransform viewport, Font font)
        {
            mapViewport = viewport;
            labelFont = font;
        }

        public void RefreshMinimap(DungeonGenerator generator)
        {
            ClearElements();

            if (mapViewport == null || generator == null || !generator.IsGenerated || generator.CurrentLayout == null)
            {
                return;
            }

            IReadOnlyList<RoomModule> rooms = generator.SpawnedRooms;
            if (rooms == null || rooms.Count == 0)
            {
                return;
            }

            if (labelFont == null)
            {
                labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            // Compute XZ bounding box across all rooms
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            var roomById = new Dictionary<int, RoomModule>();

            foreach (RoomModule r in rooms)
            {
                if (r == null) continue;
                roomById[r.RoomId] = r;
                Bounds b = r.GetCurrentWorldBounds();
                if (b.min.x < minX) minX = b.min.x;
                if (b.max.x > maxX) maxX = b.max.x;
                if (b.min.z < minZ) minZ = b.min.z;
                if (b.max.z > maxZ) maxZ = b.max.z;
            }

            float spanX = Mathf.Max(20f, maxX - minX);
            float spanZ = Mathf.Max(20f, maxZ - minZ);
            float maxSpan = Mathf.Max(spanX, spanZ) * 1.18f;
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;

            Vector2 rectSize = mapViewport.rect.size;
            if (rectSize.x < 10f || rectSize.y < 10f)
            {
                rectSize = new Vector2(230f, 200f);
            }

            float usableRadius = Mathf.Min(rectSize.x, rectSize.y) * 0.44f;

            Vector2 WorldToMinimap(Vector3 worldPos)
            {
                float nx = (worldPos.x - centerX) / (maxSpan * 0.5f);
                float nz = (worldPos.z - centerZ) / (maxSpan * 0.5f);
                return new Vector2(nx * usableRadius, nz * usableRadius);
            }

            // 1. Draw corridor connection links first
            foreach (RoomConnectionData conn in generator.CurrentLayout.connections)
            {
                if (!roomById.TryGetValue(conn.fromRoomId, out RoomModule rA) ||
                    !roomById.TryGetValue(conn.toRoomId, out RoomModule rB))
                {
                    continue;
                }

                Vector2 pA = WorldToMinimap(rA.transform.position);
                Vector2 pB = WorldToMinimap(rB.transform.position);
                bool onMainPath = rA.IsOnMainPath && rB.IsOnMainPath;

                Color lineColor = onMainPath
                    ? new Color(1.0f, 0.85f, 0.2f, 0.9f)
                    : new Color(0.55f, 0.72f, 0.88f, 0.75f);

                CreateLineElement(pA, pB, onMainPath ? 4.0f : 2.8f, lineColor);
            }

            // 2. Draw Major Room nodes on top
            foreach (RoomModule r in rooms)
            {
                if (r == null || r.RoomType == RoomType.Corridor)
                {
                    continue;
                }

                Vector2 pos = WorldToMinimap(r.transform.position);
                Vector2 nodeSize = GetNodePixelSize(r);
                Color fillColor = GetNodeFillColor(r);
                Color borderColor = GetNodeBorderColor(r);
                string label = GetNodeLabel(r);

                CreateRoomNodeElement(pos, nodeSize, fillColor, borderColor, label);
            }
        }

        private static Vector2 GetNodePixelSize(RoomModule room)
        {
            return room.RoomType switch
            {
                // Boss room occupies a 2x2 macro slot (28m x 28m)
                RoomType.Exit => new Vector2(36f, 36f),
                RoomType.Combat => new Vector2(28f, 28f),
                RoomType.Treasure => new Vector2(20f, 20f),
                _ => new Vector2(24f, 24f)
            };
        }

        private static Color GetNodeFillColor(RoomModule room)
        {
            RoomLifecycleState state = room.LifecycleController != null
                ? room.LifecycleController.State
                : RoomLifecycleState.Unvisited;

            if (room.RoomType == RoomType.Start)
            {
                return new Color(0.16f, 0.75f, 0.34f, 0.95f);
            }

            if (room.RoomType == RoomType.Treasure)
            {
                return new Color(0.92f, 0.72f, 0.12f, 0.95f);
            }

            if (room.RoomType == RoomType.Exit)
            {
                return state == RoomLifecycleState.Cleared
                    ? new Color(0.20f, 0.82f, 0.45f, 0.95f)
                    : new Color(0.75f, 0.16f, 0.50f, 0.95f);
            }

            if (state == RoomLifecycleState.Combat)
            {
                return new Color(0.90f, 0.22f, 0.16f, 0.95f);
            }

            if (state == RoomLifecycleState.Cleared)
            {
                return new Color(0.20f, 0.68f, 0.42f, 0.95f);
            }

            return room.RoomType == RoomType.Combat
                ? new Color(0.48f, 0.32f, 0.25f, 0.95f)
                : new Color(0.32f, 0.38f, 0.48f, 0.95f);
        }

        private static Color GetNodeBorderColor(RoomModule room)
        {
            if (room.LifecycleController != null && room.LifecycleController.State == RoomLifecycleState.Combat)
            {
                return new Color(1.0f, 0.35f, 0.20f, 1.0f);
            }

            if (room.IsOnMainPath)
            {
                return new Color(1.0f, 0.88f, 0.30f, 0.95f);
            }

            return new Color(0.75f, 0.82f, 0.92f, 0.85f);
        }

        private static string GetNodeLabel(RoomModule room)
        {
            RoomLifecycleState state = room.LifecycleController != null
                ? room.LifecycleController.State
                : RoomLifecycleState.Unvisited;

            if (room.RoomType == RoomType.Start) return "S";
            if (room.RoomType == RoomType.Treasure) return "$";
            if (room.RoomType == RoomType.Exit) return state == RoomLifecycleState.Cleared ? "B✓" : "BOSS";
            if (state == RoomLifecycleState.Combat) return "!";
            if (state == RoomLifecycleState.Cleared) return "✓";
            return room.RoomType == RoomType.Combat ? "E" : "C";
        }

        private void CreateLineElement(Vector2 a, Vector2 b, float thickness, Color color)
        {
            var go = new GameObject("MapEdge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(mapViewport, false);
            spawnedElements.Add(go);

            var img = go.GetComponent<Image>();
            img.color = color;

            Vector2 diff = b - a;
            float len = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(len, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void CreateRoomNodeElement(Vector2 pos, Vector2 size, Color fillColor, Color borderColor, string textLabel)
        {
            var borderGo = new GameObject("MapNode", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(mapViewport, false);
            spawnedElements.Add(borderGo);

            var borderImg = borderGo.GetComponent<Image>();
            borderImg.color = borderColor;

            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0.5f, 0.5f);
            borderRt.anchorMax = new Vector2(0.5f, 0.5f);
            borderRt.pivot = new Vector2(0.5f, 0.5f);
            borderRt.anchoredPosition = pos;
            borderRt.sizeDelta = size + new Vector2(4f, 4f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(borderGo.transform, false);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = fillColor;

            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0.5f, 0.5f);
            fillRt.anchorMax = new Vector2(0.5f, 0.5f);
            fillRt.pivot = new Vector2(0.5f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = size;

            var txtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(fillGo.transform, false);
            var txt = txtGo.GetComponent<Text>();
            txt.font = labelFont;
            txt.fontSize = textLabel.Length > 2 ? 9 : 11;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = textLabel;

            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
        }

        private void ClearElements()
        {
            if (mapViewport != null)
            {
                for (int i = mapViewport.childCount - 1; i >= 0; i--)
                {
                    GameObject child = mapViewport.GetChild(i).gameObject;
                    if (Application.isPlaying)
                    {
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
            spawnedElements.Clear();
        }
    }
}
