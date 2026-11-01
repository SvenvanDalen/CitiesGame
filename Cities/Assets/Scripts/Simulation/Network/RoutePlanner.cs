using System;
using System.Collections.Generic;

namespace Cities.Simulation.Network
{
    /// <summary>Een stap in een routegraaf: naar welke knoop en wat dat kost (seconden).</summary>
    public readonly struct RouteStep
    {
        public RouteStep(ulong to, float cost)
        {
            To = to;
            Cost = cost;
        }

        public ulong To { get; }

        public float Cost { get; }
    }

    /// <summary>Abstracte graaf voor <see cref="RoutePlanner"/>; knopen zijn opaque sleutels.</summary>
    public interface IRouteGraph
    {
        /// <summary>Voegt de uitgaande stappen van <paramref name="node"/> toe aan <paramref name="steps"/>.</summary>
        void Expand(ulong node, List<RouteStep> steps);

        /// <summary>Ondergrens van de kosten van <paramref name="from"/> naar <paramref name="goal"/>; 0 is altijd veilig.</summary>
        float Estimate(ulong from, ulong goal);
    }

    /// <summary>Resultaat van een routeberekening; <see cref="Path"/> is leeg als er geen route is.</summary>
    public readonly struct RouteResult
    {
        public RouteResult(IReadOnlyList<ulong> path, float cost)
        {
            Path = path;
            Cost = cost;
        }

        public IReadOnlyList<ulong> Path { get; }

        public float Cost { get; }

        public bool Found => Path.Count > 0;
    }

    /// <summary>
    /// A* op een abstracte graaf (E08-01). Deterministisch: bij gelijke kosten wint de laagste sleutel. De wachtrij is een
    /// <see cref="SortedSet{T}"/> omdat Unity geen <c>PriorityQueue</c> heeft.
    /// </summary>
    public static class RoutePlanner
    {
        // shortcut: managed A* zonder Burst of contraction hierarchies; upgraden zodra routes per tick de budgetten raken.
        public static RouteResult Find(IRouteGraph graph, ulong start, ulong goal)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var cost = new Dictionary<ulong, float> { [start] = 0f };
            var cameFrom = new Dictionary<ulong, ulong>();
            var closed = new HashSet<ulong>();
            var open = new SortedSet<(float F, ulong Node)> { (graph.Estimate(start, goal), start) };
            var steps = new List<RouteStep>();

            while (open.Count > 0)
            {
                var node = open.Min.Node;
                open.Remove(open.Min);
                if (node == goal)
                {
                    return new RouteResult(Rebuild(cameFrom, start, goal), cost[goal]);
                }

                if (!closed.Add(node))
                {
                    continue;
                }

                steps.Clear();
                graph.Expand(node, steps);
                foreach (var step in steps)
                {
                    var next = cost[node] + step.Cost;
                    if (closed.Contains(step.To) || (cost.TryGetValue(step.To, out var known) && known <= next))
                    {
                        continue;
                    }

                    cost[step.To] = next;
                    cameFrom[step.To] = node;
                    open.Add((next + graph.Estimate(step.To, goal), step.To));
                }
            }

            return new RouteResult(Array.Empty<ulong>(), 0f);
        }

        private static List<ulong> Rebuild(Dictionary<ulong, ulong> cameFrom, ulong start, ulong goal)
        {
            var path = new List<ulong> { goal };
            for (var node = goal; node != start;)
            {
                node = cameFrom[node];
                path.Add(node);
            }

            path.Reverse();
            return path;
        }
    }
}
