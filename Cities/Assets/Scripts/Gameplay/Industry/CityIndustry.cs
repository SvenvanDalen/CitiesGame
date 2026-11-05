using System;
using Cities.Simulation.Economy;
using Cities.Simulation.Industry;

namespace Cities.Gameplay.Industry
{
    /// <summary>Uitkomst van één week industrie en toerisme voor de stad.</summary>
    public readonly struct CityIndustryWeek
    {
        public CityIndustryWeek(long exported, long exportIncome, float pollution, float workers, TourismWeek tourism)
        {
            Exported = exported;
            ExportIncome = exportIncome;
            Pollution = pollution;
            Workers = workers;
            Tourism = tourism;
        }

        public long Exported { get; }

        public long ExportIncome { get; }

        public float Pollution { get; }

        public float Workers { get; }

        public TourismWeek Tourism { get; }
    }

    /// <summary>
    /// Industrie, handel en toerisme van de stad (E26-11): winningen leveren grondstoffen die over de grens worden verkocht, trekpleisters
    /// en hotelbedden vormen het toerisme. Alle uitkomsten gaan als weekafsluiting naar het <see cref="Ledger"/>.
    /// </summary>
    public sealed class CityIndustry
    {
        /// <summary>Verkoopprijs per geëxporteerde eenheid en de weekcapaciteit van de grens.</summary>
        public const long ExportPrice = 12;

        public const long ExportCapacity = 100_000;

        public const int BedsPerHotel = 40;

        private int _nextSite = 1;

        public CityIndustry()
        {
            Sites = new IndustryNetwork();
            Tourism = new Tourism();
            Trade = new ResourceTrade();
            Trade.SetOffer(TradeResource.Goods, ExportPrice * 2, ExportPrice, ExportCapacity);
        }

        public IndustryNetwork Sites { get; }

        public Tourism Tourism { get; }

        public ResourceTrade Trade { get; }

        public int Beds { get; private set; }

        public ExtractionSite AddSite(IndustryKind kind, int level = 1, float richness = 1f) =>
            Sites.Add(new ExtractionSite(_nextSite++, kind, level, richness));

        public int AddAttraction(string name, int touristsPerDay, long ticketPrice, long upkeepPerWeek) =>
            Tourism.Add(name, touristsPerDay, ticketPrice, upkeepPerWeek, false);

        public void AddHotel() => Beds += BedsPerHotel;

        /// <summary>Sluit de week af: winnen, grondstoffen verkopen, toerisme afrekenen.</summary>
        public CityIndustryWeek EndWeek(int week, Ledger ledger)
        {
            if (ledger is null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var produced = Sites.Step(week);
            var total = 0f;
            foreach (var amount in produced.Extracted.Values)
            {
                total += amount;
            }

            var deal = Trade.Sell(week, ledger, TradeResource.Goods, (long)total);
            var tourism = Tourism.Settle(week, ledger, Beds);
            return new CityIndustryWeek(deal.Units, deal.Amount, produced.Pollution, produced.Workers, tourism);
        }
    }
}
