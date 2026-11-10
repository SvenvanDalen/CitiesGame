using Cities.Gameplay.Economy;
using Cities.Gameplay.Industry;
using Cities.Gameplay.NewCity;
using Cities.Gameplay.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    /// <summary>Wat plaatsen ongedaan maakt (E01-12): diensten, stations en industrie gaan weer uit het model.</summary>
    public class PlacementRemovalTests
    {
        [Test]
        public void RemovingAPlacedServiceRemovesItsCoverage()
        {
            var services = new CityServices();
            var position = new float2(100f, 100f);
            services.Place(CityService.Police, position);
            Assert.That(services.IsCovered(CityService.Police, position), Is.True);

            Assert.That(services.Remove(CityService.Police, position), Is.True);

            Assert.That(services.Count, Is.EqualTo(0));
            Assert.That(services.IsCovered(CityService.Police, position), Is.False);
            Assert.That(services.Remove(CityService.Police, position), Is.False, "wat er niet staat, haal je niet weg");
        }

        [Test]
        public void RemovingAStationTakesItOutOfTheEconomy()
        {
            var economy = new CityEconomy(Difficulty.Normal);
            var station = new CityServices().Place(CityService.Fire, new float2(10f, 10f));
            economy.AddStation(station);
            Assert.That(economy.Stations.Count, Is.EqualTo(1));

            economy.RemoveStation(station);

            Assert.That(economy.Stations.Count, Is.EqualTo(0));
        }

        [Test]
        public void IndustryPlacementsCanBeTakenBack()
        {
            var industry = new CityIndustry();
            var site = industry.AddSite(Cities.Simulation.Industry.IndustryKind.Ore);
            industry.AddHotel();
            var attraction = industry.AddAttraction("Pretpark", 60, 8, 400);

            Assert.That(industry.RemoveSite(site.Id), Is.True);
            Assert.That(industry.Sites.Contains(site.Id), Is.False);
            industry.RemoveHotel();
            Assert.That(industry.Beds, Is.EqualTo(0));
            industry.RemoveHotel();
            Assert.That(industry.Beds, Is.EqualTo(0), "bedden worden nooit negatief");
            Assert.That(industry.RemoveAttraction(attraction), Is.True);
        }
    }
}
