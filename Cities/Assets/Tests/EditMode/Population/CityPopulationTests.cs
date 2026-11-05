using Cities.Gameplay.Population;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Population
{
    public class CityPopulationTests
    {
        [Test]
        public void CityWithHomesFillsUp()
        {
            var city = new CityPopulation();
            city.AddBuilding(households: 10, workplaces: 0, shopCapacity: 0);
            city.AddBuilding(households: 0, workplaces: 50, shopCapacity: 0);

            for (var day = 0; day < 10; day++)
            {
                city.Daily();
            }

            Assert.That(city.Population, Is.GreaterThan(0));
            Assert.That(city.Population, Is.LessThanOrEqualTo(city.HomeCapacity));
        }

        [Test]
        public void NoHomesMeansNoPopulation()
        {
            var city = new CityPopulation();
            city.AddBuilding(0, 20, 0);
            city.Daily();
            Assert.That(city.Population, Is.EqualTo(0));
        }

        [Test]
        public void ResidentialDemandIsPositiveInAnEmptyCity()
        {
            var city = new CityPopulation();
            city.Daily();
            Assert.That(city.DemandFor(ZoneType.Residential), Is.GreaterThan(0f));
        }

        [Test]
        public void UnemploymentWhenThereAreFewJobs()
        {
            var city = new CityPopulation();
            city.AddBuilding(100, 0, 0);
            city.AddBuilding(0, 5, 0);
            for (var day = 0; day < 30; day++)
            {
                city.Daily();
            }

            Assert.That(city.Unemployed, Is.GreaterThan(0));
            Assert.That(city.Employed, Is.LessThanOrEqualTo(city.Jobs));
        }

        [Test]
        public void DemandForIndustryIsBounded()
        {
            var city = new CityPopulation();
            city.AddBuilding(100, 0, 0);
            for (var day = 0; day < 20; day++)
            {
                city.Daily();
            }

            Assert.That(city.DemandFor(ZoneType.Industrial), Is.InRange(-1f, 1f));
        }

        [Test]
        public void RemovingAHomeRemovesItsResidentsAndJobs()
        {
            var city = new CityPopulation();
            var home = city.AddBuilding(10, 30, 0);
            for (var day = 0; day < 5; day++)
            {
                city.Daily();
            }

            Assert.That(city.Population, Is.GreaterThan(0));
            city.RemoveBuilding(home);

            Assert.That(city.Population, Is.EqualTo(0));
            Assert.That(city.HomeCapacity, Is.EqualTo(0));
            Assert.That(city.Jobs, Is.EqualTo(0));
        }

        [Test]
        public void UnattractiveCityDoesNotGrow()
        {
            var city = new CityPopulation { LandValueAppeal = 0f, ServicesAppeal = 0f, TaxAppeal = 0f };
            city.AddBuilding(200, 0, 0);
            for (var day = 0; day < 5; day++)
            {
                city.Daily();
            }

            Assert.That(city.Population, Is.EqualTo(0));
        }
    }
}
