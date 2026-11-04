using System;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Bouwvoorschriften (E15-12). Aardbevingsbestendig bouwen (<see cref="BuildingCode.Reinforced"/>) kost meer bij nieuwbouw, maar de
    /// weerstand tegen schudden stijgt (<see cref="Earthquake.ResistanceOf"/>); een bestaand gebouw kan achteraf worden versterkt tegen het
    /// verschil in bouwkosten plus een toeslag voor het ombouwen. Daarmee is de afweging: investeren vooraf, of schade na de aardbeving.
    /// </summary>
    public static class BuildingCodes
    {
        /// <summary>Toeslag op het verschil in bouwkosten bij het achteraf versterken.</summary>
        public const float RetrofitSurcharge = 0.5f;

        /// <summary>Bouwkosten ten opzichte van normaal bouwen (1).</summary>
        public static float CostFactor(BuildingCode code) => code switch
        {
            BuildingCode.None => 0.9f,
            BuildingCode.Basic => 1f,
            BuildingCode.Reinforced => 1.25f,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Onbekend bouwvoorschrift."),
        };

        /// <summary>Bouwkosten van een gebouw met een basisprijs van <paramref name="baseCost"/> onder het opgegeven voorschrift.</summary>
        public static long BuildCost(long baseCost, BuildingCode code)
        {
            if (baseCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseCost), baseCost, "Kosten kunnen niet negatief zijn.");
            }

            return (long)Math.Round(baseCost * (double)CostFactor(code), MidpointRounding.AwayFromZero);
        }

        /// <summary>Kosten om een bestaand gebouw te versterken van <paramref name="from"/> naar <paramref name="to"/>; terugbouwen kost niets.</summary>
        public static long RetrofitCost(long baseCost, BuildingCode from, BuildingCode to)
        {
            var difference = BuildCost(baseCost, to) - BuildCost(baseCost, from);
            return difference <= 0 ? 0 : (long)Math.Round(difference * (1.0 + RetrofitSurcharge), MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Het voorschrift waaronder nieuwbouw valt: het strengste van wat de stad eist en wat de bouwer zelf kiest. Zo kan een stad
        /// versterkt bouwen verplicht stellen zonder dat een bouwer onder het gekozen niveau blijft.
        /// </summary>
        public static BuildingCode Apply(BuildingCode required, BuildingCode chosen) => (BuildingCode)Math.Max((byte)required, (byte)chosen);

        /// <summary>
        /// Hoeveel schade het voorschrift scheelt bij een aardbeving van deze intensiteit: de schade onder <paramref name="reference"/>
        /// min de schade onder <paramref name="code"/>, op een gebouw van het opgegeven niveau.
        /// </summary>
        public static float DamageAvoided(float intensity, BuildingCode reference, BuildingCode code, int level) =>
            Earthquake.Damage(intensity, reference, level) - Earthquake.Damage(intensity, code, level);
    }
}
