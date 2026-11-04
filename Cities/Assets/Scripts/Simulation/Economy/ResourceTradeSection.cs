using System;
using Cities.Core.Persistence;

namespace Cities.Simulation.Economy
{
    /// <summary>Sectie <c>XTRD</c>: prijzen, capaciteit en weekverbruik van import en export van goederen, stroom, water en afval (E14-05).</summary>
    public sealed class ResourceTradeSection : ISaveSection
    {
        public const string SectionId = "XTRD";

        private readonly ResourceTrade _trade;

        public ResourceTradeSection(ResourceTrade trade)
        {
            _trade = trade ?? throw new ArgumentNullException(nameof(trade));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var count = ResourceTrade.ResourceCount;
            var offers = new ResourceOffer[count];
            var bought = new long[count];
            var sold = new long[count];
            _trade.Snapshot(out var week, offers, bought, sold);
            writer.Write(week);
            writer.Write(count);
            for (var i = 0; i < count; i++)
            {
                writer.Write(offers[i].BuyPrice);
                writer.Write(offers[i].SellPrice);
                writer.Write(offers[i].WeeklyCapacity);
                writer.Write(bought[i]);
                writer.Write(sold[i]);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var week = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (count != ResourceTrade.ResourceCount)
            {
                throw reader.Corrupt($"onverwacht aantal grondstoffen: {count}");
            }

            var offers = new ResourceOffer[count];
            var bought = new long[count];
            var sold = new long[count];
            for (var i = 0; i < count; i++)
            {
                var buy = reader.ReadInt64();
                var sell = reader.ReadInt64();
                var capacity = reader.ReadInt64();
                bought[i] = reader.ReadInt64();
                sold[i] = reader.ReadInt64();
                try
                {
                    ResourceTrade.Validate((TradeResource)i, buy, sell, capacity);
                }
                catch (ArgumentException e)
                {
                    throw reader.Corrupt(e.Message);
                }

                if (bought[i] < 0 || sold[i] < 0 || bought[i] > capacity || sold[i] > capacity)
                {
                    throw reader.Corrupt("ongeldig weekverbruik");
                }

                offers[i] = new ResourceOffer(buy, sell, capacity);
            }

            reader.ExpectEnd();
            return () => _trade.Restore(week, offers, bought, sold);
        }
    }
}
