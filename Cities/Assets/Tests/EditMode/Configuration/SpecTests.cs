using System;
using System.Collections.Generic;
using Cities.Simulation.Policies;
using Cities.Simulation.Vehicles;
using Cities.World.Buildings;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Configuration
{
    public class SpecTests
    {
        private static VehicleSpec CityBus() => new VehicleSpec
        {
            Category = VehicleCategory.Bus,
            LengthMeters = 12f,
            WidthMeters = 2.55f,
            MaxSpeedKmh = 80f,
            AccelerationMs2 = 1.2f,
            BrakingMs2 = 4f,
            PassengerCapacity = 80,
            CargoCapacityTonnes = 0f,
            PurchaseCost = 300_000,
            UpkeepPerMonth = 12_000,
        };

        private static BuildingSpec Terrace() => new BuildingSpec
        {
            FootprintWidth = 1,
            FootprintDepth = 2,
            Zone = ZoneType.Residential,
            Density = ZoneDensity.Low,
            Level = 1,
            Households = 1,
            ConstructionDays = 30,
        };

        private static List<string> Errors(Action<ICollection<string>> validate)
        {
            var errors = new List<string>();
            validate(errors);
            return errors;
        }

        [Test]
        public void RealisticBusIsValid()
        {
            var bus = CityBus();

            Assert.That(Errors(bus.Validate), Is.Empty);
            Assert.That(bus.MaxSpeedMetersPerSecond, Is.EqualTo(22.222f).Within(1e-3f));
            Assert.That(bus.BrakingDistanceFromMaxSpeed, Is.EqualTo(61.73f).Within(0.01f));
        }

        [Test]
        public void VehicleOutOfRangeIsReportedPerField()
        {
            var bus = CityBus();
            bus.LengthMeters = 0.5f;
            bus.MaxSpeedKmh = float.NaN;
            bus.PassengerCapacity = 4;
            bus.UpkeepPerMonth = -1;

            var errors = Errors(bus.Validate);

            Assert.That(errors, Has.Some.StartsWith(nameof(VehicleSpec.LengthMeters)));
            Assert.That(errors, Has.Some.StartsWith(nameof(VehicleSpec.MaxSpeedKmh)));
            Assert.That(errors, Has.Some.StartsWith(nameof(VehicleSpec.UpkeepPerMonth)));
            Assert.That(errors, Has.Some.Contains("minstens 10 reizigers"));
        }

        [Test]
        public void TruckNeedsCargoAndZeroBrakingHasNoDistance()
        {
            var truck = CityBus();
            truck.Category = VehicleCategory.Truck;
            truck.CargoCapacityTonnes = 0f;
            truck.BrakingMs2 = 0f;

            Assert.That(Errors(truck.Validate), Has.Some.Contains("laadvermogen"));
            Assert.That(truck.BrakingDistanceFromMaxSpeed, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void ResidentialBuildingRules()
        {
            var house = Terrace();
            Assert.That(Errors(house.Validate), Is.Empty);
            Assert.That(house.IsGrowable, Is.True);
            Assert.That(house.FootprintCells, Is.EqualTo(2));

            house.Households = 0;
            house.FootprintDepth = 5;
            house.ConstructionCost = 1000;
            var errors = Errors(house.Validate);

            Assert.That(errors, Has.Some.Contains("minstens één huishouden"));
            Assert.That(errors, Has.Some.Contains("4 cellen diep"));
            Assert.That(errors, Has.Some.Contains("kosten de stad niets"));
        }

        [Test]
        public void WorkBuildingsNeedJobsAndNoHouseholds()
        {
            var shop = Terrace();
            shop.Zone = ZoneType.Commercial;

            var errors = Errors(shop.Validate);

            Assert.That(errors, Has.Some.Contains("Alleen woongebouwen"));
            Assert.That(errors, Has.Some.Contains("arbeidsplaats"));
        }

        [Test]
        public void WorkplacesAreSplitByEducationAndAlwaysAddUp()
        {
            var office = Terrace();
            office.Zone = ZoneType.Office;
            office.Households = 0;
            office.Workplaces = 25;
            office.MediumEducationPercent = 30;
            office.HighEducationPercent = 50;

            Assert.That(Errors(office.Validate), Is.Empty);
            Assert.That(office.WorkplacesFor(EducationLevel.High), Is.EqualTo(13));
            Assert.That(office.WorkplacesFor(EducationLevel.Medium), Is.EqualTo(8));
            Assert.That(office.WorkplacesFor(EducationLevel.Low), Is.EqualTo(4));

            office.Workplaces = 1;
            office.MediumEducationPercent = 50;
            office.HighEducationPercent = 50;
            var total = office.WorkplacesFor(EducationLevel.Low) + office.WorkplacesFor(EducationLevel.Medium) + office.WorkplacesFor(EducationLevel.High);
            Assert.That(total, Is.EqualTo(1), "afronding verliest nooit een baan");
            Assert.That(office.WorkplacesFor(EducationLevel.Low), Is.GreaterThanOrEqualTo(0));

            office.HighEducationPercent = 60;
            Assert.That(Errors(office.Validate), Has.Some.Contains("hoogstens 100 procent"));
        }

        [Test]
        public void ShopCapacityAndProductionBelongToTheirZoneType()
        {
            var shop = Terrace();
            shop.Zone = ZoneType.Commercial;
            shop.Households = 0;
            shop.Workplaces = 6;

            Assert.That(Errors(shop.Validate), Has.Some.Contains("winkelcapaciteit"));
            shop.ShopCapacity = 4000;
            Assert.That(Errors(shop.Validate), Is.Empty);

            var factory = shop;
            factory.Zone = ZoneType.Industrial;
            var errors = Errors(factory.Validate);
            Assert.That(errors, Has.Some.Contains("Alleen winkelgebouwen"));
            Assert.That(errors, Has.Some.Contains("produceert iets"));

            factory.ShopCapacity = 0;
            factory.Production = 120;
            Assert.That(Errors(factory.Validate), Is.Empty);

            var house = Terrace();
            house.Production = 5;
            Assert.That(Errors(house.Validate), Has.Some.Contains("Alleen industriegebouwen"));
        }

        [Test]
        public void ServiceBuildingMayBeDeepAndCostMoney()
        {
            var school = new BuildingSpec
            {
                FootprintWidth = 6,
                FootprintDepth = 8,
                Zone = ZoneType.None,
                Level = 1,
                Workplaces = 40,
                ConstructionCost = 1_200_000,
                UpkeepPerMonth = 25_000,
                ConstructionDays = 120,
            };

            Assert.That(Errors(school.Validate), Is.Empty);
            Assert.That(school.IsGrowable, Is.False);
        }

        [Test]
        public void BuildingLevelIsLimited()
        {
            var house = Terrace();
            house.Level = BuildingSpec.MaxLevel + 1;

            Assert.That(Errors(house.Validate), Has.Some.StartsWith(nameof(BuildingSpec.Level)));
        }

        [Test]
        public void PolicyCostScalesWithCitizens()
        {
            var policy = new PolicySpec { Scope = PolicyScope.District, MonthlyCost = 500, MonthlyCostPerCitizen = 0.25f };

            Assert.That(Errors(policy.Validate), Is.Empty);
            Assert.That(policy.MonthlyCostFor(0), Is.EqualTo(500));
            Assert.That(policy.MonthlyCostFor(10_000), Is.EqualTo(3000));
            Assert.That(policy.MonthlyCostFor(2), Is.EqualTo(501), "0,5 rondt af naar boven");
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.MonthlyCostFor(-1));
        }

        [Test]
        public void PolicyRejectsNegativeCosts()
        {
            var policy = new PolicySpec { ActivationCost = -1, MonthlyCostPerCitizen = 200f, UnlockPopulation = -5 };

            var errors = Errors(policy.Validate);

            Assert.That(errors.Count, Is.EqualTo(3));
        }
    }
}
