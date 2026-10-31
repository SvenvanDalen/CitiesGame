using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class WasteBinTests
    {
        [Test]
        public void BinFillsAndCallsAVehicleAtTheThreshold()
        {
            var bin = new WasteBin(100f);

            bin.Produce(79f);
            Assert.That(bin.PickupRequested, Is.False);

            bin.Produce(1f);
            Assert.That(bin.PickupRequested, Is.True);
            Assert.That(bin.Level, Is.EqualTo(80f));
        }

        [Test]
        public void VehicleEmptiesUpToItsCapacityAndTheCallEndsBelowTheThreshold()
        {
            var bin = new WasteBin(100f);
            bin.Produce(90f);

            Assert.That(bin.Collect(5f), Is.EqualTo(5f));
            Assert.That(bin.PickupRequested, Is.True, "nog steeds boven de drempel");
            Assert.That(bin.Collect(500f), Is.EqualTo(85f), "meer dan er ligt neemt hij niet mee");
            Assert.That(bin.Level, Is.Zero);
            Assert.That(bin.PickupRequested, Is.False);
        }

        [Test]
        public void WasteThatDoesNotFitStaysBehindAsOverflow()
        {
            var bin = new WasteBin(100f);

            bin.Produce(130f);

            Assert.That(bin.Level, Is.EqualTo(100f));
            Assert.That(bin.Overflow, Is.EqualTo(30f));
            bin.Produce(10f);
            Assert.That(bin.Overflow, Is.EqualTo(40f));
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var bin = new WasteBin(10f);

            Assert.Throws<ArgumentOutOfRangeException>(() => new WasteBin(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => bin.Produce(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => bin.Collect(-1f));
        }
    }
}
