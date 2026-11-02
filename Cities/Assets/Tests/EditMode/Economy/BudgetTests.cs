using System;
using Cities.Simulation.Economy;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Economy
{
    public class BudgetTests
    {
        private static ServiceStation Station(ServiceKind kind, long upkeep) => new ServiceStation(kind, float2.zero, 100f, 1, upkeep);

        private static readonly ServiceStation[] Stations =
        {
            Station(ServiceKind.Fire, 3000),
            Station(ServiceKind.Fire, 3000),
            Station(ServiceKind.Police, 6000),
        };

        [Test]
        public void TheDefaultBudgetChargesTheMonthlyUpkeepPerWeek()
        {
            var forecast = new Budget().Forecast(Stations, 10000);

            Assert.That(forecast.ServiceCosts[(int)ServiceKind.Fire], Is.EqualTo(1400), "6000 per maand is 1400 per week");
            Assert.That(forecast.ServiceCosts[(int)ServiceKind.Police], Is.EqualTo(1400));
            Assert.That(forecast.ServiceCosts[(int)ServiceKind.Health], Is.Zero);
            Assert.That(forecast.Expenses, Is.EqualTo(2800));
            Assert.That(forecast.Net, Is.EqualTo(7200));
            Assert.That(forecast.WeeksOfMoney(5000), Is.Null, "positief saldo per week loopt niet leeg");
        }

        [Test]
        public void TheSliderScalesTheCostsOfOneService()
        {
            var budget = new Budget();
            budget.SetFunding(ServiceKind.Fire, 0.5f);
            budget.SetFunding(ServiceKind.Police, 0f);

            Assert.That(budget.Funding(ServiceKind.Fire), Is.EqualTo(0.5f));
            Assert.That(budget.WeeklyCost(ServiceKind.Fire, Stations), Is.EqualTo(700));
            Assert.That(budget.WeeklyCost(ServiceKind.Police, Stations), Is.Zero);
        }

        [Test]
        public void ADeficitShowsHowLongTheMoneyLasts()
        {
            var forecast = new Budget().Forecast(Stations, 1000);

            Assert.That(forecast.Net, Is.EqualTo(-1800));
            Assert.That(forecast.WeeksOfMoney(9000), Is.EqualTo(5));
            Assert.That(forecast.WeeksOfMoney(-5), Is.Zero, "schuld: geen weken meer");
        }

        [Test]
        public void PayBooksTheServiceCostsAsAForcedExpense()
        {
            var ledger = new Ledger(1000);

            var paid = new Budget().Pay(ledger, 3, Stations);

            Assert.That(paid, Is.EqualTo(2800));
            Assert.That(ledger.Expenses(3, LedgerCategory.Services), Is.EqualTo(2800));
            Assert.That(ledger.Balance, Is.EqualTo(-1800), "verplicht, dus onder nul toegestaan");
        }

        [Test]
        public void BadInputIsRejected()
        {
            var budget = new Budget();

            Assert.Throws<ArgumentOutOfRangeException>(() => budget.SetFunding(ServiceKind.Fire, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => budget.SetFunding(ServiceKind.Fire, Budget.MaxFunding + 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => budget.SetFunding(ServiceKind.Fire, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => budget.Funding((ServiceKind)99));
            Assert.Throws<ArgumentNullException>(() => budget.WeeklyCost(ServiceKind.Fire, null));
            Assert.Throws<ArgumentNullException>(() => budget.Forecast(null, 0));
            Assert.Throws<ArgumentNullException>(() => budget.Pay(null, 0, Stations));
        }
    }
}
