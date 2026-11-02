using System;
using System.Linq;
using Cities.Simulation.Transit;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class TransitFleetTests
    {
        private TransitNetwork _network;
        private TransitFleet _fleet;
        private int _a;
        private int _b;
        private int _c;
        private int _line;

        [SetUp]
        public void SetUp()
        {
            _network = new TransitNetwork();
            _a = _network.AddStop(new float2(0f, 0f));
            _b = _network.AddStop(new float2(100f, 0f));
            _c = _network.AddStop(new float2(200f, 0f));
            _line = _network.AddLine("1", new[] { _a, _b, _c }, false, 2, 60f);
            _fleet = new TransitFleet(_network);
            _fleet.AddDepot(5);
        }

        private void Run(float seconds)
        {
            for (var t = 0f; t < seconds; t += 1f)
            {
                _fleet.Tick(1f);
            }
        }

        [Test]
        public void ABusOnlyComesIntoServiceWithADepotAndFollowsTheHeadway()
        {
            var empty = new TransitFleet(_network);
            empty.Tick(100f);
            Assert.That(empty.Buses, Is.Empty);

            _fleet.Tick(59f);
            Assert.That(_fleet.Buses, Is.Empty);
            _fleet.Tick(1f);
            Assert.That(_fleet.Buses.Count, Is.EqualTo(1));
            Run(300f);
            Assert.That(_fleet.Buses.Count, Is.EqualTo(2), "niet meer dan het aantal voertuigen van de lijn");
        }

        [Test]
        public void TheDepotCapsTheFleet()
        {
            var small = new TransitFleet(_network);
            small.AddDepot(1);
            for (var i = 0; i < 400; i++)
            {
                small.Tick(1f);
            }

            Assert.That(small.Buses.Count, Is.EqualTo(1));
        }

        [Test]
        public void ABusLoadsWaitingPassengersAndDropsThemLater()
        {
            _fleet.AddWaiting(_a, 10);

            Run(600f);

            Assert.That(_fleet.Waiting(_a), Is.Zero);
            Assert.That(_fleet.Delivered, Is.EqualTo(10));
        }

        [Test]
        public void ABusDoesNotTakeMoreThanItsCapacity()
        {
            var station = _network.AddStop(new float2(0f, 50f));
            var line = _network.AddLine("2", new[] { station, _b }, false, 1, 10f);
            _fleet.SetStation(station);
            Assert.That(_fleet.AddWaiting(station, 1000), Is.EqualTo(TransitFleet.StationCapacity));
            Assert.That(_fleet.AddWaiting(station, 5), Is.Zero, "wachtruimte vol");

            _network.RemoveLine(_line);
            _fleet.Tick(10f);
            _fleet.Tick(1f);

            Assert.That(_fleet.Buses.Single(b => b.LineId == line).Count, Is.EqualTo(TransitFleet.BusCapacity));
            Assert.That(_fleet.Waiting(station), Is.EqualTo(TransitFleet.StationCapacity - TransitFleet.BusCapacity));
        }

        [Test]
        public void ABusServesTheMiddleAndTheEndStopAndStaysInsideTheCycle()
        {
            _fleet.AddWaiting(_b, 3);
            _fleet.AddWaiting(_c, 4);

            Run(1000f);

            Assert.That(_fleet.Waiting(_b) + _fleet.Waiting(_c), Is.Zero);
            Assert.That(_fleet.Buses.All(b => b.Offset >= 0f && b.Offset < 400f), Is.True, "een cyclus is heen en terug");
            Assert.That(_fleet.Delivered, Is.EqualTo(7));
        }

        [Test]
        public void ALoopWrapsAroundAndALargeStepGivesTheSameRide()
        {
            var loop = _network.AddLine("L", new[] { _a, _b, _c }, true, 1, 30f);
            _network.RemoveLine(_line);

            _fleet.Tick(30f);
            _fleet.AddWaiting(_a, 1);
            _fleet.Tick(1000f);

            Assert.That(_fleet.Buses.Single().LineId, Is.EqualTo(loop));
            Assert.That(_fleet.Delivered, Is.EqualTo(1));
        }

        [Test]
        public void AddingWaitingPassengersIgnoresNegativesAndChecksTheStop()
        {
            Assert.That(_fleet.AddWaiting(_a, -5), Is.Zero);
            Assert.Throws<ArgumentException>(() => _fleet.AddWaiting(99, 1));
            Assert.Throws<ArgumentException>(() => _fleet.SetStation(99));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new TransitFleet(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _fleet.AddDepot(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _fleet.Tick(-1f));
            Assert.That(_fleet.DepotCapacity, Is.EqualTo(5));
        }
    }
}
