using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Cities.Simulation.Services;
using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    /// <summary>De omgeving van een burger voor zijn gezondheid: vervuiling (0 tot 1), toegang tot zorg en de drukte van de ziekenhuisbedden.</summary>
    public readonly struct HealthEnvironment
    {
        public HealthEnvironment(float air, float water, float soil, bool hasCare, float bedLoad)
        {
            Air = math.saturate(air);
            Water = math.saturate(water);
            Soil = math.saturate(soil);
            HasCare = hasCare;
            BedLoad = math.max(bedLoad, 0f);
        }

        public float Air { get; }

        public float Water { get; }

        public float Soil { get; }

        /// <summary>De woning ligt binnen het bereik van een kliniek of ziekenhuis.</summary>
        public bool HasCare { get; }

        /// <summary>Patiënten per bed; boven 1 zijn er te weinig bedden.</summary>
        public float BedLoad { get; }
    }

    /// <summary>
    /// Gezondheid en sterfte van burgers (E07-09). De gezondheidsscore van een <see cref="Citizen"/> beweegt elk jaar naar een doel dat daalt
    /// door vervuiling (lucht, water, bodem) en hoge leeftijd en stijgt door zorg binnen bereik. Zonder zorg of met overvolle bedden is de
    /// sterftekans van <see cref="LifeRules"/> hoger (<see cref="CareFactor"/>). Alles is een zuivere functie van de burger en zijn omgeving;
    /// de status zit in <see cref="Citizen.Health"/> en gaat dus al mee in de burgers-opslag. De waarden zijn schattingen.
    /// </summary>
    public static class CitizenHealthModel
    {
        /// <summary>Bedden per ambulance van een kliniek of ziekenhuis.</summary>
        public const int BedsPerVehicle = 25;

        /// <summary>Een burger met een gezondheid onder deze score ligt in de zorg en bezet een bed.</summary>
        public const int PatientBelow = 40;

        /// <summary>Maximale stap van de gezondheid per jaar.</summary>
        public const int YearlyStep = 10;

        /// <summary>Sterftefactor zonder zorg; met overvolle bedden loopt hij op tot dit maximum.</summary>
        public const float MaxCareFactor = 2f;

        private const int AgingFromAge = 60;
        private const float AirPenalty = 30f;
        private const float WaterPenalty = 15f;
        private const float SoilPenalty = 10f;
        private const float AgingPenalty = 25f;
        private const float CareBonus = 10f;

        /// <summary>Doelgezondheid (0 tot 100) bij deze leeftijd en omgeving.</summary>
        public static int TargetHealth(int age, in HealthEnvironment env)
        {
            var aging = math.saturate((age - AgingFromAge) / (float)(Citizen.MaxAge - AgingFromAge)) * AgingPenalty;
            var target = Citizen.MaxScore - (env.Air * AirPenalty) - (env.Water * WaterPenalty) - (env.Soil * SoilPenalty) - aging
                + (env.HasCare ? CareBonus : 0f);
            return (int)math.clamp(math.round(target), 0f, Citizen.MaxScore);
        }

        /// <summary>Laat de gezondheid een jaar naar het doel bewegen.</summary>
        public static void UpdateHealth(ref Citizen citizen, in HealthEnvironment env)
        {
            var target = TargetHealth(citizen.Age, env);
            citizen.Health = (byte)(citizen.Health + math.clamp(target - citizen.Health, -YearlyStep, YearlyStep));
        }

        /// <summary>Factor op de sterftekans: 1 met zorg en vrije bedden, tot <see cref="MaxCareFactor"/> zonder zorg of bij overvolle bedden.</summary>
        public static float CareFactor(in HealthEnvironment env) =>
            env.HasCare ? math.min(math.max(env.BedLoad, 1f), MaxCareFactor) : MaxCareFactor;

        /// <summary>Sterftekans in het komende jaar, met zorg en bedden meegerekend.</summary>
        public static float DeathChance(LifeRules rules, in Citizen citizen, in HealthEnvironment env)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return math.saturate(rules.DeathChance(citizen.Age, citizen.Health) * CareFactor(env));
        }

        /// <summary>
        /// Eén levensjaar: de gezondheid beweegt naar het doel en daarna volgt de sterfteworp met de zorgfactor. Overleeft de burger dat, dan
        /// loopt de gewone levenscyclus (<see cref="CitizenLife.AgeOneYear"/>) door. Bij <see cref="LifeEvent.Died"/> moet de beller hem verwijderen.
        /// </summary>
        public static LifeEvent Yearly(ref Citizen citizen, LifeRules rules, in HealthEnvironment env, ref DeterministicRandom random)
        {
            UpdateHealth(ref citizen, env);

            // De basiskans zit al in AgeOneYear; hier komt alleen het extra risico door gebrek aan zorg bij.
            var extra = DeathChance(rules, citizen, env) - rules.DeathChance(citizen.Age, citizen.Health);
            if (extra > 0f && random.Chance(extra))
            {
                return LifeEvent.Died;
            }

            return CitizenLife.AgeOneYear(ref citizen, rules, ref random);
        }

        /// <summary>Aantal burgers dat in de zorg ligt (gezondheid onder <see cref="PatientBelow"/>).</summary>
        public static int Patients(IReadOnlyList<Citizen> citizens)
        {
            if (citizens is null)
            {
                throw new ArgumentNullException(nameof(citizens));
            }

            var patients = 0;
            for (var i = 0; i < citizens.Count; i++)
            {
                if (citizens[i].Health < PatientBelow)
                {
                    patients++;
                }
            }

            return patients;
        }

        /// <summary>Totaal aantal bedden van alle gezondheidsdiensten, naar hun financiering (<see cref="ServiceStation.Capacity"/>).</summary>
        public static int Beds(ServiceDispatcher dispatcher)
        {
            if (dispatcher is null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            var beds = 0;
            foreach (var station in dispatcher.Stations)
            {
                if (station.Kind == ServiceKind.Health)
                {
                    beds += station.Capacity * BedsPerVehicle;
                }
            }

            return beds;
        }

        /// <summary>Patiënten per bed; zonder bedden is elke patiënt te veel (oneindig), zonder patiënten is het 0.</summary>
        public static float BedLoad(int patients, int beds)
        {
            if (patients <= 0)
            {
                return 0f;
            }

            return beds <= 0 ? float.PositiveInfinity : patients / (float)beds;
        }
    }
}
