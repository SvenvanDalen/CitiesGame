using System;

namespace Cities.Simulation.Fields
{
    /// <summary>Wat een cel biedt, elk van 0 tot 1: dekking met diensten, nabijheid van parken, water en openbaar vervoer, en vervuiling (hoger is slechter).</summary>
    public readonly struct LandValueFactors
    {
        public LandValueFactors(float services, float parks, float water, float transit, float pollution)
        {
            Services = Clamp01(services);
            Parks = Clamp01(parks);
            Water = Clamp01(water);
            Transit = Clamp01(transit);
            Pollution = Clamp01(pollution);
        }

        public float Services { get; }

        public float Parks { get; }

        public float Water { get; }

        public float Transit { get; }

        public float Pollution { get; }

        private static float Clamp01(float v) => Math.Min(Math.Max(v, 0f), 1f);
    }

    /// <summary>
    /// De landwaarde per cel (E12-02) van 0 tot 1, uit diensten, parken, water, openbaar vervoer en vervuiling. De uitkomst is de
    /// <c>LandValue</c> van <c>LevelInputs</c> (gebouwniveau) en bepaalt de huur. De kaart zelf is een <see cref="DiffusionField"/>, dus de
    /// waarde van een cel waait na een <c>Step</c> mee naar de buurt. De gewichten zijn schattingen, af te stellen met speltesten.
    /// </summary>
    public static class LandValueModel
    {
        public const float ServicesWeight = 0.35f;
        public const float ParksWeight = 0.15f;
        public const float WaterWeight = 0.1f;
        public const float TransitWeight = 0.2f;

        /// <summary>Vervuiling trekt de waarde met dit aandeel van zichzelf omlaag; volle vervuiling laat een kwart over.</summary>
        public const float PollutionPenalty = 0.75f;

        /// <summary>Huur per cel per week bij landwaarde 1; bij 0 is de huur een tiende hiervan.</summary>
        public const float MaxRent = 100f;

        public static float Value(in LandValueFactors f)
        {
            var amenities = (f.Services * ServicesWeight) + (f.Parks * ParksWeight) + (f.Water * WaterWeight) + (f.Transit * TransitWeight);
            var sum = ServicesWeight + ParksWeight + WaterWeight + TransitWeight;
            return amenities / sum * (1f - (f.Pollution * PollutionPenalty));
        }

        public static float Rent(float landValue) => MaxRent * (0.1f + (0.9f * Math.Min(Math.Max(landValue, 0f), 1f)));
    }
}
