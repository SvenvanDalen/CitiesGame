using System.Collections.Generic;
using System.Linq;
using Cities.Presentation.UI;
using Cities.Simulation.Economy;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.UI
{
    public class BudgetPanelModelTests
    {
        private TaxPolicy _taxes;
        private Budget _budget;
        private Ledger _ledger;
        private Loans _loans;
        private List<ServiceStation> _stations;
        private BudgetPanelModel _model;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _taxes = new TaxPolicy();
            _budget = new Budget();
            _ledger = new Ledger(100_000);
            _loans = new Loans(_ledger, 120_000);
            _stations = new List<ServiceStation>
            {
                new ServiceStation(ServiceKind.Fire, float2.zero, 100f, 4, 3000),
                new ServiceStation(ServiceKind.Police, float2.zero, 100f, 4, 6000),
            };
            _model = new BudgetPanelModel(_taxes, _budget, _loans, _ledger, () => _stations, () => 10_000, () => 3);
            _changes = 0;
            _model.Changed += () => _changes++;
        }

        [Test]
        public void ListsEveryTaxRateAndService()
        {
            var taxes = _model.TaxRows();
            var services = _model.ServiceRows();

            Assert.That(taxes.Count, Is.EqualTo(8));
            Assert.That(taxes.All(row => row.Rate.CompareTo(TaxPolicy.DefaultRate) == 0), Is.True);
            Assert.That(services.Count, Is.EqualTo(4));
            Assert.That(services.Single(row => row.Kind == ServiceKind.Fire).WeeklyCost, Is.EqualTo(700));
            Assert.That(services.Single(row => row.Kind == ServiceKind.Health).WeeklyCost, Is.Zero);
        }

        [Test]
        public void TaxStepsOnePercentAndStopsAtTheLimits()
        {
            Assert.That(_model.StepTax(TaxZone.Residential, TaxDensity.Low, 1), Is.True);
            Assert.That(_taxes.Rate(TaxZone.Residential, TaxDensity.Low), Is.EqualTo(0.10f).Within(1e-4f));
            Assert.That(_changes, Is.EqualTo(1));

            for (var i = 0; i < 40; i++)
            {
                _model.StepTax(TaxZone.Office, TaxDensity.High, 1);
            }

            Assert.That(_taxes.Rate(TaxZone.Office, TaxDensity.High), Is.EqualTo(TaxPolicy.MaxRate).Within(1e-4f));
            Assert.That(_model.StepTax(TaxZone.Office, TaxDensity.High, 1), Is.False, "al op het maximum");

            for (var i = 0; i < 40; i++)
            {
                _model.StepTax(TaxZone.Industrial, TaxDensity.Low, -1);
            }

            Assert.That(_taxes.Rate(TaxZone.Industrial, TaxDensity.Low), Is.Zero);
            Assert.That(_model.StepTax(TaxZone.Industrial, TaxDensity.Low, -1), Is.False, "al op nul");
        }

        [Test]
        public void FundingStepsAndReachesTheStations()
        {
            Assert.That(_model.StepFunding(ServiceKind.Fire, 1), Is.True);

            Assert.That(_budget.Funding(ServiceKind.Fire), Is.EqualTo(1.1f).Within(1e-4f));
            Assert.That(_stations[0].Funding, Is.EqualTo(1.1f).Within(1e-4f));
            Assert.That(_stations[1].Funding, Is.EqualTo(1f), "andere diensten blijven gelijk");

            for (var i = 0; i < 20; i++)
            {
                _model.StepFunding(ServiceKind.Police, -1);
            }

            Assert.That(_budget.Funding(ServiceKind.Police), Is.EqualTo(Budget.MinFunding).Within(1e-4f));
            Assert.That(_model.StepFunding(ServiceKind.Police, -1), Is.False);
        }

        [Test]
        public void ForecastFollowsTheSettings()
        {
            _model.StepFunding(ServiceKind.Fire, -1);

            var forecast = _model.Forecast();

            Assert.That(forecast.Income, Is.EqualTo(10_000));
            Assert.That(forecast.ServiceCosts[(int)ServiceKind.Fire], Is.EqualTo(630));
            Assert.That(forecast.Net, Is.EqualTo(10_000 - 630 - 1400));
        }

        [Test]
        public void BorrowAndRepayMoveOneStep()
        {
            Assert.That(_model.Borrow(), Is.True);
            Assert.That(_model.Debt, Is.EqualTo(BudgetPanelModel.LoanStep));
            Assert.That(_model.Balance, Is.EqualTo(150_000));
            Assert.That(_model.LoanRoom, Is.EqualTo(70_000));

            Assert.That(_model.Repay(), Is.True);
            Assert.That(_model.Debt, Is.Zero);
            Assert.That(_model.Balance, Is.EqualTo(100_000));
            Assert.That(_model.Repay(), Is.False, "geen schuld meer");
        }

        [Test]
        public void BorrowIsCappedByTheLimit()
        {
            Assert.That(_model.Borrow(), Is.True);
            Assert.That(_model.Borrow(), Is.True);
            Assert.That(_model.Borrow(), Is.True, "de laatste stap is kleiner dan 50.000");

            Assert.That(_model.Debt, Is.EqualTo(120_000));
            Assert.That(_model.Borrow(), Is.False);
        }

        [Test]
        public void CheatModeHasNoLimit()
        {
            var loans = new Loans(_ledger, 0, DebtMode.Cheat);
            var model = new BudgetPanelModel(_taxes, _budget, loans, _ledger, () => _stations, () => 0, () => 0);

            Assert.That(model.Borrow(), Is.True);
            Assert.That(loans.Debt, Is.EqualTo(BudgetPanelModel.LoanStep));
        }

        [Test]
        public void RepayIsLimitedByTheBalance()
        {
            _model.Borrow();
            _ledger.Force(3, LedgerCategory.Other, 140_000);

            Assert.That(_ledger.Balance, Is.EqualTo(10_000));
            Assert.That(_model.Repay(), Is.True);
            Assert.That(_model.Debt, Is.EqualTo(40_000));
            Assert.That(_model.Repay(), Is.False, "saldo is leeg");
        }

        [Test]
        public void ConstructorRejectsMissingParts()
        {
            Assert.Throws<System.ArgumentNullException>(() => new BudgetPanelModel(null, _budget, _loans, _ledger, () => _stations, () => 0, () => 0));
            Assert.Throws<System.ArgumentNullException>(() => new BudgetPanelModel(_taxes, null, _loans, _ledger, () => _stations, () => 0, () => 0));
            Assert.Throws<System.ArgumentNullException>(() => new BudgetPanelModel(_taxes, _budget, null, _ledger, () => _stations, () => 0, () => 0));
            Assert.Throws<System.ArgumentNullException>(() => new BudgetPanelModel(_taxes, _budget, _loans, null, () => _stations, () => 0, () => 0));
            Assert.Throws<System.ArgumentNullException>(() => new BudgetPanelModel(_taxes, _budget, _loans, _ledger, null, () => 0, () => 0));
            Assert.Throws<System.ArgumentNullException>(() => new BudgetPanelModel(_taxes, _budget, _loans, _ledger, () => _stations, null, () => 0));
            Assert.Throws<System.ArgumentNullException>(() => new BudgetPanelModel(_taxes, _budget, _loans, _ledger, () => _stations, () => 0, null));
        }
    }
}
