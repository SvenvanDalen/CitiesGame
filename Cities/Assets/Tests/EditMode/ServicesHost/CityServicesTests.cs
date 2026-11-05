using System.Collections.Generic;
using Cities.Gameplay.Services;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.ServicesHost
{
    public class CityServicesTests
    {
        [Test]
        public void PointInsideRadiusIsCoveredAndOutsideIsNot()
        {
            var services = new CityServices();
            services.Place(CityService.Fire, new float2(0, 0));

            Assert.That(services.IsCovered(CityService.Fire, new float2(100, 0)), Is.True);
            Assert.That(services.IsCovered(CityService.Fire, new float2(5000, 0)), Is.False);
            Assert.That(services.IsCovered(CityService.Police, new float2(100, 0)), Is.False);
        }

        [Test]
        public void CoverageIsFractionOfKinds()
        {
            var services = new CityServices();
            services.Place(CityService.Fire, float2.zero);
            services.Place(CityService.Health, float2.zero);

            Assert.That(services.CoverageAt(new float2(10, 10)), Is.EqualTo(0.5f).Within(0.001f));
            services.Place(CityService.Police, float2.zero);
            services.Place(CityService.Education, float2.zero);
            Assert.That(services.CoverageAt(new float2(10, 10)), Is.EqualTo(1f));
        }

        [Test]
        public void StationsExistForBudgetedServicesOnly()
        {
            var services = new CityServices();
            Assert.That(services.Place(CityService.Fire, float2.zero).Kind, Is.EqualTo(ServiceKind.Fire));
            Assert.That(services.Place(CityService.Police, float2.zero).Kind, Is.EqualTo(ServiceKind.Police));
            Assert.That(services.Place(CityService.Health, float2.zero).Kind, Is.EqualTo(ServiceKind.Health));
            Assert.That(services.Place(CityService.Education, float2.zero), Is.Null);
            Assert.That(services.Count, Is.EqualTo(4));
        }

        [Test]
        public void AverageCoverageIsNeutralWithoutPointsAndZeroWithoutServices()
        {
            var services = new CityServices();
            Assert.That(services.AverageCoverage(new List<float2>()), Is.EqualTo(0.5f));
            Assert.That(services.AverageCoverage(new List<float2> { new float2(1, 1) }), Is.EqualTo(0f));
        }

        [Test]
        public void EveryKindHasNameRadiusAndUpkeep()
        {
            for (var i = 0; i < CityServices.KindCount; i++)
            {
                var kind = (CityService)i;
                Assert.That(CityServices.NameOf(kind), Is.Not.Empty);
                Assert.That(CityServices.RadiusOf(kind), Is.GreaterThan(0f));
                Assert.That(CityServices.UpkeepPerMonthOf(kind), Is.GreaterThan(0L));
            }
        }
    }
}
