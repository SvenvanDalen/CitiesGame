using System;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Wat de <see cref="RoadJunctionTool"/> doet.</summary>
    public enum RoadJunction
    {
        /// <summary>Zet een knooppunt in het segment onder de muis en deelt het in tweeën.</summary>
        Split,

        /// <summary>Haalt een knooppunt met precies twee gelijke wegen weg en maakt er één weg van.</summary>
        Merge,
    }

    /// <summary>
    /// Plaatst een knooppunt in een weg (splitsen, zodat er daarna op aangesloten kan worden) en haalt een overbodig
    /// knooppunt weer weg (samenvoegen). Eén klik voert het uit.
    /// </summary>
    public sealed class RoadJunctionTool : ITool
    {
        private readonly RoadNetwork _network;
        private readonly RoadPicker _picker;
        private readonly RoadPreviewOverlay _overlay;
        private RoadTarget _target;
        private float _t;

        public RoadJunctionTool(RoadJunction junction, RoadNetwork network, RoadPreviewOverlay overlay)
        {
            Junction = junction;
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _picker = new RoadPicker(_network);
        }

        public RoadJunction Junction { get; }

        public string Name => Junction == RoadJunction.Split ? "Knooppunt plaatsen" : "Knooppunt verwijderen";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        public void Activate() => Reset();

        public void Deactivate() => Reset();

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            var point = input.Pick.HasGround ? input.Pick.Ground.Point : default;
            _target = input.Pick.HasGround ? _picker.Pick(point) : default;
            var preview = Junction == RoadJunction.Split ? PreviewSplit(point) : PreviewMerge();
            if (preview.IsVisible)
            {
                return preview;
            }

            _overlay.Clear();
            return ToolPreview.Hidden;
        }

        public void Apply(in ToolPreview preview)
        {
            if (!preview.IsValid)
            {
                return;
            }

            if (Junction == RoadJunction.Split)
            {
                _network.Split(_target.Segment.Value, _t);
            }
            else
            {
                _network.Merge(_target.Node.Value);
            }

            Reset();
        }

        public bool CancelStep()
        {
            var was = _target.Exists;
            Reset();
            return was;
        }

        private void Reset()
        {
            _target = default;
            _overlay.Clear();
        }

        private ToolPreview PreviewSplit(float3 ground)
        {
            if (!_target.Segment.HasValue)
            {
                return ToolPreview.Hidden;
            }

            var road = _network.GetSegment(_target.Segment.Value);
            _t = RoadPlanner.NearestT(road.Curve, ground);
            var position = road.Curve.Position(_t);
            var reason = road.Length * math.min(_t, 1f - _t) < RoadPlanner.MinLength ? "Te dicht bij het uiteinde" : null;
            _overlay.ShowMarker(position, reason == null);
            return new ToolPreview(position, 0f, float2.zero, reason, $"Knooppunt op {road.Length * _t:0} m van {road.Length:0} m · klik om te plaatsen");
        }

        private ToolPreview PreviewMerge()
        {
            if (!_target.Node.HasValue)
            {
                return ToolPreview.Hidden;
            }

            var node = _network.GetNode(_target.Node.Value);
            var reason = _network.MergeReason(_target.Node.Value);
            _overlay.ShowMarker(node.Position, reason == null);
            return new ToolPreview(node.Position, 0f, float2.zero, reason, "Twee wegen · klik om samen te voegen");
        }
    }
}
