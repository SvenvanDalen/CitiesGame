using System;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class BusinessCycleTests
    {
        [Test]
        public void DisabledCycleChangesNothing()
        {
            var cycle = new BusinessCycle(7UL);

            Assert.That(cycle.Factor(26), Is.EqualTo(1f));
            Assert.That(cycle.Scale(26, 1000), Is.EqualTo(1000));
            Assert.That(cycle.PhaseAt(26), Is.EqualTo(BusinessPhase.Normal));
        }

        [Test]
        public void EachCycleBoomsFirstThenFallsIntoRecessionAndStaysGentle()
        {
            var cycle = new BusinessCycle(7UL, true);

            Assert.That(cycle.PhaseAt(BusinessCycle.WeeksPerCycle / 4), Is.EqualTo(BusinessPhase.Boom));
            Assert.That(cycle.PhaseAt(BusinessCycle.WeeksPerCycle * 3 / 4), Is.EqualTo(BusinessPhase.Recession));
            for (var week = 0; week < BusinessCycle.WeeksPerCycle * 10; week++)
            {
                Assert.That(cycle.Factor(week), Is.InRange(1f - BusinessCycle.MaxAmplitude - 0.001f, 1f + BusinessCycle.MaxAmplitude + 0.001f));
            }
        }

        [Test]
        public void SameSeedGivesSameCyclesAndOtherSeedsDiffer()
        {
            var first = new BusinessCycle(1UL, true);
            var again = new BusinessCycle(1UL, true);
            var other = new BusinessCycle(2UL, true);

            Assert.That(first.Factor(30), Is.EqualTo(again.Factor(30)));
            Assert.That(first.Amplitude(0), Is.InRange(BusinessCycle.MinAmplitude, BusinessCycle.MaxAmplitude));
            Assert.That(first.Amplitude(0), Is.Not.EqualTo(other.Amplitude(0)));
            Assert.That(first.Factor(-5), Is.EqualTo(first.Factor(0)));
        }

        [Test]
        public void SectionRoundTripsTheSettings()
        {
            var current = new BusinessCycle(99UL, true);
            var bytes = SaveBytes.Write(new SaveMetadata(), new BusinessCycleSection(() => current, _ => { }));
            BusinessCycle loaded = null;

            SaveBytes.Read(bytes, new BusinessCycleSection(() => current, c => loaded = c));

            Assert.That(loaded.Enabled, Is.True);
            Assert.That(loaded.Seed, Is.EqualTo(99UL));
            Assert.That(new BusinessCycleSection(() => current, _ => { }).Id, Is.EqualTo("CYCL"));
            Assert.Throws<ArgumentNullException>(() => new BusinessCycleSection(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new BusinessCycleSection(() => current, null));
        }
    }
}
