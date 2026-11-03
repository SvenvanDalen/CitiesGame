using System;
using System.Collections.Generic;

namespace Cities.Simulation.Citizens
{
    /// <summary>
    /// Wat opleiding oplevert (E07-08): het weekloon per niveau (basis, middelbaar, universiteit) en de verdeling van de banen van een
    /// werkplek over die niveaus, zodat de vacatures van <c>JobMatcher</c> een minimale opleiding krijgen. Wie geen werk heeft verdient niets.
    /// </summary>
    public static class EducationEconomy
    {
        // shortcut: vaste schattingen voor het weekloon; upgrade naar data in een definitie zodra de economie (E11) kalibratie krijgt.
        private static readonly int[] WeeklyWages = { 300, 520, 900 };

        /// <summary>Weekloon bij een opleiding van 0 tot <see cref="Citizen.MaxEducation"/>.</summary>
        public static int Wage(byte education)
        {
            if (education > Citizen.MaxEducation)
            {
                throw new ArgumentOutOfRangeException(nameof(education), education, "Onbekend opleidingsniveau.");
            }

            return WeeklyWages[education];
        }

        /// <summary>Weekinkomen van één burger: zijn loon als hij werkt, anders niets.</summary>
        public static int WeeklyIncome(in Citizen citizen) => citizen.HasWork ? Wage(citizen.Education) : 0;

        /// <summary>Totaal weekinkomen van alle burgers; de basis voor de belasting.</summary>
        public static long TotalWeeklyIncome(IReadOnlyList<Citizen> citizens)
        {
            long total = 0;
            for (var i = 0; i < citizens.Count; i++)
            {
                total += WeeklyIncome(citizens[i]);
            }

            return total;
        }

        /// <summary>
        /// Verdeelt <paramref name="slots"/> banen over de drie niveaus naar de aandelen <paramref name="low"/>, <paramref name="mid"/> en <paramref name="high"/>.
        /// Het restant gaat naar het grootste afgeronde deel, zodat de som altijd klopt.
        /// </summary>
        public static int[] SplitJobs(int slots, float low, float mid, float high)
        {
            if (slots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slots), slots, "Het aantal banen kan niet negatief zijn.");
            }

            var shares = new[] { low, mid, high };
            var sum = 0f;
            foreach (var share in shares)
            {
                if (!(share >= 0f))
                {
                    throw new ArgumentOutOfRangeException(nameof(low), share, "Een aandeel kan niet negatief zijn.");
                }

                sum += share;
            }

            if (sum <= 0f)
            {
                throw new ArgumentException("Minstens één aandeel moet groter dan nul zijn.", nameof(low));
            }

            var jobs = new int[shares.Length];
            var given = 0;
            var largest = 0;
            for (var i = 0; i < shares.Length; i++)
            {
                jobs[i] = (int)(slots * (shares[i] / sum));
                given += jobs[i];
                largest = shares[i] > shares[largest] ? i : largest;
            }

            jobs[largest] += slots - given;
            return jobs;
        }
    }
}
