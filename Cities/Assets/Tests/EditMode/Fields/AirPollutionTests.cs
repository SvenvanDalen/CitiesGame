using System;
using Cities.Simulation.Fields;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public class AirPollutionTests
    {
        [Test]
        public void ACoalPlantPollutesMoreThanIndustryAndIndustryMoreThanACar()
        {
            Assert.That(AirPollution.Emission(PollutionSource.CoalPlant), Is.GreaterThan(AirPollution.Emission(PollutionSource.Industry)));
            Assert.That(AirPollution.Emission(PollutionSource.Industry), Is.GreaterThan(AirPollution.Emission(PollutionSource.Traffic)));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirPollution.Emission((PollutionSource)99));
        }

        [Test]
        public void TheLevelSaturatesBetweenZeroAndOne()
        {
            Assert.That(AirPollution.Level(0f), Is.Zero);
            Assert.That(AirPollution.Level(-5f), Is.Zero);
            Assert.That(AirPollution.Level(AirPollution.HalfLevel), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(AirPollution.Level(1e6f), Is.LessThanOrEqualTo(1f).And.GreaterThan(0.99f));
        }

        [Test]
        public void PollutionLowersLandValueAndRaisesTheChanceOfIllness()
        {
            var clean = LandValueModel.Value(new LandValueFactors(1f, 1f, 1f, 1f, AirPollution.Level(0f)));
            var dirty = LandValueModel.Value(new LandValueFactors(1f, 1f, 1f, 1f, AirPollution.Level(200f)));

            Assert.That(dirty, Is.LessThan(clean));
            Assert.That(AirPollution.HealthFactor(0f), Is.EqualTo(1f));
            Assert.That(AirPollution.HealthFactor(1f), Is.EqualTo(AirPollution.MaxHealthFactor));
            Assert.That(AirPollution.HealthFactor(5f), Is.EqualTo(AirPollution.MaxHealthFactor), "begrensd");
        }
    }
}
