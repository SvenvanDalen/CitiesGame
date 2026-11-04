using System;

namespace Cities.Simulation.Industry
{
    /// <summary>Uitkomst van één week winning.</summary>
    public readonly struct SiteWeek
    {
        public SiteWeek(float output, float pollution)
        {
            Output = output;
            Pollution = pollution;
        }

        public float Output { get; }

        public float Pollution { get; }
    }

    /// <summary>
    /// Eén winning (E14-02): een boerderij, houtkap, mijn of oliepomp op een stuk grond met een rijkdom (0 tot 1) uit de grondstoflaag.
    /// Landbouw volgt het seizoen en put de grond uit; bosbouw oogst staand hout dat langzaam aangroeit; erts en olie zijn eindig,
    /// waarbij de oliedruk met de voorraad zakt.
    /// </summary>
    public sealed class ExtractionSite
    {
        public const int MaxLevel = 3;
        public const int WeeksPerYear = 52;

        private const float WinterBelow = 0.5f;
        private const float FertilityDecay = 0.003f;
        private const float FertilityRecovery = 0.01f;
        private const float FertilityFloor = 0.2f;
        private const float ForestGrowth = 0.05f;
        private const float ForestSeed = 0.02f;

        // Per kwartaal (lente, zomer, herfst, winter) het aandeel van de piekopbrengst.
        private static readonly float[] Seasons = { 0.7f, 1.2f, 1.4f, 0.2f };

        public ExtractionSite(int id, IndustryKind kind, int level, float richness)
        {
            if (level < 1 || level > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Het niveau ligt tussen 1 en " + MaxLevel + ".");
            }

            if (!(richness >= 0f && richness <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(richness), "Rijkdom ligt tussen 0 en 1.");
            }

            Id = id;
            Kind = kind;
            Profile = IndustryProfile.For(kind);
            Level = level;
            Richness = richness;
            Capacity = Profile.StockPerRichness * (Profile.Behaviour == StockBehaviour.Fertility ? 1f : richness);
            Stock = Profile.Behaviour == StockBehaviour.Regrowing ? Capacity * 0.8f : Capacity;
        }

        public int Id { get; }

        public IndustryKind Kind { get; }

        public IndustryProfile Profile { get; }

        public int Level { get; }

        public float Richness { get; }

        /// <summary>Beginvoorraad en bovengrens van <see cref="Stock"/>.</summary>
        public float Capacity { get; }

        /// <summary>Vruchtbaarheid, staand hout of resterende delfstof, afhankelijk van het type.</summary>
        public float Stock { get; private set; }

        public bool Depleted => Profile.Behaviour == StockBehaviour.Finite && Stock <= 0f;

        public float Workers => Profile.WorkersPerLevel * Level;

        /// <summary>Seizoensfactor van een week van het jaar.</summary>
        public static float SeasonFactor(int week)
        {
            var w = ((week % WeeksPerYear) + WeeksPerYear) % WeeksPerYear;
            return Seasons[w * Seasons.Length / WeeksPerYear];
        }

        public SiteWeek Step(int week)
        {
            float output;
            switch (Profile.Behaviour)
            {
                case StockBehaviour.Fertility:
                    output = StepFarm(week);
                    break;
                case StockBehaviour.Regrowing:
                    output = StepForest();
                    break;
                default:
                    output = StepFinite();
                    break;
            }

            return new SiteWeek(output, output * Profile.PollutionPerUnit);
        }

        private float StepFarm(int week)
        {
            var season = SeasonFactor(week);
            var output = Profile.BaseOutput * Level * (0.5f + (Richness * 0.5f)) * season * Stock;
            Stock = season < WinterBelow
                ? Math.Min(Capacity, Stock + FertilityRecovery)
                : Math.Max(FertilityFloor, Stock - FertilityDecay);
            return output;
        }

        private float StepForest()
        {
            if (Capacity <= 0f)
            {
                return 0f;
            }

            var harvest = Math.Min(Stock, Profile.BaseOutput * Level);
            Stock -= harvest;
            var seed = Math.Max(Stock, Capacity * ForestSeed);
            Stock = Math.Min(Capacity, Stock + (ForestGrowth * seed * (1f - (seed / Capacity))));
            return harvest;
        }

        private float StepFinite()
        {
            if (Stock <= 0f)
            {
                return 0f;
            }

            var pressure = Kind == IndustryKind.Oil ? (float)Math.Sqrt(Stock / Capacity) : 1f;
            var output = Math.Min(Stock, Profile.BaseOutput * Level * (0.5f + Richness) * pressure);
            Stock -= output;
            return output;
        }

        internal void Restore(float stock)
        {
            Stock = Math.Max(0f, Math.Min(Capacity, stock));
        }
    }
}
