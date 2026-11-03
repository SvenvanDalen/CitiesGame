using Cities.Simulation.Citizens;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>Wat er op één plek aan vervuiling is, elk als niveau van 0 (schoon) tot 1 (extreem): lucht, geluid, grond en water.</summary>
    public readonly struct PollutionExposure
    {
        public PollutionExposure(float air, float noise, float soil, float water)
        {
            Air = math.saturate(air);
            Noise = math.saturate(noise);
            Soil = math.saturate(soil);
            Water = math.saturate(water);
        }

        public float Air { get; }

        public float Noise { get; }

        public float Soil { get; }

        public float Water { get; }

        /// <summary>Maakt een blootstelling uit de ruwe veldwaarden: concentraties via de <c>Level</c> van elk veld, geluid in decibel via <see cref="NoiseModel.Annoyance"/>.</summary>
        public static PollutionExposure FromFields(float airConcentration, float noiseDecibel, float soilConcentration, float waterLevel) =>
            new PollutionExposure(AirPollution.Level(airConcentration), NoiseModel.Annoyance(noiseDecibel), SoilPollution.Level(soilConcentration), waterLevel);
    }

    /// <summary>
    /// Effecten van vervuiling (E12-08): zet de vier vervuilingsvelden om in wat de rest van het spel leest. Ziekte (via
    /// <see cref="HealthEnvironment"/>), geluk (via <see cref="HappinessFactors"/>), landwaarde en gebouwniveau (via
    /// <see cref="Overall"/>) en leegstand (via <see cref="VacancyRate"/>). Zonder eigen status. De gewichten zijn schattingen.
    /// </summary>
    public static class PollutionEffects
    {
        // Samen 1: lucht weegt het zwaarst voor de leefomgeving, geluid heeft een eigen gewicht, water raakt wonen het minst.
        public const float AirWeight = 0.4f;
        public const float NoiseWeight = 0.25f;
        public const float SoilWeight = 0.2f;
        public const float WaterWeight = 0.15f;

        /// <summary>Aandeel gebouweenheden dat altijd leeg staat (verhuizing, onderhoud).</summary>
        public const float BaseVacancy = 0.05f;

        /// <summary>Extra leegstand bij volle vervuiling.</summary>
        public const float PollutionVacancy = 0.4f;

        /// <summary>Extra leegstand bij landwaarde 0.</summary>
        public const float LowValueVacancy = 0.1f;

        /// <summary>Meer dan dit aandeel leeg komt niet voor: er blijft altijd een kern bewoond.</summary>
        public const float MaxVacancy = 0.6f;

        /// <summary>De totale vervuilingsbelasting van een plek, 0 tot 1: de gewogen som van de vier niveaus. Dit is de <c>Pollution</c> voor landwaarde en gebouwniveau.</summary>
        public static float Overall(in PollutionExposure e) =>
            (AirWeight * e.Air) + (NoiseWeight * e.Noise) + (SoilWeight * e.Soil) + (WaterWeight * e.Water);

        /// <summary>De gezondheidsomgeving van een woning: lucht, water en grond rechtstreeks, plus zorgdekking en bezetting van de bedden.</summary>
        public static HealthEnvironment ToHealth(in PollutionExposure e, bool hasCare, float bedLoad) =>
            new HealthEnvironment(e.Air, e.Water, e.Soil, hasCare, bedLoad);

        /// <summary>De vervuilingscomponent van de geluksfactoren: lucht, grond en water als vervuiling, geluid apart (het geluksmodel weegt ze los).</summary>
        public static float HappinessPollution(in PollutionExposure e)
        {
            var sum = AirWeight + SoilWeight + WaterWeight;
            return ((AirWeight * e.Air) + (SoilWeight * e.Soil) + (WaterWeight * e.Water)) / sum;
        }

        /// <summary>Vult de vervuiling en het geluid van <see cref="HappinessFactors"/> uit de blootstelling; de overige factoren komen van de aanroeper.</summary>
        public static HappinessFactors ToHappiness(in PollutionExposure e, float crime, float services, float taxShift, float commuteMinutes) =>
            new HappinessFactors(HappinessPollution(e), e.Noise, crime, services, taxShift, commuteMinutes);

        /// <summary>Het aandeel eenheden dat leegstaat: een basis, meer bij vervuiling en bij lage landwaarde, begrensd op <see cref="MaxVacancy"/>.</summary>
        public static float VacancyRate(in PollutionExposure e, float landValue) =>
            math.min(BaseVacancy + (PollutionVacancy * Overall(e)) + (LowValueVacancy * (1f - math.saturate(landValue))), MaxVacancy);

        /// <summary>Het aantal bezette eenheden van <paramref name="capacity"/> bij dit leegstandsaandeel (naar beneden afgerond, nooit negatief).</summary>
        public static int OccupiedUnits(int capacity, float vacancyRate) =>
            math.max(0, (int)math.floor(math.max(capacity, 0) * (1f - math.saturate(vacancyRate))));
    }
}
