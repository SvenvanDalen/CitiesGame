using System;
using Cities.Simulation.Scheduling;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Scheduling
{
    public sealed class SimulationTiersTests
    {
        private static SimulationTiers Tiers(int interval = 10, int linger = 100) => new SimulationTiers(10, 10, 100f, 150f, interval, linger);

        [Test]
        public void EverythingStartsStatistical()
        {
            var tiers = Tiers();
            Assert.That(tiers.FullCount(0), Is.Zero);
            Assert.That(tiers.TierAt(new float2(50f, 50f), 0), Is.EqualTo(SimulationTier.Statistical));
        }

        [Test]
        public void RegionsNearTheFocusBecomeFull()
        {
            var tiers = Tiers();
            tiers.Visit(new float2(500f, 500f), 0);
            Assert.That(tiers.TierAt(new float2(500f, 500f), 0), Is.EqualTo(SimulationTier.Full));
            Assert.That(tiers.TierAt(new float2(600f, 500f), 0), Is.EqualTo(SimulationTier.Full));
            Assert.That(tiers.TierAt(new float2(50f, 50f), 0), Is.EqualTo(SimulationTier.Statistical));
        }

        [Test]
        public void RadiusIsMeasuredToTheRegionEdge()
        {
            var tiers = Tiers();
            tiers.Visit(new float2(550f, 550f), 0);
            // Gebied (7,5) begint op x=700: 150 m van het brandpunt, precies op de rand.
            Assert.That(tiers.TierOf((5 * 10) + 7, 0), Is.EqualTo(SimulationTier.Full));
            Assert.That(tiers.TierOf((5 * 10) + 8, 0), Is.EqualTo(SimulationTier.Statistical));
        }

        [Test]
        public void FullRegionsLingerThenFall()
        {
            var tiers = Tiers(10, 100);
            tiers.Visit(new float2(500f, 500f), 0);
            Assert.That(tiers.TierAt(new float2(500f, 500f), 100), Is.EqualTo(SimulationTier.Full));
            Assert.That(tiers.TierAt(new float2(500f, 500f), 101), Is.EqualTo(SimulationTier.Statistical));
        }

        [Test]
        public void FocusOutsideTheMapIsClamped()
        {
            var tiers = Tiers();
            Assert.That(tiers.RegionOf(new float2(-50f, -50f)), Is.Zero);
            Assert.That(tiers.RegionOf(new float2(9999f, 9999f)), Is.EqualTo(99));
            tiers.Visit(new float2(-1000f, -1000f), 0);
            Assert.That(tiers.FullCount(0), Is.Zero);
        }

        [Test]
        public void FullRegionsStepEveryTick()
        {
            var tiers = Tiers();
            tiers.Visit(new float2(500f, 500f), 0);
            var region = tiers.RegionOf(new float2(500f, 500f));
            for (long tick = 1; tick <= 5; tick++)
            {
                Assert.That(tiers.ShouldStep(region, tick, out var elapsed), Is.True);
                Assert.That(elapsed, Is.EqualTo(1));
            }
        }

        [Test]
        public void StatisticalRegionsStepOncePerIntervalWithElapsedTicks()
        {
            var tiers = Tiers(10);
            var steps = 0;
            var total = 0;
            for (long tick = 1; tick <= 100; tick++)
            {
                if (tiers.ShouldStep(3, tick, out var elapsed))
                {
                    steps++;
                    total += elapsed;
                }
            }

            Assert.That(steps, Is.EqualTo(10));
            Assert.That(total, Is.EqualTo(97), "alle verstreken ticks worden doorgegeven");
        }

        [Test]
        public void RegionsAreStaggeredAcrossTheInterval()
        {
            var tiers = Tiers(10);
            var stepping = 0;
            for (var region = 0; region < tiers.RegionCount; region++)
            {
                stepping += tiers.ShouldStep(region, 20, out _) ? 1 : 0;
            }

            Assert.That(stepping, Is.EqualTo(10), "een tiende van de gebieden per tick");
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTiers(0, 1, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTiers(1, 0, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTiers(1, 1, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTiers(1, 1, 1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTiers(1, 1, 1f, 1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTiers(1, 1, 1f, 1f, 1, -1));
        }
    }
}
