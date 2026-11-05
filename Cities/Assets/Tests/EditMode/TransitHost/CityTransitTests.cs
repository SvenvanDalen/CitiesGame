using System.Linq;
using Cities.Gameplay.Transit;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.TransitHost
{
    public class CityTransitTests
    {
        [Test]
        public void NeedsTwoStopsForALine()
        {
            var transit = new CityTransit();
            transit.AddStop(new float2(0, 0));
            Assert.That(transit.CreateLineFromUnlinked("Lijn 1"), Is.EqualTo(-1));
        }

        [Test]
        public void LineUsesAllUnlinkedStopsOrderedByDistance()
        {
            var transit = new CityTransit();
            var a = transit.AddStop(new float2(0, 0));
            var far = transit.AddStop(new float2(1000, 0));
            var near = transit.AddStop(new float2(100, 0));

            var id = transit.CreateLineFromUnlinked("Lijn 1");

            Assert.That(id, Is.GreaterThanOrEqualTo(0));
            Assert.That(transit.Network.GetLine(id).Stops.ToArray(), Is.EqualTo(new[] { a, near, far }));
            Assert.That(transit.UnlinkedStops(), Is.Empty);
        }

        [Test]
        public void BusesSpawnDriveAndPickUpPassengers()
        {
            var transit = new CityTransit();
            transit.AddStop(new float2(0, 0));
            transit.AddStop(new float2(300, 0));
            var line = transit.CreateLineFromUnlinked("Lijn 1", 2, 10f);

            for (var i = 0; i < 60; i++)
            {
                transit.Daily(2000);
                transit.Fleet.Tick(5f);
            }

            Assert.That(transit.Fleet.Buses.Count, Is.GreaterThan(0));
            Assert.That(transit.Fleet.Boardings(line), Is.GreaterThan(0));
            var position = transit.PositionOf(transit.Fleet.Buses[0]);
            Assert.That(position.x, Is.InRange(-1f, 301f));
        }

        [Test]
        public void AdjustVehiclesStaysWithinBounds()
        {
            var transit = new CityTransit();
            transit.AddStop(float2.zero);
            transit.AddStop(new float2(200, 0));
            var line = transit.CreateLineFromUnlinked("Lijn 1", 2);

            transit.AdjustVehicles(line, -10);
            Assert.That(transit.Network.GetLine(line).Vehicles, Is.EqualTo(1));
            transit.AdjustVehicles(line, +3);
            Assert.That(transit.Network.GetLine(line).Vehicles, Is.EqualTo(4));
        }

        [Test]
        public void NoPassengersWithoutStopsOrPeople()
        {
            var transit = new CityTransit();
            Assert.DoesNotThrow(() => transit.Daily(5000));
            var stop = transit.AddStop(float2.zero);
            transit.Daily(0);
            Assert.That(transit.Fleet.Waiting(stop), Is.EqualTo(0));
        }
    }
}
