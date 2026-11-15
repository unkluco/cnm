using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Bộ sinh bố cục địa hình và vật cản chiến thuật có chủ đích (Intentional Procedural Encounter Generator):
    /// Thực hiện quy trình 7 giai đoạn:
    /// Stage 1: Room Topology
    /// Stage 2: Terrain Formation
    /// Stage 3: Tactical Layout
    /// Stage 4: Hazard Pattern Generation
    /// Stage 5: Enemy & Loot Spawn Placement
    /// Stage 6: Hard Constraints Validation & Soft Scoring (10 Candidates)
    /// Stage 7: Layout Finalization & Fallback Selection
    /// </summary>
    public static class ProceduralLayoutGenerator
    {
        public const float DoorwayClearanceRadius = 2.5f;
        public const float DoorwaySegmentWidth = 4.0f;
        public const float MaxLowCoverHeight = 1.2f;
        public const float MinBridgeWidth = 2.5f;

        private const int CandidateCount = 10;
        private const float GridCellSize = 0.6f;

        /// <summary>
        /// Sinh bố cục chiến thuật hoàn chỉnh dựa trên Seed tái tạo được (Deterministic Seed).
        /// </summary>
        public static RoomTacticalLayout Generate(
            int seed,
            TerrainTheme theme,
            Vector2 roomSize,
            List<DoorDirection> sockets = null,
            Queue<string> recentSignatures = null)
        {
            if (sockets == null || sockets.Count == 0)
            {
                sockets = new List<DoorDirection>
                {
                    DoorDirection.North,
                    DoorDirection.South,
                    DoorDirection.East,
                    DoorDirection.West
                };
            }

            var rng = new System.Random(seed);
            var candidates = new List<RoomTacticalLayout>(CandidateCount);

            for (int i = 0; i < CandidateCount; i++)
            {
                int candidateSeed = rng.Next();
                RoomTacticalLayout candidate = BuildCandidate(candidateSeed, theme, roomSize, sockets, i);
                ValidateAndScoreCandidate(candidate, roomSize, sockets, recentSignatures);
                candidates.Add(candidate);
            }

            // Lọc các ứng viên vượt qua toàn bộ Hard Constraints
            var validCandidates = new List<RoomTacticalLayout>();
            foreach (var cand in candidates)
            {
                if (cand.IsValid)
                {
                    validCandidates.Add(cand);
                }
            }

            RoomTacticalLayout selected;
            if (validCandidates.Count > 0)
            {
                // Sắp xếp điểm chất lượng Soft Score giảm dần
                validCandidates.Sort((a, b) => b.Score.TotalScore.CompareTo(a.Score.TotalScore));
                selected = validCandidates[0];
            }
            else
            {
                // Nếu cả 10 ứng viên đều lỗi (trường hợp cực đoan), kích hoạt Fallback đã qua kiểm định
                selected = BuildGuaranteedFallback(seed, theme, roomSize, sockets);
                ValidateAndScoreCandidate(selected, roomSize, sockets, recentSignatures);
            }

            // Ghi nhận chữ ký vào lịch sử để chống lặp
            if (recentSignatures != null)
            {
                recentSignatures.Enqueue(selected.GenerateSignature());
                if (recentSignatures.Count > 8)
                {
                    recentSignatures.Dequeue();
                }
            }

            return selected;
        }

        #region Candidate Construction

        private static RoomTacticalLayout BuildCandidate(
            int seed,
            TerrainTheme theme,
            Vector2 roomSize,
            List<DoorDirection> sockets,
            int candidateIndex)
        {
            var candRng = new System.Random(seed);
            var layout = new RoomTacticalLayout
            {
                Seed = seed,
                Theme = theme,
                RoomSize = roomSize
            };

            switch (theme)
            {
                case TerrainTheme.Lava:
                    BuildLavaPattern(layout, candRng, roomSize, candidateIndex);
                    break;
                case TerrainTheme.Spike:
                    BuildSpikePattern(layout, candRng, roomSize, candidateIndex);
                    break;
                case TerrainTheme.Water:
                    BuildWaterPattern(layout, candRng, roomSize, candidateIndex);
                    break;
                case TerrainTheme.LowCover:
                    BuildLowCoverPattern(layout, candRng, roomSize, candidateIndex);
                    break;
            }

            // Stage 5: Bố trí điểm sinh quái và điểm thưởng
            PopulateSpawns(layout, candRng, roomSize, sockets);

            return layout;
        }

        #endregion

        #region Pattern Implementations (18 Patterns across 4 Categories)

        // ===== NHÓM A: LAVA TERRAIN (6 Patterns) =====
        private static void BuildLavaPattern(RoomTacticalLayout layout, System.Random rng, Vector2 size, int index)
        {
            int patternChoice = (index + rng.Next(0, 6)) % 6;
            var pattern = (LavaPatternType)patternChoice;
            layout.PatternName = pattern.ToString();

            float hw = size.x * 0.5f;
            float hl = size.y * 0.5f;

            switch (pattern)
            {
                case LavaPatternType.PerimeterRiver:
                    // Rãnh dung nham men theo một góc phòng (chừa lối đi cửa)
                    layout.Hazards.Add(new HazardPlacementData
                    {
                        HazardType = ProceduralHazardType.Lava,
                        Name = "Lava_Perimeter_1",
                        LocalPosition = new Vector3(-hw + 6f, 0f, hl - 6f),
                        LocalScale = new Vector3(1.3f, 1f, 1.3f)
                    });
                    layout.Hazards.Add(new HazardPlacementData
                    {
                        HazardType = ProceduralHazardType.Lava,
                        Name = "Lava_Perimeter_2",
                        LocalPosition = new Vector3(hw - 6f, 0f, -hl + 6f),
                        LocalScale = new Vector3(1.3f, 1f, 1.3f)
                    });
                    AddCoverBox(layout, "Cover_Center_1", new Vector3(-2f, 0.5f, 2f), new Vector3(1.4f, 1.0f, 1.4f));
                    AddCoverBox(layout, "Cover_Center_2", new Vector3(2f, 0.5f, -2f), new Vector3(1.4f, 1.0f, 1.4f));
                    break;

                case LavaPatternType.SplitIslands:
                    // 4 Vùng dung nham ở 4 góc, chừa bục trung tâm 8x8m và 4 cây cầu rộng 7m
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_NW", LocalPosition = new Vector3(-6.5f, 0f, 6.5f), LocalScale = new Vector3(1.35f, 1f, 1.35f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_NE", LocalPosition = new Vector3(6.5f, 0f, 6.5f), LocalScale = new Vector3(1.35f, 1f, 1.35f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_SW", LocalPosition = new Vector3(-6.5f, 0f, -6.5f), LocalScale = new Vector3(1.35f, 1f, 1.35f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_SE", LocalPosition = new Vector3(6.5f, 0f, -6.5f), LocalScale = new Vector3(1.35f, 1f, 1.35f) });
                    AddCoverBox(layout, "Cover_Island_1", new Vector3(-3.5f, 0.5f, 3.5f), new Vector3(1.4f, 1.0f, 1.4f));
                    AddCoverBox(layout, "Cover_Island_2", new Vector3(3.5f, 0.5f, -3.5f), new Vector3(1.4f, 1.0f, 1.4f));
                    break;

                case LavaPatternType.DiagonalChannel:
                    // Rãnh dung nham cắt chéo, chừa cầu trung tâm rộng 5m
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Diag_1", LocalPosition = new Vector3(-5.5f, 0f, 4.5f), LocalScale = new Vector3(1.2f, 1f, 1.8f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Diag_2", LocalPosition = new Vector3(5.5f, 0f, -4.5f), LocalScale = new Vector3(1.2f, 1f, 1.8f) });
                    AddCoverBox(layout, "Cover_Diag_Bridge", new Vector3(-2f, 0.5f, 0f), new Vector3(1.6f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Diag_Bridge_R", new Vector3(2f, 0.5f, 0f), new Vector3(1.6f, 1.0f, 0.5f));
                    break;

                case LavaPatternType.CrescentHazard:
                    // Dung nham dạng vòng cung ôm một phần tư phòng
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Crescent_1", LocalPosition = new Vector3(-6f, 0f, -5.5f), LocalScale = new Vector3(1.3f, 1f, 1.3f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Crescent_2", LocalPosition = new Vector3(-5.5f, 0f, -1.5f), LocalScale = new Vector3(1.0f, 1f, 1.0f) });
                    AddCoverBox(layout, "Cover_Crescent", new Vector3(3f, 0.5f, 3f), new Vector3(2.0f, 1.0f, 0.5f));
                    AddThinPillar(layout, "Pillar_Crescent", new Vector3(1f, 1.75f, -1f));
                    break;

                case LavaPatternType.ScatteredPools:
                    // 3 Vũng dung nham kích thước lệch nhau rải rác
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Pool_1", LocalPosition = new Vector3(-6f, 0f, 5.5f), LocalScale = new Vector3(1.2f, 1f, 1.2f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Pool_2", LocalPosition = new Vector3(5.5f, 0f, 5.5f), LocalScale = new Vector3(1.1f, 1f, 1.1f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Pool_3", LocalPosition = new Vector3(0f, 0f, -5.5f), LocalScale = new Vector3(1.2f, 1f, 1.0f) });
                    AddCoverBox(layout, "Cover_Pool_1", new Vector3(-2f, 0.5f, 1f), new Vector3(1.5f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Pool_2", new Vector3(2f, 0.5f, 1f), new Vector3(1.5f, 1.0f, 0.5f));
                    break;

                case LavaPatternType.MultipleCrossings:
                    // 2 Hào dung nham song song có cầu cắt ngang lệch vị trí
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Trench_W", LocalPosition = new Vector3(-5.5f, 0f, 0f), LocalScale = new Vector3(1.1f, 1f, 2.2f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Lava_Trench_E", LocalPosition = new Vector3(5.5f, 0f, 0f), LocalScale = new Vector3(1.1f, 1f, 2.2f) });
                    AddCoverBox(layout, "Cover_Trench_W", new Vector3(-2.5f, 0.5f, 2f), new Vector3(1.4f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Trench_E", new Vector3(2.5f, 0.5f, -2f), new Vector3(1.4f, 1.0f, 0.5f));
                    break;
            }
        }

        // ===== NHÓM B: SPIKE TERRAIN (4 Patterns) =====
        private static void BuildSpikePattern(RoomTacticalLayout layout, System.Random rng, Vector2 size, int index)
        {
            int patternChoice = (index + rng.Next(0, 4)) % 4;
            var pattern = (SpikePatternType)patternChoice;
            layout.PatternName = pattern.ToString();

            switch (pattern)
            {
                case SpikePatternType.ParallelStrips:
                    // Các dải gai song song lệch pha
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_Strip_1", LocalPosition = new Vector3(-2.8f, 0f, 1.8f), PhaseOffset = 0.0f });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_Strip_2", LocalPosition = new Vector3(2.8f, 0f, -1.8f), PhaseOffset = 0.6f });
                    AddCoverBox(layout, "Cover_Strip_1", new Vector3(-2.8f, 0.5f, -1.8f), new Vector3(1.6f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Strip_2", new Vector3(2.8f, 0.5f, 1.8f), new Vector3(1.6f, 1.0f, 0.4f));
                    break;

                case SpikePatternType.TimedCrossroads:
                    // Gai tại các giao lộ có cover bao bọc
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_N", LocalPosition = new Vector3(0f, 0f, 2.6f), PhaseOffset = 0.0f });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_E", LocalPosition = new Vector3(2.6f, 0f, 0f), PhaseOffset = 0.4f });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_S", LocalPosition = new Vector3(0f, 0f, -2.6f), PhaseOffset = 0.8f });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_W", LocalPosition = new Vector3(-2.6f, 0f, 0f), PhaseOffset = 0.4f });
                    AddCoverBox(layout, "Cover_Cross_1", new Vector3(-2.6f, 0.5f, 2.6f), new Vector3(1.6f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Cross_2", new Vector3(2.6f, 0.5f, 2.6f), new Vector3(1.6f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Cross_3", new Vector3(-2.6f, 0.5f, -2.6f), new Vector3(1.6f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Cross_4", new Vector3(2.6f, 0.5f, -2.6f), new Vector3(1.6f, 1.0f, 0.4f));
                    break;

                case SpikePatternType.PressurePockets:
                    // Các túi gai nhỏ đặt lệch tâm
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_Pocket_1", LocalPosition = new Vector3(-3.2f, 0f, 2.5f), PhaseOffset = 0.2f });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_Pocket_2", LocalPosition = new Vector3(3.2f, 0f, -2.5f), PhaseOffset = 0.8f });
                    AddCoverBox(layout, "Cover_Pocket_1", new Vector3(0f, 0.5f, 2.5f), new Vector3(2.0f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Pocket_2", new Vector3(0f, 0.5f, -2.5f), new Vector3(2.0f, 1.0f, 0.4f));
                    break;

                case SpikePatternType.StaggeredLanes:
                    // Các dải so le, chừa nhiều lối đi an toàn
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_Lane_1", LocalPosition = new Vector3(-2.5f, 0f, 0f), PhaseOffset = 0.0f });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Spike_Lane_2", LocalPosition = new Vector3(2.5f, 0f, 0f), PhaseOffset = 0.5f });
                    AddCoverBox(layout, "Cover_Lane_1", new Vector3(-2.5f, 0.5f, 2.5f), new Vector3(1.4f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Lane_2", new Vector3(2.5f, 0.5f, -2.5f), new Vector3(1.4f, 1.0f, 0.4f));
                    break;
            }
        }

        // ===== NHÓM C: WATER TERRAIN (4 Patterns) =====
        private static void BuildWaterPattern(RoomTacticalLayout layout, System.Random rng, Vector2 size, int index)
        {
            int patternChoice = (index + rng.Next(0, 4)) % 4;
            var pattern = (WaterPatternType)patternChoice;
            layout.PatternName = pattern.ToString();

            switch (pattern)
            {
                case WaterPatternType.CentralBasin:
                    // Hồ nước trung tâm (6x6m), khô ráo xung quanh + 3 thùng nổ
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Water, Name = "Water_Basin", LocalPosition = Vector3.zero, LocalScale = new Vector3(1.5f, 1f, 1.5f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_1", LocalPosition = new Vector3(-2f, 0f, 2f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_2", LocalPosition = new Vector3(2f, 0f, -2f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_3", LocalPosition = new Vector3(0f, 0f, 0f) });
                    AddCoverBox(layout, "Cover_Perimeter_W", new Vector3(-4.5f, 0.5f, 0f), new Vector3(0.5f, 1.0f, 2.0f));
                    AddCoverBox(layout, "Cover_Perimeter_E", new Vector3(4.5f, 0.5f, 0f), new Vector3(0.5f, 1.0f, 2.0f));
                    break;

                case WaterPatternType.BranchingStreams:
                    // 2 Nhánh nước chia ranh giới các góc phòng
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Water, Name = "Water_Branch_1", LocalPosition = new Vector3(-4f, 0f, 3.5f), LocalScale = new Vector3(1.1f, 1f, 1.1f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Water, Name = "Water_Branch_2", LocalPosition = new Vector3(4f, 0f, -3.5f), LocalScale = new Vector3(1.1f, 1f, 1.1f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_1", LocalPosition = new Vector3(-3.5f, 0f, 2f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_2", LocalPosition = new Vector3(3.5f, 0f, -2f) });
                    AddCoverBox(layout, "Cover_Stream_1", new Vector3(0f, 0.5f, 2.5f), new Vector3(2.0f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Stream_2", new Vector3(0f, 0.5f, -2.5f), new Vector3(2.0f, 1.0f, 0.4f));
                    break;

                case WaterPatternType.WaterPatches:
                    // Các vũng nước nhỏ kèm thùng nổ
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Water, Name = "Water_Patch_1", LocalPosition = new Vector3(-3.5f, 0f, -3.5f), LocalScale = new Vector3(1.1f, 1f, 1.1f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Water, Name = "Water_Patch_2", LocalPosition = new Vector3(3.5f, 0f, 3.5f), LocalScale = new Vector3(1.1f, 1f, 1.1f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_1", LocalPosition = new Vector3(-2.8f, 0f, -2.8f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_2", LocalPosition = new Vector3(2.8f, 0f, 2.8f) });
                    AddCoverBox(layout, "Cover_Patch", new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1.0f, 1.5f));
                    break;

                case WaterPatternType.FloodedRoute:
                    // Một làn nước làm chậm (0.5x), nhưng có đường ven khô ráo
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Water, Name = "Water_FloodedLane", LocalPosition = new Vector3(0f, 0f, 2.5f), LocalScale = new Vector3(1.8f, 1f, 0.9f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_1", LocalPosition = new Vector3(0f, 0f, 1.5f) });
                    layout.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.ExplosiveBarrel, Name = "Barrel_2", LocalPosition = new Vector3(2.5f, 0f, 0f) });
                    AddCoverBox(layout, "Cover_Flooded_1", new Vector3(-3.0f, 0.5f, -2.0f), new Vector3(1.6f, 1.0f, 0.4f));
                    AddCoverBox(layout, "Cover_Flooded_2", new Vector3(3.0f, 0.5f, -2.0f), new Vector3(1.6f, 1.0f, 0.4f));
                    break;
            }
        }

        // ===== NHÓM D: LOW COVER (4 Patterns) =====
        private static void BuildLowCoverPattern(RoomTacticalLayout layout, System.Random rng, Vector2 size, int index)
        {
            int patternChoice = (index + rng.Next(0, 4)) % 4;
            var pattern = (LowCoverPatternType)patternChoice;
            layout.PatternName = pattern.ToString();

            float hw = size.x * 0.5f;
            float hl = size.y * 0.5f;

            switch (pattern)
            {
                case LowCoverPatternType.StaggeredWalls:
                    // Các vách tường thấp xếp so le, giữ thông thoáng tầm nhìn
                    AddCoverBox(layout, "Cover_Stagger_NW", new Vector3(-5.5f, 0.5f, 5.5f), new Vector3(4.5f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Stagger_NE", new Vector3(5.5f, 0.5f, 5.5f), new Vector3(4.5f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Stagger_SW", new Vector3(-5.5f, 0.5f, -5.5f), new Vector3(4.5f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Stagger_SE", new Vector3(5.5f, 0.5f, -5.5f), new Vector3(4.5f, 1.0f, 0.5f));
                    AddThinPillar(layout, "Pillar_Center_NW", new Vector3(-4.0f, 1.75f, 3.5f));
                    AddThinPillar(layout, "Pillar_Center_NE", new Vector3(4.0f, 1.75f, 3.5f));
                    AddThinPillar(layout, "Pillar_Center_SW", new Vector3(-4.0f, 1.75f, -3.5f));
                    AddThinPillar(layout, "Pillar_Center_SE", new Vector3(4.0f, 1.75f, -3.5f));
                    break;

                case LowCoverPatternType.PillarField:
                    // Cụm cột mỏng (0.7m) phân bố cách đều theo Poisson-disk style
                    AddThinPillar(layout, "Pillar_1", new Vector3(-5.0f, 1.75f, 5.0f));
                    AddThinPillar(layout, "Pillar_2", new Vector3(5.0f, 1.75f, 5.0f));
                    AddThinPillar(layout, "Pillar_3", new Vector3(-5.0f, 1.75f, -5.0f));
                    AddThinPillar(layout, "Pillar_4", new Vector3(5.0f, 1.75f, -5.0f));
                    AddCoverBox(layout, "Cover_Center_Low", new Vector3(0f, 0.5f, 0f), new Vector3(2.5f, 1.0f, 0.5f));
                    break;

                case LowCoverPatternType.RuinedStructures:
                    // Các vách đổ vỡ chữ L tầm thấp
                    AddCoverBox(layout, "Cover_Ruin_1_H", new Vector3(-5.0f, 0.5f, 4.0f), new Vector3(4.0f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Ruin_1_V", new Vector3(-7.0f, 0.5f, 2.0f), new Vector3(0.5f, 1.0f, 3.5f));
                    AddCoverBox(layout, "Cover_Ruin_2_H", new Vector3(5.0f, 0.5f, -4.0f), new Vector3(4.0f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Ruin_2_V", new Vector3(7.0f, 0.5f, -2.0f), new Vector3(0.5f, 1.0f, 3.5f));
                    AddThinPillar(layout, "Pillar_Ruin_1", new Vector3(0f, 1.75f, 3.5f));
                    AddThinPillar(layout, "Pillar_Ruin_2", new Vector3(0f, 1.75f, -3.5f));
                    break;

                case LowCoverPatternType.CentralCoverCluster:
                    // Pháo đài che chắn trung tâm, 4 làn bao quanh rộng 5m
                    AddCoverBox(layout, "Cover_Hub_N", new Vector3(0f, 0.5f, 3.0f), new Vector3(3.0f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Hub_S", new Vector3(0f, 0.5f, -3.0f), new Vector3(3.0f, 1.0f, 0.5f));
                    AddCoverBox(layout, "Cover_Hub_W", new Vector3(-3.0f, 0.5f, 0f), new Vector3(0.5f, 1.0f, 3.0f));
                    AddCoverBox(layout, "Cover_Hub_E", new Vector3(3.0f, 0.5f, 0f), new Vector3(0.5f, 1.0f, 3.0f));
                    AddThinPillar(layout, "Pillar_Corner_NW", new Vector3(-6.0f, 1.75f, 6.0f));
                    AddThinPillar(layout, "Pillar_Corner_NE", new Vector3(6.0f, 1.75f, 6.0f));
                    AddThinPillar(layout, "Pillar_Corner_SW", new Vector3(-6.0f, 1.75f, -6.0f));
                    AddThinPillar(layout, "Pillar_Corner_SE", new Vector3(6.0f, 1.75f, -6.0f));
                    break;
            }
        }

        private static void AddCoverBox(RoomTacticalLayout layout, string name, Vector3 pos, Vector3 size)
        {
            layout.Covers.Add(new CoverPlacementData
            {
                Name = name,
                LocalPosition = pos,
                LocalScale = size,
                IsThinPillar = false
            });
        }

        private static void AddThinPillar(RoomTacticalLayout layout, string name, Vector3 pos)
        {
            layout.Covers.Add(new CoverPlacementData
            {
                Name = name,
                LocalPosition = pos,
                LocalScale = new Vector3(0.7f, 3.5f, 0.7f),
                IsThinPillar = true
            });
        }

        #endregion

        #region Spawn Distribution (Stage 5)

        private static void PopulateSpawns(RoomTacticalLayout layout, System.Random rng, Vector2 roomSize, List<DoorDirection> sockets)
        {
            float hw = roomSize.x * 0.5f;
            float hl = roomSize.y * 0.5f;
            int enemyCount = roomSize.x > 18f ? 5 : 3;

            // Ứng viên các điểm spawn chiến thuật
            var candidatePoints = new List<Vector3>
            {
                new Vector3(-hw * 0.45f, 0f, hl * 0.45f),
                new Vector3(hw * 0.45f, 0f, hl * 0.45f),
                new Vector3(-hw * 0.45f, 0f, -hl * 0.45f),
                new Vector3(hw * 0.45f, 0f, -hl * 0.45f),
                new Vector3(0f, 0f, hl * 0.4f),
                new Vector3(0f, 0f, -hl * 0.4f),
                new Vector3(-hw * 0.4f, 0f, 0f),
                new Vector3(hw * 0.4f, 0f, 0f)
            };

            // Shuffle bằng seed
            for (int i = 0; i < candidatePoints.Count; i++)
            {
                int swapIdx = rng.Next(i, candidatePoints.Count);
                Vector3 temp = candidatePoints[i];
                candidatePoints[i] = candidatePoints[swapIdx];
                candidatePoints[swapIdx] = temp;
            }

            foreach (Vector3 pt in candidatePoints)
            {
                if (layout.EnemySpawns.Count >= enemyCount) break;

                // Kiểm tra điểm không nằm trong Doorway Safe Zone (2.5m)
                if (IsPointInsideAnyDoorwaySafeZone(pt, roomSize, sockets)) continue;

                // Kiểm tra điểm không nằm trực tiếp trong Lava hoặc Spikes
                if (IsPointInsideDamagingHazard(pt, layout.Hazards)) continue;

                layout.EnemySpawns.Add(pt);
            }

            // Điểm thưởng / rương
            Vector3 lootPoint = new Vector3(0f, 0.45f, 0f);
            if (IsPointInsideDamagingHazard(lootPoint, layout.Hazards))
            {
                lootPoint = new Vector3(0f, 0.45f, -hl * 0.45f);
            }
            layout.LootSpawns.Add(lootPoint);
        }

        #endregion

        #region Hard Constraints Validation & Scoring (Stage 6)

        private static void ValidateAndScoreCandidate(
            RoomTacticalLayout candidate,
            Vector2 roomSize,
            List<DoorDirection> sockets,
            Queue<string> recentSignatures)
        {
            candidate.IsValid = true;
            candidate.RejectionReason = string.Empty;

            // 1. Hard Constraint: 0 hazard nằm trong Doorway Safe Zone (2.5m từ cửa)
            foreach (var h in candidate.Hazards)
            {
                Bounds hBounds = h.GetLocalBounds();
                if (IsBoundsOverlappingDoorwaySafeZones(hBounds, roomSize, sockets))
                {
                    candidate.IsValid = false;
                    candidate.RejectionReason = $"Hazard '{h.Name}' xâm phạm vùng an toàn cửa!";
                    return;
                }
            }

            // 2. Hard Constraint: Chiều cao vật cản tầm thấp <= 1.2m
            foreach (var c in candidate.Covers)
            {
                if (!c.IsThinPillar && c.LocalScale.y > MaxLowCoverHeight)
                {
                    candidate.IsValid = false;
                    candidate.RejectionReason = $"Cover '{c.Name}' có chiều cao {c.LocalScale.y:F2}m vượt quá 1.2m!";
                    return;
                }
            }

            // 3. Hard Constraint: Điểm sinh quái không nằm trong vùng cấm
            foreach (var sp in candidate.EnemySpawns)
            {
                if (IsPointInsideAnyDoorwaySafeZone(sp, roomSize, sockets))
                {
                    candidate.IsValid = false;
                    candidate.RejectionReason = "Điểm sinh quái nằm trong vùng an toàn cửa!";
                    return;
                }
            }

            // 4. Hard Constraint: Mọi cặp cửa ĐỀU PHẢI có ít nhất 1 đường an toàn liên tục
            if (!VerifySafeRouteConnectivity(candidate, roomSize, sockets, out var verifiedRoutes))
            {
                candidate.IsValid = false;
                candidate.RejectionReason = "Không tồn tại đường an toàn liên tục giữa các cửa!";
                return;
            }

            candidate.VerifiedSafeRoutes = verifiedRoutes;

            // Soft Constraints Quality Scoring
            // Score = 0.25D + 0.20M + 0.20C + 0.15R + 0.10V + 0.10E
            float diversity = 1.0f;
            if (recentSignatures != null && recentSignatures.Contains(candidate.GenerateSignature()))
            {
                diversity = 0.4f;
            }

            float movement = Mathf.Clamp01(1.0f - (candidate.Hazards.Count * 0.08f));
            float combat = Mathf.Clamp01(candidate.Covers.Count * 0.2f);
            float readability = 0.85f;
            float visual = 0.90f;
            float enemyQuality = candidate.EnemySpawns.Count >= 3 ? 0.95f : 0.70f;

            candidate.Score = new CandidateScoreBreakdown
            {
                Diversity = diversity,
                Movement = movement,
                Combat = combat,
                Readability = readability,
                Visual = visual,
                EnemyQuality = enemyQuality
            };
            candidate.Score.CalculateTotal();
        }

        /// <summary>
        /// Phân tích đồ thị / lưới không gian để xác minh đường an toàn giữa mọi cặp cửa.
        /// Sử dụng thuật toán Breadth-First Search (BFS) trên lưới ô 0.6m.
        /// </summary>
        public static bool VerifySafeRouteConnectivity(
            RoomTacticalLayout candidate,
            Vector2 roomSize,
            List<DoorDirection> sockets,
            out List<List<Vector3>> verifiedRoutes)
        {
            verifiedRoutes = new List<List<Vector3>>();
            float hw = roomSize.x * 0.5f;
            float hl = roomSize.y * 0.5f;

            int cols = Mathf.Max(10, Mathf.RoundToInt(roomSize.x / GridCellSize));
            int rows = Mathf.Max(10, Mathf.RoundToInt(roomSize.y / GridCellSize));

            bool[,] isWalkable = new bool[cols, rows];

            // 1. Khởi tạo lưới an toàn
            for (int c = 0; c < cols; c++)
            {
                float x = -hw + (c + 0.5f) * GridCellSize;
                for (int r = 0; r < rows; r++)
                {
                    float z = -hl + (r + 0.5f) * GridCellSize;
                    Vector3 worldPoint = new Vector3(x, 0f, z);

                    // Ô bị chặn nếu nằm trong Lava hoặc Spikes
                    bool inDamagingHazard = false;
                    foreach (var h in candidate.Hazards)
                    {
                        if (h.HazardType == ProceduralHazardType.Lava || h.HazardType == ProceduralHazardType.Spikes)
                        {
                            Bounds b = h.GetLocalBounds();
                            b.Expand(0.3f); // Thêm bán kính agent
                            if (b.Contains(worldPoint))
                            {
                                inDamagingHazard = true;
                                break;
                            }
                        }
                    }

                    // Ô bị chặn nếu nằm trong tường cover
                    bool inCover = false;
                    foreach (var cov in candidate.Covers)
                    {
                        Bounds b = cov.GetLocalBounds();
                        if (b.Contains(worldPoint))
                        {
                            inCover = true;
                            break;
                        }
                    }

                    isWalkable[c, r] = !inDamagingHazard && !inCover;
                }
            }

            // 2. Chuyển đổi tọa độ các cửa thành tọa độ ô lưới
            var doorCells = new List<(int c, int r, DoorDirection dir)>();
            foreach (DoorDirection dir in sockets)
            {
                Vector3 doorPos = GetDoorCenter(dir, hw, hl);
                // Lấy điểm an toàn ngay trước cửa (cách 1m vào trong)
                Vector3 entryPos = doorPos - dir.ToVector3() * 1.0f;
                int c = Mathf.Clamp(Mathf.FloorToInt((entryPos.x + hw) / GridCellSize), 0, cols - 1);
                int r = Mathf.Clamp(Mathf.FloorToInt((entryPos.z + hl) / GridCellSize), 0, rows - 1);
                isWalkable[c, r] = true; // Cửa luôn an toàn
                doorCells.Add((c, r, dir));
            }

            // 3. BFS tìm đường giữa cửa đầu tiên tới tất cả các cửa còn lại
            if (doorCells.Count < 2)
            {
                return true;
            }

            for (int i = 0; i < doorCells.Count; i++)
            {
                for (int j = i + 1; j < doorCells.Count; j++)
                {
                    var start = doorCells[i];
                    var goal = doorCells[j];

                    if (!TryFindPathBFS(start.c, start.r, goal.c, goal.r, isWalkable, cols, rows, hw, hl, out var path))
                    {
                        return false; // Phát hiện ít nhất một cặp cửa bị đứt đoạn an toàn
                    }
                    verifiedRoutes.Add(path);
                }
            }

            return true;
        }

        private static bool TryFindPathBFS(
            int startC, int startR, int goalC, int goalR,
            bool[,] isWalkable, int cols, int rows,
            float hw, float hl,
            out List<Vector3> worldPath)
        {
            worldPath = new List<Vector3>();
            var queue = new Queue<(int c, int r)>();
            var parent = new Dictionary<(int c, int r), (int c, int r)>();
            var visited = new bool[cols, rows];

            queue.Enqueue((startC, startR));
            visited[startC, startR] = true;

            int[] dc = { 1, -1, 0, 0 };
            int[] dr = { 0, 0, 1, -1 };
            bool found = false;

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (curr.c == goalC && curr.r == goalR)
                {
                    found = true;
                    break;
                }

                for (int i = 0; i < 4; i++)
                {
                    int nc = curr.c + dc[i];
                    int nr = curr.r + dr[i];

                    if (nc >= 0 && nc < cols && nr >= 0 && nr < rows && !visited[nc, nr] && isWalkable[nc, nr])
                    {
                        visited[nc, nr] = true;
                        parent[(nc, nr)] = curr;
                        queue.Enqueue((nc, nr));
                    }
                }
            }

            if (!found) return false;

            // Truy vết đường đi
            var trace = (goalC, goalR);
            while (trace != (startC, startR))
            {
                float x = -hw + (trace.goalC + 0.5f) * GridCellSize;
                float z = -hl + (trace.goalR + 0.5f) * GridCellSize;
                worldPath.Add(new Vector3(x, 0.05f, z));
                trace = parent[trace];
            }
            worldPath.Reverse();
            return true;
        }

        #endregion

        #region Geometry & Safe Zone Helpers

        public static Vector3 GetDoorCenter(DoorDirection dir, float halfW, float halfL)
        {
            return dir switch
            {
                DoorDirection.North => new Vector3(0f, 0f, halfL),
                DoorDirection.South => new Vector3(0f, 0f, -halfL),
                DoorDirection.East => new Vector3(halfW, 0f, 0f),
                DoorDirection.West => new Vector3(-halfW, 0f, 0f),
                _ => Vector3.zero
            };
        }

        public static bool IsPointInsideAnyDoorwaySafeZone(Vector3 localPoint, Vector2 roomSize, List<DoorDirection> sockets)
        {
            float hw = roomSize.x * 0.5f;
            float hl = roomSize.y * 0.5f;

            foreach (DoorDirection dir in sockets)
            {
                Vector3 doorPos = GetDoorCenter(dir, hw, hl);
                float dist = CalculateDistanceToDoorSegment(localPoint, doorPos, dir, DoorwaySegmentWidth);
                if (dist < DoorwayClearanceRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsBoundsOverlappingDoorwaySafeZones(Bounds bounds, Vector2 roomSize, List<DoorDirection> sockets)
        {
            float hw = roomSize.x * 0.5f;
            float hl = roomSize.y * 0.5f;

            foreach (DoorDirection dir in sockets)
            {
                Vector3 doorPos = GetDoorCenter(dir, hw, hl);
                Vector3 closestPt = bounds.ClosestPoint(doorPos);
                float distClosest = CalculateDistanceToDoorSegment(closestPt, doorPos, dir, DoorwaySegmentWidth);
                float distCenter = CalculateDistanceToDoorSegment(bounds.center, doorPos, dir, DoorwaySegmentWidth);

                if (Mathf.Min(distClosest, distCenter) < DoorwayClearanceRadius)
                {
                    return true;
                }
            }
            return false;
        }

        private static float CalculateDistanceToDoorSegment(Vector3 point, Vector3 doorCenter, DoorDirection dir, float doorWidth)
        {
            Vector3 diff = point - doorCenter;
            float halfWidth = doorWidth * 0.5f;

            if (dir == DoorDirection.North || dir == DoorDirection.South)
            {
                // Đoạn cửa chạy dọc trục X (-halfWidth đến +halfWidth)
                float clampedX = Mathf.Clamp(diff.x, -halfWidth, halfWidth);
                Vector2 closest = new Vector2(clampedX, 0f);
                Vector2 pt2D = new Vector2(diff.x, diff.z);
                return Vector2.Distance(pt2D, closest);
            }
            else
            {
                // Đoạn cửa chạy dọc trục Z (-halfWidth đến +halfWidth)
                float clampedZ = Mathf.Clamp(diff.z, -halfWidth, halfWidth);
                Vector2 closest = new Vector2(0f, clampedZ);
                Vector2 pt2D = new Vector2(diff.x, diff.z);
                return Vector2.Distance(pt2D, closest);
            }
        }

        private static bool IsPointInsideDamagingHazard(Vector3 point, List<HazardPlacementData> hazards)
        {
            foreach (var h in hazards)
            {
                if (h.HazardType == ProceduralHazardType.Lava || h.HazardType == ProceduralHazardType.Spikes)
                {
                    Bounds b = h.GetLocalBounds();
                    if (b.Contains(point)) return true;
                }
            }
            return false;
        }

        #endregion

        #region Fallback Construction

        /// <summary>
        /// Tạo bố cục dự phòng đã được bảo đảm 100% hợp lệ để hệ thống không bao giờ bị gián đoạn.
        /// </summary>
        public static RoomTacticalLayout BuildGuaranteedFallback(
            int seed,
            TerrainTheme theme,
            Vector2 roomSize,
            List<DoorDirection> sockets)
        {
            var fallback = new RoomTacticalLayout
            {
                Seed = seed,
                Theme = theme,
                PatternName = "GuaranteedFallback",
                RoomSize = roomSize
            };

            float hw = roomSize.x * 0.5f;
            float hl = roomSize.y * 0.5f;

            // Đặt hazard ở 2 góc xa nhất, cách mọi cửa > 5m
            if (theme == TerrainTheme.Lava)
            {
                fallback.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Fallback_Lava_NW", LocalPosition = new Vector3(-hw * 0.55f, 0f, hl * 0.55f), LocalScale = new Vector3(1.1f, 1f, 1.1f) });
                fallback.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Lava, Name = "Fallback_Lava_SE", LocalPosition = new Vector3(hw * 0.55f, 0f, -hl * 0.55f), LocalScale = new Vector3(1.1f, 1f, 1.1f) });
            }
            else if (theme == TerrainTheme.Spike)
            {
                fallback.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Fallback_Spike_NW", LocalPosition = new Vector3(-hw * 0.45f, 0f, hl * 0.45f), PhaseOffset = 0.0f });
                fallback.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Spikes, Name = "Fallback_Spike_SE", LocalPosition = new Vector3(hw * 0.45f, 0f, -hl * 0.45f), PhaseOffset = 0.4f });
            }
            else if (theme == TerrainTheme.Water)
            {
                fallback.Hazards.Add(new HazardPlacementData { HazardType = ProceduralHazardType.Water, Name = "Fallback_Water", LocalPosition = Vector3.zero, LocalScale = new Vector3(1.2f, 1f, 1.2f) });
            }

            AddCoverBox(fallback, "Fallback_Cover_1", new Vector3(-2f, 0.5f, 0f), new Vector3(1.4f, 1.0f, 0.5f));
            AddCoverBox(fallback, "Fallback_Cover_2", new Vector3(2f, 0.5f, 0f), new Vector3(1.4f, 1.0f, 0.5f));

            fallback.EnemySpawns.Add(new Vector3(0f, 0f, 3.5f));
            fallback.EnemySpawns.Add(new Vector3(0f, 0f, -3.5f));
            fallback.EnemySpawns.Add(new Vector3(-3.5f, 0f, 0f));
            fallback.LootSpawns.Add(new Vector3(0f, 0.45f, 0f));

            return fallback;
        }

        #endregion
    }
}
