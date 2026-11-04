using System.Linq;
using Cities.World.Benchmark;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Benchmark
{
    public sealed class FullMapScenarioTests
    {
        // Eengezinswoning, rijtjeshuizen, kleine flat, grote flat.
        private static readonly int[] Households = { 1, 6, 24, 60 };

        [Test]
        public void DefaultMapHoldsAMillionInhabitants()
        {
            var report = FullMapScenario.Plan(WorldDimensions.Default, Households);

            Assert.That(report.Plan.Population, Is.GreaterThanOrEqualTo(FullMapScenario.TargetPopulation));
            Assert.That(report.MaxPopulation, Is.GreaterThanOrEqualTo(FullMapScenario.TargetPopulation));
            Assert.That(report.Budget.Population, Is.EqualTo(1_000_000));
        }

        [Test]
        public void CityIsSpreadOverMostOfTheTiles()
        {
            var report = FullMapScenario.Plan(WorldDimensions.Default, Households);

            Assert.That(report.LotsPerTile, Has.Length.EqualTo(81));
            Assert.That(report.LotsPerTile.Sum(), Is.EqualTo(report.Plan.Lots.Count));
            Assert.That(report.TilesUsed, Is.EqualTo(report.LotsPerTile.Count(count => count > 0)));
            Assert.That(report.TilesUsed, Is.GreaterThan(0));
        }

        [Test]
        [Category("UnityRuntime")]
        public void LayerEstimateMatchesTheRealMap()
        {
            var dimensions = new WorldDimensions(2, 2, 16, 8f);
            using (var map = new WorldMap(dimensions))
            {
                Assert.That(FullMapScenario.LayerBytes(dimensions), Is.EqualTo(map.MemoryBytes));
            }
        }

        [Test]
        public void MapLayersFitInHalfTheMemoryBudget()
        {
            var report = FullMapScenario.Plan(WorldDimensions.Default, Households);

            Assert.That(report.MapFitsBudget, Is.True, $"{report.MapMegabytes:0.#} MB");
        }

        [Test]
        public void TooSmallMapStillFailsLoudly()
        {
            var tiny = new WorldDimensions(1, 1, 20, 8f);

            Assert.Throws<System.InvalidOperationException>(() => FullMapScenario.Plan(tiny, Households));
        }
    }
}
