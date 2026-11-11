using System;
using Cities.Simulation.Aviation;
using Cities.Simulation.Fields;
using Cities.Simulation.Freight;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Freight
{
    public class OptionalGrowthTests
    {
        [Test]
        public void WarehouseBuffersAndLosesTheOverflow()
        {
            var warehouse = new Warehouse(100);

            Assert.That(warehouse.Store(80), Is.EqualTo(80));
            Assert.That(warehouse.Store(50), Is.EqualTo(20));
            Assert.That(warehouse.Lost, Is.EqualTo(30));
            Assert.That(warehouse.Take(500), Is.EqualTo(100));
            Assert.That(warehouse.Stored, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => warehouse.Store(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => warehouse.Take(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Warehouse(0));
        }

        [Test]
        public void OverfishingShrinksTheStockAndItRecovers()
        {
            var fishing = new PortFishing();
            var first = fishing.FishDay(20);
            fishing.FishDay(20);
            fishing.FishDay(20);

            Assert.That(first, Is.EqualTo(100));
            Assert.That(fishing.Stock, Is.LessThan(1f));
            Assert.That(fishing.FishDay(20), Is.LessThan(first));
            var low = fishing.Stock;
            fishing.FishDay(0);
            Assert.That(fishing.Stock, Is.GreaterThan(low));
            Assert.That(PortFishing.Throughput(2, 1), Is.EqualTo(160));
            Assert.That(PortFishing.WasteProcessed(3), Is.EqualTo(90));
            Assert.Throws<ArgumentOutOfRangeException>(() => fishing.FishDay(-1));
        }

        [Test]
        public void AirportCapacityAndNoiseFollowTheLayout()
        {
            var airport = new AirportComplex { Terminals = 2, Runways = 1, CargoHalls = 1 };

            Assert.That(airport.PassengerCapacity, Is.EqualTo(12_000));
            Assert.That(airport.CargoTonsPerDay, Is.EqualTo(250));
            var loud = airport.Noise(300);
            airport.NightBan = true;
            airport.NoiseBarriers = 2;
            Assert.That(airport.Noise(300), Is.LessThan(loud));
            airport.NoiseBarriers = 99;
            Assert.That(airport.Noise(600), Is.GreaterThan(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => airport.Noise(-1));
        }

        [Test]
        public void PromenadesDrawPeopleAndLiftShops()
        {
            var plain = new Promenade(500f, 10, false);
            var plaza = new Promenade(500f, 40, true);

            Assert.That(plain.PedestriansPerHour, Is.EqualTo(1000));
            Assert.That(plaza.PedestriansPerHour, Is.EqualTo(1500));
            Assert.That(plain.ShopBonus, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(plaza.ShopBonus, Is.EqualTo(0.3f).Within(0.001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Promenade(0f, 1, false));
        }
    }
}
