using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Cities.World.Roads;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Wat er gesloopt is: het aantal segmenten en hun totale lengte langs de hartlijn.</summary>
    public readonly struct Demolition
    {
        public Demolition(int segments, float length)
        {
            Segments = segments;
            Length = length;
        }

        public int Segments { get; }

        public float Length { get; }
    }

    /// <summary>
    /// Sloopt wegen: een klik kiest een segment of knooppunt (met alle wegen erop), een tweede klik op hetzelfde bevestigt.
    /// Het voorbeeld is rood en de uitleg zegt wat er weggaat. De terugbetaling hoort bij de economie (E11): die
    /// luistert op <see cref="Demolished"/> en rekent met de gesloopte lengte.
    /// </summary>
    public sealed class DemolishTool : ITool
    {
        public const float NodeRadius = 6f;
        public const float SegmentSlack = 3f;

        private readonly RoadNetwork _network;
        private readonly RoadPreviewOverlay _overlay;
        private Target _hover;
        private Target _selected;

        public DemolishTool(RoadNetwork network, RoadPreviewOverlay overlay)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        public string Name => "Slopen";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        /// <summary>Na elke sloop.</summary>
        public event Action<Demolition> Demolished;

        /// <summary><c>true</c> als er iets gekozen is en de volgende klik erop de sloop bevestigt.</summary>
        public bool IsSelected => _selected.Exists;

        public void Activate() => Reset();

        public void Deactivate() => Reset();

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            _hover = input.Pick.HasGround ? Pick(input.Pick.Ground.Point) : default;
            if (!_hover.Exists)
            {
                _overlay.Clear();
                return ToolPreview.Hidden;
            }

            var (position, count, length) = Describe(_hover);
            if (_hover.Segment.HasValue)
            {
                _overlay.ShowCurve(_network.GetSegment(_hover.Segment.Value).Curve, false);
            }
            else
            {
                _overlay.ShowMarker(position, false);
            }

            var what = _hover.Segment.HasValue ? $"Weg · {length:0} m" : $"Kruispunt · {count} wegen, {length:0} m";
            var next = _hover.Equals(_selected) ? "klik nogmaals om te slopen" : "klik om te kiezen";
            return new ToolPreview(position, 0f, float2.zero, null, $"{what} · {next}");
        }

        public void Apply(in ToolPreview preview)
        {
            if (!_hover.Exists)
            {
                return;
            }

            if (!_hover.Equals(_selected))
            {
                _selected = _hover;
                return;
            }

            var target = _selected;
            Reset();
            Demolish(target);
        }

        public bool CancelStep()
        {
            var was = _selected.Exists;
            Reset();
            return was;
        }

        private void Reset()
        {
            _hover = default;
            _selected = default;
            _overlay.Clear();
        }

        private void Demolish(Target target)
        {
            var segments = new List<EntityId<SegmentKind>>();
            if (target.Segment.HasValue)
            {
                segments.Add(target.Segment.Value);
            }
            else
            {
                foreach (var id in _network.GetNode(target.Node.Value).Segments)
                {
                    segments.Add(id);
                }
            }

            var length = 0f;
            foreach (var id in segments)
            {
                length += _network.GetSegment(id).Length;
                _network.RemoveSegment(id);
            }

            Demolished?.Invoke(new Demolition(segments.Count, length));
        }

        /// <summary>Een knooppunt dichtbij gaat voor; anders het segment waar de muis op of vlak naast ligt.</summary>
        private Target Pick(float3 point)
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
                return new Target(node, null);
            }

            EntityId<SegmentKind>? segment = null;
            best = float.MaxValue;
            foreach (var id in _network.Segments())
            {
                var road = _network.GetSegment(id);
                var distance = RoadPlanner.DistanceTo(road.Curve, point) - HalfWidth(road);
                if (distance < SegmentSlack && distance < best)
                {
                    segment = id;
                    best = distance;
                }
            }

            return new Target(null, segment);
        }

        private float HalfWidth(RoadSegment road)
        {
            var specs = new List<LaneSpec>();
            foreach (var lane in road.Lanes)
            {
                specs.Add(_network.GetLane(lane).Spec);
            }

            return RoadTerrainShaper.HalfWidth(specs);
        }

        private (float3 Position, int Count, float Length) Describe(Target target)
        {
            if (target.Segment.HasValue)
            {
                var road = _network.GetSegment(target.Segment.Value);
                return (road.Curve.Position(0.5f), 1, road.Length);
            }

            var node = _network.GetNode(target.Node.Value);
            var length = 0f;
            foreach (var id in node.Segments)
            {
                length += _network.GetSegment(id).Length;
            }

            return (node.Position, node.Segments.Length, length);
        }

        private readonly struct Target : IEquatable<Target>
        {
            public Target(EntityId<NodeKind>? node, EntityId<SegmentKind>? segment)
            {
                Node = node;
                Segment = segment;
            }

            public EntityId<NodeKind>? Node { get; }

            public EntityId<SegmentKind>? Segment { get; }

            public bool Exists => Node.HasValue || Segment.HasValue;

            public bool Equals(Target other) => Node == other.Node && Segment == other.Segment;

            public override bool Equals(object obj) => obj is Target other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Node, Segment);
        }
    }
}
