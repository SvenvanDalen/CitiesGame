using System;

namespace Cities.Simulation.Economy
{
    /// <summary>Wat er over de stadsgrens kan worden gekocht of verkocht.</summary>
    public enum TradeResource
    {
        Goods = 0,
        Power = 1,
        Water = 2,
        Waste = 3,
    }

    /// <summary>Uitkomst van een koop of verkoop: hoeveel eenheden er echt zijn verhandeld en wat dat kostte of opleverde.</summary>
    public readonly struct ResourceDeal
    {
        public ResourceDeal(long units, long amount)
        {
            Units = units;
            Amount = amount;
        }

        public long Units { get; }

        public long Amount { get; }
    }

    /// <summary>Prijzen en weekcapaciteit van één grondstof.</summary>
    public readonly struct ResourceOffer
    {
        public ResourceOffer(long buyPrice, long sellPrice, long weeklyCapacity)
        {
            BuyPrice = buyPrice;
            SellPrice = sellPrice;
            WeeklyCapacity = weeklyCapacity;
        }

        /// <summary>Wat een ingekochte eenheid kost.</summary>
        public long BuyPrice { get; }

        /// <summary>Wat een verkochte eenheid opbrengt; bij afval is dit de verwerkingsprijs die de stad betaalt.</summary>
        public long SellPrice { get; }

        /// <summary>Hoeveel eenheden per week de verbinding (leiding, net, havenkade) in elke richting aankan.</summary>
        public long WeeklyCapacity { get; }
    }

    /// <summary>
    /// Import en export van goederen, stroom, water en afval (E14-05) met een prijs per eenheid, geboekt in het geldmodel
    /// (<see cref="Ledger"/>, categorie <see cref="LedgerCategory.Trade"/>). Elke grondstof heeft een eigen koop- en verkoopprijs en een
    /// weekcapaciteit per richting; wat boven de capaciteit uitkomt of niet te betalen is, wordt niet verhandeld. De koopprijs ligt
    /// minstens op de verkoopprijs, zodat handel drijven op de grens niets oplevert. Afval kan alleen worden afgevoerd en kost geld
    /// (de verkoopprijs is een verwerkingsprijs). Het weekverbruik van de capaciteit loopt mee met de spelweek en reset vanzelf.
    /// </summary>
    public sealed class ResourceTrade
    {
        private static readonly int Count = Enum.GetValues(typeof(TradeResource)).Length;

        private readonly ResourceOffer[] _offers = new ResourceOffer[Count];
        private readonly long[] _bought = new long[Count];
        private readonly long[] _sold = new long[Count];

        /// <summary>De week waarvoor de verbruikte capaciteit geldt.</summary>
        public int CurrentWeek { get; private set; }

        internal static int ResourceCount => Count;

        /// <summary>Of een grondstof ingekocht kan worden; afval niet.</summary>
        public static bool CanBuy(TradeResource resource) => resource != TradeResource.Waste;

        public ResourceOffer Offer(TradeResource resource) => _offers[Index(resource)];

        /// <summary>Stelt de aansluiting in. Standaard is er geen verbinding (capaciteit 0).</summary>
        public void SetOffer(TradeResource resource, long buyPrice, long sellPrice, long weeklyCapacity)
        {
            var index = Index(resource);
            Validate(resource, buyPrice, sellPrice, weeklyCapacity);
            _offers[index] = new ResourceOffer(buyPrice, sellPrice, weeklyCapacity);
        }

        /// <summary>Hoeveel eenheden er in <paramref name="week"/> nog ingekocht kunnen worden binnen de capaciteit.</summary>
        public long BuyRoom(int week, TradeResource resource)
        {
            if (!CanBuy(resource))
            {
                return 0;
            }

            Roll(week);
            var index = Index(resource);
            return Math.Max(0, _offers[index].WeeklyCapacity - _bought[index]);
        }

        /// <summary>Hoeveel eenheden er in <paramref name="week"/> nog verkocht of afgevoerd kunnen worden.</summary>
        public long SellRoom(int week, TradeResource resource)
        {
            Roll(week);
            var index = Index(resource);
            return Math.Max(0, _offers[index].WeeklyCapacity - _sold[index]);
        }

        public long BoughtThisWeek(TradeResource resource) => _bought[Index(resource)];

        public long SoldThisWeek(TradeResource resource) => _sold[Index(resource)];

        /// <summary>
        /// Koopt tot <paramref name="units"/> eenheden in. Het aantal wordt beperkt door de weekcapaciteit en het saldo;
        /// de uitkomst zegt wat er echt is gekocht.
        /// </summary>
        public ResourceDeal Buy(int week, Ledger ledger, TradeResource resource, long units)
        {
            CheckArguments(ledger, units);
            var index = Index(resource);
            var price = _offers[index].BuyPrice;
            var actual = Affordable(ledger, Math.Min(units, BuyRoom(week, resource)), price);
            if (actual <= 0)
            {
                return default;
            }

            var amount = checked(actual * price);
            ledger.Force(week, LedgerCategory.Trade, amount);
            _bought[index] += actual;
            return new ResourceDeal(actual, amount);
        }

        /// <summary>
        /// Verkoopt tot <paramref name="units"/> eenheden over de grens (bij afval: voert ze af tegen de verwerkingsprijs). Het aantal wordt
        /// beperkt door de weekcapaciteit; bij afval ook door het saldo.
        /// </summary>
        public ResourceDeal Sell(int week, Ledger ledger, TradeResource resource, long units)
        {
            CheckArguments(ledger, units);
            var index = Index(resource);
            var price = _offers[index].SellPrice;
            var actual = Math.Min(units, SellRoom(week, resource));
            var isWaste = resource == TradeResource.Waste;
            if (isWaste)
            {
                actual = Affordable(ledger, actual, price);
            }

            if (actual <= 0)
            {
                return default;
            }

            var amount = checked(actual * price);
            if (isWaste)
            {
                ledger.Force(week, LedgerCategory.Trade, amount);
            }
            else
            {
                ledger.Earn(week, LedgerCategory.Trade, amount);
            }

            _sold[index] += actual;
            return new ResourceDeal(actual, amount);
        }

        /// <summary>
        /// Handelt een saldo af: positief is overschot (verkopen of afvoeren), negatief is tekort (bijkopen). Handig voor de netwerken die
        /// per week een nettostand hebben (stroom, water, afval).
        /// </summary>
        public ResourceDeal Settle(int week, Ledger ledger, TradeResource resource, long surplus) =>
            surplus >= 0 ? Sell(week, ledger, resource, surplus) : Buy(week, ledger, resource, -surplus);

        internal static void Validate(TradeResource resource, long buyPrice, long sellPrice, long weeklyCapacity)
        {
            if (sellPrice < 0 || weeklyCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sellPrice), "Prijs en capaciteit zijn niet negatief.");
            }

            if (CanBuy(resource) && buyPrice < sellPrice)
            {
                throw new ArgumentOutOfRangeException(nameof(buyPrice), "De koopprijs is minstens zo hoog als de verkoopprijs.");
            }

            if (!CanBuy(resource) && buyPrice != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(buyPrice), "Afval kan niet worden ingekocht.");
            }
        }

        internal void Snapshot(out int week, ResourceOffer[] offers, long[] bought, long[] sold)
        {
            week = CurrentWeek;
            Array.Copy(_offers, offers, Count);
            Array.Copy(_bought, bought, Count);
            Array.Copy(_sold, sold, Count);
        }

        internal void Restore(int week, ResourceOffer[] offers, long[] bought, long[] sold)
        {
            CurrentWeek = week;
            Array.Copy(offers, _offers, Count);
            Array.Copy(bought, _bought, Count);
            Array.Copy(sold, _sold, Count);
        }

        private static long Affordable(Ledger ledger, long units, long price)
        {
            if (price <= 0 || ledger.UnlimitedMoney)
            {
                return units;
            }

            return Math.Min(units, Math.Max(0, ledger.Balance) / price);
        }

        private static void CheckArguments(Ledger ledger, long units)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (units < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(units), units, "Een aantal eenheden is niet negatief.");
            }
        }

        private static int Index(TradeResource resource)
        {
            var index = (int)resource;
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(resource));
            }

            return index;
        }

        private void Roll(int week)
        {
            if (week == CurrentWeek)
            {
                return;
            }

            CurrentWeek = week;
            Array.Clear(_bought, 0, Count);
            Array.Clear(_sold, 0, Count);
        }
    }
}
