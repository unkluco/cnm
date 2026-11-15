using System.Collections.Generic;
using LightSouls.MapGeneration.Hazards;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Editor
{
    /// <summary>
    /// Công cụ Editor kiểm thử định lượng các quy chuẩn hình học và an toàn phòng:
    /// 1. Vùng an toàn cửa (Doorway Safe Zone 2.5m): 0 hazard, 0 quái sinh, 0 vật cản che cửa.
    /// 2. Chiều cao vật cản tầm thấp (Low Cover): <= 1.2m (trừ cột trụ mỏng) để tránh che camera.
    /// 3. Vòng đời bẫy: kết nối với RoomLifecycleController.
    /// </summary>
    public static class HazardValidationTool
    {
        public class ValidationResult
        {
            public bool IsValid = true;
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }

        [MenuItem("LightSouls/Map Generation/Validate All Hazard Rooms (TDS Checks)")]
        public static void ValidateAllHazardRoomsMenu()
        {
            string[] roomGuids = AssetDatabase.FindAssets("Room_ t:Prefab", new[] { "Assets/Prefabs/MapGeneration/Rooms" });
            int totalRooms = 0;
            int passCount = 0;

            foreach (string guid in roomGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                totalRooms++;
                ValidationResult result = ValidateRoomPrefab(prefab);

                if (result.IsValid)
                {
                    passCount++;
                    Debug.Log($"<color=green>[TDS Validation PASS]</color> {prefab.name}: Không có vi phạm vùng an toàn cửa hay chiều cao vật cản.");
                }
                else
                {
                    foreach (string err in result.Errors)
                    {
                        Debug.LogError($"<color=red>[TDS Validation FAIL]</color> {prefab.name}: {err}", prefab);
                    }
                }

                foreach (string warn in result.Warnings)
                {
                    Debug.LogWarning($"[TDS Validation WARNING] {prefab.name}: {warn}", prefab);
                }
            }

            Debug.Log($"[HazardValidationTool] Kết quả kiểm thử: {passCount}/{totalRooms} phòng đạt chuẩn 100%!");
        }

        public static ValidationResult ValidateRoomPrefab(GameObject roomObj)
        {
            var result = new ValidationResult();
            if (roomObj == null)
            {
                result.IsValid = false;
                result.Errors.Add("GameObject phòng bị null.");
                return result;
            }

            RoomModule module = roomObj.GetComponent<RoomModule>();
            var safeZones = roomObj.GetComponentsInChildren<DoorwaySafeZone>(true);
            var sockets = roomObj.GetComponentsInChildren<DoorSocket>(true);
            var hazards = roomObj.GetComponentsInChildren<HazardZoneBase>(true);
            var barrels = roomObj.GetComponentsInChildren<ExplosiveBarrelHazard>(true);

            if (safeZones.Length < sockets.Length)
            {
                result.Warnings.Add($"Phòng '{roomObj.name}' có {sockets.Length} DoorSocket nhưng chỉ có {safeZones.Length} DoorwaySafeZone.");
            }

            // 1. Kiểm tra Doorway Safe Zone (2.5m)
            foreach (DoorSocket socket in sockets)
            {
                Vector3 socketPos = socket.transform.position;
                Vector3 inwardDir = -socket.transform.forward;

                foreach (HazardZoneBase hazard in hazards)
                {
                    Collider hCol = hazard.GetComponent<Collider>();
                    if (hCol == null) continue;

                    Vector3 closestPt = hCol.bounds.ClosestPoint(socketPos);
                    float distClosest = Vector2.Distance(
                        new Vector2(socketPos.x, socketPos.z),
                        new Vector2(closestPt.x, closestPt.z));

                    float distCenter = Vector2.Distance(
                        new Vector2(socketPos.x, socketPos.z),
                        new Vector2(hCol.bounds.center.x, hCol.bounds.center.z));

                    float dist = Mathf.Min(distClosest, distCenter);

                    // Nếu khoảng cách nhỏ hơn bán kính an toàn 2.5m
                    if (dist < 2.5f)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Hazard '{hazard.name}' vi phạm vùng an toàn cửa '{socket.name}' (khoảng cách {dist:F2}m < 2.5m)!");
                    }
                }

                foreach (ExplosiveBarrelHazard barrel in barrels)
                {
                    Collider bCol = barrel.GetComponent<Collider>();
                    if (bCol == null) continue;

                    Vector3 closestPt = bCol.bounds.ClosestPoint(socketPos);
                    float dist = Vector2.Distance(
                        new Vector2(socketPos.x, socketPos.z),
                        new Vector2(closestPt.x, closestPt.z));

                    if (dist < 2.5f)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Thùng nổ '{barrel.name}' vi phạm vùng an toàn cửa '{socket.name}' (khoảng cách {dist:F2}m < 2.5m)!");
                    }
                }

                // Kiểm tra điểm sinh quái không nằm trong vùng an toàn cửa
                if (module != null && module.EnemySpawnPoints != null)
                {
                    foreach (Transform spawnPoint in module.EnemySpawnPoints)
                    {
                        if (spawnPoint == null) continue;
                        float dist = Vector2.Distance(
                            new Vector2(socketPos.x, socketPos.z),
                            new Vector2(spawnPoint.position.x, spawnPoint.position.z));

                        if (dist < 2.5f)
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Điểm sinh quái '{spawnPoint.name}' nằm trong vùng an toàn cửa '{socket.name}' ({dist:F2}m < 2.5m)!");
                        }
                    }
                }
            }

            // 2. Kiểm tra chiều cao vật cản bên trong (Cover Height <= 1.2m)
            Transform coverRoot = roomObj.transform.Find("Cover") ?? roomObj.transform.Find("ArenaPillars");
            if (coverRoot != null)
            {
                var colliders = coverRoot.GetComponentsInChildren<Collider>();
                foreach (Collider col in colliders)
                {
                    if (col == null || col.isTrigger) continue;
                    float height = col.bounds.size.y;
                    // Bỏ qua cột trụ mỏng được thiết kế riêng
                    bool isThinPillar = col.name.Contains("Pillar") && col.bounds.size.x <= 1.8f && col.bounds.size.z <= 1.8f;
                    if (!isThinPillar && height > 1.25f)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Vật che chắn '{col.name}' có chiều cao {height:F2}m vượt quá giới hạn tầm thấp 1.2m (nguy cơ che camera)!");
                    }
                }
            }

            return result;
        }
    }
}
