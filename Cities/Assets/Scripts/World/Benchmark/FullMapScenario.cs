using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.Simulation.Benchmark;
using Cities.World.Grid;

namespace Cities.World.Benchmark
{
    /// <summary>Uitkomst van <see cref="FullMapScenario.Plan"/>: wat de volledige kaart bij een bevolkingsdoel vraagt.</summary>
    public sealed class FullMapReport
    {
        public FullMapReport(StressCityPlan plan, int[] lotsPerTile, int tilesUsed, long mapBytes, long maxPopulation, PerformanceBudget budget)
        {
            Plan = plan;
            LotsPerTile = lotsPerTile;
            TilesUsed = tilesUsed;
            MapBytes = mapBytes;
            MaxPopulation = maxPopulation;
            Budget = budget;
        }

        public StressCityPlan Plan { get; }

        /// <summary>Aantal kavels per tegel, rij voor rij (index = z * tegels in X + x).</summary>
        public int[] LotsPerTile { get; }

        /// <summary>Tegels met minstens één kavel.</summary>
        public int TilesUsed { get; }

        /// <summary>Geheugen van alle kaartlagen samen.</summary>
        public long MapBytes { get; }

        /// <summary>Hoogste bevolking die dit raster met deze sjablonen kan bevatten.</summary>
        public long MaxPopulation { get; }

        /// <summary>Het budget voor het bevolkingsdoel (zie <c>docs/PERFORMANCE.md</c>).</summary>
        public PerformanceBudget Budget { get; }

        public double MapMegabytes => MapBytes / (1024.0 * 1024.0);

        /// <summary>De kaartlagen alleen mogen niet meer dan de helft van het geheugenbudget gebruiken.</summary>
        public bool MapFitsBudget => MapMegabytes <= Budget.MemoryMb / 2.0;
    }

    /// <summary>
    /// Het stretchdoel van E23-10: de hele kaart van 9×9 tegels bebouwd met 1 miljoen inwoners. Dit rekent uit of dat past
    /// (kavels, spreiding over de tegels, geheugen van de lagen) zonder de stad echt te bouwen; de echte meting draait
    /// op de benchmark-scene met dit plan.
    /// </summary>
    public static class FullMapScenario
    {
        public const int TargetPopulation = 1_000_000;

        public static FullMapReport Plan(WorldDimensions dimensions, IReadOnlyList<int> householdsPerTemplate, ulong seed = 1, int population = TargetPopulation)
        {
            var plan = StressCity.Generate(dimensions, population, seed, householdsPerTemplate);
            var lotsPerTile = new int[dimensions.TileCount];
            var tilesUsed = 0;
            foreach (var lot in plan.Lots)
            {
                var tile = dimensions.CellToTile(new CellCoord(lot.X, lot.Z));
                var index = (tile.Z * dimensions.TilesX) + tile.X;
                if (lotsPerTile[index]++ == 0)
                {
                    tilesUsed++;
                }
            }

            return new FullMapReport(
                plan,
                lotsPerTile,
                tilesUsed,
                LayerBytes(dimensions),
                MaxPopulation(dimensions, householdsPerTemplate),
                PerformanceBudgets.For(population));
        }

        /// <summary>Geheugen van de kaartlagen van <see cref="WorldMap"/> zonder de kaart aan te maken.</summary>
        public static long LayerBytes(WorldDimensions dimensions)
        {
            // Hoekpunten (cellen + 1) voor hoogte en water, grove monsters voor vervuiling en grondwaarde, cellen voor de rest.
            var corners = (long)(dimensions.CellsX + 1) * (dimensions.CellsZ + 1);
            var cells = (long)dimensions.CellCount;
            var coarse = (long)(dimensions.CellsX / WorldMap.CoarseCellsPerSample) * (dimensions.CellsZ / WorldMap.CoarseCellsPerSample);
            return (corners * sizeof(float) * 2) + (cells * 2) + (coarse * sizeof(float) * 2) + cells + cells;
        }

        /// <summary>Bovengrens: alle blokken woonblokken met het grootste sjabloon.</summary>
        public static long MaxPopulation(WorldDimensions dimensions, IReadOnlyList<int> householdsPerTemplate)
        {
            var largest = 0;
            foreach (var households in householdsPerTemplate)
            {
                largest = Math.Max(largest, households);
            }

            var blocks = (long)(dimensions.CellsX / StressCity.Pitch) * (dimensions.CellsZ / StressCity.Pitch);
            return (long)(blocks * 4L * largest * BenchmarkCity.HouseholdSize);
        }
    }
}
