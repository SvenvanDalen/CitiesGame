using System;
using System.Collections.Generic;

namespace Cities.Simulation.Economy
{
    /// <summary>
    /// Aanleg, onderhoud en sloop van objecten (E11-03). Aanleg gaat via <see cref="Ledger.TrySpend"/> en mislukt zonder genoeg saldo;
    /// onderhoud is een verplichte weeklast (<see cref="Ledger.Force"/>) die uit de maandbedragen van de specs komt; bij sloop krijgt de stad
    /// <see cref="RefundShare"/> van de aanlegkosten terug. Kosten zijn al met de moeilijkheidsfactor vermenigvuldigd.
    /// </summary>
    public static class BuildingCosts
    {
        /// <summary>Deel van de aanlegkosten dat bij sloop terugkomt.</summary>
        public const double RefundShare = 0.5;

        /// <summary>Speldagen in een maand voor het omrekenen van maandelijks onderhoud naar een week.</summary>
        public const int DaysPerMonth = 30;

        public static bool Build(Ledger ledger, int week, long cost) => ledger.TrySpend(week, LedgerCategory.Construction, cost);

        /// <summary>Boekt de terugbetaling van een gesloopt object en geeft het bedrag terug.</summary>
        public static long Demolish(Ledger ledger, int week, long constructionCost)
        {
            var refund = RefundFor(constructionCost);
            ledger.Earn(week, LedgerCategory.Construction, refund);
            return refund;
        }

        public static long RefundFor(long constructionCost)
        {
            if (constructionCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(constructionCost), constructionCost, "De kosten kunnen niet negatief zijn.");
            }

            return (long)Math.Floor(constructionCost * RefundShare);
        }

        /// <summary>Onderhoud van één week bij een maandbedrag, naar boven afgerond zodat goedkoop onderhoud niet gratis wordt.</summary>
        public static long WeeklyUpkeep(long upkeepPerMonth)
        {
            if (upkeepPerMonth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upkeepPerMonth), upkeepPerMonth, "Het onderhoud kan niet negatief zijn.");
            }

            return ((upkeepPerMonth * Ledger.DaysPerWeek) + DaysPerMonth - 1) / DaysPerMonth;
        }

        /// <summary>Boekt het onderhoud van alle objecten voor <paramref name="week"/> en geeft het totaal terug.</summary>
        public static long PayUpkeep(Ledger ledger, int week, IEnumerable<long> upkeepPerMonth)
        {
            long total = 0;
            foreach (var monthly in upkeepPerMonth)
            {
                total += WeeklyUpkeep(monthly);
            }

            ledger.Force(week, LedgerCategory.Upkeep, total);
            return total;
        }
    }
}
