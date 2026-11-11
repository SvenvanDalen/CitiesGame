using System;
using Unity.Mathematics;

namespace Cities.Simulation.Attractions
{
    /// <summary>
    /// Campus (E25-06): faculteiten, sportteams en studentenleven. Studenten volgen uit faculteiten, onderzoek levert een bonus op de
    /// opbrengst van kennisbedrijven (hooguit +15 %) en sportteams brengen wedstrijdinkomsten.
    /// </summary>
    public sealed class CampusLife
    {
        public const int StudentsPerFaculty = 800;

        public const int MaxFaculties = 8;

        public int Faculties { get; private set; }

        public int SportTeams { get; private set; }

        public int Students => Faculties * StudentsPerFaculty;

        public void AddFaculty()
        {
            if (Faculties >= MaxFaculties)
            {
                throw new InvalidOperationException("De campus heeft al het maximum aan faculteiten.");
            }

            Faculties++;
        }

        public void AddSportTeam() => SportTeams++;

        /// <summary>Bonus op kennisbedrijven: 2 % per faculteit, maximaal 15 %.</summary>
        public float ResearchBonus => math.min(0.15f, Faculties * 0.02f);

        /// <summary>Inkomsten per wedstrijddag: 500 per team plus 2 per student.</summary>
        public long MatchDayIncome => (SportTeams * 500L) + (Students * 2L);
    }
}
