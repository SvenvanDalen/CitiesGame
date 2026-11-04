using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Economy;
using Cities.Simulation.Timing;
using Cities.Simulation.Visitors;

namespace Cities.Simulation.Events
{
    /// <summary>
    /// Evenementen (E10-12): stadions, concertzalen en markten plannen evenementen op een datum en beginuur van de spelkalender.
    /// Het verwachte publiek (<see cref="Forecast"/>) bestaat uit inwoners (promille van de bevolking, naar aantrekkingskracht) en gasten
    /// van buiten (aandeel van de capaciteit). Is het te druk, dan krimpen beide evenredig; gasten van buiten worden daarna begrensd door wat
    /// de buitenverbindingen (<see cref="IOutsideConnections"/> uit het bezoekersmodel) per dag overhouden. <see cref="Settle"/> boekt kaartjes
    /// en belasting als inkomst en de organisatiekosten als verplichte uitgave in het <see cref="Ledger"/>. <see cref="DemandOn"/> levert
    /// de reisvraag voor het verkeer. Alles is geheel getal en dus deterministisch.
    /// </summary>
    public sealed class EventPlanner : IEventDemandSource
    {
        public const int MaxItems = 100_000;

        private readonly SortedDictionary<int, EventVenue> _venues = new SortedDictionary<int, EventVenue>();
        private readonly SortedDictionary<int, PlannedEvent> _events = new SortedDictionary<int, PlannedEvent>();
        private readonly Func<int> _population;
        private readonly IOutsideConnections _connections;

        public EventPlanner(Func<int> population, IOutsideConnections connections)
        {
            _population = population ?? throw new ArgumentNullException(nameof(population));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        public int NextVenueId { get; private set; } = 1;

        public int NextEventId { get; private set; } = 1;

        public IEnumerable<EventVenue> Venues => _venues.Values;

        public IEnumerable<PlannedEvent> Events => _events.Values;

        public int AddVenue(string name, EventVenueKind kind, int capacity, long ticketPrice, long hostingCost)
        {
            if (_venues.Count >= MaxItems)
            {
                throw new InvalidOperationException("Te veel locaties.");
            }

            _venues[NextVenueId] = new EventVenue(NextVenueId, name, kind, capacity, ticketPrice, hostingCost);
            return NextVenueId++;
        }

        /// <summary>Verwijdert een locatie met haar geplande evenementen.</summary>
        public bool RemoveVenue(int id)
        {
            if (!_venues.Remove(id))
            {
                return false;
            }

            foreach (var key in _events.Values.Where(e => e.VenueId == id).Select(e => e.Id).ToArray())
            {
                _events.Remove(key);
            }

            return true;
        }

        /// <summary>Plant een evenement; per locatie kan er één per dag, en niet in het verleden.</summary>
        public int Plan(int venueId, string name, GameDate date, int startHour, int appeal, GameDate today)
        {
            if (!_venues.ContainsKey(venueId))
            {
                throw new InvalidOperationException("Onbekende locatie.");
            }

            if (_events.Count >= MaxItems)
            {
                throw new InvalidOperationException("Te veel evenementen.");
            }

            var planned = new PlannedEvent(NextEventId, venueId, name, date, startHour, appeal);
            if (date < today)
            {
                throw new InvalidOperationException("Een evenement kan niet in het verleden.");
            }

            if (_events.Values.Any(e => e.VenueId == venueId && e.Date == date))
            {
                throw new InvalidOperationException("De locatie heeft die dag al een evenement.");
            }

            _events[NextEventId] = planned;
            return NextEventId++;
        }

        public bool Cancel(int eventId) => _events.Remove(eventId);

        /// <summary>Evenementen op een datum, op beginuur en dan volgnummer.</summary>
        public IEnumerable<PlannedEvent> EventsOn(GameDate date) =>
            _events.Values.Where(e => e.Date == date).OrderBy(e => e.StartHour).ThenBy(e => e.Id);

        /// <summary>Gasten van buiten die de buitenverbindingen per dag in totaal kunnen aanvoeren.</summary>
        public long DailyOutsideCapacity()
        {
            long weekly = 0;
            for (var i = 0; i < VisitorProfiles.ChannelCount; i++)
            {
                weekly += Math.Max(0, _connections.WeeklyCapacity((VisitorChannel)i));
            }

            return weekly / Ledger.DaysPerWeek;
        }

        /// <summary>
        /// Verwacht publiek van een evenement. <paramref name="outsideRoom"/> is wat de buitenverbindingen nog over hebben; het
        /// publiek van buiten past daar binnen.
        /// </summary>
        public EventTrafficDemand Forecast(PlannedEvent planned, long outsideRoom)
        {
            if (planned == null)
            {
                throw new ArgumentNullException(nameof(planned));
            }

            if (!_venues.TryGetValue(planned.VenueId, out var venue))
            {
                throw new InvalidOperationException("Onbekende locatie.");
            }

            var localWant = Math.Max(0, _population()) * (long)EventProfiles.LocalPermille(venue.Kind) * planned.Appeal / 100_000;
            var outsideWant = venue.Capacity * (long)EventProfiles.OutsidePercent(venue.Kind) * planned.Appeal / 10_000;
            var want = localWant + outsideWant;
            var local = localWant;
            var outside = outsideWant;
            if (want > venue.Capacity)
            {
                local = venue.Capacity * localWant / want;
                outside = venue.Capacity - local;
            }

            outside = Math.Min(outside, Math.Max(0, outsideRoom));
            return new EventTrafficDemand(planned.Id, venue.Id, planned.Date, planned.StartHour, local, outside);
        }

        /// <summary>De reisvraag van een dag, voor het verkeer; evenementen delen de buitenverbindingen in volgorde van beginuur.</summary>
        public IReadOnlyList<EventTrafficDemand> DemandOn(GameDate date)
        {
            var room = DailyOutsideCapacity();
            var result = new List<EventTrafficDemand>();
            foreach (var planned in EventsOn(date))
            {
                var demand = Forecast(planned, room);
                room -= demand.Outside;
                result.Add(demand);
            }

            return result;
        }

        /// <summary>Houdt alle evenementen tot en met <paramref name="today"/> en boekt de opbrengst in <paramref name="week"/>.</summary>
        public IReadOnlyList<EventOutcome> Settle(GameDate today, int week, Ledger ledger)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var due = _events.Values.Where(e => e.Date <= today).OrderBy(e => e.Date).ThenBy(e => e.StartHour).ThenBy(e => e.Id).ToArray();
            var outcomes = new List<EventOutcome>();
            var day = default(GameDate);
            var room = 0L;
            for (var i = 0; i < due.Length; i++)
            {
                if (i == 0 || due[i].Date != day)
                {
                    day = due[i].Date;
                    room = DailyOutsideCapacity();
                }

                var outcome = Hold(due[i], room);
                room -= outcome.Outside;
                outcomes.Add(outcome);
                _events.Remove(due[i].Id);
                ledger.Earn(week, LedgerCategory.Other, outcome.Tickets);
                ledger.Earn(week, LedgerCategory.Taxes, outcome.Taxes);
                ledger.Force(week, LedgerCategory.Services, outcome.HostingCost);
            }

            return outcomes;
        }

        internal void Restore(IEnumerable<EventVenue> venues, IEnumerable<PlannedEvent> events, int nextVenueId, int nextEventId)
        {
            _venues.Clear();
            foreach (var venue in venues)
            {
                _venues[venue.Id] = venue;
            }

            _events.Clear();
            foreach (var planned in events)
            {
                _events[planned.Id] = planned;
            }

            NextVenueId = nextVenueId;
            NextEventId = nextEventId;
        }

        private EventOutcome Hold(PlannedEvent planned, long room)
        {
            var venue = _venues[planned.VenueId];
            var demand = Forecast(planned, room);
            var wanted = Forecast(planned, long.MaxValue);
            var spending = (demand.Local * EventProfiles.LocalSpend(venue.Kind)) + (demand.Outside * EventProfiles.OutsideSpend(venue.Kind));
            return new EventOutcome
            {
                EventId = planned.Id,
                VenueId = venue.Id,
                Date = planned.Date,
                Local = demand.Local,
                Outside = demand.Outside,
                TurnedAway = wanted.Outside - demand.Outside,
                Tickets = demand.Total * venue.TicketPrice,
                Spending = spending,
                Taxes = spending * VisitorEconomy.SpendingTaxPercent / 100,
                HostingCost = venue.HostingCost,
            };
        }
    }
}
