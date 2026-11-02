using System;
using Cities.Simulation.Citizens;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    public enum SchoolLevel : byte
    {
        Basic = 0,
        Secondary = 1,
        University = 2,
    }

    /// <summary>
    /// Een school (E10-06): basisschool, middelbare school of universiteit. Ze heeft een vaste capaciteit (<see cref="Capacity"/>) en een bereik
    /// (<see cref="Radius"/>); een leerling uit dat bereik krijgt een plek zolang er plaats is. Wie afstudeert krijgt het opleidingsniveau van de
    /// school (<see cref="Citizen.Education"/>, gelijk aan <see cref="SchoolLevel"/>). Welke school bij een burger past, bepaalt <see cref="LevelFor"/>.
    /// </summary>
    public sealed class School
    {
        /// <summary>Leeftijd waarop een kind naar de basisschool gaat.</summary>
        public const int BasicAge = 6;

        public const int SecondaryAge = 12;

        /// <summary>Tot deze leeftijd (exclusief) volgt iemand de universiteit.</summary>
        public const int UniversityMaxAge = 26;

        public School(SchoolLevel level, float2 position, float radius, int capacity, long upkeepPerMonth)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Het bereik moet groter dan nul zijn.");
            }

            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Een school heeft minstens één plek nodig.");
            }

            if (upkeepPerMonth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upkeepPerMonth), upkeepPerMonth, "De onderhoudskosten kunnen niet negatief zijn.");
            }

            Level = level;
            Position = position;
            Radius = radius;
            Capacity = capacity;
            UpkeepPerMonth = upkeepPerMonth;
        }

        public SchoolLevel Level { get; }

        public float2 Position { get; }

        public float Radius { get; }

        public int Capacity { get; }

        public long UpkeepPerMonth { get; }

        public int Enrolled { get; private set; }

        public bool IsFull => Enrolled >= Capacity;

        public static School Elementary(float2 position) => new School(SchoolLevel.Basic, position, 600f, 200, 30000);

        public static School HighSchool(float2 position) => new School(SchoolLevel.Secondary, position, 1200f, 400, 60000);

        public static School College(float2 position) => new School(SchoolLevel.University, position, 3000f, 1000, 250000);

        /// <summary>Welke school past bij deze burger, of null als hij niet (meer) naar school hoort.</summary>
        public static SchoolLevel? LevelFor(in Citizen citizen)
        {
            if (citizen.Age >= BasicAge && citizen.Age < SecondaryAge && citizen.Education == 0)
            {
                return SchoolLevel.Basic;
            }

            if (citizen.Age >= SecondaryAge && citizen.Age < Citizen.AdultAge && citizen.Education <= (byte)SchoolLevel.Basic)
            {
                return SchoolLevel.Secondary;
            }

            if (citizen.Age >= Citizen.AdultAge && citizen.Age < UniversityMaxAge && citizen.Education == (byte)SchoolLevel.Secondary)
            {
                return SchoolLevel.University;
            }

            return null;
        }

        public bool Covers(float2 point) => math.lengthsq(point - Position) <= Radius * Radius;

        /// <summary>Schrijft een leerling in als hij binnen bereik woont en er plaats is; false anders.</summary>
        public bool TryEnroll(float2 home)
        {
            if (IsFull || !Covers(home))
            {
                return false;
            }

            Enrolled++;
            return true;
        }

        /// <summary>De leerling verlaat de school; met <paramref name="graduated"/> krijgt hij het niveau van de school.</summary>
        public void Leave(ref Citizen citizen, bool graduated)
        {
            if (Enrolled == 0)
            {
                throw new InvalidOperationException("Er zijn geen leerlingen om uit te schrijven.");
            }

            Enrolled--;
            if (graduated)
            {
                citizen.Education = (byte)Level;
            }
        }
    }
}
