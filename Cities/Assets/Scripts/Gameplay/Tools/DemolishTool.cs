using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Network;
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
        private readonly RoadNetwork _network;
        private readonly RoadPicker _picker;
        private readonly RoadPreviewOverlay _overlay;
        private RoadTarget _hover;
        private RoadTarget _selected;

        public DemolishTool(RoadNetwork network, RoadPreviewOverlay overlay)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _picker = new RoadPicker(_network);
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
            _hover = input.Pick.HasGround ? _picker.Pick(input.Pick.Ground.Point) : default;
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

        private void Demolish(RoadTarget target)
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

        private (float3 Position, int Count, float Length) Describe(RoadTarget target)
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
    }
}
