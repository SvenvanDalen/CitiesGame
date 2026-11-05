using Cities.Gameplay.Environment;
using Cities.Gameplay.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.MilieuHost
{
    public class CityEnvironmentTests
    {
        [Test]
        public void AirFallsOffWithDistance()
        {
            var env = new CityEnvironment();
            env.AddIndustry(float2.zero);
            Assert.That(env.AirAt(new float2(10, 0)), Is.GreaterThan(env.AirAt(new float2(300, 0))));
            Assert.That(env.AirAt(new float2(1000, 0)), Is.EqualTo(0f));
        }

        [Test]
        public void NoiseIsLouderNearIndustry()
        {
            var env = new CityEnvironment();
            env.AddIndustry(float2.zero);
            Assert.That(env.NoiseAt(new float2(5, 0)), Is.GreaterThan(env.NoiseAt(new float2(500, 0))));
        }

        [Test]
        public void PollutionLowersAndTransitRaisesLandValue()
        {
            var clean = new CityEnvironment();
            var dirty = new CityEnvironment();
            dirty.AddIndustry(float2.zero);
            Assert.That(dirty.LandValueAt(float2.zero, null), Is.LessThan(clean.LandValueAt(float2.zero, null)));

            var served = new CityEnvironment();
            served.AddStop(float2.zero);
            Assert.That(served.LandValueAt(float2.zero, null), Is.GreaterThan(clean.LandValueAt(float2.zero, null)));
        }

        [Test]
        public void ServicesRaiseLandValue()
        {
            var env = new CityEnvironment();
            var services = new CityServices();
            var before = env.LandValueAt(float2.zero, services);
            services.Place(CityService.Fire, float2.zero);
            Assert.That(env.LandValueAt(float2.zero, services), Is.GreaterThan(before));
        }

        [Test]
        public void HomeAppealIsNeutralWithoutHomes()
        {
            Assert.That(new CityEnvironment().HomeAppeal(null), Is.EqualTo(0.5f));
        }
    }
}
