using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Events;
using Cities.Simulation.Timing;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Events
{
    public class EventTrafficTests
    {
        private static readonly GameDate Today = new GameDate(2026, 11, 4);

        private sealed class FakeSource : IEventDemandSource
        {
            public IReadOnlyList<EventTrafficDemand> DemandOn(GameDate date) =>
                date == Today ? new[] { Demand(1000, 2000), Demand(0, 0, 2) } : new EventTrafficDemand[0];
        }

        private static EventTrafficDemand Demand(long local, long outside, int venue = 1) =>
            new EventTrafficDemand(1, venue, Today, 20, local, outside);

        [Test]
        public void CurvesConserveVisitorsAndArrivalsPrecedeDepartures()
        {
            var report = EventTrafficModel.Analyze(Demand(1001, 2003), default, 500);

            Assert.That(report.Hours.Where(h => h.Arriving).Sum(h => h.Persons), Is.EqualTo(3004));
            Assert.That(report.Hours.Where(h => !h.Arriving).Sum(h => h.Persons), Is.EqualTo(3004));
            Assert.That(report.Hours.Select(h => h.HourOffset).ToArray(), Is.EqualTo(new[] { -3, -2, -1, 3, 4, 5 }));
            Assert.That(report.Hours[2].Persons, Is.GreaterThan(report.Hours[0].Persons));
        }

        [Test]
        public void ModeChoiceSplitsCarTransitAndWalking()
        {
            var report = EventTrafficModel.Analyze(Demand(1000, 1000), default, 5000);
            var last = report.Hours[2];

            Assert.That(last.Persons, Is.EqualTo(1000));
            Assert.That(last.Transit, Is.EqualTo(100 + 175));
            Assert.That(last.Active, Is.EqualTo(75));
            Assert.That(last.Cars, Is.EqualTo(650));
            Assert.That(last.Vehicles, Is.EqualTo(260));
        }

        [Test]
        public void ExtraBusesMoveCarTravellersToTransit()
        {
            var without = EventTrafficModel.Analyze(Demand(1000, 1000), default, 5000);
            var with = EventTrafficModel.Analyze(Demand(1000, 1000), new EventTrafficSettings(4, 0, false), 5000);
            var huge = EventTrafficModel.Analyze(Demand(100, 0), new EventTrafficSettings(50, 0, false), 5000);

            Assert.That(with.PeakVehiclesPerHour, Is.LessThan(without.PeakVehiclesPerHour));
            Assert.That(with.PeakTransitPerHour, Is.EqualTo(without.PeakTransitPerHour + 200));
            Assert.That(huge.Hours.All(h => h.Cars == 0), Is.True);
        }

        [Test]
        public void ParkingPressureAndOverflowFollowFreeSpots()
        {
            var roomy = EventTrafficModel.Analyze(Demand(1000, 1000), default, 10000);
            var tight = EventTrafficModel.Analyze(Demand(1000, 1000), default, 100);
            var none = EventTrafficModel.Analyze(Demand(1000, 1000), default, -3);
            var empty = EventTrafficModel.Analyze(Demand(0, 0), default, 0);

            Assert.That(roomy.ParkingOverflow, Is.EqualTo(0));
            Assert.That(roomy.ParkingPressurePercent, Is.LessThan(100));
            Assert.That(tight.ParkingOverflow, Is.EqualTo(tight.ParkingDemand - 100));
            Assert.That(tight.ParkingPressurePercent, Is.GreaterThan(100));
            Assert.That(none.FreeParking, Is.EqualTo(0));
            Assert.That(none.ParkingPressurePercent, Is.EqualTo(1000));
            Assert.That(empty.ParkingPressurePercent, Is.EqualTo(0));
            Assert.That(empty.Hours.All(h => h.Persons == 0), Is.True);
        }

        [Test]
        public void ClosuresReduceAndOneWayRaisesAccessCapacity()
        {
            var normal = EventTrafficModel.AccessCapacity(default);
            var closed = EventTrafficModel.AccessCapacity(new EventTrafficSettings(0, 2, false));
            var oneWay = EventTrafficModel.AccessCapacity(new EventTrafficSettings(0, 0, true));

            Assert.That(normal, Is.EqualTo(EventTrafficModel.BaseAccessCapacity));
            Assert.That(closed, Is.LessThan(normal));
            Assert.That(oneWay, Is.GreaterThan(normal));

            var crowded = EventTrafficModel.Analyze(Demand(20000, 20000), new EventTrafficSettings(0, 2, false), 100000);
            Assert.That(crowded.AccessLoadPercent, Is.GreaterThan(100));
            Assert.That(crowded.AccessCapacityPerHour, Is.EqualTo(closed));
        }

        [Test]
        public void SettingsValidateRanges()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventTrafficSettings(-1, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventTrafficSettings(EventTrafficSettings.MaxBuses + 1, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventTrafficSettings(0, -1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventTrafficSettings(0, EventTrafficSettings.MaxClosedLanes + 1, false));
        }

        [Test]
        public void PlannerBuildsReportsWithPerVenueSettings()
        {
            var planner = new EventTrafficPlanner();
            planner.Set(1, new EventTrafficSettings(2, 1, true));

            var reports = planner.ReportsOn(new FakeSource(), Today, venue => venue == 1 ? 300 : 7);

            Assert.That(reports, Has.Count.EqualTo(2));
            Assert.That(reports[0].Settings.ExtraBuses, Is.EqualTo(2));
            Assert.That(reports[0].FreeParking, Is.EqualTo(300));
            Assert.That(reports[1].Settings.OneWay, Is.False);
            Assert.That(planner.ReportsOn(new FakeSource(), Today.AddDays(1), venue => 0), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => planner.ReportsOn(null, Today, venue => 0));
            Assert.Throws<ArgumentNullException>(() => planner.ReportsOn(new FakeSource(), Today, null));
        }

        [Test]
        public void SaveRoundTripKeepsSettings()
        {
            var planner = new EventTrafficPlanner();
            planner.Set(5, new EventTrafficSettings(3, 2, true));
            planner.Set(2, new EventTrafficSettings(1, 0, false));
            var bytes = SaveBytes.Write(new SaveMetadata(), new EventTrafficSection(planner));
            var loaded = new EventTrafficPlanner();
            loaded.Set(9, default);

            SaveBytes.Read(bytes, new EventTrafficSection(loaded));

            Assert.That(loaded.AllSettings.Keys.ToArray(), Is.EqualTo(new[] { 2, 5 }));
            Assert.That(loaded.For(5).ClosedLanes, Is.EqualTo(2));
            Assert.That(loaded.For(5).OneWay, Is.True);
            Assert.That(loaded.For(77).ExtraBuses, Is.EqualTo(0));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new EventTrafficSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new EventTrafficSection(planner).Id, Is.EqualTo("EVTR"));
            Assert.Throws<ArgumentNullException>(() => new EventTrafficSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            var section = new EventTrafficSection(new EventTrafficPlanner());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(1);
                w.Write(1);
                w.Write(99);
                w.Write(0);
                w.Write(false);
            }), 1));
        }
    }
}
