using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Economy
{
    /// <summary>Uitkomst van één week import en export.</summary>
    public readonly struct TradeWeek
    {
        public TradeWeek(long exports, long imports)
        {
            Exports = exports;
            Imports = imports;
        }

        public long Exports { get; }

        public long Imports { get; }

        public long Balance => Exports - Imports;
    }

    /// <summary>
    /// Import en export (E11-09): per goed een inkoop- en een verkoopprijs. Een overschot aan een goed gaat tegen de exportprijs de grens
    /// over, een tekort wordt tegen de importprijs bijgekocht; die inkoop is verplicht (<see cref="Ledger.Force"/>). De importprijs ligt
    /// minstens op de exportprijs, zodat handel drijven op de grens niets oplevert.
    /// shortcut: de overschotten worden door de aanroeper geleverd, tot de goederenketen (E11-08) ze zelf bijhoudt.
    /// </summary>
    public sealed class Trade
    {
        public const int MaxGoods = 1000;

        private readonly SortedDictionary<string, (long Import, long Export)> _prices =
            new SortedDictionary<string, (long Import, long Export)>(StringComparer.Ordinal);

        public IEnumerable<string> Goods => _prices.Keys;

        internal IEnumerable<KeyValuePair<string, (long Import, long Export)>> Prices => _prices;

        public long ImportPrice(string good) => Get(good).Import;

        public long ExportPrice(string good) => Get(good).Export;

        public void SetPrices(string good, long importPrice, long exportPrice)
        {
            if (string.IsNullOrWhiteSpace(good))
            {
                throw new ArgumentException("Een goed heeft een naam nodig.", nameof(good));
            }

            if (exportPrice < 0 || importPrice < exportPrice)
            {
                throw new ArgumentOutOfRangeException(nameof(importPrice), "De exportprijs is niet negatief en de importprijs is minstens zo hoog.");
            }

            if (!_prices.ContainsKey(good) && _prices.Count >= MaxGoods)
            {
                throw new InvalidOperationException("Te veel goederen.");
            }

            _prices[good] = (importPrice, exportPrice);
        }

        /// <summary>
        /// Boekt de handel van deze week. <paramref name="surplus"/> is per goed het overschot (positief) of tekort (negatief) in eenheden.
        /// </summary>
        public TradeWeek Settle(int week, Ledger ledger, IReadOnlyDictionary<string, float> surplus)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (surplus == null)
            {
                throw new ArgumentNullException(nameof(surplus));
            }

            long exports = 0, imports = 0;
            foreach (var good in surplus.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var prices = Get(good);
                var units = surplus[good];
                if (units > 0f)
                {
                    exports += (long)Math.Round(units * prices.Export);
                }
                else if (units < 0f)
                {
                    imports += (long)Math.Round(-units * prices.Import);
                }
            }

            ledger.Earn(week, LedgerCategory.Trade, exports);
            ledger.Force(week, LedgerCategory.Trade, imports);
            return new TradeWeek(exports, imports);
        }

        internal void Restore(IEnumerable<KeyValuePair<string, (long Import, long Export)>> prices)
        {
            _prices.Clear();
            foreach (var price in prices)
            {
                _prices[price.Key] = price.Value;
            }
        }

        private (long Import, long Export) Get(string good) =>
            _prices.TryGetValue(good ?? string.Empty, out var prices) ? prices : throw new ArgumentException($"Onbekend goed: {good}.", nameof(good));
    }
}
