using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Economy;

namespace Cities.Simulation.Visitors
{
    /// <summary>
    /// Bezoekersstroom en toerisme (E14-07). Bezoekers komen via de buitenverbindingen (<see cref="IOutsideConnections"/>), dagjesmensen
    /// keren dezelfde dag terug en de rest overnacht in hotels (hoogste sterren eerst). Gasten bezoeken attracties naar aantrekkingskracht, tot de
    /// capaciteit, en geven geld uit. De stad int entree, toeristenbelasting per nacht en een deel van de bestedingen (<see cref="Settle"/>,
    /// via het <see cref="Ledger"/>) en betaalt het onderhoud van de attracties. Tevredenheid (verbindingen, bedden, attracties) stuurt de
    /// reputatie, en die de vraag van de volgende week. Alles is geheel getal en dus deterministisch.
    /// </summary>
    public sealed class VisitorEconomy
    {
        public const int MaxItems = 100_000;
        public const int StayNights = 3;
        public const long TouristTaxPerNight = 3;
        public const long DayTripSpend = 25;
        public const int SpendingTaxPercent = 10;
        public const int StartReputation = 50;

        private readonly SortedDictionary<int, VisitorHotel> _hotels = new SortedDictionary<int, VisitorHotel>();
        private readonly SortedDictionary<int, VisitorSite> _sites = new SortedDictionary<int, VisitorSite>();

        public int Reputation { get; private set; } = StartReputation;

        public int NextHotelId { get; private set; } = 1;

        public int NextSiteId { get; private set; } = 1;

        public IEnumerable<VisitorHotel> Hotels => _hotels.Values;

        public IEnumerable<VisitorSite> Sites => _sites.Values;

        public int TotalBeds => _hotels.Values.Sum(h => h.Beds);

        public int AddHotel(int beds, int stars, long nightlyRate)
        {
            if (_hotels.Count >= MaxItems)
            {
                throw new InvalidOperationException("Te veel hotels.");
            }

            _hotels[NextHotelId] = new VisitorHotel(NextHotelId, beds, stars, nightlyRate);
            return NextHotelId++;
        }

        public bool RemoveHotel(int id) => _hotels.Remove(id);

        public int AddSite(string name, int appeal, int capacityPerDay, long ticketPrice, long upkeepPerWeek)
        {
            if (_sites.Count >= MaxItems)
            {
                throw new InvalidOperationException("Te veel attracties.");
            }

            _sites[NextSiteId] = new VisitorSite(NextSiteId, name, appeal, capacityPerDay, ticketPrice, upkeepPerWeek);
            return NextSiteId++;
        }

        public bool RemoveSite(int id) => _sites.Remove(id);

        /// <summary>Gewenste aankomsten per week bij de huidige reputatie (50 tot 150 procent van de som van de aantrekkingskracht).</summary>
        public long WeeklyDemand() => _sites.Values.Sum(s => (long)s.Appeal) * Ledger.DaysPerWeek * (50 + Reputation) / 100;

        /// <summary>Draait één week en boekt inkomsten en onderhoud in het grootboek.</summary>
        public VisitorWeek Settle(int week, Ledger ledger, IOutsideConnections connections)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            var result = new VisitorWeek { Demand = WeeklyDemand() };
            Arrive(result, connections);
            Stay(result);
            Visit(result);
            result.Taxes = (result.NightsSold * TouristTaxPerNight) + (result.Spending * SpendingTaxPercent / 100);
            ledger.Earn(week, LedgerCategory.Other, result.Tickets);
            ledger.Earn(week, LedgerCategory.Taxes, result.Taxes);
            ledger.Force(week, LedgerCategory.Upkeep, result.Upkeep);
            UpdateReputation(result);
            return result;
        }

        internal void Restore(IEnumerable<VisitorHotel> hotels, IEnumerable<VisitorSite> sites, int nextHotelId, int nextSiteId, int reputation)
        {
            _hotels.Clear();
            foreach (var hotel in hotels)
            {
                _hotels[hotel.Id] = hotel;
            }

            _sites.Clear();
            foreach (var site in sites)
            {
                _sites[site.Id] = site;
            }

            NextHotelId = nextHotelId;
            NextSiteId = nextSiteId;
            Reputation = reputation;
        }

        /// <summary>Verdeelt de vraag over de kanalen naar capaciteit en telt de dagjesmensen.</summary>
        private static void Arrive(VisitorWeek result, IOutsideConnections connections)
        {
            var capacities = new long[VisitorProfiles.ChannelCount];
            for (var i = 0; i < capacities.Length; i++)
            {
                capacities[i] = Math.Max(0, connections.WeeklyCapacity((VisitorChannel)i));
            }

            result.Arrivals = Math.Min(result.Demand, capacities.Sum());
            var shares = Allocate(result.Arrivals, capacities);
            for (var i = 0; i < shares.Length; i++)
            {
                result.DayTrippers += shares[i] * VisitorProfiles.DayTripPercent((VisitorChannel)i) / 100;
            }
        }

        /// <summary>Vult de hotels van hoog naar laag in sterren; wie geen bed vindt vertrekt.</summary>
        private void Stay(VisitorWeek result)
        {
            var overnight = result.Arrivals - result.DayTrippers;
            var nightsWanted = overnight * StayNights;
            foreach (var hotel in _hotels.Values.OrderByDescending(h => h.Stars).ThenBy(h => h.Id))
            {
                var sold = Math.Min(nightsWanted, (long)hotel.Beds * Ledger.DaysPerWeek);
                nightsWanted -= sold;
                result.NightsSold += sold;
                result.HotelRevenue += sold * hotel.NightlyRate;
                result.Spending += sold * hotel.SpendPerNight;
            }

            result.HotelGuests = result.NightsSold / StayNights;
            result.TurnedAway = overnight - result.HotelGuests;
            result.Spending += result.DayTrippers * DayTripSpend;
        }

        /// <summary>Elke dagjesmens en elke gast bezoekt één attractie per verblijf; de bezoeken volgen de aantrekkingskracht, begrensd door de weekcapaciteit.</summary>
        private void Visit(VisitorWeek result)
        {
            var sites = _sites.Values.ToArray();
            result.VisitsWanted = sites.Length == 0 ? 0 : result.DayTrippers + (result.HotelGuests * StayNights);
            var shares = Allocate(result.VisitsWanted, sites.Select(s => (long)s.Appeal).ToArray());
            for (var i = 0; i < sites.Length; i++)
            {
                var visits = Math.Min(shares[i], (long)sites[i].CapacityPerDay * Ledger.DaysPerWeek);
                result.SiteVisits += visits;
                result.Tickets += visits * sites[i].TicketPrice;
                result.Upkeep += sites[i].UpkeepPerWeek;
            }
        }

        /// <summary>Reputatie schuift richting de tevredenheid: aankomsten, bedden en attracties die voldoende bleken.</summary>
        private void UpdateReputation(VisitorWeek result)
        {
            var overnight = result.Arrivals - result.DayTrippers;
            var satisfaction = (Percent(result.Arrivals, result.Demand) + Percent(result.HotelGuests, overnight) + Percent(result.SiteVisits, result.VisitsWanted)) / 3;
            Reputation = (int)Math.Clamp(((Reputation * 3L) + satisfaction) / 4, 0, 100);
            result.Reputation = Reputation;
        }

        private static long Percent(long part, long whole) => whole <= 0 ? 100 : Math.Min(100, part * 100 / whole);

        /// <summary>Verdeelt <paramref name="total"/> geheel over de gewichten, zodat de delen precies optellen.</summary>
        private static long[] Allocate(long total, long[] weights)
        {
            var shares = new long[weights.Length];
            var sum = weights.Sum();
            if (sum <= 0 || total <= 0)
            {
                return shares;
            }

            var cumulative = 0L;
            var given = 0L;
            for (var i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                var upTo = total * cumulative / sum;
                shares[i] = upTo - given;
                given = upTo;
            }

            return shares;
        }
    }
}
