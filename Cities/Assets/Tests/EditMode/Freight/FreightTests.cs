using System;
using Cities.Core.Persistence;
using Cities.Simulation.Freight;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Freight
{
    public class FreightTests
    {
        private const float Truck = 10f;

        private static float Drive(FreightNode from, FreightNode to) => math.distance(from.Position, to.Position) / 10f;

        private static FreightDispatcher Dispatcher(FreightNetwork network, Func<FreightNode, FreightNode, float> travel = null) =>
            new FreightDispatcher(network, Truck, travel ?? Drive);

        [Test]
        public void AProducerFillsItsStorageAndLosesWhatDoesNotFit()
        {
            var network = new FreightNetwork();
            var factory = network.AddProducer(1, float3.zero, 4f, 10f);

            network.Step(2f);
            Assert.That(factory.Level, Is.EqualTo(8f).Within(0.001f));

            network.Step(2f);
            Assert.That(factory.Level, Is.EqualTo(10f).Within(0.001f));
            Assert.That(factory.Lost, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void AShopSellsItsStockAndRecordsWhatItCouldNotSell()
        {
            var network = new FreightNetwork();
            var shop = network.AddShop(2, float3.zero, 5f, 10f);

            network.Step(1f);
            Assert.That(shop.Level, Is.EqualTo(5f).Within(0.001f));
            Assert.That(shop.IsEmpty, Is.False);

            network.Step(3f);
            Assert.That(shop.IsEmpty, Is.True);
            Assert.That(shop.Unmet, Is.EqualTo(10f).Within(0.001f), "van 20 ton vraag was er 10 ton");
            Assert.That(shop.EmptyDays, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void TheNearestMatchingNodeWinsAndTiesGoToTheLowestId()
        {
            var network = new FreightNetwork();
            var far = network.AddProducer(1, new float3(100, 0, 0), 1f, 5f);
            var a = network.AddProducer(2, new float3(10, 0, 0), 1f, 5f);
            var b = network.AddProducer(3, new float3(-10, 0, 0), 1f, 5f);

            Assert.That(network.FindNearest(FreightNodeKind.Producer, float3.zero, n => true), Is.SameAs(a));
            Assert.That(network.FindNearest(FreightNodeKind.Producer, float3.zero, n => n.Id != a.Id), Is.SameAs(b));
            Assert.That(network.FindNearest(FreightNodeKind.Shop, float3.zero, n => true), Is.Null);
            Assert.That(far.Id, Is.EqualTo(0));
        }

        [Test]
        public void ARunningShopGetsATruckFromTheNearestFactory()
        {
            var network = new FreightNetwork();
            var factory = network.AddProducer(1, new float3(100, 0, 0), 0f, 50f);
            factory.Level = 15f;
            var shop = network.AddShop(2, float3.zero, 10f, 20f);
            var outside = network.AddOutsideConnection(new float3(0, 0, 500));
            var dispatcher = Dispatcher(network);
            dispatcher.AddTruck(outside.Id);
            network.Step(1.5f);
            Assert.That(shop.Level, Is.LessThan(shop.Capacity * FreightDispatcher.ReorderShare));

            dispatcher.Step(0f);
            Assert.That(shop.Reserved, Is.GreaterThan(0f), "de bestelling is toegezegd");
            Assert.That(factory.Reserved, Is.GreaterThan(0f));
            Assert.That(dispatcher.Trucks[0].State, Is.EqualTo(FreightTruckState.ToPickup));

            var before = shop.Level;
            for (var i = 0; i < 100 && dispatcher.Trips == 0; i++)
            {
                dispatcher.Step(5f);
            }

            Assert.That(dispatcher.Trips, Is.EqualTo(1));
            Assert.That(shop.Level, Is.GreaterThan(before));
            Assert.That(shop.Reserved, Is.EqualTo(0f).Within(0.001f));
            Assert.That(factory.Reserved, Is.EqualTo(0f).Within(0.001f));
            Assert.That(dispatcher.DeliveredTonnes, Is.GreaterThan(0f));
            Assert.That(dispatcher.ImportedTonnes, Is.EqualTo(0f), "geleverd door een fabriek, dus geen import");
            Assert.That(dispatcher.Trucks[0].State, Is.EqualTo(FreightTruckState.Idle));
            Assert.That(dispatcher.Trucks[0].At, Is.EqualTo(shop.Id));
        }

        [Test]
        public void WithoutAFactoryTheShopImportsFromTheOutsideConnection()
        {
            var network = new FreightNetwork();
            var shop = network.AddShop(2, float3.zero, 10f, 20f);
            var outside = network.AddOutsideConnection(new float3(0, 0, 50));
            var dispatcher = Dispatcher(network);
            dispatcher.AddTruck(outside.Id);
            network.Step(1.5f);

            dispatcher.Step(0f);
            for (var i = 0; i < 100 && dispatcher.Trips == 0; i++)
            {
                dispatcher.Step(1f);
            }

            Assert.That(dispatcher.ImportedTonnes, Is.GreaterThan(0f));
            Assert.That(dispatcher.TakeTradeBalance(), Is.LessThan(0f), "import telt negatief mee in de handelsbalans");
            Assert.That(dispatcher.ImportedTonnes, Is.EqualTo(0f), "de tellers zijn teruggezet");
            Assert.That(shop.Level, Is.GreaterThan(5f));
        }

        [Test]
        public void AFullLoadAtAFactoryIsExported()
        {
            var network = new FreightNetwork();
            var factory = network.AddProducer(1, float3.zero, 1f, 40f);
            factory.Level = 18f;
            var outside = network.AddOutsideConnection(new float3(0, 0, 30));
            var dispatcher = Dispatcher(network);
            dispatcher.AddTruck(factory.Id);

            dispatcher.Step(0f);
            for (var i = 0; i < 20 && dispatcher.Trips == 0; i++)
            {
                dispatcher.Step(1f);
            }

            Assert.That(dispatcher.ExportedTonnes, Is.EqualTo(Truck).Within(0.001f));
            Assert.That(factory.Level, Is.EqualTo(8f).Within(0.001f));
            Assert.That(dispatcher.TakeTradeBalance(), Is.EqualTo(Truck).Within(0.001f));
            Assert.That(dispatcher.Trucks[0].At, Is.EqualTo(outside.Id));
        }

        [Test]
        public void ALoadSmallerThanATruckStaysAtTheFactory()
        {
            var network = new FreightNetwork();
            var factory = network.AddProducer(1, float3.zero, 1f, 40f);
            factory.Level = 4f;
            network.AddOutsideConnection(new float3(0, 0, 30));
            var dispatcher = Dispatcher(network);
            dispatcher.AddTruck(factory.Id);

            dispatcher.Step(1f);

            Assert.That(dispatcher.Trucks[0].State, Is.EqualTo(FreightTruckState.Idle));
        }

        [Test]
        public void WithoutARouteNoTruckLeavesAndItIsCounted()
        {
            var network = new FreightNetwork();
            network.AddShop(2, float3.zero, 10f, 20f).Level = 2f;
            var outside = network.AddOutsideConnection(new float3(0, 0, 50));
            var dispatcher = Dispatcher(network, (a, b) => float.PositiveInfinity);
            dispatcher.AddTruck(outside.Id);

            dispatcher.Step(1f);

            Assert.That(dispatcher.Trucks[0].State, Is.EqualTo(FreightTruckState.Idle));
            Assert.That(dispatcher.Unreachable, Is.EqualTo(1));
        }

        [Test]
        public void ABusyFleetDoesNotOverbookAShop()
        {
            var network = new FreightNetwork();
            var shop = network.AddShop(2, float3.zero, 10f, 20f);
            shop.Level = 0f;
            var outside = network.AddOutsideConnection(new float3(0, 0, 50));
            var dispatcher = Dispatcher(network);
            dispatcher.AddTruck(outside.Id);
            dispatcher.AddTruck(outside.Id);
            dispatcher.AddTruck(outside.Id);

            dispatcher.Step(1f);
            dispatcher.Step(1f);

            Assert.That(shop.Reserved, Is.LessThanOrEqualTo(shop.Capacity + 0.001f));
            var busy = 0;
            foreach (var truck in dispatcher.Trucks)
            {
                busy += truck.State == FreightTruckState.Idle ? 0 : 1;
            }

            Assert.That(busy, Is.EqualTo(2), "20 ton ruimte is twee vrachten van 10 ton");
        }

        [Test]
        public void ConstructorsAndInputsAreChecked()
        {
            var network = new FreightNetwork();
            Assert.Throws<ArgumentNullException>(() => new FreightDispatcher(null, Truck, Drive));
            Assert.Throws<ArgumentNullException>(() => new FreightDispatcher(network, Truck, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FreightDispatcher(network, 0f, Drive));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.AddProducer(1, float3.zero, -1f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.AddShop(1, float3.zero, 1f, -5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.Step(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.Get(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => Dispatcher(network).AddTruck(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Dispatcher(network).Step(-1f));
            Assert.Throws<ArgumentNullException>(() => new FreightSection(null));
        }

        [Test]
        public void FreightSurvivesSavingAndLoading()
        {
            var network = new FreightNetwork();
            var factory = network.AddProducer(7, new float3(1, 2, 3), 5f, 50f);
            factory.Level = 30f;
            network.AddShop(8, new float3(40, 0, 0), 10f, 20f).Level = 2f;
            var outside = network.AddOutsideConnection(new float3(0, 0, 90));
            var dispatcher = Dispatcher(network);
            dispatcher.AddTruck(outside.Id);
            dispatcher.AddTruck(factory.Id);
            dispatcher.Step(1f);
            dispatcher.Step(1f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new FreightSection(dispatcher));

            var loadedNetwork = new FreightNetwork();
            var loaded = Dispatcher(loadedNetwork);
            SaveBytes.Read(bytes, new FreightSection(loaded));

            Assert.That(loadedNetwork.Nodes.Count, Is.EqualTo(3));
            Assert.That(loadedNetwork.Nodes[0].Owner, Is.EqualTo(7UL));
            Assert.That(loadedNetwork.Nodes[0].Position, Is.EqualTo(new float3(1, 2, 3)));
            Assert.That(loadedNetwork.Nodes[0].Reserved, Is.EqualTo(factory.Reserved).Within(0.001f));
            Assert.That(loaded.Trucks.Count, Is.EqualTo(2));
            Assert.That(loaded.Trucks[0].State, Is.EqualTo(dispatcher.Trucks[0].State));
            Assert.That(loaded.Trucks[0].Planned, Is.EqualTo(dispatcher.Trucks[0].Planned).Within(0.001f));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new FreightSection(loaded)), Is.EqualTo(bytes), "deterministisch opgeslagen");
        }
    }
}
