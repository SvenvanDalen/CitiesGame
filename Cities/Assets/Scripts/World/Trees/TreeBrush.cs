using System;
using Cities.World.Grid;
using Cities.World.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Trees
{
    /// <summary>Bomen planten of kappen onder een rond penseel, door de bosdichtheid per cel te veranderen.</summary>
    public static class TreeBrush
    {
        /// <summary>Past het penseel toe en geeft de gewijzigde cellen (max exclusief), of een lege rect.</summary>
        /// <param name="plant"><c>true</c> om te planten, <c>false</c> om te kappen.</param>
        /// <param name="step">Dichtheid (0 tot 255) die het midden deze keer hoogstens verandert.</param>
        public static RectInt Apply(WorldMap map, bool plant, float2 center, float radius, float step)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var spacing = map.Dimensions.CellSize;
            var min = math.max((int2)math.floor((center - radius) / spacing), 0);
            var max = math.min((int2)math.ceil((center + radius) / spacing), new int2(map.Dimensions.CellsX, map.Dimensions.CellsZ));
            if (radius <= 0f || step <= 0f || math.any(max <= min))
            {
                return default;
            }

            ref var trees = ref map.Trees;
            for (var z = min.y; z < max.y; z++)
            {
                for (var x = min.x; x < max.x; x++)
                {
                    var amount = step * TerrainBrush.Falloff(math.distance((new float2(x, z) + 0.5f) * spacing, center), radius);

                    // Op bebouwde grond en akkers wordt niet geplant.
                    if (amount <= 0f || (plant && map.GroundCover[x, z] != GroundCover.Natural))
                    {
                        continue;
                    }

                    trees[x, z] = (byte)math.clamp(math.round(trees[x, z] + (plant ? amount : -amount)), 0f, 255f);
                }
            }

            return new RectInt(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>Maakt cellen bebouwd en kapt de bomen erop, bijvoorbeeld onder een nieuw gebouw.</summary>
        public static void ClearForBuilding(WorldMap map, RectInt cells)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            cells.ClampToBounds(map.AllCells);
            for (var z = cells.yMin; z < cells.yMax; z++)
            {
                for (var x = cells.xMin; x < cells.xMax; x++)
                {
                    map.Trees[x, z] = 0;
                    map.GroundCover[x, z] = GroundCover.Built;
                }
            }

            map.NotifyTreesChanged(cells);
            map.NotifyGroundCoverChanged(cells);
        }
    }
}
