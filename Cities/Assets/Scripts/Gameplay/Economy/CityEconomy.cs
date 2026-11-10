using System.Collections.Generic;
using Cities.Gameplay.NewCity;
using Cities.Gameplay.Population;
using Cities.Simulation.Economy;
using Cities.Simulation.Services;

namespace Cities.Gameplay.Economy
{
    /// <summary>
    /// De geldstroom van de stad (E26-06): saldo, belastingen, dienstbudget en leningen op één plek. Elke spelweek int de stad belasting van
    /// wonen, winkels, industrie en kantoren, betaalt ze de diensten en het onderhoud van gebouwen, en rekent de lening door.
    /// Het startkapitaal volgt de moeilijkheid.
    /// </summary>
    public sealed class CityEconomy
    {
        /// <summary>Belastbare waarde per inwoner per week, in euro.</summary>
        public const long TaxableValuePerResident = 500;

        /// <summary>Belastbare waarde per arbeidsplaats in industrie en kantoren per week.</summary>
        public const long TaxableValuePerJob = 400;

        public const long LoanLimit = 1_000_000;

        private readonly List<ServiceStation> _stations = new List<ServiceStation>();
        private long _upkeepPerMonth;

        public CityEconomy(Difficulty difficulty)
        {
            Settings = DifficultySettings.For(difficulty);
            Ledger = new Ledger(Settings.StartMoney) { UnlimitedMoney = Settings.UnlimitedMoney };
            Taxes = new TaxPolicy();
            Budget = new Budget();
            Loans = new Loans(Ledger, LoanLimit);
        }

        public DifficultySettings Settings { get; }

        public Ledger Ledger { get; }

        public TaxPolicy Taxes { get; }

        public Budget Budget { get; }

        public Loans Loans { get; }

        public IReadOnlyList<ServiceStation> Stations => _stations;

        public long Balance => Ledger.Balance;

        /// <summary>Totaal onderhoud van de gebouwen per maand.</summary>
        public long UpkeepPerMonth => _upkeepPerMonth;

        public void AddStation(ServiceStation station)
        {
            _stations.Add(station);
            Budget.Apply(_stations);
        }

        public void RemoveStation(ServiceStation station)
        {
            if (_stations.Remove(station))
            {
                Budget.Apply(_stations);
            }
        }

        public void AddUpkeep(long perMonth) => _upkeepPerMonth += perMonth;

        /// <summary>Kosten na de moeilijkheidsfactor.</summary>
        public long Cost(long baseCost) => Settings.Cost(baseCost);

        /// <summary>Boekt een uitgave voor bouwen; <c>false</c> bij te weinig geld.</summary>
        public bool TryBuild(int week, long cost) => BuildingCosts.Build(Ledger, week, cost);

        public bool CanAfford(long cost) => Ledger.CanAfford(cost);

        /// <summary>Wat de stad deze week zou ophalen aan belasting.</summary>
        public long WeeklyTaxIncome(CityPopulation population)
        {
            return Taxes.Revenue(TaxZone.Residential, TaxDensity.Low, population.Population * TaxableValuePerResident)
                + Taxes.Revenue(TaxZone.Commercial, TaxDensity.Low, population.ShopCapacity * 7)
                + Taxes.Revenue(TaxZone.Industrial, TaxDensity.Low, population.Jobs * TaxableValuePerJob / 2)
                + Taxes.Revenue(TaxZone.Office, TaxDensity.Low, population.Jobs * TaxableValuePerJob / 2);
        }

        /// <summary>Sluit een spelweek af: belasting binnen, diensten en onderhoud betaald, lening doorgerekend.</summary>
        public void EndWeek(int week, CityPopulation population)
        {
            Taxes.Collect(Ledger, week, TaxZone.Residential, TaxDensity.Low, population.Population * TaxableValuePerResident);
            Taxes.Collect(Ledger, week, TaxZone.Commercial, TaxDensity.Low, population.ShopCapacity * 7);
            Taxes.Collect(Ledger, week, TaxZone.Industrial, TaxDensity.Low, population.Jobs * TaxableValuePerJob / 2);
            Taxes.Collect(Ledger, week, TaxZone.Office, TaxDensity.Low, population.Jobs * TaxableValuePerJob / 2);
            Budget.Pay(Ledger, week, _stations);
            var upkeep = BuildingCosts.WeeklyUpkeep(Settings.Cost(_upkeepPerMonth));
            if (upkeep > 0)
            {
                Ledger.Force(week, LedgerCategory.Upkeep, upkeep);
            }

            Loans.EndWeek(week);
        }
    }
}
