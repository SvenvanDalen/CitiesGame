using System;
using Cities.Simulation;
using NUnit.Framework;

namespace Cities.Tests.EditMode
{
    public class ExtraTransitModesTests
    {
        [Test]
        public void EveryModeHasASpec()
        {
            foreach (ExtraTransitMode mode in Enum.GetValues<ExtraTransitMode>())
            {
                var spec = ExtraTransitModes.Of(mode);
                Assert.That(spec.Name, Is.Not.Empty);
                Assert.That(spec.Speed, Is.GreaterThan(0f));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => ExtraTransitModes.Of((ExtraTransitMode)99));
            Assert.That(ExtraTransitModes.Of(ExtraTransitMode.Taxi).NeedsHub, Is.False);
            Assert.That(ExtraTransitModes.Of(ExtraTransitMode.Monorail).NeedsHub, Is.True);
        }

        [Test]
        public void CapacityGrowsWithVehiclesAndCostsWithLength()
        {
            var mono = ExtraTransitModes.Of(ExtraTransitMode.Monorail);

            Assert.That(mono.PassengersPerHour(4, 5000f), Is.GreaterThan(mono.PassengersPerHour(2, 5000f)));
            Assert.That(mono.PassengersPerHour(0, 5000f), Is.EqualTo(0));
            Assert.That(mono.BuildCost(2000f), Is.EqualTo(1_800_000));
            Assert.That(mono.UpkeepPerMonth(1000f), Is.EqualTo(9_000));
            Assert.Throws<ArgumentOutOfRangeException>(() => mono.PassengersPerHour(1, 0f));
        }
    }
}
