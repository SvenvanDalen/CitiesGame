using System;
using System.Collections.Generic;

namespace Cities.Simulation.Citizens
{
    /// <summary>Uitkomst van <see cref="DemographicStatistics.Compute"/>; alle tellingen zijn aantallen burgers.</summary>
    public sealed class DemographicReport
    {
        public DemographicReport(int[] ageBands, int adults, int unemployed, int[] education, int[] income)
        {
            AgeBands = ageBands;
            Adults = adults;
            Unemployed = unemployed;
            Education = education;
            Income = income;
        }

        /// <summary>Leeftijdspiramide: burgers per band van <see cref="DemographicStatistics.AgeBandYears"/> jaar, jongste band eerst.</summary>
        public int[] AgeBands { get; }

        /// <summary>Volwassenen (18 tot pensioen): de beroepsbevolking.</summary>
        public int Adults { get; }

        /// <summary>Volwassenen zonder baan.</summary>
        public int Unemployed { get; }

        /// <summary>Volwassenen en gepensioneerden per opleidingsniveau (0 laag, 1 middelbaar, 2 hoog).</summary>
        public int[] Education { get; }

        /// <summary>Volwassenen per inkomensklasse van <see cref="DemographicStatistics.IncomeBracketEdges"/>; klasse 0 is geen inkomen.</summary>
        public int[] Income { get; }

        /// <summary>Werkloosheid als aandeel van de volwassenen (0 tot 1); 0 zonder volwassenen.</summary>
        public float UnemploymentRate => Adults == 0 ? 0f : (float)Unemployed / Adults;
    }

    /// <summary>Demografische statistieken (E07-11): leeftijdspiramide, werkloosheid, opleiding en inkomensverdeling over alle burgers. Alleen lezen; er is geen eigen toestand om op te slaan.</summary>
    public static class DemographicStatistics
    {
        public const int AgeBandYears = 5;

        // shortcut: vaste klassegrenzen op weekinkomen (geen inkomen / laag / midden / hoog); upgrade naar data zodra lonen uit E11 komen.
        public static readonly int[] IncomeBracketEdges = { 1, 400, 700 };

        public static DemographicReport Compute(IReadOnlyList<Citizen> citizens)
        {
            var ageBands = new int[(Citizen.MaxAge / AgeBandYears) + 1];
            var education = new int[Citizen.MaxEducation + 1];
            var income = new int[IncomeBracketEdges.Length + 1];
            var adults = 0;
            var unemployed = 0;

            for (var i = 0; i < citizens.Count; i++)
            {
                var citizen = citizens[i];
                ageBands[Math.Min((int)citizen.Age, Citizen.MaxAge) / AgeBandYears]++;
                if (citizen.Group == AgeGroup.Child)
                {
                    continue;
                }

                education[Math.Min((int)citizen.Education, Citizen.MaxEducation)]++;
                if (citizen.Group != AgeGroup.Adult)
                {
                    continue;
                }

                adults++;
                unemployed += citizen.HasWork ? 0 : 1;
                var bracket = 0;
                var weekly = EducationEconomy.WeeklyIncome(citizen);
                while (bracket < IncomeBracketEdges.Length && weekly >= IncomeBracketEdges[bracket])
                {
                    bracket++;
                }

                income[bracket]++;
            }

            return new DemographicReport(ageBands, adults, unemployed, education, income);
        }
    }
}
