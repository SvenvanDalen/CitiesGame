using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Services;

namespace Cities.Simulation.Economy
{
    /// <summary>Weekoverzicht van de begroting: verwachte inkomsten, uitgaven en wat er per dienst naartoe gaat.</summary>
    public readonly struct BudgetForecast
    {
        public BudgetForecast(long income, long[] serviceCosts)
        {
            Income = income;
            ServiceCosts = serviceCosts;
            Expenses = serviceCosts.Sum();
        }

        public long Income { get; }

        public long Expenses { get; }

        /// <summary>Weekkosten per <see cref="ServiceKind"/>, in de volgorde van de enum.</summary>
        public long[] ServiceCosts { get; }

        public long Net => Income - Expenses;

        /// <summary>Hoeveel weken het saldo het uithoudt bij dit tempo; <c>null</c> als de stad niet leeg loopt.</summary>
        public int? WeeksOfMoney(long balance) => Net >= 0 ? (int?)null : (int)Math.Min(int.MaxValue, Math.Max(0L, balance) / -Net);
    }

    /// <summary>
    /// De begroting (E11-04): per dienst een financieringsniveau (0 tot <see cref="MaxFunding"/>, 1 is de normale begroting) dat de onderhoudskosten
    /// van de dienstgebouwen schaalt, en een weekprognose uit die kosten en de verwachte belastingopbrengst. <see cref="Pay"/> boekt de kosten van een week.
    /// </summary>
    public sealed class Budget
    {
        public const float DefaultFunding = 1f;

        public const float MaxFunding = 1.5f;

        /// <summary>De onderhoudskosten van een station gelden per maand; een maand telt hier 30 dagen.</summary>
        public const float DaysPerMonth = 30f;

        private static readonly int Kinds = Enum.GetValues(typeof(ServiceKind)).Length;

        private readonly float[] _funding;

        public Budget()
        {
            _funding = Enumerable.Repeat(DefaultFunding, Kinds).ToArray();
        }

        public float Funding(ServiceKind kind) => _funding[IndexOf(kind)];

        public void SetFunding(ServiceKind kind, float funding)
        {
            if (!(funding >= 0f && funding <= MaxFunding))
            {
                throw new ArgumentOutOfRangeException(nameof(funding), funding, "Het financieringsniveau ligt tussen 0 en " + MaxFunding + ".");
            }

            _funding[IndexOf(kind)] = funding;
        }

        /// <summary>Weekkosten van alle stations van één soort bij het ingestelde niveau, afgerond op hele euro's.</summary>
        public long WeeklyCost(ServiceKind kind, IEnumerable<ServiceStation> stations)
        {
            if (stations is null)
            {
                throw new ArgumentNullException(nameof(stations));
            }

            var monthly = stations.Where(s => s.Kind == kind).Sum(s => s.UpkeepPerMonth);
            return (long)Math.Round(monthly * (double)Funding(kind) * Ledger.DaysPerWeek / DaysPerMonth);
        }

        public BudgetForecast Forecast(IEnumerable<ServiceStation> stations, long weeklyIncome)
        {
            var list = stations?.ToList() ?? throw new ArgumentNullException(nameof(stations));
            var costs = Enumerable.Range(0, Kinds).Select(i => WeeklyCost((ServiceKind)i, list)).ToArray();
            return new BudgetForecast(weeklyIncome, costs);
        }

        /// <summary>Boekt de dienstkosten van <paramref name="week"/> als verplichte uitgave; geeft het totaal terug.</summary>
        public long Pay(Ledger ledger, int week, IEnumerable<ServiceStation> stations)
        {
            if (ledger is null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var total = Forecast(stations, 0L).Expenses;
            ledger.Force(week, LedgerCategory.Services, total);
            return total;
        }

        private static int IndexOf(ServiceKind kind)
        {
            if ((uint)kind >= Kinds)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende dienst.");
            }

            return (int)kind;
        }
    }
}
