using System;
using System.Collections.Generic;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Cache voor <see cref="RoutePlanner"/> (E23-05): agents met hetzelfde begin en doel delen één route, en een agent die op de
    /// route van een ander naar hetzelfde doel ligt, krijgt het staartstuk daarvan zonder nieuwe berekening. Verandert de graaf
    /// ergens, dan vervallen met <see cref="Invalidate"/> alleen de routes die die knoop gebruiken; routes ouder dan
    /// <see cref="MaxAgeTicks"/> worden herberekend omdat de kosten (drukte) verlopen. Alleen voor de simulatiethread.
    /// </summary>
    public sealed class RouteCache
    {
        private readonly Dictionary<(ulong Start, ulong Goal), Entry> _entries = new Dictionary<(ulong, ulong), Entry>();
        private readonly Dictionary<ulong, List<Entry>> _byGoal = new Dictionary<ulong, List<Entry>>();
        private readonly Dictionary<ulong, HashSet<Entry>> _byNode = new Dictionary<ulong, HashSet<Entry>>();
        private readonly Queue<Entry> _order = new Queue<Entry>();

        public RouteCache(int capacity = 4096, long maxAgeTicks = long.MaxValue)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Minstens één route.");
            }

            if (maxAgeTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAgeTicks), maxAgeTicks, "Minstens één tick.");
            }

            Capacity = capacity;
            MaxAgeTicks = maxAgeTicks;
        }

        public int Capacity { get; }

        public long MaxAgeTicks { get; }

        public int Count => _entries.Count;

        /// <summary>Aantal keer dat een route uit de cache kwam (ook als staartstuk).</summary>
        public long Hits { get; private set; }

        /// <summary>Aantal keer dat <see cref="RoutePlanner"/> moest rekenen.</summary>
        public long Misses { get; private set; }

        public RouteResult Find(IRouteGraph graph, ulong start, ulong goal, long tick = 0)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (_entries.TryGetValue((start, goal), out var exact) && exact.IsFresh(tick, MaxAgeTicks))
            {
                Hits++;
                return exact.Result;
            }

            if (_byGoal.TryGetValue(goal, out var sharing))
            {
                foreach (var entry in sharing)
                {
                    var index = entry.IndexOf(start);
                    if (index > 0 && entry.IsFresh(tick, MaxAgeTicks))
                    {
                        Hits++;
                        return entry.Tail(index);
                    }
                }
            }

            Misses++;
            var result = RoutePlanner.Find(graph, start, goal);
            if (result.Found)
            {
                Store(graph, start, goal, result, tick);
            }

            return result;
        }

        /// <summary>Gooit alle routes weg die <paramref name="node"/> gebruiken; geeft het aantal terug.</summary>
        public int Invalidate(ulong node)
        {
            if (!_byNode.TryGetValue(node, out var users))
            {
                return 0;
            }

            var victims = new List<Entry>(users);
            foreach (var entry in victims)
            {
                Remove(entry);
            }

            return victims.Count;
        }

        public void Clear()
        {
            _entries.Clear();
            _byGoal.Clear();
            _byNode.Clear();
            _order.Clear();
        }

        private void Store(IRouteGraph graph, ulong start, ulong goal, RouteResult result, long tick)
        {
            while (_entries.Count >= Capacity && _order.Count > 0)
            {
                var oldest = _order.Dequeue();
                if (_entries.TryGetValue((oldest.Start, oldest.Goal), out var live) && ReferenceEquals(live, oldest))
                {
                    Remove(oldest);
                }
            }

            if (_entries.TryGetValue((start, goal), out var stale))
            {
                Remove(stale);
            }

            var entry = new Entry(start, goal, result, tick, Cumulative(graph, result));
            _entries[(start, goal)] = entry;
            _order.Enqueue(entry);
            if (!_byGoal.TryGetValue(goal, out var list))
            {
                _byGoal[goal] = list = new List<Entry>();
            }

            list.Add(entry);
            foreach (var node in result.Path)
            {
                if (!_byNode.TryGetValue(node, out var users))
                {
                    _byNode[node] = users = new HashSet<Entry>();
                }

                users.Add(entry);
            }
        }

        private void Remove(Entry entry)
        {
            _entries.Remove((entry.Start, entry.Goal));
            if (_byGoal.TryGetValue(entry.Goal, out var list))
            {
                list.Remove(entry);
                if (list.Count == 0)
                {
                    _byGoal.Remove(entry.Goal);
                }
            }

            foreach (var node in entry.Result.Path)
            {
                if (_byNode.TryGetValue(node, out var users) && users.Remove(entry) && users.Count == 0)
                {
                    _byNode.Remove(node);
                }
            }
        }

        // Kosten vanaf het begin tot elke knoop langs het pad, zodat een staartstuk zijn eigen kosten krijgt.
        private static float[] Cumulative(IRouteGraph graph, RouteResult result)
        {
            var total = new float[result.Path.Count];
            var steps = new List<RouteStep>();
            var sum = 0f;
            for (var i = 1; i < result.Path.Count; i++)
            {
                steps.Clear();
                graph.Expand(result.Path[i - 1], steps);
                var best = 0f;
                var found = false;
                foreach (var step in steps)
                {
                    if (step.To == result.Path[i] && (!found || step.Cost < best))
                    {
                        best = step.Cost;
                        found = true;
                    }
                }

                sum += best;
                total[i] = sum;
            }

            return total;
        }

        private sealed class Entry
        {
            private readonly long _tick;
            private readonly float[] _cumulative;

            public Entry(ulong start, ulong goal, RouteResult result, long tick, float[] cumulative)
            {
                Start = start;
                Goal = goal;
                Result = result;
                _tick = tick;
                _cumulative = cumulative;
            }

            public ulong Start { get; }

            public ulong Goal { get; }

            public RouteResult Result { get; }

            public bool IsFresh(long tick, long maxAge) => tick - _tick < maxAge;

            public int IndexOf(ulong node)
            {
                for (var i = 0; i < Result.Path.Count; i++)
                {
                    if (Result.Path[i] == node)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public RouteResult Tail(int index)
            {
                var path = new ulong[Result.Path.Count - index];
                for (var i = 0; i < path.Length; i++)
                {
                    path[i] = Result.Path[index + i];
                }

                return new RouteResult(path, Result.Cost - _cumulative[index]);
            }
        }
    }
}
