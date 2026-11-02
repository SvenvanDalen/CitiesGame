using System;
using Cities.Simulation.Economy;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class LoansTests
    {
        [Test]
        public void BorrowingAddsMoneyAndDebtUpToTheLimit()
        {
            var ledger = new Ledger(0);
            var loans = new Loans(ledger, 1000);

            Assert.That(loans.Borrow(0, 600), Is.True);
            Assert.That(loans.Borrow(0, 500), Is.False, "boven de limiet");
            Assert.That(ledger.Balance, Is.EqualTo(600));
            Assert.That(loans.Debt, Is.EqualTo(600));
        }

        [Test]
        public void TheCheatModeIgnoresTheLimit()
        {
            var loans = new Loans(new Ledger(0), 10, DebtMode.Cheat);

            Assert.That(loans.Borrow(0, 1_000_000), Is.True);
        }

        [Test]
        public void EachWeekCostsInterestAndRepaysAPart()
        {
            var ledger = new Ledger(0);
            var loans = new Loans(ledger, 100_000);
            loans.Borrow(0, 100_000);

            loans.EndWeek(1);

            Assert.That(ledger.Expenses(1, LedgerCategory.Interest), Is.EqualTo(140));
            Assert.That(loans.Debt, Is.EqualTo(99_000), "1% afgelost");
            Assert.That(ledger.Balance, Is.EqualTo(100_000 - 140 - 1000));
        }

        [Test]
        public void RepaymentIsLimitedByTheDebtAndTheBalance()
        {
            var ledger = new Ledger(0);
            var loans = new Loans(ledger, 1000);
            loans.Borrow(0, 1000);
            ledger.Force(0, LedgerCategory.Other, 900);

            Assert.That(loans.Repay(0, 5000), Is.EqualTo(100), "alleen wat het saldo toelaat");
            Assert.That(loans.Repay(0, 5000), Is.Zero, "saldo is op");
            Assert.That(loans.Debt, Is.EqualTo(900));
        }

        [Test]
        public void StayingInTheRedTooLongMeansBankruptcy()
        {
            var ledger = new Ledger(0);
            var loans = new Loans(ledger, 1000);
            ledger.Force(0, LedgerCategory.Upkeep, 10);

            for (var week = 1; week < Loans.BankruptAfterWeeks; week++)
            {
                loans.EndWeek(week);
            }

            Assert.That(loans.IsBankrupt, Is.False);
            loans.EndWeek(Loans.BankruptAfterWeeks);
            Assert.That(loans.IsBankrupt, Is.True);
            Assert.That(loans.Borrow(9, 1), Is.False, "een failliete stad leent niet meer");
        }

        [Test]
        public void BeingBackInTheBlackResetsTheCountdownAndCheatNeverGoesBankrupt()
        {
            var ledger = new Ledger(0);
            var loans = new Loans(ledger, 1000);
            ledger.Force(0, LedgerCategory.Upkeep, 10);
            loans.EndWeek(1);
            ledger.Earn(1, LedgerCategory.Taxes, 100);
            loans.EndWeek(2);
            Assert.That(loans.IsBankrupt, Is.False);

            var cheatLedger = new Ledger(-1);
            var cheat = new Loans(cheatLedger, 0, DebtMode.Cheat);
            for (var week = 0; week < 20; week++)
            {
                cheat.EndWeek(week);
            }

            Assert.That(cheat.IsBankrupt, Is.False);
        }

        [Test]
        public void BadInputIsRejected()
        {
            var loans = new Loans(new Ledger(0), 10);

            Assert.Throws<ArgumentNullException>(() => new Loans(null, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Loans(new Ledger(0), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => loans.Borrow(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => loans.Repay(0, -1));
        }
    }
}
