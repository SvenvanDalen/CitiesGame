using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Scheduling
{
    /// <summary>
    /// Bepaalt een vaste uitvoervolgorde voor knopen met "na"-afhankelijkheden (topologische sortering, Kahn).
    /// Bij keuzevrijheid wint de volgorde van toevoegen, zodat de uitkomst bij dezelfde registraties altijd gelijk is.
    /// </summary>
    public sealed class SystemGraph<T> where T : class
    {
        private readonly List<T> _nodes = new List<T>();
        private readonly Dictionary<T, int> _indexOf = new Dictionary<T, int>();
        private readonly List<List<T>> _after = new List<List<T>>();
        private readonly Func<T, string> _describe;

        /// <param name="describe">Naam van een knoop in foutmeldingen; standaard <c>ToString()</c>.</param>
        public SystemGraph(Func<T, string> describe = null)
        {
            _describe = describe ?? (node => node.ToString());
        }

        public int Count => _nodes.Count;

        /// <summary>Knopen in volgorde van toevoegen.</summary>
        public IReadOnlyList<T> Nodes => _nodes;

        /// <summary>Voegt een knoop toe die pas na <paramref name="after"/> mag draaien.</summary>
        public void Add(T node, IEnumerable<T> after)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (_indexOf.ContainsKey(node))
            {
                throw new InvalidOperationException($"{_describe(node)} is al toegevoegd.");
            }

            var predecessors = after?.ToList() ?? new List<T>();
            if (predecessors.Contains(null))
            {
                throw new ArgumentException("Afhankelijkheden mogen niet null zijn.", nameof(after));
            }

            if (predecessors.Contains(node))
            {
                throw new ArgumentException($"{_describe(node)} kan niet van zichzelf afhangen.", nameof(after));
            }

            _indexOf.Add(node, _nodes.Count);
            _nodes.Add(node);
            _after.Add(predecessors.Distinct().ToList());
        }

        /// <summary>De directe afhankelijkheden van een knoop, in de opgegeven volgorde.</summary>
        public IReadOnlyList<T> PredecessorsOf(T node) => _after[IndexOf(node)];

        /// <summary>Alle knopen in uitvoervolgorde.</summary>
        /// <exception cref="InvalidOperationException">Bij een onbekende afhankelijkheid of een cyclus.</exception>
        public List<T> Sort()
        {
            var inDegree = new int[_nodes.Count];
            var successors = new List<int>[_nodes.Count];
            for (var i = 0; i < _nodes.Count; i++)
            {
                successors[i] = new List<int>();
            }

            for (var i = 0; i < _nodes.Count; i++)
            {
                foreach (var predecessor in _after[i])
                {
                    if (!_indexOf.TryGetValue(predecessor, out var p))
                    {
                        throw new InvalidOperationException($"{_describe(_nodes[i])} hangt af van {_describe(predecessor)}, dat niet is toegevoegd.");
                    }

                    successors[p].Add(i);
                    inDegree[i]++;
                }
            }

            // Kleinste registratie-index eerst; een SortedSet houdt dat deterministisch.
            var ready = new SortedSet<int>(Enumerable.Range(0, _nodes.Count).Where(i => inDegree[i] == 0));
            var order = new List<T>(_nodes.Count);
            while (ready.Count > 0)
            {
                var next = ready.Min;
                ready.Remove(next);
                order.Add(_nodes[next]);
                foreach (var successor in successors[next])
                {
                    if (--inDegree[successor] == 0)
                    {
                        ready.Add(successor);
                    }
                }
            }

            if (order.Count != _nodes.Count)
            {
                var cyclic = Enumerable.Range(0, _nodes.Count).Where(i => inDegree[i] > 0).Select(i => _describe(_nodes[i]));
                throw new InvalidOperationException($"Cyclische afhankelijkheid tussen: {string.Join(", ", cyclic)}.");
            }

            return order;
        }

        private int IndexOf(T node)
        {
            if (node == null || !_indexOf.TryGetValue(node, out var index))
            {
                throw new ArgumentException($"{(node == null ? "null" : _describe(node))} is niet toegevoegd.", nameof(node));
            }

            return index;
        }
    }
}
