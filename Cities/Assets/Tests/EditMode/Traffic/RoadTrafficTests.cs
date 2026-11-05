using System.Collections.Generic;
using Cities.Gameplay.Traffic;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Traffic
{
    [Category("UnityRuntime")]
    public class RoadTrafficTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp()
        {
            _network = new RoadNetwork(8);
            var a = _network.AddNode(new float3(0, 0, 0));
            var b = _network.AddNode(new float3(0, 0, 200));
            var c = _network.AddNode(new float3(200, 0, 200));
            var profile = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);
            _network.AddSegment(a, b, Bezier3.Straight(new float3(0, 0, 0), new float3(0, 0, 200)), profile, 13.9f);
            _network.AddSegment(b, c, Bezier3.Straight(new float3(0, 0, 200), new float3(200, 0, 200)), profile, 13.9f);
        }

        [TearDown]
        public void TearDown() => _network.Dispose();

        [Test]
        public void SpawnWithoutRoadFails()
        {
            using var empty = new RoadNetwork(2);
            Assert.That(new RoadTraffic(empty, 1).Spawn(), Is.False);
        }

        [Test]
        public void CarsDriveAlongTheRoad()
        {
            var traffic = new RoadTraffic(_network, 7);
            Assert.That(traffic.Spawn(), Is.True);
            var cars = new List<TrafficCar>();
            traffic.Collect(cars);
            var start = cars[0].Position;

            for (var i = 0; i < 100; i++)
            {
                traffic.Step(0.1f);
            }

            traffic.Collect(cars);
            Assert.That(cars.Count, Is.EqualTo(1));
            Assert.That(math.distance(cars[0].Position, start), Is.GreaterThan(5f));
        }

        [Test]
        public void ManyCarsFormQueuesAndCountStaysConstant()
        {
            var traffic = new RoadTraffic(_network, 3);
            var spawned = 0;
            for (var i = 0; i < 200; i++)
            {
                if (traffic.Spawn())
                {
                    spawned++;
                }

                traffic.Step(0.2f);
            }

            Assert.That(traffic.Count, Is.EqualTo(spawned));
            Assert.That(traffic.AverageOccupancy, Is.InRange(0f, 1f));
        }

        [Test]
        public void TrimRemovesCars()
        {
            var traffic = new RoadTraffic(_network, 5);
            for (var i = 0; i < 20; i++)
            {
                traffic.Spawn();
                traffic.Step(0.5f);
            }

            traffic.Trim(2);
            Assert.That(traffic.Count, Is.LessThanOrEqualTo(2));
        }
    }
}
