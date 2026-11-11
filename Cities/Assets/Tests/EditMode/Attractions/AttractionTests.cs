using System;
using Cities.Simulation.Attractions;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Attractions
{
    public class AttractionTests
    {
        [Test]
        public void ParksDrawVisitorsByAccessAndWeather()
        {
            var zoo = new ParkAttraction(ParkKind.Zoo);
            var theme = new ParkAttraction(ParkKind.ThemePark);
            var reserve = new ParkAttraction(ParkKind.NatureReserve, 2000);

            Assert.That(zoo.VisitorsPerDay(1f, 1f), Is.EqualTo(1500));
            Assert.That(zoo.VisitorsPerDay(1f, 0f), Is.EqualTo(600));
            Assert.That(zoo.VisitorsPerDay(0f, 1f), Is.EqualTo(0));
            Assert.That(theme.IncomePerDay(1f, 1f), Is.EqualTo(240_000));
            Assert.That(reserve.IncomePerDay(1f, 1f), Is.EqualTo(0));
            Assert.That(reserve.HappinessBonus, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(new ParkAttraction(ParkKind.Zoo, 1_000_000).HappinessBonus, Is.EqualTo(0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ParkAttraction(ParkKind.Zoo, -1));
        }

        [Test]
        public void CampusGrowsWithFacultiesUpToTheCap()
        {
            var campus = new CampusLife();
            campus.AddFaculty();
            campus.AddFaculty();
            campus.AddSportTeam();

            Assert.That(campus.Students, Is.EqualTo(1600));
            Assert.That(campus.ResearchBonus, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(campus.MatchDayIncome, Is.EqualTo(500 + 3200));
            for (var i = 2; i < CampusLife.MaxFaculties; i++)
            {
                campus.AddFaculty();
            }

            Assert.That(campus.ResearchBonus, Is.EqualTo(0.15f));
            Assert.Throws<InvalidOperationException>(campus.AddFaculty);
        }

        [Test]
        public void ExchangeNeedsOfficesAndHotelsFillUp()
        {
            var district = new FinancialDistrict { OfficeBuildings = 3, HasExchange = true, Hotels = 2 };

            Assert.That(district.OfficeTaxFactor, Is.EqualTo(1f));
            district.OfficeBuildings = 4;
            Assert.That(district.OfficeTaxFactor, Is.EqualTo(1.2f));
            Assert.That(district.OccupiedRooms(100, 50), Is.EqualTo(150));
            Assert.That(district.OccupiedRooms(500, 500), Is.EqualTo(240));
            Assert.That(district.HotelIncomePerDay(10, 0), Is.EqualTo(900));
            Assert.Throws<ArgumentOutOfRangeException>(() => district.OccupiedRooms(-1, 0));
        }
    }
}
