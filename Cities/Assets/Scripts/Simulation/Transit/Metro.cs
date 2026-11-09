using System;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// De regels van de metro (E09-06): stations met diepte en tunnels onder het terrein. Een metro rijdt op een eigen, ondergrondse baan
    /// en heeft nooit last van het verkeer, maar een station kost reistijd om te bereiken (trap en roltrap) en een tunnel mag niet te steil
    /// zijn. shortcut: de metro rijdt nog niet met eigen snelheid en capaciteit in <see cref="TransitFleet"/>; dat volgt zodra voertuigsoorten per
    /// lijn bestaan (zie ook <see cref="Tram"/>).
    /// </summary>
    public static class Metro
    {
        /// <summary>Topsnelheid in m/s (80 km/u).</summary>
        public const float TopSpeed = 22.2f;

        /// <summary>Optrekken en remmen in m/s².</summary>
        public const float Acceleration = 1f;

        public const int Capacity = 800;

        /// <summary>Minimale afstand tussen twee metrostations in meter.</summary>
        public const float MinStationSpacing = 600f;

        /// <summary>Ondiepste ligging van een perron in meter onder het maaiveld: ruim onder leidingen en funderingen.</summary>
        public const float MinDepth = 6f;

        /// <summary>Diepste ligging van een perron in meter onder het maaiveld.</summary>
        public const float MaxDepth = 40f;

        /// <summary>Steilste helling van een metrotunnel als deel (0,04 = 4%); treinen halen meer niet.</summary>
        public const float MaxGrade = 0.04f;

        private const float GradeTolerance = 1e-6f;

        /// <summary>Verticale snelheid van een roltrap in m/s.</summary>
        public const float EscalatorSpeed = 0.5f;

        /// <summary>Vaste looptijd in seconden van de straat naar de roltrap en van de roltrap naar het perron.</summary>
        public const float WalkTime = 30f;

        public static bool IsDepthOk(float depth) => depth >= MinDepth && depth <= MaxDepth;

        public static bool IsSpacingOk(float2 a, float2 b) => math.distance(a, b) >= MinStationSpacing;

        /// <summary>Of een tunnel tussen twee perrons op verschillende diepte niet te steil is over de afstand over de grond.</summary>
        public static bool IsGradeOk(float depthA, float depthB, float distance)
        {
            if (!(distance > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "De afstand moet groter dan nul zijn.");
            }

            // Kleine marge: 40 / 1000 is op de ene runtime iets meer dan 0,04f en op de andere iets minder.
            return math.abs(depthA - depthB) / distance <= MaxGrade + GradeTolerance;
        }

        /// <summary>Seconden van de straat naar het perron (of terug): lopen plus de roltrap naar de diepte.</summary>
        public static float AccessTime(float depth)
        {
            if (!IsDepthOk(depth))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, $"Een perron ligt {MinDepth} tot {MaxDepth} m onder het maaiveld.");
            }

            return WalkTime + depth / EscalatorSpeed;
        }

        /// <summary>
        /// Seconden voor één rit tussen twee stations over <paramref name="distance"/> meter: optrekken, rijden op topsnelheid en remmen.
        /// Op een korte afstand haalt de metro de topsnelheid niet.
        /// </summary>
        public static float TravelTime(float distance)
        {
            if (!(distance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "De afstand kan niet negatief zijn.");
            }

            var ramp = TopSpeed * TopSpeed / Acceleration;
            return distance >= ramp
                ? distance / TopSpeed + TopSpeed / Acceleration
                : 2f * math.sqrt(distance / Acceleration);
        }
    }
}
