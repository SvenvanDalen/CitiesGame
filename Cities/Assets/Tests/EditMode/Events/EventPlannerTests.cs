using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Simulation.Events;
using Cities.Simulation.Timing;
using Cities.Simulation.Visitors;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Events
{
    public class EventPlannerTests
    {
        private static readonly GameDate Today = new GameDate(2026, 11, 4);

        [Test]
        public void ForecastSplitsLocalAndOutsideAudience()
        {
            var planner = Planner(10000, 7000, out var stadium, out _);
            var id = planner.Plan(stadium, "Derby", Today.AddDays(3), 20, 50, Today);

            var demand = planner.Forecast(planner.Events.Single(e => e.Id == id), long.MaxValue);

            Assert.That(demand.Local, Is.EqualTo(150));
            Assert.That(demand.Outside, Is.EqualTo(1250));
            Assert.That(demand.Total, Is.EqualTo(1400));
            Assert.That(demand.StartHour, Is.EqualTo(20));
            Assert.That(demand.Date, Is.EqualTo(Today.AddDays(3)));
        }

        [Test]
        public void ASoldOutVenueShrinksBothGroupsAndOutsideRoomLimitsGuests()
        {
            var planner = Planner(10000, 7000, out _, out _);
            var small = planner.AddVenue("Kleine hal", EventVenueKind.ConcertHall, 100, 5, 0);
            planner.Plan(small, "Festival", Today, 12, 100, Today);
            var planned = planner.Events.Single();

            var full = planner.Forecast(planned, long.MaxValue);
            var limited = planner.Forecast(planned, 10);
            var none = planner.Forecast(planned, -5);

            Assert.That(full.Total, Is.EqualTo(100));
            Assert.That(full.Local, Is.EqualTo(83));
            Assert.That(full.Outside, Is.EqualTo(17));
            Assert.That(limited.Outside, Is.EqualTo(10));
            Assert.That(none.Outside, Is.EqualTo(0));
            Assert.That(none.Local, Is.EqualTo(83));
        }

        [Test]
        public void SettleBooksTicketsTaxesAndHostingCostsInTheLedger()
        {
            var planner = Planner(10000, 7000, out var stadium, out _);
            planner.Plan(stadium, "Derby", Today, 20, 50, Today);
            var future = planner.Plan(stadium, "Later", Today.AddDays(1), 20, 50, Today);
            var ledger = new Ledger(0);

            var outcomes = planner.Settle(Today, 5, ledger);

            var outcome = outcomes.Single();
            Assert.That(outcome.Local, Is.EqualTo(150));
            Assert.That(outcome.Outside, Is.EqualTo(1000));
            Assert.That(outcome.Attendance, Is.EqualTo(1150));
            Assert.That(outcome.TurnedAway, Is.EqualTo(250));
            Assert.That(outcome.Tickets, Is.EqualTo(11500));
            Assert.That(outcome.Spending, Is.EqualTo((150 * 20) + (1000 * 40)));
            Assert.That(outcome.Taxes, Is.EqualTo(outcome.Spending * VisitorEconomy.SpendingTaxPercent / 100));
            Assert.That(ledger.Income(5, LedgerCategory.Other), Is.EqualTo(11500));
            Assert.That(ledger.Income(5, LedgerCategory.Taxes), Is.EqualTo(outcome.Taxes));
            Assert.That(ledger.Expenses(5, LedgerCategory.Services), Is.EqualTo(500));
            Assert.That(planner.Events.Select(e => e.Id).ToArray(), Is.EqualTo(new[] { future }));
            Assert.That(planner.Settle(Today, 5, ledger), Is.Empty);
        }

        [Test]
        public void EventsOnTheSameDayShareTheOutsideConnectionsAndEachDayStartsFresh()
        {
            var planner = Planner(10000, 7000, out var stadium, out var hall);
            planner.Plan(stadium, "Derby", Today, 20, 50, Today);
            planner.Plan(hall, "Concert", Today, 14, 100, Today);

            var demand = planner.DemandOn(Today);

            Assert.That(demand.Select(d => d.StartHour).ToArray(), Is.EqualTo(new[] { 14, 20 }));
            Assert.That(demand[0].Outside, Is.EqualTo(400));
            Assert.That(demand[1].Outside, Is.EqualTo(600));
            Assert.That(planner.DemandOn(Today.AddDays(1)), Is.Empty);

            planner.Plan(stadium, "Morgen", Today.AddDays(1), 20, 50, Today);
            var outcomes = planner.Settle(Today.AddDays(1), 0, new Ledger(0));
            Assert.That(outcomes.Select(o => o.Outside).ToArray(), Is.EqualTo(new[] { 400L, 600L, 1000L }));
        }

        [Test]
        public void NoConnectionsMeansOnlyLocalAudience()
        {
            var planner = Planner(10000, 0, out var stadium, out _);
            planner.Plan(stadium, "Derby", Today, 20, 50, Today);

            var demand = planner.DemandOn(Today).Single();

            Assert.That(demand.Outside, Is.EqualTo(0));
            Assert.That(demand.Local, Is.EqualTo(150));
        }

        [Test]
        public void PlanningIsValidated()
        {
            var planner = Planner(10000, 7000, out var stadium, out _);
            planner.Plan(stadium, "Derby", Today, 20, 50, Today);

            Assert.Throws<InvalidOperationException>(() => planner.Plan(99, "X", Today, 1, 5, Today));
            Assert.Throws<InvalidOperationException>(() => planner.Plan(stadium, "Dubbel", Today, 1, 5, Today));
            Assert.Throws<InvalidOperationException>(() => planner.Plan(stadium, "Verleden", Today.AddDays(-1), 1, 5, Today));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(stadium, "Uur", Today.AddDays(1), 24, 5, Today));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(stadium, "Kracht", Today.AddDays(1), 1, 0, Today));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(stadium, "Datum", new GameDate(2026, 2, 30), 1, 5, Today));
            Assert.Throws<ArgumentException>(() => planner.Plan(stadium, " ", Today.AddDays(1), 1, 5, Today));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.AddVenue("Zaal", EventVenueKind.Market, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.AddVenue("Zaal", EventVenueKind.Market, 5, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.AddVenue("Zaal", (EventVenueKind)9, 5, 1, 0));
            Assert.Throws<ArgumentException>(() => planner.AddVenue("", EventVenueKind.Market, 5, 1, 0));
            Assert.Throws<ArgumentNullException>(() => new EventPlanner(null, new FixedOutsideConnections()));
            Assert.Throws<ArgumentNullException>(() => new EventPlanner(() => 1, null));
            Assert.Throws<ArgumentNullException>(() => planner.Settle(Today, 0, null));
            Assert.Throws<ArgumentNullException>(() => planner.Forecast(null, 0));
        }

        [Test]
        public void CancelAndRemoveVenueDropEvents()
        {
            var planner = Planner(10000, 7000, out var stadium, out var hall);
            var a = planner.Plan(stadium, "A", Today, 10, 50, Today);
            planner.Plan(hall, "B", Today, 11, 50, Today);
            planner.Plan(hall, "C", Today.AddDays(1), 11, 50, Today);

            Assert.That(planner.Cancel(a), Is.True);
            Assert.That(planner.Cancel(a), Is.False);
            Assert.That(planner.RemoveVenue(hall), Is.True);
            Assert.That(planner.RemoveVenue(hall), Is.False);

            Assert.That(planner.Events, Is.Empty);
            Assert.That(planner.Venues.Single().Id, Is.EqualTo(stadium));
        }

        [Test]
        public void MarketsAreMostlyLocalAndStadiumsDrawMoreGuests()
        {
            var planner = Planner(10000, 70000, out var stadium, out _);
            var market = planner.AddVenue("Markt", EventVenueKind.Market, 5000, 0, 0);
            planner.Plan(stadium, "Wedstrijd", Today, 15, 100, Today);
            planner.Plan(market, "Weekmarkt", Today, 9, 100, Today);

            var demand = planner.DemandOn(Today);

            Assert.That(demand[0].Local, Is.EqualTo(600));
            Assert.That(demand[0].Outside, Is.EqualTo(500));
            Assert.That(demand[1].Local, Is.EqualTo(300));
            Assert.That(demand[1].Outside, Is.EqualTo(2500));
            Assert.That(EventProfiles.OutsideSpend(EventVenueKind.Market), Is.GreaterThan(EventProfiles.LocalSpend(EventVenueKind.Market)));
        }

        [Test]
        public void SaveRoundTripKeepsVenuesAndEvents()
        {
            var planner = Planner(10000, 7000, out var stadium, out var hall);
            planner.RemoveVenue(planner.AddVenue("Weg", EventVenueKind.Market, 1, 0, 0));
            planner.Plan(stadium, "Derby", Today.AddDays(2), 20, 50, Today);
            planner.Plan(hall, "Concert", Today, 14, 100, Today);
            var bytes = SaveBytes.Write(new SaveMetadata(), new EventSection(planner));
            var loaded = Planner(1, 0, out _, out _);

            SaveBytes.Read(bytes, new EventSection(loaded));

            Assert.That(loaded.Venues.Select(v => v.Name).ToArray(), Is.EqualTo(new[] { "Stadion", "Hal" }));
            Assert.That(loaded.Events.Select(e => e.Name).ToArray(), Is.EqualTo(new[] { "Derby", "Concert" }));
            Assert.That(loaded.Events.First().Date, Is.EqualTo(Today.AddDays(2)));
            Assert.That(loaded.NextVenueId, Is.EqualTo(planner.NextVenueId));
            Assert.That(loaded.NextEventId, Is.EqualTo(planner.NextEventId));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new EventSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new EventSection(planner).Id, Is.EqualTo("EVNT"));
            Assert.Throws<ArgumentNullException>(() => new EventSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var planner = Planner(10000, 7000, out _, out _);
            var section = new EventSection(planner);

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Counts(w, 1, -1, 1, 0)), 1), "aantal locaties");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Counts(w, 1, 0, 1, -1)), 1), "aantal evenementen");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Venue(w, 1, 1, 100)), 1), "volgnummer locatie");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Venue(w, 2, 1, 0)), 1), "capaciteit");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => VenueWithEvent(w, 2, 1, 1, 1, 20, 5)), 1), "volgnummer evenement");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => VenueWithEvent(w, 2, 2, 7, 1, 20, 5)), 1), "locatie onbekend");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => VenueWithEvent(w, 2, 2, 1, 1, 25, 5)), 1), "beginuur");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => VenueWithEvent(w, 2, 3, 1, 1, 20, 5, 2)), 1), "twee op een dag");
            Assert.That(planner.Venues.Count(), Is.EqualTo(2));
        }

        private static EventPlanner Planner(int population, int roadWeekly, out int stadium, out int hall)
        {
            var planner = new EventPlanner(() => population, new FixedOutsideConnections().Set(VisitorChannel.Road, roadWeekly));
            stadium = planner.AddVenue("Stadion", EventVenueKind.Stadium, 5000, 10, 500);
            hall = planner.AddVenue("Hal", EventVenueKind.ConcertHall, 1000, 30, 200);
            return planner;
        }

        private static void Counts(SaveWriter w, int nextVenue, int venues, int nextEvent, int events)
        {
            w.Write(nextVenue);
            w.Write(venues);
            if (venues == 0)
            {
                w.Write(nextEvent);
                w.Write(events);
            }
        }

        private static void WriteVenue(SaveWriter w, int id, int capacity)
        {
            w.Write(id);
            w.Write("Stadion");
            w.Write(0);
            w.Write(capacity);
            w.Write(10L);
            w.Write(5L);
        }

        private static void Venue(SaveWriter w, int nextVenue, int id, int capacity)
        {
            w.Write(nextVenue);
            w.Write(1);
            WriteVenue(w, id, capacity);
            w.Write(1);
            w.Write(0);
        }

        private static void VenueWithEvent(SaveWriter w, int nextVenue, int nextEvent, int venueId, int eventId, int hour, int appeal, int count = 1)
        {
            w.Write(nextVenue);
            w.Write(1);
            WriteVenue(w, 1, 100);
            w.Write(nextEvent);
            w.Write(count);
            for (var i = 0; i < count; i++)
            {
                w.Write(eventId + i);
                w.Write(venueId);
                w.Write("Wedstrijd");
                w.Write(2026);
                w.Write(11);
                w.Write(4);
                w.Write(hour);
                w.Write(appeal);
            }
        }
    }
}
