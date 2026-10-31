using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Cities.World.Roads;
using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Een knooppunt of segment van het wegennet dat een tool aanwijst.</summary>
    public readonly struct RoadTarget : IEquatable<RoadTarget>
    {
        public RoadTarget(EntityId<NodeKind>? node, EntityId<SegmentKind>? segment)
        {
            Node = node;
            Segment = segment;
        }

        public EntityId<NodeKind>? Node { get; }

        public EntityId<SegmentKind>? Segment { get; }

        public bool Exists => Node.HasValue || Segment.HasValue;

        public bool Equals(RoadTarget other) => Node == other.Node && Segment == other.Segment;

        public override bool Equals(object obj) => obj is RoadTarget other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Node, Segment);
    }

    /// <summary>Zoekt het knooppunt of segment onder een punt op de grond; gedeeld door de sloop- en inspectietool.</summary>
    public sealed class RoadPicker
    {
        public const float NodeRadius = 6f;
        public const float SegmentSlack = 3f;

        private readonly RoadNetwork _network;

        public RoadPicker(RoadNetwork network) => _network = network ?? throw new ArgumentNullException(nameof(network));

        /// <summary>Een knooppunt dichtbij gaat voor; anders het segment waar het punt op of vlak naast ligt.</summary>
        public RoadTarget Pick(float3 point)
        {
            EntityId<NodeKind>? node = null;
            var best = NodeRadius;
            foreach (var id in _network.Nodes())
            {
                var distance = math.distance(_network.GetNode(id).Position.xz, point.xz);
                if (distance < best)
                {
                    node = id;
                    best = distance;
                }
            }

            if (node.HasValue)
            {
                return new RoadTarget(node, null);
            }

            EntityId<SegmentKind>? segment = null;
            best = float.MaxValue;
            foreach (var id in _network.Segments())
            {
                var road = _network.GetSegment(id);
                var distance = RoadPlanner.DistanceTo(road.Curve, point) - HalfWidth(_network, road);
                if (distance < SegmentSlack && distance < best)
                {
                    segment = id;
                    best = distance;
                }
            }

            return new RoadTarget(null, segment);
        }

        /// <summary>Halve breedte van het wegdek van een segment, uit zijn lanes.</summary>
        public static float HalfWidth(RoadNetwork network, RoadSegment road)
        {
            var specs = new List<LaneSpec>();
            foreach (var lane in road.Lanes)
            {
                specs.Add(network.GetLane(lane).Spec);
            }

            return RoadTerrainShaper.HalfWidth(specs);
        }
    }
}
