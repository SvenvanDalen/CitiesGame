using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>Welk knooppunt tussen snelweg en kruisende weg (E04-11).</summary>
    public enum InterchangeKind
    {
        /// <summary>Op- en afritten naar een kruisende weg (ruitvorm): vier korte ramps, de kruisende weg ligt over de snelweg.</summary>
        Diamond,

        /// <summary>Klaverblad: vier lussen voor linksaf en vier boogramps voor rechtsaf.</summary>
        Cloverleaf,

        /// <summary>Turbine (vereenvoudigd): vier ruime linksaframps en vier boogramps voor rechtsaf.</summary>
        Turbine,
    }

    /// <summary>De rol van een weg in het knooppunt; bepaalt het dwarsprofiel.</summary>
    public enum InterchangeRole
    {
        Highway,
        Crossing,
        Ramp,
    }

    /// <summary>Een voorbeeldpreset voor de werkbalk: soort, naam en korte uitleg.</summary>
    public readonly struct InterchangePreset
    {
        public InterchangePreset(InterchangeKind kind, string name, string description)
        {
            Kind = kind;
            Name = name;
            Description = description;
        }

        public InterchangeKind Kind { get; }

        public string Name { get; }

        public string Description { get; }
    }

    /// <summary>Een geplande weg van het knooppunt, met indices in <see cref="InterchangePlan.Nodes"/>.</summary>
    public readonly struct PlannedRoad
    {
        public PlannedRoad(InterchangeRole role, int start, int end, Bezier3 curve, RoadElevation elevation)
        {
            Role = role;
            Start = start;
            End = end;
            Curve = curve;
            Elevation = elevation;
        }

        public InterchangeRole Role { get; }

        public int Start { get; }

        public int End { get; }

        public Bezier3 Curve { get; }

        public RoadElevation Elevation { get; }
    }

    /// <summary>Alle knooppunten en wegen van een knooppunt in wereldcoördinaten, nog niet in het net gezet.</summary>
    public sealed class InterchangePlan
    {
        public InterchangePlan(IReadOnlyList<float3> nodes, IReadOnlyList<PlannedRoad> roads, int west, int east, int south, int north)
        {
            Nodes = nodes;
            Roads = roads;
            West = west;
            East = east;
            South = south;
            North = north;
        }

        public IReadOnlyList<float3> Nodes { get; }

        public IReadOnlyList<PlannedRoad> Roads { get; }

        /// <summary>De vier uiteinden van de snelweg (west, oost) en de kruisende weg (zuid, noord) vóór draaiing: hier sluit de speler aan.</summary>
        public int West { get; }

        public int East { get; }

        public int South { get; }

        public int North { get; }
    }

    /// <summary>
    /// Vorm van snelwegknooppunten (E04-11). De snelweg loopt langs de lokale X-as, de kruisende weg langs Z en gaat over de snelweg heen op
    /// <see cref="DeckHeight"/>. Alle ramps raken de assen rakend aan, zodat ze netjes aansluiten. De snelweg is één tweerichtingssegment: de
    /// ramps vertrekken vanaf de hartlijn, dus de gescheiden rijbanen zijn een vereenvoudiging.
    /// </summary>
    public static class InterchangeGeometry
    {
        /// <summary>Afstand (m) van het midden tot de uiteinden van beide wegen.</summary>
        public const float HalfLength = 260f;

        /// <summary>Straal (m) waarbinnen niets anders mag liggen.</summary>
        public const float Footprint = 280f;

        /// <summary>Hoogte (m) van het brugdek van de kruisende weg boven de snelweg.</summary>
        public const float DeckHeight = 5.5f;

        /// <summary>Het brugdek loopt tot deze afstand (m) van het midden; daarna loopt de weg omlaag.</summary>
        public const float BridgeHalfSpan = 28f;

        /// <summary>Op deze afstand (m) van het midden ligt de kruisende weg weer op de grond (helling ca. 6%).</summary>
        public const float ApproachLength = 120f;

        private const float Tolerance = 0.5f;
        private const float DiamondRampX = 150f;
        private const float SlipRadius = 60f;
        private const float LoopRadius = 75f;
        private const float TurbineRadius = 150f;
        private const float DiamondHandle = 60f;

        public static IReadOnlyList<InterchangePreset> Presets { get; } = new[]
        {
            new InterchangePreset(InterchangeKind.Diamond, "Op- en afritten", "Vier korte ramps naar een kruisende weg"),
            new InterchangePreset(InterchangeKind.Cloverleaf, "Klaverblad", "Vier lussen voor linksaf, vier boogramps voor rechtsaf"),
            new InterchangePreset(InterchangeKind.Turbine, "Turbine", "Vier ruime linksaframps, vier boogramps voor rechtsaf"),
        };

        /// <summary>Hoogte (m) van de kruisende weg op afstand <paramref name="distance"/> van het midden.</summary>
        public static float CrossingHeight(float distance)
        {
            var d = math.abs(distance);
            if (d >= ApproachLength)
            {
                return 0f;
            }

            return d <= BridgeHalfSpan ? DeckHeight : DeckHeight * (ApproachLength - d) / (ApproachLength - BridgeHalfSpan);
        }

        /// <summary>Het plan voor <paramref name="kind"/> rond <paramref name="center"/>, gedraaid met <paramref name="angle"/> (radialen) om de verticale as.</summary>
        public static InterchangePlan Plan(InterchangeKind kind, float3 center, float angle)
        {
            var builder = new Builder();
            switch (kind)
            {
                case InterchangeKind.Diamond:
                    builder.Diamond();
                    break;
                case InterchangeKind.Cloverleaf:
                    builder.Cloverleaf();
                    break;
                case InterchangeKind.Turbine:
                    builder.Turbine();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekend soort knooppunt.");
            }

            return builder.Finish(center, angle);
        }

        private sealed class Builder
        {
            private readonly List<float3> _nodes = new List<float3>();
            private readonly List<PlannedRoad> _roads = new List<PlannedRoad>();

            /// <summary>Cirkelboog als kubische Bezier; een negatieve <paramref name="to"/> - <paramref name="from"/> loopt met de klok mee. Nauwkeurig tot ca. 135 graden.</summary>
            private static Bezier3 Arc(float2 center, float radius, float from, float to)
            {
                var handle = 4f / 3f * math.tan((to - from) / 4f) * radius;
                var start = center + (radius * new float2(math.cos(from), math.sin(from)));
                var end = center + (radius * new float2(math.cos(to), math.sin(to)));
                var startTangent = new float2(-math.sin(from), math.cos(from));
                var endTangent = new float2(-math.sin(to), math.cos(to));
                return Flat(start, start + (startTangent * handle), end - (endTangent * handle), end);
            }

            private static Bezier3 Flat(float2 p0, float2 p1, float2 p2, float2 p3) =>
                new Bezier3(new float3(p0.x, 0f, p0.y), new float3(p1.x, 0f, p1.y), new float3(p2.x, 0f, p2.y), new float3(p3.x, 0f, p3.y));

            private static float2 Rotate(float2 p, int quarterTurns)
            {
                for (var i = 0; i < quarterTurns; i++)
                {
                    p = p.yx * new float2(-1f, 1f);
                }

                return p;
            }

            /// <summary>Op de kruisende weg volgt de hoogte uit het profiel, op de snelweg is die nul; tussenpunten van lussen liggen halverwege.</summary>
            private static float Height(float2 p)
            {
                if (math.abs(p.x) < Tolerance)
                {
                    return CrossingHeight(p.y);
                }

                return math.abs(p.y) < Tolerance ? 0f : CrossingHeight(LoopRadius) * 0.5f;
            }

            public void Diamond()
            {
                var off = Flat(new float2(-DiamondRampX, 0f), new float2(-DiamondRampX + DiamondHandle, 0f), new float2(-DiamondHandle, -ApproachLength), new float2(0f, -ApproachLength));
                var on = Flat(new float2(0f, -ApproachLength), new float2(DiamondHandle, -ApproachLength), new float2(DiamondRampX - DiamondHandle, 0f), new float2(DiamondRampX, 0f));
                foreach (var turns in new[] { 0, 2 })
                {
                    Ramp(off, turns);
                    Ramp(on, turns);
                }
            }

            public void Cloverleaf()
            {
                for (var turns = 0; turns < 4; turns++)
                {
                    Ramp(Slip(), turns);

                    // Lus: van de kruisende weg met de klok mee 270 graden naar de snelweg, in twee helften van 135 graden.
                    var center = new float2(-LoopRadius, -LoopRadius);
                    Ramp(Arc(center, LoopRadius, 0f, -3f * math.PI / 4f), turns);
                    Ramp(Arc(center, LoopRadius, -3f * math.PI / 4f, -3f * math.PI / 2f), turns);
                }
            }

            public void Turbine()
            {
                for (var turns = 0; turns < 4; turns++)
                {
                    Ramp(Slip(), turns);
                    Ramp(Arc(new float2(-TurbineRadius, TurbineRadius), TurbineRadius, -math.PI / 2f, 0f), turns);
                }
            }

            public InterchangePlan Finish(float3 center, float angle)
            {
                var west = Node(new float2(-HalfLength, 0f));
                var east = Node(new float2(HalfLength, 0f));
                var south = Node(new float2(0f, -HalfLength));
                var north = Node(new float2(0f, HalfLength));
                Node(new float2(0f, -BridgeHalfSpan));
                Node(new float2(0f, BridgeHalfSpan));

                var highway = Enumerable.Range(0, _nodes.Count).Where(i => math.abs(_nodes[i].z) < Tolerance).OrderBy(i => _nodes[i].x).ToList();
                var crossing = Enumerable.Range(0, _nodes.Count).Where(i => math.abs(_nodes[i].x) < Tolerance).OrderBy(i => _nodes[i].z).ToList();
                Axis(highway, InterchangeRole.Highway);
                Axis(crossing, InterchangeRole.Crossing);

                var rotation = quaternion.AxisAngle(math.up(), angle);
                float3 ToWorld(float3 p) => center + math.rotate(rotation, p);
                var roads = _roads
                    .Select(r => new PlannedRoad(r.Role, r.Start, r.End, new Bezier3(ToWorld(r.Curve.P0), ToWorld(r.Curve.P1), ToWorld(r.Curve.P2), ToWorld(r.Curve.P3)), r.Elevation))
                    .ToList();
                return new InterchangePlan(_nodes.Select(ToWorld).ToList(), roads, west, east, south, north);
            }

            /// <summary>Boogramp voor rechtsaf in het zuidwestelijke kwadrant: van de snelweg (west) naar de kruisende weg (zuid).</summary>
            private static Bezier3 Slip() => Arc(new float2(-SlipRadius, -SlipRadius), SlipRadius, math.PI / 2f, 0f);

            private void Axis(List<int> ordered, InterchangeRole role)
            {
                for (var i = 0; i + 1 < ordered.Count; i++)
                {
                    var from = _nodes[ordered[i]];
                    var to = _nodes[ordered[i + 1]];
                    var bridge = role == InterchangeRole.Crossing && math.abs(from.z) <= BridgeHalfSpan + Tolerance && math.abs(to.z) <= BridgeHalfSpan + Tolerance;
                    _roads.Add(new PlannedRoad(role, ordered[i], ordered[i + 1], Bezier3.Straight(from, to), bridge ? RoadElevation.Bridge : RoadElevation.Ground));
                }
            }

            /// <summary>Zet een ramp (in het zuidwestelijke prototype getekend) <paramref name="quarterTurns"/> kwartslagen gedraaid neer; de uiteinden worden knooppunten.</summary>
            private void Ramp(Bezier3 prototype, int quarterTurns)
            {
                var p0 = Rotate(prototype.P0.xz, quarterTurns);
                var p3 = Rotate(prototype.P3.xz, quarterTurns);
                var start = Node(p0);
                var end = Node(p3);
                var from = _nodes[start];
                var to = _nodes[end];
                var p1 = Rotate(prototype.P1.xz, quarterTurns);
                var p2 = Rotate(prototype.P2.xz, quarterTurns);
                var curve = new Bezier3(from, new float3(p1.x, from.y, p1.y), new float3(p2.x, to.y, p2.y), to);
                _roads.Add(new PlannedRoad(InterchangeRole.Ramp, start, end, curve, RoadElevation.Ground));
            }

            private int Node(float2 p)
            {
                for (var i = 0; i < _nodes.Count; i++)
                {
                    if (math.distancesq(_nodes[i].xz, p) < Tolerance * Tolerance)
                    {
                        return i;
                    }
                }

                _nodes.Add(new float3(p.x, Height(p), p.y));
                return _nodes.Count - 1;
            }
        }
    }
}
