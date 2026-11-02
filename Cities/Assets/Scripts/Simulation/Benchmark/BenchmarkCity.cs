using System;
using System.Collections.Generic;
using Cities.Core.Randomness;

namespace Cities.Simulation.Benchmark
{
    /// <summary>
    /// Generator voor de vaste teststeden (E23-02). Een inwoner is een huishouden uit een woongebouw maal <see cref="HouseholdSize"/>. De stad
    /// is niets meer dan de keuze van een woonsjabloon per gebouw: dezelfde seed en dezelfde sjablonen geven altijd dezelfde stad, zodat
    /// metingen vergelijkbaar blijven.
    /// </summary>
    public static class BenchmarkCity
    {
        /// <summary>Gemiddelde gezinsgrootte; een schatting rond het Nederlandse gemiddelde.</summary>
        public const float HouseholdSize = 2.4f;

        /// <summary>Vaste teststeden in inwoners.</summary>
        public static readonly int[] Sizes = { 10_000, 100_000, 500_000 };

        /// <summary>
        /// Kiest per gebouw een sjabloon tot de stad minstens <paramref name="population"/> inwoners heeft. <paramref name="householdsPerTemplate"/>
        /// zijn de huishoudens van de beschikbare woonsjablonen; het resultaat is de sjabloonindex per gebouw.
        /// </summary>
        public static int[] Generate(int population, ulong seed, IReadOnlyList<int> householdsPerTemplate)
        {
            if (population <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Een teststad heeft inwoners nodig.");
            }

            if (householdsPerTemplate is null || householdsPerTemplate.Count == 0)
            {
                throw new ArgumentException("Geen woonsjablonen.", nameof(householdsPerTemplate));
            }

            foreach (var households in householdsPerTemplate)
            {
                if (households <= 0)
                {
                    throw new ArgumentException("Elk woonsjabloon heeft huishoudens nodig.", nameof(householdsPerTemplate));
                }
            }

            var buildings = new List<int>();
            var inhabitants = 0f;
            while (inhabitants < population)
            {
                var template = (int)(SeedHash.Combine(seed, (ulong)buildings.Count) % (ulong)householdsPerTemplate.Count);
                buildings.Add(template);
                inhabitants += householdsPerTemplate[template] * HouseholdSize;
            }

            return buildings.ToArray();
        }

        /// <summary>Inwoners van een gegenereerde stad.</summary>
        public static long Population(IReadOnlyList<int> buildings, IReadOnlyList<int> householdsPerTemplate)
        {
            long households = 0;
            foreach (var template in buildings)
            {
                households += householdsPerTemplate[template];
            }

            return (long)(households * HouseholdSize);
        }
    }
}
