using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cities.World.Terrain
{
    /// <summary>Vult de hoogtekaart met het landschap van <see cref="TerrainShape"/>.</summary>
    public static class TerrainGenerator
    {
        public static void Generate(Grid.WorldMap map, in TerrainShapeSettings settings)
        {
            var layout = map.Height.Layout;
            new TerrainGenerateJob { Settings = settings, Width = layout.Width, Spacing = layout.Spacing, Heights = map.Height.Values }
                .Schedule(layout.Depth, 8).Complete();
            map.NotifyHeightChanged(map.AllCorners);
        }
    }

    /// <summary>Vult een hoogtelaag (hoekpunten) met <see cref="TerrainShape"/>, één rij per werkeenheid.</summary>
    [BurstCompile]
    internal struct TerrainGenerateJob : IJobParallelFor
    {
        public TerrainShapeSettings Settings;
        public int Width;
        public float Spacing;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<float> Heights;

        public void Execute(int index)
        {
            var row = index;
            for (var x = 0; x < Width; x++)
            {
                Heights[(row * Width) + x] = TerrainShape.Height(new float2(x, row) * Spacing, Settings);
            }
        }
    }
}
