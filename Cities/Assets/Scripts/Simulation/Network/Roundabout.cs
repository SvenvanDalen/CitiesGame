using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Ids;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>De knooppunten en segmenten van een aangelegde rotonde, in de volgorde van de ring.</summary>
    public readonly struct RoundaboutRing
    {
        public RoundaboutRing(EntityId<NodeKind>[] nodes, EntityId<SegmentKind>[] segments)
        {
            Nodes = nodes;
            Segments = segments;
        }

        /// <summary>Aansluitpunt per arm; de speler bouwt de toegangswegen hier aan.</summary>
        public EntityId<NodeKind>[] Nodes { get; }

        /// <summary>Segment <c>i</c> loopt van <c>Nodes[i]</c> naar <c>Nodes[i + 1]</c>.</summary>
        public EntityId<SegmentKind>[] Segments { get; }
    }

    /// <summary>
    /// Legt een rotonde aan in het <see cref="RoadNetwork"/> (E04-12) en leest er voorrang uit af. De ring is een lus van
    /// eenrichtingssegmenten met één rijstrook; wat een ring is, volgt uit het net zelf en hoeft dus niet apart te worden
    /// opgeslagen. De voorrang zelf (wie wacht waarop) volgt met E04-13: <see cref="GivesWay"/> is daarvoor de data.
    /// </summary>
    public static class Roundabout
    {
        /// <summary>Snelheid op de ring in m/s (30 km/u).</summary>
        public const float RingSpeed = 8.3f;

        /// <summary>Ruimte rond de ring waar nog geen weg mag liggen, voor de rijstrook en stoep.</summary>
        private const float Margin = 6f;

        /// <summary>Waarom hier geen rotonde kan, of <c>null</c>. Alleen open terrein: er mag geen weg of knooppunt in de ring liggen.</summary>
        public static string CanBuild(RoadNetwork network, float3 center, float radius, int arms)
        {
            var reason = RoundaboutGeometry.Validate(radius, arms);
            if (reason != null)
            {
                return reason;
            }

            var limit = radius + Margin;
            var busy = network.Nodes().Any(n => Near(network.GetNode(n).Position, center, limit))
                || network.Segments().Any(s => Enumerable.Range(0, 9).Any(i => Near(network.GetSegment(s).Curve.Position(i / 8f), center, limit)));
            return busy ? "Er ligt al een weg in de rotonde" : null;
        }

        /// <summary>Legt de ring aan met <paramref name="arms"/> aansluitpunten, beginnend bij hoek <paramref name="startAngle"/> (radialen).</summary>
        public static RoundaboutRing Build(RoadNetwork network, float3 center, float radius, int arms, float startAngle = 0f)
        {
            var reason = RoundaboutGeometry.Validate(radius, arms);
            if (reason != null)
            {
                throw new ArgumentException(reason);
            }

            var profile = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true, oneWay: true);
            var nodes = Enumerable.Range(0, arms)
                .Select(i => network.AddNode(RoundaboutGeometry.Point(center, radius, RoundaboutGeometry.Angle(i, arms, startAngle))))
                .ToArray();
            var segments = Enumerable.Range(0, arms)
                .Select(i => network.AddSegment(
                    nodes[i],
                    nodes[(i + 1) % arms],
                    RoundaboutGeometry.Arc(center, radius, RoundaboutGeometry.Angle(i, arms, startAngle), RoundaboutGeometry.Angle(i + 1, arms, startAngle)),
                    profile,
                    RingSpeed))
                .ToArray();
            return new RoundaboutRing(nodes, segments);
        }

        /// <summary><c>true</c> als dit segment deel is van een lus van eenrichtingssegmenten (hoogstens <see cref="RoadNetwork.MaxArms"/> stuks).</summary>
        public static bool IsRingSegment(RoadNetwork network, EntityId<SegmentKind> segment)
        {
            var first = network.GetSegment(segment);
            return IsForwardOneWay(network, first) && Reaches(network, first.End, first.Start, new List<EntityId<SegmentKind>> { segment });
        }

        /// <summary>
        /// <c>true</c> als verkeer dat via <paramref name="arm"/> bij <paramref name="node"/> aankomt voorrang moet geven aan de
        /// ring: het knooppunt hoort bij een ring en de arm zelf niet.
        /// </summary>
        public static bool GivesWay(RoadNetwork network, EntityId<SegmentKind> arm, EntityId<NodeKind> node)
        {
            var road = network.GetSegment(arm);
            return (road.Start == node || road.End == node)
                && ArrivesAt(network, road, node)
                && !IsRingSegment(network, arm)
                && network.GetNode(node).Segments.ToArray().Any(s => IsRingSegment(network, s));
        }

        private static bool Reaches(RoadNetwork network, EntityId<NodeKind> from, EntityId<NodeKind> target, List<EntityId<SegmentKind>> path)
        {
            if (from == target)
            {
                return true;
            }

            return path.Count < RoadNetwork.MaxArms
                && network.GetNode(from).Segments.ToArray()
                    .Where(s => !path.Contains(s) && network.GetSegment(s).Start == from && IsForwardOneWay(network, network.GetSegment(s)))
                    .Any(s => Reaches(network, network.GetSegment(s).End, target, new List<EntityId<SegmentKind>>(path) { s }));
        }

        private static bool IsForwardOneWay(RoadNetwork network, RoadSegment segment) =>
            network.IsOneWay(segment) && segment.Lanes.ToArray().Select(network.GetLane)
                .Where(l => (l.Spec.Types & LaneTypes.Vehicles) != LaneTypes.None)
                .All(l => l.Spec.Direction == LaneDirection.Forward);

        private static bool ArrivesAt(RoadNetwork network, RoadSegment segment, EntityId<NodeKind> node) =>
            segment.Lanes.ToArray().Select(network.GetLane)
                .Where(l => (l.Spec.Types & LaneTypes.Vehicles) != LaneTypes.None)
                .Any(l => l.Spec.Direction == LaneDirection.Both
                    || (l.Spec.Direction == LaneDirection.Forward && segment.End == node)
                    || (l.Spec.Direction == LaneDirection.Backward && segment.Start == node));

        private static bool Near(float3 point, float3 center, float limit) => math.lengthsq(point.xz - center.xz) < limit * limit;
    }
}
