using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Economy
{
    /// <summary>Alle goederenketens van de stad, op naam (E11-08).</summary>
    public sealed class GoodsNetwork
    {
        public const int MaxChains = 1000;

        private readonly SortedDictionary<string, GoodsChain> _chains = new SortedDictionary<string, GoodsChain>(StringComparer.Ordinal);

        public IEnumerable<GoodsChain> Chains => _chains.Values;

        public GoodsChain Add(GoodsChain chain)
        {
            if (chain is null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (_chains.ContainsKey(chain.Name))
            {
                throw new InvalidOperationException("Dit goed heeft al een keten: " + chain.Name);
            }

            if (_chains.Count >= MaxChains)
            {
                throw new InvalidOperationException("Te veel goederenketens.");
            }

            _chains.Add(chain.Name, chain);
            return chain;
        }

        public GoodsChain Get(string good) =>
            _chains.TryGetValue(good ?? string.Empty, out var chain) ? chain : throw new KeyNotFoundException("Onbekend goed: " + good);

        public bool Contains(string good) => good != null && _chains.ContainsKey(good);

        /// <summary>Gemiddelde beschikbaarheid over alle ketens, gelijk gewogen; 1 zonder ketens.</summary>
        public float AverageAvailability() => _chains.Count == 0 ? 1f : _chains.Values.Average(c => c.Availability);
    }
}
