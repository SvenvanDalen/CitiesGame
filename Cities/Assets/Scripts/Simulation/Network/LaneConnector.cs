using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>Soort afslag bij een knooppunt, gezien vanuit de bestuurder.</summary>
    public enum Turn : byte
    {
        Straight,
        Left,
        Right,
        UTurn,
    }

    /// <summary>Een lane van een arm, met een nummer dat de aanroeper zelf kiest.</summary>
    public readonly struct ArmLane
    {
        public ArmLane(int id, LaneSpec spec)
        {
            Id = id;
            Spec = spec;
        }

        public int Id { get; }

        public LaneSpec Spec { get; }
    }

    /// <summary>Een segment zoals het bij een knooppunt aankomt.</summary>
    public readonly struct NodeArm
    {
        /// <param name="heading">Horizontale richting vanaf het knooppunt het segment in (x, z).</param>
        /// <param name="nodeIsEnd"><c>true</c> als het knooppunt het eindpunt van het segment is.</param>
        public NodeArm(float2 heading, bool nodeIsEnd, IReadOnlyList<ArmLane> lanes)
        {
            Heading = math.normalizesafe(heading);
            NodeIsEnd = nodeIsEnd;
            Lanes = lanes ?? throw new ArgumentNullException(nameof(lanes));
        }

        public float2 Heading { get; }

        public bool NodeIsEnd { get; }

        public IReadOnlyList<ArmLane> Lanes { get; }

        /// <summary>Komt het verkeer op deze lane het knooppunt in?</summary>
        public bool Enters(LaneSpec spec) => spec.Direction == LaneDirection.Both || (spec.Direction == LaneDirection.Forward) == NodeIsEnd;

        /// <summary>Gaat het verkeer op deze lane het knooppunt uit?</summary>
        public bool Leaves(LaneSpec spec) => spec.Direction == LaneDirection.Both || (spec.Direction == LaneDirection.Forward) != NodeIsEnd;
    }

    /// <summary>Een toegestane overgang van de ene lane naar de andere bij een knooppunt.</summary>
    public readonly struct LanePair : IEquatable<LanePair>
    {
        public LanePair(int from, int to, Turn turn)
        {
            From = from;
            To = to;
            Turn = turn;
        }

        public int From { get; }

        public int To { get; }

        public Turn Turn { get; }

        public bool Equals(LanePair other) => From == other.From && To == other.To && Turn == other.Turn;

        public override bool Equals(object obj) => obj is LanePair other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(From, To, Turn);

        public override string ToString() => $"{From} → {To} ({Turn})";
    }

    /// <summary>
    /// Bepaalt welke lanes bij een knooppunt op elkaar aansluiten, voor rechtsrijdend verkeer:
    /// <list type="bullet">
    /// <item>Rechtsaf gaat van de rechterstrook naar de rechterstrook, linksaf van de linker- naar de linkerstrook.</item>
    /// <item>Rechtdoor sluiten de stroken van rechts af paarsgewijs aan; overtollige stroken vallen samen met de buitenste.</item>
    /// <item>Keren alleen op een doodlopend eind, van de linkerstrook naar de linkerstrook.</item>
    /// <item>Rijstroken, fietspaden en stoepen sluiten alleen op hun eigen soort aan; een stoep op elke stoep van een andere arm.</item>
    /// </list>
    /// </summary>
    public static class LaneConnector
    {
        /// <summary>Tot deze hoek (graden) is een afslag rechtdoor.</summary>
        public const float StraightAngle = 30f;

        private static readonly LaneTypes[] Groups = { LaneTypes.Vehicles, LaneTypes.Bike, LaneTypes.Pedestrian };

        public static List<LanePair> Connect(IReadOnlyList<NodeArm> arms)
        {
            if (arms == null)
            {
                throw new ArgumentNullException(nameof(arms));
            }

            var pairs = new List<LanePair>();
            foreach (var group in Groups)
            {
                for (var from = 0; from < arms.Count; from++)
                {
                    for (var to = 0; to < arms.Count; to++)
                    {
                        Connect(arms, from, to, group, pairs);
                    }
                }
            }

            return pairs.Distinct().ToList();
        }

        /// <summary>Afslag van een bestuurder die over <paramref name="from"/> binnenkomt en over <paramref name="to"/> vertrekt.</summary>
        public static Turn Classify(NodeArm from, NodeArm to, bool sameArm)
        {
            if (sameArm)
            {
                return Turn.UTurn;
            }

            var travelIn = -from.Heading;
            var travelOut = to.Heading;
            var cross = (travelIn.x * travelOut.y) - (travelIn.y * travelOut.x);
            var angle = math.degrees(math.atan2(cross, math.dot(travelIn, travelOut)));
            if (math.abs(angle) <= StraightAngle)
            {
                return Turn.Straight;
            }

            // In x-z van bovenaf (z vooruit, x rechts) is een draai naar rechts negatief.
            return angle < 0f ? Turn.Right : Turn.Left;
        }

        private static void Connect(IReadOnlyList<NodeArm> arms, int from, int to, LaneTypes group, List<LanePair> pairs)
        {
            var deadEnd = arms.Count == 1;
            if (from == to && !deadEnd)
            {
                return;
            }

            var incoming = Ordered(arms[from], group, entering: true);
            var outgoing = Ordered(arms[to], group, entering: false);
            if (incoming.Count == 0 || outgoing.Count == 0)
            {
                return;
            }

            var turn = Classify(arms[from], arms[to], from == to);
            if (group == LaneTypes.Pedestrian)
            {
                // Voetgangers lopen alle kanten op: elke stoep naar elke stoep van een andere arm.
                if (from == to)
                {
                    return;
                }

                foreach (var a in incoming)
                {
                    pairs.AddRange(outgoing.Select(b => new LanePair(a.Id, b.Id, turn)));
                }

                return;
            }

            switch (turn)
            {
                case Turn.Right:
                    pairs.Add(new LanePair(incoming[0].Id, outgoing[0].Id, turn));
                    break;
                case Turn.Left:
                case Turn.UTurn:
                    pairs.Add(new LanePair(incoming[^1].Id, outgoing[^1].Id, turn));
                    break;
                default:
                    for (var i = 0; i < math.max(incoming.Count, outgoing.Count); i++)
                    {
                        pairs.Add(new LanePair(incoming[math.min(i, incoming.Count - 1)].Id, outgoing[math.min(i, outgoing.Count - 1)].Id, turn));
                    }

                    break;
            }
        }

        /// <summary>Lanes van de groep die binnenkomen of vertrekken, van rechts naar links gezien vanuit de bestuurder.</summary>
        private static List<ArmLane> Ordered(NodeArm arm, LaneTypes group, bool entering) =>
            arm.Lanes
                .Where(lane => (lane.Spec.Types & group) != 0 && (entering ? arm.Enters(lane.Spec) : arm.Leaves(lane.Spec)))
                .OrderByDescending(lane => Side(arm, lane.Spec, entering))
                .ToList();

        /// <summary>Hoe ver rechts de lane ligt voor wie erover rijdt, het knooppunt in of uit.</summary>
        private static float Side(NodeArm arm, LaneSpec spec, bool entering)
        {
            // Wie het knooppunt binnenrijdt, rijdt in de richting van het segment als het knooppunt het eind is.
            var alongSegment = entering == arm.NodeIsEnd;
            return alongSegment ? spec.Offset : -spec.Offset;
        }
    }
}
