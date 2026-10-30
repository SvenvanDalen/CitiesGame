using Cities.Core.Ids;
using Unity.Collections;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>Knooppunt: een kruispunt, splitsing of doodlopend eind, met de segmenten die er samenkomen.</summary>
    public readonly struct RoadNode
    {
        public RoadNode(float3 position, FixedList128Bytes<EntityId<SegmentKind>> segments)
        {
            Position = position;
            Segments = segments;
        }

        public float3 Position { get; }

        /// <summary>Hoogstens <see cref="RoadNetwork.MaxArms"/> segmenten.</summary>
        public FixedList128Bytes<EntityId<SegmentKind>> Segments { get; }
    }

    /// <summary>Wegstuk tussen twee knooppunten, met zijn vorm en lanes.</summary>
    public readonly struct RoadSegment
    {
        public RoadSegment(EntityId<NodeKind> start, EntityId<NodeKind> end, Bezier3 curve, float speedLimit, FixedList128Bytes<EntityId<LaneKind>> lanes)
        {
            Start = start;
            End = end;
            Curve = curve;
            Length = curve.Length();
            SpeedLimit = speedLimit;
            Lanes = lanes;
        }

        public EntityId<NodeKind> Start { get; }

        public EntityId<NodeKind> End { get; }

        /// <summary>De hartlijn, van <see cref="Start"/> naar <see cref="End"/>.</summary>
        public Bezier3 Curve { get; }

        public float Length { get; }

        /// <summary>Maximumsnelheid in m/s.</summary>
        public float SpeedLimit { get; }

        /// <summary>Hoogstens <see cref="RoadNetwork.MaxLanes"/> lanes, in de volgorde van het profiel.</summary>
        public FixedList128Bytes<EntityId<LaneKind>> Lanes { get; }

        public EntityId<NodeKind> Other(EntityId<NodeKind> node) => node == Start ? End : Start;
    }

    /// <summary>Een rijstrook, fietspad of stoep; <see cref="Curve"/> loopt in de rijrichting (een stoep in die van het segment).</summary>
    public readonly struct Lane
    {
        public Lane(EntityId<SegmentKind> segment, LaneSpec spec, Bezier3 curve)
        {
            Segment = segment;
            Spec = spec;
            Curve = curve;
            Length = curve.Length();
        }

        public EntityId<SegmentKind> Segment { get; }

        public LaneSpec Spec { get; }

        public Bezier3 Curve { get; }

        public float Length { get; }
    }

    /// <summary>Overgang van het eind van een lane naar het begin van een andere, over een knooppunt.</summary>
    public readonly struct LaneConnection
    {
        public LaneConnection(EntityId<LaneKind> from, EntityId<LaneKind> to, EntityId<NodeKind> node, Turn turn, Bezier3 curve)
        {
            From = from;
            To = to;
            Node = node;
            Turn = turn;
            Curve = curve;
        }

        public EntityId<LaneKind> From { get; }

        public EntityId<LaneKind> To { get; }

        public EntityId<NodeKind> Node { get; }

        public Turn Turn { get; }

        /// <summary>Het pad over het knooppunt, van het eind van <see cref="From"/> naar het begin van <see cref="To"/>.</summary>
        public Bezier3 Curve { get; }
    }
}
