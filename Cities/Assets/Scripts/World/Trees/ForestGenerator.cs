using Cities.World.Grid;
using Cities.World.Terrain;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cities.World.Trees
{
    /// <summary>Vult de bosdichtheid van de hele kaart met <see cref="ForestShape"/>; draai dit na het terrein.</summary>
    public static class ForestGenerator
    {
        public static void Generate(WorldMap map, in ForestSettings settings)
        {
            var dimensions = map.Dimensions;
            new ForestGenerateJob
            {
                Settings = settings,
                Heights = map.Height.Values,
                Water = map.WaterDepth.Values,
                Cover = map.GroundCover.Values,
                CornersWidth = map.Height.Layout.Width,
                CellsWidth = dimensions.CellsX,
                CellSize = dimensions.CellSize,
                Trees = map.Trees.Values,
            }.Schedule(dimensions.CellsZ, 16).Complete();
            map.NotifyTreesChanged(map.AllCells);
        }
    }

    /// <summary>Eén rij cellen per werkeenheid.</summary>
    [BurstCompile]
    internal struct ForestGenerateJob : IJobParallelFor
    {
        public ForestSettings Settings;
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<float> Water;
        [ReadOnly] public NativeArray<GroundCover> Cover;
        public int CornersWidth;
        public int CellsWidth;
        public float CellSize;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<byte> Trees;

        public void Execute(int index)
        {
            var z = index;
            for (var x = 0; x < CellsWidth; x++)
            {
                var c00 = (z * CornersWidth) + x;
                var c01 = c00 + CornersWidth;
                var h00 = Heights[c00];
                var h10 = Heights[c00 + 1];
                var h01 = Heights[c01];
                var h11 = Heights[c01 + 1];
                var water = math.max(math.max(Water[c00], Water[c00 + 1]), math.max(Water[c01], Water[c01 + 1]));
                var cell = (z * CellsWidth) + x;
                Trees[cell] = ForestShape.Density(
                    (new float2(x, z) + 0.5f) * CellSize,
                    (h00 + h10 + h01 + h11) * 0.25f,
                    TerrainSplat.SlopeDegrees(h00, h10, h01, h11, CellSize),
                    water,
                    Cover[cell],
                    Settings);
            }
        }
    }
}
