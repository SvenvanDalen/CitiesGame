using System;
using System.Collections.Generic;

namespace Cities.Simulation
{
    public enum ExtraTransitMode : byte
    {
        Monorail,
        CableCar,
        Airship,
        Taxi,
        FerryRoute,
    }

    /// <summary>Kenmerken van een extra vervoersmodus (E09-13, E25-02).</summary>
    public sealed class ExtraTransitSpec
    {
        internal ExtraTransitSpec(string name, float speed, int capacity, long buildPerKm, long upkeepPerKmMonth, bool needsHub)
        {
            Name = name;
            Speed = speed;
            Capacity = capacity;
            BuildPerKm = buildPerKm;
            UpkeepPerKmMonth = upkeepPerKmMonth;
            NeedsHub = needsHub;
        }

        public string Name { get; }

        /// <summary>Snelheid in meter per seconde.</summary>
        public float Speed { get; }

        /// <summary>Passagiers per voertuig.</summary>
        public int Capacity { get; }

        public long BuildPerKm { get; }

        public long UpkeepPerKmMonth { get; }

        /// <summary>Vraagt een knooppunt (halte met overstap) aan het begin en einde.</summary>
        public bool NeedsHub { get; }

        /// <summary>Passagiers per uur voor een lijn met <paramref name="vehicles"/> voertuigen over <paramref name="lengthMeters"/>.</summary>
        public int PassengersPerHour(int vehicles, float lengthMeters)
        {
            if (vehicles < 0 || !(lengthMeters > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(vehicles), "Een lijn heeft een lengte en geen negatief aantal voertuigen.");
            }

            var roundTrip = (2f * lengthMeters / Speed) + 60f;
            return (int)(vehicles * Capacity * (3600f / roundTrip));
        }

        public long BuildCost(float lengthMeters) => (long)(BuildPerKm * lengthMeters / 1000f);

        public long UpkeepPerMonth(float lengthMeters) => (long)(UpkeepPerKmMonth * lengthMeters / 1000f);
    }

    /// <summary>De vaste tabel van extra vervoersmodi.</summary>
    public static class ExtraTransitModes
    {
        private static readonly Dictionary<ExtraTransitMode, ExtraTransitSpec> Specs = new Dictionary<ExtraTransitMode, ExtraTransitSpec>
        {
            { ExtraTransitMode.Monorail, new ExtraTransitSpec("Monorail", 25f, 120, 900_000, 9_000, true) },
            { ExtraTransitMode.CableCar, new ExtraTransitSpec("Kabelbaan", 6f, 30, 400_000, 4_000, true) },
            { ExtraTransitMode.Airship, new ExtraTransitSpec("Luchtschip", 15f, 60, 150_000, 6_000, true) },
            { ExtraTransitMode.Taxi, new ExtraTransitSpec("Taxi", 12f, 3, 0, 300, false) },
            { ExtraTransitMode.FerryRoute, new ExtraTransitSpec("Veerboot", 8f, 80, 200_000, 3_000, true) },
        };

        public static ExtraTransitSpec Of(ExtraTransitMode mode) =>
            Specs.TryGetValue(mode, out var spec) ? spec : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Onbekende vervoersmodus.");
    }
}
