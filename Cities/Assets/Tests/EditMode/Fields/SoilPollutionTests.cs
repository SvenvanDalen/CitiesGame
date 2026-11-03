using System;
using Cities.Simulation.Economy;
using Cities.Simulation.Fields;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public class SoilPollutionTests
    {
        [Test]
        public void PollutionBuildsUpAndHardlyGoesAway()
        {
            var soil = new SoilPollution(4, 4);
            soil.Add(1, 2, 100f);
            soil.Step();

            Assert.That(soil[1, 2], Is.EqualTo(100f * (1f - SoilPollution.NaturalDecayPerDay)).Within(1e-3f));
            Assert.That(soil[0, 0], Is.Zero);
            Assert.That(SoilPollution.Level(soil[1, 2]), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(SoilPollution.Level(-5f), Is.Zero);
        }

        [Test]
        public void CleanupCostsMoneyAndTakesTime()
        {
            var soil = new SoilPollution(2, 2);
            var ledger = new Ledger(10000);
            soil.Add(0, 0, 10f);

            Assert.That(soil.CleanupCost(0, 0), Is.EqualTo(500));
            Assert.That(soil.TryStartCleanup(0, 0, ledger, 0), Is.True);
            Assert.That(ledger.Balance, Is.EqualTo(9500));
            Assert.That(soil.IsCleaning(0, 0), Is.True);
            Assert.That(soil.TryStartCleanup(0, 0, ledger, 0), Is.False, "al bezig");

            for (var day = 0; day < 5; day++)
            {
                soil.Step();
            }

            Assert.That(soil[0, 0], Is.Zero);
            Assert.That(soil.IsCleaning(0, 0), Is.False, "klaar na vijf dagen");
        }

        [Test]
        public void CleanupFailsOnACleanCellOrWithoutMoney()
        {
            var soil = new SoilPollution(2, 2);
            var ledger = new Ledger(10);
            soil.Add(1, 1, 10f);

            Assert.That(soil.TryStartCleanup(0, 0, ledger, 0), Is.False, "schone cel");
            Assert.That(soil.TryStartCleanup(1, 1, ledger, 0), Is.False, "te weinig geld");
            Assert.That(soil.IsCleaning(1, 1), Is.False);
            Assert.That(ledger.Balance, Is.EqualTo(10));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var soil = new SoilPollution(2, 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => new SoilPollution(0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => soil.Add(0, 0, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => soil.Add(2, 0, 1f));
            Assert.Throws<ArgumentNullException>(() => soil.TryStartCleanup(0, 0, null, 0));
        }
    }
}
