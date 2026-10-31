using System;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    public enum WaterSource
    {
        River = 0,
        Groundwater = 1,
    }

    /// <summary>
    /// Waterkwaliteit als waarde van 0 (ondrinkbaar) tot 1 (zuiver), van inname (E06-06) tot zuivering. Rivierwater neemt vervuiling
    /// vrijwel volledig over; grondwater wordt door de bodem gefilterd.
    /// </summary>
    public static class WaterQuality
    {
        public const float GroundwaterPollutionShare = 0.2f;

        /// <summary>Kwaliteit van het ingenomen water bij een vervuilingsgraad van 0 tot 1 op die plek.</summary>
        public static float Intake(WaterSource source, float pollution)
        {
            var share = source == WaterSource.River ? 1f : GroundwaterPollutionShare;
            return 1f - (math.clamp(pollution, 0f, 1f) * share);
        }

        /// <summary>Kwaliteit na zuivering: de installatie haalt het deel <paramref name="efficiency"/> (0 tot 1) van de vervuiling eruit.</summary>
        public static float Treat(float quality, float efficiency)
        {
            quality = math.clamp(quality, 0f, 1f);
            return quality + ((1f - quality) * math.clamp(efficiency, 0f, 1f));
        }

        /// <summary>Het water mag de kraan in vanaf deze kwaliteit.</summary>
        public const float Drinkable = 0.8f;
    }

    /// <summary>Watertoren: buffert water tussen de aanvoer van de zuivering en de wisselende vraag, in kubieke meters.</summary>
    public sealed class WaterTower
    {
        public WaterTower(float capacity, float level = 0f)
        {
            if (!(capacity > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "De toren heeft een inhoud groter dan nul nodig.");
            }

            Capacity = capacity;
            Level = math.clamp(level, 0f, capacity);
        }

        public float Capacity { get; }

        public float Level { get; private set; }

        /// <summary>Verwerkt één stap: vult met <paramref name="inflow"/> en levert zoveel van <paramref name="demand"/> als beschikbaar is. Geeft het geleverde volume.</summary>
        public float Step(float inflow, float demand)
        {
            if (!(inflow >= 0f) || !(demand >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(inflow), "Aanvoer en vraag moeten nul of meer zijn.");
            }

            var available = Level + inflow;
            var delivered = math.min(demand, available);
            Level = math.min(Capacity, available - delivered);
            return delivered;
        }
    }
}
