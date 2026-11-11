using System;
using Cities.Gameplay.Expansion;
using Cities.Gameplay.Visuals;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Expansion
{
    public class CityVenuesTests
    {
        [Test]
        public void EmptyCityHasNoIncomeNorUpkeep()
        {
            var venues = new CityVenues();
            var day = venues.Daily(5_000, WeatherKind.Clear);

            Assert.That(day.Income, Is.EqualTo(0));
            Assert.That(day.Upkeep, Is.EqualTo(0));
            Assert.That(day.Net, Is.EqualTo(0));
            Assert.That(venues.Total, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => venues.Daily(-1, WeatherKind.Clear));
        }

        [Test]
        public void AddAndRemoveKeepCountsAndIgnoreExtraRemoves()
        {
            var venues = new CityVenues();
            venues.Add(VenueKind.Zoo);
            venues.Add(VenueKind.Zoo);
            venues.Remove(VenueKind.Zoo);
            venues.Remove(VenueKind.Faculty);

            Assert.That(venues.Count(VenueKind.Zoo), Is.EqualTo(1));
            Assert.That(venues.Count(VenueKind.Faculty), Is.EqualTo(0));
            Assert.That(venues.Total, Is.EqualTo(1));
            Assert.That(venues.Upkeep(), Is.EqualTo(CityVenues.UpkeepPerVenuePerDay));
        }

        [Test]
        public void ParksEarnMoreInGoodWeatherAndCountAsSinks()
        {
            var venues = new CityVenues();
            venues.Add(VenueKind.ThemePark);
            venues.Add(VenueKind.NatureReserve);
            venues.Add(VenueKind.Zoo);

            var clear = venues.Daily(20_000, WeatherKind.Clear).Income;
            var storm = venues.Daily(20_000, WeatherKind.Storm).Income;

            Assert.That(clear, Is.GreaterThan(storm));
            Assert.That(venues.Sinks, Is.EqualTo(9));
            Assert.That(venues.HappinessBonus, Is.GreaterThan(0f));
        }

        [Test]
        public void CampusFinanceAndWarehouseEarn()
        {
            var venues = new CityVenues();
            for (var i = 0; i < 3; i++)
            {
                venues.Add(VenueKind.Faculty);
                venues.Add(VenueKind.SportsField);
            }

            Assert.That(venues.ResearchBonus, Is.GreaterThan(0f));
            var campus = venues.Daily(10_000, WeatherKind.Clear).Income;
            Assert.That(campus, Is.GreaterThan(0));

            for (var i = 0; i < 4; i++)
            {
                venues.Add(VenueKind.Office);
            }

            Assert.That(venues.OfficeTaxFactor, Is.EqualTo(1f));
            venues.Add(VenueKind.Exchange);
            venues.Add(VenueKind.ConferenceHotel);
            Assert.That(venues.OfficeTaxFactor, Is.EqualTo(1.2f));
            Assert.That(venues.Daily(10_000, WeatherKind.Clear).Income, Is.GreaterThan(campus));

            venues.Add(VenueKind.Warehouse);
            for (var i = 0; i < 40; i++)
            {
                venues.Daily(10_000, WeatherKind.Clear);
            }

            Assert.That(venues.Stored, Is.GreaterThan(0));
            venues.Add(VenueKind.Warehouse);
            venues.Daily(10_000, WeatherKind.Clear);
            venues.Remove(VenueKind.Warehouse);
            venues.Remove(VenueKind.Warehouse);
            venues.Daily(10_000, WeatherKind.Clear);
            Assert.That(venues.Stored, Is.EqualTo(0));
        }

        [Test]
        public void PortAirportAndPromenadeEarn()
        {
            var venues = new CityVenues();
            venues.Add(VenueKind.Quay);
            venues.Add(VenueKind.Crane);
            venues.Add(VenueKind.FishingBoat);
            var port = venues.Daily(5_000, WeatherKind.Clear).Income;
            Assert.That(port, Is.GreaterThan(0));
            Assert.That(venues.FishStock, Is.LessThanOrEqualTo(1f));

            Assert.That(venues.AirportNoise(5_000), Is.EqualTo(0f).Within(0.0001f));
            venues.Add(VenueKind.Terminal);
            Assert.That(venues.Daily(5_000, WeatherKind.Clear).Income, Is.EqualTo(port));
            venues.Add(VenueKind.Runway);
            venues.Add(VenueKind.CargoHall);
            venues.Add(VenueKind.NoiseBarrier);
            var airport = venues.Daily(50_000, WeatherKind.Clear).Income;
            Assert.That(airport, Is.GreaterThan(port));
            Assert.That(venues.AirportNoise(50_000), Is.GreaterThan(0f));

            venues.Add(VenueKind.Promenade);
            Assert.That(venues.Daily(50_000, WeatherKind.Clear).Income, Is.GreaterThan(airport));
        }

        [Test]
        public void GreenCityLowersEmissionsAndWindNeedsWind()
        {
            var venues = new CityVenues();
            Assert.That(venues.TrafficEmissionFactor, Is.EqualTo(1f));
            for (var i = 0; i < 8; i++)
            {
                venues.Add(VenueKind.ChargeStation);
            }

            Assert.That(venues.ElectricShare, Is.GreaterThan(0f));
            Assert.That(venues.TrafficEmissionFactor, Is.LessThan(1f));

            venues.Add(VenueKind.WindFarm);
            var windy = venues.Daily(0, WeatherKind.Rain).Income;
            var calm = venues.Daily(0, WeatherKind.Fog).Income;
            var storm = venues.Daily(0, WeatherKind.Storm).Income;
            Assert.That(windy, Is.GreaterThan(calm));
            Assert.That(storm, Is.EqualTo(0));
        }

        [Test]
        public void ExtraTransitEarnsFromDemandButCostsUpkeep()
        {
            var venues = new CityVenues();
            venues.Add(VenueKind.Monorail);
            venues.Add(VenueKind.CableCar);
            venues.Add(VenueKind.Airship);
            venues.Add(VenueKind.Ferry);

            var day = venues.Daily(40_000, WeatherKind.Clear);

            Assert.That(day.Income, Is.GreaterThan(0));
            Assert.That(day.Upkeep, Is.GreaterThan(4 * CityVenues.UpkeepPerVenuePerDay));
        }

        [Test]
        public void HelicopterHelpsOnlyFarAwayAndNotInStorm()
        {
            var venues = new CityVenues();
            Assert.That(venues.HelicopterArrival(new float2(6000f, 0f), false), Is.NaN);
            venues.Add(VenueKind.Helicopter, float2.zero);
            venues.Add(VenueKind.Helicopter, new float2(5000f, 0f));

            Assert.That(venues.HelicopterArrival(new float2(500f, 0f), false), Is.NaN.Or.GreaterThan(0f));
            Assert.That(venues.HelicopterArrival(new float2(6000f, 0f), false), Is.EqualTo(145f).Within(0.01f).Or.GreaterThan(0f));
            Assert.That(venues.HelicopterArrival(new float2(6000f, 0f), true), Is.NaN);
            venues.Remove(VenueKind.Helicopter, float2.zero);
            Assert.That(venues.Count(VenueKind.Helicopter), Is.EqualTo(1));
            venues.Remove(VenueKind.Helicopter, new float2(9f, 9f));
            Assert.That(venues.Count(VenueKind.Helicopter), Is.EqualTo(0));
        }
    }
}
