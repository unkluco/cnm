namespace LightSouls.MapGeneration
{
    public enum TerrainTheme
    {
        Lava = 0,
        Spike = 1,
        Water = 2,
        LowCover = 3
    }

    public enum LavaPatternType
    {
        PerimeterRiver = 0,
        SplitIslands = 1,
        DiagonalChannel = 2,
        CrescentHazard = 3,
        ScatteredPools = 4,
        MultipleCrossings = 5
    }

    public enum SpikePatternType
    {
        ParallelStrips = 0,
        TimedCrossroads = 1,
        PressurePockets = 2,
        StaggeredLanes = 3
    }

    public enum WaterPatternType
    {
        CentralBasin = 0,
        BranchingStreams = 1,
        WaterPatches = 2,
        FloodedRoute = 3
    }

    public enum LowCoverPatternType
    {
        StaggeredWalls = 0,
        PillarField = 1,
        RuinedStructures = 2,
        CentralCoverCluster = 3
    }
}
