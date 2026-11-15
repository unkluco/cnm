using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    public enum ProceduralHazardType
    {
        Lava = 0,
        Water = 1,
        Spikes = 2,
        ExplosiveBarrel = 3
    }

    [Serializable]
    public class HazardPlacementData
    {
        public ProceduralHazardType HazardType;
        public string Name;
        public Vector3 LocalPosition;
        public Vector3 LocalScale = Vector3.one;
        public Quaternion LocalRotation = Quaternion.identity;
        public float PhaseOffset = 0f;

        public Bounds GetLocalBounds()
        {
            // Kích thước chuẩn cơ bản của mỗi loại hazard prefab
            Vector3 baseSize = HazardType switch
            {
                ProceduralHazardType.Lava => new Vector3(4f, 0.8f, 4f),
                ProceduralHazardType.Water => new Vector3(4f, 0.8f, 4f),
                ProceduralHazardType.Spikes => new Vector3(2.5f, 0.8f, 2.5f),
                ProceduralHazardType.ExplosiveBarrel => new Vector3(0.8f, 1.1f, 0.8f),
                _ => Vector3.one
            };

            Vector3 size = new Vector3(
                baseSize.x * LocalScale.x,
                baseSize.y * LocalScale.y,
                baseSize.z * LocalScale.z);

            return new Bounds(LocalPosition, size);
        }
    }

    [Serializable]
    public class CoverPlacementData
    {
        public string Name;
        public Vector3 LocalPosition;
        public Vector3 LocalScale;
        public Quaternion LocalRotation = Quaternion.identity;
        public bool IsThinPillar = false;

        public Bounds GetLocalBounds()
        {
            return new Bounds(LocalPosition, LocalScale);
        }
    }

    [Serializable]
    public struct CandidateScoreBreakdown
    {
        public float Diversity;      // D (25%)
        public float Movement;       // M (20%)
        public float Combat;         // C (20%)
        public float Readability;    // R (15%)
        public float Visual;         // V (10%)
        public float EnemyQuality;   // E (10%)
        public float TotalScore;

        public void CalculateTotal()
        {
            TotalScore = 0.25f * Diversity +
                         0.20f * Movement +
                         0.20f * Combat +
                         0.15f * Readability +
                         0.10f * Visual +
                         0.10f * EnemyQuality;
        }
    }

    [Serializable]
    public class RoomTacticalLayout
    {
        public int Seed;
        public TerrainTheme Theme;
        public string PatternName;
        public Vector2 RoomSize = new Vector2(24f, 24f);

        public List<HazardPlacementData> Hazards = new List<HazardPlacementData>();
        public List<CoverPlacementData> Covers = new List<CoverPlacementData>();
        public List<Vector3> EnemySpawns = new List<Vector3>();
        public List<Vector3> LootSpawns = new List<Vector3>();

        // Tuyến đường an toàn đã được xác minh kết nối giữa các cặp cửa
        [NonSerialized] public List<List<Vector3>> VerifiedSafeRoutes = new List<List<Vector3>>();

        public bool IsValid = true;
        public string RejectionReason = string.Empty;
        public CandidateScoreBreakdown Score;

        /// <summary>
        /// Tạo chữ ký địa hình (Terrain Signature) phục vụ tính toán độ đa dạng (Diversity)
        /// và chống lặp lại mẫu bố cục giữa các phòng liên tiếp.
        /// </summary>
        public string GenerateSignature()
        {
            return $"{Theme}_{PatternName}_H{Hazards.Count}_C{Covers.Count}_E{EnemySpawns.Count}";
        }
    }
}
