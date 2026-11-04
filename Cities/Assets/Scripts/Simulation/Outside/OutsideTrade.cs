using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Economy;

namespace Cities.Simulation.Outside
{
    /// <summary>Wat er van het overschot en tekort van een week werkelijk de grens over kon.</summary>
    public sealed class OutsideTradeWeek
    {
        internal OutsideTradeWeek(IReadOnlyDictionary<string, float> surplus, float exported, float imported, float blockedExports, float blockedImports)
        {
            Surplus = surplus;
            Exported = exported;
            Imported = imported;
            BlockedExports = blockedExports;
            BlockedImports = blockedImports;
        }

        /// <summary>Het overschot (positief) en tekort (negatief) na de capaciteitsgrens, klaar voor <see cref="Trade.Settle"/>.</summary>
        public IReadOnlyDictionary<string, float> Surplus { get; }

        public float Exported { get; }

        public float Imported { get; }

        /// <summary>Ton overschot die niet weg kon omdat de verbindingen vol of dicht waren.</summary>
        public float BlockedExports { get; }

        /// <summary>Ton tekort die niet kon worden aangevoerd; die goederen ontbreken dus in de stad.</summary>
        public float BlockedImports { get; }
    }

    /// <summary>
    /// Import en export lopen door de buitenverbindingen (E14-04): per richting past er per week hoogstens de goederencapaciteit van de open
    /// verbindingen doorheen. Is er meer overschot of tekort, dan schalen alle goederen evenredig mee; zonder open verbinding gaat er niets over de
    /// grens. Eén eenheid is één ton, zoals in het goederenverkeer.
    /// </summary>
    public static class OutsideTrade
    {
        public const int DaysPerWeek = 7;

        public static OutsideTradeWeek Limit(OutsideConnections connections, IReadOnlyDictionary<string, float> surplus)
        {
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            if (surplus == null)
            {
                throw new ArgumentNullException(nameof(surplus));
            }

            var capacity = connections.FreightTonnesPerDay * DaysPerWeek;
            var totalExport = surplus.Values.Where(v => v > 0f).Sum();
            var totalImport = -surplus.Values.Where(v => v < 0f).Sum();
            var exportFactor = Factor(capacity, totalExport);
            var importFactor = Factor(capacity, totalImport);

            var limited = new SortedDictionary<string, float>(StringComparer.Ordinal);
            foreach (var good in surplus.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var units = surplus[good];
                limited[good] = units * (units > 0f ? exportFactor : importFactor);
            }

            var exported = totalExport * exportFactor;
            var imported = totalImport * importFactor;
            return new OutsideTradeWeek(limited, exported, imported, totalExport - exported, totalImport - imported);
        }

        /// <summary>Beperkt het overschot door de verbindingen en boekt daarna de handel.</summary>
        public static (TradeWeek Trade, OutsideTradeWeek Flow) Settle(
            int week, OutsideConnections connections, Trade trade, Ledger ledger, IReadOnlyDictionary<string, float> surplus)
        {
            if (trade == null)
            {
                throw new ArgumentNullException(nameof(trade));
            }

            var flow = Limit(connections, surplus);
            return (trade.Settle(week, ledger, flow.Surplus), flow);
        }

        private static float Factor(float capacity, float demand) => demand <= capacity ? 1f : capacity / demand;
    }
}
