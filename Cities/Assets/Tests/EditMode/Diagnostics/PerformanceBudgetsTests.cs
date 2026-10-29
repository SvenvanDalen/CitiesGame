using System;
using Cities.Core.Diagnostics;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class PerformanceBudgetsTests
    {
        [TestCase(0, 10_000)]
        [TestCase(10_000, 10_000)]
        [TestCase(10_001, 50_000)]
        [TestCase(180_000, 250_000)]
        [TestCase(1_000_000, 1_000_000)]
        [TestCase(3_000_000, 1_000_000)]
        public void PicksTheSmallestTierThatFits(int population, int expectedTier)
        {
            Assert.That(PerformanceBudgets.For(population).Population, Is.EqualTo(expectedTier));
        }

        [Test]
        public void NegativePopulationIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceBudgets.For(-1));
        }

        [Test]
        public void LargerCitiesGetLooserBudgetsButNeverTighter()
        {
            var tiers = PerformanceBudgets.All;
            for (var i = 1; i < tiers.Count; i++)
            {
                var smaller = tiers[i - 1];
                var larger = tiers[i];
                Assert.That(larger.Population, Is.GreaterThan(smaller.Population));
                Assert.That(larger.TargetFps, Is.LessThanOrEqualTo(smaller.TargetFps));
                Assert.That(larger.MinimumSpecFps, Is.LessThanOrEqualTo(smaller.MinimumSpecFps));
                Assert.That(larger.TickMs, Is.GreaterThanOrEqualTo(smaller.TickMs));
                Assert.That(larger.MainThreadSimulationMs, Is.GreaterThanOrEqualTo(smaller.MainThreadSimulationMs));
                Assert.That(larger.MemoryMb, Is.GreaterThanOrEqualTo(smaller.MemoryMb));
                Assert.That(larger.SaveFileMb, Is.GreaterThanOrEqualTo(smaller.SaveFileMb));
                Assert.That(larger.SaveSeconds, Is.GreaterThanOrEqualTo(smaller.SaveSeconds));
                Assert.That(larger.LoadSeconds, Is.GreaterThanOrEqualTo(smaller.LoadSeconds));
            }
        }

        [Test]
        public void TicksAtFastestSpeedLeaveHalfTheTickPeriodFree()
        {
            var period = 1000f / PerformanceBudgets.TicksPerSecondAtFastest;
            foreach (var tier in PerformanceBudgets.All)
            {
                Assert.That(tier.TickMs, Is.LessThanOrEqualTo(period / 2f), $"{tier.Population} inwoners");
            }
        }

        [Test]
        public void MainThreadSimulationFitsInTheFrame()
        {
            foreach (var tier in PerformanceBudgets.All)
            {
                Assert.That(tier.MainThreadSimulationMs, Is.LessThanOrEqualTo(tier.FrameMs * 0.15f), $"{tier.Population} inwoners");
                Assert.That(tier.MinimumSpecFps, Is.LessThanOrEqualTo(tier.TargetFps));
            }

            Assert.That(PerformanceBudgets.For(100_000).FrameMs, Is.EqualTo(1000f / 60f).Within(1e-4f));
            Assert.That(PerformanceBudgets.TickAllocationBytes, Is.Zero);
            Assert.That(PerformanceBudgets.FrameAllocationBytes, Is.EqualTo(1024));
        }
    }
}
