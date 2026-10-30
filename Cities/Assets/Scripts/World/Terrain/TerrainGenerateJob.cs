using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cities.World.Terrain
{
    /// <summary>Vult een hoogtelaag (hoekpunten) met <see cref="TerrainShape"/>, één rij per werkeenheid.</summary>
    [BurstCompile]
    public struct TerrainGenerateJob : IJobParallelFor
    {
        public TerrainShapeSettings Settings;
        public int Width;
        public float Spacing;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<float> Heights;

        public void Execute(int row)
        {
            for (var x = 0; x < Width; x++)
            {
                Heights[(row * Width) + x] = TerrainShape.Height(new float2(x, row) * Spacing, Settings);
            }
        }
    }
}
