using System;
using Cities.Simulation.Fields;
using Cities.World.Grid;

namespace Cities.Gameplay.Environment
{
    /// <summary>
    /// Verbindt <see cref="SoilPollution"/> (E12-05, de bron) met de wereldlaag <see cref="WorldMap.GroundPollution"/> (E03-10):
    /// de laag bevat het niveau van 0 tot 1, dat overlays, grondwaarde en opslaan (WMAP) lezen. Beide hebben één waarde per grof blok.
    /// </summary>
    public static class GroundPollutionLayer
    {
        /// <summary>Schrijft het vervuilingsniveau van elke cel naar de wereldlaag.</summary>
        public static void Publish(SoilPollution soil, WorldMap map)
        {
            Check(soil, map);
            var layer = map.GroundPollution;
            for (var z = 0; z < soil.Height; z++)
            {
                for (var x = 0; x < soil.Width; x++)
                {
                    layer[x, z] = SoilPollution.Level(soil[x, z]);
                }
            }
        }

        /// <summary>Zet de concentraties terug uit de wereldlaag, bijvoorbeeld na het laden van een opslag.</summary>
        public static void Restore(SoilPollution soil, WorldMap map)
        {
            Check(soil, map);
            var layer = map.GroundPollution;
            for (var z = 0; z < soil.Height; z++)
            {
                for (var x = 0; x < soil.Width; x++)
                {
                    soil.SetConcentration(x, z, SoilPollution.Concentration(layer[x, z]));
                }
            }
        }

        private static void Check(SoilPollution soil, WorldMap map)
        {
            if (soil is null)
            {
                throw new ArgumentNullException(nameof(soil));
            }

            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var layout = map.GroundPollution.Layout;
            if (soil.Width != layout.Width || soil.Height != layout.Depth)
            {
                throw new ArgumentException($"De grondvervuiling ({soil.Width}x{soil.Height}) past niet op de wereldlaag ({layout.Width}x{layout.Depth}).", nameof(soil));
            }
        }
    }
}
