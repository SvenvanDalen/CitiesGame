using System;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>
    /// Parameters van één diffusiestap (E12-01), gedeeld door alle milieulagen: <see cref="Spread"/> is het deel dat per stap naar elke
    /// buurcel uitwaaiert, <see cref="Decay"/> het deel dat per stap vervalt en <see cref="Wind"/> het aantal cellen dat de waarde per stap
    /// meeschuift (x en z). De stap is alleen stabiel als 4 * Spread + |Wind.x| + |Wind.y| niet boven 1 komt.
    /// </summary>
    public readonly struct DiffusionSettings
    {
        public DiffusionSettings(float spread, float decay, float2 wind)
        {
            if (!(spread >= 0f) || !(decay >= 0f) || decay > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(spread), "Spreiding moet 0 of meer zijn en verval tussen 0 en 1.");
            }

            if (!((4f * spread) + math.csum(math.abs(wind)) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(wind), wind, "Spreiding en wind samen zijn te groot voor een stabiele stap.");
            }

            Spread = spread;
            Decay = decay;
            Wind = wind;
        }

        public float Spread { get; }

        public float Decay { get; }

        public float2 Wind { get; }
    }
}
