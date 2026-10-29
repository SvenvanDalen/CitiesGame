using System;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>Oppervlaktehoogte uit de lagen van de <see cref="WorldMap"/>: terrein plus waterdiepte, bilineair bemonsterd.</summary>
    public sealed class WorldMapTerrain : ITerrainHeight
    {
        private readonly WorldMap _map;

        public WorldMapTerrain(WorldMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        public float SurfaceHeight(float2 position)
        {
            var p = new Vector2(position.x, position.y);
            return _map.Height.Sample(p) + _map.WaterDepth.Sample(p);
        }
    }
}
