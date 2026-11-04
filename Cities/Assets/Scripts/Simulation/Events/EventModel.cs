using System;
using System.Collections.Generic;
using Cities.Simulation.Timing;

namespace Cities.Simulation.Events
{
    /// <summary>Soort evenementenlocatie (E10-12); bepaalt hoe lokaal of buitenlands het publiek is en wat men besteedt.</summary>
    public enum EventVenueKind
    {
        Stadium = 0,
        ConcertHall = 1,
        Market = 2,
    }

    /// <summary>Kengetallen per soort locatie. Alles in gehele getallen.</summary>
    public static class EventProfiles
    {
        public const int KindCount = 3;

        /// <summary>Promille van de inwoners dat bij volle aantrekkingskracht (100) komt.</summary>
        public static int LocalPermille(EventVenueKind kind)
        {
            switch (kind)
            {
                case EventVenueKind.Stadium:
                    return 30;
                case EventVenueKind.ConcertHall:
                    return 20;
                default:
                    return 60;
            }
        }

        /// <summary>Publiek van buiten als percentage van de capaciteit bij volle aantrekkingskracht.</summary>
        public static int OutsidePercent(EventVenueKind kind)
        {
            switch (kind)
            {
                case EventVenueKind.Stadium:
                    return 50;
                case EventVenueKind.ConcertHall:
                    return 40;
                default:
                    return 10;
            }
        }

        /// <summary>Besteding van een inwoner buiten het kaartje (horeca, souvenirs).</summary>
        public static long LocalSpend(EventVenueKind kind)
        {
            switch (kind)
            {
                case EventVenueKind.Stadium:
                    return 20;
                case EventVenueKind.ConcertHall:
                    return 25;
                default:
                    return 12;
            }
        }

        /// <summary>Gasten van buiten geven meer uit: vervoer, eten en vaak een extra drankje.</summary>
        public static long OutsideSpend(EventVenueKind kind) => LocalSpend(kind) * 2;
    }

    /// <summary>Een locatie die evenementen kan houden (stadion, concertzaal, markt).</summary>
    public sealed class EventVenue
    {
        public EventVenue(int id, string name, EventVenueKind kind, int capacity, long ticketPrice, long hostingCost)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een locatie heeft een naam nodig.", nameof(name));
            }

            if (kind < 0 || (int)kind >= EventProfiles.KindCount)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Onbekende soort locatie.");
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Een locatie heeft minstens één plek.");
            }

            if (ticketPrice < 0 || hostingCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticketPrice), "Kaartprijs en kosten kunnen niet negatief zijn.");
            }

            Id = id;
            Name = name;
            Kind = kind;
            Capacity = capacity;
            TicketPrice = ticketPrice;
            HostingCost = hostingCost;
        }

        public int Id { get; }

        public string Name { get; }

        public EventVenueKind Kind { get; }

        public int Capacity { get; }

        public long TicketPrice { get; }

        /// <summary>Kosten voor de stad per evenement (beveiliging, schoonmaak).</summary>
        public long HostingCost { get; }
    }

    /// <summary>Een gepland evenement op een datum en beginuur in de spelkalender.</summary>
    public sealed class PlannedEvent
    {
        public const int MaxAppeal = 100;

        public PlannedEvent(int id, int venueId, string name, GameDate date, int startHour, int appeal)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een evenement heeft een naam nodig.", nameof(name));
            }

            if (startHour < 0 || startHour > 23)
            {
                throw new ArgumentOutOfRangeException(nameof(startHour), "Het beginuur ligt tussen 0 en 23.");
            }

            if (appeal < 1 || appeal > MaxAppeal)
            {
                throw new ArgumentOutOfRangeException(nameof(appeal), "Aantrekkingskracht ligt tussen 1 en 100.");
            }

            if (date.Year < 1 || date.Year > 9999 || date.Month < 1 || date.Month > 12 || date.Day < 1 || date.Day > DateTime.DaysInMonth(date.Year, date.Month))
            {
                throw new ArgumentOutOfRangeException(nameof(date), "Ongeldige datum.");
            }

            Id = id;
            VenueId = venueId;
            Name = name;
            Date = date;
            StartHour = startHour;
            Appeal = appeal;
        }

        public int Id { get; }

        public int VenueId { get; }

        public string Name { get; }

        public GameDate Date { get; }

        public int StartHour { get; }

        public int Appeal { get; }
    }

    /// <summary>
    /// Verwacht publiek van een evenement, gesplitst in inwoners en gasten van buiten. Dit is ook de uitvoer voor het verkeer
    /// (E08-17): hoeveel mensen rond <see cref="StartHour"/> naar de locatie reizen en hoeveel daarvan via de buitenverbindingen komen.
    /// </summary>
    public readonly struct EventTrafficDemand
    {
        public EventTrafficDemand(int eventId, int venueId, GameDate date, int startHour, long local, long outside)
        {
            EventId = eventId;
            VenueId = venueId;
            Date = date;
            StartHour = startHour;
            Local = local;
            Outside = outside;
        }

        public int EventId { get; }

        public int VenueId { get; }

        public GameDate Date { get; }

        public int StartHour { get; }

        public long Local { get; }

        public long Outside { get; }

        public long Total => Local + Outside;
    }

    /// <summary>Bron van evenementenvraag voor andere systemen (verkeer): wie komt er op een dag naar welke locatie.</summary>
    public interface IEventDemandSource
    {
        IReadOnlyList<EventTrafficDemand> DemandOn(GameDate date);
    }

    /// <summary>Uitkomst van één gehouden evenement.</summary>
    public sealed class EventOutcome
    {
        public int EventId { get; internal set; }

        public int VenueId { get; internal set; }

        public GameDate Date { get; internal set; }

        public long Local { get; internal set; }

        public long Outside { get; internal set; }

        /// <summary>Gasten van buiten die door de buitenverbindingen niet konden komen.</summary>
        public long TurnedAway { get; internal set; }

        public long Tickets { get; internal set; }

        /// <summary>Uitgaven buiten het kaartje, in de stad.</summary>
        public long Spending { get; internal set; }

        public long Taxes { get; internal set; }

        public long HostingCost { get; internal set; }

        public long Attendance => Local + Outside;
    }
}
