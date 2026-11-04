using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Freight.Terminals
{
    /// <summary>Alle havens en railterminals van de stad (E14-06).</summary>
    public sealed class TerminalNetwork
    {
        public const int MaxTerminals = 256;

        private readonly List<FreightTerminal> _terminals = new List<FreightTerminal>();

        public IReadOnlyList<FreightTerminal> Terminals => _terminals;

        public FreightTerminal Add(ulong owner, float3 position, TerminalSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_terminals.Count >= MaxTerminals)
            {
                throw new InvalidOperationException("Te veel terminals.");
            }

            var terminal = new FreightTerminal(_terminals.Count, owner, position, spec);
            _terminals.Add(terminal);
            return terminal;
        }

        public FreightTerminal Get(int id)
        {
            if (id < 0 || id >= _terminals.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            return _terminals[id];
        }

        /// <summary>De dichtstbijzijnde terminal van dit soort, of null als er geen is.</summary>
        public FreightTerminal Nearest(TerminalKind kind, float3 position)
        {
            FreightTerminal best = null;
            var bestDistance = float.MaxValue;
            foreach (var terminal in _terminals)
            {
                if (terminal.Spec.Kind != kind)
                {
                    continue;
                }

                var distance = math.distancesq(terminal.Position, position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = terminal;
                }
            }

            return best;
        }

        public void Step(float days)
        {
            if (!(days >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(days));
            }

            foreach (var terminal in _terminals)
            {
                terminal.Step(days);
            }
        }

        internal void Restore(IEnumerable<FreightTerminal> terminals)
        {
            _terminals.Clear();
            _terminals.AddRange(terminals);
        }
    }
}
