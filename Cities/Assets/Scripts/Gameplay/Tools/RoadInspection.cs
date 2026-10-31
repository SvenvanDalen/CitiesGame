using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Inspectiebron voor het wegennet: een kruispunt of segment onder het punt, met lanes en snelheid.</summary>
    public sealed class RoadInspection : IInspectionSource
    {
        private readonly RoadNetwork _network;
        private readonly RoadPicker _picker;

        public RoadInspection(RoadNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _picker = new RoadPicker(network);
        }

        public bool TryInspect(float3 point, out InspectionReport report)
        {
            var target = _picker.Pick(point);
            if (target.Segment.HasValue)
            {
                var road = _network.GetSegment(target.Segment.Value);
                report = new InspectionReport(
                    road.Curve.Position(0.5f),
                    "Weg",
                    $"{road.Length:0} m",
                    $"max {road.SpeedLimit * 3.6f:0} km/u",
                    Lanes(road));
                return true;
            }

            if (target.Node.HasValue)
            {
                var node = _network.GetNode(target.Node.Value);
                report = new InspectionReport(node.Position, "Kruispunt", $"{node.Segments.Length} wegen", $"{Connections(target.Node.Value, node)} verbindingen");
                return true;
            }

            report = default;
            return false;
        }

        private string Lanes(RoadSegment road)
        {
            int vehicles = 0, bikes = 0, pedestrians = 0;
            foreach (var id in road.Lanes)
            {
                var types = _network.GetLane(id).Spec.Types;
                vehicles += (types & LaneTypes.Vehicles) != 0 ? 1 : 0;
                bikes += (types & LaneTypes.Bike) != 0 ? 1 : 0;
                pedestrians += (types & LaneTypes.Pedestrian) != 0 ? 1 : 0;
            }

            return $"{vehicles} rijbanen, {bikes} fietspaden, {pedestrians} voetpaden";
        }

        /// <summary>Het aantal lane-verbindingen dat in dit knooppunt samenkomt.</summary>
        private int Connections(EntityId<NodeKind> id, RoadNode node)
        {
            var results = new List<LaneConnection>();
            var count = 0;
            foreach (var segmentId in node.Segments)
            {
                var segment = _network.GetSegment(segmentId);
                foreach (var laneId in segment.Lanes)
                {
                    results.Clear();
                    _network.GetConnections(laneId, results);
                    foreach (var connection in results)
                    {
                        count += connection.Node == id ? 1 : 0;
                    }
                }
            }

            return count;
        }
    }
}
