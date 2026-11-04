using System;
using Cities.Simulation.Fields;
using Cities.World.Grid;

namespace Cities.Gameplay.Pollution
{
    /// <summary>
    /// Verbindt de bosdichtheid van <see cref="WorldMap.Trees"/> met de put <see cref="ForestSink"/> (E12-09): rekent de bomen om naar een
    /// bedekkingskaart op het raster van een milieulaag. Bos kappen of planten werkt zo vanzelf door in de vervuiling.
    /// </summary>
    public static class ForestCoverLayer
    {
        /// <summary>Bedekking (0 tot 1) per cel van een milieulaag van <paramref name="width"/> x <paramref name="height"/>.</summary>
        public static float[] Build(WorldMap map, int width, int height)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var layout = map.Trees.Layout;
            return ForestSink.CoverMap(map.Trees.Values.ToArray(), layout.Width, layout.Depth, width, height);
        }
    }
}
