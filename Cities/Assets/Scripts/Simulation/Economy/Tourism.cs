using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Economy
{
    /// <summary>
    /// Een trekpleister (E11-09): een gebouw dat toeristen aantrekt en eventueel entree vraagt. Een unieke trekpleister
    /// (<see cref="IsUnique"/>, zoals een monument) kan maar één keer in de stad staan.
    /// </summary>
    public sealed class Attraction
    {
        public Attraction(int id, string name, int touristsPerDay, long ticketPrice, long upkeepPerWeek, bool isUnique)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een trekpleister heeft een naam nodig.", nameof(name));
            }

            if (touristsPerDay < 0 || ticketPrice < 0 || upkeepPerWeek < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(touristsPerDay), "Toeristen, entree en onderhoud kunnen niet negatief zijn.");
            }

            Id = id;
            Name = name;
            TouristsPerDay = touristsPerDay;
            TicketPrice = ticketPrice;
            UpkeepPerWeek = upkeepPerWeek;
            IsUnique = isUnique;
        }

        public int Id { get; }

        public string Name { get; }

        public int TouristsPerDay { get; }

        public long TicketPrice { get; }

        public long UpkeepPerWeek { get; }

        public bool IsUnique { get; }
    }

    /// <summary>Uitkomst van één week toerisme.</summary>
    public readonly struct TourismWeek
    {
        public TourismWeek(long tourists, long spending, long tickets, long upkeep)
        {
            Tourists = tourists;
            Spending = spending;
            Tickets = tickets;
            Upkeep = upkeep;
        }

        public long Tourists { get; }

        /// <summary>Wat toeristen in de stad uitgeven (horeca, winkels).</summary>
        public long Spending { get; }

        /// <summary>Entree bij de trekpleisters.</summary>
        public long Tickets { get; }

        public long Upkeep { get; }
    }

    /// <summary>
    /// Toerisme en entreegelden (E11-09). Trekpleisters trekken elk hun eigen toeristen; hotelbedden beperken hoeveel er per week
    /// kunnen komen (<see cref="StaysPerBedPerWeek"/> verblijven per bed). Bij een tekort aan bedden krimpt elke trekpleister even sterk mee.
    /// <see cref="Settle"/> boekt uitgaven en entree als inkomst en het onderhoud als verplichte uitgave in het <see cref="Ledger"/>.
    /// </summary>
    public sealed class Tourism
    {
        public const long SpendPerTourist = 40;
        public const int StaysPerBedPerWeek = 2;
        public const int MaxAttractions = 100_000;

        private readonly SortedDictionary<int, Attraction> _attractions = new SortedDictionary<int, Attraction>();

        public int NextId { get; private set; } = 1;

        public IEnumerable<Attraction> Attractions => _attractions.Values;

        /// <summary>Voegt een trekpleister toe; een tweede unieke met dezelfde naam is niet toegestaan.</summary>
        public int Add(string name, int touristsPerDay, long ticketPrice, long upkeepPerWeek, bool isUnique)
        {
            if (_attractions.Count >= MaxAttractions)
            {
                throw new InvalidOperationException("Te veel trekpleisters.");
            }

            var attraction = new Attraction(NextId, name, touristsPerDay, ticketPrice, upkeepPerWeek, isUnique);
            if (isUnique && _attractions.Values.Any(a => a.IsUnique && a.Name == name))
            {
                throw new InvalidOperationException($"{name} is uniek en staat al in de stad.");
            }

            _attractions[NextId] = attraction;
            return NextId++;
        }

        public bool Remove(int id) => _attractions.Remove(id);

        /// <summary>Boekt deze week voor <paramref name="beds"/> hotelbedden.</summary>
        public TourismWeek Settle(int week, Ledger ledger, int beds)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var demand = _attractions.Values.Sum(a => (long)a.TouristsPerDay) * Ledger.DaysPerWeek;
            var capacity = Math.Max(0L, beds) * (long)StaysPerBedPerWeek;
            var scale = demand > capacity ? (double)capacity / demand : 1.0;
            var tourists = (long)Math.Round(demand * scale);
            var tickets = _attractions.Values.Sum(a => (long)Math.Round(a.TouristsPerDay * Ledger.DaysPerWeek * scale) * a.TicketPrice);
            var upkeep = _attractions.Values.Sum(a => a.UpkeepPerWeek);
            var result = new TourismWeek(tourists, tourists * SpendPerTourist, tickets, upkeep);
            ledger.Earn(week, LedgerCategory.Other, result.Spending + result.Tickets);
            ledger.Force(week, LedgerCategory.Upkeep, upkeep);
            return result;
        }

        internal void Restore(IEnumerable<Attraction> attractions, int nextId)
        {
            _attractions.Clear();
            foreach (var attraction in attractions)
            {
                _attractions[attraction.Id] = attraction;
            }

            NextId = nextId;
        }
    }
}
