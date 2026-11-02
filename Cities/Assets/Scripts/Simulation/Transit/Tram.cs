using System;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>Waar een tramrail ligt: in het wegdek tussen het verkeer of op een eigen baan naast de weg.</summary>
    public enum TrackPlacement
    {
        OnRoad,
        BesideRoad,
    }

    /// <summary>
    /// De regels van de tram (E09-05): rails op of naast de weg, tramhaltes en voorrang op kruispunten. Een tram is sneller en groter dan een bus,
    /// maar op de weg zit hij vast achter het verkeer; op een eigen baan rijdt hij vrij. Halteafstand en voorrang zijn hier als rekenregels
    /// vastgelegd. shortcut: de tram rijdt nog niet met eigen snelheid en capaciteit in <see cref="TransitFleet"/>; dat volgt zodra
    /// voertuigsoorten per lijn bestaan.
    /// </summary>
    public static class Tram
    {
        /// <summary>Topsnelheid in m/s (60 km/u).</summary>
        public const float TopSpeed = 16.7f;

        public const int Capacity = 200;

        /// <summary>Minimale afstand tussen twee tramhaltes in meter; dichter bij elkaar maakt de tram trager dan lopen.</summary>
        public const float MinStopSpacing = 300f;

        /// <summary>Binnen deze afstand tot een kruispunt moet het wegverkeer voor een naderende tram wijken.</summary>
        public const float PriorityDistance = 40f;

        /// <summary>Snelheid van de tram: op de weg nooit sneller dan het verkeer, op een eigen baan de topsnelheid.</summary>
        public static float SpeedOn(TrackPlacement placement, float trafficSpeed)
        {
            if (!(trafficSpeed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(trafficSpeed), trafficSpeed, "De verkeerssnelheid kan niet negatief zijn.");
            }

            return placement == TrackPlacement.BesideRoad ? TopSpeed : math.min(TopSpeed, trafficSpeed);
        }

        public static bool IsStopSpacingOk(float2 a, float2 b) => math.distance(a, b) >= MinStopSpacing;

        /// <summary>
        /// Of het wegverkeer moet wachten: een tram heeft voorrang als hij op het kruispunt is of er binnen <see cref="PriorityDistance"/>
        /// naartoe rijdt. De tram zelf wacht nooit op auto's.
        /// </summary>
        public static bool RoadMustYield(float tramDistanceToJunction, bool tramApproaching)
        {
            if (!(tramDistanceToJunction >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(tramDistanceToJunction), tramDistanceToJunction, "De afstand kan niet negatief zijn.");
            }

            return tramDistanceToJunction == 0f || (tramApproaching && tramDistanceToJunction <= PriorityDistance);
        }
    }
}
