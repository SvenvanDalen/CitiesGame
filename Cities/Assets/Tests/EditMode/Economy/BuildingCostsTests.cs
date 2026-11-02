using System;
using Cities.Simulation.Economy;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class BuildingCostsTests
    {
        [Test]
        public void BuildingNeedsEnoughMoney()
        {
            var ledger = new Ledger(1000);

            Assert.That(BuildingCosts.Build(ledger, 0, 600), Is.True);
            Assert.That(BuildingCosts.Build(ledger, 0, 600), Is.False);
            Assert.That(ledger.Balance, Is.EqualTo(400));
            Assert.That(ledger.Expenses(0, LedgerCategory.Construction), Is.EqualTo(600));
        }

        [Test]
        public void DemolitionRefundsHalfRoundedDown()
        {
            var ledger = new Ledger(0);

            Assert.That(BuildingCosts.Demolish(ledger, 1, 1001), Is.EqualTo(500));
            Assert.That(ledger.Balance, Is.EqualTo(500));
            Assert.That(BuildingCosts.RefundFor(0), Is.Zero);
        }

        [Test]
        public void MonthlyUpkeepBecomesAWeeklyChargeRoundedUp()
        {
            Assert.That(BuildingCosts.WeeklyUpkeep(0), Is.Zero);
            Assert.That(BuildingCosts.WeeklyUpkeep(30), Is.EqualTo(7));
            Assert.That(BuildingCosts.WeeklyUpkeep(1), Is.EqualTo(1));
        }

        [Test]
        public void UpkeepIsMandatoryEvenWhenTheBalanceRunsNegative()
        {
            var ledger = new Ledger(10);

            var total = BuildingCosts.PayUpkeep(ledger, 2, new long[] { 30, 60 });

            Assert.That(total, Is.EqualTo(21));
            Assert.That(ledger.Balance, Is.EqualTo(-11));
            Assert.That(ledger.Expenses(2, LedgerCategory.Upkeep), Is.EqualTo(21));
        }

        [Test]
        public void NegativeAmountsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingCosts.RefundFor(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingCosts.WeeklyUpkeep(-1));
        }
    }
}
