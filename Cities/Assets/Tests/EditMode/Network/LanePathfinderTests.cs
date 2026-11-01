using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class LanePathfinderTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp() => _network = new RoadNetwork(16);

        [TearDown]
        public void TearDown() => _network.Dispose();

        /// <summary>Kruispunt op de oorsprong met twee armen van 50 m, de een naar het noorden, de ander naar het oosten.</summary>
        private (EntityId<LaneKind> Inbound, EntityId<LaneKind> Outbound) TwoArms()
        {
            var center = _network.AddNode(float3.zero);
            var profile = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);
            var north = _network.AddNode(new float3(0f, 0f, 50f));
            var east = _network.AddNode(new float3(50f, 0f, 0f));
            var a = _network.GetSegment(_network.AddSegment(center, north, Bezier3.Straight(float3.zero, new float3(0f, 0f, 50f)), profile, 13.9f));
            var b = _network.GetSegment(_network.AddSegment(center, east, Bezier3.Straight(float3.zero, new float3(50f, 0f, 0f)), profile, 13.9f));
            return (VehicleLane(a, LaneDirection.Backward), VehicleLane(b, LaneDirection.Forward));
        }

        private EntityId<LaneKind> VehicleLane(RoadSegment segment, LaneDirection direction) =>
            Enumerable.Range(0, segment.Lanes.Length)
                .Select(i => segment.Lanes[i])
                .First(l => _network.GetLane(l).Spec.Direction == direction && (_network.GetLane(l).Spec.Types & LaneTypes.Vehicles) != 0);

        [Test]
        public void ARouteFollowsTheConnectionOverTheCrossing()
        {
            var (inbound, outbound) = TwoArms();

            var (lanes, seconds) = new LanePathfinder(_network).FindRoute(inbound, outbound);

            Assert.That(lanes, Is.EqualTo(new[] { inbound, outbound }));
            Assert.That(seconds, Is.GreaterThan(50f / 13.9f * 0.9f), "de doellane van 50 m kost minstens zijn reistijd");
        }

        [Test]
        public void CongestionMakesTheRouteSlowerButNeverFaster()
        {
            var (inbound, outbound) = TwoArms();
            var free = new LanePathfinder(_network).FindRoute(inbound, outbound).Seconds;

            var busy = new LanePathfinder(_network, l => l == outbound ? 3f : 0.1f).FindRoute(inbound, outbound).Seconds;

            Assert.That(busy, Is.GreaterThan(free * 2f));
        }

        [Test]
        public void ARouteToAPedestrianLaneDoesNotExist()
        {
            var (inbound, _) = TwoArms();
            var segment = _network.GetSegment(_network.GetLane(inbound).Segment);
            var sidewalk = Enumerable.Range(0, segment.Lanes.Length)
                .Select(i => segment.Lanes[i])
                .First(l => _network.GetLane(l).Spec.Types == LaneTypes.Pedestrian);

            var (lanes, _) = new LanePathfinder(_network).FindRoute(inbound, sidewalk);

            Assert.That(lanes, Is.Empty);
        }

        [Test]
        public void ANullNetworkIsRejected() =>
            Assert.Throws<ArgumentNullException>(() => new LanePathfinder(null));
    }
}
