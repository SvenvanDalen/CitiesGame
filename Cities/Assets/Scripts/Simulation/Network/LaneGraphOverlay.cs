using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Overlay <c>Lanegraaf</c>: de hartlijnen van segmenten, de lanes met een pijl in de rijrichting (rijstroken,
    /// fietspaden en stoepen elk in een eigen kleur) en de verbindingen over de knooppunten. Alleen rond de camera.
    /// </summary>
    public sealed class LaneGraphOverlay : IDebugOverlay
    {
        public const string OverlayName = "Lanegraaf";

        /// <summary>Lijnen komen iets boven de weg, zodat ze niet in het wegdek verdwijnen.</summary>
        public const float Lift = 0.4f;

        private const int Steps = 8;

        private readonly RoadNetwork _network;
        private readonly List<LaneConnection> _connections = new List<LaneConnection>();

        public LaneGraphOverlay(RoadNetwork network) => _network = network ?? throw new ArgumentNullException(nameof(network));

        public string Name => OverlayName;

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (_network.IsDisposed)
            {
                return;
            }

            var focus = view.Focus.xz;
            var radius = view.Radius;
            foreach (var id in _network.Segments())
            {
                var segment = _network.GetSegment(id);
                if (math.distance(segment.Curve.Position(0.5f).xz, focus) > radius + (segment.Length * 0.5f))
                {
                    continue;
                }

                AddCurve(geometry, segment.Curve, DebugColor.Neutral);
                foreach (var laneId in segment.Lanes)
                {
                    AddLane(geometry, _network.GetLane(laneId), laneId);
                }
            }
        }

        private void AddLane(DebugGeometry geometry, Lane lane, Cities.Core.Ids.EntityId<Cities.Core.Ids.LaneKind> id)
        {
            var color = Color(lane.Spec.Types);
            AddCurve(geometry, lane.Curve, color);
            if (lane.Spec.Direction != LaneDirection.Both)
            {
                AddArrow(geometry, lane.Curve, color);
            }

            foreach (var atEnd in lane.Spec.Direction == LaneDirection.Both ? new[] { false, true } : new[] { lane.Spec.Direction == LaneDirection.Forward })
            {
                _network.GetConnections(id, atEnd, _connections);
                foreach (var connection in _connections)
                {
                    AddCurve(geometry, connection.Curve, DebugColor.Warning);
                }
            }
        }

        private static DebugColor Color(LaneTypes types)
        {
            if ((types & LaneTypes.Vehicles) != 0)
            {
                return DebugColor.Accent;
            }

            return (types & LaneTypes.Bike) != 0 ? DebugColor.Good : DebugColor.Info;
        }

        private static void AddCurve(DebugGeometry geometry, Bezier3 curve, DebugColor color)
        {
            var lift = new float3(0f, Lift, 0f);
            var previous = curve.P0 + lift;
            for (var i = 1; i <= Steps; i++)
            {
                var point = curve.Position(i / (float)Steps) + lift;
                geometry.AddLine(previous, point, color);
                previous = point;
            }
        }

        /// <summary>Pijlpunt van 1,5 m op driekwart van de lane.</summary>
        private static void AddArrow(DebugGeometry geometry, Bezier3 curve, DebugColor color)
        {
            var tip = curve.Position(0.75f) + new float3(0f, Lift, 0f);
            var back = curve.Direction(0.75f) * -1.5f;
            var side = Bezier3.Right(curve.Direction(0.75f)) * 0.75f;
            geometry.AddLine(tip, tip + back + side, color);
            geometry.AddLine(tip, tip + back - side, color);
        }
    }
}
