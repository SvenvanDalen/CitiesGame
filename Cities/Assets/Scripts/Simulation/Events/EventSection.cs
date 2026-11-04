using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Timing;

namespace Cities.Simulation.Events
{
    /// <summary>Sectie <c>EVNT</c>: locaties en geplande evenementen (E10-12).</summary>
    public sealed class EventSection : ISaveSection
    {
        public const string SectionId = "EVNT";

        private readonly EventPlanner _planner;

        public EventSection(EventPlanner planner)
        {
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_planner.NextVenueId);
            var venues = _planner.Venues.ToArray();
            writer.Write(venues.Length);
            foreach (var v in venues)
            {
                writer.Write(v.Id);
                writer.Write(v.Name);
                writer.Write((int)v.Kind);
                writer.Write(v.Capacity);
                writer.Write(v.TicketPrice);
                writer.Write(v.HostingCost);
            }

            writer.Write(_planner.NextEventId);
            var events = _planner.Events.ToArray();
            writer.Write(events.Length);
            foreach (var e in events)
            {
                writer.Write(e.Id);
                writer.Write(e.VenueId);
                writer.Write(e.Name);
                writer.Write(e.Date.Year);
                writer.Write(e.Date.Month);
                writer.Write(e.Date.Day);
                writer.Write(e.StartHour);
                writer.Write(e.Appeal);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextVenueId = reader.ReadInt32();
            var venues = new List<EventVenue>();
            var venueCount = ReadCount(reader, "locaties");
            for (var i = 0; i < venueCount; i++)
            {
                venues.Add(ReadVenue(reader));
            }

            var nextEventId = reader.ReadInt32();
            var events = new List<PlannedEvent>();
            var eventCount = ReadCount(reader, "evenementen");
            for (var i = 0; i < eventCount; i++)
            {
                events.Add(ReadEvent(reader));
            }

            reader.ExpectEnd();
            if (venues.Any(v => v.Id >= nextVenueId) || venues.Select(v => v.Id).Distinct().Count() != venueCount)
            {
                throw reader.Corrupt("locatievolgnummer te laag of dubbel ID");
            }

            if (events.Any(e => e.Id >= nextEventId) || events.Select(e => e.Id).Distinct().Count() != eventCount)
            {
                throw reader.Corrupt("evenementvolgnummer te laag of dubbel ID");
            }

            if (events.Any(e => venues.All(v => v.Id != e.VenueId)))
            {
                throw reader.Corrupt("evenement zonder locatie");
            }

            if (events.GroupBy(e => (e.VenueId, e.Date)).Any(g => g.Count() > 1))
            {
                throw reader.Corrupt("twee evenementen op één locatie en dag");
            }

            return () => _planner.Restore(venues, events, nextVenueId, nextEventId);
        }

        private static EventVenue ReadVenue(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var name = reader.ReadString();
            var kind = (EventVenueKind)reader.ReadInt32();
            var capacity = reader.ReadInt32();
            var ticket = reader.ReadInt64();
            var hosting = reader.ReadInt64();
            try
            {
                return new EventVenue(id, name, kind, capacity, ticket, hosting);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }

        private static PlannedEvent ReadEvent(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var venueId = reader.ReadInt32();
            var name = reader.ReadString();
            var year = reader.ReadInt32();
            var month = reader.ReadInt32();
            var day = reader.ReadInt32();
            var hour = reader.ReadInt32();
            var appeal = reader.ReadInt32();
            try
            {
                return new PlannedEvent(id, venueId, name, new GameDate(year, month, day), hour, appeal);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }

        private static int ReadCount(SaveReader reader, string what)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > EventPlanner.MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal {what}: {count}");
            }

            return count;
        }
    }
}
