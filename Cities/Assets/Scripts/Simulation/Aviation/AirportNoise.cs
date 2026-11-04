using System;
using System.Collections.Generic;
using Cities.Simulation.Fields;
using Unity.Mathematics;

namespace Cities.Simulation.Aviation
{
    /// <summary>
    /// Geluidsoverlast van luchthavens (E09-09): zet een luchthaven om in <see cref="NoiseSource"/>s voor het <see cref="NoiseModel"/>.
    /// Het lawaai zit bij de baanuiteinden (opstijgen en landen), langs de baan zelf (taxiën, optrekken) en op de aanvliegroute daarvoor.
    /// Zwaardere toestellen tellen meerdere keren mee (<see cref="AviationRules.NoiseMultiplicity"/>) en een stille luchthaven geeft niets.
    /// </summary>
    public static class AirportNoise
    {
        /// <summary>Onder deze benutting maakt de luchthaven geen lawaai (geen vluchten).</summary>
        public const float MinUtilisation = 0.01f;

        /// <summary>Boven deze benutting volgen toestellen elkaar zo snel op dat er twee tegelijk op de aanvliegroute hoorbaar zijn.</summary>
        public const float BusyUtilisation = 0.5f;

        /// <summary>De bronnen van één luchthaven bij een benutting van <paramref name="utilisation"/> (0 tot 1).</summary>
        public static NoiseSource[] Sources(Airport airport, float utilisation)
        {
            if (airport == null)
            {
                throw new ArgumentNullException(nameof(airport));
            }

            if (!(utilisation >= MinUtilisation))
            {
                return Array.Empty<NoiseSource>();
            }

            var sources = new List<NoiseSource>();
            var multiplicity = AviationRules.NoiseMultiplicity(airport.LoudestClass);
            for (var i = 0; i < multiplicity; i++)
            {
                sources.Add(new NoiseSource(airport.RunwayStart, NoiseKind.Airport));
                sources.Add(new NoiseSource(airport.RunwayEnd, NoiseKind.Airport));
            }

            sources.Add(new NoiseSource(airport.Position, NoiseKind.Airport));
            var approach = new NoiseSource(airport.RunwayStart - (airport.Direction * AviationRules.ApproachDistance), NoiseKind.Airport);
            var departure = new NoiseSource(airport.RunwayEnd + (airport.Direction * AviationRules.ApproachDistance), NoiseKind.Airport);
            sources.Add(approach);
            sources.Add(departure);
            if (utilisation > BusyUtilisation)
            {
                sources.Add(approach);
                sources.Add(departure);
            }

            return sources.ToArray();
        }

        /// <summary>Afstand in meter van één bron waarbinnen het niveau boven <paramref name="decibel"/> ligt.</summary>
        public static float ContourRadius(float decibel)
        {
            return math.pow(10f, (NoiseModel.SourceLevel(NoiseKind.Airport) - decibel) / 20f);
        }
    }
}
