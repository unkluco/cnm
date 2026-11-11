using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// ScriptableObject configuration controlling procedural dungeon size, topology, and room pools.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonGenerationConfig", menuName = "LightSouls/Map Generation/Dungeon Config")]
    public class DungeonGenerationConfigSO : ScriptableObject
    {
        [Header("Seed Settings")]
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int fixedSeed = 12345;

        [Header("Layout Topology")]
        [Range(3, 40)]
        [SerializeField] private int targetRoomCount = 9;
        [Range(2, 15)]
        [Tooltip("Minimum number of rooms on the critical path from Start to Exit (inclusive).")]
        [SerializeField] private int minMainPathLength = 4;
        [Range(0f, 1f)]
        [SerializeField] private float branchProbability = 0.55f;
        [Range(0f, 1f)]
        [Tooltip("Chance to open a doorway when two placed rooms end up with touching opposite sockets.")]
        [SerializeField] private float loopConnectionChance = 0.25f;

        [Header("Collision & Solver Safety")]
        [Min(0.01f)]
        [Tooltip("Inward margin shrunk from room bounds so rooms touching cleanly at door seams do not count as overlapping.")]
        [SerializeField] private float boundsOverlapMargin = 0.05f;
        [Min(1)]
        [SerializeField] private int maxGenerationAttempts = 25;
        [Min(50)]
        [SerializeField] private int maxBacktrackSteps = 350;

        [Header("Room Template Pools")]
        [SerializeField] private List<RoomTemplateSO> startRooms = new List<RoomTemplateSO>();
        [SerializeField] private List<RoomTemplateSO> corridorRooms = new List<RoomTemplateSO>();
        [SerializeField] private List<RoomTemplateSO> normalRooms = new List<RoomTemplateSO>();
        [SerializeField] private List<RoomTemplateSO> specialBranchRooms = new List<RoomTemplateSO>();
        [SerializeField] private List<RoomTemplateSO> exitRooms = new List<RoomTemplateSO>();

        public bool UseRandomSeed => useRandomSeed;
        public int FixedSeed => fixedSeed;
        public int TargetRoomCount
        {
            get => targetRoomCount;
            set => targetRoomCount = Mathf.Clamp(value, 3, 40);
        }

        public int MinMainPathLength
        {
            get => minMainPathLength;
            set => minMainPathLength = Mathf.Clamp(value, 2, 15);
        }

        public float BranchProbability => branchProbability;
        public float LoopConnectionChance => loopConnectionChance;
        public float BoundsOverlapMargin => boundsOverlapMargin;
        public int MaxGenerationAttempts => maxGenerationAttempts;
        public int MaxBacktrackSteps => maxBacktrackSteps;
        public IReadOnlyList<RoomTemplateSO> StartRooms => startRooms;
        public IReadOnlyList<RoomTemplateSO> CorridorRooms => corridorRooms;
        public IReadOnlyList<RoomTemplateSO> NormalRooms => normalRooms;
        public IReadOnlyList<RoomTemplateSO> SpecialBranchRooms => specialBranchRooms;
        public IReadOnlyList<RoomTemplateSO> ExitRooms => exitRooms;

        public void ConfigurePools(
            List<RoomTemplateSO> starts,
            List<RoomTemplateSO> corridors,
            List<RoomTemplateSO> normals,
            List<RoomTemplateSO> specials,
            List<RoomTemplateSO> exits)
        {
            startRooms = starts ?? new List<RoomTemplateSO>();
            corridorRooms = corridors ?? new List<RoomTemplateSO>();
            normalRooms = normals ?? new List<RoomTemplateSO>();
            specialBranchRooms = specials ?? new List<RoomTemplateSO>();
            exitRooms = exits ?? new List<RoomTemplateSO>();
        }

        public void ConfigurePools(
            List<RoomTemplateSO> starts,
            List<RoomTemplateSO> normals,
            List<RoomTemplateSO> specials,
            List<RoomTemplateSO> exits)
        {
            ConfigurePools(starts, null, normals, specials, exits);
        }

        /// <summary>
        /// Converts this ScriptableObject config into a pure C# planning request and lookup map.
        /// </summary>
        public DungeonPlanRequest ToPlanRequest(out Dictionary<string, RoomTemplateSO> templateLookup)
        {
            templateLookup = new Dictionary<string, RoomTemplateSO>();
            var request = new DungeonPlanRequest
            {
                targetRoomCount = Mathf.Max(targetRoomCount, minMainPathLength),
                minMainPathLength = Mathf.Max(2, minMainPathLength),
                branchProbability = branchProbability,
                loopConnectionChance = loopConnectionChance,
                boundsOverlapMargin = boundsOverlapMargin,
                maxGenerationAttempts = maxGenerationAttempts,
                maxBacktrackSteps = maxBacktrackSteps
            };

            ConvertPool(startRooms, request.startTemplates, templateLookup);
            ConvertPool(corridorRooms, request.corridorTemplates, templateLookup);
            ConvertPool(normalRooms, request.normalTemplates, templateLookup);
            ConvertPool(specialBranchRooms, request.specialBranchTemplates, templateLookup);
            ConvertPool(exitRooms, request.exitTemplates, templateLookup);

            return request;
        }

        private static void ConvertPool(
            List<RoomTemplateSO> source,
            List<RoomTemplateData> destination,
            Dictionary<string, RoomTemplateSO> lookup)
        {
            if (source == null)
            {
                return;
            }

            foreach (RoomTemplateSO so in source)
            {
                if (so == null || so.Prefab == null)
                {
                    continue;
                }

                so.SyncFromPrefab();
                destination.Add(so.ToRuntimeData());
                lookup[so.TemplateId] = so;
            }
        }
    }
}
