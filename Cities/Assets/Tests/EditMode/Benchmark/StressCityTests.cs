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

        private static readonly WorldDimensions Big = new WorldDimensions(1, 1, 140, 8f);

        [Test]
        public void ReachesThePopulation()
        {
            var plan = StressCity.Generate(Big, 2000, 7, Households);
            Assert.That(plan.Population, Is.GreaterThanOrEqualTo(2000));
            Assert.That(plan.Lots.Count % 4, Is.EqualTo(0));
        }

        [Test]
        public void SameSeedGivesSameCity()
        {
            var first = StressCity.Generate(Big, 1500, 3, Households);
            var second = StressCity.Generate(Big, 1500, 3, Households);
            Assert.That(second.Lots.Select(l => (l.X, l.Z, l.Zone, l.Template)), Is.EqualTo(first.Lots.Select(l => (l.X, l.Z, l.Zone, l.Template))));
        }

        [Test]
        public void DifferentSeedsDiffer()
        {
            var first = StressCity.Generate(Big, 1500, 3, Households);
            var second = StressCity.Generate(Big, 1500, 4, Households);
            Assert.That(second.Lots.Select(l => l.Zone), Is.Not.EqualTo(first.Lots.Select(l => l.Zone)));
        }

        [Test]
        public void LotsSitOnBlocksAndLeaveStreetsFree()
        {
            var plan = StressCity.Generate(Big, 1000, 1, Households);
            Assert.That(plan.Lots.All(l => (l.X % StressCity.Pitch) + StressCity.LotCells <= StressCity.BlockCells), Is.True);
            Assert.That(plan.Lots.All(l => l.X + StressCity.LotCells <= Big.CellsX && l.Z + StressCity.LotCells <= Big.CellsZ), Is.True);
        }

        [Test]
        public void ApplyCoversEveryLotCellOnce()
        {
            var plan = StressCity.Generate(Big, 500, 2, Households);
            var painted = new System.Collections.Generic.Dictionary<(int, int), ZoneType>();
            StressCity.Apply(plan, (x, z, cell) => painted.Add((x, z), cell.Type));
            Assert.That(painted.Count, Is.EqualTo(plan.Lots.Count * StressCity.LotCells * StressCity.LotCells));
            Assert.That(painted[(plan.Lots[0].X, plan.Lots[0].Z)], Is.EqualTo(plan.Lots[0].Zone));
            Assert.That(painted.ContainsKey((StressCity.BlockCells, 0)), Is.False);
            Assert.Throws<ArgumentNullException>(() => StressCity.Apply(plan, null));
            Assert.Throws<ArgumentNullException>(() => StressCity.Apply((StressCityPlan)null, (x, z, c) => { }));
        }

        [Test]
        [Category("UnityRuntime")]
        public void ApplyPaintsLotZones()
        {
            using var map = new WorldMap(Big);
            var plan = StressCity.Generate(Big, 1000, 1, Households);
            StressCity.Apply(map, plan);
            var lot = plan.Lots[0];
            Assert.That(map.Zones[lot.X, lot.Z].Type, Is.EqualTo(lot.Zone));
            Assert.That(map.Zones[StressCity.BlockCells, 0].IsZoned, Is.False);
        }

        [Test]
        public void OnlyResidentialLotsHaveTemplates()
        {
            var plan = StressCity.Generate(Big, 3000, 5, Households);
            Assert.That(plan.Lots.All(l => (l.Zone == ZoneType.Residential) == (l.Template >= 0)), Is.True);
            Assert.That(plan.Lots.Select(l => l.Zone).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void TooSmallMapThrows()
        {
            Assert.Throws<InvalidOperationException>(() => StressCity.Generate(new WorldDimensions(1, 1, 28, 8f), 100000, 1, Households));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentException>(() => StressCity.Generate(Big, 10, 1, null));
            Assert.Throws<ArgumentNullException>(() => StressCity.Apply((WorldMap)null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => StressCity.Generate(Big, 0, 1, Households));
            Assert.Throws<ArgumentException>(() => StressCity.Generate(Big, 10, 1, Array.Empty<int>()));
        }
    }
}
