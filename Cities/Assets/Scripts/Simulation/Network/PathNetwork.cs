using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>Hoe iemand zich over het padennet verplaatst.</summary>
    public enum PathMode : byte
    {
        Walk = 0,
        Bike = 1,
    }

    /// <summary>Wie een boog van het padennet mag gebruiken.</summary>
    [Flags]
    public enum PathModes : byte
    {
        None = 0,
        Walk = 1,
        Bike = 2,
        Both = Walk | Bike,
    }

    /// <summary>Uitkomst van een route over het padennet. Zonder route is <see cref="Found"/> onwaar en zijn de lijsten leeg.</summary>
    public readonly struct PathRoute
    {
        public PathRoute(bool found, IReadOnlyList<int> nodes, float length, float seconds)
        {
            Found = found;
            Nodes = nodes;
            Length = length;
            Seconds = seconds;
        }

        public bool Found { get; }

        /// <summary>De knopen van begin tot eind, beide meegeteld.</summary>
        public IReadOnlyList<int> Nodes { get; }

        /// <summary>Afgelegde afstand in meters (over het oppervlak, dus met hoogteverschil).</summary>
        public float Length { get; }

        public float Seconds { get; }
    }

    /// <summary>
    /// Het fiets- en voetpadnetwerk (E04-15): losse paden en de fietsstroken en trottoirs van wegen in één eigen graaf, met eigen routering.
    /// Voetgangers gebruiken trottoirs en paden in beide richtingen; fietsers gebruiken fietsstroken in hun rijrichting en fietspaden.
    /// De reistijd volgt uit de snelheid van de vorm van verplaatsing, vertraagd bergop.
    /// </summary>
    public sealed class PathNetwork
    {
        public const float WalkSpeed = 1.4f;
        public const float BikeSpeed = 4.5f;

        /// <summary>Hoeveel langzamer het op een helling van 100% gaat, per eenheid helling bergop; begrensd door <see cref="MaxSlopeFactor"/>.</summary>
        public const float UphillPenalty = 5f;

        public const float MaxSlopeFactor = 3f;

        private struct Arc
        {
            public int To;
            public float Length;
            public float Rise;
            public PathModes Modes;
        }

        private readonly List<float3> _positions = new List<float3>();
        private readonly List<List<Arc>> _arcs = new List<List<Arc>>();

        public int NodeCount => _positions.Count;

        public int AddNode(float3 position)
        {
            _positions.Add(position);
            _arcs.Add(new List<Arc>());
            return _positions.Count - 1;
        }

        /// <summary>Een los pad (voet- of fietspad) tussen twee knopen, in beide richtingen.</summary>
        public void AddPath(int a, int b, PathModes modes)
        {
            Check(a);
            Check(b);
            if (modes == PathModes.None)
            {
                throw new ArgumentException("Een pad is voor minstens één soort verplaatsing.", nameof(modes));
            }

            var length = math.distance(_positions[a], _positions[b]);
            AddArc(a, b, length, modes);
            AddArc(b, a, length, modes);
        }

        /// <summary>
        /// Voegt de fietsstroken en trottoirs van een wegstuk van <paramref name="a"/> naar <paramref name="b"/> toe. Een lane met richting
        /// <see cref="LaneDirection.Forward"/> loopt van a naar b, <see cref="LaneDirection.Backward"/> van b naar a. Autostroken doen niet mee.
        /// </summary>
        public void AddRoadSegment(int a, int b, IReadOnlyList<LaneSpec> lanes)
        {
            if (lanes is null)
            {
                throw new ArgumentNullException(nameof(lanes));
            }

            Check(a);
            Check(b);
            var length = math.distance(_positions[a], _positions[b]);
            foreach (var lane in lanes)
            {
                var modes = (lane.Types.HasFlag(LaneTypes.Pedestrian) ? PathModes.Walk : PathModes.None) | (lane.Types.HasFlag(LaneTypes.Bike) ? PathModes.Bike : PathModes.None);
                if (modes == PathModes.None)
                {
                    continue;
                }

                if (lane.Direction != LaneDirection.Backward)
                {
                    AddArc(a, b, length, modes);
                }

                if (lane.Direction != LaneDirection.Forward)
                {
                    AddArc(b, a, length, modes);
                }
            }
        }

        /// <summary>Kortste route in reistijd van <paramref name="from"/> naar <paramref name="to"/> voor <paramref name="mode"/>.</summary>
        // shortcut: Dijkstra met een SortedSet is O(n log n) per route, prima voor een wijk; bij routes over de hele stad A* met een heuristiek.
        public PathRoute Route(int from, int to, PathMode mode)
        {
            Check(from);
            Check(to);
            var allowed = mode == PathMode.Walk ? PathModes.Walk : PathModes.Bike;
            var best = new float[_positions.Count];
            var previous = new int[_positions.Count];
            for (var i = 0; i < best.Length; i++)
            {
                best[i] = float.PositiveInfinity;
                previous[i] = -1;
            }

            best[from] = 0f;
            var queue = new SortedSet<(float Seconds, int Node)> { (0f, from) };
            while (queue.Count > 0)
            {
                var (seconds, node) = queue.Min;
                queue.Remove(queue.Min);
                if (node == to)
                {
                    break;
                }

                foreach (var arc in _arcs[node])
                {
                    if ((arc.Modes & allowed) == PathModes.None)
                    {
                        continue;
                    }

                    var next = seconds + Seconds(arc, mode);
                    if (next < best[arc.To])
                    {
                        queue.Remove((best[arc.To], arc.To));
                        best[arc.To] = next;
                        previous[arc.To] = node;
                        queue.Add((next, arc.To));
                    }
                }
            }

            if (float.IsPositiveInfinity(best[to]))
            {
                return new PathRoute(false, Array.Empty<int>(), 0f, 0f);
            }

            var nodes = Trace(previous, to);
            return new PathRoute(true, nodes, LengthOf(nodes, allowed), best[to]);
        }

        private static List<int> Trace(int[] previous, int to)
        {
            var nodes = new List<int>();
            for (var n = to; n != -1; n = previous[n])
            {
                nodes.Add(n);
            }

            nodes.Reverse();
            return nodes;
        }

        private float LengthOf(List<int> nodes, PathModes allowed)
        {
            var length = 0f;
            for (var i = 1; i < nodes.Count; i++)
            {
                var next = nodes[i];
                length += _arcs[nodes[i - 1]].Find(arc => arc.To == next && (arc.Modes & allowed) != PathModes.None).Length;
            }

            return length;
        }

        private static float Seconds(Arc arc, PathMode mode)
        {
            var grade = arc.Length > 0f ? arc.Rise / arc.Length : 0f;
            var slowdown = math.min(1f + (UphillPenalty * math.max(0f, grade)), MaxSlopeFactor);
            return arc.Length / (mode == PathMode.Walk ? WalkSpeed : BikeSpeed) * slowdown;
        }

        private void AddArc(int from, int to, float length, PathModes modes)
        {
            _arcs[from].Add(new Arc { To = to, Length = length, Rise = _positions[to].y - _positions[from].y, Modes = modes });
        }

        private void Check(int node)
        {
            if (node < 0 || node >= _positions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(node), node, "Onbekende knoop.");
            }
        }
    }
}
