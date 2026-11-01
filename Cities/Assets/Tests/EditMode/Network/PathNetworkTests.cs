using System;
using System.Collections.Generic;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    public class PathNetworkTests
    {
        private static LaneSpec Lane(LaneTypes types, LaneDirection direction) => new LaneSpec(types, direction, 3f, 1.8f);

        [Test]
        public void ARouteFollowsThePathsAndReportsLengthAndTime()
        {
            var net = new PathNetwork();
            var a = net.AddNode(new float3(0f, 0f, 0f));
            var b = net.AddNode(new float3(100f, 0f, 0f));
            var c = net.AddNode(new float3(100f, 0f, 100f));
            net.AddPath(a, b, PathModes.Both);
            net.AddPath(b, c, PathModes.Both);

            var walk = net.Route(a, c, PathMode.Walk);
            var bike = net.Route(a, c, PathMode.Bike);

            Assert.That(walk.Found, Is.True);
            Assert.That(walk.Nodes, Is.EqualTo(new[] { a, b, c }));
            Assert.That(walk.Length, Is.EqualTo(200f).Within(1e-3f));
            Assert.That(walk.Seconds, Is.EqualTo(200f / PathNetwork.WalkSpeed).Within(1e-2f));
            Assert.That(bike.Seconds, Is.LessThan(walk.Seconds), "fietsen is sneller");
        }

        [Test]
        public void ARouteToTheSameNodeIsEmptyButFound()
        {
            var net = new PathNetwork();
            var a = net.AddNode(float3.zero);

            var route = net.Route(a, a, PathMode.Walk);

            Assert.That(route.Found, Is.True);
            Assert.That(route.Nodes, Is.EqualTo(new[] { a }));
            Assert.That(route.Length, Is.Zero);
        }

        [Test]
        public void ModesOnlyUsePathsMeantForThem()
        {
            var net = new PathNetwork();
            var a = net.AddNode(float3.zero);
            var b = net.AddNode(new float3(50f, 0f, 0f));
            net.AddPath(a, b, PathModes.Walk);

            Assert.That(net.Route(a, b, PathMode.Walk).Found, Is.True);
            var bike = net.Route(a, b, PathMode.Bike);
            Assert.That(bike.Found, Is.False, "geen fietspad");
            Assert.That(bike.Nodes, Is.Empty);
        }

        [Test]
        public void TheShorterWayInTimeWinsAndHillsSlowDownTheClimb()
        {
            var net = new PathNetwork();
            var a = net.AddNode(float3.zero);
            var top = net.AddNode(new float3(100f, 40f, 0f));
            var b = net.AddNode(new float3(200f, 0f, 0f));
            var detour = net.AddNode(new float3(100f, 0f, 60f));
            net.AddPath(a, top, PathModes.Both);
            net.AddPath(top, b, PathModes.Both);
            net.AddPath(a, detour, PathModes.Both);
            net.AddPath(detour, b, PathModes.Both);

            var route = net.Route(a, b, PathMode.Walk);

            Assert.That(route.Nodes, Is.EqualTo(new[] { a, detour, b }), "de omweg is vlakker en dus sneller dan over de heuvel");
        }

        [Test]
        public void RoadSidewalksAndBikeLanesJoinTheNetwork()
        {
            var net = new PathNetwork();
            var a = net.AddNode(float3.zero);
            var b = net.AddNode(new float3(100f, 0f, 0f));
            net.AddRoadSegment(a, b, new List<LaneSpec>
            {
                Lane(LaneTypes.Car, LaneDirection.Forward),
                Lane(LaneTypes.Bike, LaneDirection.Forward),
                Lane(LaneTypes.Pedestrian, LaneDirection.Both),
            });

            Assert.That(net.Route(a, b, PathMode.Walk).Found, Is.True);
            Assert.That(net.Route(b, a, PathMode.Walk).Found, Is.True, "trottoir beide kanten op");
            Assert.That(net.Route(a, b, PathMode.Bike).Found, Is.True);
            Assert.That(net.Route(b, a, PathMode.Bike).Found, Is.False, "fietsstrook alleen in rijrichting");
        }

        [Test]
        public void CarLanesAloneGiveNoPaths()
        {
            var net = new PathNetwork();
            var a = net.AddNode(float3.zero);
            var b = net.AddNode(new float3(10f, 0f, 0f));
            net.AddRoadSegment(a, b, new List<LaneSpec> { Lane(LaneTypes.Car, LaneDirection.Both), Lane(LaneTypes.Bike, LaneDirection.Backward) });

            Assert.That(net.Route(a, b, PathMode.Walk).Found, Is.False);
            Assert.That(net.Route(a, b, PathMode.Bike).Found, Is.False, "achterwaartse fietsstrook loopt van b naar a");
            Assert.That(net.Route(b, a, PathMode.Bike).Found, Is.True);
        }

        [Test]
        public void BadInputIsRejected()
        {
            var net = new PathNetwork();
            var a = net.AddNode(float3.zero);
            var b = net.AddNode(float3.zero);

            Assert.That(net.NodeCount, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => net.AddPath(a, b, PathModes.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => net.AddPath(a, 5, PathModes.Walk));
            Assert.Throws<ArgumentOutOfRangeException>(() => net.Route(-1, a, PathMode.Walk));
            Assert.Throws<ArgumentNullException>(() => net.AddRoadSegment(a, b, null));
        }
    }
}
