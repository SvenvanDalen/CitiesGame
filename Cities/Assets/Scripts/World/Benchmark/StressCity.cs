using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Cities.Simulation.Benchmark;
using Cities.World.Grid;

namespace Cities.World.Benchmark
{
    /// <summary>Eén bouwkavel van de stresstest-stad: linkeronderhoek in cellen, zone en (bij wonen) het woonsjabloon.</summary>
    public readonly struct StressLot
    {
        public StressLot(int x, int z, ZoneType zone, int template)
        {
            X = x;
            Z = z;
            Zone = zone;
            Template = template;
        }

        public int X { get; }

        public int Z { get; }

        public ZoneType Zone { get; }

        /// <summary>Index in de woonsjablonen; -1 voor niet-woonkavels.</summary>
        public int Template { get; }
    }

    /// <summary>Uitkomst van <see cref="StressCity.Generate"/>.</summary>
    public sealed class StressCityPlan
    {
        public StressCityPlan(IReadOnlyList<StressLot> lots, long population)
        {
            Lots = lots;
            Population = population;
        }

        public IReadOnlyList<StressLot> Lots { get; }

        public long Population { get; }
    }

    /// <summary>
    /// Genereert een grote stad voor benchmarks en regressietests (E24-06): een raster van blokken met straatcellen ertussen, per blok
    /// één zonetype, per blok vier kavels. Woonkavels krijgen een sjabloon tot <c>population</c> inwoners bereikt is (zie
    /// <see cref="BenchmarkCity.HouseholdSize"/>); de zones worden in de <see cref="WorldMap"/> geschilderd. Dezelfde seed geeft dezelfde stad.
    /// </summary>
    public static class StressCity
    {
        /// <summary>Zijde van een blok in cellen.</summary>
        public const int BlockCells = 6;

        /// <summary>Zijde van een kavel in cellen; een blok heeft 2×2 kavels.</summary>
        public const int LotCells = BlockCells / 2;

        /// <summary>Afstand van blok tot blok: het blok plus één straatcel.</summary>
        public const int Pitch = BlockCells + 1;

        /// <summary>Verdeling van de blokken over de zonetypen, in procenten: wonen, handel, industrie.</summary>
        private const int ResidentialPercent = 60;
        private const int CommercialPercent = 75;
        private const int IndustrialPercent = 90;

        public static StressCityPlan Generate(WorldDimensions dimensions, int population, ulong seed, IReadOnlyList<int> householdsPerTemplate)
        {
            if (population <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Een stresstest-stad heeft inwoners nodig.");
            }

            if (householdsPerTemplate is null || householdsPerTemplate.Count == 0)
            {
                throw new ArgumentException("Geen woonsjablonen.", nameof(householdsPerTemplate));
            }

            var blocksX = dimensions.CellsX / Pitch;
            var blocksZ = dimensions.CellsZ / Pitch;
            var lots = new List<StressLot>();
            var households = 0L;
            for (var index = 0; index < blocksX * blocksZ && Inhabitants(households) < population; index++)
            {
                var zone = ZoneOf(SeedHash.Combine(seed, (ulong)index));
                var originX = (index % blocksX) * Pitch;
                var originZ = (index / blocksX) * Pitch;
                for (var lot = 0; lot < 4; lot++)
                {
                    var template = -1;
                    if (zone == ZoneType.Residential)
                    {
                        template = (int)(SeedHash.Combine(seed, (ulong)index, (ulong)lot) % (ulong)householdsPerTemplate.Count);
                        households += householdsPerTemplate[template];
                    }

                    lots.Add(new StressLot(originX + ((lot % 2) * LotCells), originZ + ((lot / 2) * LotCells), zone, template));
                }
            }

            if (Inhabitants(households) < population)
            {
                throw new InvalidOperationException("De kaart is te klein voor deze stad.");
            }

            return new StressCityPlan(lots, Inhabitants(households));
        }

        /// <summary>Schildert de zones van <paramref name="plan"/> in de kaart (alleen kavels; straten blijven leeg).</summary>
        public static void Apply(WorldMap map, StressCityPlan plan)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (plan is null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            foreach (var lot in plan.Lots)
            {
                var cell = new ZoneCell(lot.Zone, ZoneDensity.Low);
                for (var z = 0; z < LotCells; z++)
                {
                    for (var x = 0; x < LotCells; x++)
                    {
                        map.Zones[lot.X + x, lot.Z + z] = cell;
                    }
                }
            }
        }

        private static long Inhabitants(long households) => (long)(households * BenchmarkCity.HouseholdSize);

        private static ZoneType ZoneOf(ulong hash)
        {
            var roll = (int)(hash % 100UL);
            if (roll < ResidentialPercent)
            {
                return ZoneType.Residential;
            }

            if (roll < CommercialPercent)
            {
                return ZoneType.Commercial;
            }

            return roll < IndustrialPercent ? ZoneType.Industrial : ZoneType.Office;
        }
    }
}
