using System;
using Cities.Simulation.Economy;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class GreenCityTests
    {
        [Test]
        public void RangeDependsOnCharge()
        {
            Assert.That(GreenCity.CanComplete(100_000f, 0.5f), Is.True);
            Assert.That(GreenCity.CanComplete(200_000f, 0.5f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => GreenCity.CanComplete(1f, 2f));
        }

        [Test]
        public void ShareGrowsWithChargersAndSubsidyAndCutsEmissions()
        {
            Assert.That(GreenCity.ElectricShare(0f, 1f), Is.EqualTo(0f));
            Assert.That(GreenCity.ElectricShare(2f, 1f), Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(GreenCity.ElectricShare(2f, 1f), Is.GreaterThan(GreenCity.ElectricShare(2f, 0f)));
            Assert.That(GreenCity.TrafficEmissionFactor(0f), Is.EqualTo(1f));
            Assert.That(GreenCity.TrafficEmissionFactor(1f), Is.EqualTo(0.1f).Within(0.001f));
        }

        [Test]
        public void WindParkNeedsWindButNotAStorm()
        {
            Assert.That(GreenCity.WindPower(10, 1f), Is.EqualTo(0f));
            Assert.That(GreenCity.WindPower(10, 30f), Is.EqualTo(0f));
            Assert.That(GreenCity.WindPower(10, 12f), Is.EqualTo(30f).Within(0.001f));
            Assert.That(GreenCity.WindPower(10, 8f), Is.LessThan(30f));
            Assert.Throws<ArgumentOutOfRangeException>(() => GreenCity.WindPower(-1, 5f));
        }
    }
}
