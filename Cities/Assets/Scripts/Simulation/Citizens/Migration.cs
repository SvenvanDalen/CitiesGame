using System;
using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    /// <summary>Wat een nieuwkomer of vertrekker over de stad ziet; alle waarden lopen van 0 (slecht) tot 1 (goed).</summary>
    public readonly struct CityAppeal
    {
        public CityAppeal(float jobs, float landValue, float services, float taxFactor)
        {
            Jobs = jobs;
            LandValue = landValue;
            Services = services;
            TaxFactor = taxFactor;
        }

        /// <summary>Hoeveel werkzoekenden de stad kan opnemen (1 = veel open banen, 0 = geen).</summary>
        public float Jobs { get; }

        /// <summary>Gemiddelde landwaarde (<c>LandValueModel</c>).</summary>
        public float LandValue { get; }

        /// <summary>Dekking van de diensten.</summary>
        public float Services { get; }

        /// <summary>Uitkomst van <c>TaxPolicy.MigrationFactor</c>: 1 bij een normale belasting, lager bij een hoge.</summary>
        public float TaxFactor { get; }
    }

    /// <summary>
    /// Migratie (E07-04). De aantrekkelijkheid van de stad (werk, landwaarde, diensten, belasting) bepaalt of er mensen bij komen of weggaan.
    /// Boven <see cref="Neutral"/> komen er mensen, eronder vertrekken er; nieuwkomers hebben een woning nodig en vertrekkers zijn nooit meer
    /// dan de bevolking. De uitkomst is een saldo voor één dag; de beller maakt of verwijdert de burgers.
    /// </summary>
    public static class Migration
    {
        /// <summary>Aantrekkelijkheid waarbij niemand komt of gaat.</summary>
        public const float Neutral = 0.5f;

        /// <summary>Deel van de bevolking dat per dag verhuist bij maximale (of minimale) aantrekkelijkheid.</summary>
        public const float MaxDailyRate = 0.002f;

        // Werk weegt het zwaarst: zonder baan blijft niemand.
        private const float JobsWeight = 0.4f;
        private const float LandValueWeight = 0.2f;
        private const float ServicesWeight = 0.2f;
        private const float TaxWeight = 0.2f;

        public static float Attractiveness(in CityAppeal city) =>
            math.saturate((JobsWeight * math.saturate(city.Jobs)) + (LandValueWeight * math.saturate(city.LandValue))
                + (ServicesWeight * math.saturate(city.Services)) + (TaxWeight * math.saturate(city.TaxFactor)));

        /// <summary>
        /// Netto verhuizingen vandaag: positief is instroom (hoogstens <paramref name="vacantHomes"/>, en minstens één nieuwkomer als de stad
        /// aantrekt zodat een lege stad kan beginnen), negatief is uitstroom (hoogstens de bevolking).
        /// </summary>
        public static int DailyNet(in CityAppeal city, int population, int vacantHomes)
        {
            if (population < 0 || vacantHomes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), "Aantallen kunnen niet negatief zijn.");
            }

            var pull = (Attractiveness(city) - Neutral) * 2f;
            if (pull > 0f)
            {
                var arrivals = (int)math.ceil(math.max(1, population) * MaxDailyRate * pull);
                return math.min(arrivals, vacantHomes);
            }

            return -math.min((int)math.round(population * MaxDailyRate * -pull), population);
        }
    }
}
