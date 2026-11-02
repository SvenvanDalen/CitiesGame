using System;
using Cities.Simulation.Economy;
using Cities.Simulation.Transit;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class TransitFinanceTests
    {
        private TransitNetwork _network;
        private TransitFleet _fleet;
        private TransitFinance _finance;
        private int _a;
        private int _line;

        [SetUp]
        public void SetUp()
        {
            _network = new TransitNetwork();
            _a = _network.AddStop(new float2(0f, 0f));
            var b = _network.AddStop(new float2(100f, 0f));
            _line = _network.AddLine("1", new[] { _a, b }, false, 2, 60f);
            _fleet = new TransitFleet(_network);
            _fleet.AddDepot(5);
            _finance = new TransitFinance(_network, _fleet);
        }

        private void Ride(int passengers)
        {
            _fleet.AddWaiting(_a, passengers);
            for (var t = 0; t < 200; t++)
            {
                _fleet.Tick(1f);
            }
        }

        [Test]
        public void BoardingsEarnTheFareAndTheVehiclesCostUpkeep()
        {
            var ledger = new Ledger(1000);
            Ride(10);

            var report = _finance.Settle(0, ledger)[0];

            Assert.That(report.Boardings, Is.EqualTo(10));
            Assert.That(report.Revenue, Is.EqualTo(10 * TransitFinance.DefaultFare));
            Assert.That(report.Cost, Is.EqualTo(2 * TransitFinance.UpkeepPerVehiclePerWeek));
            Assert.That(report.Profit, Is.EqualTo(report.Revenue - report.Cost));
            Assert.That(ledger.Income(0, LedgerCategory.Trade), Is.EqualTo(report.Revenue));
            Assert.That(ledger.Expenses(0, LedgerCategory.Upkeep), Is.EqualTo(report.Cost));
        }

        [Test]
        public void OnlyNewBoardingsArePaidAndTheFareIsPerLine()
        {
            var ledger = new Ledger(0);
            _finance.SetFare(_line, 5);
            Ride(4);
            _finance.Settle(0, ledger);

            var second = _finance.Settle(1, ledger)[0];

            Assert.That(_finance.FareOf(_line), Is.EqualTo(5));
            Assert.That(second.Boardings, Is.Zero, "dezelfde instappers worden niet twee keer betaald");
            Assert.That(second.Revenue, Is.Zero);
            Assert.That(ledger.Income(0), Is.EqualTo(20));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _finance.SetFare(_line, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _finance.SetFare(_line, TransitFinance.MaxFare + 1));
            Assert.Throws<ArgumentException>(() => _finance.SetFare(99, 1));
            Assert.Throws<ArgumentNullException>(() => _finance.Settle(0, null));
            Assert.Throws<ArgumentNullException>(() => new TransitFinance(null, _fleet));
            Assert.Throws<ArgumentNullException>(() => new TransitFinance(_network, null));
        }
    }
}
