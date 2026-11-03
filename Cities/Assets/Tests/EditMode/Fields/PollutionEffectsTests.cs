using Cities.Simulation.Citizens;
using Cities.Simulation.Fields;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public sealed class PollutionEffectsTests
    {
        private static readonly PollutionExposure Clean = new PollutionExposure(0f, 0f, 0f, 0f);
        private static readonly PollutionExposure Filthy = new PollutionExposure(1f, 1f, 1f, 1f);

        [Test]
        public void ExposureClampsEachLevelToZeroToOne()
        {
            var e = new PollutionExposure(-1f, 2f, 0.5f, 9f);

            Assert.That(e.Air, Is.EqualTo(0f));
            Assert.That(e.Noise, Is.EqualTo(1f));
            Assert.That(e.Soil, Is.EqualTo(0.5f));
            Assert.That(e.Water, Is.EqualTo(1f));
        }

        [Test]
        public void FromFieldsUsesTheLevelFunctionOfEachField()
        {
            var e = PollutionExposure.FromFields(AirPollution.HalfLevel, 85f, SoilPollution.HalfLevel, 0.3f);

            Assert.That(e.Air, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(e.Noise, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(e.Soil, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(e.Water, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void WeightsSumToOne()
        {
            Assert.That(PollutionEffects.AirWeight + PollutionEffects.NoiseWeight + PollutionEffects.SoilWeight + PollutionEffects.WaterWeight,
                Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void OverallRunsFromZeroToOne()
        {
            Assert.That(PollutionEffects.Overall(Clean), Is.EqualTo(0f));
            Assert.That(PollutionEffects.Overall(Filthy), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void AirPollutionWeighsHeavierThanWater()
        {
            var air = new PollutionExposure(1f, 0f, 0f, 0f);
            var water = new PollutionExposure(0f, 0f, 0f, 1f);

            Assert.That(PollutionEffects.Overall(air), Is.GreaterThan(PollutionEffects.Overall(water)));
        }

        [Test]
        public void HealthEnvironmentCarriesAirWaterAndSoil()
        {
            var env = PollutionEffects.ToHealth(new PollutionExposure(0.2f, 1f, 0.4f, 0.6f), true, 1.5f);

            Assert.That(env.Air, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(env.Soil, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(env.Water, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(env.HasCare, Is.True);
            Assert.That(env.BedLoad, Is.EqualTo(1.5f));
        }

        [Test]
        public void DirtyAirLowersTheTargetHealth()
        {
            var clean = CitizenHealthModel.TargetHealth(30, PollutionEffects.ToHealth(Clean, false, 0f));
            var dirty = CitizenHealthModel.TargetHealth(30, PollutionEffects.ToHealth(new PollutionExposure(1f, 0f, 0f, 0f), false, 0f));

            Assert.That(dirty, Is.LessThan(clean));
        }

        [Test]
        public void HappinessPollutionLeavesNoiseOut()
        {
            var noisy = new PollutionExposure(0f, 1f, 0f, 0f);

            Assert.That(PollutionEffects.HappinessPollution(noisy), Is.EqualTo(0f));
            Assert.That(PollutionEffects.HappinessPollution(Filthy), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void HappinessFactorsTakePollutionAndNoiseFromTheExposure()
        {
            var f = PollutionEffects.ToHappiness(new PollutionExposure(1f, 0.5f, 1f, 1f), 0.1f, 0.7f, -5f, 20f);

            Assert.That(f.Pollution, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(f.Noise, Is.EqualTo(0.5f));
            Assert.That(f.Crime, Is.EqualTo(0.1f));
            Assert.That(f.Services, Is.EqualTo(0.7f));
            Assert.That(f.TaxShift, Is.EqualTo(-5f));
            Assert.That(f.CommuteMinutes, Is.EqualTo(20f));
        }

        [Test]
        public void VacancyIsTheBaseOnCleanExpensiveLand()
        {
            Assert.That(PollutionEffects.VacancyRate(Clean, 1f), Is.EqualTo(PollutionEffects.BaseVacancy).Within(1e-5f));
        }

        [Test]
        public void VacancyRisesWithPollutionAndFallingLandValue()
        {
            var baseRate = PollutionEffects.VacancyRate(Clean, 1f);

            Assert.That(PollutionEffects.VacancyRate(new PollutionExposure(1f, 0f, 0f, 0f), 1f), Is.GreaterThan(baseRate));
            Assert.That(PollutionEffects.VacancyRate(Clean, 0f), Is.GreaterThan(baseRate));
        }

        [Test]
        public void VacancyNeverExceedsTheMaximum()
        {
            Assert.That(PollutionEffects.VacancyRate(Filthy, 0f), Is.LessThanOrEqualTo(PollutionEffects.MaxVacancy));
        }

        [Test]
        public void OccupiedUnitsRoundDownAndStayWithinCapacity()
        {
            Assert.That(PollutionEffects.OccupiedUnits(10, 0.25f), Is.EqualTo(7));
            Assert.That(PollutionEffects.OccupiedUnits(10, 0f), Is.EqualTo(10));
            Assert.That(PollutionEffects.OccupiedUnits(10, 2f), Is.EqualTo(0));
            Assert.That(PollutionEffects.OccupiedUnits(-3, 0.1f), Is.EqualTo(0));
        }

        [Test]
        public void LandValueFallsWithTheOverallPollution()
        {
            var clean = new LandValueFactors(1f, 1f, 1f, 1f, PollutionEffects.Overall(Clean));
            var polluted = new LandValueFactors(1f, 1f, 1f, 1f, PollutionEffects.Overall(Filthy));

            Assert.That(LandValueModel.Value(polluted), Is.LessThan(LandValueModel.Value(clean)));
        }
    }
}
