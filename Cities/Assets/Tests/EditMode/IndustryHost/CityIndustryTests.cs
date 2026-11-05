using Cities.Gameplay.Industry;
using Cities.Simulation.Economy;
using Cities.Simulation.Industry;
using NUnit.Framework;

namespace Cities.Tests.EditMode.IndustryHost
{
    public class CityIndustryTests
    {
        [Test]
        public void ExtractionIsExportedForMoney()
        {
            var industry = new CityIndustry();
            industry.AddSite(IndustryKind.Ore, 2);
            var ledger = new Ledger(0);

            var week = industry.EndWeek(1, ledger);

            Assert.That(week.Exported, Is.GreaterThan(0));
            Assert.That(week.ExportIncome, Is.EqualTo(week.Exported * CityIndustry.ExportPrice));
            Assert.That(ledger.Balance, Is.EqualTo(week.ExportIncome));
            Assert.That(week.Pollution, Is.GreaterThan(0f));
        }

        [Test]
        public void TouristsNeedHotelBeds()
        {
            var industry = new CityIndustry();
            industry.AddAttraction("Museum", 100, 5, 50);
            var withoutBeds = industry.EndWeek(1, new Ledger(10_000)).Tourism.Tourists;
            industry.AddHotel();
            var withBeds = industry.EndWeek(2, new Ledger(10_000)).Tourism.Tourists;

            Assert.That(withoutBeds, Is.EqualTo(0));
            Assert.That(withBeds, Is.GreaterThan(0));
        }

        [Test]
        public void EmptyCityHasNoIncome()
        {
            var ledger = new Ledger(100);
            var week = new CityIndustry().EndWeek(1, ledger);
            Assert.That(week.Exported, Is.EqualTo(0));
            Assert.That(ledger.Balance, Is.EqualTo(100));
        }

        [Test]
        public void SitesGetUniqueIds()
        {
            var industry = new CityIndustry();
            var a = industry.AddSite(IndustryKind.Agriculture);
            var b = industry.AddSite(IndustryKind.Forestry);
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
            Assert.That(industry.Sites.Count, Is.EqualTo(2));
        }
    }
}
