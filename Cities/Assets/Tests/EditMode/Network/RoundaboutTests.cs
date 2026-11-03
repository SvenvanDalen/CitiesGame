using System;
using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class RoundaboutTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp() => _network = new RoadNetwork(16);

        [TearDown]
        public void TearDown() => _network.Dispose();

        [Test]
        public void TheRingIsALoopOfOneWaySegmentsOnTheCircle()
        {
            var ring = Roundabout.Build(_network, float3.zero, 20f, 4);

            Assert.That(ring.Nodes.Length, Is.EqualTo(4));
            Assert.That(ring.Segments.Length, Is.EqualTo(4));
            Assert.That(ring.Segments.All(s => _network.IsOneWay(_network.GetSegment(s))), Is.True);
            Assert.That(_network.GetSegment(ring.Segments[3]).End, Is.EqualTo(ring.Nodes[0]), "de ring sluit");
            Assert.That(ring.Segments.All(s => Roundabout.IsRingSegment(_network, s)), Is.True);
            Assert.That(_network.GetNode(ring.Nodes[1]).Position.z, Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void AnAccessRoadGivesWayToTheRingButARingSegmentDoesNot()
        {
            var ring = Roundabout.Build(_network, float3.zero, 20f, 4);
            var outer = _network.AddNode(new float3(80f, 0f, 0f));
            var arm = _network.AddSegment(
                outer,
                ring.Nodes[0],
                Bezier3.Straight(new float3(80f, 0f, 0f), new float3(20f, 0f, 0f)),
                LaneProfiles.Road(1, bikeLanes: false, sidewalks: true),
                13.9f);

            Assert.That(Roundabout.IsRingSegment(_network, arm), Is.False);
            Assert.That(Roundabout.GivesWay(_network, arm, ring.Nodes[0]), Is.True);
            Assert.That(Roundabout.GivesWay(_network, ring.Segments[0], ring.Nodes[1]), Is.False);
            Assert.That(Roundabout.GivesWay(_network, arm, ring.Nodes[1]), Is.False, "de arm komt niet bij dit knooppunt");
        }

        [Test]
        public void ALoneOneWayRoadIsNoRing()
        {
            var a = _network.AddNode(float3.zero);
            var b = _network.AddNode(new float3(50f, 0f, 0f));
            var road = _network.AddSegment(a, b, Bezier3.Straight(float3.zero, new float3(50f, 0f, 0f)), LaneProfiles.Road(1, false, false, oneWay: true), 13.9f);

            Assert.That(Roundabout.IsRingSegment(_network, road), Is.False);
            Assert.That(Roundabout.GivesWay(_network, road, b), Is.False);
        }

        [Test]
        public void BuildingNeedsOpenTerrainAndValidSettings()
        {
            Assert.That(Roundabout.CanBuild(_network, float3.zero, 20f, 4), Is.Null);
            Assert.That(Roundabout.CanBuild(_network, float3.zero, 5f, 4), Does.Contain("straal"));

            Roundabout.Build(_network, float3.zero, 20f, 4);

            Assert.That(Roundabout.CanBuild(_network, new float3(10f, 0f, 0f), 20f, 4), Does.Contain("weg"));
            Assert.That(Roundabout.CanBuild(_network, new float3(200f, 0f, 0f), 20f, 4), Is.Null);
            Assert.Throws<ArgumentException>(() => Roundabout.Build(_network, float3.zero, 99f, 4));
        }
    }
}
