using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Economy
{
    /// <summary>Sectie <c>INCM</c>: de trekpleisters en de handelsprijzen (E11-09).</summary>
    public sealed class OtherIncomeSection : ISaveSection
    {
        public const string SectionId = "INCM";

        private readonly Tourism _tourism;
        private readonly Trade _trade;

        public OtherIncomeSection(Tourism tourism, Trade trade)
        {
            _tourism = tourism ?? throw new ArgumentNullException(nameof(tourism));
            _trade = trade ?? throw new ArgumentNullException(nameof(trade));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_tourism.NextId);
            var attractions = _tourism.Attractions.ToArray();
            writer.Write(attractions.Length);
            foreach (var a in attractions)
            {
                writer.Write(a.Id);
                writer.Write(a.Name);
                writer.Write(a.TouristsPerDay);
                writer.Write(a.TicketPrice);
                writer.Write(a.UpkeepPerWeek);
                writer.Write(a.IsUnique);
            }

            var prices = _trade.Prices.ToArray();
            writer.Write(prices.Length);
            foreach (var price in prices)
            {
                writer.Write(price.Key);
                writer.Write(price.Value.Import);
                writer.Write(price.Value.Export);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var attractions = new List<Attraction>();
            var count = ReadCount(reader, Tourism.MaxAttractions, "trekpleisters");
            for (var i = 0; i < count; i++)
            {
                attractions.Add(ReadAttraction(reader));
            }

            var prices = new List<KeyValuePair<string, (long Import, long Export)>>();
            var priceCount = ReadCount(reader, Trade.MaxGoods, "goederen");
            for (var i = 0; i < priceCount; i++)
            {
                var good = reader.ReadString();
                var importPrice = reader.ReadInt64();
                var exportPrice = reader.ReadInt64();
                prices.Add(new KeyValuePair<string, (long Import, long Export)>(good, (importPrice, exportPrice)));
            }

            reader.ExpectEnd();
            if (attractions.Any(a => a.Id >= nextId) || attractions.Select(a => a.Id).Distinct().Count() != count)
            {
                throw reader.Corrupt("volgnummer te laag of dubbel ID");
            }

            if (attractions.Where(a => a.IsUnique).GroupBy(a => a.Name).Any(g => g.Count() > 1))
            {
                throw reader.Corrupt("een unieke trekpleister staat twee keer in de stad");
            }

            if (prices.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value.Export < 0 || p.Value.Import < p.Value.Export)
                || prices.Select(p => p.Key).Distinct().Count() != priceCount)
            {
                throw reader.Corrupt("ongeldige handelsprijs");
            }

            return () =>
            {
                _tourism.Restore(attractions, nextId);
                _trade.Restore(prices);
            };
        }

        private static Attraction ReadAttraction(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var name = reader.ReadString();
            var tourists = reader.ReadInt32();
            var ticket = reader.ReadInt64();
            var upkeep = reader.ReadInt64();
            var isUnique = reader.ReadBool();
            try
            {
                return new Attraction(id, name, tourists, ticket, upkeep, isUnique);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
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
