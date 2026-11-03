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
        Gas = 3,
        Oil = 4,
        Hydro = 5,
        Geothermal = 6,
        Nuclear = 7,
        SolarTower = 8,
        WasteToEnergy = 9,
        Battery = 10,
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

        [Tooltip("Afvalenergiecentrale: opgewekte megawattuur per ton verbrand afval (ca. 0,6).")]
        public float MwhPerTonne;

        [Tooltip("Batterij: opslagcapaciteit in megawattuur.")]
        public float StorageMwh;

        [Tooltip("Batterij: deel van de geladen energie dat er weer uitkomt, van 0 tot 1.")]
        public float RoundTripEfficiency;

        /// <summary>Verbrandt brandstof (kolen, gas, olie) en kost dus per geleverde megawattuur.</summary>
        public bool BurnsFuel => Source == PowerSource.Coal || Source == PowerSource.Gas || Source == PowerSource.Oil;

        /// <summary>Gratis energiebron zonder uitstoot: wind, zon, zonnetoren, water en aardwarmte.</summary>
        public bool IsClean => Source == PowerSource.Wind || Source == PowerSource.Solar || Source == PowerSource.SolarTower
            || Source == PowerSource.Hydro || Source == PowerSource.Geothermal;

        /// <summary>Kan op commando op- en afschakelen (gas, olie, water en batterij); de rest levert wat de bron geeft.</summary>
        public bool IsDispatchable => Source == PowerSource.Gas || Source == PowerSource.Oil || Source == PowerSource.Hydro || Source == PowerSource.Battery;

        /// <summary>Wat de centrale nu levert in megawatt, bij tijd van de dag (0 tot 1) en windsnelheid in m/s.</summary>
        /// <param name="waterFlow">Debiet van de rivier als fractie van het ontwerpdebiet (0 tot 1+), voor waterkracht.</param>
        /// <param name="wasteTonnesPerHour">Aanbod van afval per uur, voor afvalenergie.</param>
        public float Output(float timeOfDay, float windSpeed, float waterFlow = 1f, float wasteTonnesPerHour = 0f)
        {
            switch (Source)
            {
                case PowerSource.Solar:
                    return CapacityMw * PowerCurves.Solar(timeOfDay);
                case PowerSource.SolarTower:
                    return CapacityMw * PowerCurves.SolarTower(timeOfDay);
                case PowerSource.Wind:
                    return CapacityMw * PowerCurves.Wind(windSpeed);
                case PowerSource.Hydro:
                    return CapacityMw * PowerCurves.Hydro(waterFlow);
                case PowerSource.WasteToEnergy:
                    return Mathf.Clamp(wasteTonnesPerHour * MwhPerTonne, 0f, CapacityMw);
                case PowerSource.Battery:
                    return 0f; // een batterij levert uit zijn lading (PowerBattery), niet uit een bron
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

            if (IsClean && (FuelCostPerMwh > 0f || AirPollution > 0f))
            {
                errors.Add("Wind, zon, water en aardwarmte verbranden niets: geen brandstofkosten en geen luchtvervuiling.");
            }

            if (BurnsFuel && FuelCostPerMwh <= 0f)
            {
                errors.Add("Een brandstofcentrale heeft brandstofkosten.");
            }

            if (Source == PowerSource.Nuclear && (FuelCostPerMwh <= 0f || AirPollution > 0f))
            {
                errors.Add("Een kerncentrale heeft splijtstofkosten en stoot geen lucht vervuilende stoffen uit.");
            }

            if (Source == PowerSource.WasteToEnergy && !(MwhPerTonne > 0f && FuelCostPerMwh == 0f))
            {
                errors.Add("Een afvalenergiecentrale heeft een rendement per ton en geen brandstofkosten.");
            }

            if (Source == PowerSource.Battery && (!(StorageMwh > 0f) || !(RoundTripEfficiency > 0f && RoundTripEfficiency <= 1f)))
            {
                errors.Add("Een batterij heeft opslag en een rendement tussen 0 en 1 nodig.");
            }
        }
    }
}
