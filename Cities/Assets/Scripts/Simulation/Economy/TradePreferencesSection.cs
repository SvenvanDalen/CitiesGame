using System;
using System.Collections.Generic;
using Cities.Core.Persistence;

namespace Cities.Simulation.Economy
{
    /// <summary>Sectie <c>TRDP</c>: seed, marktstand per goed, bekende verbindingen en tegenvallers (E14-09).</summary>
    public sealed class TradePreferencesSection : ISaveSection
    {
        public const string SectionId = "TRDP";

        private static readonly int KindCount = Enum.GetValues(typeof(ConnectionKind)).Length;

        private readonly TradePreferences _preferences;

        public TradePreferencesSection(TradePreferences preferences)
        {
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(unchecked((long)_preferences.Seed));
            var goods = new List<KeyValuePair<string, TradeGoodState>>(_preferences.GoodStates);
            writer.Write(goods.Count);
            foreach (var pair in goods)
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value.BaseImport);
                writer.Write(pair.Value.BaseExport);
                writer.Write((int)pair.Value.Route);
                writer.Write(pair.Value.PriceFactor);
                writer.Write(pair.Value.DemandFactor);
            }

            var known = new List<KeyValuePair<int, ConnectionKind>>(_preferences.KnownConnections);
            writer.Write(known.Count);
            foreach (var pair in known)
            {
                writer.Write(pair.Key);
                writer.Write((int)pair.Value);
            }

            for (var i = 0; i < KindCount; i++)
            {
                writer.Write(_preferences.Setbacks[i]);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var seed = unchecked((ulong)reader.ReadInt64());
            var goods = new List<KeyValuePair<string, TradeGoodState>>();
            var goodCount = ReadCount(reader, TradePreferences.MaxGoods, "goederen");
            for (var i = 0; i < goodCount; i++)
            {
                goods.Add(ReadGood(reader));
            }

            var known = new List<KeyValuePair<int, ConnectionKind>>();
            var knownCount = ReadCount(reader, TradePreferences.MaxConnections, "verbindingen");
            for (var i = 0; i < knownCount; i++)
            {
                var id = reader.ReadInt32();
                known.Add(new KeyValuePair<int, ConnectionKind>(id, ReadKind(reader)));
            }

            var setbacks = new float[KindCount];
            for (var i = 0; i < KindCount; i++)
            {
                setbacks[i] = reader.ReadSingle();
                if (!(setbacks[i] >= 0f && setbacks[i] <= 1f))
                {
                    throw reader.Corrupt("tegenvaller buiten 0 tot 1");
                }
            }

            reader.ExpectEnd();
            if (HasDuplicates(goods, p => p.Key) || HasDuplicates(known, p => p.Key))
            {
                throw reader.Corrupt("dubbel goed of dubbele verbinding");
            }

            return () => _preferences.Restore(seed, goods, known, setbacks);
        }

        private static bool HasDuplicates<T, TKey>(List<T> items, Func<T, TKey> key)
        {
            var seen = new HashSet<TKey>();
            foreach (var item in items)
            {
                if (!seen.Add(key(item)))
                {
                    return true;
                }
            }

            return false;
        }

        private static KeyValuePair<string, TradeGoodState> ReadGood(SaveReader reader)
        {
            var name = reader.ReadString();
            var baseImport = reader.ReadInt64();
            var baseExport = reader.ReadInt64();
            var route = ReadKind(reader);
            var price = reader.ReadSingle();
            var demand = reader.ReadSingle();
            if (string.IsNullOrWhiteSpace(name) || baseExport < 0 || baseImport < baseExport)
            {
                throw reader.Corrupt("ongeldig goed");
            }

            if (!(price >= TradePreferences.MinPrice && price <= TradePreferences.MaxPrice)
                || !(demand >= TradePreferences.MinDemand && demand <= TradePreferences.MaxDemand))
            {
                throw reader.Corrupt("marktfactor buiten het bereik");
            }

            return new KeyValuePair<string, TradeGoodState>(name, new TradeGoodState(baseImport, baseExport, route, price, demand));
        }

        private static ConnectionKind ReadKind(SaveReader reader)
        {
            var kind = reader.ReadInt32();
            if (kind < 0 || kind >= KindCount)
            {
                throw reader.Corrupt($"onbekende soort verbinding: {kind}");
            }

            return (ConnectionKind)kind;
        }

        private static int ReadCount(SaveReader reader, int max, string what)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > max)
            {
                throw reader.Corrupt($"onmogelijk aantal {what}: {count}");
            }

            return count;
        }
    }
}
