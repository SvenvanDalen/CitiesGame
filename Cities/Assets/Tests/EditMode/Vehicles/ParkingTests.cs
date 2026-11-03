using System;
using Cities.Core.Persistence;
using Cities.Simulation.Vehicles;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Vehicles
{
    public class ParkingTests
    {
        [Test]
        public void ParkingFillsALotAndLeavingFreesAPlace()
        {
            var registry = new ParkingRegistry();
            var lot = registry.Add(ParkingKind.Building, 7, float3.zero, 2);

            Assert.That(registry.TryPark(lot.Id), Is.True);
            Assert.That(registry.TryPark(lot.Id), Is.True);
            Assert.That(registry.TryPark(lot.Id), Is.False, "vol");
            Assert.That(registry.TryPark(99), Is.False, "onbekend lot");
            Assert.That(registry.Occupied, Is.EqualTo(2));

            registry.Leave(lot.Id);
            registry.Leave(99);
            Assert.That(lot.Free, Is.EqualTo(1));
            Assert.That(registry.Capacity, Is.EqualTo(2));
        }

        [Test]
        public void NearestFreeLotWinsAndFullOrFarLotsAreSkipped()
        {
            var registry = new ParkingRegistry();
            var full = registry.Add(ParkingKind.Street, 1, new float3(10, 0, 0), 1);
            registry.TryPark(full.Id);
            var near = registry.Add(ParkingKind.Garage, 2, new float3(50, 0, 0), 5);
            registry.Add(ParkingKind.Street, 3, new float3(500, 0, 0), 5);

            Assert.That(registry.FindNearestFree(float3.zero, 100f), Is.SameAs(near));
            Assert.That(registry.FindNearestFree(float3.zero, 20f), Is.Null);
        }

        [Test]
        public void ClosedLotsTakeNoNewCars()
        {
            var registry = new ParkingRegistry();
            var lot = registry.Add(ParkingKind.Street, 4, float3.zero, 3);
            registry.Close(ParkingKind.Street, 4);
            registry.Close(ParkingKind.Garage, 4);

            Assert.That(registry.TryPark(lot.Id), Is.False);
        }

        [Test]
        public void ASearchWidensAndCirclesUntilAPlaceIsFree()
        {
            var registry = new ParkingRegistry();
            var far = registry.Add(ParkingKind.Garage, 1, new float3(400, 0, 0), 1);
            var search = new ParkingSearch();

            Assert.That(search.TryPark(registry, float3.zero), Is.Null, "binnen 150 m is niets");
            Assert.That(search.TryPark(registry, float3.zero), Is.Null, "binnen 300 m ook niet");
            Assert.That(search.Rounds, Is.EqualTo(2));
            Assert.That(search.TryPark(registry, float3.zero), Is.SameAs(far), "binnen 450 m wel");
            Assert.That(far.Occupied, Is.EqualTo(1));
        }

        [Test]
        public void ASearchGivesUpWhenEverythingStaysFull()
        {
            var registry = new ParkingRegistry();
            var search = new ParkingSearch();
            for (var i = 0; i < ParkingSearch.MaxRounds; i++)
            {
                Assert.That(search.GaveUp, Is.False);
                Assert.That(search.TryPark(registry, float3.zero), Is.Null);
            }

            Assert.That(search.GaveUp, Is.True);
            registry.Add(ParkingKind.Street, 1, float3.zero, 1);
            Assert.That(search.TryPark(registry, float3.zero), Is.Null, "wie opgegeven heeft zoekt niet meer");
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var registry = new ParkingRegistry();
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.Add(ParkingKind.Street, 1, float3.zero, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.Add((ParkingKind)9, 1, float3.zero, 1));
            Assert.Throws<ArgumentNullException>(() => new ParkingSearch().TryPark(null, float3.zero));
            Assert.Throws<ArgumentNullException>(() => new ParkingSection(null));
        }

        [Test]
        public void ParkingSurvivesSavingAndLoading()
        {
            var registry = new ParkingRegistry();
            var a = registry.Add(ParkingKind.Garage, 9, new float3(1, 2, 3), 10);
            registry.Add(ParkingKind.Street, 5, new float3(4, 5, 6), 3);
            registry.TryPark(a.Id);
            registry.TryPark(a.Id);
            var bytes = SaveBytes.Write(new SaveMetadata(), new ParkingSection(registry));

            var loaded = new ParkingRegistry();
            SaveBytes.Read(bytes, new ParkingSection(loaded));

            Assert.That(loaded.Lots.Count, Is.EqualTo(2));
            Assert.That(loaded.Lots[0].Kind, Is.EqualTo(ParkingKind.Garage));
            Assert.That(loaded.Lots[0].Occupied, Is.EqualTo(2));
            Assert.That(loaded.Lots[0].Position, Is.EqualTo(new float3(1, 2, 3)));
            Assert.That(loaded.Lots[1].Owner, Is.EqualTo(5UL));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new ParkingSection(loaded)), Is.EqualTo(bytes), "deterministisch opgeslagen");
        }
    }
}
