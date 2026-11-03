using System;
using System.Collections.Generic;
using Cities.Simulation.Economy;
using Cities.Simulation.Services;

namespace Cities.Presentation.UI
{
    public readonly struct TaxRow
    {
        public TaxRow(TaxZone zone, TaxDensity density, float rate)
        {
            Zone = zone;
            Density = density;
            Rate = rate;
        }

        public TaxZone Zone { get; }

        public TaxDensity Density { get; }

        public float Rate { get; }
    }

    public readonly struct ServiceRow
    {
        public ServiceRow(ServiceKind kind, float funding, long weeklyCost)
        {
            Kind = kind;
            Funding = funding;
            WeeklyCost = weeklyCost;
        }

        public ServiceKind Kind { get; }

        public float Funding { get; }

        public long WeeklyCost { get; }
    }

    /// <summary>
    /// Het budgetpaneel (E18-06): belastingtarieven, financieringsniveau per dienst, lening en weekprognose als één model boven
    /// <see cref="TaxPolicy"/>, <see cref="Budget"/>, <see cref="Loans"/> en <see cref="Ledger"/>. Het paneel stapt met knoppen in vaste
    /// stappen; het model houdt de waarden binnen de grenzen van de onderliggende systemen en meldt elke wijziging via <see cref="Changed"/>.
    /// </summary>
    public sealed class BudgetPanelModel
    {
        public const float TaxStep = 0.01f;

        public const float FundingStep = 0.1f;

        public const long LoanStep = 50_000L;

        private static readonly TaxZone[] Zones = { TaxZone.Residential, TaxZone.Commercial, TaxZone.Industrial, TaxZone.Office };
        private static readonly TaxDensity[] Densities = { TaxDensity.Low, TaxDensity.High };
        private static readonly ServiceKind[] Kinds = { ServiceKind.Fire, ServiceKind.Police, ServiceKind.Health, ServiceKind.Death };

        private readonly TaxPolicy _taxes;
        private readonly Budget _budget;
        private readonly Loans _loans;
        private readonly Ledger _ledger;
        private readonly Func<IEnumerable<ServiceStation>> _stations;
        private readonly Func<long> _weeklyIncome;
        private readonly Func<int> _week;

        public BudgetPanelModel(
            TaxPolicy taxes,
            Budget budget,
            Loans loans,
            Ledger ledger,
            Func<IEnumerable<ServiceStation>> stations,
            Func<long> weeklyIncome,
            Func<int> week)
        {
            _taxes = taxes ?? throw new ArgumentNullException(nameof(taxes));
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _loans = loans ?? throw new ArgumentNullException(nameof(loans));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _stations = stations ?? throw new ArgumentNullException(nameof(stations));
            _weeklyIncome = weeklyIncome ?? throw new ArgumentNullException(nameof(weeklyIncome));
            _week = week ?? throw new ArgumentNullException(nameof(week));
        }

        public event Action Changed;

        public long Balance => _ledger.Balance;

        public long Debt => _loans.Debt;

        public long LoanRoom => Math.Max(0L, _loans.Limit - _loans.Debt);

        public IReadOnlyList<TaxRow> TaxRows()
        {
            var rows = new List<TaxRow>(Zones.Length * Densities.Length);
            foreach (var zone in Zones)
            {
                foreach (var density in Densities)
                {
                    rows.Add(new TaxRow(zone, density, _taxes.Rate(zone, density)));
                }
            }

            return rows;
        }

        public IReadOnlyList<ServiceRow> ServiceRows()
        {
            var stations = new List<ServiceStation>(_stations());
            var rows = new List<ServiceRow>(Kinds.Length);
            foreach (var kind in Kinds)
            {
                rows.Add(new ServiceRow(kind, _budget.Funding(kind), _budget.WeeklyCost(kind, stations)));
            }

            return rows;
        }

        public BudgetForecast Forecast() => _budget.Forecast(_stations(), _weeklyIncome());

        /// <summary>Verhoogt (+1) of verlaagt (-1) het tarief met één stap; geeft <c>false</c> als de grens al bereikt is.</summary>
        public bool StepTax(TaxZone zone, TaxDensity density, int direction)
        {
            var current = _taxes.Rate(zone, density);
            var target = Snap(current + (Math.Sign(direction) * TaxStep), TaxStep, 0f, TaxPolicy.MaxRate);
            if (target.CompareTo(current) == 0)
            {
                return false;
            }

            _taxes.SetRate(zone, density, target);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Verhoogt (+1) of verlaagt (-1) het financieringsniveau van een dienst; zet de stations meteen op het nieuwe niveau.</summary>
        public bool StepFunding(ServiceKind kind, int direction)
        {
            var current = _budget.Funding(kind);
            var target = Snap(current + (Math.Sign(direction) * FundingStep), FundingStep, Budget.MinFunding, Budget.MaxFunding);
            if (target.CompareTo(current) == 0)
            {
                return false;
            }

            _budget.SetFunding(kind, target);
            _budget.Apply(_stations());
            Changed?.Invoke();
            return true;
        }

        /// <summary>Leent één stap (of minder als de limiet dat niet toelaat); <c>false</c> als er niets te lenen valt.</summary>
        public bool Borrow()
        {
            var amount = _loans.Mode == DebtMode.Cheat ? LoanStep : Math.Min(LoanStep, LoanRoom);
            if (amount <= 0 || !_loans.Borrow(_week(), amount))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Lost één stap af, beperkt tot de schuld en het saldo; <c>false</c> als er niets af te lossen valt.</summary>
        public bool Repay()
        {
            var amount = Math.Min(LoanStep, Math.Min(_loans.Debt, Math.Max(0L, _ledger.Balance)));
            if (amount <= 0 || _loans.Repay(_week(), amount) <= 0)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        private static float Snap(float value, float step, float min, float max)
        {
            var snapped = (float)Math.Round(value / step) * step;
            return Math.Min(max, Math.Max(min, (float)Math.Round(snapped, 4)));
        }
    }
}
