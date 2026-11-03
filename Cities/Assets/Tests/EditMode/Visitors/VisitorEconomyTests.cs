using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Simulation.Visitors;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Visitors
{
    public class VisitorEconomyTests
    {
        [Test]
        public void VisitorsArriveStayInHotelsVisitSitesAndSpend()
        {
            var economy = new VisitorEconomy();
            economy.AddSite("Kasteel", 100, 1000, 10, 50);
            economy.AddHotel(100, 3, 80);
            var ledger = new Ledger(0);

            var week = economy.Settle(0, ledger, new FixedOutsideConnections().Set(VisitorChannel.Road, 10000));

            Assert.That(week.Demand, Is.EqualTo(700));
            Assert.That(week.Arrivals, Is.EqualTo(700));
            Assert.That(week.DayTrippers, Is.EqualTo(280));
            Assert.That(week.NightsSold, Is.EqualTo(700));
            Assert.That(week.HotelGuests, Is.EqualTo(233));
            Assert.That(week.TurnedAway, Is.EqualTo(420 - 233));
            Assert.That(week.HotelRevenue, Is.EqualTo(700 * 80));
            Assert.That(week.Spending, Is.EqualTo((700 * 60) + (280 * VisitorEconomy.DayTripSpend)));
            Assert.That(week.VisitsWanted, Is.EqualTo(280 + (233 * 3)));
            Assert.That(week.SiteVisits, Is.EqualTo(week.VisitsWanted));
            Assert.That(week.Tickets, Is.EqualTo(week.SiteVisits * 10));
            Assert.That(week.Taxes, Is.EqualTo((700 * VisitorEconomy.TouristTaxPerNight) + (week.Spending / 10)));
            Assert.That(ledger.Income(0, LedgerCategory.Other), Is.EqualTo(week.Tickets));
            Assert.That(ledger.Income(0, LedgerCategory.Taxes), Is.EqualTo(week.Taxes));
            Assert.That(ledger.Expenses(0, LedgerCategory.Upkeep), Is.EqualTo(50));
            Assert.That(week.Reputation, Is.EqualTo(58));
            Assert.That(economy.Reputation, Is.EqualTo(58));
        }

        [Test]
        public void ArrivalsAreLimitedAndSplitOverTheConnections()
        {
            var economy = new VisitorEconomy();
            economy.AddSite("Park", 1000, 5000, 0, 0);
            var connections = new FixedOutsideConnections().Set(VisitorChannel.Road, 100).Set(VisitorChannel.Air, 100);

            var week = economy.Settle(0, new Ledger(0), connections);

            Assert.That(week.Demand, Is.GreaterThan(200));
            Assert.That(week.Arrivals, Is.EqualTo(200));
            Assert.That(week.DayTrippers, Is.EqualTo(40 + 5));
        }

        [Test]
        public void NoConnectionMeansNoVisitorsAndNegativeCapacityCountsAsZero()
        {
            var economy = new VisitorEconomy();
            economy.AddSite("Park", 10, 10, 5, 0);

            Assert.That(economy.Settle(0, new Ledger(0), new FixedOutsideConnections().Set(VisitorChannel.Rail, -4)).Arrivals, Is.Zero);
            Assert.That(economy.Settle(1, new Ledger(0), new FixedOutsideConnections()).SiteVisits, Is.Zero);
        }

        [Test]
        public void TheBestHotelsFillFirst()
        {
            var economy = new VisitorEconomy();
            economy.AddSite("Park", 200, 5000, 0, 0);
            economy.AddHotel(10, 1, 20);
            economy.AddHotel(10, 5, 200);
            var connections = new FixedOutsideConnections().Set(VisitorChannel.Air, 100000);

            var week = economy.Settle(0, new Ledger(0), connections);

            // Twee hotels van 70 bednachten: alle 140 zijn verkocht, het vijfsterrenhotel telt hoger.
            Assert.That(week.NightsSold, Is.EqualTo(140));
            Assert.That(week.HotelRevenue, Is.EqualTo((70 * 200) + (70 * 20)));
            Assert.That(week.Spending, Is.EqualTo((70 * 80) + (70 * 40) + (week.DayTrippers * VisitorEconomy.DayTripSpend)));
        }

        [Test]
        public void SiteCapacityLimitsTheVisitsAndALackOfBedsHurtsTheReputation()
        {
            var economy = new VisitorEconomy();
            economy.AddSite("Klein", 500, 1, 7, 0);

            var week = economy.Settle(0, new Ledger(0), new FixedOutsideConnections().Set(VisitorChannel.Road, 100000));

            Assert.That(week.SiteVisits, Is.EqualTo(7));
            Assert.That(week.HotelGuests, Is.Zero);
            Assert.That(week.TurnedAway, Is.GreaterThan(0));
            Assert.That(economy.Reputation, Is.LessThan(VisitorEconomy.StartReputation));
        }

        [Test]
        public void ReputationRisesWhenEveryoneIsServedAndStaysWithinBounds()
        {
            var economy = new VisitorEconomy();
            economy.AddSite("Park", 10, 1000, 1, 0);
            economy.AddHotel(500, 4, 100);
            var connections = new FixedOutsideConnections().Set(VisitorChannel.Rail, 100000);

            for (var week = 0; week < 40; week++)
            {
                economy.Settle(week, new Ledger(0), connections);
            }

            Assert.That(economy.Reputation, Is.InRange(95, 100));
            Assert.That(economy.WeeklyDemand(), Is.EqualTo(10 * 7 * (50 + economy.Reputation) / 100));
        }

        [Test]
        public void WithoutSitesNothingHappens()
        {
            var economy = new VisitorEconomy();
            economy.AddHotel(10, 2, 50);

            var week = economy.Settle(0, new Ledger(0), new FixedOutsideConnections().Set(VisitorChannel.Road, 1000));

            Assert.That(week.Arrivals, Is.Zero);
            Assert.That(week.Spending, Is.Zero);
            Assert.That(week.Reputation, Is.EqualTo(economy.Reputation));
        }

        [Test]
        public void TheSameInputsGiveTheSameWeeks()
        {
            VisitorWeek Run()
            {
                var economy = new VisitorEconomy();
                economy.AddSite("A", 33, 40, 4, 1);
                economy.AddSite("B", 17, 400, 2, 1);
                economy.AddHotel(37, 2, 60);
                var connections = new FixedOutsideConnections().Set(VisitorChannel.Road, 91).Set(VisitorChannel.Sea, 53);
                VisitorWeek last = null;
                for (var week = 0; week < 5; week++)
                {
                    last = economy.Settle(week, new Ledger(0), connections);
                }

                return last;
            }

            var a = Run();
            var b = Run();

            Assert.That(a.Spending, Is.EqualTo(b.Spending));
            Assert.That(a.SiteVisits, Is.EqualTo(b.SiteVisits));
            Assert.That(a.Reputation, Is.EqualTo(b.Reputation));
        }

        [Test]
        public void SettleRejectsNullsAndTheModelRejectsBadValues()
        {
            var economy = new VisitorEconomy();

            Assert.Throws<ArgumentNullException>(() => economy.Settle(0, null, new FixedOutsideConnections()));
            Assert.Throws<ArgumentNullException>(() => economy.Settle(0, new Ledger(0), null));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddHotel(0, 3, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddHotel(5, 6, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddHotel(5, 3, -1));
            Assert.Throws<ArgumentException>(() => economy.AddSite(" ", 1, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddSite("x", 0, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.AddSite("x", 1, 1, -1, 0));
        }

        [Test]
        public void HotelsAndSitesCanBeRemoved()
        {
            var economy = new VisitorEconomy();
            var hotel = economy.AddHotel(5, 2, 10);
            var site = economy.AddSite("x", 1, 1, 0, 0);

            Assert.That(economy.TotalBeds, Is.EqualTo(5));
            Assert.That(economy.RemoveHotel(hotel), Is.True);
            Assert.That(economy.RemoveHotel(hotel), Is.False);
            Assert.That(economy.RemoveSite(site), Is.True);
            Assert.That(economy.RemoveSite(site), Is.False);
            Assert.That(economy.TotalBeds, Is.Zero);
        }

        [Test]
        public void SaveRoundTripKeepsEverything()
        {
            var economy = new VisitorEconomy();
            economy.AddSite("Kasteel", 100, 1000, 10, 50);
            economy.AddHotel(100, 3, 80);
            economy.RemoveHotel(economy.AddHotel(1, 1, 1));
            economy.Settle(0, new Ledger(0), new FixedOutsideConnections().Set(VisitorChannel.Road, 10000));
            var bytes = SaveBytes.Write(new SaveMetadata(), new VisitorSection(economy));
            var loaded = new VisitorEconomy();
            loaded.AddHotel(9, 1, 1);

            SaveBytes.Read(bytes, new VisitorSection(loaded));

            Assert.That(loaded.Reputation, Is.EqualTo(economy.Reputation));
            Assert.That(loaded.Hotels.Select(h => h.Beds).ToArray(), Is.EqualTo(new[] { 100 }));
            Assert.That(loaded.Sites.Single().Name, Is.EqualTo("Kasteel"));
            Assert.That(loaded.NextHotelId, Is.EqualTo(economy.NextHotelId));
            Assert.That(loaded.NextSiteId, Is.EqualTo(economy.NextSiteId));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new VisitorSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new VisitorSection(economy).Id, Is.EqualTo("VIST"));
            Assert.Throws<ArgumentNullException>(() => new VisitorSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var economy = new VisitorEconomy();
            economy.AddHotel(5, 2, 10);
            var section = new VisitorSection(economy);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(101, 1, 0, 1, 0), 1), "reputatie");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(50, 1, -1, 1, 0), 1), "aantal hotels");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(50, 1, 0, 1, -1), 1), "aantal attracties");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Header(w, 2, 1, 0, 2)), 1), "bed=0");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Header(w, 2, 1, 5, 9)), 1), "sterren");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Header(w, 2, 5, 5, 2)), 1), "volgnummer");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(50);
                w.Write(5);
                w.Write(0);
                w.Write(2);
                w.Write(1);
                w.Write(string.Empty);
                w.Write(1);
                w.Write(1);
                w.Write(1L);
                w.Write(1L);
            }), 1), "naamloze attractie");
            Assert.That(economy.Hotels.Single().Beds, Is.EqualTo(5));
        }

        private static SaveReader Save(int reputation, int nextHotel, int hotels, int nextSite, int sites) => SaveBytes.ReaderFor(w =>
        {
            w.Write(reputation);
            w.Write(nextHotel);
            w.Write(hotels);
            w.Write(nextSite);
            w.Write(sites);
        });

        private static void Header(SaveWriter w, int nextHotel, int hotelId, int beds, int stars)
        {
            w.Write(50);
            w.Write(nextHotel);
            w.Write(1);
            w.Write(hotelId);
            w.Write(beds);
            w.Write(stars);
            w.Write(10L);
            w.Write(1);
            w.Write(0);
        }
    }
}
