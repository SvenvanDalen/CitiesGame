using System;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    /// <summary>Wat er met een burger gebeurde in een levensjaar.</summary>
    public enum LifeEvent
    {
        None = 0,
        GrewUp = 1,
        Retired = 2,
        Died = 3,
    }

    /// <summary>
    /// De instelbare kansen en leeftijdsgrenzen van de levenscyclus (E07-02). Sterfte is een kleine basiskans plus een term die met de
    /// leeftijd steil oploopt, schaalt met de gezondheid en is zeker op <see cref="Citizen.MaxAge"/>.
    /// </summary>
    public sealed class LifeRules
    {
        public static readonly LifeRules Default = new LifeRules(Citizen.AdultAge, Citizen.RetirementAge, 45, 0.08f, 0.0005f, 0.5f);

        public LifeRules(int adultAge, int retirementAge, int fertileUntil, float birthChance, float baseDeathChance, float oldAgeDeathChance)
        {
            if (adultAge <= 0 || retirementAge <= adultAge || retirementAge > Citizen.MaxAge)
            {
                throw new ArgumentOutOfRangeException(nameof(retirementAge), "De leeftijdsgrenzen moeten oplopen: kind, volwassen, gepensioneerd.");
            }

            if (fertileUntil < adultAge || fertileUntil > retirementAge)
            {
                throw new ArgumentOutOfRangeException(nameof(fertileUntil), "De vruchtbare leeftijd ligt tussen volwassen en pensioen.");
            }

            AdultAge = adultAge;
            RetirementAge = retirementAge;
            FertileUntil = fertileUntil;
            BirthChance = Probability(birthChance, nameof(birthChance));
            BaseDeathChance = Probability(baseDeathChance, nameof(baseDeathChance));
            OldAgeDeathChance = Probability(oldAgeDeathChance, nameof(oldAgeDeathChance));
        }

        public int AdultAge { get; }

        public int RetirementAge { get; }

        /// <summary>Tot en met deze leeftijd kan een volwassene een kind krijgen.</summary>
        public int FertileUntil { get; }

        /// <summary>Kans per jaar dat een vruchtbare volwassene een kind krijgt.</summary>
        public float BirthChance { get; }

        /// <summary>Sterftekans per jaar voor een gezonde jonge burger.</summary>
        public float BaseDeathChance { get; }

        /// <summary>Extra sterftekans per jaar rond de maximale leeftijd; loopt met de vierde macht van de leeftijd op.</summary>
        public float OldAgeDeathChance { get; }

        public AgeGroup GroupOf(int age)
        {
            if (age < AdultAge)
            {
                return AgeGroup.Child;
            }

            return age >= RetirementAge ? AgeGroup.Retired : AgeGroup.Adult;
        }

        /// <summary>Kans op overlijden in het komende jaar; slechte gezondheid verdubbelt hem, goede laat hem ongemoeid.</summary>
        public float DeathChance(int age, int health)
        {
            if (age >= Citizen.MaxAge)
            {
                return 1f;
            }

            var ageShare = (float)age / Citizen.MaxAge;
            var chance = BaseDeathChance + (OldAgeDeathChance * ageShare * ageShare * ageShare * ageShare);
            return math.saturate(chance * (2f - (math.clamp(health, 0, Citizen.MaxScore) / (float)Citizen.MaxScore)));
        }

        private static float Probability(float value, string name)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(name, value, "Een kans ligt tussen 0 en 1.");
            }

            return value;
        }
    }

    /// <summary>Het jaarlijkse leven van een burger: opgroeien, pensioen, geboorte en overlijden.</summary>
    public static class CitizenLife
    {
        /// <summary>
        /// Laat de burger een jaar ouder worden. Bij overlijden blijft de burger ongewijzigd en moet de beller hem verwijderen; bij pensioen
        /// verliest hij zijn werk.
        /// </summary>
        public static LifeEvent AgeOneYear(ref Citizen citizen, LifeRules rules, ref DeterministicRandom random)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (random.Chance(rules.DeathChance(citizen.Age, citizen.Health)))
            {
                return LifeEvent.Died;
            }

            citizen.Age++;
            if (citizen.Age == rules.AdultAge)
            {
                return LifeEvent.GrewUp;
            }

            if (citizen.Age == rules.RetirementAge)
            {
                citizen.Work = default;
                return LifeEvent.Retired;
            }

            return LifeEvent.None;
        }

        /// <summary>Kans dat een vruchtbare volwassene dit jaar een kind krijgt; de baby woont in het huishouden van de ouder.</summary>
        public static bool TryBirth(in Citizen parent, LifeRules rules, ref DeterministicRandom random, out Citizen baby)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            baby = default;
            if (!parent.HasHome || parent.Age < rules.AdultAge || parent.Age > rules.FertileUntil || !random.Chance(rules.BirthChance))
            {
                return false;
            }

            baby = new Citizen
            {
                Home = parent.Home,
                Household = parent.Household,
                Health = Citizen.MaxScore,
                Happiness = Citizen.MaxScore / 2,
            };
            return true;
        }
    }
}
