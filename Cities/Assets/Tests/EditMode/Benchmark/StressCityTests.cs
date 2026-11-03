using System;
using System.Linq;
using Cities.World.Benchmark;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Benchmark
{
    public sealed class StressCityTests
    {
        private static readonly int[] Households = { 1, 4, 12 };

        private static WorldMap Map() => new WorldMap(new WorldDimensions(1, 1, 140, 8f));

        [Test]
        public void ReachesThePopulation()
        {
            using var map = Map();
            var plan = StressCity.Generate(map, 2000, 7, Households);
            Assert.That(plan.Population, Is.GreaterThanOrEqualTo(2000));
            Assert.That(plan.Lots.Count % 4, Is.EqualTo(0));
        }

        [Test]
        public void SameSeedGivesSameCity()
        {
            using var a = Map();
            using var b = Map();
            var first = StressCity.Generate(a, 1500, 3, Households);
            var second = StressCity.Generate(b, 1500, 3, Households);
            Assert.That(second.Lots.Select(l => (l.X, l.Z, l.Zone, l.Template)), Is.EqualTo(first.Lots.Select(l => (l.X, l.Z, l.Zone, l.Template))));
        }

        [Test]
        public void DifferentSeedsDiffer()
        {
            using var a = Map();
            using var b = Map();
            var first = StressCity.Generate(a, 1500, 3, Households);
            var second = StressCity.Generate(b, 1500, 4, Households);
            Assert.That(second.Lots.Select(l => l.Zone), Is.Not.EqualTo(first.Lots.Select(l => l.Zone)));
        }

        [Test]
        public void PaintsZonesAndKeepsStreetsFree()
        {
            using var map = Map();
            var plan = StressCity.Generate(map, 1000, 1, Households);
            var lot = plan.Lots[0];
            Assert.That(map.Zones[lot.X, lot.Z].Type, Is.EqualTo(lot.Zone));
            Assert.That(map.Zones[StressCity.BlockCells, 0].IsZoned, Is.False, "straatcel naast het eerste blok");
        }

        [Test]
        public void OnlyResidentialLotsHaveTemplates()
        {
            using var map = Map();
            var plan = StressCity.Generate(map, 3000, 5, Households);
            Assert.That(plan.Lots.All(l => (l.Zone == ZoneType.Residential) == (l.Template >= 0)), Is.True);
            Assert.That(plan.Lots.Select(l => l.Zone).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void TooSmallMapThrows()
        {
            using var map = new WorldMap(new WorldDimensions(1, 1, 28, 8f));
            Assert.Throws<InvalidOperationException>(() => StressCity.Generate(map, 100000, 1, Households));
        }

        [Test]
        public void RejectsBadArguments()
        {
            using var map = Map();
            Assert.Throws<ArgumentNullException>(() => StressCity.Generate(null, 10, 1, Households));
            Assert.Throws<ArgumentOutOfRangeException>(() => StressCity.Generate(map, 0, 1, Households));
            Assert.Throws<ArgumentException>(() => StressCity.Generate(map, 10, 1, Array.Empty<int>()));
        }
    }
}
