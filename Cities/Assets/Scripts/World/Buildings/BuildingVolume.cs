using System;
using Cities.World.Grid;

namespace Cities.World.Buildings
{
    /// <summary>Soort bebouwing op een kavel (E05-11).</summary>
    public enum BuildingTypology
    {
        Detached,
        Terraced,
        Block,
    }

    /// <summary>
    /// Indelingsregel voor bouwblokken (E05-11): het bouwvolume van een kavel is zijn oppervlak in cellen maal een dichtheidsfactor,
    /// en het volume bepaalt de typologie. Kleine kavels worden een rij, middelgrote bij lage dichtheid een vrijstaand huis, grote
    /// kavels een blok met een gedeeld erf. Het erf is alleen visueel (gras en bomen, geen gebouw).
    /// </summary>
    public static class BuildingVolume
    {
        public const int LowDensityFactor = 1;
        public const int HighDensityFactor = 3;

        /// <summary>Vanaf dit volume is een kavel een blok.</summary>
        public const int BlockFrom = 12;

        /// <summary>Vanaf dit volume is een kavel bij lage dichtheid vrijstaand.</summary>
        public const int DetachedFrom = 4;

        /// <summary>Elke zoveel cellen van een blokkavel is erf.</summary>
        public const int CellsPerYardCell = 4;

        public static int Of(int lotCells, ZoneDensity density)
        {
            if (lotCells < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lotCells), lotCells, "Een kavel heeft minstens één cel.");
            }

            return lotCells * (density == ZoneDensity.High ? HighDensityFactor : LowDensityFactor);
        }

        public static BuildingTypology TypologyFor(int lotCells, ZoneDensity density)
        {
            var volume = Of(lotCells, density);
            if (volume >= BlockFrom)
            {
                return BuildingTypology.Block;
            }

            return density == ZoneDensity.Low && volume >= DetachedFrom ? BuildingTypology.Detached : BuildingTypology.Terraced;
        }

        /// <summary>Het aantal cellen gedeeld erf: alleen blokken hebben er een.</summary>
        public static int YardCells(int lotCells, ZoneDensity density) =>
            TypologyFor(lotCells, density) == BuildingTypology.Block ? lotCells / CellsPerYardCell : 0;
    }
}
