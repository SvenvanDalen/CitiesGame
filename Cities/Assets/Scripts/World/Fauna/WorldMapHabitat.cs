using System;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Fauna
{
    /// <summary>De habitat uit de lagen van de <see cref="WorldMap"/>.</summary>
    public sealed class WorldMapHabitat : IFaunaHabitat
    {
        /// <summary>Boven deze bosdichtheid (van 255) is het geen open land meer.</summary>
        public const byte MaxOpenTreeDensity = 20;

        /// <summary>Steilste helling (hoogteverschil per meter) waarop een ree graast.</summary>
        public const float MaxSlope = 0.35f;

        private readonly WorldMap _map;

        public WorldMapHabitat(WorldMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        public bool InBounds(float2 position) => _map.Dimensions.Contains(new Vector2(position.x, position.y));

        public float GroundHeight(float2 position) => _map.Height.Sample(new Vector2(position.x, position.y));

        public float WaterDepth(float2 position) => _map.WaterDepth.Sample(new Vector2(position.x, position.y));

        public bool IsOpenLand(float2 position)
        {
            var point = new Vector2(position.x, position.y);
            if (!_map.Dimensions.Contains(point))
            {
                return false;
            }

            var cell = _map.Dimensions.WorldToCell(point);
            if (_map.GroundCover.GetForCell(cell) == GroundCover.Built || _map.Trees.GetForCell(cell) > MaxOpenTreeDensity)
            {
                return false;
            }

            var east = _map.Height.Sample(point + Vector2.right);
            var north = _map.Height.Sample(point + Vector2.up);
            var here = _map.Height.Sample(point);
            return Math.Max(Math.Abs(east - here), Math.Abs(north - here)) <= MaxSlope;
        }
    }
}
