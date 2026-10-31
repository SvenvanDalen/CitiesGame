using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Cities.World.Grid;
using Cities.World.Roads;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Wat de <see cref="RoadEditTool"/> met het aangewezen segment doet.</summary>
    public enum RoadEdit
    {
        /// <summary>Geeft het segment het wegtype dat de <see cref="RoadTool"/> nu gekozen heeft.</summary>
        Upgrade,

        /// <summary>Keert de rijrichting van een eenrichtingsweg om.</summary>
        Reverse,
    }

    /// <summary>
    /// Past bestaande wegen aan zonder ze opnieuw te leggen: het wegtype wisselen (de aansluitingen worden opnieuw
    /// verbonden, het terrein wordt zo nodig verbreed) of een eenrichtingsweg omkeren. Eén klik op een segment voert het uit.
    /// </summary>
    public sealed class RoadEditTool : ITool
    {
        private readonly RoadNetwork _network;
        private readonly WorldMap _map;
        private readonly RoadTool _roads;
        private readonly RoadPicker _picker;
        private readonly RoadPreviewOverlay _overlay;
        private EntityId<SegmentKind>? _hover;

        public RoadEditTool(RoadEdit edit, RoadNetwork network, WorldMap map, RoadTool roads, RoadPreviewOverlay overlay)
        {
            Edit = edit;
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _picker = new RoadPicker(_network);
        }

        public RoadEdit Edit { get; }

        public string Name => Edit == RoadEdit.Upgrade ? "Upgraden" : "Omkeren";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        public void Activate() => Reset();

        public void Deactivate() => Reset();

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            var target = input.Pick.HasGround ? _picker.Pick(input.Pick.Ground.Point) : default;
            _hover = target.Segment;
            if (!_hover.HasValue)
            {
                _overlay.Clear();
                return ToolPreview.Hidden;
            }

            var road = _network.GetSegment(_hover.Value);
            var reason = Reason(road);
            _overlay.ShowCurve(road.Curve, reason == null);
            var cost = Edit == RoadEdit.Upgrade ? (long)math.round(road.Length * _roads.CostPerMeter) : 0L;
            return new ToolPreview(road.Curve.Position(0.5f), 0f, float2.zero, reason, Info(road), cost);
        }

        public void Apply(in ToolPreview preview)
        {
            if (!_hover.HasValue || Reason(_network.GetSegment(_hover.Value)) != null)
            {
                return;
            }

            var id = _hover.Value;
            if (Edit == RoadEdit.Reverse)
            {
                _network.Reverse(id);
                return;
            }

            _network.Retype(id, _roads.Profile, _roads.SpeedLimit);
            RoadTerrainShaper.Apply(_map, _network.GetSegment(id).Curve, RoadTerrainShaper.HalfWidth(_roads.Profile));
        }

        public bool CancelStep()
        {
            var was = _hover.HasValue;
            Reset();
            return was;
        }

        private void Reset()
        {
            _hover = null;
            _overlay.Clear();
        }

        private string Reason(RoadSegment road)
        {
            if (Edit == RoadEdit.Reverse)
            {
                return _network.IsOneWay(road) ? null : "Alleen een eenrichtingsweg kan omgekeerd worden";
            }

            if (_roads.TypeId == null)
            {
                return "Kies eerst een wegtype";
            }

            return SameAsChosen(road) ? "Al dit wegtype" : null;
        }

        private string Info(RoadSegment road) =>
            Edit == RoadEdit.Reverse ? $"Weg · {road.Length:0} m · klik om de richting om te keren" : $"Weg · {road.Length:0} m · klik om te upgraden naar {_roads.TypeId}";

        /// <summary>Of het segment al precies de lanes en snelheid van het gekozen wegtype heeft.</summary>
        private bool SameAsChosen(RoadSegment road)
        {
            var wanted = _roads.Profile;
            if (road.Lanes.Length != wanted.Count || Math.Abs(road.SpeedLimit - _roads.SpeedLimit) > 1e-4f)
            {
                return false;
            }

            var current = new List<LaneSpec>(road.Lanes.Length);
            foreach (var lane in road.Lanes)
            {
                current.Add(_network.GetLane(lane).Spec);
            }

            for (var i = 0; i < current.Count; i++)
            {
                if (current[i] != wanted[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
