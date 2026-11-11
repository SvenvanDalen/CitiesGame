using System;
using Unity.Mathematics;

namespace Cities.Simulation.Aviation
{
    /// <summary>
    /// Luchthavens op maat (E25-08): meerdere terminals, cargo en vliegroutes met geluidsbeheer. Capaciteit volgt uit terminals en
    /// start- en landingsbanen; geluidsoverlast uit de vluchten, gedempt door nachtregels en geluidswallen.
    /// </summary>
    public sealed class AirportComplex
    {
        public const int PassengersPerTerminalPerDay = 6_000;

        public const int FlightsPerRunwayPerDay = 300;

        public int Terminals { get; set; } = 1;

        public int Runways { get; set; } = 1;

        public int CargoHalls { get; set; }

        public bool NightBan { get; set; }

        public int NoiseBarriers { get; set; }

        public int FlightsPerDay => Runways * FlightsPerRunwayPerDay;

        public int PassengerCapacity => Math.Min(Terminals * PassengersPerTerminalPerDay, FlightsPerDay * 150);

        public int CargoTonsPerDay => Math.Min(CargoHalls * 250, FlightsPerDay * 2);

        /// <summary>Geluidsoverlast (0 tot 1) voor de omgeving, uit de vluchten die echt vliegen.</summary>
        public float Noise(int flights)
        {
            if (flights < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flights), flights, "Aantal vluchten is niet negatief.");
            }

            var raw = math.saturate(flights / 600f);
            var night = NightBan ? 0.75f : 1f;
            var barrier = math.max(0.4f, 1f - (NoiseBarriers * 0.15f));
            return raw * night * barrier;
        }
    }
}
