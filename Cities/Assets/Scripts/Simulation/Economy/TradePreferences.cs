using System;
using System.Collections.Generic;
using Cities.Core.Randomness;

namespace Cities.Simulation.Economy
{
    /// <summary>Basisprijzen en marktstand van één goed.</summary>
    internal sealed class TradeGoodState
    {
        public TradeGoodState(long baseImport, long baseExport, ConnectionKind route, float priceFactor, float demandFactor)
        {
            BaseImport = baseImport;
            BaseExport = baseExport;
            Route = route;
            PriceFactor = priceFactor;
            DemandFactor = demandFactor;
        }

        public long BaseImport { get; }

        public long BaseExport { get; }

        public ConnectionKind Route { get; }

        public float PriceFactor { get; set; }

        public float DemandFactor { get; set; }
    }

    /// <summary>
    /// Handelsvoorkeuren (E14-09): de prijzen en de vraag van buiten wisselen per goed. Elke week schuift de prijsfactor en de
    /// vraagfactor met seedgebonden ruis (<see cref="DeterministicRandom"/> uit seed, goed en week) en trekt ze terug naar 1.
    /// Sluit een verbinding (of verdwijnt ze), dan is dat een tegenvaller: voor de goederen die over die soort verbinding gaan stijgt
    /// de importprijs, daalt de exportprijs en zakt de vraag, naar het aandeel van de verbinding in die soort. De tegenvaller
    /// ebt per week weg. <see cref="Apply"/> zet de uitkomst in <see cref="Trade"/>.
    /// </summary>
    public sealed class TradePreferences
    {
        public const float MinPrice = 0.6f;
        public const float MaxPrice = 1.6f;
        public const float MinDemand = 0.5f;
        public const float MaxDemand = 1.5f;
        public const float Reversion = 0.15f;
        public const float PriceVolatility = 0.06f;
        public const float DemandVolatility = 0.08f;
        public const float SetbackDecay = 0.85f;
        public const float SetbackFloor = 0.01f;
        public const float SetbackImportRise = 0.5f;
        public const float SetbackExportDrop = 0.4f;
        public const float SetbackDemandDrop = 0.6f;
        public const int MaxGoods = Trade.MaxGoods;
        public const int MaxConnections = 10000;

        private readonly SortedDictionary<string, TradeGoodState> _goods = new SortedDictionary<string, TradeGoodState>(StringComparer.Ordinal);
        private readonly SortedDictionary<int, ConnectionKind> _known = new SortedDictionary<int, ConnectionKind>();
        private readonly float[] _setback = new float[Enum.GetValues(typeof(ConnectionKind)).Length];

        public TradePreferences(ulong seed)
        {
            Seed = seed;
        }

        public ulong Seed { get; private set; }

        public IEnumerable<string> Goods => _goods.Keys;

        internal IEnumerable<KeyValuePair<string, TradeGoodState>> GoodStates => _goods;

        internal IEnumerable<KeyValuePair<int, ConnectionKind>> KnownConnections => _known;

        internal IReadOnlyList<float> Setbacks => _setback;

        /// <summary>Meldt een goed aan met zijn basisprijzen en de soort verbinding waarover het vooral gaat.</summary>
        public void Define(string good, long baseImport, long baseExport, ConnectionKind route)
        {
            if (string.IsNullOrWhiteSpace(good))
            {
                throw new ArgumentException("Een goed heeft een naam nodig.", nameof(good));
            }

            if (baseExport < 0 || baseImport < baseExport)
            {
                throw new ArgumentOutOfRangeException(nameof(baseImport), "De exportprijs is niet negatief en de importprijs is minstens zo hoog.");
            }

            if (!_goods.ContainsKey(good) && _goods.Count >= MaxGoods)
            {
                throw new InvalidOperationException("Te veel goederen.");
            }

            _goods[good] = new TradeGoodState(baseImport, baseExport, route, 1f, 1f);
        }

        public float PriceFactor(string good) => Get(good).PriceFactor;

        public float DemandFactor(string good) => Get(good).DemandFactor;

        /// <summary>Hoe zwaar de tegenvaller voor een soort verbinding nu nog drukt (0 tot 1).</summary>
        public float Setback(ConnectionKind kind) => _setback[(int)kind];

        /// <summary>Importprijs nu: marktfactor en tegenvaller verwerkt.</summary>
        public long ImportPrice(string good)
        {
            var state = Get(good);
            var rise = 1f + (SetbackImportRise * _setback[(int)state.Route]);
            return Math.Max(ExportPrice(good), (long)Math.Round(state.BaseImport * state.PriceFactor * rise));
        }

        /// <summary>Exportprijs nu: marktfactor en tegenvaller verwerkt.</summary>
        public long ExportPrice(string good)
        {
            var state = Get(good);
            var drop = 1f - (SetbackExportDrop * _setback[(int)state.Route]);
            return (long)Math.Round(state.BaseExport * state.PriceFactor * drop);
        }

        /// <summary>Hoeveel van de vraag van buiten er nu is (1 is normaal), met de tegenvaller erin.</summary>
        public float ExternalDemand(string good)
        {
            var state = Get(good);
            return state.DemandFactor * (1f - (SetbackDemandDrop * _setback[(int)state.Route]));
        }

        /// <summary>
        /// Wat van het overschot (positief) of tekort (negatief) echt over de grens gaat: bij zwakke vraag blijft een deel van het
        /// overschot liggen. Een tekort moet altijd bijgekocht worden.
        /// </summary>
        public float Tradeable(string good, float surplus) => surplus > 0f ? surplus * Math.Min(1f, ExternalDemand(good)) : surplus;

        /// <summary>Verwerkt een week: tegenvaller laten wegebben, verbindingen vergelijken en de markt laten schuiven.</summary>
        public void Step(int week, ITradeConnections connections)
        {
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            for (var i = 0; i < _setback.Length; i++)
            {
                var decayed = _setback[i] * SetbackDecay;
                _setback[i] = decayed < SetbackFloor ? 0f : decayed;
            }

            SyncConnections(connections);
            foreach (var pair in _goods)
            {
                var rng = new DeterministicRandom(SeedHash.Combine(Seed, SeedHash.Of(pair.Key), (ulong)(uint)week));
                var state = pair.Value;
                state.PriceFactor = Drift(state.PriceFactor, rng.NextGaussian(0f, PriceVolatility), MinPrice, MaxPrice);
                state.DemandFactor = Drift(state.DemandFactor, rng.NextGaussian(0f, DemandVolatility), MinDemand, MaxDemand);
            }
        }

        /// <summary>Zet de huidige prijzen in <paramref name="trade"/>.</summary>
        public void Apply(Trade trade)
        {
            if (trade == null)
            {
                throw new ArgumentNullException(nameof(trade));
            }

            foreach (var good in _goods.Keys)
            {
                trade.SetPrices(good, ImportPrice(good), ExportPrice(good));
            }
        }

        internal void Restore(ulong seed, IEnumerable<KeyValuePair<string, TradeGoodState>> goods, IEnumerable<KeyValuePair<int, ConnectionKind>> known, float[] setbacks)
        {
            Seed = seed;
            _goods.Clear();
            foreach (var pair in goods)
            {
                _goods[pair.Key] = pair.Value;
            }

            _known.Clear();
            foreach (var pair in known)
            {
                _known[pair.Key] = pair.Value;
            }

            Array.Copy(setbacks, _setback, _setback.Length);
        }

        private static float Drift(float factor, float noise, float min, float max)
        {
            var next = factor + ((1f - factor) * Reversion) + noise;
            return Math.Max(min, Math.Min(max, next));
        }

        private void SyncConnections(ITradeConnections connections)
        {
            var open = new SortedDictionary<int, ConnectionKind>();
            foreach (var c in connections.Connections)
            {
                if (c.IsOpen)
                {
                    open[c.Id] = c.Kind;
                }
            }

            var openBefore = new int[_setback.Length];
            foreach (var kind in _known.Values)
            {
                openBefore[(int)kind]++;
            }

            foreach (var pair in _known)
            {
                if (!open.ContainsKey(pair.Key))
                {
                    var index = (int)pair.Value;
                    _setback[index] = Math.Min(1f, _setback[index] + (1f / openBefore[index]));
                }
            }

            _known.Clear();
            foreach (var pair in open)
            {
                _known[pair.Key] = pair.Value;
            }
        }

        private TradeGoodState Get(string good) =>
            _goods.TryGetValue(good ?? string.Empty, out var state) ? state : throw new ArgumentException($"Onbekend goed: {good}.", nameof(good));
    }
}
