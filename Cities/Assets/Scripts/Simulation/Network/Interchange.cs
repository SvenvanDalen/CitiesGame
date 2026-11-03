using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Ids;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>De aangelegde knooppunten en segmenten van een snelwegknooppunt; de speler sluit wegen aan op de vier uiteinden.</summary>
    public sealed class InterchangeResult
    {
        public InterchangeResult(EntityId<NodeKind>[] nodes, EntityId<SegmentKind>[] segments, InterchangePlan plan)
        {
            Nodes = nodes;
            Segments = segments;
            West = nodes[plan.West];
            East = nodes[plan.East];
            South = nodes[plan.South];
            North = nodes[plan.North];
        }

        public EntityId<NodeKind>[] Nodes { get; }

        public EntityId<SegmentKind>[] Segments { get; }

        public EntityId<NodeKind> West { get; }

        public EntityId<NodeKind> East { get; }

        public EntityId<NodeKind> South { get; }

        public EntityId<NodeKind> North { get; }
    }

    /// <summary>
    /// Legt een snelwegknooppunt aan in het <see cref="RoadNetwork"/> (E04-11) volgens <see cref="InterchangeGeometry"/>. Er is geen eigen
    /// opslag nodig: alles is gewone knooppunten en segmenten en gaat mee in sectie <c>NETW</c>.
    /// </summary>
    public static class Interchange
    {
        /// <summary>Snelheid in m/s per rol (120, 50 en 60 km/u).</summary>
        public static float SpeedLimit(InterchangeRole role) => role switch
        {
            InterchangeRole.Highway => 120f / 3.6f,
            InterchangeRole.Crossing => 50f / 3.6f,
            _ => 60f / 3.6f,
        };

        public static IReadOnlyList<LaneSpec> Profile(InterchangeRole role) => role switch
        {
            InterchangeRole.Highway => LaneProfiles.Road(3, bikeLanes: false, sidewalks: false),
            InterchangeRole.Crossing => LaneProfiles.Road(1, bikeLanes: false, sidewalks: true),
            _ => LaneProfiles.Road(1, bikeLanes: false, sidewalks: false, oneWay: true),
        };

        /// <summary>Waarom hier geen knooppunt kan, of <c>null</c>. Alleen open terrein: binnen <see cref="InterchangeGeometry.Footprint"/> mag niets liggen.</summary>
        public static string CanBuild(RoadNetwork network, float3 center)
        {
            var limit = InterchangeGeometry.Footprint;
            var busy = network.Nodes().Any(n => Near(network.GetNode(n).Position, center, limit))
                || network.Segments().Any(s => Enumerable.Range(0, 9).Any(i => Near(network.GetSegment(s).Curve.Position(i / 8f), center, limit)));
            return busy ? "Er ligt al een weg in het knooppunt" : null;
        }

        public static InterchangeResult Build(RoadNetwork network, InterchangeKind kind, float3 center, float angle = 0f)
        {
            if (network is null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            var plan = InterchangeGeometry.Plan(kind, center, angle);
            var nodes = plan.Nodes.Select(network.AddNode).ToArray();
            var segments = plan.Roads
                .Select(r => network.AddSegment(nodes[r.Start], nodes[r.End], r.Curve, Profile(r.Role), SpeedLimit(r.Role), r.Elevation))
                .ToArray();
            return new InterchangeResult(nodes, segments, plan);
        }

        private static bool Near(float3 point, float3 center, float limit) => math.lengthsq(point.xz - center.xz) < limit * limit;
    }
}
