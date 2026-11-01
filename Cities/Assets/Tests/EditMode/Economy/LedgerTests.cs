using System;
using Cities.Simulation.Economy;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class LedgerTests
    {
        [Test]
        public void IncomeAndSpendingMoveTheBalanceAndAreBookedPerCategory()
        {
            var ledger = new Ledger(1000);

            ledger.Earn(0, LedgerCategory.Taxes, 500);
            Assert.That(ledger.TrySpend(0, LedgerCategory.Construction, 300), Is.True);

            Assert.That(ledger.Balance, Is.EqualTo(1200));
            Assert.That(ledger.Income(0), Is.EqualTo(500));
            Assert.That(ledger.Expenses(0), Is.EqualTo(300));
            Assert.That(ledger.Net(0), Is.EqualTo(200));
            Assert.That(ledger.Income(0, LedgerCategory.Taxes), Is.EqualTo(500));
            Assert.That(ledger.Income(0, LedgerCategory.Trade), Is.Zero);
            Assert.That(ledger.Expenses(0, LedgerCategory.Construction), Is.EqualTo(300));
        }

        [Test]
        public void ASpendBeyondTheBalanceIsRefusedAndNotBooked()
        {
            var ledger = new Ledger(100);

            Assert.That(ledger.CanAfford(100), Is.True);
            Assert.That(ledger.CanAfford(101), Is.False);
            Assert.That(ledger.CanAfford(-1), Is.False);
            Assert.That(ledger.TrySpend(0, LedgerCategory.Construction, 101), Is.False);

            Assert.That(ledger.Balance, Is.EqualTo(100));
            Assert.That(ledger.Expenses(0), Is.Zero);
        }

        [Test]
        public void MandatoryCostsMayTakeTheBalanceBelowZero()
        {
            var ledger = new Ledger(50);

            ledger.Force(0, LedgerCategory.Interest, 80);

            Assert.That(ledger.Balance, Is.EqualTo(-30));
            Assert.That(ledger.Expenses(0, LedgerCategory.Interest), Is.EqualTo(80));
            Assert.That(ledger.TrySpend(0, LedgerCategory.Other, 1), Is.False, "met schuld mag je niets meer uitgeven");
        }

        [Test]
        public void WeeksAreSeparateAndFollowTheDays()
        {
            var ledger = new Ledger(0);

            ledger.Earn(Ledger.WeekOf(0), LedgerCategory.Taxes, 10);
            ledger.Earn(Ledger.WeekOf(6), LedgerCategory.Taxes, 5);
            ledger.Earn(Ledger.WeekOf(7), LedgerCategory.Taxes, 7);

            Assert.That(ledger.Income(0), Is.EqualTo(15));
            Assert.That(ledger.Income(1), Is.EqualTo(7));
            Assert.That(ledger.Income(2), Is.Zero);
            Assert.That(Ledger.WeekOf(-3), Is.Zero);
        }

        [Test]
        public void OldWeeksFallOutOfTheHistory()
        {
            var ledger = new Ledger(0);
            ledger.Earn(0, LedgerCategory.Taxes, 10);
            ledger.Force(0, LedgerCategory.Upkeep, 1);

            ledger.Earn(Ledger.HistoryWeeks, LedgerCategory.Taxes, 1);

            Assert.That(ledger.Income(0), Is.Zero, "week 0 is een volle historie geleden");
            Assert.That(ledger.Expenses(0), Is.Zero);
            Assert.That(ledger.Balance, Is.EqualTo(10), "het saldo blijft kloppen");
            Assert.That(ledger.Income(Ledger.HistoryWeeks), Is.EqualTo(1));
        }

        [Test]
        public void NegativeAmountsAreRejected()
        {
            var ledger = new Ledger(0);

            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Earn(0, LedgerCategory.Taxes, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.TrySpend(0, LedgerCategory.Taxes, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Force(0, LedgerCategory.Taxes, -1));
        }
    }
}
