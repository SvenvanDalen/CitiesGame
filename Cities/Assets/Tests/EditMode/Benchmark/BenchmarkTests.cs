using System;
using Cities.Simulation.Benchmark;
using Cities.Simulation.Scheduling;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Benchmark
{
    public class BenchmarkTests
    {
        private static readonly int[] Templates = { 4, 12, 40 };

        [Test]
        public void TheSameSeedGivesTheSameCityAndAnotherSeedDoesNot()
        {
            var a = BenchmarkCity.Generate(10_000, 7, Templates);
            var b = BenchmarkCity.Generate(10_000, 7, Templates);
            var c = BenchmarkCity.Generate(10_000, 8, Templates);

            Assert.That(b, Is.EqualTo(a));
            Assert.That(c, Is.Not.EqualTo(a));
        }

        [TestCase(10_000)]
        [TestCase(100_000)]
        [TestCase(500_000)]
        public void EveryFixedCityReachesItsPopulationWithoutOvershootingByMoreThanOneBuilding(int population)
        {
            var city = BenchmarkCity.Generate(population, 1, Templates);
            var inhabitants = BenchmarkCity.Population(city, Templates);

            Assert.That(inhabitants, Is.GreaterThanOrEqualTo(population));
            Assert.That(inhabitants - population, Is.LessThan(40 * BenchmarkCity.HouseholdSize + 1));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BenchmarkCity.Generate(0, 1, Templates));
            Assert.Throws<ArgumentException>(() => BenchmarkCity.Generate(100, 1, Array.Empty<int>()));
            Assert.Throws<ArgumentException>(() => BenchmarkCity.Generate(100, 1, null));
            Assert.Throws<ArgumentException>(() => BenchmarkCity.Generate(100, 1, new[] { 4, 0 }));
        }

        [Test]
        public void ATickWithinTheBudgetPassesAndAnySlowerOneFails()
        {
            var fast = new SystemTimings();
            fast.Record(SystemTimings.TickName, 1.0);
            fast.Record("Verkeer", 0.6);
            var slow = new SystemTimings();
            slow.Record(SystemTimings.TickName, 5.0);

            var ok = BenchmarkReport.Evaluate(10_000, fast);
            var bad = BenchmarkReport.Evaluate(10_000, slow);

            Assert.That(ok.Passed, Is.True);
            Assert.That(ok.Text, Does.StartWith("GESLAAGD").And.Contain("Verkeer: 0.60 ms"));
            Assert.That(bad.Passed, Is.False);
            Assert.That(bad.Text, Does.StartWith("GEFAALD"));
        }

        [Test]
        public void AMeasurementWithoutTicksFailsAndNullIsRejected()
        {
            Assert.That(BenchmarkReport.Evaluate(10_000, new SystemTimings()).Passed, Is.False);
            Assert.Throws<ArgumentNullException>(() => BenchmarkReport.Evaluate(10_000, null));
        }
    }
}
