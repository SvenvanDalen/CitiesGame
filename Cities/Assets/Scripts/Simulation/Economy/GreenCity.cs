using System;
using Unity.Mathematics;

namespace Cities.Simulation.Economy
{
    /// <summary>
    /// Groene stad (E25-03, E08-16): elektrische voertuigen met bereik en laadstations, windparken en groen beleid. Het aandeel elektrische
    /// voertuigen volgt uit de dekking van laadstations en het beleid; elk elektrisch voertuig scheelt uitstoot. Alles is optioneel en zacht.
    /// </summary>
    public static class GreenCity
    {
        public const float RangeMeters = 300_000f;

        public const float ChargeStationsPerKm2ForFullCover = 2f;

        /// <summary>Hoeveel van de uitstoot een elektrisch voertuig bespaart.</summary>
        public const float EmissionSaving = 0.9f;

        /// <summary>Kan een elektrisch voertuig deze rit afmaken, met de lading die het nog heeft (0 tot 1)?</summary>
        public static bool CanComplete(float tripMeters, float charge)
        {
            if (tripMeters < 0f || charge < 0f || charge > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(charge), charge, "Lading ligt tussen 0 en 1, een rit is niet negatief.");
            }

            return tripMeters <= RangeMeters * charge;
        }

        /// <summary>Aandeel elektrische voertuigen (0 tot 0.9) uit de laaddekking (stations per km²) en de subsidie (0 tot 1).</summary>
        public static float ElectricShare(float stationsPerKm2, float subsidy)
        {
            var cover = math.saturate(stationsPerKm2 / ChargeStationsPerKm2ForFullCover);
            return 0.9f * cover * (0.4f + (0.6f * math.saturate(subsidy)));
        }

        /// <summary>De uitstootfactor van het verkeer (1 = geen elektrische voertuigen).</summary>
        public static float TrafficEmissionFactor(float electricShare) => 1f - (EmissionSaving * math.saturate(electricShare));

        /// <summary>Vermogen van een windpark in megawatt: nul onder 3 en boven 25 m/s, vol vanaf 12 m/s.</summary>
        public static float WindPower(int turbines, float windSpeed)
        {
            if (turbines < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(turbines), turbines, "Aantal turbines is niet negatief.");
            }

            if (windSpeed < 3f || windSpeed > 25f)
            {
                return 0f;
            }

            var f = math.saturate((windSpeed - 3f) / 9f);
            return turbines * 3f * f * f * f;
        }
    }
}
