using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Kortste route over de lanegraaf (E08-01). Kosten zijn reistijd in seconden: afstand gedeeld door de maximumsnelheid van het
    /// segment, maal een congestiefactor per lane (1 = vrij, 2 = half zo snel). Alleen voertuiglanes worden gebruikt.
    /// </summary>
    public sealed class LanePathfinder : IRouteGraph
    {
        /// <summary>Snelste snelheid (m/s, 50 m/s = 180 km/u) voor de schatting; sneller dan dit maakt A* niet meer optimaal.</summary>
        // shortcut: vaste bovengrens, upgraden als er wegtypes boven 180 km/u komen.
        public const float MaxSpeed = 50f;

        private readonly RoadNetwork _network;
        private readonly Func<EntityId<LaneKind>, float> _congestion;
        private readonly List<LaneConnection> _connections = new List<LaneConnection>();

        /// <param name="congestion">Factor ≥ 1 per lane; <c>null</c> betekent overal vrij verkeer.</param>
        public LanePathfinder(RoadNetwork network, Func<EntityId<LaneKind>, float> congestion = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _congestion = congestion;
        }

        /// <summary>Lanes van begin tot eind (beide inbegrepen) en de totale reistijd; leeg als er geen route is.</summary>
        public (IReadOnlyList<EntityId<LaneKind>> Lanes, float Seconds) FindRoute(EntityId<LaneKind> from, EntityId<LaneKind> to)
        {
            var result = RoutePlanner.Find(this, from.Packed, to.Packed);
            var lanes = new List<EntityId<LaneKind>>(result.Path.Count);
            foreach (var key in result.Path)
            {
                lanes.Add(EntityId<LaneKind>.FromPacked(key));
            }

            return (lanes, result.Cost);
        }

        void IRouteGraph.Expand(ulong node, List<RouteStep> steps)
        {
            var lane = EntityId<LaneKind>.FromPacked(node);
            _connections.Clear();
            _network.GetConnections(lane, _connections);
            foreach (var connection in _connections)
            {
                var target = _network.GetLane(connection.To);
                if ((target.Spec.Types & LaneTypes.Vehicles) == LaneTypes.None)
                {
                    continue;
                }

                var speed = Math.Max(_network.GetSegment(target.Segment).SpeedLimit, 0.1f);
                var factor = _congestion is null ? 1f : Math.Max(_congestion(connection.To), 1f);
                steps.Add(new RouteStep(connection.To.Packed, (connection.Curve.Length() + target.Length * factor) / speed));
            }
        }

        float IRouteGraph.Estimate(ulong from, ulong goal)
        {
            var a = _network.GetLane(EntityId<LaneKind>.FromPacked(from)).Curve.P3;
            var b = _network.GetLane(EntityId<LaneKind>.FromPacked(goal)).Curve.P3;
            return math.distance(a, b) / MaxSpeed;
        }
    }
}
