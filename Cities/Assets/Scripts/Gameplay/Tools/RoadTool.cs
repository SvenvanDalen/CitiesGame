using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Commands;
using Cities.Simulation.Network;
using Cities.World.Grid;
using Cities.World.Roads;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Legt wegen aan in het <see cref="RoadNetwork"/>: recht, met een buigpunt of als aaneengesloten lijn (zie
    /// <see cref="RoadDrawMode"/>). Het begin en eind snappen aan bestaande knooppunten; anders aan een hoek- en lengtestap
    /// (de hoek ten opzichte van de bestaande weg waar je aan begint). Een nieuwe weg vlakt het terrein eronder af (<see cref="RoadTerrainShaper"/>). Midden op een bestaand segment aansluiten kan ook: het segment wordt dan bij de eerste klik in tweeën gesplitst.
    /// </summary>
    public sealed class RoadTool : ITool, IUndoSource
    {
        public const float SnapRadius = 10f;
        public const float DefaultSpeedLimit = 13.9f;

        private enum Stage
        {
            Idle,
            Started,
            Bent,
        }

        private readonly RoadNetwork _network;
        private readonly WorldMap _map;
        private readonly RoadPreviewOverlay _overlay;
        private Stage _stage;
        private float3 _start;
        private EntityId<NodeKind>? _startNode;
        private float3 _control;
        private float _referenceYaw;
        private Plan _plan;

        public RoadTool(RoadNetwork network, WorldMap map, RoadPreviewOverlay overlay)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        public string Name => "Weg";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        public RoadDrawMode Mode { get; set; }

        /// <summary>Hoekstap in graden voor het eind van de weg; 0 zet het snappen uit.</summary>
        public float AngleStep { get; set; } = 15f;

        /// <summary>Lengtestap in meters; 0 zet het snappen uit.</summary>
        public float LengthStep { get; set; } = 5f;

        /// <summary>Rasterstap in meters voor het begin-, buig- en eindpunt; 0 zet het uit. Met raster aan vervangt het de hoek- en lengtestap voor het eindpunt.</summary>
        public float GridStep { get; set; }

        /// <summary>De lanes van nieuwe wegen; <see cref="Use"/> zet ze volgens een wegtype.</summary>
        public IReadOnlyList<LaneSpec> Profile { get; set; } = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);

        public float SpeedLimit { get; set; } = DefaultSpeedLimit;

        /// <summary>Het gekozen wegtype (<c>road.…</c>), of <c>null</c> zolang er geen is gekozen.</summary>
        public string TypeId { get; private set; }

        /// <summary>Aanlegkosten per meter van het gekozen type.</summary>
        public long CostPerMeter { get; private set; }

        /// <summary>Legt voortaan wegen van dit type aan.</summary>
        public void Use(string id, RoadTypeSpec type)
        {
            TypeId = id;
            CostPerMeter = type.CostPerMeter;
            Profile = type.Profile();
            SpeedLimit = type.SpeedLimitMetersPerSecond;
        }

        /// <summary><c>true</c> als er al een beginpunt is en de volgende klik de weg verder legt.</summary>
        public bool IsDrawing => _stage != Stage.Idle;

        public void Activate() => Reset();

        public void Deactivate() => Reset();

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            if (!input.Pick.HasGround)
            {
                _overlay.Clear();
                return ToolPreview.Hidden;
            }

            var ground = input.Pick.Ground.Point;
            var node = FindNode(ground);
            EntityId<SegmentKind>? onSegment = null;
            var t = 0f;
            float3 point;
            if (node.HasValue)
            {
                point = _network.GetNode(node.Value).Position;
            }
            else if (FindSegment(ground, out var segment, out t))
            {
                onSegment = segment;
                point = _network.GetSegment(segment).Curve.Position(t);
            }
            else
            {
                point = RoadPlanner.SnapToGrid(ground, GridStep);
            }

            _plan = new Plan(point, node, default, onSegment, t);
            if (_stage == Stage.Idle)
            {
                var idleReason = OutsideReason(ground) ?? (node.HasValue ? Cap(node.Value) : null);
                return Marker(point, node, idleReason, onSegment.HasValue);
            }

            if (_stage == Stage.Started && Mode == RoadDrawMode.Curve)
            {
                return Guide(point, ground);
            }

            var end = node.HasValue || onSegment.HasValue || GridStep > 0f ? point : RoadPlanner.Snap(_start, point, AngleStep, LengthStep, _referenceYaw);
            var curve = _stage == Stage.Bent ? RoadPlanner.Quadratic(_start, _control, end) : Bezier3.Straight(_start, end);
            _plan = new Plan(end, node, curve, onSegment, t);
            return Segment(curve, node, ground);
        }

        public void Apply(in ToolPreview preview)
        {
            if (_stage == Stage.Idle)
            {
                Begin(_plan.End, _plan.Node ?? SplitNode(_plan));
            }
            else if (_stage == Stage.Started && Mode == RoadDrawMode.Curve)
            {
                _control = _plan.End;
                _stage = Stage.Bent;
            }
            else
            {
                Build();
            }
        }

        public bool CancelStep()
        {
            var was = IsDrawing;
            Reset();
            return was;
        }

        private UndoAction _lastAction;

        public UndoAction TakeUndo()
        {
            var action = _lastAction;
            _lastAction = null;
            return action;
        }

        /// <summary>Ongedaan maken haalt het segment weg (en knooppunten die daarna leeg zijn); opnieuw leest het terug. Het afgevlakte terrein blijft.</summary>
        private UndoAction MakeUndo(EntityId<SegmentKind> segment, EntityId<NodeKind> startNode, EntityId<NodeKind> endNode, Bezier3 curve, IReadOnlyList<LaneSpec> profile, float speed)
        {
            var current = segment;
            var startPosition = _network.GetNode(startNode).Position;
            var endPosition = _network.GetNode(endNode).Position;
            return new UndoAction(
                "Weg aangelegd",
                0,
                () =>
                {
                    Reset();
                    if (_network.Contains(current))
                    {
                        _network.RemoveSegment(current);
                    }
                },
                () =>
                {
                    Reset();
                    var a = _network.Contains(startNode) ? startNode : _network.AddNode(startPosition);
                    var b = _network.Contains(endNode) ? endNode : _network.AddNode(endPosition);
                    current = _network.AddSegment(a, b, curve, profile, speed);
                });
        }

        private void Reset()
        {
            _stage = Stage.Idle;
            _startNode = null;
            _plan = default;
            _overlay.Clear();
        }

        private void Begin(float3 position, EntityId<NodeKind>? node)
        {
            _start = position;
            _startNode = node;
            _stage = Stage.Started;
            _referenceYaw = node.HasValue ? ContinuationYaw(node.Value) : 0f;
        }

        private void Build()
        {
            var startNode = _startNode ?? _network.AddNode(_start);
            var split = _plan.Segment.HasValue && !_plan.Node.HasValue;
            var endNode = _plan.Node ?? SplitNode(_plan) ?? _network.AddNode(_plan.End);
            var segment = _network.AddSegment(startNode, endNode, _plan.Curve, Profile, SpeedLimit);
            _lastAction = split ? null : MakeUndo(segment, startNode, endNode, _plan.Curve, Profile, SpeedLimit);
            RoadTerrainShaper.Apply(_map, _plan.Curve, RoadTerrainShaper.HalfWidth(Profile));
            if (Mode == RoadDrawMode.Free)
            {
                var heading = RoadPlanner.Yaw(_plan.Curve.Direction(1f));
                Begin(_plan.End, endNode);
                _referenceYaw = heading;
            }
            else
            {
                Reset();
            }
        }

        private ToolPreview Marker(float3 point, EntityId<NodeKind>? node, string reason, bool onSegment)
        {
            _overlay.ShowMarker(point, reason == null);
            return new ToolPreview(point, 0f, float2.zero, reason, node.HasValue ? "Bestaand kruispunt · klik om hier te beginnen" : onSegment ? "Aansluiten op bestaande weg · klik om hier te beginnen" : "Klik om de weg te beginnen");
        }

        /// <summary>De hulplijn van het begin naar het buigpunt.</summary>
        private ToolPreview Guide(float3 point, float3 ground)
        {
            var curve = Bezier3.Straight(_start, point);
            var reason = RoadPlanner.Check(curve) ?? OutsideReason(ground);
            _overlay.ShowCurve(curve, reason == null);
            return new ToolPreview(point, 0f, float2.zero, reason, $"Buigpunt · {curve.Length():0.0} m · klik om het buigpunt te zetten");
        }

        private ToolPreview Segment(Bezier3 curve, EntityId<NodeKind>? node, float3 ground)
        {
            var reason = RoadPlanner.Check(curve) ?? OutsideReason(curve.P3) ?? OutsideReason(ground) ?? EndReason(node);
            _overlay.ShowCurve(curve, reason == null);
            return new ToolPreview(curve.P3, 0f, float2.zero, reason, RoadPlanner.Describe(curve) + " · klik om te bouwen, rechtsklik om te stoppen", (long)math.round(curve.Length() * CostPerMeter));
        }

        /// <summary>Splitst het segment waar het plan op uitkomt en geeft het nieuwe knooppunt, of <c>null</c> als het plan niet op een segment ligt.</summary>
        private EntityId<NodeKind>? SplitNode(Plan plan) =>
            plan.Segment.HasValue && _network.Contains(plan.Segment.Value) ? _network.Split(plan.Segment.Value, plan.T) : (EntityId<NodeKind>?)null;

        /// <summary>Het dichtstbijzijnde segment binnen <see cref="SnapRadius"/> waar nog een kruispunt past (niet te dicht bij een uiteinde).</summary>
        private bool FindSegment(float3 point, out EntityId<SegmentKind> best, out float bestT)
        {
            best = default;
            bestT = 0f;
            var found = false;
            var bestDistance = SnapRadius;
            foreach (var id in _network.Segments())
            {
                var road = _network.GetSegment(id);
                var distance = RoadPlanner.DistanceTo(road.Curve, point);
                if (distance >= bestDistance)
                {
                    continue;
                }

                var t = RoadPlanner.NearestT(road.Curve, point);
                if (road.Length * math.min(t, 1f - t) < RoadPlanner.MinLength)
                {
                    continue;
                }

                best = id;
                bestT = t;
                bestDistance = distance;
                found = true;
            }

            return found;
        }

        private string EndReason(EntityId<NodeKind>? node)
        {
            if (!node.HasValue)
            {
                return null;
            }

            return node == _startNode ? "Begin en eind vallen samen" : Cap(node.Value);
        }

        private string Cap(EntityId<NodeKind> node) =>
            _network.GetNode(node).Segments.Length >= RoadNetwork.MaxArms ? "Te veel wegen bij dit kruispunt" : null;

        private string OutsideReason(float3 point)
        {
            var size = new float2(_map.Dimensions.SizeX, _map.Dimensions.SizeZ);
            return math.any(point.xz < 0f) || math.any(point.xz > size) ? "Buiten de kaart" : null;
        }

        private EntityId<NodeKind>? FindNode(float3 point)
        {
            EntityId<NodeKind>? best = null;
            var bestDistance = SnapRadius;
            foreach (var id in _network.Nodes())
            {
                var distance = math.distance(_network.GetNode(id).Position.xz, point.xz);
                if (distance < bestDistance)
                {
                    best = id;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Koers van een weg die rechtdoor gaat vanaf <paramref name="node"/>, ten opzichte van de eerste weg daar.</summary>
        private float ContinuationYaw(EntityId<NodeKind> node)
        {
            var segments = _network.GetNode(node).Segments;
            if (segments.Length == 0)
            {
                return 0f;
            }

            var segment = _network.GetSegment(segments[0]);
            var away = segment.End == node ? -segment.Curve.Direction(1f) : segment.Curve.Direction(0f);
            return RoadPlanner.Yaw(-away);
        }

        private readonly struct Plan
        {
            public Plan(float3 end, EntityId<NodeKind>? node, Bezier3 curve, EntityId<SegmentKind>? segment, float t)
            {
                End = end;
                Node = node;
                Curve = curve;
                Segment = segment;
                T = t;
            }

            public EntityId<SegmentKind>? Segment { get; }

            public float T { get; }

            public float3 End { get; }

            public EntityId<NodeKind>? Node { get; }

            public Bezier3 Curve { get; }
        }
    }
}
