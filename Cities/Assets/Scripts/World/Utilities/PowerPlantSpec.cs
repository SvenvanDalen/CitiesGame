using System;
using System.Collections.Generic;
using Cities.Core.Configuration;
using Cities.Simulation.Utilities;
using UnityEngine;

namespace Cities.World.Utilities
{
    public enum PowerSource
    {
        Coal = 0,
        Wind = 1,
        Solar = 2,
    }

    /// <summary>
    /// Eigenschappen van een stroomcentrale (E06-02): wat hij levert, kost en vervuilt. Kolen levert altijd, zon alleen overdag en wind
    /// afhankelijk van de windsnelheid (<see cref="Output"/>).
    /// </summary>
    [Serializable]
    public struct PowerPlantSpec
    {
        public PowerSource Source;

        [Tooltip("Breedte langs de weg, in cellen van 8 m.")]
        public int FootprintWidth;

        [Tooltip("Diepte vanaf de weg, in cellen van 8 m.")]
        public int FootprintDepth;

        [Tooltip("Vermogen bij vol bedrijf, in megawatt.")]
        public float CapacityMw;

        public long ConstructionCost;

        public long UpkeepPerMonth;

        [Tooltip("Brandstofkosten per geleverde megawattuur, in euro; wind en zon zijn gratis.")]
        public float FuelCostPerMwh;

        [Tooltip("Luchtvervuiling bij vol bedrijf, van 0 tot 100; schaalt mee met de levering.")]
        public float AirPollution;

        [Tooltip("Geluidsniveau bij vol bedrijf, van 0 tot 100; schaalt mee met de levering.")]
        public float Noise;

        [Tooltip("Bouwtijd in speldagen.")]
        public int ConstructionDays;

        /// <summary>Wat de centrale nu levert in megawatt, bij tijd van de dag (0 tot 1) en windsnelheid in m/s.</summary>
        public float Output(float timeOfDay, float windSpeed)
        {
            switch (Source)
            {
                case PowerSource.Solar:
                    return CapacityMw * PowerCurves.Solar(timeOfDay);
                case PowerSource.Wind:
                    return CapacityMw * PowerCurves.Wind(windSpeed);
                default:
                    return CapacityMw;
            }
        }

        /// <summary>Vervuiling bij een levering van <paramref name="outputMw"/>: evenredig met het deel van het vermogen dat draait.</summary>
        public float PollutionAt(float outputMw) => CapacityMw <= 0f ? 0f : AirPollution * Mathf.Clamp01(outputMw / CapacityMw);

        /// <summary>Brandstofkosten per uur bij een levering van <paramref name="outputMw"/>.</summary>
        public float FuelCostPerHour(float outputMw) => outputMw * FuelCostPerMwh;

        public void Validate(ICollection<string> errors)
        {
            SpecValidation.Range(errors, nameof(FootprintWidth), FootprintWidth, 1, 16);
            SpecValidation.Range(errors, nameof(FootprintDepth), FootprintDepth, 1, 16);
            SpecValidation.Range(errors, nameof(CapacityMw), CapacityMw, 0.1f, 5000f, " MW");
            SpecValidation.Range(errors, nameof(FuelCostPerMwh), FuelCostPerMwh, 0f, 1000f);
            SpecValidation.Range(errors, nameof(AirPollution), AirPollution, 0f, 100f);
            SpecValidation.Range(errors, nameof(Noise), Noise, 0f, 100f);
            SpecValidation.Range(errors, nameof(ConstructionDays), ConstructionDays, 0, 3650);
            SpecValidation.NotNegative(errors, nameof(ConstructionCost), ConstructionCost);
            SpecValidation.NotNegative(errors, nameof(UpkeepPerMonth), UpkeepPerMonth);

            if (Source != PowerSource.Coal && (FuelCostPerMwh > 0f || AirPollution > 0f))
            {
                errors.Add("Wind en zon verbranden niets: geen brandstofkosten en geen luchtvervuiling.");
            }

            if (Source == PowerSource.Coal && FuelCostPerMwh <= 0f)
            {
                errors.Add("Een kolencentrale heeft brandstofkosten.");
            }
        }
    }
}
