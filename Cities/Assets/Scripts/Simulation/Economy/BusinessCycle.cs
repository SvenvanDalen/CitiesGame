using System;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Economy
{
    /// <summary>De fase van de conjunctuur.</summary>
    public enum BusinessPhase
    {
        Normal,
        Boom,
        Recession,
    }

    /// <summary>
    /// Optionele conjunctuur (E11-12): de economie golft rond een gemiddelde met opbloei en recessie. De golf duurt twee jaar
    /// (<see cref="WeeksPerCycle"/> weken) en de uitslag van elke golf volgt uit de seed, dus dezelfde stad heeft dezelfde cycli. De golf is
    /// bewust zacht (hooguit <see cref="MaxAmplitude"/>) zodat een recessie het spel niet onmogelijk maakt. Uitgeschakeld is de factor 1.
    /// </summary>
    public sealed class BusinessCycle
    {
        public const int WeeksPerCycle = 104;

        /// <summary>Kleinste en grootste uitslag van een golf (aandeel van de belastbare waarde).</summary>
        public const float MinAmplitude = 0.04f;

        public const float MaxAmplitude = 0.12f;

        /// <summary>Boven of onder deze afwijking telt de golf als opbloei of recessie.</summary>
        public const float PhaseThreshold = 0.03f;

        public BusinessCycle(ulong seed, bool enabled = false)
        {
            Seed = seed;
            Enabled = enabled;
        }

        public ulong Seed { get; }

        public bool Enabled { get; set; }

        /// <summary>Uitslag van de golf waar <paramref name="week"/> in valt.</summary>
        public float Amplitude(int week)
        {
            var cycle = (ulong)math.max(0, week) / WeeksPerCycle;
            var unit = (SeedHash.Combine(Seed, cycle) >> 40) / (float)(1 << 24);
            return math.lerp(MinAmplitude, MaxAmplitude, unit);
        }

        /// <summary>Vermenigvuldiger op de belastbare waarde in <paramref name="week"/>; 1 als de conjunctuur uit staat.</summary>
        public float Factor(int week)
        {
            if (!Enabled)
            {
                return 1f;
            }

            // De sinus start bij nul en klimt eerst: elke golf begint met een opbloei en eindigt met een recessie.
            var angle = 2f * math.PI * (math.max(0, week) % WeeksPerCycle) / WeeksPerCycle;
            return 1f + (Amplitude(week) * math.sin(angle));
        }

        public BusinessPhase PhaseAt(int week)
        {
            var deviation = Factor(week) - 1f;
            if (deviation >= PhaseThreshold)
            {
                return BusinessPhase.Boom;
            }

            return deviation <= -PhaseThreshold ? BusinessPhase.Recession : BusinessPhase.Normal;
        }

        /// <summary>Past de factor toe op een bedrag.</summary>
        public long Scale(int week, long value) => (long)Math.Round(value * (double)Factor(week));
    }
}
