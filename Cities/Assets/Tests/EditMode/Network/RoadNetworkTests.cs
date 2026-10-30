using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Core.Ids;
using Cities.Core.Persistence;
using Cities.Simulation.Network;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class RoadNetworkTests
    {
        private static readonly List<LaneSpec> TwoLane = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);

        private RoadNetwork _network;

        [SetUp]
        public void SetUp() => _network = new RoadNetwork(16);

        [TearDown]
        public void TearDown() => _network.Dispose();

        private EntityId<SegmentKind> Road(EntityId<NodeKind> from, EntityId<NodeKind> to, IReadOnlyList<LaneSpec> profile = null) =>
            _network.AddSegment(from, to, Bezier3.Straight(_network.GetNode(from).Position, _network.GetNode(to).Position), profile ?? TwoLane, 13.9f);

        /// <summary>Kruispunt op de oorsprong met vier armen van 50 m die er allemaal beginnen.</summary>
        private (EntityId<NodeKind> Center, EntityId<SegmentKind>[] Arms) Crossroads()
        {
            var center = _network.AddNode(float3.zero);
            var arms = new[] { new float3(0f, 0f, 50f), new float3(50f, 0f, 0f), new float3(0f, 0f, -50f), new float3(-50f, 0f, 0f) }
                .Select(p => Road(center, _network.AddNode(p)))
                .ToArray();
            return (center, arms);
        }

        [Test]
        public void SegmentsGetLanesThatFollowTheirDirection()
        {
            var a = _network.AddNode(new float3(0f, 10f, 0f));
            var b = _network.AddNode(new float3(0f, 10f, 100f));

            var segment = _network.GetSegment(Road(a, b));
            var forward = _network.GetLane(segment.Lanes[0]);
            var backward = _network.GetLane(segment.Lanes[1]);

            Assert.That(segment.Length, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(segment.Lanes.Length, Is.EqualTo(4));
            Assert.That(forward.Curve.P0, Is.EqualTo(new float3(1.625f, 10f, 0f)), "heen rechts van de hartlijn");
            Assert.That(backward.Curve.P0, Is.EqualTo(new float3(-1.625f, 10f, 100f)), "terug begint bij het eind");
            Assert.That(backward.Curve.Direction(0.5f).z, Is.LessThan(0f));
            Assert.That(_network.GetNode(a).Segments.Length, Is.EqualTo(1));
            Assert.That(segment.Other(a), Is.EqualTo(b));
        }

        [Test]
        public void CrossroadsConnectLanesWithCurvesOverTheNode()
        {
            var (center, arms) = Crossroads();
            var connections = new List<LaneConnection>();

            // Rijdend naar het kruispunt over de zuidelijke arm: de terug-lane (die begint buiten en eindigt bij het midden).
            var incoming = _network.GetSegment(arms[2]).Lanes[1];
            _network.GetConnections(incoming, connections);

            Assert.That(connections.Select(c => c.Turn), Is.EquivalentTo(new[] { Turn.Straight, Turn.Left, Turn.Right }));
            Assert.That(connections.All(c => c.Node == center), Is.True);
            foreach (var connection in connections)
            {
                Assert.That(connection.Curve.P0, Is.EqualTo(_network.GetLane(incoming).Curve.P3));
                Assert.That(connection.Curve.P3, Is.EqualTo(_network.GetLane(connection.To).Curve.P0));
            }

            Assert.That(_network.ConnectionCount, Is.GreaterThan(12));
            Assert.That(_network.NodeCount, Is.EqualTo(5));
            Assert.That(_network.LaneCount, Is.EqualTo(16));
        }

        [Test]
        public void SidewalksConnectAtBothEnds()
        {
            var (_, arms) = Crossroads();
            var connections = new List<LaneConnection>();
            var sidewalk = _network.GetSegment(arms[0]).Lanes[2];

            _network.GetConnections(sidewalk, atSegmentEnd: false, connections);
            Assert.That(connections, Has.Count.EqualTo(6), "beide stoepen van de drie andere armen");

            _network.GetConnections(sidewalk, atSegmentEnd: true, connections);
            Assert.That(connections, Is.Empty, "aan het andere eind loopt hij dood");
        }

        [Test]
        public void RemovingASegmentReconnectsAndDropsLooseNodes()
        {
            var (center, arms) = Crossroads();
            var outer = _network.GetSegment(arms[0]).End;
            var removed = new List<EntityId<SegmentKind>>();
            _network.SegmentChanged += removed.Add;

            _network.RemoveSegment(arms[0]);

            Assert.That(removed, Is.EqualTo(new[] { arms[0] }));
            Assert.That(_network.Contains(arms[0]), Is.False);
            Assert.That(_network.Contains(outer), Is.False, "het losse eindpunt verdwijnt");
            Assert.That(_network.GetNode(center).Segments.Length, Is.EqualTo(3));
            Assert.That(_network.LaneCount, Is.EqualTo(12));

            var connections = new List<LaneConnection>();
            _network.GetConnections(_network.GetSegment(arms[2]).Lanes[1], connections);
            Assert.That(connections.Select(c => c.Turn), Is.EquivalentTo(new[] { Turn.Left, Turn.Right }), "rechtdoor is weg");
        }

        [Test]
        public void ADeadEndLetsCarsTurnAround()
        {
            var a = _network.AddNode(float3.zero);
            var b = _network.AddNode(new float3(30f, 0f, 0f));
            var segment = _network.GetSegment(Road(a, b));
            var connections = new List<LaneConnection>();

            _network.GetConnections(segment.Lanes[0], connections);

            Assert.That(connections.Select(c => (c.To, c.Turn)), Is.EqualTo(new[] { (segment.Lanes[1], Turn.UTurn) }));
        }

        [Test]
        public void InvalidSegmentsAreRejected()
        {
            var a = _network.AddNode(float3.zero);
            var b = _network.AddNode(new float3(30f, 0f, 0f));

            Assert.Throws<ArgumentException>(() => Road(a, a));
            Assert.Throws<ArgumentException>(() => _network.AddSegment(a, b, Bezier3.Straight(float3.zero, new float3(31f, 0f, 0f)), TwoLane, 10f));
            Assert.Throws<ArgumentException>(() => _network.AddSegment(a, b, Bezier3.Straight(float3.zero, new float3(30f, 0f, 0f)), new LaneSpec[0], 10f));
            Assert.Throws<KeyNotFoundException>(() => _network.GetNode(new EntityId<NodeKind>(9, 1)));

            for (var i = 0; i < RoadNetwork.MaxArms; i++)
            {
                Road(a, _network.AddNode(new float3(math.cos(i), 0f, math.sin(i)) * 40f));
            }

            Assert.Throws<InvalidOperationException>(() => Road(a, b), "hoogstens acht armen");
        }

        [Test]
        public void SaveAndLoadKeepIdsAndRebuildConnections()
        {
            var (center, arms) = Crossroads();
            _network.RemoveSegment(arms[3]);
            var connections = _network.ConnectionCount;
            var bytes = SaveBytes.Write(new SaveMetadata(), new RoadNetworkSection(_network));

            using (var loaded = new RoadNetwork(4))
            {
                SaveBytes.Read(bytes, new RoadNetworkSection(loaded));

                Assert.That(loaded.Segments(), Is.EqualTo(_network.Segments()));
                Assert.That(loaded.Nodes(), Is.EqualTo(_network.Nodes()));
                Assert.That(loaded.ConnectionCount, Is.EqualTo(connections));
                Assert.That(loaded.GetSegment(arms[1]).Curve.P3, Is.EqualTo(_network.GetSegment(arms[1]).Curve.P3));
                Assert.That(loaded.AddNode(float3.zero), Is.EqualTo(_network.AddNode(float3.zero)), "de volgende ID is dezelfde");
                Assert.That(loaded.GetNode(center).Segments.Length, Is.EqualTo(3));
            }
        }

        [Test]
        public void BrokenReferencesAreRejected()
        {
            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(SaveBytes.Write(new SaveMetadata(), new DanglingSegment()), new RoadNetworkSection(_network)));
            Assert.That(_network.SegmentCount, Is.EqualTo(0), "niets toegepast");
        }

        [Test]
        public void TheLaneGraphOverlayDrawsLanesArrowsAndConnections()
        {
            Crossroads();
            var geometry = new DebugGeometry();

            new LaneGraphOverlay(_network).Collect(geometry, new DebugView(new float3(0f, 100f, -50f), float3.zero, 500f));

            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Accent), Is.GreaterThan(4 * 2 * 8));
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Warning), Is.GreaterThan(0));
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Info), Is.GreaterThan(0), "stoepen");
        }

        /// <summary>Een net met één knooppunt en een segment dat naar een tweede, niet bestaand knooppunt wijst.</summary>
        private sealed class DanglingSegment : ISaveSection
        {
            public string Id => RoadNetworkSection.SectionId;

            public int Version => 1;

            public void Write(SaveWriter writer)
            {
                var nodes = new IdAllocator<NodeKind>(1, Unity.Collections.Allocator.Temp);
                var segments = new IdAllocator<SegmentKind>(1, Unity.Collections.Allocator.Temp);
                var lanes = new IdAllocator<LaneKind>(1, Unity.Collections.Allocator.Temp);
                try
                {
                    var node = nodes.Allocate();
                    var segment = segments.Allocate();
                    var lane = lanes.Allocate();
                    var laneList = new Unity.Collections.FixedList128Bytes<EntityId<LaneKind>> { lane };
                    var curve = Bezier3.Straight(float3.zero, new float3(30f, 0f, 0f));
                    nodes.WriteTo(writer);
                    segments.WriteTo(writer);
                    lanes.WriteTo(writer);
                    writer.WriteShuffled<RoadNode>(new[] { new RoadNode(float3.zero, new Unity.Collections.FixedList128Bytes<EntityId<SegmentKind>> { segment }) });
                    writer.WriteShuffled<RoadSegment>(new[] { new RoadSegment(node, new EntityId<NodeKind>(1, 1), curve, 10f, laneList) });
                    writer.WriteShuffled<Lane>(new[] { new Lane(segment, TwoLane[0], curve) });
                }
                finally
                {
                    nodes.Dispose();
                    segments.Dispose();
                    lanes.Dispose();
                }
            }

            public Action Read(SaveReader reader, int version) => throw new NotSupportedException();
        }
    }
}
