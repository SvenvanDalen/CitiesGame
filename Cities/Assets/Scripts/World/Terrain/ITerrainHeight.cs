using Unity.Mathematics;

namespace Cities.World.Terrain
{
    /// <summary>Hoogte van het oppervlak (terrein of water, wat hoger is) op een positie in het grondvlak.</summary>
    public interface ITerrainHeight
    {
        /// <param name="position">Wereld-X en -Z in meters.</param>
        float SurfaceHeight(float2 position);
    }

    /// <summary>Vlak oppervlak op een vaste hoogte; voor tests en scenes zonder terrein.</summary>
    public sealed class FlatTerrain : ITerrainHeight
    {
        public static readonly FlatTerrain Zero = new FlatTerrain(0f);

        public FlatTerrain(float height) => Height = height;

        public float Height { get; }

        public float SurfaceHeight(float2 position) => Height;
    }
}
