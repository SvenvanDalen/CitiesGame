using Cities.Gameplay.Tools;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class RoadInspectionTests
    {
        private RoadNetwork _network;
        private RoadInspection _source;

        [SetUp]
        public void SetUp()
        {
            _network = new RoadNetwork(8);
            _source = new RoadInspection(_network);
            var middle = _network.AddNode(new float3(100f, 10f, 100f));
            foreach (var end in new[] { new float3(200f, 10f, 100f), new float3(100f, 10f, 200f), new float3(0f, 10f, 100f) })
            {
                _network.AddSegment(middle, _network.AddNode(end), Bezier3.Straight(new float3(100f, 10f, 100f), end), LaneProfiles.Road(1, false, true), 13.9f);
            }
        }

        [TearDown]
        public void TearDown() => _network.Dispose();

        [Test]
        public void ASegmentReportsLengthSpeedAndLanes()
        {
            Assert.That(_source.TryInspect(new float3(150f, 10f, 101f), out var report), Is.True);

            Assert.That(report.Title, Is.EqualTo("Weg"));
            Assert.That(report.ToString(), Does.Contain("100 m").And.Contain("50 km/u").And.Contain("2 rijbanen"));
        }

        [Test]
        public void ANodeReportsItsArmsAndConnections()
        {
            Assert.That(_source.TryInspect(new float3(101f, 10f, 100f), out var report), Is.True);

            Assert.That(report.Title, Is.EqualTo("Kruispunt"));
            Assert.That(report.ToString(), Does.Contain("3 wegen").And.Contain("verbindingen"));
        }

        [Test]
        public void EmptyGroundReportsNothing()
        {
            Assert.That(_source.TryInspect(new float3(500f, 10f, 500f), out _), Is.False);
            Assert.Throws<System.ArgumentNullException>(() => new RoadInspection(null));
        }
    }
}
