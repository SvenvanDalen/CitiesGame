using System;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Overstroming en tsunami (E15-06). Het water staat op één peil (meter boven zeeniveau) en overstroomt elke plek waarvan de grond lager ligt:
    /// de waterdiepte volgt dus het terrein. Een rivier stijgt langzaam naar een piek en zakt daarna weer; een tsunami komt als golf aan de kust
    /// en verliest landinwaarts hoogte. Een gebouw is beschadigd tot de hoogte waar het water komt (<see cref="Damage"/>): het deel boven het
    /// waterpeil blijft heel.
    /// </summary>
    public static class Flood
    {
        /// <summary>Hoogteverlies van een tsunamigolf per meter landinwaarts.</summary>
        public const float WaveLossPerMeter = 0.01f;

        // shortcut: golfverlies is lineair en onafhankelijk van helling of bebouwing; verfijnen met een stroomsimulatie als tsunami's er echt toe doen.
        /// <summary>Een overstroming kondigt zich een dag eerder aan, een tsunami niet.</summary>
        public static DisasterSpec RiverSpec { get; } = new DisasterSpec("Overstroming", 0.0008f, 1, 1, 7, 50_000, 10_000);

        public static DisasterSpec TsunamiSpec { get; } = new DisasterSpec("Tsunami", 0.0001f, 0, 1, 14, 150_000, 30_000);

        /// <summary>Waterpeil bij een rivier: stijgt lineair van <paramref name="baseLevel"/> naar <paramref name="peak"/> en zakt daarna even snel terug.</summary>
        public static float RiverLevel(float baseLevel, float peak, float riseDays, float day)
        {
            if (!(riseDays > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(riseDays), riseDays, "De stijging duurt langer dan nul dagen.");
            }

            var progress = math.clamp(day, 0f, 2f * riseDays);
            var fraction = 1f - math.abs((progress / riseDays) - 1f);
            return baseLevel + (math.max(0f, peak - baseLevel) * fraction);
        }

        /// <summary>Peil van een tsunami op <paramref name="distanceInland"/> meter van de kust: kustpeil plus golfhoogte min het verlies onderweg.</summary>
        public static float TsunamiLevel(float coastLevel, float waveHeight, float distanceInland) =>
            coastLevel + math.max(0f, waveHeight - (WaveLossPerMeter * math.max(0f, distanceInland)));

        /// <summary>Waterdiepte boven de grond; nul op droog land.</summary>
        public static float Depth(float waterLevel, float groundHeight) => math.max(0f, waterLevel - groundHeight);

        /// <summary>Schade van 0 tot 1: het deel van een gebouw van <paramref name="buildingHeight"/> meter dat onder water staat.</summary>
        public static float Damage(float depth, float buildingHeight)
        {
            if (!(buildingHeight > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(buildingHeight), buildingHeight, "Een gebouw heeft een hoogte groter dan nul.");
            }

            return math.saturate(depth / buildingHeight);
        }
    }
}
