using System;
using Unity.Mathematics;

namespace Cities.Simulation.Attractions
{
    public enum ParkKind : byte
    {
        Zoo,
        ThemePark,
        NatureReserve,
    }

    /// <summary>
    /// Parken en attracties (E25-04): bezoekers per dag uit de soort, de bereikbaarheid (0 tot 1) en het weer (0 tot 1), met entree als
    /// inkomsten. Een natuurreservaat is gratis maar verhoogt het geluk via wandelroutes.
    /// </summary>
    public sealed class ParkAttraction
    {
        public ParkAttraction(ParkKind kind, int walkingRouteMeters = 0)
        {
            if (walkingRouteMeters < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(walkingRouteMeters), walkingRouteMeters, "Een route heeft geen negatieve lengte.");
            }

            Kind = kind;
            WalkingRouteMeters = walkingRouteMeters;
        }

        public ParkKind Kind { get; }

        public int WalkingRouteMeters { get; }

        public int Capacity => Kind switch
        {
            ParkKind.Zoo => 1500,
            ParkKind.ThemePark => 4000,
            _ => 600,
        };

        public int TicketPrice => Kind switch
        {
            ParkKind.Zoo => 25,
            ParkKind.ThemePark => 60,
            _ => 0,
        };

        public int VisitorsPerDay(float accessibility, float weather) =>
            (int)(Capacity * math.saturate(accessibility) * (0.4f + (0.6f * math.saturate(weather))));

        public long IncomePerDay(float accessibility, float weather) => (long)VisitorsPerDay(accessibility, weather) * TicketPrice;

        /// <summary>Geluksbonus (0 tot 0.1) van wandelroutes, 1 km geeft 2 procentpunt.</summary>
        public float HappinessBonus => math.min(0.1f, WalkingRouteMeters / 50_000f);
    }
}
