using System;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    public enum PollutionSource : byte
    {
        Industry,
        Traffic,
        CoalPlant,
    }

    /// <summary>
    /// Luchtvervuiling (E12-03): een <see cref="DiffusionField"/> waar industrie, verkeer en kolencentrales per stap in uitstoten en die
    /// met de wind meewaait en langzaam vervalt (de aanroeper telt <see cref="Emission"/> per bron bij de cel op). <see cref="Level"/> zet de concentratie om naar 0 tot 1; dat getal is de <c>Pollution</c>
    /// van <see cref="LandValueFactors"/> en, via <see cref="HealthFactor"/>, de extra kans op ziekte in <c>HealthModel</c>. De waarden zijn schattingen.
    /// </summary>
    public static class AirPollution
    {
        /// <summary>Concentratie waarbij het niveau 0,5 is.</summary>
        public const float HalfLevel = 50f;

        /// <summary>Ziektekans bij volle vervuiling, als veelvoud van de normale kans.</summary>
        public const float MaxHealthFactor = 3f;

        /// <summary>Uitstoot per stap: een fabriek per gebouw, verkeer per voertuig, een kolencentrale per centrale.</summary>
        public static float Emission(PollutionSource source) => source switch
        {
            PollutionSource.Industry => 4f,
            PollutionSource.Traffic => 0.2f,
            PollutionSource.CoalPlant => 20f,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Onbekende bron."),
        };

        /// <summary>Concentratie (0 of meer) naar vervuilingsniveau van 0 tot 1; verzadigt, zodat extreme pieken het bereik niet uitrekken.</summary>
        public static float Level(float concentration)
        {
            var c = math.max(concentration, 0f);
            return c / (c + HalfLevel);
        }

        /// <summary>Factor op de kans om ziek te worden: 1 bij schone lucht, <see cref="MaxHealthFactor"/> bij volle vervuiling.</summary>
        public static float HealthFactor(float level) => 1f + ((MaxHealthFactor - 1f) * math.clamp(level, 0f, 1f));
    }
}
