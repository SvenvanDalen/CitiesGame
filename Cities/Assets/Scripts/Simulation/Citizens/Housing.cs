using System;
using Cities.Simulation.Fields;

namespace Cities.Simulation.Citizens
{
    /// <summary>Inkomensklasse van een huishouden, afgeleid van het gezamenlijke weekinkomen.</summary>
    public enum IncomeClass
    {
        Low = 0,
        Middle = 1,
        High = 2,
    }

    /// <summary>Soort woning naar huurprijs: goedkoop, gemiddeld of duur. Elke klasse heeft een eigen niveau van woningen waar ze op zoekt.</summary>
    public enum HousingTier
    {
        Affordable = 0,
        Standard = 1,
        Premium = 2,
    }

    /// <summary>
    /// Inkomens en huisvesting (E11-10). Het weekinkomen van een huishouden bepaalt zijn inkomensklasse; de landwaarde van een kavel bepaalt
    /// de huur en daarmee het soort woning. Een huishouden zoekt een woning van zijn eigen niveau of goedkoper en betaalt hooguit
    /// <see cref="RentShare"/> van zijn inkomen aan huur. De vraag per klasse bepaalt welke woningen waar groeien: op een kavel met hoge
    /// landwaarde komen alleen premium woningen als er genoeg huishoudens met een hoog inkomen zijn.
    /// </summary>
    public static class Housing
    {
        /// <summary>Weekinkomen van het huishouden waarboven het middenklasse is.</summary>
        public const int MiddleIncomeFrom = 600;

        /// <summary>Weekinkomen van het huishouden waarboven het hoge inkomensklasse is.</summary>
        public const int HighIncomeFrom = 1400;

        /// <summary>Aandeel van het inkomen dat een huishouden hooguit aan huur kwijt wil zijn.</summary>
        public const float RentShare = 0.3f;

        /// <summary>Landwaarde vanaf waar een kavel standaard woningen krijgt, in plaats van goedkope.</summary>
        public const float StandardFromLandValue = 0.33f;

        /// <summary>Landwaarde vanaf waar een kavel premium woningen krijgt.</summary>
        public const float PremiumFromLandValue = 0.66f;

        public const int ClassCount = 3;

        /// <summary>Totaal weekinkomen van de leden van één huishouden.</summary>
        public static int HouseholdIncome(ReadOnlySpan<Citizen> members)
        {
            var total = 0;
            foreach (var member in members)
            {
                total += EducationEconomy.WeeklyIncome(member);
            }

            return total;
        }

        /// <summary>De inkomensklasse bij een weekinkomen van het huishouden.</summary>
        public static IncomeClass ClassOf(int weeklyIncome)
        {
            if (weeklyIncome >= HighIncomeFrom)
            {
                return IncomeClass.High;
            }

            return weeklyIncome >= MiddleIncomeFrom ? IncomeClass.Middle : IncomeClass.Low;
        }

        /// <summary>Het soort woning dat bij een kavel met deze landwaarde hoort.</summary>
        public static HousingTier TierOf(float landValue)
        {
            if (landValue >= PremiumFromLandValue)
            {
                return HousingTier.Premium;
            }

            return landValue >= StandardFromLandValue ? HousingTier.Standard : HousingTier.Affordable;
        }

        /// <summary>De huur per week van een woning op een kavel met deze landwaarde.</summary>
        public static float RentFor(float landValue) => LandValueModel.Rent(landValue);

        /// <summary>Of een huishouden met dit weekinkomen de huur kan opbrengen.</summary>
        public static bool CanAfford(int weeklyIncome, float rent) => weeklyIncome > 0 && rent <= weeklyIncome * RentShare;

        /// <summary>
        /// Of een huishouden van deze klasse op een kavel met deze landwaarde wil en kan wonen: de woning is van zijn niveau of goedkoper
        /// en de huur past binnen het inkomen.
        /// </summary>
        public static bool Fits(int weeklyIncome, float landValue) =>
            (int)TierOf(landValue) <= (int)ClassOf(weeklyIncome) && CanAfford(weeklyIncome, RentFor(landValue));

        /// <summary>Het aantal huishoudens per inkomensklasse, uit de weekinkomens van de huishoudens.</summary>
        public static void CountClasses(ReadOnlySpan<int> householdIncomes, Span<int> counts)
        {
            if (counts.Length < ClassCount)
            {
                throw new ArgumentException("Er zijn drie plekken nodig, één per inkomensklasse.", nameof(counts));
            }

            counts.Clear();
            foreach (var income in householdIncomes)
            {
                counts[(int)ClassOf(income)]++;
            }
        }

        /// <summary>
        /// Het gewicht van 0 tot 1 waarmee een kavel met deze landwaarde woningen laat groeien: het aandeel van de zoekende huishoudens
        /// dat bij het soort woning van de kavel hoort. Zonder vraag groeit er niets.
        /// </summary>
        public static float GrowthWeight(float landValue, ReadOnlySpan<int> householdsPerClass)
        {
            if (householdsPerClass.Length < ClassCount)
            {
                throw new ArgumentException("Er zijn drie aantallen nodig, één per inkomensklasse.", nameof(householdsPerClass));
            }

            var total = 0;
            for (var i = 0; i < ClassCount; i++)
            {
                if (householdsPerClass[i] < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(householdsPerClass), householdsPerClass[i], "Een aantal kan niet negatief zijn.");
                }

                total += householdsPerClass[i];
            }

            return total == 0 ? 0f : householdsPerClass[(int)TierOf(landValue)] / (float)total;
        }
    }
}
