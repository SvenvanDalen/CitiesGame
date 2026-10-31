using System;
using System.Collections.Generic;
using System.Linq;
using Cities.World.Grid;
using Unity.Mathematics;

namespace Cities.Gameplay.Zoning
{
    /// <summary>De vormen waarmee de <see cref="ZoneTool"/> cellen kiest: penseel, rechthoek en vullen.</summary>
    public static class ZoneShapes
    {
        /// <summary>Cellen waarvan het midden binnen <paramref name="radius"/> meter van <paramref name="centre"/> (x, z) ligt.</summary>
        public static List<CellCoord> Disc(float2 centre, float radius, float cellSize)
        {
            var cells = new List<CellCoord>();
            var minX = (int)math.floor((centre.x - radius) / cellSize);
            var maxX = (int)math.floor((centre.x + radius) / cellSize);
            var minZ = (int)math.floor((centre.y - radius) / cellSize);
            var maxZ = (int)math.floor((centre.y + radius) / cellSize);
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var middle = new float2((x + 0.5f) * cellSize, (z + 0.5f) * cellSize);
                    if (math.distancesq(middle, centre) <= radius * radius)
                    {
                        cells.Add(new CellCoord(x, z));
                    }
                }
            }

            return cells;
        }

        /// <summary>Alle cellen van de rechthoek tussen twee hoekcellen, die er allebei bij horen.</summary>
        public static List<CellCoord> Rectangle(CellCoord a, CellCoord b)
        {
            var cells = new List<CellCoord>();
            for (var z = Math.Min(a.Z, b.Z); z <= Math.Max(a.Z, b.Z); z++)
            {
                for (var x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
                {
                    cells.Add(new CellCoord(x, z));
                }
            }

            return cells;
        }

        /// <summary>
        /// De cellen die vanaf <paramref name="start"/> aaneengesloten (via de vier buren) aan <paramref name="include"/> voldoen,
        /// hoogstens <paramref name="limit"/>; leeg als de startcel zelf niet voldoet.
        /// </summary>
        public static List<CellCoord> Fill(CellCoord start, Func<CellCoord, bool> include, int limit)
        {
            var cells = new List<CellCoord>();
            if (!include(start))
            {
                return cells;
            }

            var seen = new HashSet<CellCoord> { start };
            var queue = new Queue<CellCoord>();
            queue.Enqueue(start);
            while (queue.Count > 0 && cells.Count < limit)
            {
                var cell = queue.Dequeue();
                cells.Add(cell);
                foreach (var next in new[] { cell.North, cell.East, cell.South, cell.West }.Where(n => seen.Add(n) && include(n)))
                {
                    queue.Enqueue(next);
                }
            }

            return cells;
        }
    }
}
