using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Vùng an toàn cửa ra vào (Doorway Safe Zone):
    /// Bán kính tối thiểu 2.5m trên mặt phẳng XZ tính từ đoạn cửa (doorway segment rộng 4m).
    /// Nghiêm cấm: dung nham, nước sâu, bẫy cơ học, chướng ngại vật cao, điểm sinh quái bên trong vùng này.
    /// Cung cấp Gizmos màu xanh lá trực quan trong Scene View.
    /// </summary>
    [ExecuteAlways]
    public class DoorwaySafeZone : MonoBehaviour
    {
        public const float DefaultSafeRadius = 2.5f;
        public const float DefaultDoorWidth = 4.0f;

        [Header("Doorway Configuration")]
        [SerializeField] private float safeRadius = DefaultSafeRadius;
        [SerializeField] private float doorWidth = DefaultDoorWidth;
        [SerializeField] private DoorDirection doorDirection = DoorDirection.North;

        [Header("Gizmos Visualization")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color safeZoneColor = new Color(0.2f, 0.9f, 0.3f, 0.35f);
        [SerializeField] private Color wireColor = new Color(0.1f, 0.8f, 0.2f, 0.9f);

        public float SafeRadius => safeRadius;
        public float DoorWidth => doorWidth;
        public DoorDirection Direction => doorDirection;

        public void Configure(DoorDirection dir, float radius = DefaultSafeRadius, float width = DefaultDoorWidth)
        {
            doorDirection = dir;
            safeRadius = radius;
            doorWidth = width;
        }

        /// <summary>
        /// Kiểm tra một điểm trên mặt phẳng XZ có nằm trong vùng cấm/vùng an toàn của cửa hay không.
        /// Tính khoảng cách từ điểm tới đoạn thẳng cửa (segment) thay vì chỉ tính tới một điểm tâm.
        /// </summary>
        public bool IsInsideSafeZone(Vector3 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            // Cửa nằm dọc theo trục X nội bộ (từ -doorWidth/2 đến +doorWidth/2)
            float halfWidth = doorWidth * 0.5f;
            float clampedX = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
            Vector2 closestPointOnSegment = new Vector2(clampedX, 0f);
            Vector2 point2D = new Vector2(localPoint.x, localPoint.z);

            return Vector2.Distance(point2D, closestPointOnSegment) < safeRadius;
        }

        /// <summary>
        /// Kiểm tra một hộp bao (Bounds) có giao thoa với vùng an toàn cửa hay không.
        /// </summary>
        public bool IsOverlapping(Bounds worldBounds)
        {
            // Kiểm tra tâm và 4 góc đáy của bounds
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3 center = worldBounds.center;

            if (IsInsideSafeZone(center)) return true;
            if (IsInsideSafeZone(new Vector3(min.x, center.y, min.z))) return true;
            if (IsInsideSafeZone(new Vector3(max.x, center.y, min.z))) return true;
            if (IsInsideSafeZone(new Vector3(min.x, center.y, max.z))) return true;
            if (IsInsideSafeZone(new Vector3(max.x, center.y, max.z))) return true;

            Vector3 closest = worldBounds.ClosestPoint(transform.position);
            if (IsInsideSafeZone(closest)) return true;

            return false;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Gizmos.color = wireColor;
            Vector3 center = transform.position;
            Vector3 right = transform.right * (doorWidth * 0.5f);
            Vector3 forward = transform.forward * safeRadius;

            Vector3 pLeft = center - right;
            Vector3 pRight = center + right;

            // Vẽ vạch cửa
            Gizmos.DrawLine(pLeft, pRight);

            // Vẽ cung tròn an toàn tỏa vào trong phòng
            const int segments = 24;
            Vector3 prevPoint = pLeft;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-90f, 90f, t);
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 offset = rot * transform.forward * safeRadius;

                // Chiếu theo bề rộng cửa
                Vector3 origin = Vector3.Lerp(pLeft, pRight, t);
                Vector3 currPoint = origin + offset;

                if (i > 0)
                {
                    Gizmos.DrawLine(prevPoint, currPoint);
                }
                prevPoint = currPoint;
            }
        }
    }
}
